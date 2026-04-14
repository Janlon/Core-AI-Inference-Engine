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

    [Fact]
    public void MergeFrom_VisitorNameThenResidentName_PreservesBothRoles()
    {
        var context = new VisitContext();

        context.MergeFrom(new DadosExtraidosDto
        {
            NomeVisitante = "Fernando",
            TemDadosExtraidos = true
        });

        context.MergeFrom(new DadosExtraidosDto
        {
            Nome = "Giovana",
            TemDadosExtraidos = true
        });

        Assert.Equal("Fernando", context.VisitorName);
        Assert.Equal("Giovana", context.ResidentName);
    }

    [Fact]
    public void MergeFrom_ResidentNameWithDuplicatedVisitorField_UsesExistingVisitorAsReference()
    {
        var context = new VisitContext
        {
            VisitorName = "João Pedro"
        };

        context.MergeFrom(new DadosExtraidosDto
        {
            Nome = "Rodrigo",
            NomeVisitante = "Rodrigo",
            TemDadosExtraidos = true
        });

        Assert.Equal("João Pedro", context.VisitorName);
        Assert.Equal("Rodrigo", context.ResidentName);
        Assert.True(context.HasChangedSinceLastMerge);
    }
}