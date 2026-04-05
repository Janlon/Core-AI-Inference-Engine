using System.Text.Json;
using AIPort.Adapter.Orchestrator.Integrations.Interfaces;

namespace AIPort.Adapter.Orchestrator.Integrations;

public sealed class SandboxWebhookClient : IWebhookClient
{
    private readonly ILogger<SandboxWebhookClient> _logger;

    public SandboxWebhookClient(ILogger<SandboxWebhookClient> logger)
    {
        _logger = logger;
    }

    public Task<bool> SendNotificationAsync(string url, string? token, object payload, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[SANDBOX] Webhook suprimido. Url={Url} TokenPresent={HasToken} Payload={Payload}",
            url,
            !string.IsNullOrWhiteSpace(token),
            JsonSerializer.Serialize(payload));

        return Task.FromResult(true);
    }
}