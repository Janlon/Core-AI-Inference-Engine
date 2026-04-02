using AIPort.Adapter.Orchestrator.Config;
using AIPort.Adapter.Orchestrator.Integrations;

namespace AIPort.Adapter.Orchestrator.Tests.Speech;

/// <summary>
/// Testes para TextToSpeechService.
/// - Testes Asterisk: executam sem credenciais (apenas verificam o marcador retornado).
/// - Testes Google: requerem credenciais reais e conectividade com Google Cloud.
///   Configure AIPORT_GOOGLE_CREDENTIALS_PATH antes de rodar.
/// </summary>
public class TextToSpeechServiceTests
{
    private static IOptions<SpeechOptions> BuildOptions(Action<SpeechOptions> configure)
    {
        var opts = new SpeechOptions();
        configure(opts);
        return Options.Create(opts);
    }

    private static TextToSpeechService BuildService(IOptions<SpeechOptions> opts) =>
        new(opts, Mock.Of<ILogger<TextToSpeechService>>());

    // ──────────────────────────────────────────────────────────────────────────
    // TTS ASTERISK (sem credenciais, sempre executa)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Asterisk_SynthesizeAsync_ReturnsFliteMarker()
    {
        var opts = BuildOptions(o =>
        {
            o.TtsProvider = TtsProviderType.Asterisk;
            o.Asterisk.TtsApplication = "FLITE";
        });

        var svc = BuildService(opts);
        var result = await svc.SynthesizeAsync("Olá, como posso ajudar?");

        Assert.StartsWith("asterisk-tts://FLITE/", result);
        Assert.Contains(Uri.EscapeDataString("Olá, como posso ajudar?"), result);
    }

    [Fact]
    public async Task Asterisk_SynthesizeAsync_UsaAppPersonalizado()
    {
        var opts = BuildOptions(o =>
        {
            o.TtsProvider = TtsProviderType.Asterisk;
            o.Asterisk.TtsApplication = "ESPEAK";
        });

        var svc = BuildService(opts);
        var result = await svc.SynthesizeAsync("Teste");

        Assert.StartsWith("asterisk-tts://ESPEAK/", result);
    }

    [Fact]
    public async Task Asterisk_SynthesizeAsync_AppVazio_UsaFliteComoDefault()
    {
        var opts = BuildOptions(o =>
        {
            o.TtsProvider = TtsProviderType.Asterisk;
            o.Asterisk.TtsApplication = string.Empty;
        });

        var svc = BuildService(opts);
        var result = await svc.SynthesizeAsync("Teste default");

        Assert.StartsWith("asterisk-tts://FLITE/", result);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // TTS GOOGLE — validação de configuração (sem rede)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Google_SemCredentialsPath_LancaInvalidOperationException()
    {
        var opts = BuildOptions(o =>
        {
            o.TtsProvider = TtsProviderType.Google;
            o.Google.CredentialsPath = null;
        });

        var svc = BuildService(opts);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.SynthesizeAsync("Texto qualquer"));
    }

    [Fact]
    public async Task Google_CredentialsPathInexistente_LancaFileNotFoundException()
    {
        var opts = BuildOptions(o =>
        {
            o.TtsProvider = TtsProviderType.Google;
            o.Google.CredentialsPath = "/tmp/nao-existe-xpto.json";
        });

        var svc = BuildService(opts);

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => svc.SynthesizeAsync("Texto qualquer"));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // TTS GOOGLE — integração real (requer credenciais + rede)
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Teste de integração real com Google Cloud TTS.
    /// Requer variável de ambiente AIPORT_GOOGLE_CREDENTIALS_PATH apontando
    /// para um JSON de service account válido.
    /// Pular com `dotnet test --filter "Category!=Integration"`.
    /// </summary>
    [Fact]
    public async Task Google_SynthesizeAsync_GeraArquivoWav()
    {
        var credPath = Environment.GetEnvironmentVariable("AIPORT_GOOGLE_CREDENTIALS_PATH")
            ?? throw new InvalidOperationException("Defina AIPORT_GOOGLE_CREDENTIALS_PATH para este teste.");

        var tempDir = Path.Combine(Path.GetTempPath(), "aiport-tts-test-" + Guid.NewGuid().ToString("N")[..8]);

        var opts = BuildOptions(o =>
        {
            o.TtsProvider = TtsProviderType.Google;
            o.Google.CredentialsPath = credPath;
            o.Google.TtsVoiceName = "pt-BR-Wavenet-A";
            o.Google.TtsLanguageCode = "pt-BR";
            o.Google.TtsSampleRateHertz = 8000;
            o.Google.TtsSpeakingRate = 1.0;
            o.Google.TempDirectory = tempDir;
        });

        var svc = BuildService(opts);

        try
        {
            var result = await svc.SynthesizeAsync("Olá, este é um teste de síntese de voz.");

            Assert.False(string.IsNullOrWhiteSpace(result));
            var wavPath = result + ".wav";
            Assert.True(File.Exists(wavPath), $"Arquivo WAV não encontrado em: {wavPath}");

            var info = new FileInfo(wavPath);
            Assert.True(info.Length > 0, "Arquivo WAV gerado está vazio.");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
