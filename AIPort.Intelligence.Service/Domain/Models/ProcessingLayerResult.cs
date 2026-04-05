using AIPort.Intelligence.Service.Domain.Enums;

namespace AIPort.Intelligence.Service.Domain.Models;

/// <summary>
/// Resultado intermediário retornado por cada camada de processamento.
/// </summary>
public record ProcessingLayerResult
{
    /// <summary>Score de confiança desta camada (0.0 – 1.0).</summary>
    public double Confianca { get; init; }

    /// <summary>Intenção identificada, ou null se não determinada.</summary>
    public Intencao? Intencao { get; init; }

    /// <summary>Dados parcialmente extraídos pelo processador.</summary>
    public DadosExtraidos DadosExtraidos { get; init; } = new();

    /// <summary>Texto de resposta gerado (preenchido principalmente pela camada LLM).</summary>
    public string? RespostaTexto { get; init; }

    /// <summary>Ação sugerida, ou null se não determinada.</summary>
    public AcaoSugerida? AcaoSugerida { get; init; }

    /// <summary>Nome identificador da camada que gerou este resultado.</summary>
    public required string Camada { get; init; }

    /// <summary>Metadados opcionais de depuração gerados pela camada.</summary>
    public DecisionDebugInfo? Debug { get; init; }
}
