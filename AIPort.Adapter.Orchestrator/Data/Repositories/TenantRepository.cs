using Dapper;
using AIPort.Adapter.Orchestrator.Data.Entities;

namespace AIPort.Adapter.Orchestrator.Data.Repositories;

public sealed class TenantRepository : ITenantRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public TenantRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<Tenant>> ListAsync(bool includeInactive = true, CancellationToken ct = default)
    {
        var sql = includeInactive
            ? """
            SELECT
                Id,
                Pid,
                NomeIdentificador,
                TipoLocal,
                SystemType,
                WebhookUrl,
                ApiToken,
                SipTrunkPrefix,
                RamalTransfHumano,
                UsaBloco,
                UsaTorre,
                RecordingEnabled,
                AiProfile,
                AiRegexConfidenceThreshold,
                AiNlpConfidenceThreshold,
                AiGlobalConfidenceThreshold,
                IsActive,
                CreatedAt
            FROM Tenants
            ORDER BY IsActive DESC, NomeIdentificador ASC;
            """
            : """
            SELECT
                Id,
                Pid,
                NomeIdentificador,
                TipoLocal,
                SystemType,
                WebhookUrl,
                ApiToken,
                SipTrunkPrefix,
                RamalTransfHumano,
                UsaBloco,
                UsaTorre,
                RecordingEnabled,
                AiProfile,
                AiRegexConfidenceThreshold,
                AiNlpConfidenceThreshold,
                AiGlobalConfidenceThreshold,
                IsActive,
                CreatedAt
            FROM Tenants
            WHERE IsActive = 1
            ORDER BY NomeIdentificador ASC;
            """;

        using var conn = _connectionFactory.CreateConnection();
        var cmd = new CommandDefinition(sql, cancellationToken: ct);
        var items = await conn.QueryAsync<Tenant>(cmd);
        return items.ToList();
    }

    public async Task<Tenant?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                Id,
                Pid,
                NomeIdentificador,
                TipoLocal,
                SystemType,
                WebhookUrl,
                ApiToken,
                SipTrunkPrefix,
                RamalTransfHumano,
                UsaBloco,
                UsaTorre,
                RecordingEnabled,
                AiProfile,
                AiRegexConfidenceThreshold,
                AiNlpConfidenceThreshold,
                AiGlobalConfidenceThreshold,
                IsActive,
                CreatedAt
            FROM Tenants
            WHERE Id = @Id
            LIMIT 1;
            """;

        using var conn = _connectionFactory.CreateConnection();
        var cmd = new CommandDefinition(sql, new { Id = id }, cancellationToken: ct);
        return await conn.QueryFirstOrDefaultAsync<Tenant>(cmd);
    }

    public async Task<Tenant?> GetByPidAsync(int pid, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                Id,
                Pid,
                NomeIdentificador,
                TipoLocal,
                SystemType,
                WebhookUrl,
                ApiToken,
                SipTrunkPrefix,
                RamalTransfHumano,
                UsaBloco,
                UsaTorre,
                RecordingEnabled,
                AiProfile,
                AiRegexConfidenceThreshold,
                AiNlpConfidenceThreshold,
                AiGlobalConfidenceThreshold,
                IsActive,
                CreatedAt
            FROM Tenants t
            WHERE t.Pid = @Pid AND t.IsActive = 1
            LIMIT 1;
            """;

        using var conn = _connectionFactory.CreateConnection();
        var cmd = new CommandDefinition(sql, new { Pid = pid }, cancellationToken: ct);
        return await conn.QueryFirstOrDefaultAsync<Tenant>(cmd);
    }

    public async Task<int> CreateAsync(Tenant tenant, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO Tenants
            (
                Pid,
                NomeIdentificador,
                TipoLocal,
                SystemType,
                WebhookUrl,
                ApiToken,
                SipTrunkPrefix,
                RamalTransfHumano,
                UsaBloco,
                UsaTorre,
                RecordingEnabled,
                AiProfile,
                AiRegexConfidenceThreshold,
                AiNlpConfidenceThreshold,
                AiGlobalConfidenceThreshold,
                IsActive
            )
            VALUES
            (
                @Pid,
                @NomeIdentificador,
                @TipoLocal,
                @SystemType,
                @WebhookUrl,
                @ApiToken,
                @SipTrunkPrefix,
                @RamalTransfHumano,
                @UsaBloco,
                @UsaTorre,
                @RecordingEnabled,
                @AiProfile,
                @AiRegexConfidenceThreshold,
                @AiNlpConfidenceThreshold,
                @AiGlobalConfidenceThreshold,
                @IsActive
            );

            SELECT LAST_INSERT_ID();
            """;

        using var conn = _connectionFactory.CreateConnection();
        var cmd = new CommandDefinition(sql, tenant, cancellationToken: ct);
        return await conn.ExecuteScalarAsync<int>(cmd);
    }

    public async Task<bool> UpdateAsync(Tenant tenant, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE Tenants
            SET
                Pid = @Pid,
                NomeIdentificador = @NomeIdentificador,
                TipoLocal = @TipoLocal,
                SystemType = @SystemType,
                WebhookUrl = @WebhookUrl,
                ApiToken = @ApiToken,
                SipTrunkPrefix = @SipTrunkPrefix,
                RamalTransfHumano = @RamalTransfHumano,
                UsaBloco = @UsaBloco,
                UsaTorre = @UsaTorre,
                RecordingEnabled = @RecordingEnabled,
                AiProfile = @AiProfile,
                AiRegexConfidenceThreshold = @AiRegexConfidenceThreshold,
                AiNlpConfidenceThreshold = @AiNlpConfidenceThreshold,
                AiGlobalConfidenceThreshold = @AiGlobalConfidenceThreshold,
                IsActive = @IsActive
            WHERE Id = @Id;
            """;

        using var conn = _connectionFactory.CreateConnection();
        var cmd = new CommandDefinition(sql, tenant, cancellationToken: ct);
        return await conn.ExecuteAsync(cmd) > 0;
    }

    public async Task<bool> DeactivateAsync(int id, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE Tenants
            SET IsActive = 0
            WHERE Id = @Id;
            """;

        using var conn = _connectionFactory.CreateConnection();
        var cmd = new CommandDefinition(sql, new { Id = id }, cancellationToken: ct);
        return await conn.ExecuteAsync(cmd) > 0;
    }
}
