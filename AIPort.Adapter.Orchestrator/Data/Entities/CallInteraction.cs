namespace AIPort.Adapter.Orchestrator.Data.Entities;

public sealed class CallInteraction
{
    public long Id { get; set; }
    public required string SessionId { get; set; }
    public int InteractionOrder { get; set; }
    public required string BotPrompt { get; set; }
    public string? UserTranscription { get; set; }
    public string? ResolutionLayer { get; set; }
    public string? ExtractedDataJson { get; set; }
    public long InteractionDurationMs { get; set; }
    public long LlmProcessingTimeMs { get; set; }
    public DateTime CreatedAt { get; set; }
}
