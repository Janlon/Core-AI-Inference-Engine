using System.Data;
using System.Net;
using System.Net.Http;
using System.Text;
using AIPort.Adapter.Orchestrator.Agi.Models;
using AIPort.Adapter.Orchestrator.Agi.Interfaces;
using AIPort.Adapter.Orchestrator.Config;
using AIPort.Adapter.Orchestrator.Data;
using AIPort.Adapter.Orchestrator.Services;
using AIPort.Adapter.Orchestrator.Services.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AIPort.Adapter.Orchestrator.Tests.Services;

#pragma warning disable CS8767

public sealed class HealthCheckServiceTests
{
    [Fact]
    public async Task GetHealthStatusAsync_UsesLiveFastAgiChannelsForActiveCalls()
    {
        var dbConnectionFactory = new StubDbConnectionFactory(3L);
        var runtimeState = new Mock<IAgiRuntimeState>();
        var httpClientFactory = new Mock<IHttpClientFactory>();

        runtimeState.SetupGet(x => x.IsEnabled).Returns(true);
        runtimeState.SetupGet(x => x.IsListening).Returns(true);
        runtimeState.SetupGet(x => x.Host).Returns("127.0.0.1");
        runtimeState.SetupGet(x => x.Port).Returns(4573);
        runtimeState.SetupGet(x => x.ActiveChannels).Returns(0);
        runtimeState.Setup(x => x.GetActiveChannelSnapshots()).Returns(Array.Empty<ActiveAgiChannelSnapshot>());

        httpClientFactory
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(new StaticHttpMessageHandler(HttpStatusCode.OK))
            {
                BaseAddress = new Uri("http://localhost")
            });

        var sut = new HealthCheckService(
            dbConnectionFactory,
            runtimeState.Object,
            httpClientFactory.Object,
            new StubSystemTelemetryProvider(),
            new StubSpeechWarmupStatusProvider(),
            Options.Create(new IntelligenceServiceOptions { BaseUrl = "http://localhost:9999" }),
            NullLogger<HealthCheckService>.Instance);

        var status = await sut.GetHealthStatusAsync();

        Assert.Equal(0, Assert.IsType<int>(status["activeCalls"]));

        var asterisk = status["asterisk"];
        var activeCallsProperty = asterisk.GetType().GetProperty("activeCalls");
        Assert.NotNull(activeCallsProperty);
        Assert.Equal(0, activeCallsProperty!.GetValue(asterisk));
    }

    [Fact]
    public async Task GetHealthStatusAsync_IncludesSystemTelemetrySnapshot()
    {
        var dbConnectionFactory = new StubDbConnectionFactory(3L);
        var runtimeState = new Mock<IAgiRuntimeState>();
        var httpClientFactory = new Mock<IHttpClientFactory>();

        runtimeState.SetupGet(x => x.IsEnabled).Returns(true);
        runtimeState.SetupGet(x => x.IsListening).Returns(true);
        runtimeState.SetupGet(x => x.Host).Returns("127.0.0.1");
        runtimeState.SetupGet(x => x.Port).Returns(4573);
        runtimeState.SetupGet(x => x.ActiveChannels).Returns(2);
        runtimeState.Setup(x => x.GetActiveChannelSnapshots()).Returns(new[]
        {
            new ActiveAgiChannelSnapshot("c1", new DateTime(2026, 4, 8, 12, 0, 0, DateTimeKind.Utc), "10.0.0.10:4000", "s1", "100", "PJSIP/100-00000001", 200),
            new ActiveAgiChannelSnapshot("c2", new DateTime(2026, 4, 8, 12, 0, 2, DateTimeKind.Utc), "10.0.0.11:4001", "s2", "101", "PJSIP/101-00000002", 200)
        });

        httpClientFactory
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(new StaticHttpMessageHandler(HttpStatusCode.OK))
            {
                BaseAddress = new Uri("http://localhost")
            });

        var snapshot = new SystemTelemetrySnapshot(
            Status: "healthy",
            Platform: "linux",
            SampledAtUtc: new DateTime(2026, 4, 8, 12, 0, 0, DateTimeKind.Utc),
            LogicalCores: 8,
            CpuUsagePercent: 21.4,
            TotalMemoryBytes: 16L * 1024 * 1024 * 1024,
            UsedMemoryBytes: 6L * 1024 * 1024 * 1024,
            AvailableMemoryBytes: 10L * 1024 * 1024 * 1024,
            MemoryUsagePercent: 37.5,
            Message: "Telemetria coletada do host Linux via /proc.");

        var sut = new HealthCheckService(
            dbConnectionFactory,
            runtimeState.Object,
            httpClientFactory.Object,
            new StubSystemTelemetryProvider(snapshot),
            new StubSpeechWarmupStatusProvider(),
            Options.Create(new IntelligenceServiceOptions { BaseUrl = "http://localhost:9999" }),
            NullLogger<HealthCheckService>.Instance);

        var status = await sut.GetHealthStatusAsync();

        var asterisk = status["asterisk"];
        var details = asterisk.GetType().GetProperty("activeChannelDetails")?.GetValue(asterisk) as IReadOnlyCollection<ActiveAgiChannelSnapshot>;
        Assert.NotNull(details);
        Assert.Equal(2, details!.Count);

        var system = status["system"];
        var systemType = system.GetType();
        Assert.Equal("healthy", systemType.GetProperty("status")?.GetValue(system));
        Assert.Equal("linux", systemType.GetProperty("platform")?.GetValue(system));

        var cpu = systemType.GetProperty("cpu")?.GetValue(system);
        Assert.NotNull(cpu);
        Assert.Equal(21.4, cpu!.GetType().GetProperty("usagePercent")?.GetValue(cpu));

        var memory = systemType.GetProperty("memory")?.GetValue(system);
        Assert.NotNull(memory);
        Assert.Equal(37.5, memory!.GetType().GetProperty("usagePercent")?.GetValue(memory));
    }

    [Fact]
    public async Task GetHealthStatusAsync_IncludesSpeechWarmupStatus()
    {
        var dbConnectionFactory = new StubDbConnectionFactory(3L);
        var runtimeState = new Mock<IAgiRuntimeState>();
        var httpClientFactory = new Mock<IHttpClientFactory>();

        runtimeState.SetupGet(x => x.IsEnabled).Returns(true);
        runtimeState.SetupGet(x => x.IsListening).Returns(true);
        runtimeState.SetupGet(x => x.Host).Returns("127.0.0.1");
        runtimeState.SetupGet(x => x.Port).Returns(4573);
        runtimeState.SetupGet(x => x.ActiveChannels).Returns(1);
        runtimeState.Setup(x => x.GetActiveChannelSnapshots()).Returns(Array.Empty<ActiveAgiChannelSnapshot>());

        httpClientFactory
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(new StaticHttpMessageHandler(HttpStatusCode.OK))
            {
                BaseAddress = new Uri("http://localhost")
            });

        var speechSnapshot = new SpeechWarmupStatusSnapshot(
            Status: "healthy",
            Provider: "google",
            Ready: true,
            LastWarmupAtUtc: new DateTime(2026, 4, 8, 12, 1, 0, DateTimeKind.Utc),
            LastWarmupElapsedMs: 842,
            Message: "Warmup do TTS concluido e cliente pronto.");

        var sut = new HealthCheckService(
            dbConnectionFactory,
            runtimeState.Object,
            httpClientFactory.Object,
            new StubSystemTelemetryProvider(),
            new StubSpeechWarmupStatusProvider(speechSnapshot),
            Options.Create(new IntelligenceServiceOptions { BaseUrl = "http://localhost:9999" }),
            NullLogger<HealthCheckService>.Instance);

        var status = await sut.GetHealthStatusAsync();

        var speech = status["speech"];
        var speechType = speech.GetType();
        Assert.Equal("healthy", speechType.GetProperty("status")?.GetValue(speech));
        Assert.Equal("google", speechType.GetProperty("provider")?.GetValue(speech));
        Assert.Equal(true, speechType.GetProperty("ready")?.GetValue(speech));
        Assert.Equal(842L, speechType.GetProperty("lastWarmupElapsedMs")?.GetValue(speech));
    }

    private sealed class StubDbConnectionFactory : IDbConnectionFactory
    {
        private readonly long _activeTenants;

        public StubDbConnectionFactory(long activeTenants)
        {
            _activeTenants = activeTenants;
        }

        public IDbConnection CreateConnection()
        {
            return new StubDbConnection(_activeTenants);
        }
    }

    private sealed class StubDbConnection : IDbConnection
    {
        private readonly long _activeTenants;

        public StubDbConnection(long activeTenants)
        {
            _activeTenants = activeTenants;
        }

        public string ConnectionString { get; set; } = string.Empty;
        public int ConnectionTimeout => 0;
        public string Database => "Stub";
        public ConnectionState State { get; private set; } = ConnectionState.Closed;

        public IDbTransaction BeginTransaction() => throw new NotSupportedException();
        public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotSupportedException();
        public void ChangeDatabase(string databaseName) => throw new NotSupportedException();
        public void Close() => State = ConnectionState.Closed;
        public IDbCommand CreateCommand() => new StubDbCommand(_activeTenants);
        public void Dispose() => Close();
        public void Open() => State = ConnectionState.Open;
    }

    private sealed class StubDbCommand : IDbCommand
    {
        private readonly long _result;

        public StubDbCommand(long result)
        {
            _result = result;
            Parameters = new StubParameterCollection();
        }

        public string CommandText { get; set; } = string.Empty;
        public int CommandTimeout { get; set; }
        public CommandType CommandType { get; set; } = CommandType.Text;
        public IDbConnection? Connection { get; set; }
        public IDataParameterCollection Parameters { get; }
        public IDbTransaction? Transaction { get; set; }
        public UpdateRowSource UpdatedRowSource { get; set; }

        public void Cancel() { }
        public IDbDataParameter CreateParameter() => new StubDbParameter();
        public void Dispose() { }
        public int ExecuteNonQuery() => 0;
        public IDataReader ExecuteReader() => throw new NotSupportedException();
        public IDataReader ExecuteReader(CommandBehavior behavior) => throw new NotSupportedException();
        public object ExecuteScalar() => _result;
        public void Prepare() { }
    }

    private sealed class StubDbParameter : IDbDataParameter
    {
        public byte Precision { get; set; }
        public byte Scale { get; set; }
        public int Size { get; set; }
        public DbType DbType { get; set; }
        public ParameterDirection Direction { get; set; } = ParameterDirection.Input;
        public bool IsNullable => true;
        public string ParameterName { get; set; } = string.Empty;
        public string SourceColumn { get; set; } = string.Empty;
        public DataRowVersion SourceVersion { get; set; } = DataRowVersion.Current;
        public object? Value { get; set; }
    }

    private sealed class StubParameterCollection : List<object>, IDataParameterCollection
    {
        public object this[string parameterName]
        {
            get => this[0];
            set { }
        }

        public bool Contains(string parameterName) => false;
        public int IndexOf(string parameterName) => -1;
        public void RemoveAt(string parameterName) { }
    }

    private sealed class StaticHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;

        public StaticHttpMessageHandler(HttpStatusCode statusCode)
        {
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class StubSystemTelemetryProvider : ISystemTelemetryProvider
    {
        private readonly SystemTelemetrySnapshot _snapshot;

        public StubSystemTelemetryProvider()
            : this(new SystemTelemetrySnapshot(
                Status: "healthy",
                Platform: "linux",
                SampledAtUtc: DateTime.UtcNow,
                LogicalCores: 4,
                CpuUsagePercent: 12.5,
                TotalMemoryBytes: 8L * 1024 * 1024 * 1024,
                UsedMemoryBytes: 3L * 1024 * 1024 * 1024,
                AvailableMemoryBytes: 5L * 1024 * 1024 * 1024,
                MemoryUsagePercent: 37.5,
                Message: "stub"))
        {
        }

        public StubSystemTelemetryProvider(SystemTelemetrySnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public Task<SystemTelemetrySnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_snapshot);
        }
    }

    private sealed class StubSpeechWarmupStatusProvider : ISpeechWarmupStatusProvider
    {
        private readonly SpeechWarmupStatusSnapshot _snapshot;

        public StubSpeechWarmupStatusProvider()
            : this(new SpeechWarmupStatusSnapshot(
                Status: "healthy",
                Provider: "google",
                Ready: true,
                LastWarmupAtUtc: DateTime.UtcNow,
                LastWarmupElapsedMs: 100,
                Message: "stub"))
        {
        }

        public StubSpeechWarmupStatusProvider(SpeechWarmupStatusSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public SpeechWarmupStatusSnapshot GetCurrent() => _snapshot;
    }
}

#pragma warning restore CS8767