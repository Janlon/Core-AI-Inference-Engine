using AIPort.Adapter.Orchestrator.Config;
using AIPort.Adapter.Orchestrator.Integrations;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace AIPort.Adapter.Orchestrator.Services;

public sealed class SpeechWarmupHostedService : IHostedService
{
    private static readonly TimeSpan WarmupTimeout = TimeSpan.FromSeconds(8);
    private readonly SpeechOptions _speechOptions;
    private readonly TextToSpeechService _textToSpeechService;
    private readonly SpeechWarmupStatusTracker _statusTracker;
    private readonly ILogger<SpeechWarmupHostedService> _logger;

    public SpeechWarmupHostedService(
        IOptions<SpeechOptions> speechOptions,
        TextToSpeechService textToSpeechService,
        SpeechWarmupStatusTracker statusTracker,
        ILogger<SpeechWarmupHostedService> logger)
    {
        _speechOptions = speechOptions.Value;
        _textToSpeechService = textToSpeechService;
        _statusTracker = statusTracker;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var provider = _speechOptions.TtsProvider.ToString().ToLowerInvariant();

        if (_speechOptions.TtsProvider != TtsProviderType.Google)
        {
            _statusTracker.MarkReady(provider, DateTime.UtcNow, 0, "Provider sem warmup remoto; pronto para uso.");
            _logger.LogInformation("Warmup de TTS ignorado porque o provider configurado e {Provider}.", _speechOptions.TtsProvider);
            return;
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(WarmupTimeout);
        _statusTracker.MarkStarting(provider, "Executando warmup inicial do TTS Google.");
        var timer = Stopwatch.StartNew();

        try
        {
            await _textToSpeechService.WarmupAsync(linkedCts.Token);
            timer.Stop();
            _statusTracker.MarkReady(provider, DateTime.UtcNow, timer.ElapsedMilliseconds, "Warmup do TTS concluido e cliente pronto.");
            _logger.LogInformation("Warmup de TTS concluido antes de aceitar chamadas FastAGI.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            timer.Stop();
            _statusTracker.MarkDegraded(provider, DateTime.UtcNow, timer.ElapsedMilliseconds, "Warmup expirou; primeira chamada ainda pode sofrer cold start.");
            _logger.LogWarning("Warmup de TTS expirou apos {TimeoutSeconds}s. O servico seguira iniciando e a primeira chamada pode sofrer cold start.", WarmupTimeout.TotalSeconds);
        }
        catch (Exception ex)
        {
            timer.Stop();
            _statusTracker.MarkDegraded(provider, DateTime.UtcNow, timer.ElapsedMilliseconds, $"Warmup falhou: {ex.Message}");
            _logger.LogWarning(ex, "Warmup de TTS falhou. O servico seguira iniciando com risco de cold start na primeira chamada.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}