namespace AIPort.Adapter.Orchestrator.Integrations.Interfaces;

public interface ISpeechToTextService
{
    Task<string> TranscribeAsync(string? audioPath, string? preTranscribedText, CancellationToken ct = default);
}
