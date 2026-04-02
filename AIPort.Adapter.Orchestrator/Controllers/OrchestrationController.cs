using AIPort.Adapter.Orchestrator.Domain.Models;
using AIPort.Adapter.Orchestrator.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AIPort.Adapter.Orchestrator.Controllers;

[ApiController]
[Route("api/orchestration")]
public sealed class OrchestrationController : ControllerBase
{
    private readonly IOrchestrationService _orchestration;

    public OrchestrationController(IOrchestrationService orchestration)
    {
        _orchestration = orchestration;
    }

    [HttpPost("simulate")]
    public async Task<ActionResult<CallOrchestrationResult>> SimulateAsync([FromBody] AgiCallContext context, CancellationToken ct)
    {
        if (context.TenantPid <= 0)
            return BadRequest("tenantPid é obrigatório e deve ser maior que zero.");

        if (string.IsNullOrWhiteSpace(context.SessionId))
            context.SessionId = Guid.NewGuid().ToString("N");

        if (string.IsNullOrWhiteSpace(context.UniqueId))
            context.UniqueId = context.SessionId;

        var result = await _orchestration.HandleCallAsync(context, ct);
        return result.Sucesso ? Ok(result) : StatusCode(StatusCodes.Status500InternalServerError, result);
    }
}
