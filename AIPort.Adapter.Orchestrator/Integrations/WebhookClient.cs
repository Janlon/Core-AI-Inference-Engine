using System.Net.Http.Json;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using AIPort.Adapter.Orchestrator.Domain.Models;
using AIPort.Adapter.Orchestrator.Integrations.Interfaces;

namespace AIPort.Adapter.Orchestrator.Integrations;

public sealed class WebhookClient : IWebhookClient
{
    private readonly HttpClient _http;
    private readonly ILogger<WebhookClient> _logger;

    public WebhookClient(HttpClient http, ILogger<WebhookClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<WebhookDeliveryResult> SendNotificationAsync(string url, string? token, object payload, CancellationToken ct = default)
    {
        var timer = Stopwatch.StartNew();
        var payloadJson = JsonSerializer.Serialize(payload);
        var payloadHash = ComputeSha256(payloadJson);
        var payloadSentAtUtc = DateTime.UtcNow;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(payload)
            };

            if (!string.IsNullOrWhiteSpace(token))
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            using var response = await _http.SendAsync(request, ct);
            var responseBody = await SafeReadBodyAsync(response, ct);
            var correlation = TryExtractCorrelation(responseBody);
            timer.Stop();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Webhook retornou status {StatusCode}", response.StatusCode);
                return new WebhookDeliveryResult(
                    Success: false,
                    Code: $"WEBHOOK_HTTP_{(int)response.StatusCode}",
                    Category: "http",
                    Message: $"Webhook retornou HTTP {(int)response.StatusCode}.",
                    HttpStatusCode: (int)response.StatusCode,
                    ResponseBodyExcerpt: responseBody,
                    ElapsedMs: timer.ElapsedMilliseconds,
                    PayloadHash: payloadHash,
                    PayloadSentAtUtc: payloadSentAtUtc,
                    CorrelationId: correlation.Id,
                    CorrelationField: correlation.Field);
            }

            if (TryInterpretNegativeResponse(responseBody, out var negativeMessage))
            {
                _logger.LogWarning("Webhook retornou resposta negativa em payload 2xx: {Message}", negativeMessage);
                return new WebhookDeliveryResult(
                    Success: false,
                    Code: "WEBHOOK_NEGATIVE_RESPONSE",
                    Category: "business",
                    Message: negativeMessage,
                    HttpStatusCode: (int)response.StatusCode,
                    ResponseBodyExcerpt: responseBody,
                    ElapsedMs: timer.ElapsedMilliseconds,
                    PayloadHash: payloadHash,
                    PayloadSentAtUtc: payloadSentAtUtc,
                    CorrelationId: correlation.Id,
                    CorrelationField: correlation.Field);
            }

            return new WebhookDeliveryResult(
                Success: true,
                Code: "WEBHOOK_DELIVERED",
                Category: "success",
                Message: "Webhook entregue com sucesso.",
                HttpStatusCode: (int)response.StatusCode,
                ResponseBodyExcerpt: responseBody,
                ElapsedMs: timer.ElapsedMilliseconds,
                PayloadHash: payloadHash,
                PayloadSentAtUtc: payloadSentAtUtc,
                CorrelationId: correlation.Id,
                CorrelationField: correlation.Field);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            timer.Stop();
            _logger.LogWarning(ex, "Timeout ao enviar webhook para {Url}", url);
            return new WebhookDeliveryResult(
                Success: false,
                Code: "WEBHOOK_TIMEOUT",
                Category: "timeout",
                Message: "Webhook excedeu o tempo limite configurado.",
                HttpStatusCode: null,
                ResponseBodyExcerpt: null,
                ElapsedMs: timer.ElapsedMilliseconds,
                PayloadHash: payloadHash,
                PayloadSentAtUtc: payloadSentAtUtc,
                CorrelationId: null,
                CorrelationField: null);
        }
        catch (HttpRequestException ex)
        {
            timer.Stop();
            _logger.LogWarning(ex, "Falha HTTP ao enviar webhook para {Url}", url);
            return new WebhookDeliveryResult(
                Success: false,
                Code: "WEBHOOK_TRANSPORT_ERROR",
                Category: "transport",
                Message: ex.Message,
                HttpStatusCode: (int?)ex.StatusCode,
                ResponseBodyExcerpt: null,
                ElapsedMs: timer.ElapsedMilliseconds,
                PayloadHash: payloadHash,
                PayloadSentAtUtc: payloadSentAtUtc,
                CorrelationId: null,
                CorrelationField: null);
        }
        catch (Exception ex)
        {
            timer.Stop();
            _logger.LogWarning(ex, "Falha ao enviar webhook para {Url}", url);
            return new WebhookDeliveryResult(
                Success: false,
                Code: "WEBHOOK_UNEXPECTED_ERROR",
                Category: "unexpected",
                Message: ex.Message,
                HttpStatusCode: null,
                ResponseBodyExcerpt: null,
                ElapsedMs: timer.ElapsedMilliseconds,
                PayloadHash: payloadHash,
                PayloadSentAtUtc: payloadSentAtUtc,
                CorrelationId: null,
                CorrelationField: null);
        }
    }

    private static string ComputeSha256(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes);
    }

    private static (string? Id, string? Field) TryExtractCorrelation(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return (null, null);

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return (null, null);

            var root = doc.RootElement;
            foreach (var field in new[] { "requestId", "request_id", "correlationId", "correlation_id", "protocol", "protocolId", "protocol_id", "protocolo", "ticket", "attendanceId", "atendimentoId" })
            {
                if (!root.TryGetProperty(field, out var property) || property.ValueKind != JsonValueKind.String)
                    continue;

                var value = property.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    return (value, field);
            }
        }
        catch
        {
        }

        return (null, null);
    }

    private static async Task<string?> SafeReadBodyAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.Content is null)
            return null;

        var body = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(body))
            return null;

        return body.Length <= 2000 ? body : body[..2000];
    }

    private static bool TryInterpretNegativeResponse(string? responseBody, out string message)
    {
        message = "Webhook retornou resposta negativa do destinatario.";

        if (string.IsNullOrWhiteSpace(responseBody))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            var root = doc.RootElement;
            if (TryGetBoolean(root, out var boolMessage, "success", "approved", "accepted", "authorized", "allow", "permitted", "granted") == false)
            {
                message = boolMessage ?? message;
                return true;
            }

            if (TryGetNegativeStatus(root, out var statusMessage))
            {
                message = statusMessage ?? message;
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static bool? TryGetBoolean(JsonElement root, out string? message, params string[] names)
    {
        message = TryGetString(root, "message", "reason", "detail", "descricao", "descricaoRetorno", "motivo");

        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var property))
                continue;

            if (property.ValueKind == JsonValueKind.False)
                return false;

            if (property.ValueKind == JsonValueKind.True)
                return true;
        }

        return null;
    }

    private static bool TryGetNegativeStatus(JsonElement root, out string? message)
    {
        message = null;
        var value = TryGetString(root, "status", "result", "decision", "outcome", "response", "state");
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Trim().ToLowerInvariant();
        var negatives = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "rejected", "denied", "negative", "busy", "no_answer", "no-answer", "not_answered", "not-answered",
            "unavailable", "refused", "declined", "timeout", "nao_autorizado", "nao_autorizada", "negado", "recusado",
            "ocupado", "sem_resposta", "sem-resposta"
        };

        if (!negatives.Contains(normalized))
            return false;

        message = TryGetString(root, "message", "reason", "detail", "descricao", "motivo") ?? $"Webhook retornou status negativo '{value}'.";
        return true;
    }

    private static string? TryGetString(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
                continue;

            var value = property.GetString();
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }
}
