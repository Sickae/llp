using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;
using LLP.UI.Models;

namespace LLP.UI.Services;

public class LogFileReader : IDisposable
{
    private MemoryMappedFile? _mmf;
    private long _fileLength;
    private readonly List<long> _lineOffsets = new();
    private readonly List<int> _filteredIndices = new();
    private string _currentFilter = string.Empty;
    private IQuery _query = new FullTextQuery(string.Empty);
    private ILogParser _parser = new RawLogParser();
    private readonly IndexService _indexService = new();
    private bool _isIndexing = false;
    private Task? _indexingTask;
    private FileSystemWatcher? _watcher;
    private bool _isTailEnabled;
    private string? _filePath;
    private bool _isDescending;

    public event Action? FileUpdated;

    public bool IsIndexing => _isIndexing;

    public bool IsDescending
    {
        get => _isDescending;
        set => _isDescending = value;
    }

    public void SetTailEnabled(bool enabled)
    {
        _isTailEnabled = enabled;
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = enabled;
        }
    }

    public async Task OpenFileAsync(string filePath, IProgress<double>? progress = null)
    {
        Dispose();
        _filePath = filePath;
        _currentFilter = string.Empty;
        _query = new FullTextQuery(string.Empty);
        _filteredIndices.Clear();

        var fileInfo = new FileInfo(filePath);
        _fileLength = fileInfo.Length;
        
        // Use a FileStream with FileShare.ReadWrite to allow opening files that are being written to by other processes
        using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            _mmf = MemoryMappedFile.CreateFromFile(fs, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, false);
        }

        await Task.Run(() => ScanLineOffsets(filePath, progress));

        // Auto-detect format
        var sampleLines = new List<string>();
        for (int i = 0; i < Math.Min(10, _lineOffsets.Count); i++)
        {
            sampleLines.Add(GetRawLine(i));
        }
        _parser = LogFormatDetector.Detect(sampleLines);

        // Start background indexing
        _indexService.Initialize(filePath);
        _indexingTask = Task.Run(() => BackgroundIndex());

        SetupWatcher(filePath);
    }

    private void SetupWatcher(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        var fileName = Path.GetFileName(filePath);
        if (directory == null) return;

        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
        }

        _watcher = new FileSystemWatcher(directory, fileName);
        _watcher.NotifyFilter = NotifyFilters.Size | NotifyFilters.LastWrite;
        _watcher.Changed += OnFileChanged;
        _watcher.EnableRaisingEvents = _isTailEnabled;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (!_isTailEnabled || _filePath == null) return;

        // Use lock or similar to prevent concurrent executions of OnFileChanged
        lock (this)
        {
            // Small delay to allow file to be unlocked by the writer
            // Reduced delay for better responsiveness, but kept some for safety
            Thread.Sleep(50);

            long newLength = 0;
            try
            {
                var fi = new FileInfo(_filePath);
                fi.Refresh(); // Force refresh of file info
                newLength = fi.Length;
            }
            catch (IOException)
            {
                return;
            }

            if (newLength <= _fileLength) return;

            try
            {
                using var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                fs.Seek(_fileLength, SeekOrigin.Begin);
                
                long currentPos = _fileLength;
                int b;
                var newLines = new List<(string Content, int Index)>();

                var buffer = new List<byte>();
                while ((b = fs.ReadByte()) != -1)
                {
                    currentPos++;
                    buffer.Add((byte)b);
                    if (b == '\n')
                    {
                        int index = _lineOffsets.Count;
                        _lineOffsets.Add(currentPos);
                        
                        var lineBytes = buffer.ToArray();
                        var line = Encoding.UTF8.GetString(lineBytes).TrimEnd('\r', '\n');
                        buffer.Clear();
                        
                        newLines.Add((line, index));
                        var entry = _parser.Parse(index, line);

                        if (!string.IsNullOrEmpty(_currentFilter))
                        {
                            if (_query.IsMatch(entry))
                            {
                                _filteredIndices.Add(index);
                            }
                        }
                    }
                }
                
                if (buffer.Count > 0)
                {
                     int index = _lineOffsets.Count;
                     _lineOffsets.Add(currentPos);
                     var line = Encoding.UTF8.GetString(buffer.ToArray());
                     newLines.Add((line, index));
                     var entry = _parser.Parse(index, line);
                     if (!string.IsNullOrEmpty(_currentFilter) && _query.IsMatch(entry))
                     {
                         _filteredIndices.Add(index);
                     }
                }
                
                _fileLength = currentPos;
                if (newLines.Count > 0)
                {
                    _indexService.AddEntries(newLines);
                    FileUpdated?.Invoke();
                }
            }
            catch (Exception)
            {
                // File might still be locked
            }
        }
    }

    private void BackgroundIndex()
    {
        _isIndexing = true;
        try
        {
            const int batchSize = 10000;
            for (int i = 0; i < _lineOffsets.Count; i += batchSize)
            {
                var batch = new List<(string Content, int LineIndex)>();
                for (int j = i; j < Math.Min(i + batchSize, _lineOffsets.Count); j++)
                {
                    batch.Add((GetRawLine(j), j));
                }
                _indexService.AddEntries(batch);
            }
        }
        finally
        {
            _isIndexing = false;
        }
    }

    private void ScanLineOffsets(string filePath, IProgress<double>? progress)
    {
        _lineOffsets.Clear();
        _lineOffsets.Add(0); // First line starts at offset 0

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 64 * 1024);
        long offset = 0;
        int b;
        long lastReportedOffset = 0;
        const long reportThreshold = 10 * 1024 * 1024; // Report every 10MB

        while ((b = fs.ReadByte()) != -1)
        {
            offset++;
            if (b == '\n')
            {
                _lineOffsets.Add(offset);
            }

            if (progress != null && offset - lastReportedOffset > reportThreshold)
            {
                progress.Report((double)offset / _fileLength * 100);
                lastReportedOffset = offset;
            }
        }

        // Remove the last offset if it points to the end of the file (trailing newline)
        if (_lineOffsets.Count > 1 && _lineOffsets[^1] == _fileLength)
        {
            _lineOffsets.RemoveAt(_lineOffsets.Count - 1);
        }

        progress?.Report(100);
    }

    public int LineCount => string.IsNullOrEmpty(_currentFilter) ? _lineOffsets.Count : _filteredIndices.Count;

    private LogEntry GetEntryInternal(int actualIndex)
    {
        string raw = GetRawLine(actualIndex);
        var entry = _parser.Parse(actualIndex, raw);
        // System.Console.WriteLine($"[DEBUG_LOG] GetEntryInternal({actualIndex}) raw: '{raw}'");
        // System.Console.WriteLine($"[DEBUG_LOG] GetEntryInternal({actualIndex}) message: '{entry.Message}'");
        return entry;
    }

    public LogEntry GetEntry(int index)
    {
        int effectiveIndex = _isDescending ? (LineCount - 1 - index) : index;
        int actualIndex = string.IsNullOrEmpty(_currentFilter) ? effectiveIndex : _filteredIndices[effectiveIndex];
        return GetEntryInternal(actualIndex);
    }

    public void Search(string searchText)
    {
        _currentFilter = searchText;
        _query = QueryParser.Parse(searchText);
        _filteredIndices.Clear();

        if (string.IsNullOrEmpty(searchText))
            return;

        // If it's a simple full-text search and we have an index, use it
        if (_query is FullTextQuery fullTextQuery && !_isIndexing)
        {
            var results = _indexService.Search(fullTextQuery.SearchText);
            if (results.Count > 0)
            {
                _filteredIndices.AddRange(results);
                return;
            }
            
            // If the query is empty, we don't need to do anything else (everything is visible)
            if (string.IsNullOrEmpty(fullTextQuery.SearchText))
                return;
        }

        // Fallback to linear scan for complex queries or if index is not ready/nothing found
        for (int i = 0; i < _lineOffsets.Count; i++)
        {
            var entry = GetEntryInternal(i);
            if (_query.IsMatch(entry))
            {
                _filteredIndices.Add(i);
            }
        }
    }

    public string GetRawLine(int index)
    {
        if (_mmf == null || index < 0 || index >= _lineOffsets.Count)
            return string.Empty;

        long start = _lineOffsets[index];
        long end = (index + 1 < _lineOffsets.Count) ? _lineOffsets[index + 1] : _fileLength;

        // If the line was just appended, it might be beyond the MMF's initial size.
        // MemoryMappedFile.CreateFromFile(fs, ...) with fs as FileStream 
        // does not automatically expand the mapping when the file grows.
        
        int length = (int)(end - start);
        if (length <= 0) return string.Empty;

        // Try to use MMF first (best performance for historical logs)
        try
        {
            // We only use MMF if the range is within what we initially mapped.
            // In a real app, we might want to re-map occasionally.
            using var accessor = _mmf.CreateViewAccessor(start, length, MemoryMappedFileAccess.Read);
            byte[] buffer = new byte[length];
            accessor.ReadArray(0, buffer, 0, length);
            return Encoding.UTF8.GetString(buffer).TrimEnd('\r', '\n');
        }
        catch
        {
            // Fallback to direct file read for appended lines (Tail -f) 
            // or if MMF accessor creation fails (e.g. beyond initial size)
            if (_filePath != null)
            {
                try
                {
                    using var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    fs.Seek(start, SeekOrigin.Begin);
                    byte[] buffer = new byte[length];
                    fs.Read(buffer, 0, length);
                    return Encoding.UTF8.GetString(buffer).TrimEnd('\r', '\n');
                }
                catch { }
            }
        }

        return string.Empty;
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _watcher = null;
        _mmf?.Dispose();
        _mmf = null;
        _lineOffsets.Clear();
        _indexService.Dispose();
    }
}
