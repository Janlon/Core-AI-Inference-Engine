namespace AIPort.Adapter.Orchestrator.Services.Interfaces;

/// <summary>
/// Serviço responsável por verificar a saúde e disponibilidade de componentes críticos
/// </summary>
public interface IHealthCheckService
{
    /// <summary>
    /// Verifica conectividade com banco e valida quantidade mínima de tenants ativos
    /// </summary>
    /// <param name="minActiveTenants">Quantidade mínima esperada de tenants ativos</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Resultado detalhado da checagem do banco</returns>
    Task<DatabaseHealthResult> CheckDatabaseHealthAsync(int minActiveTenants = 1, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica se a conexão com o banco de dados está disponível
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se a conexão está funcionando, False caso contrário</returns>
    Task<bool> IsDatabaseHealthyAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Obtém o status geral de saúde do serviço incluindo banco de dados
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Dicionário com status de cada componente</returns>
    Task<IDictionary<string, object>> GetHealthStatusAsync(CancellationToken cancellationToken = default);
}
