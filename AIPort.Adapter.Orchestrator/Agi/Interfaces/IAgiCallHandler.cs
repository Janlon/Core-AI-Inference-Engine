using AIPort.Adapter.Orchestrator.Agi.Models;

namespace AIPort.Adapter.Orchestrator.Agi.Interfaces;

public interface IAgiCallHandler
{
    Task HandleAsync(FastAgiRequest request, IAgiChannel channel, CancellationToken ct = default);
}
