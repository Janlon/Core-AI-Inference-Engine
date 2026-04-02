using System.Data;

namespace AIPort.Adapter.Orchestrator.Data;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}
