namespace AIPort.Adapter.Orchestrator.Domain.Models;

public sealed record WebhookDeliveryResult(
    bool Success,
    string Code,
    string Category,
    string Message,
    int? HttpStatusCode,
    string? ResponseBodyExcerpt,
    long? ElapsedMs,
    string? PayloadHash,
    DateTime? PayloadSentAtUtc,
    string? CorrelationId,
    string? CorrelationField);