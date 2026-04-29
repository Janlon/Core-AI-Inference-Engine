using System.Collections.Generic;

namespace AIPort.Intelligence.Service.Domain.Options;

public static class IntelligenceEnvironmentOverrides
{
    public static void Apply(ConfigurationManager configuration)
    {
        var urls = FirstNonEmpty(
            Environment.GetEnvironmentVariable("AIPORT_INTELLIGENCE_SERVER_URLS"),
            Environment.GetEnvironmentVariable("AIPORT_INTELLIGENCE_URLS"),
            Environment.GetEnvironmentVariable("ASPNETCORE_URLS"));

        var requestHeadersTimeout = FirstNonEmpty(
            Environment.GetEnvironmentVariable("AIPORT_INTELLIGENCE_SERVER_REQUEST_HEADERS_TIMEOUT_SECONDS"),
            Environment.GetEnvironmentVariable("AIPORT_SERVER_REQUEST_HEADERS_TIMEOUT_SECONDS"));

        var keepAliveTimeout = FirstNonEmpty(
            Environment.GetEnvironmentVariable("AIPORT_INTELLIGENCE_SERVER_KEEP_ALIVE_TIMEOUT_SECONDS"),
            Environment.GetEnvironmentVariable("AIPORT_SERVER_KEEP_ALIVE_TIMEOUT_SECONDS"));

        var overrides = new Dictionary<string, string?>
        {
            ["Server:Urls"] = urls,
            ["Server:RequestHeadersTimeoutSeconds"] = requestHeadersTimeout,
            ["Server:KeepAliveTimeoutSeconds"] = keepAliveTimeout,

            ["AIService:GlobalConfidenceThreshold"] = Environment.GetEnvironmentVariable("AIPORT_AI_GLOBAL_CONFIDENCE_THRESHOLD"),
            ["AIService:RulesFilePath"] = Environment.GetEnvironmentVariable("AIPORT_AI_RULES_FILE_PATH"),

            ["AIService:Regex:Enabled"] = Environment.GetEnvironmentVariable("AIPORT_AI_REGEX_ENABLED"),
            ["AIService:Regex:ConfidenceThreshold"] = Environment.GetEnvironmentVariable("AIPORT_AI_REGEX_CONFIDENCE_THRESHOLD"),
            ["AIService:Regex:TimeoutMs"] = Environment.GetEnvironmentVariable("AIPORT_AI_REGEX_TIMEOUT_MS"),

            ["AIService:Nlp:Enabled"] = Environment.GetEnvironmentVariable("AIPORT_AI_NLP_ENABLED"),
            ["AIService:Nlp:UseExternalApi"] = Environment.GetEnvironmentVariable("AIPORT_AI_NLP_USE_EXTERNAL_API"),
            ["AIService:Nlp:ExternalApiBaseUrl"] = Environment.GetEnvironmentVariable("AIPORT_AI_NLP_EXTERNAL_API_BASE_URL"),
            ["AIService:Nlp:ExternalApiTimeoutMs"] = Environment.GetEnvironmentVariable("AIPORT_AI_NLP_EXTERNAL_API_TIMEOUT_MS"),
            ["AIService:Nlp:ExternalApiKey"] = Environment.GetEnvironmentVariable("AIPORT_AI_NLP_EXTERNAL_API_KEY"),
            ["AIService:Nlp:UseSpacy"] = Environment.GetEnvironmentVariable("AIPORT_AI_NLP_USE_SPACY"),
            ["AIService:Nlp:SpacyPythonExecutable"] = Environment.GetEnvironmentVariable("AIPORT_AI_NLP_SPACY_PYTHON"),
            ["AIService:Nlp:SpacyModel"] = Environment.GetEnvironmentVariable("AIPORT_AI_NLP_SPACY_MODEL"),
            ["AIService:Nlp:SpacyTimeoutMs"] = Environment.GetEnvironmentVariable("AIPORT_AI_NLP_SPACY_TIMEOUT_MS"),
            ["AIService:Nlp:UseCatalyst"] = Environment.GetEnvironmentVariable("AIPORT_AI_NLP_USE_CATALYST"),
            ["AIService:Nlp:ModelLanguage"] = Environment.GetEnvironmentVariable("AIPORT_AI_NLP_MODEL_LANGUAGE"),
            ["AIService:Nlp:ConfidenceThreshold"] = Environment.GetEnvironmentVariable("AIPORT_AI_NLP_CONFIDENCE_THRESHOLD"),

            ["AIService:Llm:ProviderTimeoutSeconds"] = Environment.GetEnvironmentVariable("AIPORT_AI_LLM_PROVIDER_TIMEOUT_SECONDS"),
            ["AIService:Llm:Temperature"] = Environment.GetEnvironmentVariable("AIPORT_AI_LLM_TEMPERATURE"),
            ["AIService:Llm:MaxTokens"] = Environment.GetEnvironmentVariable("AIPORT_AI_LLM_MAX_TOKENS"),

            ["AIService:LlmProviders:0:Name"] = Environment.GetEnvironmentVariable("AIPORT_AI_LLM_PRIMARY_NAME"),
            ["AIService:LlmProviders:0:ServiceType"] = Environment.GetEnvironmentVariable("AIPORT_AI_LLM_PRIMARY_SERVICE_TYPE"),
            ["AIService:LlmProviders:0:ApiKey"] = Environment.GetEnvironmentVariable("AIPORT_AI_LLM_PRIMARY_API_KEY"),
            ["AIService:LlmProviders:0:Model"] = Environment.GetEnvironmentVariable("AIPORT_AI_LLM_PRIMARY_MODEL"),
            ["AIService:LlmProviders:0:DeploymentName"] = Environment.GetEnvironmentVariable("AIPORT_AI_LLM_PRIMARY_DEPLOYMENT_NAME"),
            ["AIService:LlmProviders:0:Endpoint"] = Environment.GetEnvironmentVariable("AIPORT_AI_LLM_PRIMARY_ENDPOINT"),
            ["AIService:LlmProviders:0:IsEnabled"] = Environment.GetEnvironmentVariable("AIPORT_AI_LLM_PRIMARY_ENABLED"),
            ["AIService:LlmProviders:0:Priority"] = Environment.GetEnvironmentVariable("AIPORT_AI_LLM_PRIMARY_PRIORITY")
        };

        configuration.AddInMemoryCollection(overrides.Where(x => !string.IsNullOrWhiteSpace(x.Value))!);
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
