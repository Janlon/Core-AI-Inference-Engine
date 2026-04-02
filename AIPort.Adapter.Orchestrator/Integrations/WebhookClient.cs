using System.Net.Http.Json;
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

    public async Task<bool> SendNotificationAsync(string url, string? token, object payload, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(payload)
            };

            if (!string.IsNullOrWhiteSpace(token))
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            using var response = await _http.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Webhook retornou status {StatusCode}", response.StatusCode);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao enviar webhook para {Url}", url);
            return false;
        }
    }
}
