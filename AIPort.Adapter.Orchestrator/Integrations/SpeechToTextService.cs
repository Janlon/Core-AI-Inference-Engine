using AIPort.Adapter.Orchestrator.Config;
using AIPort.Adapter.Orchestrator.Integrations.Interfaces;
using Google.Cloud.Speech.V1;
using Microsoft.Extensions.Options;

namespace AIPort.Adapter.Orchestrator.Integrations;

public sealed class SpeechToTextService : ISpeechToTextService
{
    private readonly SpeechOptions _options;
    private readonly ILogger<SpeechToTextService> _logger;

    public SpeechToTextService(IOptions<SpeechOptions> options, ILogger<SpeechToTextService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> TranscribeAsync(string? audioPath, string? preTranscribedText, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(preTranscribedText))
            return preTranscribedText;

        if (string.IsNullOrWhiteSpace(audioPath))
            return string.Empty;

        EnsureCredentials();

        var actualPath = ResolveAudioPath(audioPath);
        if (!File.Exists(actualPath))
        {
            _logger.LogWarning("Arquivo de áudio para STT não encontrado: {Path}", actualPath);
            return string.Empty;
        }

        var bytes = await File.ReadAllBytesAsync(actualPath, ct);
        var audio = RecognitionAudio.FromBytes(bytes);

        var config = new RecognitionConfig
        {
            Encoding = RecognitionConfig.Types.AudioEncoding.Linear16,
            SampleRateHertz = _options.Google.SttSampleRateHertz,
            LanguageCode = _options.Google.SttLanguageCode,
            AudioChannelCount = 1,
            EnableAutomaticPunctuation = true
        };

        var client = await SpeechClient.CreateAsync();
        var response = await client.RecognizeAsync(new RecognizeRequest
        {
            Config = config,
            Audio = audio
        }, cancellationToken: ct);

        var transcript = string.Join(' ', response.Results
            .Select(r => r.Alternatives.FirstOrDefault()?.Transcript)
            .Where(t => !string.IsNullOrWhiteSpace(t)));

        _logger.LogInformation("STT concluído. Texto='{Transcript}'", transcript);
        return transcript;
    }

    private string ResolveAudioPath(string path)
    {
        if (File.Exists(path))
            return path;

        var withWav = path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ? path : path + ".wav";
        return withWav;
    }

    private void EnsureCredentials()
    {
        if (string.IsNullOrWhiteSpace(_options.Google.CredentialsPath))
            return;

        if (!File.Exists(_options.Google.CredentialsPath))
            throw new FileNotFoundException("Google credentials file not found.", _options.Google.CredentialsPath);

        Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", _options.Google.CredentialsPath);
    }
}
