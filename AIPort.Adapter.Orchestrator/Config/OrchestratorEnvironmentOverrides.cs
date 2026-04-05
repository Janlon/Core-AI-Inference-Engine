using System.Collections.Generic;

namespace AIPort.Adapter.Orchestrator.Config;

public static class OrchestratorEnvironmentOverrides
{
    public static void Apply(ConfigurationManager configuration)
    {
        var urls = FirstNonEmpty(
            Environment.GetEnvironmentVariable("AIPORT_ORCHESTRATOR_SERVER_URLS"),
            Environment.GetEnvironmentVariable("AIPORT_ORCHESTRATOR_URLS"),
            Environment.GetEnvironmentVariable("ASPNETCORE_URLS"));

        var requestHeadersTimeout = FirstNonEmpty(
            Environment.GetEnvironmentVariable("AIPORT_ORCHESTRATOR_SERVER_REQUEST_HEADERS_TIMEOUT_SECONDS"),
            Environment.GetEnvironmentVariable("AIPORT_SERVER_REQUEST_HEADERS_TIMEOUT_SECONDS"));

        var keepAliveTimeout = FirstNonEmpty(
            Environment.GetEnvironmentVariable("AIPORT_ORCHESTRATOR_SERVER_KEEP_ALIVE_TIMEOUT_SECONDS"),
            Environment.GetEnvironmentVariable("AIPORT_SERVER_KEEP_ALIVE_TIMEOUT_SECONDS"));

        var overrides = new Dictionary<string, string?>
        {
            ["InputSourceMode"] = Environment.GetEnvironmentVariable("AIPORT_INPUT_SOURCE_MODE"),

            ["Server:Urls"] = urls,
            ["Server:RequestHeadersTimeoutSeconds"] = requestHeadersTimeout,
            ["Server:KeepAliveTimeoutSeconds"] = keepAliveTimeout,

            ["AgiServer:Enabled"] = Environment.GetEnvironmentVariable("AIPORT_AGI_ENABLED"),
            ["AgiServer:Host"] = Environment.GetEnvironmentVariable("AIPORT_AGI_HOST"),
            ["AgiServer:Port"] = Environment.GetEnvironmentVariable("AIPORT_AGI_PORT"),
            ["AgiServer:Backlog"] = Environment.GetEnvironmentVariable("AIPORT_AGI_BACKLOG"),

            ["IntelligenceService:BaseUrl"] = Environment.GetEnvironmentVariable("AIPORT_INTELLIGENCE_BASE_URL"),
            ["IntelligenceService:ProcessPath"] = Environment.GetEnvironmentVariable("AIPORT_INTELLIGENCE_PROCESS_PATH"),
            ["IntelligenceService:TimeoutMs"] = Environment.GetEnvironmentVariable("AIPORT_INTELLIGENCE_TIMEOUT_MS"),
            ["IntelligenceService:MaxRetryAttempts"] = Environment.GetEnvironmentVariable("AIPORT_INTELLIGENCE_MAX_RETRY_ATTEMPTS"),
            ["IntelligenceService:RetryBaseDelayMs"] = Environment.GetEnvironmentVariable("AIPORT_INTELLIGENCE_RETRY_BASE_DELAY_MS"),

            ["MariaDb:ConnectionString"] = Environment.GetEnvironmentVariable("AIPORT_MARIADB_CONNECTION_STRING"),

            ["Webhook:TimeoutMs"] = Environment.GetEnvironmentVariable("AIPORT_WEBHOOK_TIMEOUT_MS"),
            ["Webhook:MaxRetryAttempts"] = Environment.GetEnvironmentVariable("AIPORT_WEBHOOK_MAX_RETRY_ATTEMPTS"),
            ["Webhook:RetryBaseDelayMs"] = Environment.GetEnvironmentVariable("AIPORT_WEBHOOK_RETRY_BASE_DELAY_MS"),

            ["Speech:TtsProvider"] = Environment.GetEnvironmentVariable("AIPORT_TTS_PROVIDER"),
            ["Speech:Asterisk:TtsApplication"] = Environment.GetEnvironmentVariable("AIPORT_ASTERISK_TTS_APP"),
            ["Speech:Google:CredentialsPath"] = Environment.GetEnvironmentVariable("AIPORT_GOOGLE_CREDENTIALS_PATH"),
            ["Speech:Google:UseStreamingStt"] = Environment.GetEnvironmentVariable("AIPORT_GOOGLE_USE_STREAMING_STT"),
            ["Speech:Google:TempDirectory"] = Environment.GetEnvironmentVariable("AIPORT_GOOGLE_TEMP_DIRECTORY"),
            ["Speech:Google:TtsLanguageCode"] = Environment.GetEnvironmentVariable("AIPORT_GOOGLE_TTS_LANGUAGE_CODE"),
            ["Speech:Google:TtsVoiceName"] = Environment.GetEnvironmentVariable("AIPORT_GOOGLE_TTS_VOICE_NAME"),
            ["Speech:Google:TtsSampleRateHertz"] = Environment.GetEnvironmentVariable("AIPORT_GOOGLE_TTS_SAMPLE_RATE_HZ"),
            ["Speech:Google:TtsSpeakingRate"] = Environment.GetEnvironmentVariable("AIPORT_GOOGLE_TTS_SPEAKING_RATE"),
            ["Speech:Google:SttLanguageCode"] = Environment.GetEnvironmentVariable("AIPORT_GOOGLE_STT_LANGUAGE_CODE"),
            ["Speech:Google:SttSampleRateHertz"] = Environment.GetEnvironmentVariable("AIPORT_GOOGLE_STT_SAMPLE_RATE_HZ"),

            ["DeveloperSandbox:TenantPid"] = Environment.GetEnvironmentVariable("AIPORT_SANDBOX_TENANT_PID"),
            ["DeveloperSandbox:CallerId"] = Environment.GetEnvironmentVariable("AIPORT_SANDBOX_CALLER_ID"),
            ["DeveloperSandbox:CalledNumber"] = Environment.GetEnvironmentVariable("AIPORT_SANDBOX_CALLED_NUMBER"),
            ["DeveloperSandbox:Context"] = Environment.GetEnvironmentVariable("AIPORT_SANDBOX_CONTEXT"),
            ["DeveloperSandbox:SessionPrefix"] = Environment.GetEnvironmentVariable("AIPORT_SANDBOX_SESSION_PREFIX"),
            ["DeveloperSandbox:DisableWebhookCalls"] = Environment.GetEnvironmentVariable("AIPORT_SANDBOX_DISABLE_WEBHOOK_CALLS"),
            ["DeveloperSandbox:WindowsVoice:Provider"] = Environment.GetEnvironmentVariable("AIPORT_SANDBOX_WINDOWS_VOICE_PROVIDER"),
            ["DeveloperSandbox:WindowsVoice:SubscriptionKey"] = Environment.GetEnvironmentVariable("AIPORT_SANDBOX_WINDOWS_VOICE_SUBSCRIPTION_KEY"),
            ["DeveloperSandbox:WindowsVoice:Region"] = Environment.GetEnvironmentVariable("AIPORT_SANDBOX_WINDOWS_VOICE_REGION"),
            ["DeveloperSandbox:WindowsVoice:Language"] = Environment.GetEnvironmentVariable("AIPORT_SANDBOX_WINDOWS_VOICE_LANGUAGE")
        };

        configuration.AddInMemoryCollection(overrides.Where(x => !string.IsNullOrWhiteSpace(x.Value))!);
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
