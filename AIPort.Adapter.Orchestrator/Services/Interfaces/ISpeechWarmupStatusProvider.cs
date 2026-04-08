namespace AIPort.Adapter.Orchestrator.Services.Interfaces;

public interface ISpeechWarmupStatusProvider
{
    SpeechWarmupStatusSnapshot GetCurrent();
}