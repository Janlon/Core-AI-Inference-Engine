using AIPort.Adapter.Orchestrator.Services.Interfaces;
using System.Collections.Concurrent;
using System.Text.Json;

namespace AIPort.Adapter.Orchestrator.Services;

/// <summary>
/// Implementação de serviço para gerenciamento e streaming de eventos em tempo real
/// Mantém histórico de eventos em memória e permite subscribers via SSE
/// </summary>
public class EventService : IEventService
{
    private readonly ILogger<EventService> _logger;
    private readonly int _maxHistorySize = 250;  // Limite de eventos mantidos em memória
    private readonly ConcurrentQueue<EventRecord> _eventHistory = new();
    private readonly ConcurrentBag<StreamWriter> _subscribers = new();
    private readonly SemaphoreSlim _subscriberLock = new(1, 1);

    public EventService(ILogger<EventService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Publica um novo evento para todos os subscribers
    /// </summary>
    public void PublishEvent(string level, string message, string? category = null, Dictionary<string, object>? data = null)
    {
        var eventRecord = new EventRecord(
            Id: Guid.NewGuid().ToString("N"),
            At: DateTime.UtcNow,
            Level: level?.ToLower() ?? "info",
            Message: message ?? string.Empty,
            Category: category,
            Data: data
        );

        // Adiciona ao histórico
        _eventHistory.Enqueue(eventRecord);
        if (_eventHistory.Count > _maxHistorySize)
        {
            _eventHistory.TryDequeue(out _);
        }

        // Notifica subscribers via SSE
        _ = BroadcastEventAsync(eventRecord);
    }

    /// <summary>
    /// Publica um evento de chamada iniciada
    /// </summary>
    public void PublishCallStarted(string tenantName, string visitorInfo)
    {
        PublishEvent(
            level: "info",
            message: $"Chamada iniciada em {tenantName} - {visitorInfo}",
            category: "CALL_START",
            data: new Dictionary<string, object>
            {
                { "tenant", tenantName },
                { "visitor", visitorInfo },
                { "type", "call_start" }
            }
        );
    }

    /// <summary>
    /// Publica um evento de chamada encerrada
    /// </summary>
    public void PublishCallEnded(string tenantName, string? reason = null)
    {
        PublishEvent(
            level: "info",
            message: $"Chamada encerrada em {tenantName}" + (reason is not null ? $": {reason}" : ""),
            category: "CALL_END",
            data: new Dictionary<string, object>
            {
                { "tenant", tenantName },
                { "reason", reason ?? "normal" },
                { "type", "call_end" }
            }
        );
    }

    /// <summary>
    /// Publica um evento de erro
    /// </summary>
    public void PublishError(string message, string? category = null)
    {
        PublishEvent(level: "error", message: message, category: category ?? "ERROR");
    }

    /// <summary>
    /// Publica um evento de aviso
    /// </summary>
    public void PublishWarning(string message, string? category = null)
    {
        PublishEvent(level: "warn", message: message, category: category ?? "WARNING");
    }

    /// <summary>
    /// Obtém os últimos N eventos do histórico
    /// </summary>
    public IEnumerable<EventRecord> GetLatestEvents(int count = 20)
    {
        return _eventHistory
            .OrderByDescending(e => e.At)
            .Take(count)
            .OrderBy(e => e.At)
            .ToList();
    }

    /// <summary>
    /// Limpa todo o histórico de eventos
    /// </summary>
    public void ClearHistory()
    {
        while (_eventHistory.TryDequeue(out _)) { }
        _logger.LogInformation("Histórico de eventos limpo");
    }

    /// <summary>
    /// Se inscreve para receber eventos em tempo real via StreamWriter (SSE)
    /// </summary>
    public async Task SubscribeAsync(StreamWriter writer, CancellationToken cancellationToken)
    {
        if (writer == null) throw new ArgumentNullException(nameof(writer));

        await _subscriberLock.WaitAsync(cancellationToken);
        try
        {
            _subscribers.Add(writer);
        }
        finally
        {
            _subscriberLock.Release();
        }

        _logger.LogInformation("Novo subscriber SSE conectado. Total: {Count}", _subscribers.Count);

        try
        {
            // Aguarda até que a conexão seja fechada
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(1000, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Esperado quando a conexão é fechada
        }
        finally
        {
            _logger.LogInformation("Subscriber SSE desconectado. Total: {Count}", _subscribers.Count - 1);
        }
    }

    /// <summary>
    /// Envia um evento para todos os subscribers conectados
    /// </summary>
    private async Task BroadcastEventAsync(EventRecord eventRecord)
    {
        if (_subscribers.Count == 0) return;

        var json = JsonSerializer.Serialize(eventRecord);
        var sseMessage = $"data: {json}\n\n";

        var disconnectedWriters = new List<StreamWriter>();

        foreach (var subscriber in _subscribers)
        {
            try
            {
                await subscriber.WriteAsync(sseMessage);
                await subscriber.FlushAsync();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Erro ao enviar evento para subscriber");
                disconnectedWriters.Add(subscriber);
            }
        }

        // Remove subscribers desconectados
        foreach (var writer in disconnectedWriters)
        {
            // Cria nova bag sem os desconectados
            while (_subscribers.TryTake(out var item))
            {
                if (item != writer)
                {
                    _subscribers.Add(item);
                }
            }
        }
    }
}
