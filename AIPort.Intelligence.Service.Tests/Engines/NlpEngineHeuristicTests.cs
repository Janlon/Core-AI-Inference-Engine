using System.Threading;
using System.Threading.Tasks;
using AIPort.Intelligence.Service.Domain.Enums;
using AIPort.Intelligence.Service.Domain.Models;
using AIPort.Intelligence.Service.Domain.Options;
using AIPort.Intelligence.Service.Services.Engines;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AIPort.Intelligence.Service.Tests.Engines;

public sealed class NlpEngineHeuristicTests
{
    private static NlpEngine CreateEngine()
    {
        var options = Options.Create(new AIServiceOptions
        {
            Nlp = new NlpConfig
            {
                Enabled = true,
                UseCatalyst = false,
                UseSpacy = false
            }
        });

        return new NlpEngine(options, NullLogger<NlpEngine>.Instance);
    }

    [Fact]
    public async Task ProcessAsync_GreetingOnly_DoesNotExtractName()
    {
        var engine = CreateEngine();
        var state = new ProcessingState("ola", "residential")
        {
            Intencao = Intencao.Saudacao,
            MelhorConfianca = 0.43
        };

        var result = await engine.ProcessAsync("ola", state, CancellationToken.None);

        Assert.Equal(Intencao.Saudacao, result.Intencao);
        Assert.Null(result.DadosExtraidos.Nome);
        Assert.Null(result.DadosExtraidos.NomeVisitante);
    }

    [Theory]
    [InlineData("beleza")]
    [InlineData("joia")]
    [InlineData("baum")]
    [InlineData("bão")]
    public async Task ProcessAsync_ColloquialGreetingOnly_DoesNotExtractName(string texto)
    {
        var engine = CreateEngine();
        var state = new ProcessingState(texto, "residential")
        {
            Intencao = Intencao.Saudacao,
            MelhorConfianca = 0.43
        };

        var result = await engine.ProcessAsync(texto, state, CancellationToken.None);

        Assert.Equal(Intencao.Saudacao, result.Intencao);
        Assert.Null(result.DadosExtraidos.Nome);
        Assert.Null(result.DadosExtraidos.NomeVisitante);
    }

    [Theory]
    [InlineData("entrega")]
    [InlineData("ifood")]
    public async Task ProcessAsync_DeliveryKeywordOnly_DoesNotExtractName(string texto)
    {
        var engine = CreateEngine();
        var state = new ProcessingState(texto, "residential");

        var result = await engine.ProcessAsync(texto, state, CancellationToken.None);

        Assert.Null(result.DadosExtraidos.Nome);
        Assert.Null(result.DadosExtraidos.NomeVisitante);
    }

    [Fact]
    public async Task ProcessAsync_LowercaseRealName_StillExtractsVisitorName()
    {
        var engine = CreateEngine();
        var state = new ProcessingState("joao", "residential");

        var result = await engine.ProcessAsync("joao", state, CancellationToken.None);

        Assert.Equal(Intencao.Identificacao, result.Intencao);
        Assert.Equal("joao", result.DadosExtraidos.Nome);
        Assert.Equal("joao", result.DadosExtraidos.NomeVisitante);
    }
}