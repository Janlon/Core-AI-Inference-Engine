using AIPort.Adapter.Orchestrator.Domain.Models;

namespace AIPort.Adapter.Orchestrator.Services.Interfaces;

public interface IOrchestrationService
{
    Task<CallOrchestrationResult> HandleCallAsync(AgiCallContext call, CancellationToken ct = default);
}
