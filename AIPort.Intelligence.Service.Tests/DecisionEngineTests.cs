using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AIPort.Intelligence.Service.Domain.Enums;
using AIPort.Intelligence.Service.Domain.Models;
using AIPort.Intelligence.Service.Domain.Options;
using AIPort.Intelligence.Service.Domain.Rules;
using AIPort.Intelligence.Service.Services;
using AIPort.Intelligence.Service.Services.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AIPort.Intelligence.Service.Tests;

public sealed class DecisionEngineTests
{
    [Fact]
    public async Task ProcessAsync_VisitorNameAndUnitWithoutDocument_ReturnsSolicitarDoc()
    {
        var regexResult = new ProcessingLayerResult
        {
            Camada = "Regex",
            Confianca = 0.97,
            Intencao = Intencao.Identificacao,
            DadosExtraidos = new DadosExtraidos
            {
                Nome = "João",
                NomeVisitante = "João",
                Unidade = "204",
                Bloco = "12"
            }
        };

        var sut = CreateSut(
            regexProcessor: new StubRegexProcessor(regexResult),
            rulesLoader: new StubRulesLoader(CreateResidentialRule(requireMoradorConfirmation: true)));

        var result = await sut.ProcessAsync(new InferenceRequest
        {
            Texto = "João bloco 12 apartamento 204",
            TenantType = "residential",
            SessionId = "test-session"
        }, CancellationToken.None);

        Assert.Equal(Intencao.Identificacao, result.Intencao);
        Assert.Equal(AcaoSugerida.SOLICITAR_DOC, result.AcaoSugerida);
    }

    [Fact]
    public async Task ProcessAsync_LlmNotifyWithoutDocument_IsNormalizedToSolicitarDoc()
    {
        var regexResult = new ProcessingLayerResult
        {
            Camada = "Regex",
            Confianca = 0.20,
            Intencao = Intencao.Identificacao,
            DadosExtraidos = new DadosExtraidos
            {
                NomeVisitante = "João",
                Unidade = "204"
            }
        };

        var llmResult = new ProcessingLayerResult
        {
            Camada = "LLM",
            Confianca = 0.91,
            Intencao = Intencao.Identificacao,
            AcaoSugerida = AcaoSugerida.NOTIFICAR_MORADOR,
            DadosExtraidos = new DadosExtraidos
            {
                NomeVisitante = "João",
                Unidade = "204"
            }
        };

        var sut = CreateSut(
            regexProcessor: new StubRegexProcessor(regexResult),
            llmProcessor: new StubLlmProcessor(llmResult),
            rulesLoader: new StubRulesLoader(CreateResidentialRule(requireMoradorConfirmation: true)));

        var result = await sut.ProcessAsync(new InferenceRequest
        {
            Texto = "João apartamento 204",
            TenantType = "residential",
            SessionId = "test-session"
        }, CancellationToken.None);

        Assert.Equal(AcaoSugerida.SOLICITAR_DOC, result.AcaoSugerida);
        Assert.Equal("LLM-Normalized", result.CamadaResolucao);
    }

    [Fact]
    public async Task ProcessAsync_SlotFillingResidentOnly_DoesNotMirrorResidentIntoVisitorName()
    {
        var regexResult = new ProcessingLayerResult
        {
            Camada = "Regex",
            Confianca = 0.20,
            Intencao = Intencao.Identificacao,
            DadosExtraidos = new DadosExtraidos
            {
                Nome = "Giovana",
                NomeVisitante = null,
                Unidade = "214",
                Bloco = "12"
            }
        };

        var sut = CreateSut(
            regexProcessor: new StubRegexProcessor(regexResult),
            nlpProcessor: new StubNlpProcessor(new ProcessingLayerResult { Camada = "NLP-Heuristic", Confianca = 0.0, DadosExtraidos = new DadosExtraidos() }));

        var result = await sut.ProcessAsync(new InferenceRequest
        {
            Texto = "Giovana apartamento 214 bloco 12",
            TenantType = "residential",
            SessionId = "test-session",
            Metadata = new Dictionary<string, string>
            {
                ["slotFilling"] = "true"
            }
        }, CancellationToken.None);

        Assert.Equal("SlotFilling-FastPath", result.CamadaResolucao);
        Assert.Equal("Giovana", result.DadosExtraidos.Nome);
        Assert.Null(result.DadosExtraidos.NomeVisitante);
    }

    private static DecisionEngine CreateSut(
        IRegexProcessor regexProcessor = null,
        INlpProcessor nlpProcessor = null,
        ILlmProcessor llmProcessor = null,
        IRulesLoader rulesLoader = null)
    {
        return new DecisionEngine(
            regexProcessor ?? new StubRegexProcessor(new ProcessingLayerResult { Camada = "Regex", Confianca = 0.0 }),
            nlpProcessor ?? new StubNlpProcessor(new ProcessingLayerResult { Camada = "NLP-Heuristic", Confianca = 0.0 }),
            llmProcessor ?? new StubLlmProcessor(new ProcessingLayerResult { Camada = "LLM", Confianca = 0.0, Intencao = Intencao.Indefinida }),
            rulesLoader ?? new StubRulesLoader(CreateResidentialRule(requireMoradorConfirmation: true)),
            Options.Create(new AIServiceOptions()),
            NullLogger<DecisionEngine>.Instance);
    }

    private static TenantRule CreateResidentialRule(bool requireMoradorConfirmation)
    {
        return new TenantRule
        {
            TenantType = "residential",
            DisplayName = "Residential",
            AccessRules = new AccessRule
            {
                RequireMoradorConfirmation = requireMoradorConfirmation,
                VisitantDefaultAction = AcaoSugerida.NOTIFICAR_MORADOR
            },
            Responses = new ResponseTemplates()
        };
    }

    private sealed class StubRegexProcessor(ProcessingLayerResult result) : IRegexProcessor
    {
        public Task<ProcessingLayerResult> ProcessAsync(string texto, CancellationToken ct = default) => Task.FromResult(result);
    }

    private sealed class StubNlpProcessor(ProcessingLayerResult result) : INlpProcessor
    {
        public Task<ProcessingLayerResult> ProcessAsync(string texto, ProcessingState estadoAtual, CancellationToken ct = default) => Task.FromResult(result);
    }

    private sealed class StubLlmProcessor(ProcessingLayerResult result) : ILlmProcessor
    {
        public Task<ProcessingLayerResult> ProcessAsync(string texto, ProcessingState estadoAtual, TenantRule regrasTenant, CancellationToken ct = default) => Task.FromResult(result);
    }

    private sealed class StubRulesLoader(TenantRule rule) : IRulesLoader
    {
        public TenantRule GetRule(string tenantType) => string.Equals(tenantType, rule.TenantType, System.StringComparison.OrdinalIgnoreCase) ? rule : null;

        public IReadOnlyList<TenantRule> GetAll() => new List<TenantRule> { rule };
    }
}