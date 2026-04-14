using System.Net.Http.Json;
using System.Text.Json;
using AIPort.Intelligence.Service.Domain.Enums;
using AIPort.Intelligence.Service.Domain.Models;
using AIPort.Intelligence.Service.Domain.Options;
using AIPort.Intelligence.Service.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace AIPort.Intelligence.Service.Services.Engines;

public sealed class HttpNlpProcessor : INlpProcessor
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = null,
        PropertyNameCaseInsensitive = false
    };

    private readonly HttpClient _httpClient;
    private readonly IOptions<AIServiceOptions> _options;
    private readonly ILogger<HttpNlpProcessor> _logger;

    public HttpNlpProcessor(
        HttpClient httpClient,
        IOptions<AIServiceOptions> options,
        ILogger<HttpNlpProcessor> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public async Task<ProcessingLayerResult> ProcessAsync(
        string texto,
        ProcessingState estadoAtual,
        CancellationToken ct = default)
    {
        var nlpOptions = _options.Value.Nlp;

        if (!nlpOptions.Enabled)
            return BuildFromState(estadoAtual, 0.0, "NLP-Disabled");

        if (!nlpOptions.UseExternalApi)
        {
            _logger.LogInformation("Camada NLP externa desabilitada. Fluxo seguira para LLM se necessario.");
            return BuildFromState(estadoAtual, 0.0, "NLP-ExternalApiDisabled");
        }

        if (!Uri.TryCreate(nlpOptions.ExternalApiBaseUrl, UriKind.Absolute, out _))
        {
            _logger.LogWarning("NLP externo configurado sem BaseUrl valida. Valor atual: {BaseUrl}", nlpOptions.ExternalApiBaseUrl);
            return BuildFromState(estadoAtual, 0.0, "NLP-ExternalApiInvalidConfig");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/inference/process")
        {
            Content = JsonContent.Create(
                new ExternalInferenceRequest
                {
                    Texto = texto,
                    TenantType = estadoAtual.TenantType,
                    SessionId = estadoAtual.SessionId,
                    Metadata = estadoAtual.Metadata
                },
                options: SerializerOptions)
        };

        if (!string.IsNullOrWhiteSpace(nlpOptions.ExternalApiKey))
            request.Headers.TryAddWithoutValidation("X-Api-Key", nlpOptions.ExternalApiKey);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(Math.Max(1000, nlpOptions.ExternalApiTimeoutMs));

        try
        {
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("NLP externo retornou HTTP {StatusCode}.", (int)response.StatusCode);
                return BuildFromState(estadoAtual, 0.0, "NLP-ExternalApiHttpError");
            }

            var payload = await response.Content.ReadFromJsonAsync<ExternalInferenceResponse>(SerializerOptions, timeoutCts.Token);
            if (payload is null)
            {
                _logger.LogWarning("NLP externo retornou payload vazio.");
                return BuildFromState(estadoAtual, 0.0, "NLP-ExternalApiEmpty");
            }

            return new ProcessingLayerResult
            {
                Camada = string.IsNullOrWhiteSpace(payload.Camada) ? "NLP-ExternalApi" : payload.Camada,
                Confianca = payload.Confianca,
                Intencao = ParseIntencao(payload.Intencao),
                DadosExtraidos = payload.DadosExtraidos?.ToDomainModel() ?? new DadosExtraidos(),
                Debug = null
            };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Timeout ao chamar servico NLP externo apos {TimeoutMs}ms.", nlpOptions.ExternalApiTimeoutMs);
            return BuildFromState(estadoAtual, 0.0, "NLP-ExternalApiTimeout");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao chamar servico NLP externo.");
            return BuildFromState(estadoAtual, 0.0, "NLP-ExternalApiError");
        }
    }

    private static Intencao? ParseIntencao(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return Enum.TryParse<Intencao>(value, ignoreCase: true, out var parsed)
            ? parsed
            : null;
    }

    private static ProcessingLayerResult BuildFromState(ProcessingState state, double confianca, string camada) => new()
    {
        Camada = camada,
        Confianca = confianca,
        Intencao = state.Intencao,
        DadosExtraidos = state.ToDadosExtraidos()
    };

    private sealed record ExternalInferenceRequest
    {
        public required string Texto { get; init; }
        public required string TenantType { get; init; }
        public string? SessionId { get; init; }
        public IDictionary<string, string>? Metadata { get; init; }
    }

    private sealed record ExternalInferenceResponse
    {
        public string? Intencao { get; init; }
        public ExternalDadosExtraidos? DadosExtraidos { get; init; }
        public double Confianca { get; init; }
        public string? Camada { get; init; }
    }

    private sealed record ExternalDadosExtraidos
    {
        public string? Nome { get; init; }
        public string? NomeVisitante { get; init; }
        public string? Documento { get; init; }
        public string? Cpf { get; init; }
        public string? Unidade { get; init; }
        public string? Bloco { get; init; }
        public string? Torre { get; init; }
        public string? Empresa { get; init; }
        public string? Parentesco { get; init; }
        public bool EstaComVeiculo { get; init; }
        public string? Placa { get; init; }
        public bool EEntregador { get; init; }

        public DadosExtraidos ToDomainModel() => new()
        {
            Nome = Nome,
            NomeVisitante = NomeVisitante,
            Documento = Documento,
            Cpf = Cpf,
            Unidade = Unidade,
            Bloco = Bloco,
            Torre = Torre,
            Empresa = Empresa,
            Parentesco = Parentesco,
            EstaComVeiculo = EstaComVeiculo,
            Placa = Placa,
            EEntregador = EEntregador
        };
    }
}