using LLP.UI.Models;

namespace LLP.UI.Services;

public interface ILogParser
{
    string Name { get; }
    LogEntry Parse(int index, string rawContent);
}
