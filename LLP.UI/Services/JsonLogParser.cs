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
            FlattenElement(root, fields, "");

            return new LogEntry(index, rawContent, timestamp, level, message, fields);
        }
        catch (JsonException)
        {
            return new LogEntry(index, rawContent, Message: rawContent);
        }
    }

    private void FlattenElement(JsonElement element, Dictionary<string, string> fields, string prefix)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    string name = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";
                    FlattenElement(property.Value, fields, name);
                }
                break;
            case JsonValueKind.Array:
                int index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    string name = $"{prefix}[{index}]";
                    FlattenElement(item, fields, name);
                    index++;
                }
                break;
            default:
                if (!string.IsNullOrEmpty(prefix))
                {
                    fields[prefix] = element.ToString() ?? "";
                }
                break;
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
