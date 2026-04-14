using AIPort.Adapter.Orchestrator.Agi.Models;

namespace AIPort.Adapter.Orchestrator.Agi.Interfaces;

public interface IAgiRuntimeState
{
    bool IsEnabled { get; }
    bool IsListening { get; }
    string Host { get; }
    int Port { get; }
    int ActiveChannels { get; }
    IReadOnlyCollection<ActiveAgiChannelSnapshot> GetActiveChannelSnapshots();
    void SetConfigured(string host, int port, bool enabled);
    void SetListening(bool isListening);
    string IncrementActiveChannels(string? remoteEndpoint = null);
    void AttachHandshake(string connectionId, string? sessionId, string? callerId, string? channel, int? tenantPid);
    void DecrementActiveChannels(string connectionId);
}