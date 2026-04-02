using AIPort.Adapter.Orchestrator.Config;
using AIPort.Adapter.Orchestrator.Integrations;

namespace AIPort.Adapter.Orchestrator.Tests.Speech;

/// <summary>
/// Testes para SpeechToTextService (STT batch/síncrono via Google Cloud Speech).
/// - Testes de validação: executam sem credenciais.
/// - Teste de integração real: requer AIPORT_GOOGLE_CREDENTIALS_PATH + arquivo WAV.
/// </summary>
public class SpeechToTextServiceTests
{
    private static IOptions<SpeechOptions> BuildOptions(Action<GoogleSpeechOptions> configure)
    {
        var opts = new SpeechOptions();
        configure(opts.Google);
        return Options.Create(opts);
    }

    private static SpeechToTextService BuildService(IOptions<SpeechOptions> opts) =>
        new(opts, Mock.Of<ILogger<SpeechToTextService>>());

    // ──────────────────────────────────────────────────────────────────────────
    // Casos sem rede
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TranscribeAsync_PreTranscribedText_RetornaTextoSemChamarGoogle()
    {
        // Se preTranscribedText já vem preenchido, o serviço deve retorná-lo direto
        // sem tentar abrir conexão com Google.
        var opts = BuildOptions(_ => { });
        var svc = BuildService(opts);

        var result = await svc.TranscribeAsync(null, "texto já transcrito");

        Assert.Equal("texto já transcrito", result);
    }

    [Fact]
    public async Task TranscribeAsync_AudioPathNulo_RetornaVazio()
    {
        var opts = BuildOptions(_ => { });
        var svc = BuildService(opts);

        var result = await svc.TranscribeAsync(null, null);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task TranscribeAsync_AudioPathInexistente_RetornaVazio()
    {
        // Arquivo não existe → retorna string vazia (sem lançar exceção)
        var opts = BuildOptions(g =>
        {
            g.CredentialsPath = null; // sem credenciais; não chegará ao Google
        });
        var svc = BuildService(opts);

        var result = await svc.TranscribeAsync("/tmp/arquivo-nao-existe-xpto.wav", null);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task TranscribeAsync_CredentialsPathInexistente_LancaFileNotFoundException()
    {
        // Arquivo de credenciais existe no config mas não no disco
        var opts = BuildOptions(g =>
        {
            g.CredentialsPath = "/tmp/nao-existe-credentials.json";
            g.SttLanguageCode = "pt-BR";
            g.SttSampleRateHertz = 8000;
        });

        // Cria um arquivo WAV dummy para passar pela validação do path de áudio
        var dummyAudio = Path.GetTempFileName() + ".wav";
        await File.WriteAllBytesAsync(dummyAudio, GenerateMinimalWav());

        try
        {
            var svc = BuildService(opts);
            await Assert.ThrowsAsync<FileNotFoundException>(
                () => svc.TranscribeAsync(dummyAudio, null));
        }
        finally
        {
            File.Delete(dummyAudio);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Integração real (requer credenciais + rede + arquivo WAV válido)
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Teste de integração real com Google Cloud STT (batch).
    /// Requer:
    ///   - AIPORT_GOOGLE_CREDENTIALS_PATH = caminho para JSON de service account.
    ///   - AIPORT_TEST_AUDIO_PATH = caminho para um arquivo WAV PCM 8kHz mono pt-BR.
    /// </summary>
    [Fact(Skip = "Requer credenciais Google e arquivo WAV real. Remova Skip para executar manualmente.")]
    public async Task Google_TranscribeAsync_RetornaTranscricao()
    {
        var credPath = Environment.GetEnvironmentVariable("AIPORT_GOOGLE_CREDENTIALS_PATH")
            ?? throw new InvalidOperationException("Defina AIPORT_GOOGLE_CREDENTIALS_PATH.");

        var audioPath = Environment.GetEnvironmentVariable("AIPORT_TEST_AUDIO_PATH")
            ?? throw new InvalidOperationException("Defina AIPORT_TEST_AUDIO_PATH com caminho para WAV de teste.");

        var opts = BuildOptions(g =>
        {
            g.CredentialsPath = credPath;
            g.SttLanguageCode = "pt-BR";
            g.SttSampleRateHertz = 8000;
        });

        var svc = BuildService(opts);
        var result = await svc.TranscribeAsync(audioPath, null);

        Assert.False(string.IsNullOrWhiteSpace(result),
            "A transcrição não deve ser vazia para um arquivo de áudio com fala.");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Gera um header WAV PCM mínimo (44 bytes + 0 bytes de dados de áudio).</summary>
    private static byte[] GenerateMinimalWav()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        const int sampleRate = 8000;
        const short channels = 1;
        const short bitsPerSample = 16;
        const int dataLen = 0;

        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataLen);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);      // PCM
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * bitsPerSample / 8); // byte rate
        writer.Write((short)(channels * bitsPerSample / 8));     // block align
        writer.Write(bitsPerSample);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(dataLen);

        return ms.ToArray();
    }
}
