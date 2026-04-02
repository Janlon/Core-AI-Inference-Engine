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

    public async Task<bool> NotifyAsync(AgiCallContext call, Tenant tenant, InferenceResponseDto iaResponse, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(tenant.WebhookUrl))
        {
            var ok = await _webhookClient.SendNotificationAsync(tenant.WebhookUrl, tenant.ApiToken, new
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

            if (ok)
                return true;

            _logger.LogWarning("Webhook primário falhou para Tenant={TenantId}.", tenant.Id);
        }

        _logger.LogWarning("Sem webhook válido ou falha no envio. Fallback para operador humano configurado.");
        return false;
    }
}
