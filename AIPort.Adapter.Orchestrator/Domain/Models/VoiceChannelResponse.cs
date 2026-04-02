namespace AIPort.Adapter.Orchestrator.Domain.Models;

public sealed record VoiceChannelResponse(int StatusCode, int Result, char? DigitPressed, string RawResponse);
