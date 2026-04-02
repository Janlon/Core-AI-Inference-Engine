using AIPort.Adapter.Orchestrator.Data.Entities;
using AIPort.Adapter.Orchestrator.Domain.Models;

namespace AIPort.Adapter.Orchestrator.Services.Interfaces;

public interface INotificationCascadeService
{
    Task<bool> NotifyAsync(AgiCallContext call, Tenant tenant, InferenceResponseDto iaResponse, CancellationToken ct = default);
}
