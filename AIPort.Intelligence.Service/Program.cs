using AIPort.Intelligence.Service.Domain.Options;
using AIPort.Intelligence.Service.Services;
using AIPort.Intelligence.Service.Services.Engines;
using AIPort.Intelligence.Service.Services.Interfaces;
using Scalar.AspNetCore;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

IntelligenceEnvironmentOverrides.Apply(builder.Configuration);

var serverConfig = new ServerOptions();
builder.Configuration.GetSection(ServerOptions.SectionName).Bind(serverConfig);

builder.WebHost.UseUrls(serverConfig.Urls)
    .ConfigureKestrel(kestrelOptions =>
    {
        kestrelOptions.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(Math.Max(1, serverConfig.RequestHeadersTimeoutSeconds));
        kestrelOptions.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(Math.Max(1, serverConfig.KeepAliveTimeoutSeconds));
    });

// ── Configurações (Padrão IOptions) ─────────────────────────────────────────
builder.Services.Configure<AIServiceOptions>(
    builder.Configuration.GetSection(AIServiceOptions.SectionName));
builder.Services.Configure<ServerOptions>(
    builder.Configuration.GetSection(ServerOptions.SectionName));

// ── Controllers ──────────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        opts.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        opts.JsonSerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
    });

// ── OpenAPI (.NET 10 nativo) ─────────────────────────────────────────────────
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info.Title = "AIPort Intelligence Service";
        document.Info.Version = "v1";
        document.Info.Description =
            "Motor de IA e NLP para portaria virtual — 'cérebro' do sistema AIPort. " +
            "Processa texto do visitante e retorna decisões estruturadas via pipeline " +
            "Regex → NLP → LLM (Semantic Kernel).";
        return Task.CompletedTask;
    });
});

// ── Serviços de Domínio — Injeção de Dependência ─────────────────────────────
//  Singleton  → RulesLoaderService: lê o JSON uma vez e mantém em memória.
//  Scoped     → Engines e DecisionEngine: ciclo de vida por request HTTP.
builder.Services.AddSingleton<IRulesLoader, RulesLoaderService>();

builder.Services.AddScoped<IRegexProcessor, RegexEngine>();
builder.Services.AddHttpClient<INlpProcessor, HttpNlpProcessor>((serviceProvider, client) =>
{
    var nlpOptions = serviceProvider.GetRequiredService<IOptions<AIServiceOptions>>().Value.Nlp;

    if (Uri.TryCreate(nlpOptions.ExternalApiBaseUrl, UriKind.Absolute, out var baseUri))
        client.BaseAddress = baseUri;

    client.Timeout = TimeSpan.FromMilliseconds(Math.Max(1000, nlpOptions.ExternalApiTimeoutMs));
});
builder.Services.AddScoped<ILlmProcessor, LlmEngine>();
builder.Services.AddScoped<ILlmHealthService, LlmHealthService>();
builder.Services.AddScoped<IDecisionEngine, DecisionEngine>();

// ── HttpClient factory (necessário para Ollama e extensões futuras) ───────────
builder.Services.AddHttpClient();

// ── Logging ───────────────────────────────────────────────────────────────────
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var app = builder.Build();

// ── Pipeline HTTP ─────────────────────────────────────────────────────────────
app.MapOpenApi();
app.MapScalarApiReference(opts =>
{
    opts.Title = "AIPort Intelligence Service";
    opts.Theme = ScalarTheme.DeepSpace;
    opts.DefaultHttpClient = new(ScalarTarget.CSharp, ScalarClient.HttpClient);
});

app.MapGet("/docs", () => Results.Redirect("/scalar/v1", permanent: false));

//app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", async (ILlmHealthService llmHealthService, CancellationToken ct) =>
{
    var llm = await llmHealthService.GetHealthAsync(ct);
    var body = new
    {
        status = llm.Status,
        service = "AIPort Intelligence",
        utc = DateTime.UtcNow,
        llm
    };

    return llm.Status == "healthy"
        ? Results.Ok(body)
        : Results.Json(body, statusCode: StatusCodes.Status503ServiceUnavailable);
});
app.Run();
