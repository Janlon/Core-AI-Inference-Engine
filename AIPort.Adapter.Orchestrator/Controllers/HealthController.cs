using AIPort.Adapter.Orchestrator.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AIPort.Adapter.Orchestrator.Controllers;

/// <summary>
/// Controller para verificação de saúde e disponibilidade do serviço
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly IHealthCheckService _healthCheckService;
    private readonly ILogger<HealthController> _logger;

    public HealthController(IHealthCheckService healthCheckService, ILogger<HealthController> logger)
    {
        _healthCheckService = healthCheckService ?? throw new ArgumentNullException(nameof(healthCheckService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Obtém o status geral de saúde no endpoint curto (/api/health)
    /// </summary>
    [HttpGet("")]
    public async Task<IActionResult> GetStatusAlias(CancellationToken cancellationToken = default)
    {
        var status = await _healthCheckService.GetHealthStatusAsync(cancellationToken);
        _logger.LogInformation("Health status consultado via GET /api/health");
        return Ok(status);
    }

    /// <summary>
    /// Obtém o status geral de saúde do serviço
    /// </summary>
    /// <remarks>
    /// Retorna informações sobre a saúde geral incluindo estado do banco de dados
    /// </remarks>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken = default)
    {
        var status = await _healthCheckService.GetHealthStatusAsync(cancellationToken);
        _logger.LogInformation("Health status consultado via GET /api/health/status");
        return Ok(status);
    }

    /// <summary>
    /// Verifica especificamente a conexão com o banco de dados
    /// </summary>
    /// <remarks>
    /// Testa se há uma conexão disponível e funcional com o MariaDB
    /// </remarks>
    [HttpGet("database")]
    public async Task<IActionResult> GetDatabaseHealth([FromQuery] int minActiveTenants = 1, CancellationToken cancellationToken = default)
    {
        if (minActiveTenants < 0)
        {
            return BadRequest(new { message = "O parâmetro minActiveTenants deve ser maior ou igual a 0." });
        }

        var databaseHealth = await _healthCheckService.CheckDatabaseHealthAsync(minActiveTenants, cancellationToken);

        if (!databaseHealth.IsReachable)
        {
            _logger.LogWarning("Verificação de banco de dados: Falha na conexão");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                status = "unhealthy",
                component = "database",
                reachable = false,
                activeTenantsCount = databaseHealth.ActiveTenantsCount,
                minActiveTenants = databaseHealth.MinActiveTenants,
                hasMinimumActiveTenants = databaseHealth.HasMinimumActiveTenants,
                message = databaseHealth.Message,
                timestamp = DateTime.UtcNow
            });
        }

        _logger.LogInformation(
            "Verificação de banco de dados: {Status}. ActiveTenants={ActiveTenantsCount}, MinExpected={MinActiveTenants}",
            databaseHealth.Status,
            databaseHealth.ActiveTenantsCount,
            databaseHealth.MinActiveTenants);

        return Ok(new
        {
            status = databaseHealth.Status,
            component = "database",
            reachable = true,
            activeTenantsCount = databaseHealth.ActiveTenantsCount,
            minActiveTenants = databaseHealth.MinActiveTenants,
            hasMinimumActiveTenants = databaseHealth.HasMinimumActiveTenants,
            message = databaseHealth.Message,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Verifica se o serviço está em execução (mesmo sem banco de dados)
    /// </summary>
    /// <remarks>
    /// Endpoint simples que apenas retorna OK se o serviço está respondendo
    /// </remarks>
    [HttpGet("live")]
    public IActionResult GetLiveness()
    {
        _logger.LogDebug("Verificação de liveness consultada");
        return Ok(new
        {
            status = "alive",
            service = "AIPort.Adapter.Orchestrator",
            timestamp = DateTime.UtcNow
        });
    }
}
