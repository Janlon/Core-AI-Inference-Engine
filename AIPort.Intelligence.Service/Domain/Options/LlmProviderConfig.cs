namespace AIPort.Intelligence.Service.Domain.Options;

/// <summary>
/// Configuração de um provedor LLM individual.
/// A lista de provedores é lida do appsettings, permitindo adicionar/remover
/// IAs sem alterar código — apenas editar a configuração.
/// </summary>
public sealed class LlmProviderConfig
{
    /// <summary>Nome amigável do provedor (apenas para logging).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Tipo do serviço. Valores suportados:
    /// "AzureOpenAI" | "OpenAI" | "Ollama".
    /// </summary>
    public string ServiceType { get; set; } = "AzureOpenAI";

    /// <summary>Chave de API do provedor.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Nome/ID do modelo a ser utilizado.
    /// Exemplos: "gpt-4o", "gpt-4o-mini", "llama3", "phi3".
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Endpoint base do serviço.
    /// Obrigatório para AzureOpenAI e Ollama; ignorado para OpenAI SaaS.
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Identificador de implantação (necessário apenas para AzureOpenAI).</summary>
    public string DeploymentName { get; set; } = string.Empty;

    /// <summary>Se false, este provedor é ignorado sem remover do arquivo de config.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Ordem de preferência — menor número = maior prioridade.</summary>
    public int Priority { get; set; } = 1;
}
