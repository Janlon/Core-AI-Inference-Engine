namespace AIPort.Intelligence.Service.Domain.Options;

public sealed class ServerOptions
{
    public const string SectionName = "Server";

    public string Urls { get; set; } = "http://0.0.0.0:5005";
    public int RequestHeadersTimeoutSeconds { get; set; } = 600000;
    public int KeepAliveTimeoutSeconds { get; set; } = 1200000;
}