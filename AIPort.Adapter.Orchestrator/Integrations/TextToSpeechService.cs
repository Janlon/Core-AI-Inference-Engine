using AIPort.Adapter.Orchestrator.Config;
using AIPort.Adapter.Orchestrator.Integrations.Interfaces;
using Google.Cloud.TextToSpeech.V1;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace AIPort.Adapter.Orchestrator.Integrations;

public sealed class TextToSpeechService : ITextToSpeechService
{
    private readonly SpeechOptions _options;
    private readonly RuntimeInputOptions _runtimeOptions;
    private readonly ILogger<TextToSpeechService> _logger;
    private readonly SemaphoreSlim _googleClientLock = new(1, 1);
    private TextToSpeechClient? _googleClient;

    public TextToSpeechService(
        IOptions<SpeechOptions> options,
        IOptions<RuntimeInputOptions> runtimeOptions,
        ILogger<TextToSpeechService> logger)
    {
        _options = options.Value;
        _runtimeOptions = runtimeOptions.Value;
        _logger = logger;
    }

    public async Task<string> SynthesizeAsync(string text, CancellationToken ct = default)
    {
        if (IsDeveloperSandboxMode())
        {
            return "sandbox-tts://" + Uri.EscapeDataString(text);
        }

        if (IsAsteriskProvider())
        {
            var app = string.IsNullOrWhiteSpace(_options.Asterisk.TtsApplication)
                ? "FLITE"
                : _options.Asterisk.TtsApplication.Trim();
            var marker = "asterisk-tts://" + app + "/" + Uri.EscapeDataString(text);
            _logger.LogInformation("TTS provider Asterisk selecionado. Texto será reproduzido via AGI app {App}.", app);
            return marker;
        }

        EnsureCredentials();

        var tempDir = ResolveTempDirectory();
        Directory.CreateDirectory(tempDir);

        var fileNoExt = Path.Combine(tempDir, $"tts-{Guid.NewGuid():N}");
        var filePath = fileNoExt + ".sln";

        var timer = Stopwatch.StartNew();
        var wavBytes = await SynthesizeGoogleAudioAsync(text, AudioEncoding.Linear16, ct);
        var pcmBytes = ExtractRawPcmFromWav(wavBytes);
        await File.WriteAllBytesAsync(filePath, pcmBytes, ct);
        timer.Stop();

        _logger.LogInformation("TTS gerado em {Path} (PCM/sln 8kHz mono, {Bytes} bytes, {ElapsedMs} ms)", filePath, pcmBytes.Length, timer.ElapsedMilliseconds);
        return fileNoExt;
    }

    public async Task WarmupAsync(CancellationToken ct = default)
    {
        if (IsAsteriskProvider())
            return;

        EnsureCredentials();

        var timer = Stopwatch.StartNew();
        var client = await GetGoogleClientAsync(ct);
        await client.SynthesizeSpeechAsync(
            new SynthesizeSpeechRequest
            {
                Input = new SynthesisInput { Text = "ok" },
                Voice = new VoiceSelectionParams
                {
                    LanguageCode = _options.Google.TtsLanguageCode,
                    Name = _options.Google.TtsVoiceName ?? string.Empty
                },
                AudioConfig = new AudioConfig
                {
                    AudioEncoding = AudioEncoding.Linear16,
                    SampleRateHertz = 8000,
                    SpeakingRate = 1.0
                }
            },
            cancellationToken: ct);
        timer.Stop();

        _logger.LogInformation("Warmup do cliente Google TTS concluido em {ElapsedMs} ms.", timer.ElapsedMilliseconds);
    }

    public async Task<(byte[] AudioBytes, string ContentType, string FileExtension)> SynthesizeDownloadAsync(
        string text,
        string format,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Texto para síntese não pode ser vazio.", nameof(text));

        if (IsAsteriskProvider())
            throw new InvalidOperationException(
                "Download de áudio requer TtsProvider=Google. Com Asterisk o orquestrador retorna apenas marcador para reprodução AGI.");

        EnsureCredentials();

        var normalizedFormat = (format ?? string.Empty).Trim().ToLowerInvariant();
        var encoding = normalizedFormat switch
        {
            "wav" => AudioEncoding.Linear16,
            "mp3" => AudioEncoding.Mp3,
            _ => throw new ArgumentOutOfRangeException(nameof(format), "Formato inválido. Use 'wav' ou 'mp3'.")
        };

        var audioBytes = await SynthesizeGoogleAudioAsync(text, encoding, ct);
        var contentType = encoding == AudioEncoding.Mp3 ? "audio/mpeg" : "audio/wav";
        var extension = encoding == AudioEncoding.Mp3 ? "mp3" : "wav";
        return (audioBytes, contentType, extension);
    }

    private async Task<byte[]> SynthesizeGoogleAudioAsync(string text, AudioEncoding audioEncoding, CancellationToken ct)
    {
        var client = await GetGoogleClientAsync(ct);
        var request = new SynthesizeSpeechRequest
        {
            Input = new SynthesisInput { Text = text },
            Voice = new VoiceSelectionParams
            {
                LanguageCode = _options.Google.TtsLanguageCode,
                Name = _options.Google.TtsVoiceName ?? string.Empty
            },
            AudioConfig = new AudioConfig
            {
                AudioEncoding = audioEncoding,
                SampleRateHertz = audioEncoding == AudioEncoding.Linear16 ? 8000 : _options.Google.TtsSampleRateHertz,
                SpeakingRate = _options.Google.TtsSpeakingRate
            }
        };

        var response = await client.SynthesizeSpeechAsync(request, cancellationToken: ct);
        return response.AudioContent.ToByteArray();
    }

    private async Task<TextToSpeechClient> GetGoogleClientAsync(CancellationToken ct)
    {
        if (_googleClient is not null)
            return _googleClient;

        await _googleClientLock.WaitAsync(ct);
        try
        {
            if (_googleClient is not null)
                return _googleClient;

            _googleClient = await TextToSpeechClient.CreateAsync();
            _logger.LogInformation("Cliente Google TTS inicializado e mantido em cache para reutilizacao.");
            return _googleClient;
        }
        finally
        {
            _googleClientLock.Release();
        }
    }

    /// <summary>
    /// Extrai o payload PCM bruto de um buffer WAV (RIFF). Se o buffer não for WAV
    /// ou o chunk "data" não for encontrado, retorna os bytes originais sem modificação.
    /// </summary>
    private static byte[] ExtractRawPcmFromWav(byte[] wavBytes)
    {
        // Cabeçalho RIFF mínimo: "RIFF"(4) + tamanho(4) + "WAVE"(4) + chunk mínimo(8) = 20 bytes
        if (wavBytes.Length < 20)
            return wavBytes;

        // Valida assinatura RIFF + WAVE
        if (wavBytes[0] != 'R' || wavBytes[1] != 'I' || wavBytes[2] != 'F' || wavBytes[3] != 'F')
            return wavBytes;
        if (wavBytes[8] != 'W' || wavBytes[9] != 'A' || wavBytes[10] != 'V' || wavBytes[11] != 'E')
            return wavBytes;

        // Percorre os chunks até encontrar "data"
        var offset = 12;
        while (offset + 8 <= wavBytes.Length)
        {
            var chunkId = System.Text.Encoding.ASCII.GetString(wavBytes, offset, 4);
            var chunkSize = BitConverter.ToInt32(wavBytes, offset + 4);

            if (chunkId == "data")
            {
                var dataStart = offset + 8;
                var dataLength = Math.Min(chunkSize, wavBytes.Length - dataStart);
                if (dataLength <= 0)
                    return wavBytes;

                var pcm = new byte[dataLength];
                Buffer.BlockCopy(wavBytes, dataStart, pcm, 0, dataLength);
                return pcm;
            }

            // Avança para o próximo chunk (WAV requer alinhamento em word de 2 bytes)
            offset += 8 + chunkSize;
            if (chunkSize % 2 != 0)
                offset++;
        }

        // Chunk "data" não encontrado — retorna bytes originais
        return wavBytes;
    }

    private void EnsureCredentials()
    {
        if (IsAsteriskProvider())
            return;

        if (string.IsNullOrWhiteSpace(_options.Google.CredentialsPath))
            throw new InvalidOperationException(
                "TtsProvider está configurado como Google mas 'Speech:Google:CredentialsPath' está vazio. " +
                "Defina o caminho para o arquivo JSON de credenciais via appsettings.json ou variável AIPORT_GOOGLE_CREDENTIALS_PATH.");

        if (!File.Exists(_options.Google.CredentialsPath))
            throw new FileNotFoundException(
                $"Arquivo de credenciais Google não encontrado em '{_options.Google.CredentialsPath}'. " +
                "Verifique se o arquivo existe e se o serviço tem permissão de leitura.",
                _options.Google.CredentialsPath);

        Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", _options.Google.CredentialsPath);
    }

    private bool IsAsteriskProvider() =>
        _options.TtsProvider == TtsProviderType.Asterisk;

    private bool IsDeveloperSandboxMode() =>
        _runtimeOptions.InputSourceMode is InputSourceMode.WindowsText or InputSourceMode.WindowsVoice;

    private string ResolveTempDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_options.Google.TempDirectory))
            return _options.Google.TempDirectory;

        return Path.GetTempPath();
    }
}
