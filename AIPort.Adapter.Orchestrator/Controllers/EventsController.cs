using AIPort.Adapter.Orchestrator.Services.Interfaces;
using AIPort.Adapter.Orchestrator.Data;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text;

namespace AIPort.Adapter.Orchestrator.Controllers;

/// <summary>
/// Controller para eventos em tempo real e histórico
/// Exposição de eventos via SSE (Server-Sent Events) e API JSON
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class EventsController : ControllerBase
{
    private readonly IEventService _eventService;
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<EventsController> _logger;

    public EventsController(IEventService eventService, IDbConnectionFactory connectionFactory, ILogger<EventsController> logger)
    {
        _eventService = eventService ?? throw new ArgumentNullException(nameof(eventService));
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Obtém os últimos eventos (endpoint REST tradicional)
    /// GET /api/events?limit=20
    /// </summary>
    [HttpGet]
    public IActionResult GetLatestEvents([FromQuery] int limit = 20)
    {
        try
        {
            var safeLimitLimit = Math.Max(1, Math.Min(100, limit));
            var events = _eventService.GetLatestEvents(safeLimitLimit);

            _logger.LogDebug("Retornando {Count} eventos", events.Count());

            return Ok(new
            {
                count = events.Count(),
                events = events,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter eventos");
            return StatusCode(500, new { error = "Erro ao buscar eventos" });
        }
    }

    /// <summary>
    /// Inicia streaming em tempo real via SSE
    /// GET /api/events/stream
    /// </summary>
    [HttpGet("stream")]
    public async Task EventStream(CancellationToken cancellationToken)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");
        Response.Headers.Append("X-Accel-Buffering", "no");

        _logger.LogInformation("Novo cliente SSE conectado: {RemoteIp}", HttpContext.Connection.RemoteIpAddress);

        try
        {
            // Envia um evento initial de conexão
            await Response.WriteAsync("data: {\"type\":\"connected\",\"message\":\"Conectado ao stream de eventos\"}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);

            // Se inscreve no serviço de eventos
            var writer = new StreamWriter(Response.Body) { AutoFlush = false };
            await _eventService.SubscribeAsync(writer, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Cliente SSE desconectado");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no streaming SSE");
        }
    }

    /// <summary>
    /// Consulta conversas reais (CallSessions + CallInteractions) com filtros.
    /// GET /api/events/conversations
    /// </summary>
    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations(
        [FromQuery] int? tenantId = null,
        [FromQuery] int? tenantPid = null,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sessionId = null,
        [FromQuery] string? loopRisk = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] int limit = 20,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var safeLimit = Math.Clamp(limit, 1, 100);
            var safeOffset = Math.Max(0, offset);
            var trimmedKeyword = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
            var hasKeyword = !string.IsNullOrWhiteSpace(trimmedKeyword);
            var likeKeyword = hasKeyword ? $"%{trimmedKeyword}%" : null;
            var normalizedLoopRisk = NormalizeLoopRisk(loopRisk);

            var queryParams = new
            {
                TenantId = tenantId,
                TenantPid = tenantPid,
                SessionId = string.IsNullOrWhiteSpace(sessionId) ? null : sessionId.Trim(),
                FromUtc = fromUtc,
                ToUtc = toUtc,
                HasKeyword = hasKeyword ? 1 : 0,
                LikeKeyword = likeKeyword,
                Limit = safeLimit,
                Offset = safeOffset
            };

            const string countSql = """
                SELECT COUNT(*)
                FROM CallSessions cs
                INNER JOIN Tenants t ON t.Id = cs.TenantId
                WHERE
                    (@TenantId IS NULL OR cs.TenantId = @TenantId)
                    AND (@TenantPid IS NULL OR t.Pid = @TenantPid)
                    AND (@SessionId IS NULL OR cs.SessionId = @SessionId)
                    AND (@FromUtc IS NULL OR cs.StartedAt >= @FromUtc)
                    AND (@ToUtc IS NULL OR cs.StartedAt <= @ToUtc)
                    AND (
                        @HasKeyword = 0
                        OR cs.CallerId LIKE @LikeKeyword
                        OR cs.Channel LIKE @LikeKeyword
                        OR EXISTS (
                            SELECT 1
                            FROM CallInteractions cix
                            WHERE cix.SessionId = cs.SessionId
                              AND (
                                  cix.BotPrompt LIKE @LikeKeyword
                                  OR cix.UserTranscription LIKE @LikeKeyword
                              )
                        )
                    );
                """;

            const string sessionsSql = """
                SELECT
                    cs.SessionId,
                    cs.TenantId,
                    t.Pid AS TenantPid,
                    t.NomeIdentificador,
                    cs.CallerId,
                    cs.Channel,
                    cs.StartedAt,
                    cs.EndedAt,
                    cs.FinalAction,
                    cs.FinalExtractedData,
                    COUNT(ci.Id) AS InteractionCount,
                    COALESCE(SUM(ci.InteractionDurationMs), 0) AS TotalInteractionDurationMs,
                    COALESCE(SUM(ci.LlmProcessingTimeMs), 0) AS TotalLlmProcessingTimeMs,
                    MIN(ci.CreatedAt) AS FirstInteractionAt,
                    MAX(ci.CreatedAt) AS LastInteractionAt
                FROM CallSessions cs
                INNER JOIN Tenants t ON t.Id = cs.TenantId
                LEFT JOIN CallInteractions ci ON ci.SessionId = cs.SessionId
                WHERE
                    (@TenantId IS NULL OR cs.TenantId = @TenantId)
                    AND (@TenantPid IS NULL OR t.Pid = @TenantPid)
                    AND (@SessionId IS NULL OR cs.SessionId = @SessionId)
                    AND (@FromUtc IS NULL OR cs.StartedAt >= @FromUtc)
                    AND (@ToUtc IS NULL OR cs.StartedAt <= @ToUtc)
                    AND (
                        @HasKeyword = 0
                        OR cs.CallerId LIKE @LikeKeyword
                        OR cs.Channel LIKE @LikeKeyword
                        OR EXISTS (
                            SELECT 1
                            FROM CallInteractions cix
                            WHERE cix.SessionId = cs.SessionId
                              AND (
                                  cix.BotPrompt LIKE @LikeKeyword
                                  OR cix.UserTranscription LIKE @LikeKeyword
                              )
                        )
                    )
                GROUP BY
                    cs.SessionId,
                    cs.TenantId,
                    t.Pid,
                    t.NomeIdentificador,
                    cs.CallerId,
                    cs.Channel,
                    cs.StartedAt,
                    cs.EndedAt,
                    cs.FinalAction,
                    cs.FinalExtractedData
                ORDER BY cs.StartedAt DESC
                LIMIT @Limit OFFSET @Offset;
                """;

            const string sessionsSqlNoPaging = """
                SELECT
                    cs.SessionId,
                    cs.TenantId,
                    t.Pid AS TenantPid,
                    t.NomeIdentificador,
                    cs.CallerId,
                    cs.Channel,
                    cs.StartedAt,
                    cs.EndedAt,
                    cs.FinalAction,
                    cs.FinalExtractedData,
                    COUNT(ci.Id) AS InteractionCount,
                    COALESCE(SUM(ci.InteractionDurationMs), 0) AS TotalInteractionDurationMs,
                    COALESCE(SUM(ci.LlmProcessingTimeMs), 0) AS TotalLlmProcessingTimeMs,
                    MIN(ci.CreatedAt) AS FirstInteractionAt,
                    MAX(ci.CreatedAt) AS LastInteractionAt
                FROM CallSessions cs
                INNER JOIN Tenants t ON t.Id = cs.TenantId
                LEFT JOIN CallInteractions ci ON ci.SessionId = cs.SessionId
                WHERE
                    (@TenantId IS NULL OR cs.TenantId = @TenantId)
                    AND (@TenantPid IS NULL OR t.Pid = @TenantPid)
                    AND (@SessionId IS NULL OR cs.SessionId = @SessionId)
                    AND (@FromUtc IS NULL OR cs.StartedAt >= @FromUtc)
                    AND (@ToUtc IS NULL OR cs.StartedAt <= @ToUtc)
                    AND (
                        @HasKeyword = 0
                        OR cs.CallerId LIKE @LikeKeyword
                        OR cs.Channel LIKE @LikeKeyword
                        OR EXISTS (
                            SELECT 1
                            FROM CallInteractions cix
                            WHERE cix.SessionId = cs.SessionId
                              AND (
                                  cix.BotPrompt LIKE @LikeKeyword
                                  OR cix.UserTranscription LIKE @LikeKeyword
                              )
                        )
                    )
                GROUP BY
                    cs.SessionId,
                    cs.TenantId,
                    t.Pid,
                    t.NomeIdentificador,
                    cs.CallerId,
                    cs.Channel,
                    cs.StartedAt,
                    cs.EndedAt,
                    cs.FinalAction,
                    cs.FinalExtractedData
                ORDER BY cs.StartedAt DESC;
                """;

            using var conn = _connectionFactory.CreateConnection();
            var totalCount = 0L;

            List<ConversationSessionRow> sessionRows;
            if (normalizedLoopRisk is null)
            {
                totalCount = await conn.ExecuteScalarAsync<long>(
                    new CommandDefinition(countSql, queryParams, cancellationToken: cancellationToken));

                sessionRows = (await conn.QueryAsync<ConversationSessionRow>(
                    new CommandDefinition(
                        sessionsSql,
                        queryParams,
                        cancellationToken: cancellationToken))).ToList();
            }
            else
            {
                // Para loopRisk server-side precisamos calcular telemetria de todas as sessões do filtro base
                // e só então paginar sobre o subconjunto (high|medium|low).
                sessionRows = (await conn.QueryAsync<ConversationSessionRow>(
                    new CommandDefinition(
                        sessionsSqlNoPaging,
                        queryParams,
                        cancellationToken: cancellationToken))).ToList();
            }

            if (sessionRows.Count == 0)
            {
                return Ok(new
                {
                    count = 0,
                    totalCount,
                    sessions = Array.Empty<object>(),
                    pagination = new
                    {
                        limit = safeLimit,
                        offset = safeOffset,
                        hasNext = false,
                        hasPrevious = safeOffset > 0
                    },
                    filters = new
                    {
                        tenantId,
                        tenantPid,
                        keyword = trimmedKeyword,
                        sessionId,
                        fromUtc,
                        toUtc,
                        limit = safeLimit,
                        offset = safeOffset
                    }
                });
            }

            var sessionIds = sessionRows.Select(x => x.SessionId).Distinct().ToArray();

            const string interactionsSql = """
                SELECT
                    Id,
                    SessionId,
                    InteractionOrder,
                    BotPrompt,
                    UserTranscription,
                    ResolutionLayer,
                    ExtractedDataJson,
                    InteractionDurationMs,
                    LlmProcessingTimeMs,
                    CreatedAt
                FROM CallInteractions
                WHERE SessionId IN @SessionIds
                ORDER BY SessionId ASC, InteractionOrder ASC, Id ASC;
                """;

            var interactionRows = (await conn.QueryAsync<ConversationInteractionRow>(
                new CommandDefinition(
                    interactionsSql,
                    new { SessionIds = sessionIds },
                    cancellationToken: cancellationToken))).ToList();

            var interactionLookup = interactionRows
                .GroupBy(x => x.SessionId)
                .ToDictionary(
                    g => g.Key,
                    g => g.ToList());

            var sessions = sessionRows.Select(row =>
            {
                interactionLookup.TryGetValue(row.SessionId, out var list);
                list ??= new List<ConversationInteractionRow>();

                var assistantPrompts = list
                    .Where(x => !string.IsNullOrWhiteSpace(x.BotPrompt))
                    .Select(x => NormalizeForLoopDetection(x.BotPrompt!))
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();

                var assistantPromptSlots = list
                    .Where(x => !string.IsNullOrWhiteSpace(x.BotPrompt))
                    .Select(x => ClassifyPromptSlot(x.BotPrompt!))
                    .Where(x => x is not null)
                    .Cast<string>()
                    .ToList();

                var visitorUtterances = list
                    .Where(x => !string.IsNullOrWhiteSpace(x.UserTranscription))
                    .Select(x => NormalizeForLoopDetection(x.UserTranscription!))
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();

                var repeatedAssistantPrompts = assistantPrompts
                    .GroupBy(x => x)
                    .Sum(g => Math.Max(0, g.Count() - 1));

                var repeatedAssistantSlotRequests = assistantPromptSlots
                    .GroupBy(x => x)
                    .Sum(g => Math.Max(0, g.Count() - 1));

                var documentRequestCount = assistantPromptSlots.Count(slot => slot == "document");

                var repeatedVisitorUtterances = visitorUtterances
                    .GroupBy(x => x)
                    .Sum(g => Math.Max(0, g.Count() - 1));

                var lowInfoVisitorMessages = visitorUtterances.Count(v =>
                {
                    var letters = v.Count(char.IsLetter);
                    var digits = v.Count(char.IsDigit);
                    return v.Length < 8 || (digits > letters && letters < 4);
                });

                var loopSignals = repeatedAssistantPrompts + repeatedAssistantSlotRequests + repeatedVisitorUtterances;
                var loopRisk = loopSignals >= 3 || documentRequestCount >= 3
                    ? "high"
                    : loopSignals >= 1
                        ? "medium"
                        : "low";

                var avgInteractionMs = row.InteractionCount > 0
                    ? row.TotalInteractionDurationMs / row.InteractionCount
                    : 0;

                var avgLlmMs = row.InteractionCount > 0
                    ? row.TotalLlmProcessingTimeMs / row.InteractionCount
                    : 0;

                var messages = new List<object>();
                foreach (var interaction in list)
                {
                    if (!string.IsNullOrWhiteSpace(interaction.BotPrompt))
                    {
                        messages.Add(new
                        {
                            id = $"{interaction.SessionId}-bot-{interaction.Id}",
                            role = "assistant",
                            text = interaction.BotPrompt,
                            interactionOrder = interaction.InteractionOrder,
                            at = interaction.CreatedAt,
                            metrics = new
                            {
                                llmProcessingTimeMs = interaction.LlmProcessingTimeMs,
                                resolutionLayer = interaction.ResolutionLayer,
                                extractedDataJson = interaction.ExtractedDataJson
                            }
                        });
                    }

                    if (!string.IsNullOrWhiteSpace(interaction.UserTranscription))
                    {
                        messages.Add(new
                        {
                            id = $"{interaction.SessionId}-user-{interaction.Id}",
                            role = "visitor",
                            text = interaction.UserTranscription,
                            interactionOrder = interaction.InteractionOrder,
                            at = interaction.CreatedAt,
                            metrics = new
                            {
                                interactionDurationMs = interaction.InteractionDurationMs,
                                resolutionLayer = interaction.ResolutionLayer,
                                extractedDataJson = interaction.ExtractedDataJson
                            }
                        });
                    }
                }

                var finalResolutionLayer = ExtractFinalResolutionLayer(row.FinalExtractedData);
                var finalReason = ExtractFinalReason(row.FinalExtractedData);
                var finalNotification = ExtractFinalNotification(row.FinalExtractedData);

                return new
                {
                    sessionId = row.SessionId,
                    tenantId = row.TenantId,
                    tenantPid = row.TenantPid,
                    tenantName = row.NomeIdentificador,
                    callerId = row.CallerId,
                    channel = row.Channel,
                    startedAt = row.StartedAt,
                    endedAt = row.EndedAt,
                    finalAction = row.FinalAction,
                    finalResolutionLayer,
                    finalReasonCode = finalReason.Code,
                    finalReasonCategory = finalReason.Category,
                    finalReasonMessage = finalReason.Message,
                    finalNotification,
                    interactionCount = row.InteractionCount,
                    totalInteractionDurationMs = row.TotalInteractionDurationMs,
                    totalLlmProcessingTimeMs = row.TotalLlmProcessingTimeMs,
                    firstInteractionAt = row.FirstInteractionAt,
                    lastInteractionAt = row.LastInteractionAt,
                    telemetry = new
                    {
                        avgInteractionDurationMs = avgInteractionMs,
                        avgLlmProcessingTimeMs = avgLlmMs,
                        repeatedAssistantPrompts,
                        repeatedAssistantSlotRequests,
                        repeatedVisitorUtterances,
                        documentRequestCount,
                        lowInfoVisitorMessages,
                        loopRisk
                    },
                    messages
                };
            })
            .Where(s => normalizedLoopRisk is null || string.Equals(s.telemetry.loopRisk, normalizedLoopRisk, StringComparison.OrdinalIgnoreCase))
            .ToList();

            if (normalizedLoopRisk is not null)
            {
                totalCount = sessions.Count;
                sessions = sessions
                    .Skip(safeOffset)
                    .Take(safeLimit)
                    .ToList();
            }

            return Ok(new
            {
                count = sessions.Count,
                totalCount,
                sessions,
                pagination = new
                {
                    limit = safeLimit,
                    offset = safeOffset,
                    hasNext = safeOffset + sessions.Count < totalCount,
                    hasPrevious = safeOffset > 0
                },
                filters = new
                {
                    tenantId,
                    tenantPid,
                    keyword = trimmedKeyword,
                    sessionId,
                    loopRisk = normalizedLoopRisk,
                    fromUtc,
                    toUtc,
                    limit = safeLimit,
                    offset = safeOffset
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao consultar conversas");
            return StatusCode(500, new { error = "Erro ao consultar conversas" });
        }
    }

    /// <summary>
    /// Publica um evento manual (para testes)
    /// POST /api/events
    /// </summary>
    [HttpPost]
    public IActionResult PublishEvent([FromBody] PublishEventRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { error = "Message é obrigatória" });
        }

        try
        {
            _eventService.PublishEvent(
                level: request.Level ?? "info",
                message: request.Message,
                category: request.Category,
                data: request.Data
            );

            _logger.LogInformation("Evento publicado manualmente: {Message}", request.Message);

            return Ok(new { status = "published" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao publicar evento");
            return StatusCode(500, new { error = "Erro ao publicar evento" });
        }
    }

    /// <summary>
    /// Limpa o histórico de eventos (requer autenticação em produção)
    /// DELETE /api/events
    /// </summary>
    [HttpDelete]
    public IActionResult ClearHistory()
    {
        try
        {
            _eventService.ClearHistory();
            _logger.LogInformation("Histórico de eventos limpo");
            return Ok(new { status = "cleared" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao limpar histórico de eventos");
            return StatusCode(500, new { error = "Erro ao limpar histórico" });
        }
    }

    private static string? NormalizeLoopRisk(string? loopRisk)
    {
        if (string.IsNullOrWhiteSpace(loopRisk))
            return null;

        var value = loopRisk.Trim().ToLowerInvariant();
        return value is "low" or "medium" or "high" ? value : null;
    }

    private static string NormalizeForLoopDetection(string value)
    {
        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
                continue;
            }

            if (char.IsWhiteSpace(ch))
                sb.Append(' ');
        }

        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string? ClassifyPromptSlot(string prompt)
    {
        var normalized = NormalizeForLoopDetection(prompt);

        if (normalized.Contains("document") || normalized.Contains("cpf") || normalized.Contains("identificacao") || normalized.Contains("rg"))
            return "document";

        if (normalized.Contains("apartamento") || normalized.Contains("unidade"))
            return "apartment";

        if (normalized.Contains("nome") && normalized.Contains("visitante"))
            return "visitor_name";

        if (normalized.Contains("nome") && (normalized.Contains("morador") || normalized.Contains("residente")))
            return "resident_name";

        if (normalized.Contains("bloco"))
            return "block";

        if (normalized.Contains("torre"))
            return "tower";

        return null;
    }

    private static string? ExtractFinalResolutionLayer(string? finalExtractedData)
    {
        if (string.IsNullOrWhiteSpace(finalExtractedData) || finalExtractedData == "{}")
            return null;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(finalExtractedData);
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
                return null;

            if (doc.RootElement.TryGetProperty("camadaResolucao", out var camadaNode) && camadaNode.ValueKind == System.Text.Json.JsonValueKind.String)
                return camadaNode.GetString();

            if (doc.RootElement.TryGetProperty("resolutionLayer", out var resolutionNode) && resolutionNode.ValueKind == System.Text.Json.JsonValueKind.String)
                return resolutionNode.GetString();
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static (string? Code, string? Category, string? Message) ExtractFinalReason(string? finalExtractedData)
    {
        if (string.IsNullOrWhiteSpace(finalExtractedData) || finalExtractedData == "{}")
            return (null, null, null);

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(finalExtractedData);
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
                return (null, null, null);

            if (!doc.RootElement.TryGetProperty("motivoFinal", out var reasonNode) || reasonNode.ValueKind != System.Text.Json.JsonValueKind.Object)
                return (null, null, null);

            return (
                GetStringProperty(reasonNode, "code"),
                GetStringProperty(reasonNode, "category"),
                GetStringProperty(reasonNode, "message"));
        }
        catch
        {
            return (null, null, null);
        }
    }

    private static object? ExtractFinalNotification(string? finalExtractedData)
    {
        if (string.IsNullOrWhiteSpace(finalExtractedData) || finalExtractedData == "{}")
            return null;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(finalExtractedData);
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
                return null;

            if (!doc.RootElement.TryGetProperty("notificacao", out var notificationNode) || notificationNode.ValueKind != System.Text.Json.JsonValueKind.Object)
                return null;

            System.Text.Json.JsonElement? webhookNode = null;
            if (notificationNode.TryGetProperty("webhook", out var webhook) && webhook.ValueKind == System.Text.Json.JsonValueKind.Object)
                webhookNode = webhook;

            return new
            {
                success = GetBooleanProperty(notificationNode, "success"),
                reasonCode = GetStringProperty(notificationNode, "reasonCode"),
                reasonCategory = GetStringProperty(notificationNode, "reasonCategory"),
                reasonMessage = GetStringProperty(notificationNode, "reasonMessage"),
                redirectToHumanRecommended = GetBooleanProperty(notificationNode, "redirectToHumanRecommended"),
                webhook = webhookNode is null
                    ? null
                    : new
                    {
                        success = GetBooleanProperty(webhookNode.Value, "success"),
                        code = GetStringProperty(webhookNode.Value, "code"),
                        category = GetStringProperty(webhookNode.Value, "category"),
                        message = GetStringProperty(webhookNode.Value, "message"),
                        httpStatusCode = GetIntProperty(webhookNode.Value, "httpStatusCode"),
                        responseBodyExcerpt = GetStringProperty(webhookNode.Value, "responseBodyExcerpt"),
                        elapsedMs = GetLongProperty(webhookNode.Value, "elapsedMs"),
                        payloadHash = GetStringProperty(webhookNode.Value, "payloadHash"),
                        payloadSentAtUtc = GetStringProperty(webhookNode.Value, "payloadSentAtUtc"),
                        correlationId = GetStringProperty(webhookNode.Value, "correlationId"),
                        correlationField = GetStringProperty(webhookNode.Value, "correlationField")
                    }
            };
        }
        catch
        {
            return null;
        }
    }

    private static string? GetStringProperty(System.Text.Json.JsonElement element, string name)
        => element.TryGetProperty(name, out var property) && property.ValueKind == System.Text.Json.JsonValueKind.String
            ? property.GetString()
            : null;

    private static bool? GetBooleanProperty(System.Text.Json.JsonElement element, string name)
        => element.TryGetProperty(name, out var property) && (property.ValueKind == System.Text.Json.JsonValueKind.True || property.ValueKind == System.Text.Json.JsonValueKind.False)
            ? property.GetBoolean()
            : null;

    private static int? GetIntProperty(System.Text.Json.JsonElement element, string name)
        => element.TryGetProperty(name, out var property) && property.TryGetInt32(out var value)
            ? value
            : null;

    private static long? GetLongProperty(System.Text.Json.JsonElement element, string name)
        => element.TryGetProperty(name, out var property) && property.TryGetInt64(out var value)
            ? value
            : null;
}

/// <summary>
/// Modelo para publicação de eventos via POST
/// </summary>
public class PublishEventRequest
{
    public string? Level { get; set; }
    public string? Message { get; set; }
    public string? Category { get; set; }
    public Dictionary<string, object>? Data { get; set; }
}

internal sealed class ConversationSessionRow
{
    public required string SessionId { get; set; }
    public int TenantId { get; set; }
    public int TenantPid { get; set; }
    public required string NomeIdentificador { get; set; }
    public string? CallerId { get; set; }
    public string? Channel { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public string? FinalAction { get; set; }
    public string? FinalExtractedData { get; set; }
    public int InteractionCount { get; set; }
    public long TotalInteractionDurationMs { get; set; }
    public long TotalLlmProcessingTimeMs { get; set; }
    public DateTime? FirstInteractionAt { get; set; }
    public DateTime? LastInteractionAt { get; set; }
}

internal sealed class ConversationInteractionRow
{
    public long Id { get; set; }
    public required string SessionId { get; set; }
    public int InteractionOrder { get; set; }
    public string? BotPrompt { get; set; }
    public string? UserTranscription { get; set; }
    public string? ResolutionLayer { get; set; }
    public string? ExtractedDataJson { get; set; }
    public long InteractionDurationMs { get; set; }
    public long LlmProcessingTimeMs { get; set; }
    public DateTime CreatedAt { get; set; }
}
