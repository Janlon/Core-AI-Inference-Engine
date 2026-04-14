using System.Globalization;
using AIPort.Intelligence.Service.Domain.Enums;
using AIPort.Intelligence.Service.Domain.Models;
using AIPort.Intelligence.Service.Domain.Options;
using AIPort.Intelligence.Service.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace AIPort.Intelligence.Service.Services;

/// <summary>
/// Orquestrador central do motor de decisão.
///
/// Fluxo de processamento (pipeline em cascata):
///   Camada 1 — Regex   : extração instantânea de padrões determinísticos.
///   Camada 2 — NLP/NER : extração de entidades nomeadas por modelo linguístico.
///   Camada 3 — LLM     : raciocínio generativo via Semantic Kernel (fallback final).
///
/// Cada camada só é chamada se a anterior não atingiu o threshold de confiança.
/// </summary>
public sealed class DecisionEngine : IDecisionEngine
{
    private readonly IRegexProcessor _regexProcessor;
    private readonly INlpProcessor _nlpProcessor;
    private readonly ILlmProcessor _llmProcessor;
    private readonly IRulesLoader _rulesLoader;
    private readonly IOptions<AIServiceOptions> _options;
    private readonly ILogger<DecisionEngine> _logger;

    public DecisionEngine(
        IRegexProcessor regexProcessor,
        INlpProcessor nlpProcessor,
        ILlmProcessor llmProcessor,
        IRulesLoader rulesLoader,
        IOptions<AIServiceOptions> options,
        ILogger<DecisionEngine> logger)
    {
        _regexProcessor = regexProcessor;
        _nlpProcessor = nlpProcessor;
        _llmProcessor = llmProcessor;
        _rulesLoader = rulesLoader;
        _options = options;
        _logger = logger;
    }

    public async Task<DecisionResult> ProcessAsync(InferenceRequest request, CancellationToken ct = default)
    {
        var opts = _options.Value;
        var thresholds = ResolveThresholds(request);
        var tenantRule = _rulesLoader.GetRule(request.TenantType);
        var state = new ProcessingState(request.Texto, request.TenantType, request.SessionId, request.Metadata);
        DecisionDebugInfo? regexDebug = null;

        _logger.LogInformation(
            "Iniciando decisão. SessionId={Session}, TenantType={Tenant}, AiProfile={Profile}, RegexThr={RegexThr:F2}, NlpThr={NlpThr:F2}, GlobalThr={GlobalThr:F2}",
            request.SessionId ?? "N/A",
            request.TenantType,
            thresholds.Profile,
            thresholds.Regex,
            thresholds.Nlp,
            thresholds.Global);

        // ── Camada 1: Regex ──────────────────────────────────────────────────
        if (opts.Regex.Enabled)
        {
            var regexResult = await _regexProcessor.ProcessAsync(request.Texto, ct);
            regexDebug = regexResult.Debug;
            MergeIntoState(state, regexResult);

            if (regexResult.Confianca >= thresholds.Regex)
            {
                _logger.LogInformation("Decisão resolvida pela camada Regex (confiança={C:P0}).", regexResult.Confianca);
                return BuildDecision(regexResult, tenantRule, request.TenantType, regexDebug);
            }
        }
        else
        {
            _logger.LogInformation("Camada Regex desabilitada por configuração. Seguindo para NLP.");
        }

        // ── Camada 2: NLP ────────────────────────────────────────────────────
        if (opts.Nlp.Enabled)
        {
            var nlpResult = await _nlpProcessor.ProcessAsync(request.Texto, state, ct);
            MergeIntoState(state, nlpResult);

            if (nlpResult.Confianca >= thresholds.Nlp)
            {
                _logger.LogInformation("Decisão resolvida pela camada NLP (confiança={C:P0}).", nlpResult.Confianca);
                return BuildDecision(nlpResult, tenantRule, request.TenantType, regexDebug);
            }
        }

        // Durante slot-filling (rodadas curtas do orquestrador residencial),
        // prioriza extração rápida e evita custo/latência de LLM a cada turno.
        if (IsSlotFillingRequest(request))
        {
            _logger.LogInformation(
                "Slot-filling fast-path ativo. Retornando dados extraídos sem escalonar para LLM.");

            var slotFillingResult = new ProcessingLayerResult
            {
                Camada = "SlotFilling-FastPath",
                Confianca = state.MelhorConfianca > 0 ? state.MelhorConfianca : 0.65,
                Intencao = state.Intencao ?? Intencao.Identificacao,
                AcaoSugerida = AcaoSugerida.SOLICITAR_IDENTIFICACAO,
                DadosExtraidos = state.ToDadosExtraidos()
            };

            return BuildDecision(slotFillingResult, tenantRule, request.TenantType, regexDebug);
        }

        // ── Camada 3: LLM ────────────────────────────────────────────────────
        if (ShouldAskClarificationBeforeLlm(request.Texto, state))
        {
            _logger.LogInformation("Entrada de baixa informação detectada. Evitando chamada ao LLM e solicitando confirmação ao visitante.");
            var clarification = new ProcessingLayerResult
            {
                Camada = "PreLLM-Guard",
                Confianca = 0.60,
                Intencao = Intencao.Indefinida,
                AcaoSugerida = AcaoSugerida.SOLICITAR_IDENTIFICACAO,
                RespostaTexto = "Não consegui captar informações suficientes. Poderia repetir seu nome e a unidade de destino, por favor?",
                DadosExtraidos = state.ToDadosExtraidos()
            };

            return BuildDecision(clarification, tenantRule, request.TenantType, regexDebug);
        }

        _logger.LogInformation("Escalando para camada LLM (confiança acumulada={C:P0}).", state.MelhorConfianca);
        var llmResult = await _llmProcessor.ProcessAsync(request.Texto, state, tenantRule, ct);

        return BuildDecision(llmResult, tenantRule, request.TenantType, regexDebug);
    }

    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Acumula os dados parciais de uma camada no estado compartilhado,
    /// sem sobrescrever campos já preenchidos com mais confiança.
    /// </summary>
    private static void MergeIntoState(ProcessingState state, ProcessingLayerResult result)
    {
        state.NomeDetectado ??= result.DadosExtraidos.Nome;
        state.NomeVisitanteDetectado ??= result.DadosExtraidos.NomeVisitante;
        state.DocumentoDetectado ??= result.DadosExtraidos.Documento;
        state.CpfDetectado ??= result.DadosExtraidos.Cpf;
        state.UnidadeDetectada ??= result.DadosExtraidos.Unidade;
        state.BlocoDetectado ??= result.DadosExtraidos.Bloco;
        state.TorreDetectada ??= result.DadosExtraidos.Torre;
        state.EmpresaDetectada ??= result.DadosExtraidos.Empresa;
        state.ParentescoDetectado ??= result.DadosExtraidos.Parentesco;
        state.PlacaDetectada ??= result.DadosExtraidos.Placa;
        if (!state.EstaComVeiculoDetectado.HasValue)
            state.EstaComVeiculoDetectado = result.DadosExtraidos.EstaComVeiculo;
        if (!state.EEntregadorDetectado.HasValue)
            state.EEntregadorDetectado = result.DadosExtraidos.EEntregador;
        state.Intencao ??= result.Intencao;

        if (result.Confianca > state.MelhorConfianca)
            state.MelhorConfianca = result.Confianca;
    }

    /// <summary>
    /// Monta o <see cref="DecisionResult"/> final a partir do resultado de uma camada
    /// e das regras do tenant.
    /// </summary>
    private static DecisionResult BuildDecision(
        ProcessingLayerResult result,
        Domain.Rules.TenantRule? tenantRule,
        string tenantType,
        DecisionDebugInfo? fallbackDebug)
    {
        var intencao = NormalizeIntencao(result.Intencao, result.DadosExtraidos);
        var acao = result.AcaoSugerida
            ?? DeriveAction(intencao, result.DadosExtraidos, tenantRule);

        var resposta = result.RespostaTexto
            ?? DeriveResponseText(intencao, acao, tenantRule);

        return new DecisionResult
        {
            Intencao = intencao,
            DadosExtraidos = result.DadosExtraidos,
            RespostaTexto = resposta,
            AcaoSugerida = acao,
            Confianca = result.Confianca,
            CamadaResolucao = result.Camada,
            TenantType = tenantType,
            Debug = result.Debug ?? fallbackDebug
        };
    }

    /// <summary>
    /// Evita classificar como saudação quando já existem evidências claras de identificação.
    /// Exemplo: "Olá, meu nome é X, apto 204" deve ser Identificacao, não Saudacao.
    /// </summary>
    private static Intencao NormalizeIntencao(Intencao? intencaoOriginal, DadosExtraidos dados)
    {
        var intencao = intencaoOriginal ?? Intencao.Indefinida;

        var hasIdentificationEvidence =
            !string.IsNullOrWhiteSpace(dados.Nome)
            || !string.IsNullOrWhiteSpace(dados.NomeVisitante)
            || !string.IsNullOrWhiteSpace(dados.Documento)
            || !string.IsNullOrWhiteSpace(dados.Cpf)
            || !string.IsNullOrWhiteSpace(dados.Unidade)
            || !string.IsNullOrWhiteSpace(dados.Bloco)
            || !string.IsNullOrWhiteSpace(dados.Torre)
            || !string.IsNullOrWhiteSpace(dados.Empresa)
            || !string.IsNullOrWhiteSpace(dados.Parentesco)
            || !string.IsNullOrWhiteSpace(dados.Placa)
            || dados.EstaComVeiculo
            || dados.EEntregador;

        if (intencao == Intencao.Saudacao && hasIdentificationEvidence)
            return Intencao.Identificacao;

        return intencao;
    }

    /// <summary>
    /// Deriva a ação sugerida com base na intenção e nas regras do tenant,
    /// quando a camada de processamento não a determinou.
    /// </summary>
    private static AcaoSugerida DeriveAction(
        Intencao intencao,
        DadosExtraidos dados,
        Domain.Rules.TenantRule? regras)
    {
        return intencao switch
        {
            Intencao.Urgencia => AcaoSugerida.ESCALAR_HUMANO,
            Intencao.Despedida => AcaoSugerida.LIBERAR_ACESSO,
            Intencao.Saudacao => AcaoSugerida.SOLICITAR_IDENTIFICACAO,
            Intencao.Identificacao => DeriveIdentificacaoAction(dados, regras),
            _ => AcaoSugerida.SOLICITAR_IDENTIFICACAO
        };
    }

    private static AcaoSugerida DeriveIdentificacaoAction(
        DadosExtraidos dados,
        Domain.Rules.TenantRule? regras)
    {
        var hasDestinoExplicito =
            !string.IsNullOrWhiteSpace(dados.Unidade)
            || !string.IsNullOrWhiteSpace(dados.Bloco)
            || !string.IsNullOrWhiteSpace(dados.Torre)
            || !string.IsNullOrWhiteSpace(dados.Empresa)
            || !string.IsNullOrWhiteSpace(dados.Parentesco)
            || (
                !string.IsNullOrWhiteSpace(dados.Nome)
                && !string.Equals(dados.Nome, dados.NomeVisitante, StringComparison.OrdinalIgnoreCase)
            );

        // Caso clássico: "Olá, meu nome é X" sem informar com quem/finalidade.
        // Nessa situação não há base para notificar morador ainda.
        if (!hasDestinoExplicito && string.IsNullOrWhiteSpace(dados.Documento) && string.IsNullOrWhiteSpace(dados.Cpf))
            return AcaoSugerida.SOLICITAR_IDENTIFICACAO;

        if (dados.Documento is null)
            return AcaoSugerida.SOLICITAR_DOC;

        if (regras?.AccessRules.RequireMoradorConfirmation == true)
            return AcaoSugerida.NOTIFICAR_MORADOR;

        return AcaoSugerida.AGUARDAR_CONFIRMACAO;
    }

    private static string DeriveResponseText(
        Intencao intencao,
        AcaoSugerida acao,
        Domain.Rules.TenantRule? regras)
    {
        var templates = regras?.Responses;

        return intencao switch
        {
            Intencao.Saudacao => templates?.Saudacao ?? "Olá! Bem-vindo. Como posso ajudar?",
            Intencao.Despedida => templates?.Despedida ?? "Obrigado pela visita. Tenha um bom dia!",
            Intencao.Urgencia => templates?.Urgencia ?? "Entendido. Comunicando a emergência imediatamente.",
            _ => acao switch
            {
                AcaoSugerida.SOLICITAR_DOC => templates?.SolicitarDocumento ?? "Por favor, me informe seu documento.",
                AcaoSugerida.SOLICITAR_IDENTIFICACAO => "Perfeito. Para prosseguir, informe com quem deseja falar ou a unidade de destino.",
                AcaoSugerida.NOTIFICAR_MORADOR or AcaoSugerida.AGUARDAR_MORADOR =>
                    templates?.Aguardar ?? "Aguarde um momento, notificando o morador.",
                _ => "Aguarde um momento, por favor."
            }
        };
    }

    private static bool ShouldAskClarificationBeforeLlm(string? texto, ProcessingState state)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return true;

        var cleaned = texto.Trim();
        if (cleaned.Length < 8)
            return true;

        var tokens = cleaned
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length > 1)
            .ToArray();

        if (tokens.Length <= 2)
            return true;

        var letters = cleaned.Count(char.IsLetter);
        var digits = cleaned.Count(char.IsDigit);

        // Sequências majoritariamente numéricas costumam ser ruído de STT fora do contexto.
        if (digits > letters && string.IsNullOrWhiteSpace(state.DocumentoDetectado) && string.IsNullOrWhiteSpace(state.CpfDetectado))
            return true;

        return false;
    }

    private static bool IsSlotFillingRequest(InferenceRequest request)
    {
        if (request.Metadata is null)
            return false;

        if (!request.Metadata.TryGetValue("slotFilling", out var rawValue))
            return false;

        return bool.TryParse(rawValue, out var enabled) && enabled;
    }

    private static ThresholdSettings ResolveThresholds(InferenceRequest request)
    {
        var profile = GetMetadataValue(request, "tenantAiProfile")?.ToUpperInvariant() ?? "CONSERVADOR";

        var defaults = profile switch
        {
            "AGRESSIVO" => new ThresholdSettings(profile, 0.75, 0.60, 0.80),
            "ULTRA_ESTAVEL" => new ThresholdSettings(profile, 0.92, 0.80, 0.96),
            _ => new ThresholdSettings("CONSERVADOR", 0.85, 0.70, 0.90)
        };

        var regex = TryGetThresholdFromMetadata(request, "tenantRegexThreshold")
            ?? defaults.Regex;

        var nlp = TryGetThresholdFromMetadata(request, "tenantNlpThreshold")
            ?? defaults.Nlp;

        var global = TryGetThresholdFromMetadata(request, "tenantGlobalThreshold")
            ?? defaults.Global;

        return new ThresholdSettings(profile, Clamp01(regex), Clamp01(nlp), Clamp01(global));
    }

    private static string? GetMetadataValue(InferenceRequest request, string key)
    {
        if (request.Metadata is null)
            return null;

        return request.Metadata.TryGetValue(key, out var value) ? value : null;
    }

    private static double? TryGetThresholdFromMetadata(InferenceRequest request, string key)
    {
        var raw = GetMetadataValue(request, key);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return null;

        return value;
    }

    private static double Clamp01(double value)
        => Math.Max(0.0, Math.Min(1.0, value));

    private sealed record ThresholdSettings(string Profile, double Regex, double Nlp, double Global);
}
