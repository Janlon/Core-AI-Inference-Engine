using Dapper;
using AIPort.Adapter.Orchestrator.Data.Entities;

namespace AIPort.Adapter.Orchestrator.Data.Repositories;

public sealed class CallSessionRepository : ICallSessionRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private bool? _hasResolutionLayerColumn;
    private bool? _hasExtractedDataJsonColumn;
    private readonly Dictionary<string, bool> _callSessionColumnCache = new(StringComparer.OrdinalIgnoreCase);

    public CallSessionRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task CreateSessionAsync(CallSession session, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO CallSessions
            (
                SessionId,
                TenantId,
                CallerId,
                Channel,
                StartedAt,
                EndedAt,
                FinalAction,
                FinalExtractedData
            )
            VALUES
            (
                @SessionId,
                @TenantId,
                @CallerId,
                @Channel,
                @StartedAt,
                @EndedAt,
                @FinalAction,
                @FinalExtractedData
            );
            """;

        using var conn = _connectionFactory.CreateConnection();
        var cmd = new CommandDefinition(sql, session, cancellationToken: ct);
        await conn.ExecuteAsync(cmd);
    }

    public async Task<long> InsertInteractionAsync(CallInteraction interaction, CancellationToken ct = default)
    {
        const string sqlWithResolutionLayer = """
            INSERT INTO CallInteractions
            (
                SessionId,
                InteractionOrder,
                BotPrompt,
                UserTranscription,
                ResolutionLayer,
                ExtractedDataJson,
                InteractionDurationMs,
                LlmProcessingTimeMs,
                CreatedAt
            )
            VALUES
            (
                @SessionId,
                @InteractionOrder,
                @BotPrompt,
                @UserTranscription,
                @ResolutionLayer,
                @ExtractedDataJson,
                @InteractionDurationMs,
                @LlmProcessingTimeMs,
                @CreatedAt
            );

            SELECT LAST_INSERT_ID();
            """;

        const string sqlWithExtractedDataOnly = """
            INSERT INTO CallInteractions
            (
                SessionId,
                InteractionOrder,
                BotPrompt,
                UserTranscription,
                ExtractedDataJson,
                InteractionDurationMs,
                LlmProcessingTimeMs,
                CreatedAt
            )
            VALUES
            (
                @SessionId,
                @InteractionOrder,
                @BotPrompt,
                @UserTranscription,
                @ExtractedDataJson,
                @InteractionDurationMs,
                @LlmProcessingTimeMs,
                @CreatedAt
            );

            SELECT LAST_INSERT_ID();
            """;

        const string sqlLegacy = """
            INSERT INTO CallInteractions
            (
                SessionId,
                InteractionOrder,
                BotPrompt,
                UserTranscription,
                InteractionDurationMs,
                LlmProcessingTimeMs,
                CreatedAt
            )
            VALUES
            (
                @SessionId,
                @InteractionOrder,
                @BotPrompt,
                @UserTranscription,
                @InteractionDurationMs,
                @LlmProcessingTimeMs,
                @CreatedAt
            );

            SELECT LAST_INSERT_ID();
            """;

        using var conn = _connectionFactory.CreateConnection();
        var hasResolutionLayerColumn = await HasResolutionLayerColumnAsync(conn, ct);
        var hasExtractedDataJsonColumn = await HasExtractedDataJsonColumnAsync(conn, ct);

        var sql = (hasResolutionLayerColumn, hasExtractedDataJsonColumn) switch
        {
            (true, true) => sqlWithResolutionLayer,
            (false, true) => sqlWithExtractedDataOnly,
            _ => sqlLegacy
        };

        var cmd = new CommandDefinition(sql, interaction, cancellationToken: ct);
        return await conn.ExecuteScalarAsync<long>(cmd);
    }

    public async Task<int> GetNextInteractionOrderAsync(string sessionId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT COALESCE(MAX(InteractionOrder), 0) + 1
            FROM CallInteractions
            WHERE SessionId = @SessionId;
            """;

        using var conn = _connectionFactory.CreateConnection();
        var cmd = new CommandDefinition(sql, new { SessionId = sessionId }, cancellationToken: ct);
        return await conn.ExecuteScalarAsync<int>(cmd);
    }

    private async Task<bool> HasResolutionLayerColumnAsync(System.Data.IDbConnection conn, CancellationToken ct)
    {
        if (_hasResolutionLayerColumn.HasValue)
            return _hasResolutionLayerColumn.Value;

        const string sql = """
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'CallInteractions'
              AND COLUMN_NAME = 'ResolutionLayer';
            """;

        var cmd = new CommandDefinition(sql, cancellationToken: ct);
        var count = await conn.ExecuteScalarAsync<long>(cmd);
        _hasResolutionLayerColumn = count > 0;
        return _hasResolutionLayerColumn.Value;
    }

    private async Task<bool> HasExtractedDataJsonColumnAsync(System.Data.IDbConnection conn, CancellationToken ct)
    {
        if (_hasExtractedDataJsonColumn.HasValue)
            return _hasExtractedDataJsonColumn.Value;

        const string sql = """
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'CallInteractions'
              AND COLUMN_NAME = 'ExtractedDataJson';
            """;

        var cmd = new CommandDefinition(sql, cancellationToken: ct);
        var count = await conn.ExecuteScalarAsync<long>(cmd);
        _hasExtractedDataJsonColumn = count > 0;
        return _hasExtractedDataJsonColumn.Value;
    }

    public async Task CompleteSessionAsync(string sessionId, CallSessionFinalizationAudit audit, CancellationToken ct = default)
    {
        using var conn = _connectionFactory.CreateConnection();
        var setClauses = new List<string>
        {
            "EndedAt = @EndedAt",
            "FinalAction = @FinalAction",
            "FinalExtractedData = @FinalExtractedData"
        };

        if (await HasCallSessionColumnAsync(conn, "FinalReasonCode", ct))
            setClauses.Add("FinalReasonCode = @FinalReasonCode");

        if (await HasCallSessionColumnAsync(conn, "FinalReasonCategory", ct))
            setClauses.Add("FinalReasonCategory = @FinalReasonCategory");

        if (await HasCallSessionColumnAsync(conn, "FinalReasonMessage", ct))
            setClauses.Add("FinalReasonMessage = @FinalReasonMessage");

        if (await HasCallSessionColumnAsync(conn, "WebhookHttpStatus", ct))
            setClauses.Add("WebhookHttpStatus = @WebhookHttpStatus");

        if (await HasCallSessionColumnAsync(conn, "WebhookPayloadHash", ct))
            setClauses.Add("WebhookPayloadHash = @WebhookPayloadHash");

        if (await HasCallSessionColumnAsync(conn, "WebhookPayloadSentAt", ct))
            setClauses.Add("WebhookPayloadSentAt = @WebhookPayloadSentAt");

        if (await HasCallSessionColumnAsync(conn, "WebhookCorrelationId", ct))
            setClauses.Add("WebhookCorrelationId = @WebhookCorrelationId");

        if (await HasCallSessionColumnAsync(conn, "WebhookCorrelationField", ct))
            setClauses.Add("WebhookCorrelationField = @WebhookCorrelationField");

        var sql = $"""
            UPDATE CallSessions
            SET
                {string.Join(",\n                ", setClauses)}
            WHERE SessionId = @SessionId;
            """;

        var cmd = new CommandDefinition(
            sql,
            new
            {
                SessionId = sessionId,
                audit.EndedAt,
                audit.FinalAction,
                audit.FinalExtractedData,
                audit.FinalReasonCode,
                audit.FinalReasonCategory,
                audit.FinalReasonMessage,
                audit.WebhookHttpStatus,
                audit.WebhookPayloadHash,
                audit.WebhookPayloadSentAt,
                audit.WebhookCorrelationId,
                audit.WebhookCorrelationField
            },
            cancellationToken: ct);

        await conn.ExecuteAsync(cmd);
    }

    private async Task<bool> HasCallSessionColumnAsync(System.Data.IDbConnection conn, string columnName, CancellationToken ct)
    {
        if (_callSessionColumnCache.TryGetValue(columnName, out var cached))
            return cached;

        const string sql = """
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'CallSessions'
              AND COLUMN_NAME = @ColumnName;
            """;

        var cmd = new CommandDefinition(sql, new { ColumnName = columnName }, cancellationToken: ct);
        var count = await conn.ExecuteScalarAsync<long>(cmd);
        var exists = count > 0;
        _callSessionColumnCache[columnName] = exists;
        return exists;
    }

    public async Task<long> CountActiveSessionsAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM CallSessions
            WHERE EndedAt IS NULL;
            """;

        using var conn = _connectionFactory.CreateConnection();
        var cmd = new CommandDefinition(sql, cancellationToken: ct);
        return await conn.ExecuteScalarAsync<long>(cmd);
    }
}
