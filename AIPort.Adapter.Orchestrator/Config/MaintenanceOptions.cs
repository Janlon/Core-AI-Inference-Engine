namespace AIPort.Adapter.Orchestrator.Config;

public sealed class MaintenanceOptions
{
    public const string SectionName = "Maintenance";

    public bool EnableServiceControl { get; set; }
    public string? StartOrchestratorMonitorCommand { get; set; }
}