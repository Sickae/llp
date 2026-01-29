using Microsoft.Data.Sqlite;
using LLP.UI.Models;
using System.IO;

namespace LLP.UI.Services;

public class IndexService : IDisposable
{
    private SqliteConnection? _connection;
    private SqliteTransaction? _currentTransaction;
    private string? _dbPath;

    public void Initialize(string logFilePath)
    {
        Dispose();
        _dbPath = logFilePath + ".idx.db";
        
        // If index exists, we might want to validate it or just reuse it.
        // For now, let's recreate it to ensure it's up to date.
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { }
        }

        _connection = new SqliteConnection($"Data Source={_dbPath}");
        _connection.Open();

        using var command = _connection.CreateCommand();
        command.CommandText = 
        @"
            CREATE VIRTUAL TABLE log_index USING fts5(content, line_index UNINDEXED);
            PRAGMA journal_mode = OFF;
            PRAGMA synchronous = OFF;
        ";
        command.ExecuteNonQuery();
    }

    public void AddEntries(IEnumerable<(string Content, int LineIndex)> entries)
    {
        if (_connection == null) return;

        _currentTransaction = _connection.BeginTransaction();
        try
        {
            using var command = _connection.CreateCommand();
            command.Transaction = _currentTransaction;
            command.CommandText = "INSERT INTO log_index (content, line_index) VALUES ($content, $line_index)";
            
            var contentParam = command.CreateParameter();
            contentParam.ParameterName = "$content";
            command.Parameters.Add(contentParam);

            var indexParam = command.CreateParameter();
            indexParam.ParameterName = "$line_index";
            command.Parameters.Add(indexParam);

            command.Prepare();

            foreach (var entry in entries)
            {
                contentParam.Value = entry.Content;
                indexParam.Value = entry.LineIndex;
                command.ExecuteNonQuery();
            }
            _currentTransaction.Commit();
        }
        finally
        {
            _currentTransaction.Dispose();
            _currentTransaction = null;
        }
    }

    public List<int> Search(string query)
    {
        var results = new List<int>();
        if (_connection == null || string.IsNullOrEmpty(query)) return results;

        // Escape double quotes in query to avoid SQL injection/syntax errors in FTS5
        string escapedQuery = query.Replace("\"", "\"\"");

        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT line_index FROM log_index WHERE content MATCH $query";
        command.Parameters.AddWithValue("$query", $"\"{escapedQuery}\"");

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(reader.GetInt32(0));
        }

        return results;
    }

    public void Dispose()
    {
        _currentTransaction?.Dispose();
        _currentTransaction = null;
        SqliteConnection.ClearAllPools();
        _connection?.Dispose();
        _connection = null;
        if (_dbPath != null && File.Exists(_dbPath))
        {
            // Optional: delete index on dispose if we don't want it to persist
            // For now, let's keep it for the session
        }
    }
}
