namespace AIPort.Intelligence.Service.Domain.Enums;

/// <summary>
/// Representa a intenção detectada na fala do visitante.
/// </summary>
public enum Intencao
{
    /// <summary>Visitante está cumprimentando — início de interação.</summary>
    Saudacao,

    /// <summary>Visitante está se identificando ou informando o destino.</summary>
    Identificacao,

    /// <summary>Visitante está encerrando a interação.</summary>
    Despedida,

    /// <summary>Visitante está reportando uma emergência ou pedido urgente.</summary>
    Urgencia,

    /// <summary>Intenção não pôde ser determinada com confiança suficiente.</summary>
    Indefinida
}
