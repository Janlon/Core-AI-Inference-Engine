using System.Globalization;
using System.Text.RegularExpressions;

namespace AIPort.Adapter.Orchestrator.Agi.Models;

public sealed record AgiResponse(int StatusCode, int Result, char? DigitPressed, string RawResponse)
{
    private static readonly Regex StatusRegex = new(@"^(\d{3})", RegexOptions.Compiled);
    private static readonly Regex ResultRegex = new(@"result=([-0-9]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static AgiResponse Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException("Resposta AGI vazia.");

        var statusMatch = StatusRegex.Match(raw);
        if (!statusMatch.Success || !int.TryParse(statusMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var statusCode))
            throw new InvalidOperationException($"Resposta AGI inválida (status ausente): '{raw}'");

        var result = 0;
        var resultMatch = ResultRegex.Match(raw);
        if (resultMatch.Success)
            _ = int.TryParse(resultMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

        char? digitPressed = null;
        if (result > 0 && result <= 255)
        {
            var candidate = Convert.ToChar(result);
            if (IsDtmfChar(candidate))
                digitPressed = candidate;
        }

        return new AgiResponse(statusCode, result, digitPressed, raw);
    }

    private static bool IsDtmfChar(char c)
        => "0123456789*#ABCDabcd".Contains(c, StringComparison.Ordinal);
}
