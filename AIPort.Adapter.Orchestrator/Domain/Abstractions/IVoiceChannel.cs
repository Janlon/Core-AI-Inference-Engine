using AIPort.Adapter.Orchestrator.Domain.Models;

namespace AIPort.Adapter.Orchestrator.Domain.Abstractions;

public interface IVoiceChannel
{
    Task<VoiceChannelResponse> PlayAsync(string filePath, CancellationToken ct = default);
    Task<VoiceChannelResponse> RecordAsync(string savePath, int maxTimeMs, CancellationToken ct = default);
    Task<char?> ReadDigitAsync(int timeoutMs, CancellationToken ct = default);
}
