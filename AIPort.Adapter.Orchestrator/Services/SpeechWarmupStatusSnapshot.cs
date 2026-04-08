namespace AIPort.Adapter.Orchestrator.Services;

public sealed record SpeechWarmupStatusSnapshot(
    string Status,
    string Provider,
    bool Ready,
    DateTime? LastWarmupAtUtc,
    long? LastWarmupElapsedMs,
    string? Message);