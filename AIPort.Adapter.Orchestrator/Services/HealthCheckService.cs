using AIPort.Adapter.Orchestrator.Data;
using AIPort.Adapter.Orchestrator.Agi.Interfaces;
using AIPort.Adapter.Orchestrator.Config;
using AIPort.Adapter.Orchestrator.Services.Interfaces;
using MySqlConnector;
using Microsoft.Extensions.Options;

namespace AIPort.Adapter.Orchestrator.Services;

/// <summary>
/// Implementação do serviço de health check
/// Responsável por validar a disponibilidade de componentes críticos
/// </summary>
public sealed class HealthCheckService : IHealthCheckService
{
    private readonly IDbConnectionFactory _dbConnectionFactory;
    private readonly IAgiRuntimeState _agiRuntimeState;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IntelligenceServiceOptions _intelligenceOptions;
    private readonly ILogger<HealthCheckService> _logger;
    private const string CountActiveTenantsSql = "SELECT COUNT(*) FROM Tenants WHERE IsActive = 1";

    public HealthCheckService(
        IDbConnectionFactory dbConnectionFactory,
        IAgiRuntimeState agiRuntimeState,
        IHttpClientFactory httpClientFactory,
        IOptions<IntelligenceServiceOptions> intelligenceOptions,
        ILogger<HealthCheckService> logger)
    {
        _dbConnectionFactory = dbConnectionFactory ?? throw new ArgumentNullException(nameof(dbConnectionFactory));
        _agiRuntimeState = agiRuntimeState ?? throw new ArgumentNullException(nameof(agiRuntimeState));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _intelligenceOptions = intelligenceOptions?.Value ?? throw new ArgumentNullException(nameof(intelligenceOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DatabaseHealthResult> CheckDatabaseHealthAsync(int minActiveTenants = 1, CancellationToken cancellationToken = default)
    {
        if (minActiveTenants < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minActiveTenants), "minActiveTenants deve ser maior ou igual a 0.");
        }

        try
        {
            var activeTenantsCount = await GetActiveTenantsCountAsync(cancellationToken);
            var hasMinimumActiveTenants = activeTenantsCount >= minActiveTenants;
            var status = hasMinimumActiveTenants ? "healthy" : "degraded";
            var message = hasMinimumActiveTenants
                ? null
                : $"Comunicação com banco OK, mas total de tenants ativos ({activeTenantsCount}) abaixo do mínimo esperado ({minActiveTenants}).";

            _logger.LogInformation(
                "Health check DB concluído com sucesso. ActiveTenants={ActiveTenantsCount}, MinExpected={MinActiveTenants}, Status={Status}",
                activeTenantsCount,
                minActiveTenants,
                status);

            return new DatabaseHealthResult(
                IsReachable: true,
                ActiveTenantsCount: activeTenantsCount,
                MinActiveTenants: minActiveTenants,
                HasMinimumActiveTenants: hasMinimumActiveTenants,
                Status: status,
                Message: message);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Verificação de conexão com banco de dados foi cancelada");
            return new DatabaseHealthResult(
                IsReachable: false,
                ActiveTenantsCount: 0,
                MinActiveTenants: minActiveTenants,
                HasMinimumActiveTenants: false,
                Status: "unhealthy",
                Message: "Verificação cancelada.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao verificar conexão com banco de dados: {ErrorMessage}", ex.Message);
            return new DatabaseHealthResult(
                IsReachable: false,
                ActiveTenantsCount: 0,
                MinActiveTenants: minActiveTenants,
                HasMinimumActiveTenants: false,
                Status: "unhealthy",
                Message: "Falha ao conectar ou consultar banco de dados.");
        }
    }

    /// <summary>
    /// Verifica se a conexão com o banco de dados MariaDB está disponível
    /// </summary>
    public async Task<bool> IsDatabaseHealthyAsync(CancellationToken cancellationToken = default)
    {
        var health = await CheckDatabaseHealthAsync(0, cancellationToken);
        return health.IsReachable;
    }

    /// <summary>
    /// Obtém o status geral de saúde do serviço
    /// </summary>
    public async Task<IDictionary<string, object>> GetHealthStatusAsync(CancellationToken cancellationToken = default)
    {
        var databaseHealth = await CheckDatabaseHealthAsync(1, cancellationToken);
        var activeCalls = _agiRuntimeState.ActiveChannels;
        var aiHealth = await ProbeIntelligenceServiceAsync(cancellationToken);

        var asteriskStatus = !_agiRuntimeState.IsEnabled
            ? "disabled"
            : _agiRuntimeState.IsListening
                ? "healthy"
                : "unhealthy";

        var overall = !databaseHealth.IsReachable || aiHealth.Status == "unhealthy" || (_agiRuntimeState.IsEnabled && !_agiRuntimeState.IsListening)
            ? "unhealthy"
            : databaseHealth.Status == "degraded"
                ? "degraded"
                : "healthy";

        var status = new Dictionary<string, object>
        {
            ["service"] = "AIPort.Adapter.Orchestrator",
            ["timestamp"] = DateTime.UtcNow,
            ["version"] = "1.0.0",
            ["activeCalls"] = activeCalls,
            ["asterisk"] = new
            {
                status = asteriskStatus,
                port = _agiRuntimeState.Port,
                host = _agiRuntimeState.Host,
                enabled = _agiRuntimeState.IsEnabled,
                listening = _agiRuntimeState.IsListening,
                activeCalls
            },
            ["database"] = new
            {
                status = databaseHealth.Status,
                reachable = databaseHealth.IsReachable,
                activeTenantsCount = databaseHealth.ActiveTenantsCount,
                minActiveTenants = databaseHealth.MinActiveTenants,
                hasMinimumActiveTenants = databaseHealth.HasMinimumActiveTenants,
                message = databaseHealth.Message,
                timestamp = DateTime.UtcNow
            },
            ["ai"] = new
            {
                status = aiHealth.Status,
                baseUrl = _intelligenceOptions.BaseUrl,
                latencyMs = aiHealth.LatencyMs,
                message = aiHealth.Message
            },
            ["overall"] = overall
        };

        return status;
    }

    private async Task<(string Status, long? LatencyMs, string? Message)> ProbeIntelligenceServiceAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_intelligenceOptions.BaseUrl))
        {
            return ("unconfigured", null, "BaseUrl do Intelligence Service não configurada.");
        }

        try
        {
            using var http = _httpClientFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(3);
            var uri = new Uri(new Uri(_intelligenceOptions.BaseUrl.TrimEnd('/') + "/"), "health");
            var started = Environment.TickCount64;
            using var response = await http.GetAsync(uri, cancellationToken);
            var elapsed = Environment.TickCount64 - started;

            return response.IsSuccessStatusCode
                ? ("healthy", elapsed, null)
                : ("unhealthy", elapsed, $"HTTP {(int)response.StatusCode}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Falha ao verificar Intelligence Service.");
            return ("unhealthy", null, ex.Message);
        }
    }

    private async Task<long> GetActiveTenantsCountAsync(CancellationToken cancellationToken)
    {
        using var connection = _dbConnectionFactory.CreateConnection();

        if (connection is MySqlConnection mysqlConnection)
        {
            // Health check estrito: desabilita pool e força ida ao servidor.
            var csb = new MySqlConnectionStringBuilder(mysqlConnection.ConnectionString)
            {
                Pooling = false,
                ConnectionTimeout = 3,
                DefaultCommandTimeout = 3
            };

            await using var strictConnection = new MySqlConnection(csb.ConnectionString);
            await strictConnection.OpenAsync(cancellationToken);
            await using var command = strictConnection.CreateCommand();
            command.CommandText = CountActiveTenantsSql;
            command.CommandTimeout = 3;
            var raw = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt64(raw ?? 0);
        }

        // Fallback para outros providers de IDbConnection.
        return await Task.Run(() =>
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = CountActiveTenantsSql;
            var raw = command.ExecuteScalar();
            connection.Close();
            return Convert.ToInt64(raw ?? 0);
        }, cancellationToken);
    }
}
