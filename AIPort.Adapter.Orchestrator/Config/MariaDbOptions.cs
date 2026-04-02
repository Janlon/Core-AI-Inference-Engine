namespace AIPort.Adapter.Orchestrator.Config;

public sealed class MariaDbOptions
{
    public const string SectionName = "MariaDb";

    public required string ConnectionString { get; set; }
}
