namespace AIPort.Intelligence.Service.Domain.Enums;

/// <summary>
/// Comandos que o motor de decisão envia ao Orquestrador externo.
/// Nomes em UPPER_SNAKE para compatibilidade com sistemas legados de portaria.
/// </summary>
public enum AcaoSugerida
{
    /// <summary>Solicitar documento de identificação (CPF, RG, CNH).</summary>
    SOLICITAR_DOC,

    /// <summary>Aguardar o morador/responsável confirmar permissão de entrada.</summary>
    AGUARDAR_MORADOR,

    /// <summary>Enviar notificação (push/intercomunicador) ao morador.</summary>
    NOTIFICAR_MORADOR,

    /// <summary>Liberar acesso imediatamente (visita confirmada).</summary>
    LIBERAR_ACESSO,

    /// <summary>Negar acesso e registrar tentativa.</summary>
    NEGAR_ACESSO,

    /// <summary>Escalar para atendimento humano.</summary>
    ESCALAR_HUMANO,

    /// <summary>Aguardar confirmação adicional antes de prosseguir.</summary>
    AGUARDAR_CONFIRMACAO,

    /// <summary>Solicitar identificação inicial sem especificar documento.</summary>
    SOLICITAR_IDENTIFICACAO
}
