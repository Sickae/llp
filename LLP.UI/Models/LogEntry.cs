using System.Collections.Generic;

namespace LLP.UI.Models;

public record LogEntry(
    int Index,
    string RawContent,
    DateTime? Timestamp = null,
    string? Level = null,
    string? Message = null,
    Dictionary<string, string>? Fields = null
);
