using AIPort.Adapter.Orchestrator.Data.Entities;

namespace AIPort.Adapter.Orchestrator.Data.Repositories;

public interface ITenantRepository
{
    Task<IReadOnlyList<Tenant>> ListAsync(bool includeInactive = true, CancellationToken ct = default);
    Task<Tenant?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Tenant?> GetByPidAsync(int pid, CancellationToken ct = default);
    Task<int> CreateAsync(Tenant tenant, CancellationToken ct = default);
    Task<bool> UpdateAsync(Tenant tenant, CancellationToken ct = default);
    Task<bool> DeactivateAsync(int id, CancellationToken ct = default);
}
