namespace AIPort.Adapter.Orchestrator.Services;

public sealed record SystemTelemetrySnapshot(
    string Status,
    string Platform,
    DateTime SampledAtUtc,
    int LogicalCores,
    double? CpuUsagePercent,
    long? TotalMemoryBytes,
    long? UsedMemoryBytes,
    long? AvailableMemoryBytes,
    double? MemoryUsagePercent,
    string? Message);