namespace AIPort.Adapter.Orchestrator.Domain.Models;

public sealed record InferenceResponseDto
{
    public required string Intencao { get; init; }
    public required DadosExtraidosDto DadosExtraidos { get; init; }
    public required string RespostaTexto { get; init; }
    public required string AcaoSugerida { get; init; }
    public double Confianca { get; init; }
    public required string CamadaResolucao { get; init; }
    public string? TenantType { get; init; }
}

public sealed record DadosExtraidosDto
{
    public string? Nome { get; init; }
    public string? NomeVisitante { get; init; }
    public string? Documento { get; init; }
    public string? Cpf { get; init; }
    public string? Unidade { get; init; }
    public string? Bloco { get; init; }
    public string? Torre { get; init; }
    public string? Empresa { get; init; }
    public string? Parentesco { get; init; }
    public bool EstaComVeiculo { get; init; }
    public string? Placa { get; init; }
    public bool EEntregador { get; init; }
    public bool TemDadosExtraidos { get; init; }
}
