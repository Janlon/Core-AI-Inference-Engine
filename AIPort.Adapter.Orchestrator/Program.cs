using AIPort.Adapter.Orchestrator.Agi;
using AIPort.Adapter.Orchestrator.Agi.Interfaces;
using AIPort.Adapter.Orchestrator.Config;
using AIPort.Adapter.Orchestrator.Data;
using AIPort.Adapter.Orchestrator.Data.Repositories;
using AIPort.Adapter.Orchestrator.Integrations;
using AIPort.Adapter.Orchestrator.Integrations.Interfaces;
using AIPort.Adapter.Orchestrator.Services;
using AIPort.Adapter.Orchestrator.Services.Interfaces;
using Polly;
using Polly.Extensions.Http;
using Scalar.AspNetCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

OrchestratorEnvironmentOverrides.Apply(builder.Configuration);
var runtimeInputOptions = builder.Configuration.Get<RuntimeInputOptions>() ?? new RuntimeInputOptions();
var useDeveloperSandbox = runtimeInputOptions.InputSourceMode is InputSourceMode.WindowsText or InputSourceMode.WindowsVoice;

var serverConfig = new ServerOptions();
builder.Configuration.GetSection(ServerOptions.SectionName).Bind(serverConfig);

builder.WebHost.UseUrls(serverConfig.Urls)
    .ConfigureKestrel(kestrelOptions =>
    {
        kestrelOptions.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(Math.Max(1, serverConfig.RequestHeadersTimeoutSeconds));
        kestrelOptions.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(Math.Max(1, serverConfig.KeepAliveTimeoutSeconds));
    });

builder.Services.Configure<AgiServerOptions>(builder.Configuration.GetSection(AgiServerOptions.SectionName));
builder.Services.Configure<IntelligenceServiceOptions>(builder.Configuration.GetSection(IntelligenceServiceOptions.SectionName));
builder.Services.Configure<MariaDbOptions>(builder.Configuration.GetSection(MariaDbOptions.SectionName));
builder.Services.Configure<WebhookOptions>(builder.Configuration.GetSection(WebhookOptions.SectionName));
builder.Services.Configure<SpeechOptions>(builder.Configuration.GetSection(SpeechOptions.SectionName));
builder.Services.Configure<ServerOptions>(builder.Configuration.GetSection(ServerOptions.SectionName));
builder.Services.Configure<FallbackRoutingOptions>(builder.Configuration.GetSection(FallbackRoutingOptions.SectionName));
builder.Services.Configure<RuntimeInputOptions>(builder.Configuration);

builder.Services.AddSingleton<IDbConnectionFactory, MariaDbConnectionFactory>();
builder.Services.AddSingleton<IAgiRuntimeState, AgiRuntimeState>();
builder.Services.AddScoped<ITenantRepository, TenantRepository>();
builder.Services.AddScoped<ICallSessionRepository, CallSessionRepository>();

builder.Services.AddScoped<SpeechToTextService>();
builder.Services.AddScoped<ITextToSpeechService, TextToSpeechService>();
builder.Services.AddScoped<GoogleCloudStreamingSttService>();
builder.Services.AddScoped<ISpeechToTextService>(sp =>
{
    var speechOptions = sp.GetRequiredService<IOptions<SpeechOptions>>().Value;
    if (speechOptions.Google.UseStreamingStt)
        return sp.GetRequiredService<GoogleCloudStreamingSttService>();

    return sp.GetRequiredService<SpeechToTextService>();
});

builder.Services.AddScoped<IAsteriskCommandClient, AsteriskCommandClient>();
builder.Services.AddScoped<IDecisionExecutor, DecisionExecutor>();
builder.Services.AddScoped<INotificationCascadeService, NotificationCascadeService>();
builder.Services.AddScoped<IOrchestrationService, OrchestrationService>();
builder.Services.AddScoped<IAgiCallHandler, AgiCallHandler>();
builder.Services.AddScoped<IHealthCheckService, HealthCheckService>();

if (useDeveloperSandbox)
{
    builder.Services.AddHostedService<DeveloperSandboxHostedService>();
}
else
{
    builder.Services.AddHostedService<FastAgiBackgroundServer>();
}

builder.Services.AddHttpClient<IIntelligenceServiceClient, IntelligenceServiceClient>((sp, client) =>
{
    var opts = sp.GetRequiredService<IOptions<IntelligenceServiceOptions>>().Value;
    client.BaseAddress = new Uri(opts.BaseUrl.Trim().TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromMilliseconds(opts.TimeoutMs);
}).AddPolicyHandler((sp, _) =>
{
    var opts = sp.GetRequiredService<IOptions<IntelligenceServiceOptions>>().Value;
    return BuildTransientHttpPolicy(opts.MaxRetryAttempts, opts.RetryBaseDelayMs);
});

if (useDeveloperSandbox && runtimeInputOptions.DeveloperSandbox.DisableWebhookCalls)
{
    builder.Services.AddSingleton<IWebhookClient, SandboxWebhookClient>();
}
else
{
    builder.Services.AddHttpClient<IWebhookClient, WebhookClient>((sp, client) =>
    {
        var opts = sp.GetRequiredService<IOptions<WebhookOptions>>().Value;
        var timeoutMs = Math.Max(1, Math.Min(2000, opts.TimeoutMs));
        client.Timeout = TimeSpan.FromMilliseconds(timeoutMs);
    }).AddPolicyHandler((sp, _) =>
    {
        var opts = sp.GetRequiredService<IOptions<WebhookOptions>>().Value;
        var maxRetries = Math.Max(0, Math.Min(2, opts.MaxRetryAttempts));
        return BuildTransientHttpPolicy(maxRetries, opts.RetryBaseDelayMs);
    });
}

builder.Services.AddControllers();
builder.Services.AddSingleton<IEventService, EventService>();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info.Title = "AIPort Adapter Orchestrator";
        document.Info.Version = "v1";
        document.Info.Description =
            "FastAGI Orchestrator para fluxo de chamadas, integração com IA e serviços de voz.";
        return Task.CompletedTask;
    });
});

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference(opts =>
{
    opts.Title = "AIPort Orchestrator API";
    opts.Theme = ScalarTheme.DeepSpace;
    opts.DefaultHttpClient = new(ScalarTarget.CSharp, ScalarClient.HttpClient);
});

app.MapGet("/docs", () => Results.Redirect("/scalar/v1", permanent: false));

app.MapGet("/health", async (IHealthCheckService healthCheck) =>
{
    var status = await healthCheck.GetHealthStatusAsync();
    return Results.Ok(status);
});

app.UseAuthorization();
app.MapControllers();


app.Run();

// Mantém o console aberto em modo texto/sandbox para facilitar testes interativos
if (useDeveloperSandbox)
{
    Console.WriteLine("Modo texto ativo. Digite 'exit' para sair.");
    while (Console.ReadLine() != "exit") { }
}

static IAsyncPolicy<HttpResponseMessage> BuildTransientHttpPolicy(int maxRetryAttempts, int retryBaseDelayMs)
{
    var attempts = Math.Max(0, maxRetryAttempts);
    var baseDelay = Math.Max(1, retryBaseDelayMs);

    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(attempts, retryAttempt => TimeSpan.FromMilliseconds(baseDelay * retryAttempt));
}
