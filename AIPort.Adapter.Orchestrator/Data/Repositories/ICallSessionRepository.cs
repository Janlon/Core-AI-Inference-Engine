using AIPort.Adapter.Orchestrator.Data.Entities;

namespace AIPort.Adapter.Orchestrator.Data.Repositories;

public interface ICallSessionRepository
{
    Task CreateSessionAsync(CallSession session, CancellationToken ct = default);
    Task<long> InsertInteractionAsync(CallInteraction interaction, CancellationToken ct = default);
    Task<int> GetNextInteractionOrderAsync(string sessionId, CancellationToken ct = default);
    Task CompleteSessionAsync(string sessionId, CallSessionFinalizationAudit audit, CancellationToken ct = default);
    Task<long> CountActiveSessionsAsync(CancellationToken ct = default);
}
