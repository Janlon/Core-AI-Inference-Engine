using Dapper;
using AIPort.Adapter.Orchestrator.Data;
using AIPort.Adapter.Orchestrator.Data.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace AIPort.Adapter.Orchestrator.Controllers;

[ApiController]
[Route("api/tenants/{tenantId:int}/ai")]
public sealed class TenantAiMetricsController : ControllerBase
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IDbConnectionFactory _connectionFactory;

    public TenantAiMetricsController(ITenantRepository tenantRepository, IDbConnectionFactory connectionFactory)
    {
        _tenantRepository = tenantRepository;
        _connectionFactory = connectionFactory;
    }

    [HttpGet("metrics")]
    public async Task<IActionResult> GetMetrics(int tenantId, [FromQuery] int days = 7, CancellationToken ct = default)
    {
        var tenant = await _tenantRepository.GetByIdAsync(tenantId, ct);
        if (tenant is null)
            return NotFound(new { message = "Tenant não encontrado." });

        var windowDays = Math.Clamp(days, 1, 30);

        const string sessionsSql = """
            SELECT
              COUNT(*) AS TotalSessions,
              SUM(CASE WHEN FinalAction = 'ESCALAR_HUMANO' THEN 1 ELSE 0 END) AS EscalarHumanoSessions,
              AVG(TIMESTAMPDIFF(SECOND, StartedAt, COALESCE(EndedAt, UTC_TIMESTAMP()))) AS AvgSessionSeconds
            FROM CallSessions
            WHERE TenantId = @TenantId
              AND StartedAt >= DATE_SUB(UTC_TIMESTAMP(), INTERVAL @WindowDays DAY);
            """;

        const string interactionsSql = """
            SELECT
              COUNT(*) AS TotalInteractions,
              SUM(CASE WHEN ResolutionLayer LIKE 'LLM%' THEN 1 ELSE 0 END) AS LlmInteractions,
              AVG(NULLIF(LlmProcessingTimeMs, 0)) AS AvgLlmProcessingMs,
              AVG(InteractionDurationMs) AS AvgInteractionDurationMs
            FROM CallInteractions ci
            INNER JOIN CallSessions cs ON cs.SessionId = ci.SessionId
            WHERE cs.TenantId = @TenantId
              AND cs.StartedAt >= DATE_SUB(UTC_TIMESTAMP(), INTERVAL @WindowDays DAY);
            """;

        using var conn = _connectionFactory.CreateConnection();
        var args = new { TenantId = tenantId, WindowDays = windowDays };

        var sessionMetrics = await conn.QueryFirstAsync<SessionMetricsRow>(
            new CommandDefinition(sessionsSql, args, cancellationToken: ct));

        var interactionMetrics = await conn.QueryFirstAsync<InteractionMetricsRow>(
            new CommandDefinition(interactionsSql, args, cancellationToken: ct));

        var totalSessions = sessionMetrics.TotalSessions;
        var escalatedRate = totalSessions == 0
            ? 0.0
            : (double)sessionMetrics.EscalarHumanoSessions / totalSessions;

        var totalInteractions = interactionMetrics.TotalInteractions;
        var llmRate = totalInteractions == 0
            ? 0.0
            : (double)interactionMetrics.LlmInteractions / totalInteractions;

        var suggestedProfile = SuggestProfile(escalatedRate, llmRate, interactionMetrics.AvgLlmProcessingMs);

        return Ok(new
        {
            tenant = new
            {
                tenant.Id,
                tenant.Pid,
                tenant.NomeIdentificador,
                tenant.AiProfile,
                tenant.AiRegexConfidenceThreshold,
                tenant.AiNlpConfidenceThreshold,
                tenant.AiGlobalConfidenceThreshold
            },
            timeWindowDays = windowDays,
            metrics = new
            {
                totalSessions,
                escalatedSessions = sessionMetrics.EscalarHumanoSessions,
                escalatedRate,
                totalInteractions,
                llmInteractions = interactionMetrics.LlmInteractions,
                llmUsageRate = llmRate,
                avgLlmProcessingMs = interactionMetrics.AvgLlmProcessingMs,
                avgInteractionDurationMs = interactionMetrics.AvgInteractionDurationMs,
                avgSessionSeconds = sessionMetrics.AvgSessionSeconds
            },
            suggestion = new
            {
                suggestedProfile,
                reason = BuildSuggestionReason(suggestedProfile, escalatedRate, llmRate, interactionMetrics.AvgLlmProcessingMs)
            }
        });
    }

    private static string SuggestProfile(double escalatedRate, double llmRate, double? avgLlmProcessingMs)
    {
        var avgLlm = avgLlmProcessingMs ?? 0;

        if (escalatedRate >= 0.25 || (llmRate >= 0.45 && avgLlm >= 5000))
            return "AGRESSIVO";

        if (escalatedRate <= 0.05 && llmRate <= 0.15 && avgLlm <= 2500)
            return "ULTRA_ESTAVEL";

        return "CONSERVADOR";
    }

    private static string BuildSuggestionReason(string suggestedProfile, double escalatedRate, double llmRate, double? avgLlmProcessingMs)
    {
        var avgLlm = avgLlmProcessingMs ?? 0;

        return suggestedProfile switch
        {
            "AGRESSIVO" =>
                $"Taxa de escalonamento humano ({escalatedRate:P1}) e/ou uso de LLM ({llmRate:P1}, {avgLlm:F0}ms) está alta para a janela analisada.",
            "ULTRA_ESTAVEL" =>
                $"Baixa taxa de escalonamento ({escalatedRate:P1}) e baixa dependência de LLM ({llmRate:P1}) indicam margem para maior rigor de confiança.",
            _ =>
                $"Métricas intermediárias (escalonamento {escalatedRate:P1}, uso LLM {llmRate:P1}) sugerem manter equilíbrio entre latência e assertividade."
        };
    }

    private sealed record SessionMetricsRow(long TotalSessions, long EscalarHumanoSessions, double? AvgSessionSeconds);

    private sealed record InteractionMetricsRow(long TotalInteractions, long LlmInteractions, double? AvgLlmProcessingMs, double? AvgInteractionDurationMs);
}
