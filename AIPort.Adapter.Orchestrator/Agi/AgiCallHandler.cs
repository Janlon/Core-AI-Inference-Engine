using AIPort.Adapter.Orchestrator.Agi.Interfaces;
using AIPort.Adapter.Orchestrator.Agi.Models;
using AIPort.Adapter.Orchestrator.Domain.Models;
using AIPort.Adapter.Orchestrator.Services.Interfaces;

namespace AIPort.Adapter.Orchestrator.Agi;

public sealed class AgiCallHandler : IAgiCallHandler
{
    private readonly IOrchestrationService _orchestrationService;
    private readonly ILogger<AgiCallHandler> _logger;

    public AgiCallHandler(
        IOrchestrationService orchestrationService,
        ILogger<AgiCallHandler> logger)
    {
        _orchestrationService = orchestrationService;
        _logger = logger;
    }

    public async Task HandleAsync(FastAgiRequest request, IAgiChannel channel, CancellationToken ct = default)
    {
        var ctx = new AgiCallContext
        {
            SessionId = request.UniqueId,
            UniqueId = request.UniqueId,
            CallerId = request.CallerId,
            Channel = request.Channel,
            CalledNumber = request.CalledNumber,
            Context = request.Context,
            TenantPid = request.TenantPid,
            AudioFilePath = request.AudioFilePath,
            PreTranscribedText = request.PreTranscribedText,
            VoiceChannel = channel
        };

        var result = await _orchestrationService.HandleCallAsync(ctx, ct);

        if (!result.Sucesso)
        {
            _logger.LogWarning("Falha na orquestração da chamada Session={SessionId}: {Motivo}", result.SessionId, result.MotivoFalha);
            return;
        }

        _logger.LogInformation(
            "Chamada Session={SessionId} processada com sucesso. Acao={AcaoExecutada}",
            result.SessionId,
            result.AcaoExecutada);
    }
}
