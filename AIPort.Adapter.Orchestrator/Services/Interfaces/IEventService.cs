namespace AIPort.Adapter.Orchestrator.Services.Interfaces;

/// <summary>
/// Modelo de evento para streaming
/// </summary>
public record EventRecord(
    string Id,
    DateTime At,
    string Level,
    string Message,
    string? Category = null,
    Dictionary<string, object>? Data = null
);

/// <summary>
/// Serviço para gerenciamento e streaming de eventos em tempo real
/// </summary>
public interface IEventService
{
    /// <summary>
    /// Publica um novo evento para todos os subscribers
    /// </summary>
    void PublishEvent(string level, string message, string? category = null, Dictionary<string, object>? data = null);

    /// <summary>
    /// Publica um evento de chamada iniciada
    /// </summary>
    void PublishCallStarted(string tenantName, string visitorInfo);

    /// <summary>
    /// Publica um evento de chamada encerrada
    /// </summary>
    void PublishCallEnded(string tenantName, string? reason = null);

    /// <summary>
    /// Publica um evento de erro
    /// </summary>
    void PublishError(string message, string? category = null);

    /// <summary>
    /// Publica um evento de aviso
    /// </summary>
    void PublishWarning(string message, string? category = null);

    /// <summary>
    /// Obtém os últimos N eventos do histórico
    /// </summary>
    IEnumerable<EventRecord> GetLatestEvents(int count = 20);

    /// <summary>
    /// Limpa todo o histórico de eventos
    /// </summary>
    void ClearHistory();

    /// <summary>
    /// Se inscreve para receber eventos em tempo real via StreamWriter (SSE)
    /// </summary>
    Task SubscribeAsync(StreamWriter writer, CancellationToken cancellationToken);
}
