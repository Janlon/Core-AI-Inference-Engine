using AIPort.Adapter.Orchestrator.Data.Entities;
using AIPort.Adapter.Orchestrator.Domain.Models;

namespace AIPort.Adapter.Orchestrator.Services.Interfaces;

public interface IDecisionExecutor
{
    Task<(string AcaoExecutada, string RespostaFalada)> ExecuteAsync(
        AgiCallContext call,
        Tenant tenant,
        InferenceResponseDto iaResponse,
        CancellationToken ct = default);
}
