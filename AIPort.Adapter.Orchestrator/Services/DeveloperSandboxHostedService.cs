using System.Text.Json;
using System.Text.Encodings.Web;
using AIPort.Adapter.Orchestrator.Config;
using AIPort.Adapter.Orchestrator.Domain.Models;
using AIPort.Adapter.Orchestrator.Services.Interfaces;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Runtime.Versioning;
using System.Speech.Recognition;
using AIPort.Adapter.Orchestrator.Agi;
using AIPort.Adapter.Orchestrator.Domain.Abstractions;

namespace AIPort.Adapter.Orchestrator.Services;

public sealed class DeveloperSandboxHostedService : BackgroundService
{
    private static readonly JsonSerializerOptions SandboxJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RuntimeInputOptions _runtimeOptions;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly ILogger<DeveloperSandboxHostedService> _logger;
    private int _tenantPid;
    private InputSourceMode _activeInputMode;
    private bool VerboseConsoleOutput => _runtimeOptions.DeveloperSandbox.VerboseConsoleOutput;

    public DeveloperSandboxHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<RuntimeInputOptions> runtimeOptions,
        IHostApplicationLifetime hostApplicationLifetime,
        ILogger<DeveloperSandboxHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _runtimeOptions = runtimeOptions.Value;
        _hostApplicationLifetime = hostApplicationLifetime;
        _logger = logger;
        _tenantPid = _runtimeOptions.DeveloperSandbox.TenantPid;
        _activeInputMode = _runtimeOptions.InputSourceMode;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_runtimeOptions.InputSourceMode == InputSourceMode.Asterisk)
            return;

        if (!OperatingSystem.IsWindows())
        {
            _logger.LogError("Developer Sandbox Mode só é suportado no Windows.");
            return;
        }

        await Task.Yield();
        EnsureTenantPid();
        PrintBanner();

        while (!stoppingToken.IsCancellationRequested)
        {
            var shouldStartCall = await WaitForNextCallAsync(stoppingToken);
            if (!shouldStartCall)
                break;

            await RunSandboxCallAsync(stoppingToken);
        }
    }

    private void EnsureTenantPid()
    {
        while (_tenantPid <= 0)
        {
            Console.Write("sandbox tenantPid> ");
            var raw = Console.ReadLine();
            if (int.TryParse(raw, out var parsed) && parsed > 0)
            {
                _tenantPid = parsed;
                break;
            }

            Console.WriteLine("Informe um tenant PID válido para iniciar o sandbox.");
        }
    }

    private void PrintBanner()
    {
        Console.WriteLine();
        Console.WriteLine("=== Developer Sandbox Mode ===");
        Console.WriteLine($"Source: {_activeInputMode}");
        Console.WriteLine($"Tenant PID: {_tenantPid}");
        Console.WriteLine("Commands: /tenant <pid>, /exit");
        Console.WriteLine();

        _logger.LogInformation(
            "Developer Sandbox Mode iniciado. Source={Source} TenantPid={TenantPid}",
            _activeInputMode,
            _tenantPid);
    }

    private async Task<bool> WaitForNextCallAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            Console.Write("sandbox idle> pressione Enter para iniciar a chamada, /tenant <pid> ou /exit\n> ");
            var line = await Task.Run(Console.ReadLine, stoppingToken);

            if (line is null)
                return false;

            var trimmed = line.Trim();
            if (trimmed.Length == 0 || string.Equals(trimmed, "/call", StringComparison.OrdinalIgnoreCase))
                return true;

            var commandResult = HandleConsoleCommand(trimmed);
            if (commandResult is null)
                return false;

            if (commandResult.Length == 0)
                continue;

            Console.WriteLine("Comando não reconhecido. Use Enter para iniciar uma chamada ou /tenant <pid> para trocar o tenant.");
        }

        return false;
    }

    private string? HandleConsoleCommand(string? line)
    {
        if (line is null)
            return null;

        var trimmed = line.Trim();
        if (trimmed.Length == 0)
            return string.Empty;

        if (string.Equals(trimmed, "/exit", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "exit", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "quit", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Encerrando Developer Sandbox Mode.");
            _hostApplicationLifetime.StopApplication();
            return null;
        }

        if (trimmed.StartsWith("/tenant ", StringComparison.OrdinalIgnoreCase))
        {
            var rawPid = trimmed[8..].Trim();
            if (int.TryParse(rawPid, out var parsedPid) && parsedPid > 0)
            {
                _tenantPid = parsedPid;
                Console.WriteLine($"Tenant PID alterado para {_tenantPid}.");
            }
            else
            {
                Console.WriteLine("PID inválido.");
            }

            return string.Empty;
        }

        return trimmed;
    }

    [SupportedOSPlatform("windows")]
    private async Task<string?> RecognizeFromDefaultMicrophoneAsync(CancellationToken stoppingToken)
    {
        var voiceOptions = _runtimeOptions.DeveloperSandbox.WindowsVoice;
        if (string.Equals(voiceOptions.Provider, "SystemSpeech", StringComparison.OrdinalIgnoreCase)
            || string.Equals(voiceOptions.Provider, "WindowsOffline", StringComparison.OrdinalIgnoreCase))
        {
            return await RecognizeWithSystemSpeechAsync(voiceOptions, stoppingToken);
        }

        if (string.Equals(voiceOptions.Provider, "AzureSpeech", StringComparison.OrdinalIgnoreCase))
        {
            return await RecognizeWithAzureSpeechAsync(voiceOptions, stoppingToken);
        }

        throw new InvalidOperationException($"Provider de voz '{voiceOptions.Provider}' não é suportado.");
    }

    [SupportedOSPlatform("windows")]
    private async Task<string?> RecognizeWithAzureSpeechAsync(WindowsVoiceInputOptions voiceOptions, CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(voiceOptions.SubscriptionKey) || string.IsNullOrWhiteSpace(voiceOptions.Region))
        {
            throw new InvalidOperationException(
                "WindowsVoice com AzureSpeech requer DeveloperSandbox:WindowsVoice:SubscriptionKey e Region configurados.");
        }

        Console.WriteLine("sandbox voice> fale agora...");

        var speechConfig = SpeechConfig.FromSubscription(voiceOptions.SubscriptionKey, voiceOptions.Region);
        speechConfig.SpeechRecognitionLanguage = string.IsNullOrWhiteSpace(voiceOptions.Language)
            ? "pt-BR"
            : voiceOptions.Language;

        using var audioConfig = AudioConfig.FromDefaultMicrophoneInput();
        using var recognizer = new Microsoft.CognitiveServices.Speech.SpeechRecognizer(speechConfig, audioConfig);
        var result = await recognizer.RecognizeOnceAsync().WaitAsync(stoppingToken);

        return result.Reason switch
        {
            ResultReason.RecognizedSpeech => result.Text?.Trim(),
            ResultReason.NoMatch => string.Empty,
            ResultReason.Canceled => throw new InvalidOperationException(
                $"Reconhecimento de voz cancelado: {CancellationDetails.FromResult(result).Reason} | {CancellationDetails.FromResult(result).ErrorDetails}"),
            _ => string.Empty
        };
    }

    [SupportedOSPlatform("windows")]
    private static async Task<string?> RecognizeWithSystemSpeechAsync(WindowsVoiceInputOptions voiceOptions, CancellationToken stoppingToken)
    {
        Console.WriteLine("sandbox voice> fale agora... (offline/System.Speech)");

        var culture = CreateRecognitionCulture(voiceOptions.Language);
        using var recognizer = CreateSystemSpeechRecognizer(culture);
        recognizer.SetInputToDefaultAudioDevice();
        recognizer.LoadGrammar(new DictationGrammar());

        var recognitionTask = WaitForSystemSpeechRecognitionAsync(recognizer, stoppingToken);
        var text = await recognitionTask;
        return text?.Trim() ?? string.Empty;
    }

    [SupportedOSPlatform("windows")]
    private static SpeechRecognitionEngine CreateSystemSpeechRecognizer(CultureInfo culture)
    {
        try
        {
            return new SpeechRecognitionEngine(culture);
        }
        catch (InvalidOperationException)
        {
            return new SpeechRecognitionEngine();
        }
    }

    private static CultureInfo CreateRecognitionCulture(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return new CultureInfo("pt-BR");

        try
        {
            return new CultureInfo(language);
        }
        catch (CultureNotFoundException)
        {
            return new CultureInfo("pt-BR");
        }
    }

    [SupportedOSPlatform("windows")]
    private static async Task<string?> WaitForSystemSpeechRecognitionAsync(SpeechRecognitionEngine recognizer, CancellationToken stoppingToken)
    {
        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        EventHandler<SpeechRecognizedEventArgs>? recognized = null;
        EventHandler<RecognizeCompletedEventArgs>? completed = null;

        recognized = (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Result?.Text))
                completion.TrySetResult(args.Result.Text);
        };

        completed = (_, args) =>
        {
            if (args.Error is not null)
                completion.TrySetException(args.Error);
            else if (args.Cancelled)
                completion.TrySetCanceled();
            else
                completion.TrySetResult(args.Result?.Text ?? string.Empty);
        };

        recognizer.SpeechRecognized += recognized;
        recognizer.RecognizeCompleted += completed;

        using var registration = stoppingToken.Register(() =>
        {
            try
            {
                recognizer.RecognizeAsyncCancel();
            }
            catch
            {
            }
        });

        try
        {
            recognizer.RecognizeAsync(RecognizeMode.Single);
            return await completion.Task;
        }
        finally
        {
            recognizer.SpeechRecognized -= recognized;
            recognizer.RecognizeCompleted -= completed;
        }
    }

    private async Task<string?> CaptureVisitorInputAsync(CancellationToken stoppingToken)
    {
        if (_activeInputMode == InputSourceMode.WindowsVoice)
        {
            try
            {
                return await RecognizeFromDefaultMicrophoneAsync(stoppingToken);
            }
            catch (Exception ex) when (ShouldFallbackToTextMode(ex))
            {
                _activeInputMode = InputSourceMode.WindowsText;
                _logger.LogWarning(ex,
                    "WindowsVoice indisponível no sandbox. Alternando automaticamente para WindowsText.");

                Console.WriteLine();
                Console.WriteLine("[Sandbox] WindowsVoice indisponível nesta máquina. Alternando para WindowsText.");
                Console.WriteLine("[Sandbox] As próximas respostas do visitante serão digitadas no console.");
                Console.WriteLine();
            }
        }

        Console.Write("visitante> ");
        var line = await Task.Run(Console.ReadLine, stoppingToken);
        if (line is null)
            throw new AgiHangupException("Entrada do visitante encerrada no sandbox.");

        var trimmed = line.Trim();
        if (string.Equals(trimmed, "/hangup", StringComparison.OrdinalIgnoreCase))
            throw new AgiHangupException("Chamada encerrada manualmente pelo sandbox.");

        if (string.Equals(trimmed, "/exit", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "exit", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "quit", StringComparison.OrdinalIgnoreCase))
        {
            _hostApplicationLifetime.StopApplication();
            throw new AgiHangupException("Sandbox finalizado pelo operador.");
        }

        WriteVerboseLine($"[Captured] {trimmed}");
        return trimmed;
    }

    private async Task RunSandboxCallAsync(CancellationToken stoppingToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var orchestrationService = scope.ServiceProvider.GetRequiredService<IOrchestrationService>();

        var sessionId = BuildSessionId();
        var context = new AgiCallContext
        {
            SessionId = sessionId,
            UniqueId = sessionId,
            CallerId = _runtimeOptions.DeveloperSandbox.CallerId,
            CalledNumber = _runtimeOptions.DeveloperSandbox.CalledNumber,
            Context = _runtimeOptions.DeveloperSandbox.Context,
            Channel = $"sandbox/{_activeInputMode.ToString().ToLowerInvariant()}",
            TenantPid = _tenantPid,
            InputSource = _activeInputMode.ToString(),
            StartedAtUtc = DateTimeOffset.UtcNow
        };

        using var voiceChannel = new DeveloperSandboxVoiceChannel(context, CaptureVisitorInputAsync, VerboseConsoleOutput);
        context.VoiceChannel = voiceChannel;

        WriteVerboseLine();
        WriteVerboseLine($"[Call] iniciada Session={sessionId} TenantPid={_tenantPid}");

        var result = await orchestrationService.HandleCallAsync(context, stoppingToken);

        if (result.Debug?.RegexMatches is { Count: > 0 } regexMatches)
        {
            var renderedMatches = string.Join(
                " | ",
                regexMatches.Select(match => string.IsNullOrWhiteSpace(match.Value)
                    ? match.Rule
                    : $"{match.Rule}={match.Value}"));
            WriteVerboseLine($"[Regex] {renderedMatches}");
        }
        else
        {
            WriteVerboseLine("[Regex] nenhum match capturado");
        }

        WriteVerboseLine(
            $"[Decision] sucesso={result.Sucesso} camada={result.CamadaResolucao ?? "--"} intencao={result.Intencao ?? "--"} acao={result.AcaoExecutada} confianca={(result.Confianca.HasValue ? result.Confianca.Value.ToString("P0") : "--")}");

        if (result.DadosExtraidos is not null)
            WriteVerboseLine($"[Entities] {JsonSerializer.Serialize(result.DadosExtraidos, SandboxJsonOptions)}");

        WriteVerboseLine($"[Response] {result.RespostaFalada}");

        if (!result.Sucesso && !string.IsNullOrWhiteSpace(result.MotivoFalha))
            WriteVerboseLine($"[Failure] {result.MotivoFalha}");

        WriteVerboseLine($"[Call] encerrada Session={sessionId} Acao={result.AcaoExecutada}");

        WriteVerboseLine();
    }

    private void WriteVerboseLine(string? message = null)
    {
        if (!VerboseConsoleOutput)
            return;

        if (message is null)
            Console.WriteLine();
        else
            Console.WriteLine(message);
    }

    private string BuildSessionId()
    {
        var prefix = string.IsNullOrWhiteSpace(_runtimeOptions.DeveloperSandbox.SessionPrefix)
            ? "sandbox"
            : _runtimeOptions.DeveloperSandbox.SessionPrefix.Trim();

        var guidPart = Guid.NewGuid().ToString("N")[..8];

        return $"{prefix}-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{guidPart}";
    }

    private bool ShouldFallbackToTextMode(Exception ex)
    {
        if (_runtimeOptions.DeveloperSandbox.WindowsVoice.Provider.Equals("AzureSpeech", StringComparison.OrdinalIgnoreCase))
            return false;

        return ex is ArgumentException
            || ex is PlatformNotSupportedException
            || ex is InvalidOperationException;
    }

    private sealed class DeveloperSandboxVoiceChannel : IVoiceChannel, IDisposable
    {
        private readonly AgiCallContext _context;
        private readonly Func<CancellationToken, Task<string?>> _captureInputAsync;
        private readonly bool _verboseConsoleOutput;

        public DeveloperSandboxVoiceChannel(
            AgiCallContext context,
            Func<CancellationToken, Task<string?>> captureInputAsync,
            bool verboseConsoleOutput)
        {
            _context = context;
            _captureInputAsync = captureInputAsync;
            _verboseConsoleOutput = verboseConsoleOutput;
        }

        public Task<char?> ReadDigitAsync(int timeoutMs, CancellationToken ct = default)
        {
            return Task.FromResult<char?>(null);
        }

        public Task<VoiceChannelResponse> PlayAsync(string filePath, CancellationToken ct = default)
        {
            var rendered = TryRenderPrompt(filePath, out var prompt)
                ? prompt
                : $"[audio] {filePath}";

            if (_verboseConsoleOutput)
                Console.WriteLine($"[Porteiro] {rendered}");
            return Task.FromResult(new VoiceChannelResponse(200, 0, null, rendered));
        }

        public async Task<VoiceChannelResponse> RecordAsync(string savePath, int maxTimeMs, CancellationToken ct = default)
        {
            var captured = await _captureInputAsync(ct) ?? string.Empty;
            _context.PreTranscribedText = captured;
            return new VoiceChannelResponse(200, 0, null, captured);
        }

        public void Dispose()
        {
        }

        private static bool TryRenderPrompt(string filePath, out string prompt)
        {
            const string sandboxPrefix = "sandbox-tts://";
            const string asteriskPrefix = "asterisk-tts://";

            if (filePath.StartsWith(sandboxPrefix, StringComparison.OrdinalIgnoreCase))
            {
                prompt = Uri.UnescapeDataString(filePath[sandboxPrefix.Length..]);
                return true;
            }

            if (filePath.StartsWith(asteriskPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var payload = filePath[asteriskPrefix.Length..];
                var slashIndex = payload.IndexOf('/');
                if (slashIndex >= 0 && slashIndex < payload.Length - 1)
                {
                    prompt = Uri.UnescapeDataString(payload[(slashIndex + 1)..]);
                    return true;
                }
            }

            prompt = string.Empty;
            return false;
        }
    }
}