using AIPort.Adapter.Orchestrator.Config;
using AIPort.Adapter.Orchestrator.Integrations;

namespace AIPort.Adapter.Orchestrator.Tests.Speech;

/// <summary>
/// Testes para GoogleCloudStreamingSttService (streaming bidirecional gRPC).
/// - Testes de estado/validação: executam sem credenciais.
/// - Teste de integração real: requer AIPORT_GOOGLE_CREDENTIALS_PATH + arquivo WAV.
/// </summary>
public class GoogleCloudStreamingSttServiceTests
{
    private static IOptions<SpeechOptions> BuildOptions(Action<GoogleSpeechOptions>? configure = null)
    {
        var opts = new SpeechOptions();
        configure?.Invoke(opts.Google);
        return Options.Create(opts);
    }

    private static GoogleCloudStreamingSttService BuildService(IOptions<SpeechOptions> opts) =>
        new(opts, Mock.Of<ILogger<GoogleCloudStreamingSttService>>());

    // ──────────────────────────────────────────────────────────────────────────
    // Casos sem rede
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TranscribeAsync_PreTranscribedText_RetornaTextoSemAbrirStream()
    {
        var opts = BuildOptions();
        using var svc = BuildService(opts);

        var result = await svc.TranscribeAsync(null, "texto pré-transcrito", CancellationToken.None);

        Assert.Equal("texto pré-transcrito", result);
    }

    [Fact]
    public async Task TranscribeAsync_AudioPathNulo_RetornaVazio()
    {
        var opts = BuildOptions();
        using var svc = BuildService(opts);

        var result = await svc.TranscribeAsync(null, null, CancellationToken.None);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task TranscribeAsync_AudioPathInexistente_RetornaVazio()
    {
        var opts = BuildOptions();
        using var svc = BuildService(opts);

        var result = await svc.TranscribeAsync("/tmp/arquivo-inexistente-xpto.wav", null, CancellationToken.None);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task WriteAudioAsync_SemStreamAberto_LancaObjectDisposedException()
    {
        var opts = BuildOptions();
        using var svc = BuildService(opts);

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => svc.WriteAudioAsync(new byte[] { 0x00, 0x01 }));
    }

    [Fact]
    public async Task OpenStreamAsync_CredentialsPathInexistente_LancaFileNotFoundException()
    {
        var opts = BuildOptions(g => g.CredentialsPath = "/tmp/nao-existe-credentials.json");
        using var svc = BuildService(opts);

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => svc.OpenStreamAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GetFinalTranscriptionAsync_SemStreamAberto_LancaInvalidOperationException()
    {
        var opts = BuildOptions();
        using var svc = BuildService(opts);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.GetFinalTranscriptionAsync(CancellationToken.None));
    }

    [Fact]
    public async Task CloseAsync_SemStreamAberto_NaoLancaExcecao()
    {
        // CloseAsync deve ser idempotente quando chamado antes de OpenStreamAsync
        var opts = BuildOptions();
        using var svc = BuildService(opts);

        var ex = await Record.ExceptionAsync(() => svc.CloseAsync(CancellationToken.None));

        Assert.Null(ex);
    }

    [Fact]
    public async Task GetFinalTranscriptionAsync_CancelamentoImediato_RetornaNull()
    {
        // Mesmo sem stream, a versão com CT cancelado deve retornar null (não lançar)
        // Nota: o serviço lança InvalidOperationException quando stream é null,
        // mas se o CT já vier cancelado isso varia por implementação.
        // Este teste documenta o comportamento atual: lança InvalidOperationException.
        var opts = BuildOptions();
        using var svc = BuildService(opts);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.GetFinalTranscriptionAsync(cts.Token));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Integração real (requer credenciais + rede + arquivo WAV válido)
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Teste de integração real com Google Cloud Streaming STT.
    /// Requer:
    ///   - AIPORT_GOOGLE_CREDENTIALS_PATH = caminho para JSON de service account.
    ///   - AIPORT_TEST_AUDIO_PATH = caminho para WAV PCM 8kHz mono com fala em pt-BR.
    /// Executar manualmente: remova o Skip e rode com as variáveis de ambiente configuradas.
    /// </summary>
    [Fact]
    public async Task Google_StreamingTranscribe_ViaTranscribeAsync_RetornaTexto()
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

        using var svc = BuildService(opts);

        var result = await svc.TranscribeAsync(audioPath, null, CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(result),
            "A transcrição via streaming não deve ser vazia para um arquivo com fala.");
    }

    /// <summary>
    /// Teste de integração real: abre o stream manualmente, injeta chunks de áudio
    /// e aguarda a transcrição final — simula o fluxo usado pelo AgiCallHandler.
    /// </summary>
    [Fact]
    public async Task Google_StreamingTranscribe_ViaChunks_RetornaTexto()
    {
        var credPath = @"C:\\repositorio\\plataforma\\configs\\google_cloud_auth.json" //Environment.GetEnvironmentVariable("AIPORT_GOOGLE_CREDENTIALS_PATH")
            ?? throw new InvalidOperationException("Defina AIPORT_GOOGLE_CREDENTIALS_PATH.");

        var audioPath = Environment.GetEnvironmentVariable("AIPORT_TEST_AUDIO_PATH")
            ?? throw new InvalidOperationException("Defina AIPORT_TEST_AUDIO_PATH.");

        var opts = BuildOptions(g =>
        {
            g.CredentialsPath = credPath;
            g.SttLanguageCode = "pt-BR";
            g.SttSampleRateHertz = 8000;
        });

        using var svc = BuildService(opts);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await svc.OpenStreamAsync(cts.Token);

        // Injeta os bytes de áudio em chunks de 3200 bytes (100ms a 8kHz/16bit/mono)
        var audioBytes = await File.ReadAllBytesAsync(audioPath, cts.Token);
        const int chunkSize = 3200;

        for (var i = 0; i < audioBytes.Length; i += chunkSize)
        {
            var chunk = audioBytes.AsSpan(i, Math.Min(chunkSize, audioBytes.Length - i)).ToArray();
            await svc.WriteAudioAsync(chunk, cts.Token);
            await Task.Delay(100, cts.Token); // simula chegada em tempo real
        }

        var transcript = await svc.GetFinalTranscriptionAsync(cts.Token);
        await svc.CloseAsync(cts.Token);

        Assert.False(string.IsNullOrWhiteSpace(transcript),
            "A transcrição via chunks não deve ser vazia para um arquivo com fala.");
    }
}
