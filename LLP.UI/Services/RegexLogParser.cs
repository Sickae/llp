using System.Text.RegularExpressions;
using LLP.UI.Models;
using System.Globalization;

namespace LLP.UI.Services;

public class RegexLogParser : ILogParser
{
    private readonly Regex _regex;
    private readonly string _timestampFormat;

    public string Name => "Regex";

    public RegexLogParser(string pattern, string timestampFormat = "yyyy-MM-dd HH:mm:ss")
    {
        _regex = new Regex(pattern, RegexOptions.Compiled);
        _timestampFormat = timestampFormat;
    }

    public LogEntry Parse(int index, string rawContent)
    {
        var match = _regex.Match(rawContent);
        if (!match.Success)
        {
            return new LogEntry(index, rawContent, Message: rawContent);
        }

        DateTime? timestamp = null;
        if (match.Groups["timestamp"].Success && 
            DateTime.TryParseExact(match.Groups["timestamp"].Value, _timestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            timestamp = dt;
        }

        string? level = match.Groups["level"].Success ? match.Groups["level"].Value : null;
        string? message = match.Groups["message"].Success ? match.Groups["message"].Value : rawContent;

        var fields = new Dictionary<string, string>();
        foreach (var groupName in _regex.GetGroupNames())
        {
            if (groupName == "0" || groupName == "timestamp" || groupName == "level" || groupName == "message")
                continue;

            if (match.Groups[groupName].Success)
            {
                fields[groupName] = match.Groups[groupName].Value;
            }
        }

        return new LogEntry(index, rawContent, timestamp, level, message, fields.Count > 0 ? fields : null);
    }
}
