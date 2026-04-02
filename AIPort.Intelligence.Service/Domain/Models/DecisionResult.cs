using AIPort.Intelligence.Service.Domain.Enums;

namespace AIPort.Intelligence.Service.Domain.Models;

/// <summary>
/// Contrato de saída do motor de decisão.
/// Representa uma decisão completa e pronta para ser consumida pelo Orquestrador.
/// </summary>
public record DecisionResult
{
    /// <summary>Intenção principal identificada no texto do visitante.</summary>
    public required Intencao Intencao { get; init; }

    /// <summary>Entidades extraídas da fala do visitante.</summary>
    public required DadosExtraidos DadosExtraidos { get; init; }

    /// <summary>Texto a ser sintetizado pelo sistema de voz para o visitante.</summary>
    public required string RespostaTexto { get; init; }

    /// <summary>Comando estruturado a ser executado pelo Orquestrador.</summary>
    public required AcaoSugerida AcaoSugerida { get; init; }

    /// <summary>Score de confiança geral da decisão (0.0 – 1.0).</summary>
    public double Confianca { get; init; }

    /// <summary>Camada de processamento que resolveu a decisão (Regex, NLP ou LLM).</summary>
    public required string CamadaResolucao { get; init; }

    /// <summary>Tipo do tenant usado para aplicar as regras de fluxo.</summary>
    public string? TenantType { get; init; }
}
