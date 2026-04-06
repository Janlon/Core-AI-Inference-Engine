namespace AIPort.Adapter.Orchestrator.Config;

public sealed class FallbackRoutingOptions
{
    public const string SectionName = "FallbackRouting";

    public string DatabaseOfflineExtension { get; set; } = "central";
}