namespace AIPort.Adapter.Orchestrator.Config;

public sealed class IntelligenceServiceOptions
{
    public const string SectionName = "IntelligenceService";

    public string BaseUrl { get; set; } = "http://127.0.0.1:5037";
    public string ProcessPath { get; set; } = "/api/Inference/process";
    public int TimeoutMs { get; set; } = 45000;
    public int MaxRetryAttempts { get; set; } = 3;
    public int RetryBaseDelayMs { get; set; } = 200;
}
