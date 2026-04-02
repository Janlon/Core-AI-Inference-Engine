using AIPort.Intelligence.Service.Domain.Models;
using AIPort.Intelligence.Service.Domain.Rules;

namespace AIPort.Intelligence.Service.Services.Interfaces;

/// <summary>
/// Camada 3 — Processador LLM via Semantic Kernel.
/// Acionado somente quando as camadas anteriores não atingiram confiança suficiente.
/// Suporta múltiplos provedores (AzureOpenAI, OpenAI, Ollama) com fallback automático,
/// todos configurados via appsettings.json.
/// </summary>
public interface ILlmProcessor
{
    /// <summary>
    /// Envia o texto ao LLM com contexto acumulado e regras do tenant,
    /// retornando uma decisão completa e estruturada.
    /// </summary>
    /// <param name="texto">Texto bruto do visitante.</param>
    /// <param name="estadoAtual">Estado parcial das camadas anteriores.</param>
    /// <param name="regrasTenant">Regras do tenant para injetar no prompt.</param>
    /// <param name="ct">Token de cancelamento.</param>
    Task<ProcessingLayerResult> ProcessAsync(
        string texto,
        ProcessingState estadoAtual,
        TenantRule? regrasTenant,
        CancellationToken ct = default);
}
