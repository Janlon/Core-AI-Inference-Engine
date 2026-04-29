using AIPort.Intelligence.Service.Domain.Models;
using AIPort.Intelligence.Service.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AIPort.Intelligence.Service.Controllers;

/// <summary>
/// Endpoint de inferência de IA para portaria virtual.
/// Recebe texto do visitante e retorna uma decisão estruturada.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class InferenceController : ControllerBase
{
    private readonly IDecisionEngine _decisionEngine;
    private readonly ILogger<InferenceController> _logger;

    public InferenceController(IDecisionEngine decisionEngine, ILogger<InferenceController> logger)
    {
        _decisionEngine = decisionEngine;
        _logger = logger;
    }

    /// <summary>
    /// Processa o texto do visitante e retorna uma decisão de portaria.
    /// </summary>
    /// <param name="request">Texto capturado pelo reconhecimento de voz + contexto do tenant.</param>
    /// <param name="ct">Token de cancelamento.</param>
    /// <returns>DecisionResult com intenção, dados extraídos, resposta de voz e ação para o orquestrador.</returns>
    /// <response code="200">Decisão processada com sucesso.</response>
    /// <response code="400">Requisição inválida (campos obrigatórios ausentes).</response>
    /// <response code="500">Erro interno no processamento.</response>
    [HttpPost("process")]
    [ProducesResponseType(typeof(DecisionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ProcessAsync(
        [FromBody] InferenceRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "POST /api/inference/process | Session={Session} | Tenant={Tenant} | TextLen={Len}",
            request.SessionId ?? "N/A", request.TenantType, request.Texto.Length);

        var result = await _decisionEngine.ProcessAsync(request, ct);

        _logger.LogInformation(
            "Decisão concluída | Camada={Camada} | Intenção={Int} | Ação={Acao} | Confiança={Conf:P0}",
            result.CamadaResolucao, result.Intencao, result.AcaoSugerida, result.Confianca);

        return Ok(result);
    }

    /// <summary>
    /// Retorna as regras de fluxo carregadas, agrupadas por tipo de tenant.
    /// Útil para diagnóstico e validação de configuração.
    /// </summary>
    [HttpGet("rules")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetRules([FromServices] Services.Interfaces.IRulesLoader rulesLoader)
    {
        var rules = rulesLoader.GetAll();
        return Ok(new { total = rules.Count, tenants = rules.Select(r => new { r.TenantType, r.DisplayName }) });
    }

    /// <summary>
    /// Retorna os templates de resposta de voz configurados para um tipo de tenant.
    /// Usado pelo Orquestrador para obter a saudação inicial, mensagem de espera e demais frases.
    /// </summary>
    /// <param name="tenantType">Tipo do tenant: residential, hospital, corporate, logistics.</param>
    /// <param name="rulesLoader">Serviço de carga de regras (injetado).</param>
    /// <response code="200">Templates de resposta para o tenant solicitado.</response>
    /// <response code="404">Tipo de tenant não encontrado.</response>
    [HttpGet("responses/{tenantType}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetResponses(string tenantType, [FromServices] Services.Interfaces.IRulesLoader rulesLoader)
    {
        var rule = rulesLoader.GetRule(tenantType);
        if (rule is null)
            return NotFound(new { message = $"Tipo de tenant '{tenantType}' não encontrado." });

        return Ok(rule.Responses);
    }

    /// <summary>
    /// Health-check do serviço incluindo o status dos provedores LLM habilitados.
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Health([FromServices] ILlmHealthService llmHealthService, CancellationToken ct)
    {
        var llm = await llmHealthService.GetHealthAsync(ct);
        var body = new
        {
            status = llm.Status,
            service = "AIPort.Intelligence.Service",
            utc = DateTime.UtcNow,
            llm
        };

        return llm.Status == "healthy"
            ? Ok(body)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, body);
    }
}
