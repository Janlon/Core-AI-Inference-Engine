namespace AIPort.Intelligence.Service.Domain.Options;

/// <summary>Configurações da camada de NLP (Camada 2).</summary>
public sealed class NlpConfig
{
    /// <summary>Se false, a camada NLP é pulada e o sistema vai direto para o LLM.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Quando true, usa o servico Python externo como implementacao oficial da camada NLP.</summary>
    public bool UseExternalApi { get; set; } = true;

    /// <summary>Base URL do servico Python de NLP (ex: http://localhost:8010).</summary>
    public string ExternalApiBaseUrl { get; set; } = "http://localhost:8010";

    /// <summary>Timeout em milissegundos para a chamada HTTP ao servico Python.</summary>
    public int ExternalApiTimeoutMs { get; set; } = 3000;

    /// <summary>Api key opcional enviada no header X-Api-Key ao servico Python.</summary>
    public string? ExternalApiKey { get; set; }

    /// <summary>Legado: mantido para fallback local caso a implementacao interna volte a ser usada.</summary>
    public bool UseSpacy { get; set; } = false;

    /// <summary>Legado: executavel Python usado para invocar o spaCy localmente.</summary>
    public string SpacyPythonExecutable { get; set; } = "/opt/aiport/spacy-venv/bin/python";

    /// <summary>Legado: modelo spaCy local para NER.</summary>
    public string SpacyModel { get; set; } = "pt_core_news_sm";

    /// <summary>Legado: timeout para o spaCy local.</summary>
    public int SpacyTimeoutMs { get; set; } = 2500;

    /// <summary>Legado: habilita Catalyst como fallback local.</summary>
    public bool UseCatalyst { get; set; } = false;

    /// <summary>
    /// Idioma do modelo NER.
    /// Exemplos: "pt" (Português), "en" (Inglês).
    /// </summary>
    public string ModelLanguage { get; set; } = "pt";

    /// <summary>Confiança mínima para aceitar o resultado desta camada.</summary>
    public double ConfidenceThreshold { get; set; } = 0.75;
}

/// <summary>Configurações da camada Regex (Camada 1).</summary>
public sealed class RegexConfig
{
    /// <summary>Se false, a camada Regex é pulada e o fluxo inicia no NLP.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Confiança mínima para aceitar o resultado desta camada.</summary>
    public double ConfidenceThreshold { get; set; } = 0.95;

    /// <summary>Timeout em milissegundos para cada operação de Regex individual.</summary>
    public int TimeoutMs { get; set; } = 100;
}

/// <summary>Configurações da camada LLM (Camada 3).</summary>
public sealed class LlmConfig
{
    /// <summary>Timeout HTTP em segundos para chamadas a provedores LLM.</summary>
    public int ProviderTimeoutSeconds { get; set; } = 30;

    /// <summary>Temperatura padrão para geração de resposta.</summary>
    public double Temperature { get; set; } = 0.1;

    /// <summary>Limite de tokens de saída por chamada ao provedor.</summary>
    public int MaxTokens { get; set; } = 500;
}
