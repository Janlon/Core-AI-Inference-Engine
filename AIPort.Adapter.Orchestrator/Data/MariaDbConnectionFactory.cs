using System.Data;
using AIPort.Adapter.Orchestrator.Config;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace AIPort.Adapter.Orchestrator.Data;

public sealed class MariaDbConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public MariaDbConnectionFactory(IOptions<MariaDbOptions> options)
    {
        _connectionString = options.Value.ConnectionString;
    }

    public IDbConnection CreateConnection() => new MySqlConnection(_connectionString);
}
