using AIPort.Adapter.Orchestrator.Config;
using Microsoft.Extensions.Configuration;

namespace AIPort.Adapter.Orchestrator.Tests.Configuration;

public class SpeechSettingsBindingTests
{
    [Fact]
    public void AppSettingsJson_BindSpeechOptions_CarregaValoresEsperados()
    {
        var configuration = BuildConfigurationFromProjectAppSettings();
        var options = new SpeechOptions();

        configuration.GetSection(SpeechOptions.SectionName).Bind(options);

        Assert.Equal(TtsProviderType.Google, options.TtsProvider);
        Assert.Equal("Festival", options.Asterisk.TtsApplication);
        Assert.Equal("/opt/aiport/credentials/google_cloud_auth.json", options.Google.CredentialsPath);
        Assert.Equal("/tmp", options.Google.TempDirectory);
        Assert.Equal("pt-BR", options.Google.TtsLanguageCode);
        Assert.Equal(8000, options.Google.TtsSampleRateHertz);
        Assert.Equal(1.0, options.Google.TtsSpeakingRate);
        Assert.Equal("pt-BR", options.Google.SttLanguageCode);
        Assert.Equal(8000, options.Google.SttSampleRateHertz);
        Assert.False(options.Google.UseStreamingStt);
    }

    [Fact]
    public void AppSettingsJson_BindSpeechOptions_NaoDependeDeEnvironmentVariable()
    {
        var originalProvider = Environment.GetEnvironmentVariable("AIPORT_TTS_PROVIDER");
        var originalCredentials = Environment.GetEnvironmentVariable("AIPORT_GOOGLE_CREDENTIALS_PATH");

        try
        {
            Environment.SetEnvironmentVariable("AIPORT_TTS_PROVIDER", "Asterisk");
            Environment.SetEnvironmentVariable("AIPORT_GOOGLE_CREDENTIALS_PATH", "C:/fake/from-env.json");

            var configuration = BuildConfigurationFromProjectAppSettings();
            var options = new SpeechOptions();
            configuration.GetSection(SpeechOptions.SectionName).Bind(options);

            Assert.Equal(TtsProviderType.Google, options.TtsProvider);
            Assert.Equal("/opt/aiport/credentials/google_cloud_auth.json", options.Google.CredentialsPath);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AIPORT_TTS_PROVIDER", originalProvider);
            Environment.SetEnvironmentVariable("AIPORT_GOOGLE_CREDENTIALS_PATH", originalCredentials);
        }
    }

    [Fact]
    public void AppSettingsJson_ComOrchestratorEnvironmentOverrides_AmbienteSobrescreveSettings()
    {
        var originalProvider = Environment.GetEnvironmentVariable("AIPORT_TTS_PROVIDER");
        var originalCredentials = Environment.GetEnvironmentVariable("AIPORT_GOOGLE_CREDENTIALS_PATH");

        try
        {
            Environment.SetEnvironmentVariable("AIPORT_TTS_PROVIDER", "Asterisk");
            Environment.SetEnvironmentVariable("AIPORT_GOOGLE_CREDENTIALS_PATH", "C:/fake/from-env.json");

            var builder = new ConfigurationManager();
            builder.AddJsonFile(GetProjectAppSettingsPath(), optional: false, reloadOnChange: false);
            OrchestratorEnvironmentOverrides.Apply(builder);

            var options = new SpeechOptions();
            builder.GetSection(SpeechOptions.SectionName).Bind(options);

            Assert.Equal(TtsProviderType.Asterisk, options.TtsProvider);
            Assert.Equal("C:/fake/from-env.json", options.Google.CredentialsPath);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AIPORT_TTS_PROVIDER", originalProvider);
            Environment.SetEnvironmentVariable("AIPORT_GOOGLE_CREDENTIALS_PATH", originalCredentials);
        }
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
