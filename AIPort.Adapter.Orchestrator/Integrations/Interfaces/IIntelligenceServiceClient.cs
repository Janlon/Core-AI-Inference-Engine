using AIPort.Adapter.Orchestrator.Domain.Models;

namespace AIPort.Adapter.Orchestrator.Integrations.Interfaces;

public interface IIntelligenceServiceClient
{
    Task<InferenceResponseDto> ProcessAsync(InferenceRequestDto request, CancellationToken ct = default);

    /// <summary>
    /// Busca os templates de resposta de voz do Intelligence Service para o tipo de tenant informado.
    /// Retorna o DTO com valores padrão caso o tenant não seja encontrado ou o serviço esteja indisponível.
    /// </summary>
    Task<TenantResponsesDto> GetTenantResponsesAsync(string tenantType, CancellationToken ct = default);
}
