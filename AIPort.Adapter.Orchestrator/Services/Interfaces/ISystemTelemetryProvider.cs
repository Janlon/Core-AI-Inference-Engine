namespace AIPort.Adapter.Orchestrator.Services.Interfaces;

/// <summary>
/// Fornece métricas básicas do host para o health check.
/// </summary>
public interface ISystemTelemetryProvider
{
    Task<SystemTelemetrySnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}