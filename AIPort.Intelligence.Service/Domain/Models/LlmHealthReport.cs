namespace AIPort.Intelligence.Service.Domain.Models;

public sealed record LlmHealthReport(
    string Status,
    bool AnyProviderEnabled,
    bool AnyProviderHealthy,
    DateTime CheckedAtUtc,
    IReadOnlyList<LlmProviderHealthStatus> Providers);
