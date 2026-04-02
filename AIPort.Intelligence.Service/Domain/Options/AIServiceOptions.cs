namespace AIPort.Intelligence.Service.Domain.Options;

/// <summary>
/// Configurações raiz do serviço de IA.
/// Mapeadas da seção "AIService" no appsettings.json.
/// </summary>
public sealed class AIServiceOptions
{
    public const string SectionName = "AIService";

    /// <summary>
    /// Score mínimo de confiança global. Se nenhuma camada atingir este valor,
    /// o sistema escala para a camada seguinte.
    /// </summary>
    public double GlobalConfidenceThreshold { get; set; } = 0.85;

    /// <summary>Caminho para o arquivo JSON de regras de fluxo por tenant.</summary>
    public string RulesFilePath { get; set; } = "rules/tenant-rules.json";

    /// <summary>
    /// Lista de provedores LLM configurados.
    /// O motor iterará em ordem de Priority (menor = maior prioridade) e usará
    /// o primeiro disponível. Isso permite fallback automático entre provedores.
    /// </summary>
    public List<LlmProviderConfig> LlmProviders { get; set; } = [];

    /// <summary>Configurações da camada NLP (Camada 2).</summary>
    public NlpConfig Nlp { get; set; } = new();

    /// <summary>Configurações da camada Regex (Camada 1).</summary>
    public RegexConfig Regex { get; set; } = new();

    /// <summary>Configurações de execução da camada LLM (Camada 3).</summary>
    public LlmConfig Llm { get; set; } = new();
}
