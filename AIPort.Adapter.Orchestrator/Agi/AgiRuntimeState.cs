using System.Collections.Concurrent;
using AIPort.Adapter.Orchestrator.Agi.Models;
using System.Threading;
using AIPort.Adapter.Orchestrator.Agi.Interfaces;

namespace AIPort.Adapter.Orchestrator.Agi;

public sealed class AgiRuntimeState : IAgiRuntimeState
{
    private int _activeChannels;
    private readonly ConcurrentDictionary<string, ActiveConnectionState> _connections = new();

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

    public string IncrementActiveChannels(string? remoteEndpoint = null)
    {
        var connectionId = Guid.NewGuid().ToString("N");
        _connections[connectionId] = new ActiveConnectionState
        {
            ConnectionId = connectionId,
            ConnectedAtUtc = DateTime.UtcNow,
            RemoteEndpoint = remoteEndpoint
        };

        Interlocked.Increment(ref _activeChannels);
        return connectionId;
    }

    public void AttachHandshake(string connectionId, string? sessionId, string? callerId, string? channel, int? tenantPid)
    {
        if (_connections.TryGetValue(connectionId, out var state))
        {
            state.SessionId = sessionId;
            state.CallerId = callerId;
            state.Channel = channel;
            state.TenantPid = tenantPid;
        }
    }

    public IReadOnlyCollection<ActiveAgiChannelSnapshot> GetActiveChannelSnapshots()
    {
        return _connections.Values
            .OrderBy(item => item.ConnectedAtUtc)
            .Select(item => new ActiveAgiChannelSnapshot(
                item.ConnectionId,
                item.ConnectedAtUtc,
                item.RemoteEndpoint,
                item.SessionId,
                item.CallerId,
                item.Channel,
                item.TenantPid))
            .ToArray();
    }

    public void DecrementActiveChannels(string connectionId)
    {
        _connections.TryRemove(connectionId, out _);

        if (Volatile.Read(ref _activeChannels) > 0)
        {
            Interlocked.Decrement(ref _activeChannels);
        }
    }

    private sealed class ActiveConnectionState
    {
        public required string ConnectionId { get; init; }
        public required DateTime ConnectedAtUtc { get; init; }
        public string? RemoteEndpoint { get; init; }
        public string? SessionId { get; set; }
        public string? CallerId { get; set; }
        public string? Channel { get; set; }
        public int? TenantPid { get; set; }
    }
}