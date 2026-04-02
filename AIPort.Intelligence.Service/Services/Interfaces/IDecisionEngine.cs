using AIPort.Intelligence.Service.Domain.Models;

namespace AIPort.Intelligence.Service.Services.Interfaces;

/// <summary>
/// Motor de decisão central — orquestra as três camadas de processamento
/// (Regex → NLP → LLM) e devolve uma decisão estruturada.
/// </summary>
public interface IDecisionEngine
{
    /// <summary>
    /// Processa o texto do visitante e retorna uma decisão completa.
    /// </summary>
    /// <param name="request">Requisição com texto e contexto do tenant.</param>
    /// <param name="ct">Token de cancelamento.</param>
    Task<DecisionResult> ProcessAsync(InferenceRequest request, CancellationToken ct = default);
}
