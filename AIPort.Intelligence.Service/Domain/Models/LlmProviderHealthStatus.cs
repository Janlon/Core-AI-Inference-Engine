namespace AIPort.Intelligence.Service.Domain.Models;

public sealed record LlmProviderHealthStatus(
    string Name,
    string ServiceType,
    bool IsEnabled,
    bool IsHealthy,
    int? HttpStatusCode,
    string Status,
    string Message,
    long? ElapsedMs,
    string? Endpoint,
    string? Model,
    DateTime CheckedAtUtc);
