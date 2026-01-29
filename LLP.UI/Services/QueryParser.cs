using LLP.UI.Models;
using System.Text.RegularExpressions;

namespace LLP.UI.Services;

public interface IQuery
{
    bool IsMatch(LogEntry entry);
}

public class FullTextQuery(string searchText) : IQuery
{
    public string SearchText => searchText;

    public bool IsMatch(LogEntry entry)
    {
        if (string.IsNullOrEmpty(searchText)) return true;
        return entry.RawContent.Contains(searchText, StringComparison.OrdinalIgnoreCase);
    }
}

public class FieldQuery(string field, string value, bool exact = false) : IQuery
{
    public bool IsMatch(LogEntry entry)
    {
        string? entryValue = field.ToLowerInvariant() switch
        {
            "level" => entry.Level,
            "message" => entry.Message,
            "timestamp" => entry.Timestamp?.ToString("o"),
            _ => entry.Fields?.TryGetValue(field, out var v) == true ? v : null
        };

        if (entryValue == null) return false;

        if (value.StartsWith(">") || value.StartsWith("<"))
        {
            return Compare(entryValue, value);
        }

        if (exact)
            return string.Equals(entryValue, value, StringComparison.OrdinalIgnoreCase);
        
        return entryValue.Contains(value, StringComparison.OrdinalIgnoreCase);
    }

    private bool Compare(string entryValue, string queryValue)
    {
        bool greater = queryValue.StartsWith(">");
        string valPart = queryValue.TrimStart('>', '<', '=');
        
        if (DateTime.TryParse(entryValue, out var entryDt) && DateTime.TryParse(valPart, out var queryDt))
        {
            return greater ? entryDt > queryDt : entryDt < queryDt;
        }

        if (double.TryParse(entryValue, out var entryNum) && double.TryParse(valPart, out var queryNum))
        {
            return greater ? entryNum > queryNum : entryNum < queryNum;
        }

        return false;
    }
}

public class AndQuery(IEnumerable<IQuery> queries) : IQuery
{
    public bool IsMatch(LogEntry entry) => queries.All(q => q.IsMatch(entry));
}

public class OrQuery(IEnumerable<IQuery> queries) : IQuery
{
    public bool IsMatch(LogEntry entry) => queries.Any(q => q.IsMatch(entry));
}

public class NotQuery(IQuery query) : IQuery
{
    public bool IsMatch(LogEntry entry) => !query.IsMatch(entry);
}

public static class QueryParser
{
    // Simplified parser: "field:value", "value", "AND", "OR", "NOT"
    // For now, let's handle simple space-separated terms as AND
    // and field:value syntax.
    public static IQuery Parse(string queryText)
    {
        if (string.IsNullOrWhiteSpace(queryText))
            return new FullTextQuery(string.Empty);

        var terms = Regex.Matches(queryText, @"([^\s""]+|""[^""]*"")")
                         .Select(m => m.Value.Trim('"'))
                         .ToList();

        var queries = new List<IQuery>();
        
        // This is a VERY basic parser. A real one would need to handle precedence and nesting.
        // For Milestone 3, we'll start with something functional but simple.
        
        for (int i = 0; i < terms.Count; i++)
        {
            var term = terms[i];
            
            if (term.Contains(':'))
            {
                var parts = term.Split(':', 2);
                queries.Add(new FieldQuery(parts[0], parts[1]));
            }
            else if (term.Equals("AND", StringComparison.OrdinalIgnoreCase))
            {
                // Implicit in our current simple list processing
                continue;
            }
            else if (term.Equals("NOT", StringComparison.OrdinalIgnoreCase) && i + 1 < terms.Count)
            {
                var nextTerm = terms[++i];
                if (nextTerm.Contains(':'))
                {
                    var parts = nextTerm.Split(':', 2);
                    queries.Add(new NotQuery(new FieldQuery(parts[0], parts[1])));
                }
                else
                {
                    queries.Add(new NotQuery(new FullTextQuery(nextTerm)));
                }
            }
            else
            {
                queries.Add(new FullTextQuery(term));
            }
        }

        if (queries.Count == 0) return new FullTextQuery(string.Empty);
        if (queries.Count == 1) return queries[0];
        
        return new AndQuery(queries);
    }
}
