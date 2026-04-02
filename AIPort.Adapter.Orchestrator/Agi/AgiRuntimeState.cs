using System.Threading;
using AIPort.Adapter.Orchestrator.Agi.Interfaces;

namespace AIPort.Adapter.Orchestrator.Agi;

public sealed class AgiRuntimeState : IAgiRuntimeState
{
    private int _activeChannels;

    public bool IsEnabled { get; private set; }
    public bool IsListening { get; private set; }
    public string Host { get; private set; } = string.Empty;
    public int Port { get; private set; }
    public int ActiveChannels => Volatile.Read(ref _activeChannels);

    public void SetConfigured(string host, int port, bool enabled)
    {
        Host = host;
        Port = port;
        IsEnabled = enabled;
    }

    public void SetListening(bool isListening)
    {
        IsListening = isListening;
    }

    public void IncrementActiveChannels()
    {
        Interlocked.Increment(ref _activeChannels);
    }

    public void DecrementActiveChannels()
    {
        if (Volatile.Read(ref _activeChannels) > 0)
        {
            Interlocked.Decrement(ref _activeChannels);
        }
    }
}