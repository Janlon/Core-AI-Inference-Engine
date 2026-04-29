using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AIPort.Intelligence.Service.Domain.Models;
using AIPort.Intelligence.Service.Domain.Options;
using AIPort.Intelligence.Service.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace AIPort.Intelligence.Service.Services;

public sealed class LlmHealthService : ILlmHealthService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IOptions<AIServiceOptions> _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<LlmHealthService> _logger;

    public LlmHealthService(
        IOptions<AIServiceOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<LlmHealthService> logger)
    {
        _options = options;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<LlmHealthReport> GetHealthAsync(CancellationToken ct = default)
    {
        var checkedAtUtc = DateTime.UtcNow;
        var providers = _options.Value.LlmProviders
            .Where(provider => provider.IsEnabled)
            .OrderBy(provider => provider.Priority)
            .ToList();

        if (providers.Count == 0)
        {
            return new LlmHealthReport(
                Status: "disabled",
                AnyProviderEnabled: false,
                AnyProviderHealthy: false,
                CheckedAtUtc: checkedAtUtc,
                Providers: []);
        }

        var results = new List<LlmProviderHealthStatus>(providers.Count);
        foreach (var provider in providers)
            results.Add(await CheckProviderAsync(provider, ct));

        var anyHealthy = results.Any(result => result.IsHealthy);
        var overallStatus = anyHealthy ? "healthy" : "unhealthy";

        return new LlmHealthReport(
            Status: overallStatus,
            AnyProviderEnabled: true,
            AnyProviderHealthy: anyHealthy,
            CheckedAtUtc: checkedAtUtc,
            Providers: results);
    }

    private async Task<LlmProviderHealthStatus> CheckProviderAsync(LlmProviderConfig provider, CancellationToken ct)
    {
        var startedAt = DateTime.UtcNow;
        var timer = Stopwatch.StartNew();

        try
        {
            return provider.ServiceType.ToUpperInvariant() switch
            {
                "GOOGLEAI" => await CheckOpenAiCompatibleProviderAsync(provider, startedAt, timer, "chat/completions", true, ct),
                "OPENAI" => await CheckOpenAiCompatibleProviderAsync(provider, startedAt, timer, "chat/completions", false, ct),
                "OLLAMA" => await CheckOpenAiCompatibleProviderAsync(provider, startedAt, timer, "api/tags", false, ct, useGet: true),
                "AZUREOPENAI" => await CheckAzureOpenAiProviderAsync(provider, startedAt, timer, ct),
                _ => BuildStatus(provider, false, null, "unsupported", $"Tipo de provedor '{provider.ServiceType}' não suportado no health-check.", timer.ElapsedMilliseconds, startedAt)
            };
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            timer.Stop();
            _logger.LogWarning(ex, "Timeout no health-check do provedor LLM {Provider}", provider.Name);
            return BuildStatus(provider, false, null, "timeout", "Timeout ao validar o provedor LLM.", timer.ElapsedMilliseconds, startedAt);
        }
        catch (Exception ex)
        {
            timer.Stop();
            _logger.LogWarning(ex, "Falha inesperada no health-check do provedor LLM {Provider}", provider.Name);
            return BuildStatus(provider, false, null, "error", ex.Message, timer.ElapsedMilliseconds, startedAt);
        }
    }

    private async Task<LlmProviderHealthStatus> CheckOpenAiCompatibleProviderAsync(
        LlmProviderConfig provider,
        DateTime startedAt,
        Stopwatch timer,
        string path,
        bool requireEndpoint,
        CancellationToken ct,
        bool useGet = false)
    {
        if (string.IsNullOrWhiteSpace(provider.ApiKey) && !string.Equals(provider.ServiceType, "OLLAMA", StringComparison.OrdinalIgnoreCase))
            return BuildStatus(provider, false, null, "misconfigured", "ApiKey não configurada para o provedor ativo.", null, startedAt);

        var baseUrl = ResolveBaseUrl(provider, requireEndpoint);
        if (baseUrl is null)
            return BuildStatus(provider, false, null, "misconfigured", "Endpoint não configurado para o provedor ativo.", null, startedAt);

        using var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = TimeSpan.FromSeconds(Math.Max(3, _options.Value.Llm.ProviderTimeoutSeconds));

        using var request = new HttpRequestMessage(useGet ? HttpMethod.Get : HttpMethod.Post, path);
        if (!string.IsNullOrWhiteSpace(provider.ApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);

        if (!useGet)
        {
            request.Content = JsonContent.Create(new
            {
                model = provider.Model,
                messages = new[] { new { role = "user", content = "health-check" } },
                max_tokens = 1,
                temperature = 0
            }, options: JsonOptions);
        }

        using var response = await client.SendAsync(request, ct);
        var responseBody = await SafeReadBodyAsync(response, ct);
        timer.Stop();

        if (response.IsSuccessStatusCode)
        {
            return BuildStatus(provider, true, (int)response.StatusCode, "healthy", "Provedor respondeu com sucesso ao health-check.", timer.ElapsedMilliseconds, startedAt);
        }

        return BuildStatus(
            provider,
            false,
            (int)response.StatusCode,
            "unhealthy",
            BuildFailureMessage(response, responseBody),
            timer.ElapsedMilliseconds,
            startedAt);
    }

    private async Task<LlmProviderHealthStatus> CheckAzureOpenAiProviderAsync(
        LlmProviderConfig provider,
        DateTime startedAt,
        Stopwatch timer,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(provider.ApiKey))
            return BuildStatus(provider, false, null, "misconfigured", "ApiKey não configurada para o provedor ativo.", null, startedAt);

        if (string.IsNullOrWhiteSpace(provider.Endpoint) || string.IsNullOrWhiteSpace(provider.Model) && string.IsNullOrWhiteSpace(provider.DeploymentName))
            return BuildStatus(provider, false, null, "misconfigured", "Endpoint ou deployment/model não configurado para Azure OpenAI.", null, startedAt);

        var deploymentName = string.IsNullOrWhiteSpace(provider.DeploymentName)
            ? provider.Model
            : provider.DeploymentName;

        using var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(provider.Endpoint.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(Math.Max(3, _options.Value.Llm.ProviderTimeoutSeconds));

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"openai/deployments/{Uri.EscapeDataString(deploymentName)}/chat/completions?api-version=2024-06-01");

        request.Headers.Add("api-key", provider.ApiKey);
        request.Content = JsonContent.Create(new
        {
            messages = new[] { new { role = "user", content = "health-check" } },
            max_tokens = 1,
            temperature = 0
        }, options: JsonOptions);

        using var response = await client.SendAsync(request, ct);
        var responseBody = await SafeReadBodyAsync(response, ct);
        timer.Stop();

        if (response.IsSuccessStatusCode)
        {
            return BuildStatus(provider, true, (int)response.StatusCode, "healthy", "Provedor respondeu com sucesso ao health-check.", timer.ElapsedMilliseconds, startedAt);
        }

        return BuildStatus(
            provider,
            false,
            (int)response.StatusCode,
            "unhealthy",
            BuildFailureMessage(response, responseBody),
            timer.ElapsedMilliseconds,
            startedAt);
    }

    private static string? ResolveBaseUrl(LlmProviderConfig provider, bool requireEndpoint)
    {
        if (!string.IsNullOrWhiteSpace(provider.Endpoint))
            return provider.Endpoint.TrimEnd('/') + "/";

        if (string.Equals(provider.ServiceType, "OPENAI", StringComparison.OrdinalIgnoreCase))
            return "https://api.openai.com/v1/";

        return requireEndpoint ? null : provider.Endpoint;
    }

    private static string BuildFailureMessage(HttpResponseMessage response, string? responseBody)
    {
        if ((int)response.StatusCode is 401 or 403)
            return $"Falha de autenticação/autorização no provedor LLM ({(int)response.StatusCode}). Verifique a chave de API.";

        if ((int)response.StatusCode == 429)
            return "Provedor LLM respondeu com rate limit (429).";

        if (!string.IsNullOrWhiteSpace(responseBody))
            return $"Provedor LLM respondeu HTTP {(int)response.StatusCode}: {responseBody}";

        return $"Provedor LLM respondeu HTTP {(int)response.StatusCode}.";
    }

    private static async Task<string?> SafeReadBodyAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.Content is null)
            return null;

        var body = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(body))
            return null;

        return body.Length <= 500 ? body : body[..500];
    }

    private static LlmProviderHealthStatus BuildStatus(
        LlmProviderConfig provider,
        bool isHealthy,
        int? httpStatusCode,
        string status,
        string message,
        long? elapsedMs,
        DateTime checkedAtUtc)
    {
        return new LlmProviderHealthStatus(
            Name: provider.Name,
            ServiceType: provider.ServiceType,
            IsEnabled: provider.IsEnabled,
            IsHealthy: isHealthy,
            HttpStatusCode: httpStatusCode,
            Status: status,
            Message: message,
            ElapsedMs: elapsedMs,
            Endpoint: string.IsNullOrWhiteSpace(provider.Endpoint) ? null : provider.Endpoint,
            Model: string.IsNullOrWhiteSpace(provider.Model) ? null : provider.Model,
            CheckedAtUtc: checkedAtUtc);
    }
}
