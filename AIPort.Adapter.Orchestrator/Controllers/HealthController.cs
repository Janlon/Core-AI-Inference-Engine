using AIPort.Adapter.Orchestrator.Services.Interfaces;
using AIPort.Adapter.Orchestrator.Config;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace AIPort.Adapter.Orchestrator.Controllers;

/// <summary>
/// Controller para verificação de saúde e disponibilidade do serviço
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly IHealthCheckService _healthCheckService;
    private readonly MaintenanceOptions _maintenanceOptions;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        IHealthCheckService healthCheckService,
        IOptions<MaintenanceOptions> maintenanceOptions,
        ILogger<HealthController> logger)
    {
        _healthCheckService = healthCheckService ?? throw new ArgumentNullException(nameof(healthCheckService));
        _maintenanceOptions = maintenanceOptions?.Value ?? throw new ArgumentNullException(nameof(maintenanceOptions));
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

    [HttpPost("monitor/start")]
    public async Task<IActionResult> StartOrchestratorMonitor(CancellationToken cancellationToken = default)
    {
        if (!_maintenanceOptions.EnableServiceControl)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Controle administrativo de serviços está desabilitado neste ambiente.",
                commandConfigured = !string.IsNullOrWhiteSpace(_maintenanceOptions.StartOrchestratorMonitorCommand)
            });
        }

        if (string.IsNullOrWhiteSpace(_maintenanceOptions.StartOrchestratorMonitorCommand))
        {
            return StatusCode(StatusCodes.Status501NotImplemented, new
            {
                message = "Comando para iniciar o monitor do orquestrador não foi configurado."
            });
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            ArgumentList = { "-lc", _maintenanceOptions.StartOrchestratorMonitorCommand },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        try
        {
            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            var output = string.Join("\n", new[] { stdout, stderr }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();

            if (process.ExitCode != 0)
            {
                _logger.LogWarning(
                    "Falha ao iniciar monitor do orquestrador. ExitCode={ExitCode} Output={Output}",
                    process.ExitCode,
                    output);

                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    message = "Falha ao iniciar o monitor do orquestrador.",
                    exitCode = process.ExitCode,
                    output
                });
            }

            _logger.LogInformation("Monitor do orquestrador iniciado manualmente via API administrativa.");
            return Ok(new
            {
                message = "Monitor do orquestrador iniciado com sucesso.",
                exitCode = process.ExitCode,
                output
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao iniciar monitor do orquestrador via API administrativa.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Erro inesperado ao iniciar o monitor do orquestrador.",
                detail = ex.Message
            });
        }
    }
}
