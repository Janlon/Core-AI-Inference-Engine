using AIPort.Adapter.Orchestrator.Data.Entities;
using AIPort.Adapter.Orchestrator.Domain.Models;
using AIPort.Adapter.Orchestrator.Integrations.Interfaces;
using AIPort.Adapter.Orchestrator.Services.Interfaces;

namespace AIPort.Adapter.Orchestrator.Services;

/// <summary>
/// Cascata inicial de notificação: Webhook -> fallback humano (ramal).
/// </summary>
public sealed class NotificationCascadeService : INotificationCascadeService
{
    private readonly IWebhookClient _webhookClient;
    private readonly ILogger<NotificationCascadeService> _logger;

    public NotificationCascadeService(IWebhookClient webhookClient, ILogger<NotificationCascadeService> logger)
    {
        _webhookClient = webhookClient;
        _logger = logger;
    }

    public async Task<NotificationCascadeResult> NotifyAsync(AgiCallContext call, Tenant tenant, InferenceResponseDto iaResponse, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tenant.WebhookUrl))
        {
            _logger.LogWarning("Tenant={TenantId} sem webhook configurado. Fallback para operador humano.", tenant.Id);
            return new NotificationCascadeResult(
                Success: false,
                ReasonCode: "WEBHOOK_NOT_CONFIGURED",
                ReasonCategory: "configuration",
                ReasonMessage: "Tenant sem webhook configurado para notificar o morador.",
                Webhook: null,
                RedirectToHumanRecommended: true);
        }

        var webhook = await _webhookClient.SendNotificationAsync(tenant.WebhookUrl, tenant.ApiToken, new
        {
            tenantId = tenant.Id,
            tenantPid = tenant.Pid,
            sessionId = call.SessionId,
            uniqueId = call.UniqueId,
            callerId = call.CallerId,
            acao = iaResponse.AcaoSugerida,
            resposta = iaResponse.RespostaTexto,
            dados = iaResponse.DadosExtraidos
        }, ct);

        if (webhook.Success)
        {
            return new NotificationCascadeResult(
                Success: true,
                ReasonCode: "MORADOR_NOTIFICADO",
                ReasonCategory: "success",
                ReasonMessage: "Morador notificado com sucesso via webhook.",
                Webhook: webhook,
                RedirectToHumanRecommended: false);
        }

        _logger.LogWarning(
            "Webhook primário falhou para Tenant={TenantId}. Code={Code} HttpStatus={HttpStatusCode}",
            tenant.Id,
            webhook.Code,
            webhook.HttpStatusCode);

        return new NotificationCascadeResult(
            Success: false,
            ReasonCode: webhook.Code,
            ReasonCategory: webhook.Category,
            ReasonMessage: webhook.Message,
            Webhook: webhook,
            RedirectToHumanRecommended: true);
    }
}
