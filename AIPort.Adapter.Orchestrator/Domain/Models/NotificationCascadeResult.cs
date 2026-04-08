namespace AIPort.Adapter.Orchestrator.Domain.Models;

public sealed record NotificationCascadeResult(
    bool Success,
    string ReasonCode,
    string ReasonCategory,
    string ReasonMessage,
    WebhookDeliveryResult? Webhook,
    bool RedirectToHumanRecommended);