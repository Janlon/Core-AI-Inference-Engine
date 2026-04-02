using AIPort.Adapter.Orchestrator.Domain.Models;

namespace AIPort.Adapter.Orchestrator.Tests.Domain;

public sealed class VisitContextTests
{
    [Fact]
    public void MergeFrom_ExplicitVisitorName_DoesNotMirrorIntoResidentName()
    {
        var context = new VisitContext();

        context.MergeFrom(new DadosExtraidosDto
        {
            NomeVisitante = "João Carlos da Silva",
            Nome = "João Carlos da Silva",
            TemDadosExtraidos = true
        });

        Assert.Equal("João Carlos da Silva", context.VisitorName);
        Assert.Null(context.ResidentName);
        Assert.True(context.HasChangedSinceLastMerge);
    }
}