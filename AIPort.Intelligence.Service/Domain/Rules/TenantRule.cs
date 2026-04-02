using AIPort.Intelligence.Service.Domain.Enums;

namespace AIPort.Intelligence.Service.Domain.Rules;

/// <summary>
/// Regras de fluxo carregadas do arquivo tenant-rules.json.
/// Permitem ao motor de IA adaptar seu comportamento por tipo de tenant
/// sem alteração de código.
/// </summary>
public sealed class TenantRule
{
    /// <summary>
    /// Identificador do tipo de tenant.
    /// Exemplos: "residential", "hospital", "corporate".
    /// </summary>
    public string TenantType { get; set; } = string.Empty;

    /// <summary>Nome de exibição amigável do tipo de instalação.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Regras de acesso aplicáveis a este tenant.</summary>
    public AccessRule AccessRules { get; set; } = new();

    /// <summary>Respostas de texto pré-definidas para situações comuns.</summary>
    public ResponseTemplates Responses { get; set; } = new();
}

/// <summary>Regras de controle de acesso específicas do tenant.</summary>
public sealed class AccessRule
{
    /// <summary>Se true, o morador deve confirmar antes de liberar acesso.</summary>
    public bool RequireMoradorConfirmation { get; set; } = true;

    /// <summary>Tipos de documento aceitos para identificação.</summary>
    public List<string> AllowedDocuments { get; set; } = ["CPF", "RG", "CNH"];

    /// <summary>Ação padrão quando um visitante não agendado chega.</summary>
    public AcaoSugerida VisitantDefaultAction { get; set; } = AcaoSugerida.NOTIFICAR_MORADOR;

    /// <summary>Ação padrão para entregas.</summary>
    public AcaoSugerida DeliveryDefaultAction { get; set; } = AcaoSugerida.SOLICITAR_IDENTIFICACAO;
}

/// <summary>Templates de resposta de texto para o sistema de voz.</summary>
public sealed class ResponseTemplates
{
    public string Saudacao { get; set; } = "Olá! Bem-vindo. Como posso ajudar?";
    public string SolicitarDocumento { get; set; } = "Por favor, me informe seu documento de identificação.";
    public string Aguardar { get; set; } = "Aguarde um momento, por favor.";
    public string Despedida { get; set; } = "Obrigado pela visita. Tenha um bom dia!";
    public string Urgencia { get; set; } = "Entendido. Comunicando a emergência imediatamente.";
}
