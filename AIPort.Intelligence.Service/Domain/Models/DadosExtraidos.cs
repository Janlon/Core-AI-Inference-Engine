namespace AIPort.Intelligence.Service.Domain.Models;

/// <summary>
/// Dados estruturados extraídos do texto do visitante.
/// </summary>
public record DadosExtraidos
{
    /// <summary>Nome completo da pessoa identificada.</summary>
    public string? Nome { get; init; }

    /// <summary>Nome do visitante (alias explícito para integração com orquestradores).</summary>
    public string? NomeVisitante { get; init; }

    /// <summary>Número de documento (CPF, RG, CNH, CRM, COREN etc.).</summary>
    public string? Documento { get; init; }

    /// <summary>CPF do visitante quando identificado no texto.</summary>
    public string? Cpf { get; init; }

    /// <summary>Número da unidade, apartamento ou sala de destino.</summary>
    public string? Unidade { get; init; }

    /// <summary>Bloco da unidade (ex.: 12, B, Torre A).</summary>
    public string? Bloco { get; init; }

    /// <summary>Torre da unidade (ex.: A, Norte, 2).</summary>
    public string? Torre { get; init; }

    /// <summary>Nome da empresa mencionada pelo visitante.</summary>
    public string? Empresa { get; init; }

    /// <summary>Parentesco informado com o morador/responsável.</summary>
    public string? Parentesco { get; init; }

    /// <summary>Indica se o visitante informou estar com veículo.</summary>
    public bool EstaComVeiculo { get; init; }

    /// <summary>Placa do veículo quando identificada no texto.</summary>
    public string? Placa { get; init; }

    /// <summary>Indica se o visitante se identificou como entregador.</summary>
    public bool EEntregador { get; init; }

    /// <summary>Retorna true quando ao menos um campo foi preenchido.</summary>
    public bool TemDadosExtraidos =>
        Nome is not null || NomeVisitante is not null || Documento is not null ||
        Cpf is not null || Unidade is not null || Bloco is not null || Torre is not null ||
        Empresa is not null || Parentesco is not null || Placa is not null ||
        EstaComVeiculo || EEntregador;
}
