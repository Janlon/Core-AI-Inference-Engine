using AIPort.Adapter.Orchestrator.Data.Entities;
using AIPort.Adapter.Orchestrator.Domain.Models;
using AIPort.Adapter.Orchestrator.Integrations.Interfaces;
using AIPort.Adapter.Orchestrator.Services.Interfaces;

namespace AIPort.Adapter.Orchestrator.Services;

public sealed class DecisionExecutor : IDecisionExecutor
{
    private readonly IAsteriskCommandClient _agi;
    private readonly ITextToSpeechService _tts;
    private readonly INotificationCascadeService _cascade;

    public DecisionExecutor(
        IAsteriskCommandClient agi,
        ITextToSpeechService tts,
        INotificationCascadeService cascade)
    {
        _agi = agi;
        _tts = tts;
        _cascade = cascade;
    }

    public async Task<(string AcaoExecutada, string RespostaFalada)> ExecuteAsync(
        AgiCallContext call,
        Tenant tenant,
        InferenceResponseDto iaResponse,
        CancellationToken ct = default)
    {
        var acao = iaResponse.AcaoSugerida.ToUpperInvariant();
        var resposta = iaResponse.RespostaTexto;

        switch (acao)
        {
            case "NOTIFICAR_MORADOR":
                var notificationResult = await _cascade.NotifyAsync(call, tenant, iaResponse, ct);
                call.NotificationCascadeResult = notificationResult;
                call.FinalReasonCode = notificationResult.ReasonCode;
                call.FinalReasonCategory = notificationResult.ReasonCategory;
                call.FinalReasonMessage = notificationResult.ReasonMessage;

                if (notificationResult.Success)
                {
                    if (call.VoiceChannel is not null)
                    {
                        var notifFile = await _tts.SynthesizeAsync("Aguarde, estamos notificando o morador.", ct);
                        await call.VoiceChannel.PlayAsync(notifFile, ct);
                    }

                    return ("NOTIFICAR_MORADOR", "Aguarde, estamos notificando o morador.");
                }

                return await RedirectToCentralAsync(
                    call,
                    tenant,
                    notificationResult.ReasonCode,
                    notificationResult.ReasonCategory,
                    notificationResult.ReasonMessage,
                    ct);

            case "ABRIR_PORTAO":
            case "LIBERAR_ACESSO":
                // Envia DTMF para painel de acesso e encerra a chamada via AMI stub.
                await _agi.SendDtmfAsync(call, "#9", ct);
                await _agi.HangupAsync(call, ct);
                return ("ABRIR_PORTAO", "Acesso liberado.");

            case "SOLICITAR_IDENTIFICACAO":
            case "SOLICITAR_DOC":
                if (call.VoiceChannel is not null)
                {
                    var idFile = await _tts.SynthesizeAsync(resposta, ct);
                    await call.VoiceChannel.PlayAsync(idFile, ct);
                }
                return (acao, resposta);

            case "ESCALAR_HUMANO":
                call.FinalReasonCode ??= "IA_ESCALOU_HUMANO";
                call.FinalReasonCategory ??= "decision";
                call.FinalReasonMessage ??= "A IA decidiu redirecionar o atendimento para a central humana.";
                return await RedirectToCentralAsync(call, tenant, null, null, null, ct);

            default:
                if (call.VoiceChannel is not null)
                {
                    var fallbackFile = await _tts.SynthesizeAsync(resposta, ct);
                    await call.VoiceChannel.PlayAsync(fallbackFile, ct);
                }
                return (acao, resposta);
        }
    }

    private async Task<(string AcaoExecutada, string RespostaFalada)> RedirectToCentralAsync(
        AgiCallContext call,
        Tenant tenant,
        string? reasonCode,
        string? reasonCategory,
        string? reasonMessage,
        CancellationToken ct)
    {
        const string message = "Aguarde. Não obtivemos resposta automática e seu atendimento será redirecionado para a central.";

        call.FinalReasonCode ??= reasonCode ?? "ESCALADO_PARA_CENTRAL";
        call.FinalReasonCategory ??= reasonCategory ?? "fallback";
        call.FinalReasonMessage ??= reasonMessage ?? "Atendimento redirecionado para a central humana.";

        if (call.VoiceChannel is not null)
        {
            var escalarFile = await _tts.SynthesizeAsync(message, ct);
            await call.VoiceChannel.PlayAsync(escalarFile, ct);
        }

        if (!string.IsNullOrWhiteSpace(tenant.RamalTransfHumano))
            await _agi.TransferAsync(call, tenant.RamalTransfHumano, ct);

        return ("ESCALAR_HUMANO", message);
    }
}
