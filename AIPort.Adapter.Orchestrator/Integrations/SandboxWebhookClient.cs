using System.Text.Json;
using AIPort.Adapter.Orchestrator.Domain.Models;
using AIPort.Adapter.Orchestrator.Integrations.Interfaces;

namespace AIPort.Adapter.Orchestrator.Integrations;

public sealed class SandboxWebhookClient : IWebhookClient
{
    private readonly ILogger<SandboxWebhookClient> _logger;

    public SandboxWebhookClient(ILogger<SandboxWebhookClient> logger)
    {
        _logger = logger;
    }

    public Task<WebhookDeliveryResult> SendNotificationAsync(string url, string? token, object payload, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[SANDBOX] Webhook suprimido. Url={Url} TokenPresent={HasToken} Payload={Payload}",
            url,
            !string.IsNullOrWhiteSpace(token),
            JsonSerializer.Serialize(payload));

        return Task.FromResult(new WebhookDeliveryResult(
            Success: true,
            Code: "WEBHOOK_SANDBOX_SUPPRESSED",
            Category: "sandbox",
            Message: "Webhook suprimido pelo modo sandbox.",
            HttpStatusCode: null,
            ResponseBodyExcerpt: null,
            ElapsedMs: 0,
            PayloadHash: null,
            PayloadSentAtUtc: DateTime.UtcNow,
            CorrelationId: null,
            CorrelationField: null));
    }
}