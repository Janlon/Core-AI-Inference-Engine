namespace AIPort.Adapter.Orchestrator.Config;

public sealed class AgiServerOptions
{
    public const string SectionName = "AgiServer";

    public bool Enabled { get; set; } = true;
    public string Host { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 4573;
    public int Backlog { get; set; } = 100;
}
