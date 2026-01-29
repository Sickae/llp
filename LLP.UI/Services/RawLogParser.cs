using LLP.UI.Models;

namespace LLP.UI.Services;

public class RawLogParser : ILogParser
{
    public string Name => "Raw";

    public LogEntry Parse(int index, string rawContent)
    {
        return new LogEntry(index, rawContent, Message: rawContent);
    }
}
