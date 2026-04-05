using AIPort.Adapter.Orchestrator.Config;
using Microsoft.Extensions.Configuration;

namespace AIPort.Adapter.Orchestrator.Tests.Configuration;

public class RuntimeInputSettingsBindingTests
{
    [Fact]
    public void AppSettingsJson_BindRuntimeInputOptions_CarregaValoresEsperados()
    {
        var configuration = BuildConfigurationFromProjectAppSettings();
        var options = new RuntimeInputOptions();

        configuration.Bind(options);

        Assert.Equal(InputSourceMode.WindowsVoice, options.InputSourceMode);
        Assert.Equal(200, options.DeveloperSandbox.TenantPid);
        Assert.True(options.DeveloperSandbox.DisableWebhookCalls);
        Assert.Equal("DEV-LOCAL", options.DeveloperSandbox.CallerId);
        Assert.Equal("SystemSpeech", options.DeveloperSandbox.WindowsVoice.Provider);
        Assert.Equal("pt-BR", options.DeveloperSandbox.WindowsVoice.Language);
    }

    private static IConfigurationRoot BuildConfigurationFromProjectAppSettings()
    {
        return new ConfigurationBuilder()
            .AddJsonFile(GetProjectAppSettingsPath(), optional: false, reloadOnChange: false)
            .Build();
    }

    private static string GetProjectAppSettingsPath()
    {
        var projectRoot = FindRepositoryRoot();
        var appSettingsPath = Path.Combine(projectRoot, "AIPort.Adapter.Orchestrator", "appsettings.json");

        Assert.True(File.Exists(appSettingsPath), $"Arquivo appsettings.json não encontrado em: {appSettingsPath}");
        return appSettingsPath;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var solutionPath = Path.Combine(directory.FullName, "Core AI Inference Engine.slnx");
            if (File.Exists(solutionPath))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Não foi possível localizar a raiz do repositório a partir do diretório de execução dos testes.");
    }
}