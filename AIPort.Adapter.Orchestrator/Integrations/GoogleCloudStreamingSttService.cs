using System.Collections.Concurrent;
using AIPort.Adapter.Orchestrator.Config;
using AIPort.Adapter.Orchestrator.Integrations.Interfaces;
using Google.Cloud.Speech.V1;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Options;

namespace AIPort.Adapter.Orchestrator.Integrations;

/// <summary>
/// Serviço de STT com streaming bidirecional via Google Cloud Speech-to-Text gRPC.
/// Implementa VAD através da flag IsFinal: quando o Google retorna um resultado com IsFinal == true,
/// a transcrição é considerada completa e o stream é encerrado.
/// </summary>
public sealed class GoogleCloudStreamingSttService : ISpeechToTextService, IDisposable
{
    private readonly SpeechOptions _options;
    private readonly ILogger<GoogleCloudStreamingSttService> _logger;

    private SpeechClient? _client;
    private SpeechClient.StreamingRecognizeStream? _stream;
    private CancellationTokenSource? _streamCts;
    private Task? _responseReaderTask;

    private readonly ConcurrentQueue<byte[]> _audioBuffer = new();
    private readonly ManualResetEventSlim _audioAvailable = new(false);
    private string? _finalTranscript;
    private volatile bool _isFinal;
    private volatile bool _requestCompleted;
    private volatile bool _isDisposed;

    public GoogleCloudStreamingSttService(IOptions<SpeechOptions> options, ILogger<GoogleCloudStreamingSttService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Inicia o stream bidirecional com o Google Cloud Speech-to-Text.
    /// Configura o reconhecimento para Linear PCM, idioma pt-BR e inicia a Task de leitura de respostas.
    /// </summary>
    public async Task OpenStreamAsync(CancellationToken externalCt = default)
    {
        if (_stream is not null)
            throw new InvalidOperationException("Stream já está aberto. Chame CloseAsync() primeiro.");

        _isDisposed = false;
        _isFinal = false;
        _requestCompleted = false;
        _finalTranscript = null;
        _streamCts = new CancellationTokenSource();

        EnsureCredentials();

        try
        {
            _client = await SpeechClient.CreateAsync();

            // Cria o stream bidirecional
            _stream = _client.StreamingRecognize();

            // Envia a configuração inicial
            var config = new StreamingRecognitionConfig
            {
                Config = new RecognitionConfig
                {
                    Encoding = RecognitionConfig.Types.AudioEncoding.Linear16,
                    SampleRateHertz = _options.Google.SttSampleRateHertz,
                    LanguageCode = _options.Google.SttLanguageCode,
                    AudioChannelCount = 1,
                    EnableAutomaticPunctuation = true,
                    Model = "default"
                },
                InterimResults = true,
                SingleUtterance = false
            };

            var configRequest = new StreamingRecognizeRequest
            {
                StreamingConfig = config
            };

            await _stream.WriteAsync(configRequest);
            _logger.LogInformation("Stream de STT aberto com sucesso.");

            // Inicia a Task de leitura em background
            _responseReaderTask = ReadResponsesAsync(_streamCts.Token);

            // Inicia a Task de injeção de áudio
            _ = WriteAudioToStreamAsync(_streamCts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao abrir stream de STT.");
            Dispose();
            throw;
        }
    }

    /// <summary>
    /// Injeta buffer de áudio no stream de requisição.
    /// Pode ser chamado múltiplas vezes conforme o áudio chega do AGI/Asterisk.
    /// </summary>
    public async Task WriteAudioAsync(byte[] buffer, CancellationToken externalCt = default)
    {
        if (_isDisposed || _stream is null)
            throw new ObjectDisposedException(nameof(GoogleCloudStreamingSttService), "Stream foi fechado.");

        if (buffer is null || buffer.Length == 0)
            return;

        _audioBuffer.Enqueue(buffer);
        _audioAvailable.Set();

        await Task.CompletedTask;
    }

    /// <summary>
    /// Aguarda até que o Google retorne um resultado com IsFinal == true (VAD).
    /// Retorna null se o stream for encerrado antes de obter uma transcrição final.
    /// </summary>
    public async Task<string?> GetFinalTranscriptionAsync(CancellationToken externalCt = default)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(GoogleCloudStreamingSttService), "Stream foi fechado.");

        if (_responseReaderTask is null)
            throw new InvalidOperationException("Stream não foi iniciado. Chame OpenStreamAsync() primeiro.");

        try
        {
            // Aguarda até IsFinal ser setado ou o leitor de respostas terminar
            while (!_isFinal && !externalCt.IsCancellationRequested && !(_streamCts?.Token.IsCancellationRequested ?? false))
            {
                await Task.Delay(50, externalCt);
            }

            if (externalCt.IsCancellationRequested || (_streamCts?.Token.IsCancellationRequested ?? false))
            {
                _logger.LogWarning("GetFinalTranscriptionAsync foi cancelado.");
                return null;
            }

            _logger.LogInformation("Transcrição final obtida: '{Transcript}'", _finalTranscript ?? "(vazia)");
            return _finalTranscript ?? string.Empty;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("GetFinalTranscriptionAsync foi cancelado.");
            return null;
        }
    }

    public async Task<string> TranscribeAsync(string? audioPath, string? preTranscribedText, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(preTranscribedText))
            return preTranscribedText;

        if (string.IsNullOrWhiteSpace(audioPath))
            return string.Empty;

        var actualPath = ResolveAudioPath(audioPath);
        if (!File.Exists(actualPath))
        {
            _logger.LogWarning("Arquivo de áudio para STT streaming não encontrado: {Path}", actualPath);
            return string.Empty;
        }

        var audioBytes = await File.ReadAllBytesAsync(actualPath, ct);
        var payload = StripWavHeaderIfPresent(audioBytes);

        await OpenStreamAsync(ct);
        try
        {
            if (_stream is null)
                return string.Empty;

            const int chunkSize = 3200;
            for (var i = 0; i < payload.Length; i += chunkSize)
            {
                var length = Math.Min(chunkSize, payload.Length - i);
                var chunk = ByteString.CopyFrom(payload, i, length);
                await _stream.WriteAsync(new StreamingRecognizeRequest { AudioContent = chunk });
            }

            await CompleteRequestStreamAsync();

            var transcript = await GetFinalTranscriptionAsync(ct);
            return transcript ?? string.Empty;
        }
        finally
        {
            await CloseAsync(ct);
        }
    }

    /// <summary>
    /// Encerra o stream bidirecional gracefully.
    /// </summary>
    public async Task CloseAsync(CancellationToken externalCt = default)
    {
        if (_isDisposed)
            return;

        try
        {
            await CompleteRequestStreamAsync();

            _streamCts?.Cancel();

            if (_responseReaderTask is not null)
            {
                try
                {
                    await _responseReaderTask;
                }
                catch (OperationCanceledException)
                {
                    _logger.LogDebug("Task de leitura foi cancelada normalmente.");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao fechar stream de STT.");
        }
        finally
        {
            _isDisposed = true;
        }
    }

    private async Task CompleteRequestStreamAsync()
    {
        if (_requestCompleted || _stream is null)
            return;

        await _stream.WriteCompleteAsync();
        _requestCompleted = true;
        _logger.LogInformation("Stream de áudio finalizado.");
    }

    /// <summary>
    /// Task em background que lê o fluxo de respostas do Google.
    /// Para assim que detectar IsFinal == true.
    /// </summary>
    private async Task ReadResponsesAsync(CancellationToken ct)
    {
        try
        {
            if (_stream is null)
                return;

            // SpeechClient.StreamingRecognizeStream é um IAsyncEnumerable<StreamingRecognizeResponse>
            await foreach (var response in _stream.GrpcCall.ResponseStream.ReadAllAsync(ct))
            {
                if (response?.Results is null || response.Results.Count == 0)
                    continue;

                var result = response.Results.LastOrDefault();
                if (result is null)
                    continue;

                var isFinal = result.IsFinal;
                var transcript = result.Alternatives.FirstOrDefault()?.Transcript ?? string.Empty;

                _logger.LogDebug(
                    "STT Response: IsFinal={IsFinal}, Confidence={Confidence}, Transcript='{Transcript}'",
                    isFinal,
                    result.Alternatives.FirstOrDefault()?.Confidence ?? 0,
                    transcript);

                if (isFinal)
                {
                    _finalTranscript = transcript;
                    _isFinal = true;
                    _logger.LogInformation("Resultado final recebido do STT: '{FinalTranscript}'", _finalTranscript);
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("ReadResponsesAsync foi cancelado.");
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "Erro RPC ao ler respostas do STT: {StatusCode}", ex.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao ler respostas do STT.");
        }
    }

    /// <summary>
    /// Task em background que escreve áudio do buffer no stream de requisição.
    /// </summary>
    private async Task WriteAudioToStreamAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && _stream is not null)
            {
                if (_audioBuffer.TryDequeue(out var buffer))
                {
                    var audioRequest = new StreamingRecognizeRequest
                    {
                        AudioContent = ByteString.CopyFrom(buffer)
                    };

                    await _stream.WriteAsync(audioRequest);
                }
                else
                {
                    // Sem áudio no buffer, aguarda sinal
                    _audioAvailable.Reset();
                    _audioAvailable.Wait(100, ct);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("WriteAudioToStreamAsync foi cancelado.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao escrever áudio no stream.");
        }
    }

    /// <summary>
    /// Garante que as credenciais do Google Cloud estão configuradas.
    /// </summary>
    private void EnsureCredentials()
    {
        if (string.IsNullOrWhiteSpace(_options.Google.CredentialsPath))
            return;

        if (!File.Exists(_options.Google.CredentialsPath))
            throw new FileNotFoundException("Google credentials file not found.", _options.Google.CredentialsPath);

        Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", _options.Google.CredentialsPath);
    }

    private static string ResolveAudioPath(string path)
    {
        if (File.Exists(path))
            return path;

        return path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ? path : path + ".wav";
    }

    private static byte[] StripWavHeaderIfPresent(byte[] audioBytes)
    {
        if (audioBytes.Length <= 44)
            return audioBytes;

        if (audioBytes[0] == (byte)'R' && audioBytes[1] == (byte)'I' && audioBytes[2] == (byte)'F' && audioBytes[3] == (byte)'F'
            && audioBytes[8] == (byte)'W' && audioBytes[9] == (byte)'A' && audioBytes[10] == (byte)'V' && audioBytes[11] == (byte)'E')
        {
            var pcm = new byte[audioBytes.Length - 44];
            Buffer.BlockCopy(audioBytes, 44, pcm, 0, pcm.Length);
            return pcm;
        }

        return audioBytes;
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        try
        {
            _streamCts?.Cancel();
            _streamCts?.Dispose();
            _responseReaderTask?.Dispose();
            _audioAvailable?.Dispose();
            _stream?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao disposar GoogleCloudStreamingSttService.");
        }

        _isDisposed = true;
    }
}
