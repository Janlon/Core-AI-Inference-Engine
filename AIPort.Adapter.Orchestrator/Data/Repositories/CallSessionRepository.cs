using Dapper;
using AIPort.Adapter.Orchestrator.Data.Entities;

namespace AIPort.Adapter.Orchestrator.Data.Repositories;

public sealed class CallSessionRepository : ICallSessionRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private bool? _hasResolutionLayerColumn;
    private bool? _hasExtractedDataJsonColumn;

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

    public async Task CompleteSessionAsync(string sessionId, string finalAction, string finalExtractedData, DateTime endedAt, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE CallSessions
            SET
                EndedAt = @EndedAt,
                FinalAction = @FinalAction,
                FinalExtractedData = @FinalExtractedData
            WHERE SessionId = @SessionId;
            """;

        using var conn = _connectionFactory.CreateConnection();
        var cmd = new CommandDefinition(
            sql,
            new
            {
                SessionId = sessionId,
                EndedAt = endedAt,
                FinalAction = finalAction,
                FinalExtractedData = finalExtractedData
            },
            cancellationToken: ct);

        await conn.ExecuteAsync(cmd);
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
