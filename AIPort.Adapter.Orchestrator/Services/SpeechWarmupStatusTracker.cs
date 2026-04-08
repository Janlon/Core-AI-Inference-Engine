using AIPort.Adapter.Orchestrator.Services.Interfaces;

namespace AIPort.Adapter.Orchestrator.Services;

public sealed class SpeechWarmupStatusTracker : ISpeechWarmupStatusProvider
{
    private SpeechWarmupStatusSnapshot _current = new(
        Status: "starting",
        Provider: "unknown",
        Ready: false,
        LastWarmupAtUtc: null,
        LastWarmupElapsedMs: null,
        Message: "Aguardando inicializacao da pilha de voz.");

    public SpeechWarmupStatusSnapshot GetCurrent() => _current;

    public void MarkStarting(string provider, string? message)
    {
        _current = _current with
        {
            Status = "starting",
            Provider = provider,
            Ready = false,
            Message = message
        };
    }

    public void MarkReady(string provider, DateTime warmedAtUtc, long? elapsedMs, string? message)
    {
        _current = new SpeechWarmupStatusSnapshot(
            Status: "healthy",
            Provider: provider,
            Ready: true,
            LastWarmupAtUtc: warmedAtUtc,
            LastWarmupElapsedMs: elapsedMs,
            Message: message);
    }

    public void MarkDegraded(string provider, DateTime checkedAtUtc, long? elapsedMs, string? message)
    {
        _current = new SpeechWarmupStatusSnapshot(
            Status: "degraded",
            Provider: provider,
            Ready: false,
            LastWarmupAtUtc: checkedAtUtc,
            LastWarmupElapsedMs: elapsedMs,
            Message: message);
    }
}