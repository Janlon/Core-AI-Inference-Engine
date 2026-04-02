namespace AIPort.Adapter.Orchestrator.Agi.Interfaces;

using AIPort.Adapter.Orchestrator.Agi.Models;
using AIPort.Adapter.Orchestrator.Domain.Abstractions;

public interface IAgiChannel : IVoiceChannel
{
    Task<AgiResponse> AnswerAsync(CancellationToken ct = default);
    Task<AgiResponse> PlayAudioAsync(string filePath, CancellationToken ct = default);
    Task<AgiResponse> RecordAudioAsync(string savePath, int maxTimeMs, CancellationToken ct = default);
}
