namespace AIPort.Adapter.Orchestrator.Integrations.Interfaces;

public interface IWebhookClient
{
    Task<bool> SendNotificationAsync(string url, string? token, object payload, CancellationToken ct = default);
}
