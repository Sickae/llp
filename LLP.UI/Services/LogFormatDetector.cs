using LLP.UI.Models;
using System.Text.Json;

namespace LLP.UI.Services;

public static class LogFormatDetector
{
    public static ILogParser Detect(IEnumerable<string> sampleLines)
    {
        var sample = sampleLines.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));
        if (sample == null) return new RawLogParser();

        // Check if JSON
        if (sample.TrimStart().StartsWith("{"))
        {
            try
            {
                JsonDocument.Parse(sample);
                return new JsonLogParser();
            }
            catch { }
        }

        // Check for common Regex patterns
        // 1. [2024-01-29 15:20:00] INFO: Message
        var regexPatterns = new[]
        {
            new { Name = "Standard", Pattern = @"^\[?(?<timestamp>\d{4}-\d{2}-\d{2}\s\d{2}:\d{2}:\d{2})\]?\s+(?<level>\w+):?\s+(?<message>.*)$", Format = "yyyy-MM-dd HH:mm:ss" },
            new { Name = "ISO", Pattern = @"^(?<timestamp>\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.?\d*)Z?\s+(?<level>\w+)\s+(?<message>.*)$", Format = "yyyy-MM-ddTHH:mm:ss" }
        };

        foreach (var p in regexPatterns)
        {
            var regex = new System.Text.RegularExpressions.Regex(p.Pattern);
            if (regex.IsMatch(sample))
            {
                return new RegexLogParser(p.Pattern, p.Format);
            }
        }

        return new RawLogParser();
    }
}
