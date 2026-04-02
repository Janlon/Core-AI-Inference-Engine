using AIPort.Adapter.Orchestrator.Data.Entities;
using AIPort.Adapter.Orchestrator.Data.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace AIPort.Adapter.Orchestrator.Controllers;

[ApiController]
[Route("api/tenants/{tenantId:int}/ai-config")]
public sealed class TenantAiConfigController : ControllerBase
{
    private static readonly HashSet<string> AllowedAiProfiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "AGRESSIVO",
        "CONSERVADOR",
        "ULTRA_ESTAVEL"
    };

    private readonly ITenantRepository _tenantRepository;
    private readonly ILogger<TenantAiConfigController> _logger;

    public TenantAiConfigController(ITenantRepository tenantRepository, ILogger<TenantAiConfigController> logger)
    {
        _tenantRepository = tenantRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAiConfig(int tenantId, CancellationToken ct = default)
    {
        var tenant = await _tenantRepository.GetByIdAsync(tenantId, ct);
        if (tenant is null)
            return NotFound(new { message = "Tenant não encontrado." });

        return Ok(new
        {
            tenantId = tenant.Id,
            aiProfile = tenant.AiProfile,
            aiRegexConfidenceThreshold = tenant.AiRegexConfidenceThreshold,
            aiNlpConfidenceThreshold = tenant.AiNlpConfidenceThreshold,
            aiGlobalConfidenceThreshold = tenant.AiGlobalConfidenceThreshold
        });
    }

    [HttpPatch]
    public async Task<IActionResult> UpdateAiConfig(int tenantId, [FromBody] UpdateAiConfigRequest request, CancellationToken ct = default)
    {
        var validation = Validate(request);
        if (validation is not null)
            return validation;

        var tenant = await _tenantRepository.GetByIdAsync(tenantId, ct);
        if (tenant is null)
            return NotFound(new { message = "Tenant não encontrado." });

        var updated = false;

        if (!string.IsNullOrWhiteSpace(request.AiProfile))
        {
            tenant.AiProfile = request.AiProfile.Trim().ToUpperInvariant();
            updated = true;
        }

        if (request.AiRegexConfidenceThreshold.HasValue)
        {
            tenant.AiRegexConfidenceThreshold = request.AiRegexConfidenceThreshold.Value;
            updated = true;
        }

        if (request.AiNlpConfidenceThreshold.HasValue)
        {
            tenant.AiNlpConfidenceThreshold = request.AiNlpConfidenceThreshold.Value;
            updated = true;
        }

        if (request.AiGlobalConfidenceThreshold.HasValue)
        {
            tenant.AiGlobalConfidenceThreshold = request.AiGlobalConfidenceThreshold.Value;
            updated = true;
        }

        if (!updated)
            return BadRequest(new { message = "Nenhum campo de configuração de IA foi enviado para atualização." });

        var success = await _tenantRepository.UpdateAsync(tenant, ct);
        if (!success)
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Falha ao atualizar tenant." });

        _logger.LogInformation(
            "Configuração de IA do tenant Id={TenantId} atualizada: Profile={Profile}, RegexThr={RegexThr}, NlpThr={NlpThr}, GlobalThr={GlobalThr}",
            tenantId,
            tenant.AiProfile,
            tenant.AiRegexConfidenceThreshold,
            tenant.AiNlpConfidenceThreshold,
            tenant.AiGlobalConfidenceThreshold);

        return Ok(new
        {
            message = "Configuração de IA atualizada com sucesso.",
            tenantId = tenant.Id,
            aiProfile = tenant.AiProfile,
            aiRegexConfidenceThreshold = tenant.AiRegexConfidenceThreshold,
            aiNlpConfidenceThreshold = tenant.AiNlpConfidenceThreshold,
            aiGlobalConfidenceThreshold = tenant.AiGlobalConfidenceThreshold
        });
    }

    private IActionResult? Validate(UpdateAiConfigRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.AiProfile)
            && !request.AiRegexConfidenceThreshold.HasValue
            && !request.AiNlpConfidenceThreshold.HasValue
            && !request.AiGlobalConfidenceThreshold.HasValue)
            return BadRequest(new { message = "Pelo menos um campo deve ser fornecido para atualização." });

        if (!string.IsNullOrWhiteSpace(request.AiProfile) && !AllowedAiProfiles.Contains(request.AiProfile))
            return BadRequest(new { message = "AiProfile inválido. Use AGRESSIVO, CONSERVADOR ou ULTRA_ESTAVEL." });

        if (!IsValidThreshold(request.AiRegexConfidenceThreshold)
            || !IsValidThreshold(request.AiNlpConfidenceThreshold)
            || !IsValidThreshold(request.AiGlobalConfidenceThreshold))
            return BadRequest(new { message = "Thresholds de IA devem estar entre 0.0 e 1.0." });

        return null;
    }

    private static bool IsValidThreshold(double? value)
        => !value.HasValue || (value.Value >= 0.0 && value.Value <= 1.0);
}

public sealed class UpdateAiConfigRequest
{
    public string? AiProfile { get; set; }
    public double? AiRegexConfidenceThreshold { get; set; }
    public double? AiNlpConfidenceThreshold { get; set; }
    public double? AiGlobalConfidenceThreshold { get; set; }
}
