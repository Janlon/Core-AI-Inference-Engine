using System.Text.Json.Serialization;

namespace AIPort.Adapter.Orchestrator.Domain.Models;

public sealed record InferenceRequestDto
{
    [JsonPropertyName("texto")]
    public required string Texto { get; init; }

    [JsonPropertyName("tenantType")]
    public required string TenantType { get; init; }

    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }

    [JsonPropertyName("metadata")]
    public IDictionary<string, string>? Metadata { get; init; }
}
