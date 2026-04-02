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
                var notified = await _cascade.NotifyAsync(call, tenant, iaResponse, ct);
                if (notified)
                {
                    if (call.VoiceChannel is not null)
                    {
                        var notifFile = await _tts.SynthesizeAsync("Aguarde, estamos notificando o morador.", ct);
                        await call.VoiceChannel.PlayAsync(notifFile, ct);
                    }

                    return ("NOTIFICAR_MORADOR", "Aguarde, estamos notificando o morador.");
                }

                return await RedirectToCentralAsync(call, tenant, ct);

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
                return await RedirectToCentralAsync(call, tenant, ct);

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
        CancellationToken ct)
    {
        const string message = "Aguarde. Não obtivemos resposta automática e seu atendimento será redirecionado para a central.";

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
