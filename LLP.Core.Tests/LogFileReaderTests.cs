using LLP.UI.Models;
using LLP.UI.Services;
using System.IO;
using System.Text.Json;

namespace LLP.UI.Tests;

public class LogFileReaderTests
{
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
