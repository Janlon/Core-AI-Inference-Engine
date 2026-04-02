using AIPort.Adapter.Orchestrator.Integrations.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AIPort.Adapter.Orchestrator.Controllers;

[ApiController]
[Route("api/speech")]
public sealed class SpeechController : ControllerBase
{
    private readonly ITextToSpeechService _tts;

    public SpeechController(ITextToSpeechService tts)
    {
        _tts = tts;
    }

    [HttpPost("tts/wav")]
    public Task<IActionResult> DownloadWavAsync([FromBody] TextToSpeechRequest request, CancellationToken ct) =>
        DownloadAsync(request, "wav", ct);

    [HttpPost("tts/mp3")]
    public Task<IActionResult> DownloadMp3Async([FromBody] TextToSpeechRequest request, CancellationToken ct) =>
        DownloadAsync(request, "mp3", ct);

    [HttpPost("tts/download")]
    public Task<IActionResult> DownloadByFormatAsync([FromBody] TextToSpeechRequest request, [FromQuery] string format = "wav", CancellationToken ct = default) =>
        DownloadAsync(request, format, ct);

    private async Task<IActionResult> DownloadAsync(TextToSpeechRequest request, string format, CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Text))
            return BadRequest("Informe o campo 'text' com conteúdo para sintetizar.");

        try
        {
            var result = await _tts.SynthesizeDownloadAsync(request.Text, format, ct);
            var fileName = BuildFileName(request.FileName, result.FileExtension);
            return File(result.AudioBytes, result.ContentType, fileName);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (FileNotFoundException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }

    private static string BuildFileName(string? requestedFileName, string extension)
    {
        var baseName = string.IsNullOrWhiteSpace(requestedFileName)
            ? $"tts-{DateTime.UtcNow:yyyyMMdd-HHmmss}"
            : requestedFileName.Trim();

        foreach (var invalid in Path.GetInvalidFileNameChars())
            baseName = baseName.Replace(invalid, '_');

        return baseName + "." + extension;
    }

    public sealed class TextToSpeechRequest
    {
        public string Text { get; set; } = string.Empty;
        public string? FileName { get; set; }
    }
}
