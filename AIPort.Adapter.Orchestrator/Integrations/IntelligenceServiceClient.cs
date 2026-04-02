using System.Net.Http.Json;
using AIPort.Adapter.Orchestrator.Config;
using AIPort.Adapter.Orchestrator.Domain.Models;
using AIPort.Adapter.Orchestrator.Integrations.Interfaces;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace AIPort.Adapter.Orchestrator.Integrations;

public sealed class IntelligenceServiceClient : IIntelligenceServiceClient
{
    private readonly HttpClient _http;
    private readonly IntelligenceServiceOptions _options;
    private readonly ILogger<IntelligenceServiceClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public IntelligenceServiceClient(HttpClient http, IOptions<IntelligenceServiceOptions> options, ILogger<IntelligenceServiceClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<InferenceResponseDto> ProcessAsync(InferenceRequestDto request, CancellationToken ct = default)
    {
        var requestUri = BuildRequestUri(_options.BaseUrl, _options.ProcessPath);
        _logger.LogInformation("Enviando request para Intelligence Service em {RequestUri}", requestUri);

        using var response = await _http.PostAsJsonAsync(requestUri, request, JsonOptions, ct);
        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<InferenceResponseDto>(cancellationToken: ct);
        if (dto is null)
            throw new InvalidOperationException("Resposta vazia do AIPort.Intelligence.Service.");

        return dto;
    }

    public async Task<TenantResponsesDto> GetTenantResponsesAsync(string tenantType, CancellationToken ct = default)
    {
        var requestUri = BuildRequestUri(_options.BaseUrl, $"api/inference/responses/{Uri.EscapeDataString(tenantType)}");
        _logger.LogInformation("Buscando respostas de tenant '{TenantType}' em {RequestUri}", tenantType, requestUri);

        using var response = await _http.GetAsync(requestUri, ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Intelligence Service retornou {Status} ao buscar respostas para tenant '{TenantType}'. Usando padrão.",
                (int)response.StatusCode, tenantType);
            return new TenantResponsesDto();
        }

        var dto = await response.Content.ReadFromJsonAsync<TenantResponsesDto>(JsonOptions, ct);
        return dto ?? new TenantResponsesDto();
    }

    private static string BuildRequestUri(string baseUrl, string processPath)
    {
        var normalizedBase = (baseUrl ?? string.Empty).Trim().TrimEnd('/');
        var normalizedPath = (processPath ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(normalizedBase))
            throw new InvalidOperationException("IntelligenceService:BaseUrl não foi configurado.");

        if (string.IsNullOrWhiteSpace(normalizedPath))
            throw new InvalidOperationException("IntelligenceService:ProcessPath não foi configurado.");

        normalizedPath = "/" + normalizedPath.TrimStart('/');
        return normalizedBase + normalizedPath;
    }
}
