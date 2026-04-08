namespace AIPort.Adapter.Orchestrator.Data.Entities;

public sealed class CallSession
{
    public required string SessionId { get; set; }
    public int TenantId { get; set; }
    public string? CallerId { get; set; }
    public string? Channel { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public string? FinalAction { get; set; }
    public required string FinalExtractedData { get; set; }
    public string? FinalReasonCode { get; set; }
    public string? FinalReasonCategory { get; set; }
    public string? FinalReasonMessage { get; set; }
    public int? WebhookHttpStatus { get; set; }
    public string? WebhookPayloadHash { get; set; }
    public DateTime? WebhookPayloadSentAt { get; set; }
    public string? WebhookCorrelationId { get; set; }
    public string? WebhookCorrelationField { get; set; }
}
