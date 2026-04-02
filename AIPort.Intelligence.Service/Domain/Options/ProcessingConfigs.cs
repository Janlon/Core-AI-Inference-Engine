namespace AIPort.Intelligence.Service.Domain.Options;

/// <summary>Configurações da camada de NLP (Camada 2).</summary>
public sealed class NlpConfig
{
    /// <summary>Se false, a camada NLP é pulada e o sistema vai direto para o LLM.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Habilita processamento NLP via spaCy (Python) como primeira tentativa.</summary>
    public bool UseSpacy { get; set; } = false;

    /// <summary>Executavel Python usado para invocar o spaCy (ex: python, py, caminho absoluto).</summary>
    public string SpacyPythonExecutable { get; set; } = "/opt/aiport/spacy-venv/bin/python";

    /// <summary>Modelo spaCy para NER (ex: pt_core_news_sm, pt_core_news_lg).</summary>
    public string SpacyModel { get; set; } = "pt_core_news_sm";

    /// <summary>Timeout em milissegundos para cada chamada externa ao spaCy.</summary>
    public int SpacyTimeoutMs { get; set; } = 2500;

    /// <summary>Habilita Catalyst como segunda tentativa (antes do fallback heuristico).</summary>
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
