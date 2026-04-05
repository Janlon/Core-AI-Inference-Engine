using System.Data;
using System.Net;
using System.Net.Http;
using System.Text;
using AIPort.Adapter.Orchestrator.Agi.Interfaces;
using AIPort.Adapter.Orchestrator.Config;
using AIPort.Adapter.Orchestrator.Data;
using AIPort.Adapter.Orchestrator.Services;
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
            Options.Create(new IntelligenceServiceOptions { BaseUrl = "http://localhost:9999" }),
            NullLogger<HealthCheckService>.Instance);

        var status = await sut.GetHealthStatusAsync();

        Assert.Equal(0, Assert.IsType<int>(status["activeCalls"]));

        var asterisk = status["asterisk"];
        var activeCallsProperty = asterisk.GetType().GetProperty("activeCalls");
        Assert.NotNull(activeCallsProperty);
        Assert.Equal(0, activeCallsProperty!.GetValue(asterisk));
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
}

#pragma warning restore CS8767