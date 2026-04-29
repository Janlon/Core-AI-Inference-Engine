using AIPort.Intelligence.Service.Domain.Models;

namespace AIPort.Intelligence.Service.Services.Interfaces;

public interface ILlmHealthService
{
    Task<LlmHealthReport> GetHealthAsync(CancellationToken ct = default);
}
