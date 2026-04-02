using AIPort.Adapter.Orchestrator.Domain.Models;
using AIPort.Adapter.Orchestrator.Integrations.Interfaces;

namespace AIPort.Adapter.Orchestrator.Integrations;

/// <summary>
/// Adaptador inicial para comandos AGI. Nesta fase, registra operações em log.
/// </summary>
public sealed class AsteriskCommandClient : IAsteriskCommandClient
{
    private readonly ILogger<AsteriskCommandClient> _logger;

    public AsteriskCommandClient(ILogger<AsteriskCommandClient> logger)
    {
        _logger = logger;
    }

    public Task AnswerAsync(AgiCallContext context, CancellationToken ct = default)
    {
        _logger.LogInformation("[AGI] ANSWER | Session={Session} Channel={Channel}", context.SessionId, context.Channel);
        return Task.CompletedTask;
    }

    public Task PlayAudioAsync(AgiCallContext context, string audioRef, CancellationToken ct = default)
    {
        _logger.LogInformation("[AGI] STREAM FILE | Session={Session} Audio={AudioRef}", context.SessionId, audioRef);
        return Task.CompletedTask;
    }

    public Task SendDtmfAsync(AgiCallContext context, string digits, CancellationToken ct = default)
    {
        _logger.LogInformation("[AGI] SEND DTMF | Session={Session} Digits={Digits}", context.SessionId, digits);
        return Task.CompletedTask;
    }

    public Task TransferAsync(AgiCallContext context, string targetExtension, CancellationToken ct = default)
    {
        _logger.LogInformation("[AGI] TRANSFER | Session={Session} Target={Target}", context.SessionId, targetExtension);
        return Task.CompletedTask;
    }

    public Task HangupAsync(AgiCallContext context, CancellationToken ct = default)
    {
        _logger.LogInformation("[AGI] HANGUP | Session={Session}", context.SessionId);
        return Task.CompletedTask;
    }
}
