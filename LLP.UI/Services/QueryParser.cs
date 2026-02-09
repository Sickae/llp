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

        var orGroups = new List<List<IQuery>>();
        var currentAndGroup = new List<IQuery>();
        orGroups.Add(currentAndGroup);
        
        for (int i = 0; i < terms.Count; i++)
        {
            var term = terms[i];
            
            if (term.Equals("OR", StringComparison.OrdinalIgnoreCase))
            {
                currentAndGroup = new List<IQuery>();
                orGroups.Add(currentAndGroup);
                continue;
            }

            if (term.Equals("AND", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (term.Equals("NOT", StringComparison.OrdinalIgnoreCase) && i + 1 < terms.Count)
            {
                var nextTerm = terms[++i];
                currentAndGroup.Add(new NotQuery(ParseTerm(nextTerm)));
            }
            else
            {
                currentAndGroup.Add(ParseTerm(term));
            }
        }

        var andQueries = orGroups
            .Where(g => g.Count > 0)
            .Select(g => g.Count == 1 ? g[0] : new AndQuery(g))
            .ToList();

        if (andQueries.Count == 0) return new FullTextQuery(string.Empty);
        if (andQueries.Count == 1) return andQueries[0];
        
        return new OrQuery(andQueries);
    }

    private static IQuery ParseTerm(string term)
    {
        if (term.Contains(':'))
        {
            var parts = term.Split(':', 2);
            return new FieldQuery(parts[0], parts[1]);
        }
        return new FullTextQuery(term);
    }
}
