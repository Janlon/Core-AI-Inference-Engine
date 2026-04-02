namespace AIPort.Adapter.Orchestrator.Integrations.Interfaces;

public interface ITextToSpeechService
{
    Task<string> SynthesizeAsync(string text, CancellationToken ct = default);

    Task<(byte[] AudioBytes, string ContentType, string FileExtension)> SynthesizeDownloadAsync(
        string text,
        string format,
        CancellationToken ct = default);
}
