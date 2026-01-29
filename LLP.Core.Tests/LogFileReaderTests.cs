using LLP.UI.Models;
using LLP.UI.Services;
using System.IO;
using System.Text.Json;

namespace LLP.UI.Tests;

public class IndexServiceTests : IDisposable
{
    private readonly string _tempDb;
    private readonly IndexService _indexService;

    public IndexServiceTests()
    {
        _tempDb = Path.GetTempFileName() + ".db";
        _indexService = new IndexService();
        // Initialize expects a log file path and appends .idx.db
        // To be safe, we'll use a path that will result in our tempDb
        _indexService.Initialize(_tempDb.Replace(".idx.db", ""));
    }

    [Fact]
    public void Search_ReturnsCorrectIndices()
    {
        var entries = new List<(string Content, int LineIndex)>
        {
            ("Error: Something failed", 0),
            ("Info: Application started", 1),
            ("Warning: Low memory", 2),
            ("Error: Another failure", 3)
        };

        _indexService.AddEntries(entries);

        var results = _indexService.Search("Error");
        Assert.Equal(2, results.Count);
        Assert.Contains(0, results);
        Assert.Contains(3, results);
    }

    public void Dispose()
    {
        _indexService.Dispose();
        if (File.Exists(_tempDb)) File.Delete(_tempDb);
        string idxDb = _tempDb + ".idx.db";
        if (File.Exists(idxDb)) File.Delete(idxDb);
    }
}

public class LogFileReaderTests
{
    [Fact]
    public async Task Search_UsesIndexWhenReady()
    {
        string filePath = Path.GetTempFileName();
        string content = "Error 1\nInfo 1\nError 2\n";
        File.WriteAllText(filePath, content);
        using var reader = new LogFileReader();

        try
        {
            await reader.OpenFileAsync(filePath);
            
            // Wait for indexing to complete
            int timeout = 0;
            while (reader.IsIndexing && timeout < 50) 
            {
                await Task.Delay(100);
                timeout++;
            }

            // Act
            reader.Search("Error");

            // Assert
            Assert.Equal(2, reader.LineCount);
            Assert.Equal("Error 1", reader.GetEntry(0).RawContent);
            Assert.Equal("Error 2", reader.GetEntry(1).RawContent);
        }
        finally
        {
            reader.Dispose();
            if (File.Exists(filePath)) File.Delete(filePath);
            if (File.Exists(filePath + ".idx.db")) File.Delete(filePath + ".idx.db");
        }
    }

    [Fact]
    public async Task QueryParser_ParsesFieldQuery()
    {
        var query = QueryParser.Parse("level:ERROR");
        var entry = new LogEntry(0, "raw", Level: "ERROR");
        Assert.True(query.IsMatch(entry));
        
        var entry2 = new LogEntry(0, "raw", Level: "INFO");
        Assert.False(query.IsMatch(entry2));
    }

    [Fact]
    public async Task QueryParser_ParsesNotQuery()
    {
        var query = QueryParser.Parse("NOT level:ERROR");
        var entry = new LogEntry(0, "raw", Level: "INFO");
        Assert.True(query.IsMatch(entry));
        
        var entry2 = new LogEntry(0, "raw", Level: "ERROR");
        Assert.False(query.IsMatch(entry2));
    }

    [Fact]
    public async Task QueryParser_ParsesComparisonQuery()
    {
        var query = QueryParser.Parse("timestamp:>2024-01-01");
        var entry = new LogEntry(0, "raw", Timestamp: new DateTime(2024, 02, 01));
        Assert.True(query.IsMatch(entry));
        
        var entry2 = new LogEntry(0, "raw", Timestamp: new DateTime(2023, 12, 01));
        Assert.False(query.IsMatch(entry2));
    }

    [Fact]
    public async Task OpenFileAsync_ScansLinesCorrectly()
    {
        // Arrange
        string filePath = Path.GetTempFileName();
        string content = "Line 1\nLine 2\r\nLine 3\n";
        File.WriteAllText(filePath, content);
        using var reader = new LogFileReader();

        try
        {
            // Act
            await reader.OpenFileAsync(filePath);

            // Assert
            Assert.Equal(3, reader.LineCount);
            Assert.Equal("Line 1", reader.GetEntry(0).RawContent);
            Assert.Equal("Line 2", reader.GetEntry(1).RawContent);
            Assert.Equal("Line 3", reader.GetEntry(2).RawContent);
        }
        finally
        {
            reader.Dispose();
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }

    [Fact]
    public async Task Search_FiltersLinesCorrectly()
    {
        // Arrange
        string filePath = Path.GetTempFileName();
        string content = "Error: something failed\nInfo: all good\nError: another failure\n";
        File.WriteAllText(filePath, content);
        using var reader = new LogFileReader();

        try
        {
            await reader.OpenFileAsync(filePath);

            // Act
            reader.Search("Error");

            // Assert
            Assert.Equal(2, reader.LineCount);
            Assert.Contains("Error", reader.GetEntry(0).RawContent);
            Assert.Contains("Error", reader.GetEntry(1).RawContent);

            // Reset search
            reader.Search("");
            Assert.Equal(3, reader.LineCount);
        }
        finally
        {
            reader.Dispose();
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }

    [Fact]
    public async Task JsonLogParser_ParsesCorrectly()
    {
        // Arrange
        var parser = new JsonLogParser();
        string jsonLine = "{\"timestamp\":\"2024-01-29T15:20:00Z\", \"level\":\"ERROR\", \"message\":\"Critical error\", \"user\":\"admin\"}";

        // Act
        var entry = parser.Parse(0, jsonLine);

        // Assert
        Assert.Equal("ERROR", entry.Level);
        Assert.Equal("Critical error", entry.Message);
        Assert.Equal(DateTime.Parse("2024-01-29T15:20:00Z").ToUniversalTime(), entry.Timestamp?.ToUniversalTime());
        Assert.Equal("admin", entry.Fields?["user"]);
    }

    [Fact]
    public async Task RegexLogParser_ParsesCorrectly()
    {
        // Arrange
        var pattern = @"^\[(?<timestamp>.*)\]\s+(?<level>\w+):\s+(?<message>.*)$";
        var parser = new RegexLogParser(pattern);
        string logLine = "[2024-01-29 15:20:00] INFO: Application started";

        // Act
        var entry = parser.Parse(0, logLine);

        // Assert
        Assert.Equal("INFO", entry.Level);
        Assert.Equal("Application started", entry.Message);
        Assert.Equal(new DateTime(2024, 01, 29, 15, 20, 00), entry.Timestamp);
    }

    [Fact]
    public async Task OpenFileAsync_CanOpenFileWhileUsedByAnotherProcess()
    {
        // Arrange
        string filePath = Path.GetTempFileName();
        string content = "Log entry 1\nLog entry 2";
        File.WriteAllText(filePath, content);
        
        // Open the file with another process/stream and keep it open
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        
        using var reader = new LogFileReader();

        try
        {
            // Act & Assert
            // This should not throw IOException
            await reader.OpenFileAsync(filePath);

            Assert.Equal(2, reader.LineCount);
            Assert.Equal("Log entry 1", reader.GetEntry(0).RawContent);
            Assert.Equal("Log entry 2", reader.GetEntry(1).RawContent);
        }
        finally
        {
            reader.Dispose();
            fs.Close();
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }
}
