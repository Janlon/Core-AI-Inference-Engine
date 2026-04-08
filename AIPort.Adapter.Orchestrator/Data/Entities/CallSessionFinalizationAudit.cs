namespace AIPort.Adapter.Orchestrator.Data.Entities;

public sealed record CallSessionFinalizationAudit(
    string FinalAction,
    string FinalExtractedData,
    DateTime EndedAt,
    string? FinalReasonCode,
    string? FinalReasonCategory,
    string? FinalReasonMessage,
    int? WebhookHttpStatus,
    string? WebhookPayloadHash,
    DateTime? WebhookPayloadSentAt,
    string? WebhookCorrelationId,
    string? WebhookCorrelationField);