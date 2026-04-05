namespace AIPort.Adapter.Orchestrator.Domain.Models;

public sealed record CallOrchestrationResult
{
    public required string SessionId { get; init; }
    public required string AcaoExecutada { get; init; }
    public required string RespostaFalada { get; init; }
    public required bool Sucesso { get; init; }
    public string? MotivoFalha { get; init; }
    public string? Intencao { get; init; }
    public string? CamadaResolucao { get; init; }
    public double? Confianca { get; init; }
    public DadosExtraidosDto? DadosExtraidos { get; init; }
    public DecisionDebugInfoDto? Debug { get; init; }
}
