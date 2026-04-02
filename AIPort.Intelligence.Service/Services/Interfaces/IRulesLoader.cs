using AIPort.Intelligence.Service.Domain.Rules;

namespace AIPort.Intelligence.Service.Services.Interfaces;

/// <summary>
/// Carrega e provê as regras de fluxo declarativo por tenant
/// a partir do arquivo JSON configurado (ou de outra fonte, como banco de dados).
/// </summary>
public interface IRulesLoader
{
    /// <summary>
    /// Retorna a regra de fluxo correspondente ao tenant informado.
    /// Retorna null se o tenant não possuir regra cadastrada.
    /// </summary>
    /// <param name="tenantType">Identificador do tipo de tenant.</param>
    TenantRule? GetRule(string tenantType);

    /// <summary>
    /// Retorna todas as regras carregadas.
    /// </summary>
    IReadOnlyList<TenantRule> GetAll();
}
