using AIPort.Intelligence.Service.Domain.Enums;

namespace AIPort.Intelligence.Service.Domain.Models;

/// <summary>
/// Estado acumulado de processamento entre as camadas do motor de decisão.
/// Passado de camada em camada (Regex → NLP → LLM) para evitar reprocessamento.
/// </summary>
public sealed class ProcessingState
{
    public ProcessingState(string texto, string tenantType)
    {
        Texto = texto;
        TenantType = tenantType;
    }

    public string Texto { get; }
    public string TenantType { get; }

    // --- Dados progressivamente acumulados ---
    public string? NomeDetectado { get; set; }
    public string? NomeVisitanteDetectado { get; set; }
    public string? DocumentoDetectado { get; set; }
    public string? CpfDetectado { get; set; }
    public string? UnidadeDetectada { get; set; }
    public string? BlocoDetectado { get; set; }
    public string? TorreDetectada { get; set; }
    public string? EmpresaDetectada { get; set; }
    public string? ParentescoDetectado { get; set; }
    public bool? EstaComVeiculoDetectado { get; set; }
    public string? PlacaDetectada { get; set; }
    public bool? EEntregadorDetectado { get; set; }

    /// <summary>Melhor confiança obtida até ao momento.</summary>
    public double MelhorConfianca { get; set; }

    /// <summary>Intenção identificada (pode ser null se ainda não resolvida).</summary>
    public Intencao? Intencao { get; set; }

    /// <summary>
    /// Retorna os dados extraídos até o momento como um record imutável.
    /// </summary>
    public DadosExtraidos ToDadosExtraidos() => new()
    {
        Nome = NomeDetectado,
        NomeVisitante = NomeVisitanteDetectado ?? NomeDetectado,
        Documento = DocumentoDetectado,
        Cpf = CpfDetectado,
        Unidade = UnidadeDetectada,
        Bloco = BlocoDetectado,
        Torre = TorreDetectada,
        Empresa = EmpresaDetectada,
        Parentesco = ParentescoDetectado,
        EstaComVeiculo = EstaComVeiculoDetectado ?? false,
        Placa = PlacaDetectada,
        EEntregador = EEntregadorDetectado ?? false
    };
}
