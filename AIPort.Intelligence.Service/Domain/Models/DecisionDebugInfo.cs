namespace AIPort.Intelligence.Service.Domain.Models;

public sealed record DecisionDebugInfo
{
    public IReadOnlyList<RegexDebugMatch> RegexMatches { get; init; } = Array.Empty<RegexDebugMatch>();
}

public sealed record RegexDebugMatch
{
    public required string Rule { get; init; }
    public string? Value { get; init; }
}