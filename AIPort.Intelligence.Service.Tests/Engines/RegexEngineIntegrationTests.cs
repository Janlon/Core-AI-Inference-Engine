using System.Threading.Tasks;
using Xunit;
using AIPort.Intelligence.Service.Services.Engines;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIPort.Intelligence.Service.Tests.Engines;

public class RegexEngineIntegrationTests
{
    private RegexEngine CreateEngine()
    {
        var options = Options.Create(new Domain.Options.AIServiceOptions());
        var logger = NullLogger<RegexEngine>.Instance;
        return new RegexEngine(options, logger);
    }

    [Theory]
    [InlineData("Oi, sou João, vim visitar o apartamento 204 bloco 12.", "João", "204", "12", null, null, false, false)]
    [InlineData("Boa noite, aqui é Maria, entrega do iFood para o 305.", "Maria", "305", null, null, null, false, true)]
    [InlineData("Quero falar com o Pedro na torre B.", "Pedro", null, null, "B", null, false, false)]
    [InlineData("Sou Ana, vim para a festa da família no bloco 3.", "Ana", null, "3", null, null, false, false)]
    [InlineData("Entregador, pedido para o apartamento 101.", null, "101", null, null, null, false, true)]
    [InlineData("Meu nome é Carlos, vim ver minha mãe no 402.", "Carlos", "402", null, null, null, false, false)]
    [InlineData("Olá, vim para manutenção no prédio.", null, null, null, null, null, false, false)]
    [InlineData("Sou o Rafael, preciso falar com o síndico.", "Rafael", null, null, null, null, false, false)]
    public async Task RegexEngine_ExtractsExpectedFields(
        string texto, string expectedNome, string expectedUnidade, string expectedBloco, string expectedTorre, string expectedEmpresa, bool expectedVeiculo, bool expectedEntregador)
    {
        var engine = CreateEngine();
        var result = await engine.ProcessAsync(texto);
        var dados = result.DadosExtraidos;

        Assert.Equal(expectedNome, dados.NomeVisitante);
        Assert.Equal(expectedUnidade, dados.Unidade);
        Assert.Equal(expectedBloco, dados.Bloco);
        Assert.Equal(expectedTorre, dados.Torre);
        Assert.Equal(expectedEmpresa, dados.Empresa);
        Assert.Equal(expectedVeiculo, dados.EstaComVeiculo);
        Assert.Equal(expectedEntregador, dados.EEntregador);
    }

    [Fact]
    public async Task RegexEngine_ReturnsDebugMatches_ForTriggeredRules()
    {
        var engine = CreateEngine();

        var result = await engine.ProcessAsync("Oi, sou João, vim visitar o apartamento 204 bloco 12.");

        Assert.NotNull(result.Debug);
        Assert.Contains(result.Debug!.RegexMatches, match => match.Rule == "SaudacaoPattern");
        Assert.Contains(result.Debug.RegexMatches, match => match.Rule == "NomePattern" && match.Value == "João");
        Assert.Contains(result.Debug.RegexMatches, match => match.Rule == "UnidadePattern_Novo" && match.Value == "204");
        Assert.Contains(result.Debug.RegexMatches, match => match.Rule == "BlocoPattern" && match.Value == "12");
    }
}
