namespace AIPort.Adapter.Orchestrator.Data.Entities;

public sealed class Tenant
{
    public int Id { get; set; }
    public int Pid { get; set; }
    public required string NomeIdentificador { get; set; }
    public required string TipoLocal { get; set; }
    public required string SystemType { get; set; }
    public string? WebhookUrl { get; set; }
    public string? ApiToken { get; set; }
    public string? SipTrunkPrefix { get; set; }
    public string? RamalTransfHumano { get; set; }
    public bool UsaBloco { get; set; }
    public bool UsaTorre { get; set; }
    public bool RecordingEnabled { get; set; }
    public string AiProfile { get; set; } = "CONSERVADOR";
    public double? AiRegexConfidenceThreshold { get; set; }
    public double? AiNlpConfidenceThreshold { get; set; }
    public double? AiGlobalConfidenceThreshold { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
