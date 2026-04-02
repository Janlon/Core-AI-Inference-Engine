namespace AIPort.Adapter.Orchestrator.Agi.Interfaces;

public interface IAgiRuntimeState
{
    bool IsEnabled { get; }
    bool IsListening { get; }
    string Host { get; }
    int Port { get; }
    int ActiveChannels { get; }
    void SetConfigured(string host, int port, bool enabled);
    void SetListening(bool isListening);
    void IncrementActiveChannels();
    void DecrementActiveChannels();
}