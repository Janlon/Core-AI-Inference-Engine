using AIPort.Adapter.Orchestrator.Data.Entities;
using AIPort.Adapter.Orchestrator.Data.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace AIPort.Adapter.Orchestrator.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class TenantsController : ControllerBase
{
    private static readonly HashSet<string> AllowedTipos = new(StringComparer.OrdinalIgnoreCase)
    {
        "RESIDENCIAL",
        "COMERCIAL",
        "HOSPITALAR",
        "INDUSTRIAL"
    };

    private static readonly HashSet<string> AllowedAiProfiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "AGRESSIVO",
        "CONSERVADOR",
        "ULTRA_ESTAVEL"
    };

    private readonly ITenantRepository _tenantRepository;
    private readonly ILogger<TenantsController> _logger;

    public TenantsController(ITenantRepository tenantRepository, ILogger<TenantsController> logger)
    {
        _tenantRepository = tenantRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool includeInactive = true, CancellationToken cancellationToken = default)
    {
        try
        {
            var tenants = await _tenantRepository.ListAsync(includeInactive, cancellationToken);
            return Ok(tenants.Select(MapResponse));
        }
        catch (TaskCanceledException tce)
        {
            // Log de cancelamento por task
            ILogger logger = HttpContext.RequestServices.GetService(typeof(ILogger<TenantsController>)) as ILogger<TenantsController>;
            logger?.LogWarning(tce, "Task cancelada (TaskCanceledException) em List (TenantsController).");
            return StatusCode(499, "Task cancelada (TaskCanceledException).");
        }
        catch (OperationCanceledException oce)
        {
            // Log de cancelamento explícito
            ILogger logger = HttpContext.RequestServices.GetService(typeof(ILogger<TenantsController>)) as ILogger<TenantsController>;
            logger?.LogWarning(oce, "Requisição cancelada pelo cliente ou timeout atingido em List (TenantsController).");
            return StatusCode(499, "Requisição cancelada pelo cliente ou timeout atingido.");
        }
        catch (Exception ex)
        {
            ILogger logger = HttpContext.RequestServices.GetService(typeof(ILogger<TenantsController>)) as ILogger<TenantsController>;
            logger?.LogError(ex, "Erro inesperado em List (TenantsController).");
            throw;
        }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByIdAsync(id, cancellationToken);
        return tenant is null ? NotFound() : Ok(MapResponse(tenant));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertTenantRequest request, CancellationToken cancellationToken = default)
    {
        var validation = Validate(request);
        if (validation is not null)
            return validation;

        var existing = await _tenantRepository.GetByPidAsync(request.Pid, cancellationToken);
        if (existing is not null)
            return Conflict(new { message = $"Já existe tenant com Pid {request.Pid}." });

        var tenant = MapEntity(new Tenant { NomeIdentificador = request.NomeIdentificador.Trim(), TipoLocal = request.TipoLocal.Trim().ToUpperInvariant(), SystemType = request.SystemType.Trim() }, request);
        var id = await _tenantRepository.CreateAsync(tenant, cancellationToken);
        var created = await _tenantRepository.GetByIdAsync(id, cancellationToken);

        _logger.LogInformation("Tenant criado Id={Id} Pid={Pid}", id, request.Pid);
        return CreatedAtAction(nameof(GetById), new { id }, MapResponse(created!));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertTenantRequest request, CancellationToken cancellationToken = default)
    {
        var validation = Validate(request);
        if (validation is not null)
            return validation;

        var current = await _tenantRepository.GetByIdAsync(id, cancellationToken);
        if (current is null)
            return NotFound();

        var pidOwner = await _tenantRepository.GetByPidAsync(request.Pid, cancellationToken);
        if (pidOwner is not null && pidOwner.Id != id)
            return Conflict(new { message = $"Já existe outro tenant com Pid {request.Pid}." });

        var updated = MapEntity(current, request);
        var success = await _tenantRepository.UpdateAsync(updated, cancellationToken);
        if (!success)
            return NotFound();

        _logger.LogInformation("Tenant atualizado Id={Id} Pid={Pid}", id, request.Pid);
        return Ok(MapResponse(updated));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken = default)
    {
        var success = await _tenantRepository.DeactivateAsync(id, cancellationToken);
        if (!success)
            return NotFound();

        _logger.LogInformation("Tenant desativado Id={Id}", id);
        return NoContent();
    }

    private IActionResult? Validate(UpsertTenantRequest request)
    {
        if (request.Pid <= 0)
            return BadRequest(new { message = "Pid deve ser maior que zero." });

        if (string.IsNullOrWhiteSpace(request.NomeIdentificador))
            return BadRequest(new { message = "NomeIdentificador é obrigatório." });

        if (string.IsNullOrWhiteSpace(request.SystemType))
            return BadRequest(new { message = "SystemType é obrigatório." });

        if (string.IsNullOrWhiteSpace(request.TipoLocal) || !AllowedTipos.Contains(request.TipoLocal))
            return BadRequest(new { message = "TipoLocal inválido." });

        if (string.IsNullOrWhiteSpace(request.AiProfile) || !AllowedAiProfiles.Contains(request.AiProfile))
            return BadRequest(new { message = "AiProfile inválido. Use AGRESSIVO, CONSERVADOR ou ULTRA_ESTAVEL." });

        if (!IsValidThreshold(request.AiRegexConfidenceThreshold)
            || !IsValidThreshold(request.AiNlpConfidenceThreshold)
            || !IsValidThreshold(request.AiGlobalConfidenceThreshold))
            return BadRequest(new { message = "Thresholds de IA devem estar entre 0.0 e 1.0." });

        return null;
    }

    private static Tenant MapEntity(Tenant tenant, UpsertTenantRequest request)
    {
        tenant.Pid = request.Pid;
        tenant.NomeIdentificador = request.NomeIdentificador.Trim();
        tenant.TipoLocal = request.TipoLocal.Trim().ToUpperInvariant();
        tenant.SystemType = request.SystemType.Trim();
        tenant.WebhookUrl = NullIfWhiteSpace(request.WebhookUrl);
        tenant.ApiToken = NullIfWhiteSpace(request.ApiToken);
        tenant.SipTrunkPrefix = NullIfWhiteSpace(request.SipTrunkPrefix);
        tenant.RamalTransfHumano = NullIfWhiteSpace(request.RamalTransfHumano);
        tenant.UsaBloco = request.UsaBloco;
        tenant.UsaTorre = request.UsaTorre;
        tenant.RecordingEnabled = request.RecordingEnabled;
        tenant.AiProfile = request.AiProfile.Trim().ToUpperInvariant();
        tenant.AiRegexConfidenceThreshold = request.AiRegexConfidenceThreshold;
        tenant.AiNlpConfidenceThreshold = request.AiNlpConfidenceThreshold;
        tenant.AiGlobalConfidenceThreshold = request.AiGlobalConfidenceThreshold;
        tenant.IsActive = request.IsActive;
        return tenant;
    }

    private static object MapResponse(Tenant tenant) => new
    {
        tenant.Id,
        tenant.Pid,
        tenant.NomeIdentificador,
        tenant.TipoLocal,
        tenant.SystemType,
        tenant.WebhookUrl,
        tenant.ApiToken,
        tenant.SipTrunkPrefix,
        tenant.RamalTransfHumano,
        tenant.UsaBloco,
        tenant.UsaTorre,
        tenant.RecordingEnabled,
        tenant.AiProfile,
        tenant.AiRegexConfidenceThreshold,
        tenant.AiNlpConfidenceThreshold,
        tenant.AiGlobalConfidenceThreshold,
        tenant.IsActive,
        tenant.CreatedAt
    };

    private static bool IsValidThreshold(double? value)
        => !value.HasValue || (value.Value >= 0.0 && value.Value <= 1.0);

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class UpsertTenantRequest
{
    public int Pid { get; set; }
    public string NomeIdentificador { get; set; } = string.Empty;
    public string TipoLocal { get; set; } = "RESIDENCIAL";
    public string SystemType { get; set; } = "condominio";
    public string? WebhookUrl { get; set; }
    public string? ApiToken { get; set; }
    public string? SipTrunkPrefix { get; set; }
    public string? RamalTransfHumano { get; set; }
    public bool UsaBloco { get; set; }
    public bool UsaTorre { get; set; }
    public bool RecordingEnabled { get; set; }
    public string AiProfile { get; set; } = "CONSERVADOR";
    public double? AiRegexConfidenceThreshold { get; set; }
    public double? AiNlpConfidenceThreshold { get; set; }
    public double? AiGlobalConfidenceThreshold { get; set; }
    public bool IsActive { get; set; } = true;
}