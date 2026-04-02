using System.ComponentModel.DataAnnotations;

namespace AIPort.Intelligence.Service.Domain.Models;

/// <summary>
/// Contrato de entrada para o endpoint de inferência.
/// </summary>
public record InferenceRequest
{
    /// <summary>Texto bruto capturado pelo sistema de reconhecimento de voz.</summary>
    [Required(ErrorMessage = "O campo Texto é obrigatório.")]
    [MinLength(1, ErrorMessage = "O texto não pode estar vazio.")]
    [MaxLength(2000, ErrorMessage = "O texto não pode exceder 2000 caracteres.")]
    public required string Texto { get; init; }

    /// <summary>
    /// Tipo do tenant/condomínio para aplicar regras específicas.
    /// Exemplos: "residential", "hospital", "corporate".
    /// Padrão: "residential".
    /// </summary>
    public string TenantType { get; init; } = "residential";

    /// <summary>Identificador único da sessão de atendimento.</summary>
    public string? SessionId { get; init; }

    /// <summary>Metadados adicionais opcionais (câmera, dispositivo etc.).</summary>
    public IDictionary<string, string>? Metadata { get; init; }
}
