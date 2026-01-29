using System.Text.Json;
using LLP.UI.Models;
using System.Globalization;

namespace LLP.UI.Services;

public class JsonLogParser : ILogParser
{
    public string Name => "JSON";

    public LogEntry Parse(int index, string rawContent)
    {
        try
        {
            var json = JsonDocument.Parse(rawContent);
            var root = json.RootElement;

            DateTime? timestamp = null;
            if (TryGetProperty(root, new[] { "timestamp", "time", "@timestamp", "Date" }, out var tsProp) &&
                DateTime.TryParse(tsProp.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt))
            {
                timestamp = dt;
            }

            string? level = null;
            if (TryGetProperty(root, new[] { "level", "severity", "Level" }, out var levelProp))
            {
                level = levelProp.GetString();
            }

            string? message = null;
            if (TryGetProperty(root, new[] { "message", "msg", "text", "Message" }, out var msgProp))
            {
                message = msgProp.GetString();
            }
            else
            {
                message = rawContent;
            }

            var fields = new Dictionary<string, string>();
            foreach (var property in root.EnumerateObject())
            {
                fields[property.Name] = property.Value.ToString() ?? "";
            }

            return new LogEntry(index, rawContent, timestamp, level, message, fields);
        }
        catch (JsonException)
        {
            return new LogEntry(index, rawContent, Message: rawContent);
        }
    }

    private bool TryGetProperty(JsonElement element, string[] names, out JsonElement property)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out property))
                return true;
        }
        property = default;
        return false;
    }
}
