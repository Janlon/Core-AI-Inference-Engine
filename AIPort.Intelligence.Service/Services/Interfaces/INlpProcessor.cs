using AIPort.Intelligence.Service.Domain.Models;

namespace AIPort.Intelligence.Service.Services.Interfaces;

/// <summary>
/// Camada 2 — Processador NLP/NER.
/// Usa um modelo de reconhecimento de entidades nomeadas para extrair
/// Nomes e Empresas que Regex não consegue capturar de forma confiável.
/// Implementação padrão: Catalyst (pure .NET). Pode ser substituída por
/// Python.NET + spaCy via troca de registro no DI sem alterar contratos.
/// </summary>
public interface INlpProcessor
{
    /// <summary>
    /// Executa NER sobre o texto, enriquecendo o estado acumulado das camadas anteriores.
    /// </summary>
    /// <param name="texto">Texto bruto do visitante.</param>
    /// <param name="estadoAtual">Estado parcial vindo da camada Regex.</param>
    /// <param name="ct">Token de cancelamento.</param>
    Task<ProcessingLayerResult> ProcessAsync(
        string texto,
        ProcessingState estadoAtual,
        CancellationToken ct = default);
}
