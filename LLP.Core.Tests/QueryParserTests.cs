using LLP.UI.Models;
using LLP.UI.Services;
using Xunit;

namespace LLP.Core.Tests;

public class QueryParserTests
{
    [Fact]
    public void SimpleFieldQuery_Works()
    {
        var query = QueryParser.Parse("level:Error");
        var entry = new LogEntry(1, "ERROR: something happened", null, "Error", "something happened");
        Assert.True(query.IsMatch(entry));
    }

    [Fact]
    public void OrQuery_ShouldWork()
    {
        var query = QueryParser.Parse("level:Error OR level:Info");
        var errorEntry = new LogEntry(1, "ERROR: something happened", null, "Error", "something happened");
        var infoEntry = new LogEntry(2, "INFO: something happened", null, "Info", "something happened");
        var warnEntry = new LogEntry(3, "WARN: something happened", null, "Warning", "something happened");

        Assert.True(query.IsMatch(errorEntry), "Should match Error entry");
        Assert.True(query.IsMatch(infoEntry), "Should match Info entry");
        Assert.False(query.IsMatch(warnEntry), "Should NOT match Warning entry");
    }

    [Fact]
    public void ComplexQuery_ShouldWork()
    {
        // (level:Error OR level:Warn) AND message:database
        // Our current parser is very simple and doesn't support parentheses yet, 
        // but it should at least handle multiple terms.
        // If we implement OR with lower precedence than AND, "level:Error OR level:Warn message:database"
        // might be interpreted as "level:Error OR (level:Warn AND database)"
        
        var query = QueryParser.Parse("level:Error OR level:Warn database");
        
        var errorEntry = new LogEntry(1, "ERROR: some error", null, "Error", "some error");
        var warnDbEntry = new LogEntry(2, "WARN: database error", null, "Warn", "database error");
        var warnOtherEntry = new LogEntry(3, "WARN: network error", null, "Warn", "network error");

        Assert.True(query.IsMatch(errorEntry), "Should match Error entry (OR first part)");
        Assert.True(query.IsMatch(warnDbEntry), "Should match Warn entry with database (OR second part, AND inside)");
        Assert.False(query.IsMatch(warnOtherEntry), "Should NOT match Warn entry without database (AND precedence test)");
    }

    [Fact]
    public void MultipleOr_ShouldWork()
    {
        var query = QueryParser.Parse("A OR B OR C");
        Assert.True(query.IsMatch(new LogEntry(1, "A")));
        Assert.True(query.IsMatch(new LogEntry(2, "B")));
        Assert.True(query.IsMatch(new LogEntry(3, "C")));
        Assert.False(query.IsMatch(new LogEntry(4, "D")));
    }

    [Fact]
    public void MixedAndOrNot_ShouldWork()
    {
        // Equivalent to: (level:Error AND NOT database) OR level:Info
        var query = QueryParser.Parse("level:Error NOT database OR level:Info");
        
        var errorEntry = new LogEntry(1, "ERROR: something", null, "Error", "something");
        var errorDbEntry = new LogEntry(2, "ERROR: database error", null, "Error", "database error");
        var infoEntry = new LogEntry(3, "INFO: something", null, "Info", "something");

        Assert.True(query.IsMatch(errorEntry), "Should match Error without database");
        Assert.False(query.IsMatch(errorDbEntry), "Should NOT match Error with database");
        Assert.True(query.IsMatch(infoEntry), "Should match Info");
    }
}
