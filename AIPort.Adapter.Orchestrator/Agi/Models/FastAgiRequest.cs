namespace AIPort.Adapter.Orchestrator.Agi.Models;

public sealed class FastAgiRequest
{
    public string UniqueId { get; init; } = string.Empty;
    public string CallerId { get; init; } = string.Empty;
    public string Channel { get; init; } = string.Empty;
    public string CalledNumber { get; init; } = string.Empty;
    public string Context { get; init; } = string.Empty;
    public int TenantPid { get; init; }
    public string? AudioFilePath { get; init; }
    public string? PreTranscribedText { get; init; }
    public IReadOnlyDictionary<string, string> Variables { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
