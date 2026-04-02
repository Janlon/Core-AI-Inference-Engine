namespace AIPort.Adapter.Orchestrator.Services;

public sealed record DatabaseHealthResult(
    bool IsReachable,
    long ActiveTenantsCount,
    int MinActiveTenants,
    bool HasMinimumActiveTenants,
    string Status,
    string? Message
);
