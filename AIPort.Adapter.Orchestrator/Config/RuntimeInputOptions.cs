namespace AIPort.Adapter.Orchestrator.Config;

public enum InputSourceMode
{
    Asterisk,
    WindowsText,
    WindowsVoice
}

public sealed class RuntimeInputOptions
{
    public InputSourceMode InputSourceMode { get; set; } = InputSourceMode.Asterisk;
    public DeveloperSandboxOptions DeveloperSandbox { get; set; } = new();
}

public sealed class DeveloperSandboxOptions
{
    public int TenantPid { get; set; } = 200;
    public string CallerId { get; set; } = "DEV-LOCAL";
    public string CalledNumber { get; set; } = "WINDOWS-SANDBOX";
    public string Context { get; set; } = "developer-sandbox";
    public string SessionPrefix { get; set; } = "sandbox";
    public bool DisableWebhookCalls { get; set; } = true;
    public bool VerboseConsoleOutput { get; set; } = true;
    public WindowsVoiceInputOptions WindowsVoice { get; set; } = new();
}

public sealed class WindowsVoiceInputOptions
{
    public string Provider { get; set; } = "SystemSpeech";
    public string? SubscriptionKey { get; set; }
    public string? Region { get; set; }
    public string Language { get; set; } = "pt-BR";
}