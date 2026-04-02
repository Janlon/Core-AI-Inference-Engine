namespace AIPort.Adapter.Orchestrator.Domain.Models;

/// <summary>
/// Templates de resposta de voz retornados pelo Intelligence Service para um tipo de tenant.
/// Espelha <c>ResponseTemplates</c> de AIPort.Intelligence.Service.
/// </summary>
public sealed record TenantResponsesDto
{
    public string Saudacao { get; init; } = "Olá! Bem-vindo. Aguarde um momento.";
    public string SolicitarDocumento { get; init; } = "Por favor, informe seu documento de identificação.";
    public string Aguardar { get; init; } = "Aguarde um momento, por favor.";
    public string Despedida { get; init; } = "Obrigado pela visita. Tenha um bom dia!";
    public string Urgencia { get; init; } = "Emergência registrada. Acionando equipe imediatamente.";
}
