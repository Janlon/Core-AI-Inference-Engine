namespace AIPort.Adapter.Orchestrator.Config;

public sealed class ServerOptions
{
    public const string SectionName = "Server";

    public string Urls { get; set; } = "http://0.0.0.0:5000";
    public int RequestHeadersTimeoutSeconds { get; set; } = 60;
    public int KeepAliveTimeoutSeconds { get; set; } = 120;
}