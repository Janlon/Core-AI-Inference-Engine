using System.Text.Json;
using AIPort.Intelligence.Service.Domain.Options;
using AIPort.Intelligence.Service.Domain.Rules;
using AIPort.Intelligence.Service.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace AIPort.Intelligence.Service.Services;

/// <summary>
/// Carrega as regras de fluxo declarativo do arquivo tenant-rules.json.
/// Registrado como Singleton — o arquivo é lido uma única vez na inicialização.
/// </summary>
public sealed class RulesLoaderService : IRulesLoader
{
    private readonly IReadOnlyDictionary<string, TenantRule> _rules;
    private readonly ILogger<RulesLoaderService> _logger;

    public RulesLoaderService(
        IOptions<AIServiceOptions> options,
        IHostEnvironment env,
        ILogger<RulesLoaderService> logger)
    {
        _logger = logger;
        _rules = LoadRules(options.Value.RulesFilePath, env.ContentRootPath);
    }

    public TenantRule? GetRule(string tenantType) =>
        _rules.TryGetValue(tenantType.ToLowerInvariant(), out var rule) ? rule : null;

    public IReadOnlyList<TenantRule> GetAll() =>
        _rules.Values.ToList().AsReadOnly();

    private IReadOnlyDictionary<string, TenantRule> LoadRules(string relativePath, string contentRoot)
    {
        var fullPath = Path.IsPathRooted(relativePath)
            ? relativePath
            : Path.Combine(contentRoot, relativePath);

        if (!File.Exists(fullPath))
        {
            _logger.LogWarning("Arquivo de regras não encontrado em '{Path}'. Iniciando sem regras de tenant.", fullPath);
            return new Dictionary<string, TenantRule>();
        }

        try
        {
            var json = File.ReadAllText(fullPath);
            var container = JsonSerializer.Deserialize<TenantRulesContainer>(json, JsonOptions);

            if (container?.TenantRules is null)
                return new Dictionary<string, TenantRule>();

            var dict = container.TenantRules
                .Where(r => !string.IsNullOrWhiteSpace(r.TenantType))
                .ToDictionary(r => r.TenantType.ToLowerInvariant());

            _logger.LogInformation("Regras de tenant carregadas: {Count} tenant(s).", dict.Count);
            return dict;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao carregar regras de tenant de '{Path}'.", fullPath);
            return new Dictionary<string, TenantRule>();
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private sealed class TenantRulesContainer
    {
        public List<TenantRule>? TenantRules { get; set; }
    }
}
