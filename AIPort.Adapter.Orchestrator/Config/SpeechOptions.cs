namespace AIPort.Adapter.Orchestrator.Config;

public enum TtsProviderType
{
    Google,
    Asterisk
}

public sealed class SpeechOptions
{
    public const string SectionName = "Speech";

    public TtsProviderType TtsProvider { get; set; } = TtsProviderType.Google;

    public GoogleSpeechOptions Google { get; set; } = new();

    public AsteriskSpeechOptions Asterisk { get; set; } = new();
}

public sealed class GoogleSpeechOptions
{
    public string? CredentialsPath { get; set; }
    public bool UseStreamingStt { get; set; }
    public string TempDirectory { get; set; } = "/tmp";

    public string TtsLanguageCode { get; set; } = "pt-BR";
    public string? TtsVoiceName { get; set; } = "pt-BR-Wavenet-A";
    public int TtsSampleRateHertz { get; set; } = 8000;
    public double TtsSpeakingRate { get; set; } = 1.0;

    public string SttLanguageCode { get; set; } = "pt-BR";
    public int SttSampleRateHertz { get; set; } = 8000;
}

public sealed class AsteriskSpeechOptions
{
    public string TtsApplication { get; set; } = "FLITE";
}
