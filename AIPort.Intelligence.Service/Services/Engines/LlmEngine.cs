using System.Text.Json;
using System.Text.Json.Serialization;
using AIPort.Intelligence.Service.Domain.Enums;
using AIPort.Intelligence.Service.Domain.Models;
using AIPort.Intelligence.Service.Domain.Options;
using AIPort.Intelligence.Service.Domain.Rules;
using AIPort.Intelligence.Service.Services.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AIPort.Intelligence.Service.Services.Engines;

/// <summary>
/// Camada 3 — Motor LLM via Semantic Kernel.
/// Suporta múltiplos provedores configurados em appsettings.json
/// (AzureOpenAI, OpenAI, Ollama) com fallback automático em ordem de Priority.
/// </summary>
public sealed class LlmEngine : ILlmProcessor
{
    private readonly IOptions<AIServiceOptions> _options;
    private readonly ILogger<LlmEngine> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public LlmEngine(IOptions<AIServiceOptions> options, ILogger<LlmEngine> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<ProcessingLayerResult> ProcessAsync(
        string texto,
        ProcessingState estadoAtual,
        TenantRule? regrasTenant,
        CancellationToken ct = default)
    {
        var providers = _options.Value.LlmProviders
            .Where(p => p.IsEnabled)
            .OrderBy(p => p.Priority)
            .ToList();

        if (providers.Count == 0)
        {
            _logger.LogWarning("Nenhum provedor LLM habilitado. Retornando resultado padrão.");
            return BuildFallbackResult(estadoAtual);
        }

        foreach (var provider in providers)
        {
            try
            {
                _logger.LogInformation("LLM layer: tentando provedor '{Provider}'...", provider.Name);
                var result = await InvokeProviderAsync(provider, texto, estadoAtual, regrasTenant, ct);
                _logger.LogInformation("LLM layer: provedor '{Provider}' respondeu com sucesso.", provider.Name);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Provedor '{Provider}' falhou. Tentando próximo...", provider.Name);
            }
        }

        _logger.LogError("Todos os provedores LLM falharam. Retornando resultado de fallback.");
        return BuildFallbackResult(estadoAtual);
    }

    // ──────────────────────────────────────────────────────────────────────────

    private async Task<ProcessingLayerResult> InvokeProviderAsync(
        LlmProviderConfig provider,
        string texto,
        ProcessingState estado,
        TenantRule? regras,
        CancellationToken ct)
    {
        var llmOptions = _options.Value.Llm;
        var kernel = BuildKernel(provider, llmOptions);
        var chat = kernel.GetRequiredService<IChatCompletionService>();

        var history = new ChatHistory();
        history.AddSystemMessage(BuildSystemPrompt(regras));
        history.AddUserMessage(BuildUserMessage(texto, estado));

        var raw = await GetRawResponseAsync(
            chat,
            history,
            kernel,
            temperature: llmOptions.Temperature,
            maxTokens: Math.Max(1024, llmOptions.MaxTokens),
            ct);

        var parsed = ParseLlmResponse(raw, estado);
        if (parsed is not null)
            return parsed;

        _logger.LogWarning("Resposta inicial do LLM inválida. Tentando uma segunda chamada de reparo com temperatura baixa.");

        // Retry único orientado a reparo estrutural (mantém custo e latência sob controle).
        history.AddAssistantMessage(raw.Length > 3500 ? raw[..3500] : raw);
        history.AddUserMessage("Reenvie APENAS JSON válido no formato solicitado. Não inclua markdown, explicações ou texto adicional.");

        var retryRaw = await GetRawResponseAsync(
            chat,
            history,
            kernel,
            temperature: 0.0,
            maxTokens: Math.Max(768, llmOptions.MaxTokens),
            ct);

        var retryParsed = ParseLlmResponse(retryRaw, estado);
        if (retryParsed is not null)
            return retryParsed with { Camada = "LLM-Retry" };

        _logger.LogWarning("Resposta do LLM não pôde ser desserializada após tentativas de reparo. Acionando fallback humano.");
        return BuildUnknownIntentResult(estado);
    }

    private static async Task<string> GetRawResponseAsync(
        IChatCompletionService chat,
        ChatHistory history,
        Kernel kernel,
        double temperature,
        int maxTokens,
        CancellationToken ct)
    {
        var settings = new PromptExecutionSettings
        {
            ExtensionData = new Dictionary<string, object>
            {
                { "temperature", temperature },
                { "max_tokens", maxTokens }
            }
        };

        var response = await chat.GetChatMessageContentAsync(history, settings, kernel, ct);
        return response.Content ?? string.Empty;
    }

    private Kernel BuildKernel(LlmProviderConfig provider, LlmConfig llmOptions)
    {
        var builder = Kernel.CreateBuilder();
        var timeoutSeconds = Math.Max(1, llmOptions.ProviderTimeoutSeconds);

        switch (provider.ServiceType.ToUpperInvariant())
        {
            case "AZUREOPENAI":
                builder.AddAzureOpenAIChatCompletion(
                    deploymentName: string.IsNullOrWhiteSpace(provider.DeploymentName)
                        ? provider.Model
                        : provider.DeploymentName,
                    endpoint: provider.Endpoint,
                    apiKey: provider.ApiKey);
                break;

            case "OPENAI":
                var openAiClient = new HttpClient();
                openAiClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

                if (!string.IsNullOrWhiteSpace(provider.Endpoint))
                    openAiClient.BaseAddress = new Uri(provider.Endpoint);

                builder.AddOpenAIChatCompletion(
                    modelId: provider.Model,
                    apiKey: provider.ApiKey,
                    httpClient: openAiClient);
                break;

            case "GOOGLEAI":
                // Google AI Studio — API compatível com OpenAI
                // Endpoint: https://generativelanguage.googleapis.com/v1beta/openai/
                var geminiClient = new HttpClient { BaseAddress = new Uri(provider.Endpoint) };
                geminiClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
                builder.AddOpenAIChatCompletion(
                    modelId: provider.Model,
                    apiKey: provider.ApiKey,
                    httpClient: geminiClient);
                break;

            case "OLLAMA":
                // Ollama expõe uma API compatível com OpenAI
                var ollamaClient = new HttpClient { BaseAddress = new Uri(provider.Endpoint) };
                ollamaClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
                builder.AddOpenAIChatCompletion(
                    modelId: provider.Model,
                    apiKey: provider.ApiKey,
                    httpClient: ollamaClient);
                break;

            default:
                throw new NotSupportedException(
                    $"Tipo de provedor LLM não suportado: '{provider.ServiceType}'. " +
                    "Valores aceitos: GoogleAI, AzureOpenAI, OpenAI, Ollama.");
        }

        return builder.Build();
    }

    private static string BuildSystemPrompt(TenantRule? regras)
    {
        var regrasJson = regras is not null
            ? JsonSerializer.Serialize(regras, new JsonSerializerOptions { WriteIndented = false })
            : "{}";

        // Usa $$""" (double-dollar raw string) para poder usar { } literais no schema JSON
        // enquanto {{...}} representa a interpolação real.
        return $$"""
            Você é o motor de inteligência de uma portaria virtual brasileira.
            Analise a mensagem do visitante e retorne uma decisão JSON estruturada.

            REGRAS DO TENANT (siga estritamente):
            {{regrasJson}}

            INTENÇÕES DISPONÍVEIS (escolha exatamente uma):
            - Saudacao    → visitante está cumprimentando
            - Identificacao → visitante está se identificando ou informando destino
            - Despedida   → visitante está encerrando a interação
            - Urgencia    → emergência ou pedido urgente
            - Indefinida  → não foi possível determinar

            AÇÕES DISPONÍVEIS (escolha exatamente uma):
            - SOLICITAR_DOC         → pedir documento de identificação
            - AGUARDAR_MORADOR      → aguardar o morador liberar
            - NOTIFICAR_MORADOR     → notificar o morador por push/intercomunicador
            - LIBERAR_ACESSO        → liberar imediatamente
            - NEGAR_ACESSO          → negar e registrar tentativa
            - ESCALAR_HUMANO        → transferir para atendimento humano
            - AGUARDAR_CONFIRMACAO  → aguardar confirmação adicional
            - SOLICITAR_IDENTIFICACAO → pedir identificação sem especificar tipo

            Regras de saída obrigatórias:
            - Retorne SOMENTE JSON (sem markdown, sem texto extra, sem comentários).
            - Use exatamente os nomes de campos abaixo.
            - Se a informação não estiver disponível, use null.
            - NUNCA deixe de preencher "intencao" e "acaoSugerida".
            - Extraia o máximo de informações possíveis, mesmo que o texto esteja incompleto, fragmentado ou parcialmente compreensível.
            - Sempre tente preencher todos os campos de "dadosExtraidos" com base em qualquer pista, mesmo que parcial.
            - Se o visitante disser apenas parte do nome, número, unidade, bloco, documento ou qualquer dado, preencha o campo correspondente com o valor parcial.
            - Não ignore informações parciais ou frases incompletas.

            Responda SOMENTE com JSON válido no formato abaixo:
            {
              "intencao": "<valor do enum>",
              "dadosExtraidos": {
                "nome": "<string|null>",
                "nomeVisitante": "<string|null>",
                "documento": "<string|null>",
                "cpf": "<string|null>",
                "unidade": "<string|null>",
                "bloco": "<string|null>",
                "torre": "<string|null>",
                "empresa": "<string|null>",
                "parentesco": "<string|null>",
                "estaComVeiculo": "<bool>",
                "placa": "<string|null>",
                "eEntregador": "<bool>"
              },
              "respostaTexto": "<texto natural em português brasileiro para o visitante>",
              "acaoSugerida": "<valor do enum>"
            }
            """;
    }

    private static string BuildUserMessage(string texto, ProcessingState estado)
    {
        var contexto = new
        {
            mensagem = texto,
            dadosParciais = new
            {
                nomeJaDetectado = estado.NomeDetectado,
                nomeVisitanteJaDetectado = estado.NomeVisitanteDetectado,
                documentoJaDetectado = estado.DocumentoDetectado,
                cpfJaDetectado = estado.CpfDetectado,
                unidadeJaDetectada = estado.UnidadeDetectada,
                blocoJaDetectado = estado.BlocoDetectado,
                torreJaDetectada = estado.TorreDetectada,
                empresaJaDetectada = estado.EmpresaDetectada,
                parentescoJaDetectado = estado.ParentescoDetectado,
                estaComVeiculoJaDetectado = estado.EstaComVeiculoDetectado,
                placaJaDetectada = estado.PlacaDetectada,
                eEntregadorJaDetectado = estado.EEntregadorDetectado
            }
        };
        return JsonSerializer.Serialize(contexto);
    }

    private ProcessingLayerResult? ParseLlmResponse(string raw, ProcessingState estado)
    {
        try
        {
            var payload = NormalizeJsonPayload(raw);
            var dto = TryDeserialize(payload);

            if (dto is null)
            {
                var repaired = TryCloseJsonObject(payload);
                if (!ReferenceEquals(repaired, payload))
                {
                    dto = TryDeserialize(repaired);
                }
            }

            if (dto is null)
            {
                var repaired = TryRepairTruncatedJson(payload);
                if (!ReferenceEquals(repaired, payload))
                {
                    dto = TryDeserialize(repaired);
                }
            }

            if (dto is null)
                return null;

            var normalized = NormalizeSemanticCoherence(dto, estado);
            return new ProcessingLayerResult
            {
                Camada = normalized.Camada,
                Confianca = normalized.Confianca,
                Intencao = normalized.Intencao,
                AcaoSugerida = normalized.AcaoSugerida,
                RespostaTexto = string.IsNullOrWhiteSpace(dto.RespostaTexto)
                    ? BuildFallbackTextForAction(normalized.AcaoSugerida)
                    : dto.RespostaTexto,
                DadosExtraidos = new()
                {
                    Nome = dto.DadosExtraidos?.Nome ?? estado.NomeDetectado,
                    NomeVisitante = dto.DadosExtraidos?.NomeVisitante ?? estado.NomeVisitanteDetectado ?? estado.NomeDetectado,
                    Documento = dto.DadosExtraidos?.Documento ?? estado.DocumentoDetectado,
                    Cpf = dto.DadosExtraidos?.Cpf ?? estado.CpfDetectado,
                    Unidade = dto.DadosExtraidos?.Unidade ?? estado.UnidadeDetectada,
                    Bloco = dto.DadosExtraidos?.Bloco ?? estado.BlocoDetectado,
                    Torre = dto.DadosExtraidos?.Torre ?? estado.TorreDetectada,
                    Empresa = dto.DadosExtraidos?.Empresa ?? estado.EmpresaDetectada,
                    Parentesco = dto.DadosExtraidos?.Parentesco ?? estado.ParentescoDetectado,
                    EstaComVeiculo = dto.DadosExtraidos?.EstaComVeiculo ?? estado.EstaComVeiculoDetectado ?? false,
                    Placa = dto.DadosExtraidos?.Placa ?? estado.PlacaDetectada,
                    EEntregador = dto.DadosExtraidos?.EEntregador ?? estado.EEntregadorDetectado ?? false
                }
            };
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex, "Falha ao deserializar resposta do LLM. Payload bruto será reprocessado.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha inesperada ao processar resposta do LLM.");
            return null;
        }
    }

    private static (Intencao Intencao, AcaoSugerida AcaoSugerida, double Confianca, string Camada) NormalizeSemanticCoherence(
        LlmResponseDto dto,
        ProcessingState estado)
    {
        var intencao = dto.Intencao;
        var acao = dto.AcaoSugerida;
        var confianca = 0.90;
        var camada = "LLM";

        var hasDocument =
            !string.IsNullOrWhiteSpace(dto.DadosExtraidos?.Documento)
            || !string.IsNullOrWhiteSpace(dto.DadosExtraidos?.Cpf)
            || !string.IsNullOrWhiteSpace(estado.DocumentoDetectado)
            || !string.IsNullOrWhiteSpace(estado.CpfDetectado);

        var hasDestino =
            !string.IsNullOrWhiteSpace(dto.DadosExtraidos?.Unidade)
            || !string.IsNullOrWhiteSpace(dto.DadosExtraidos?.Bloco)
            || !string.IsNullOrWhiteSpace(dto.DadosExtraidos?.Torre)
            || !string.IsNullOrWhiteSpace(estado.UnidadeDetectada)
            || !string.IsNullOrWhiteSpace(estado.BlocoDetectado)
            || !string.IsNullOrWhiteSpace(estado.TorreDetectada);

        if (intencao == Intencao.Indefinida && acao is not (AcaoSugerida.ESCALAR_HUMANO or AcaoSugerida.SOLICITAR_IDENTIFICACAO or AcaoSugerida.AGUARDAR_CONFIRMACAO))
        {
            acao = hasDocument ? AcaoSugerida.AGUARDAR_CONFIRMACAO : AcaoSugerida.SOLICITAR_IDENTIFICACAO;
            confianca = 0.72;
            camada = "LLM-Normalized";
        }

        // Evita repetição de SOLICITAR_DOC quando documento já foi informado anteriormente.
        if (acao == AcaoSugerida.SOLICITAR_DOC && hasDocument)
        {
            acao = hasDestino ? AcaoSugerida.NOTIFICAR_MORADOR : AcaoSugerida.AGUARDAR_CONFIRMACAO;
            confianca = Math.Min(confianca, 0.78);
            camada = "LLM-Normalized";
        }

        if (acao == AcaoSugerida.NOTIFICAR_MORADOR && !hasDocument)
        {
            acao = hasDestino ? AcaoSugerida.SOLICITAR_DOC : AcaoSugerida.SOLICITAR_IDENTIFICACAO;
            confianca = Math.Min(confianca, 0.78);
            camada = "LLM-Normalized";
        }

        return (intencao, acao, confianca, camada);
    }

    private static string BuildFallbackTextForAction(AcaoSugerida acao)
    {
        return acao switch
        {
            AcaoSugerida.SOLICITAR_DOC => "Para continuarmos com segurança, informe seu documento, por favor.",
            AcaoSugerida.SOLICITAR_IDENTIFICACAO => "Por favor, informe seu nome e com quem deseja falar.",
            AcaoSugerida.NOTIFICAR_MORADOR => "Perfeito, vou notificar o morador. Aguarde um instante.",
            AcaoSugerida.ESCALAR_HUMANO => "Vou encaminhar você para atendimento humano para seguir com segurança.",
            _ => "Aguarde um momento enquanto continuo seu atendimento."
        };
    }

    private static LlmResponseDto? TryDeserialize(string payload)
    {
        try
        {
            return JsonSerializer.Deserialize<LlmResponseDto>(payload, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string NormalizeJsonPayload(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var payload = raw.Trim();

        if (payload.StartsWith("```", StringComparison.Ordinal))
        {
            var firstLineBreak = payload.IndexOf('\n');
            if (firstLineBreak > -1)
                payload = payload[(firstLineBreak + 1)..].Trim();

            if (payload.EndsWith("```", StringComparison.Ordinal))
                payload = payload[..^3].Trim();
        }

        var firstBrace = payload.IndexOf('{');
        if (firstBrace < 0)
            return payload;

        var candidate = payload[firstBrace..];
        return TrySliceBalancedJsonObject(candidate, out var json)
            ? json
            : candidate;
    }

    private static bool TrySliceBalancedJsonObject(string text, out string json)
    {
        json = string.Empty;
        var depth = 0;
        var inString = false;
        var escaped = false;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];

            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (ch == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (ch == '"')
                    inString = false;

                continue;
            }

            if (ch == '"')
            {
                inString = true;
                continue;
            }

            if (ch == '{')
            {
                depth++;
                continue;
            }

            if (ch == '}')
            {
                depth--;
                if (depth == 0)
                {
                    json = text[..(i + 1)];
                    return true;
                }
            }
        }

        return false;
    }

    private static string TryCloseJsonObject(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var depth = 0;
        var inString = false;
        var escaped = false;

        foreach (var ch in text)
        {
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (ch == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (ch == '"')
                    inString = false;

                continue;
            }

            if (ch == '"')
            {
                inString = true;
                continue;
            }

            if (ch == '{') depth++;
            if (ch == '}') depth--;
        }

        if (depth <= 0 || depth > 8)
            return text;

        return text + new string('}', depth);
    }

    private static string TryRepairTruncatedJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var depth = 0;
        var inString = false;
        var escaped = false;

        foreach (var ch in text)
        {
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (ch == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (ch == '"')
                    inString = false;

                continue;
            }

            if (ch == '"')
            {
                inString = true;
                continue;
            }

            if (ch == '{') depth++;
            if (ch == '}') depth--;
        }

        var repaired = text;

        if (inString)
            repaired += '"';

        if (depth > 0 && depth <= 8)
            repaired += new string('}', depth);

        return repaired;
    }

    private static ProcessingLayerResult BuildFallbackResult(ProcessingState estado) => new()
    {
        Camada = "LLM-Fallback",
        Confianca = 0.30,
        Intencao = estado.Intencao ?? Intencao.Indefinida,
        AcaoSugerida = AcaoSugerida.ESCALAR_HUMANO,
        RespostaTexto = "Desculpe, não consegui processar sua solicitação. Transferindo para atendimento humano.",
        DadosExtraidos = estado.ToDadosExtraidos()
    };

    private static ProcessingLayerResult BuildUnknownIntentResult(ProcessingState estado) => new()
    {
        Camada = "LLM-Fallback",
        Confianca = 0.20,
        Intencao = Intencao.Indefinida,
        AcaoSugerida = AcaoSugerida.ESCALAR_HUMANO,
        RespostaTexto = "Não consegui entender com segurança sua solicitação. Vou encaminhar para atendimento humano.",
        DadosExtraidos = estado.ToDadosExtraidos()
    };

    // ──────────────────────────────────────────────────────────────────────────
    // DTOs internos para desserialização da resposta do LLM

    private sealed class LlmResponseDto
    {
        [JsonPropertyName("intencao")]
        public Intencao Intencao { get; init; }

        [JsonPropertyName("dadosExtraidos")]
        public LlmDadosDto? DadosExtraidos { get; init; }

        [JsonPropertyName("respostaTexto")]
        public string RespostaTexto { get; init; } = string.Empty;

        [JsonPropertyName("acaoSugerida")]
        public AcaoSugerida AcaoSugerida { get; init; }
    }

    private sealed class LlmDadosDto
    {
        [JsonPropertyName("nome")]
        public string? Nome { get; init; }

        [JsonPropertyName("nomeVisitante")]
        public string? NomeVisitante { get; init; }

        [JsonPropertyName("documento")]
        public string? Documento { get; init; }

        [JsonPropertyName("cpf")]
        public string? Cpf { get; init; }

        [JsonPropertyName("unidade")]
        public string? Unidade { get; init; }

        [JsonPropertyName("bloco")]
        public string? Bloco { get; init; }

        [JsonPropertyName("torre")]
        public string? Torre { get; init; }

        [JsonPropertyName("empresa")]
        public string? Empresa { get; init; }

        [JsonPropertyName("parentesco")]
        public string? Parentesco { get; init; }

        [JsonPropertyName("estaComVeiculo")]
        public bool? EstaComVeiculo { get; init; }

        [JsonPropertyName("placa")]
        public string? Placa { get; init; }

        [JsonPropertyName("eEntregador")]
        public bool? EEntregador { get; init; }
    }
}
