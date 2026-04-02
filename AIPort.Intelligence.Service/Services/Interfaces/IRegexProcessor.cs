using AIPort.Intelligence.Service.Domain.Models;

namespace AIPort.Intelligence.Service.Services.Interfaces;

/// <summary>
/// Camada 1 — Processador Regex.
/// Tenta identificar padrões comuns (CPF, unidade, saudações) sem invocar
/// modelos pesados. Deve ser extremamente rápido (&lt; 5 ms).
/// </summary>
public interface IRegexProcessor
{
    /// <summary>
    /// Executa o processamento baseado em Regex sobre o texto fornecido.
    /// </summary>
    /// <param name="texto">Texto bruto do visitante.</param>
    /// <param name="ct">Token de cancelamento.</param>
    /// <returns>Resultado parcial com dados extraídos e score de confiança.</returns>
    Task<ProcessingLayerResult> ProcessAsync(string texto, CancellationToken ct = default);
}
