namespace AIPort.Adapter.Orchestrator.Agi.Models;

public sealed record ActiveAgiChannelSnapshot(
    string ConnectionId,
    DateTime ConnectedAtUtc,
    string? RemoteEndpoint,
    string? SessionId,
    string? CallerId,
    string? Channel,
    int? TenantPid);