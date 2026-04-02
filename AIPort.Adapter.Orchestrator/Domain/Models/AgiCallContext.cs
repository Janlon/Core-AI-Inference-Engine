namespace AIPort.Adapter.Orchestrator.Domain.Models;

using AIPort.Adapter.Orchestrator.Domain.Abstractions;

public sealed class AgiCallContext
{
    public string SessionId { get; set; } = string.Empty;
    public string UniqueId { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string CallerId { get; set; } = string.Empty;
    public string CalledNumber { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public int TenantPid { get; set; }
    public string? AudioFilePath { get; set; }
    public string? PreTranscribedText { get; set; }
    public IVoiceChannel? VoiceChannel { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Objeto de estado para slot-filling residencial. Preenchido durante a coleta de
    /// dados; quando <see cref="VisitContext.IsComplete"/> for <c>true</c> o fluxo
    /// principal pula a inferência e vai direto à notificação do morador.
    /// </summary>
    public VisitContext? VisitContext { get; set; }

    /// <summary>
    /// Indica que o fluxo residencial detectou silêncio consecutivo e deve escalar
    /// para atendimento humano para evitar repetição indefinida de prompts.
    /// </summary>
    public bool EscalateDueToSilence { get; set; }
}
