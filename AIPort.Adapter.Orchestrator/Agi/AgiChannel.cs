using AIPort.Adapter.Orchestrator.Agi.Interfaces;
using AIPort.Adapter.Orchestrator.Agi.Models;
using AIPort.Adapter.Orchestrator.Domain.Abstractions;
using AIPort.Adapter.Orchestrator.Domain.Models;

namespace AIPort.Adapter.Orchestrator.Agi;

public sealed class AgiChannel : IAgiChannel, IVoiceChannel
{
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;
    private readonly ILogger<AgiChannel> _logger;
    private readonly SemaphoreSlim _ioLock = new(1, 1);

    public AgiChannel(StreamReader reader, StreamWriter writer, ILogger<AgiChannel> logger)
    {
        _reader = reader;
        _writer = writer;
        _logger = logger;
    }

    public async Task<AgiResponse> AnswerAsync(CancellationToken ct = default)
    {
        var response = await SendCommandAsync("ANSWER", requireResultZero: true, ct);
        _logger.LogInformation("ANSWER concluído com resposta AGI: {Response}", response.RawResponse);
        return response;
    }

    public async Task<AgiResponse> PlayAudioAsync(string filePath, CancellationToken ct = default)
    {
        if (TryParseAsteriskTts(filePath, out var app, out var ttsText))
        {
            var execResponse = await SendCommandAsync(
                $"EXEC {app} \"{EscapeQuotes(ttsText)}\"",
                requireResultZero: false,
                ct);

            _logger.LogInformation("EXEC {App} concluído | Result={Result} | DTMF={Digit}", app, execResponse.Result, execResponse.DigitPressed);
            return execResponse;
        }

        var noExtPath = NormalizeNoExtension(filePath);
        var response = await SendCommandAsync($"STREAM FILE \"{EscapeQuotes(noExtPath)}\" \"\"", requireResultZero: false, ct);
        _logger.LogInformation("STREAM FILE concluído: {Path} | Result={Result} | DTMF={Digit}", noExtPath, response.Result, response.DigitPressed);
        return response;
    }

    public async Task<AgiResponse> RecordAudioAsync(string savePath, int maxTimeMs, CancellationToken ct = default)
    {
        var noExtPath = NormalizeNoExtension(savePath);
        var dir = Path.GetDirectoryName(noExtPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        var response = await SendCommandAsync($"RECORD FILE \"{EscapeQuotes(noExtPath)}\" wav # {maxTimeMs}", requireResultZero: false, ct);
        _logger.LogInformation("RECORD FILE concluído: {Path} | Result={Result} | DTMF={Digit}", noExtPath, response.Result, response.DigitPressed);
        return response;
    }

    public async Task<char?> ReadDigitAsync(int timeoutMs, CancellationToken ct = default)
    {
        var response = await SendCommandAsync($"WAIT FOR DIGIT {timeoutMs}", requireResultZero: false, ct);
        return response.DigitPressed;
    }

    public async Task<VoiceChannelResponse> PlayAsync(string filePath, CancellationToken ct = default)
    {
        var response = await PlayAudioAsync(filePath, ct);
        return ToVoiceResponse(response);
    }

    public async Task<VoiceChannelResponse> RecordAsync(string savePath, int maxTimeMs, CancellationToken ct = default)
    {
        var response = await RecordAudioAsync(savePath, maxTimeMs, ct);
        return ToVoiceResponse(response);
    }

    private async Task<AgiResponse> SendCommandAsync(string command, bool requireResultZero, CancellationToken ct)
    {
        await _ioLock.WaitAsync(ct);
        try
        {
            await _writer.WriteAsync(command + "\n");
            await _writer.FlushAsync();

            var response = await _reader.ReadLineAsync(ct);
            if (response is null)
                throw new AgiHangupException($"Canal AGI encerrado durante comando '{command}'.");

            if (string.IsNullOrWhiteSpace(response))
                throw new AgiHangupException($"Resposta AGI vazia para comando '{command}'.");

            var parsed = AgiResponse.Parse(response);

            if (parsed.StatusCode >= 500)
                throw new AgiHangupException($"Comando AGI '{command}' retornou erro fatal: {parsed.RawResponse}");

            if (parsed.StatusCode != 200)
                throw new IOException($"Comando AGI '{command}' retornou status inesperado: {parsed.RawResponse}");

            if (requireResultZero)
            {
                if (parsed.Result != 0)
                    throw new IOException($"Comando AGI '{command}' esperava result=0 e recebeu '{parsed.RawResponse}'.");
            }

            if (parsed.Result == -1)
            {
                _logger.LogDebug(
                    "Asterisk sinalizou hangup no comando '{Command}'. Encerrando fluxo atual sem erro crítico. Resposta: {Response}",
                    command,
                    parsed.RawResponse);
                throw new AgiHangupException($"Asterisk sinalizou encerramento da chamada no comando '{command}'.");
            }

            return parsed;
        }
        catch (ObjectDisposedException ex)
        {
            throw new AgiHangupException($"Canal AGI foi descartado durante comando '{command}'.", ex);
        }
        catch (IOException ex)
        {
            throw new AgiHangupException($"Falha de I/O no canal AGI durante comando '{command}'.", ex);
        }
        finally
        {
            _ioLock.Release();
        }
    }

    private static string NormalizeNoExtension(string path)
    {
        var extension = Path.GetExtension(path);
        var shouldTrimAudioExtension =
            string.Equals(extension, ".wav", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".gsm", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".ulaw", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".alaw", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".sln", StringComparison.OrdinalIgnoreCase);

        if (!shouldTrimAudioExtension)
            return path;

        var directory = Path.GetDirectoryName(path);
        var name = Path.GetFileNameWithoutExtension(path);
        return string.IsNullOrWhiteSpace(directory) ? name : Path.Combine(directory, name);
    }

    private static string EscapeQuotes(string value) => value.Replace("\"", "\\\"");

    private static bool TryParseAsteriskTts(string value, out string app, out string text)
    {
        const string prefix = "asterisk-tts://";
        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            app = string.Empty;
            text = string.Empty;
            return false;
        }

        var payload = value[prefix.Length..];
        var slash = payload.IndexOf('/');
        if (slash <= 0)
        {
            app = string.Empty;
            text = string.Empty;
            return false;
        }

        app = payload[..slash].Trim();
        text = Uri.UnescapeDataString(payload[(slash + 1)..]);

        return !string.IsNullOrWhiteSpace(app) && !string.IsNullOrWhiteSpace(text);
    }

    private static VoiceChannelResponse ToVoiceResponse(AgiResponse response)
        => new(response.StatusCode, response.Result, response.DigitPressed, response.RawResponse);
}
