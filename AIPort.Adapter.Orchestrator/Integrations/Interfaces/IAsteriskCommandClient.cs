using AIPort.Adapter.Orchestrator.Domain.Models;

namespace AIPort.Adapter.Orchestrator.Integrations.Interfaces;

public interface IAsteriskCommandClient
{
    Task AnswerAsync(AgiCallContext context, CancellationToken ct = default);
    Task PlayAudioAsync(AgiCallContext context, string audioRef, CancellationToken ct = default);
    Task SendDtmfAsync(AgiCallContext context, string digits, CancellationToken ct = default);
    Task TransferAsync(AgiCallContext context, string targetExtension, CancellationToken ct = default);
    Task HangupAsync(AgiCallContext context, CancellationToken ct = default);
}
