namespace AIPort.Adapter.Orchestrator.Config;

public sealed class WebhookOptions
{
    public const string SectionName = "Webhook";

    public int TimeoutMs { get; set; } = 5000;
    public int MaxRetryAttempts { get; set; } = 3;
    public int RetryBaseDelayMs { get; set; } = 200;
}
