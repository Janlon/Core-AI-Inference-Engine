using AIPort.Adapter.Orchestrator.Domain.Models;

namespace AIPort.Adapter.Orchestrator.Integrations.Interfaces;

public interface IWebhookClient
{
    Task<WebhookDeliveryResult> SendNotificationAsync(string url, string? token, object payload, CancellationToken ct = default);
}
