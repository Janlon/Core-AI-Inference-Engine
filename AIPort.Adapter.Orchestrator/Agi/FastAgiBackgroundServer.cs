using System.Net;
using System.Net.Sockets;
using System.Text;
using AIPort.Adapter.Orchestrator.Agi.Interfaces;
using AIPort.Adapter.Orchestrator.Agi.Models;
using AIPort.Adapter.Orchestrator.Config;
using Microsoft.Extensions.Options;

namespace AIPort.Adapter.Orchestrator.Agi;

/// <summary>
/// Servidor FastAGI que mantém a conexão aberta durante toda a chamada,
/// enviando comandos AGI e aguardando respostas do Asterisk.
/// </summary>
public sealed class FastAgiBackgroundServer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAgiRuntimeState _runtimeState;
    private readonly AgiServerOptions _options;
    private readonly ILogger<FastAgiBackgroundServer> _logger;
    private TcpListener? _listener;

    public FastAgiBackgroundServer(
        IServiceScopeFactory scopeFactory,
        IAgiRuntimeState runtimeState,
        IOptions<AgiServerOptions> options,
        ILogger<FastAgiBackgroundServer> logger)
    {
        _scopeFactory = scopeFactory;
        _runtimeState = runtimeState;
        _options = options.Value;
        _logger = logger;
        _runtimeState.SetConfigured(_options.Host, _options.Port, _options.Enabled);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _runtimeState.SetListening(false);
            _logger.LogInformation("FastAGI desabilitado via configuração.");
            return;
        }

        var bindAddress = ResolveBindAddress(_options.Host);

        try
        {
            _listener = new TcpListener(bindAddress, _options.Port);
            _listener.Start(Math.Max(1, _options.Backlog));
            _runtimeState.SetListening(true);
            _logger.LogInformation(
                "FastAGI iniciado em {Host}:{Port} com backlog {Backlog}",
                bindAddress,
                _options.Port,
                Math.Max(1, _options.Backlog));
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
        {
            _logger.LogCritical(ex, "Porta FastAGI {Port} já está em uso.", _options.Port);
            throw;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(stoppingToken);
                _ = Task.Run(() => HandleClientAsync(client, stoppingToken), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao aceitar conexão FastAGI.");
            }
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _listener?.Stop();
        _runtimeState.SetListening(false);
        _logger.LogInformation("FastAGI encerrado.");
        return base.StopAsync(cancellationToken);
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        var remoteEndpoint = client.Client.RemoteEndPoint?.ToString();
        var connectionId = _runtimeState.IncrementActiveChannels(remoteEndpoint);
        using var _ = client;
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false))
        {
            AutoFlush = true,
            NewLine = "\n"
        };

        var map = await ReadHandshakeVariablesAsync(reader, ct);

        var req = new FastAgiRequest
        {
            UniqueId = FirstNonEmpty(map, "agi_uniqueid", "uniqueid"),
            CallerId = FirstNonEmpty(map, "agi_callerid", "callerid"),
            Channel = FirstNonEmpty(map, "agi_channel", "channel"),
            CalledNumber = FirstNonEmpty(map, "agi_extension", "callednumber"),
            Context = FirstNonEmpty(map, "agi_context", "context"),
            TenantPid = ParseInt(FirstNonEmpty(map, "tenantpid", "agi_arg_1")),
            AudioFilePath = FirstNonEmpty(map, "audiofilepath"),
            PreTranscribedText = FirstNonEmpty(map, "pretranscribedtext"),
            Variables = new Dictionary<string, string>(map, StringComparer.OrdinalIgnoreCase)
        };

        _runtimeState.AttachHandshake(connectionId, req.UniqueId, req.CallerId, req.Channel, req.TenantPid);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var channelLogger = scope.ServiceProvider.GetRequiredService<ILogger<AgiChannel>>();
            var channel = new AgiChannel(reader, writer, channelLogger);

            // Mantém a chamada ativa no Asterisk antes de delegar para o handler.
            try
            {
                await channel.AnswerAsync(ct);
            }
            catch (AgiHangupException ex) when (IsRedundantAnswer(ex))
            {
                _logger.LogWarning("Asterisk retornou 510 para ANSWER redundante. Prosseguindo com a chamada já atendida.");
            }

            var handler = scope.ServiceProvider.GetRequiredService<IAgiCallHandler>();
            await handler.HandleAsync(req, channel, ct);
        }
        catch (AgiHangupException ex)
        {
            _logger.LogInformation(ex, "Canal AGI encerrado durante atendimento Session={SessionId}", req.UniqueId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha inesperada no atendimento FastAGI Session={SessionId}", req.UniqueId);
        }
        finally
        {
            _runtimeState.DecrementActiveChannels(connectionId);
        }
    }

    private static async Task<Dictionary<string, string>> ReadHandshakeVariablesAsync(StreamReader reader, CancellationToken ct)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null)
                throw new AgiHangupException("Conexão AGI encerrada durante leitura do handshake inicial.");

            if (line.Length == 0)
                break;

            var idx = line.IndexOf(':');
            if (idx <= 0)
                continue;

            var key = line[..idx].Trim();
            var value = line[(idx + 1)..].Trim();

            if (!string.IsNullOrWhiteSpace(key))
                map[key] = value;
        }

        return map;
    }

    private static string FirstNonEmpty(Dictionary<string, string> data, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (data.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                return value;
        }

        return string.Empty;
    }

    private static int ParseInt(string value)
        => int.TryParse(value, out var n) ? n : 0;

    private static bool IsRedundantAnswer(AgiHangupException ex)
        => ex.Message.Contains("510", StringComparison.OrdinalIgnoreCase)
           && ex.Message.Contains("Invalid or unknown command", StringComparison.OrdinalIgnoreCase);

    private IPAddress ResolveBindAddress(string host)
    {
        if (IPAddress.TryParse(host, out var parsed))
            return parsed;

        _logger.LogWarning("Host FastAGI inválido '{Host}'. Usando 0.0.0.0.", host);
        return IPAddress.Any;
    }
}
