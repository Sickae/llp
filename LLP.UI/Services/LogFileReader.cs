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
    private bool _isIndexed = false;

    public bool IsIndexed => _isIndexed;

    public ILogParser Parser
    {
        get => _parser;
        set => _parser = value;
    }

    public async Task OpenFileAsync(string filePath, IProgress<double>? progress = null)
    {
        Dispose();
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
        return _parser.Parse(actualIndex, raw);
    }

    public LogEntry GetEntry(int index)
    {
        int actualIndex = string.IsNullOrEmpty(_currentFilter) ? index : _filteredIndices[index];
        return GetEntryInternal(actualIndex);
    }

    public void Search(string searchText)
    {
        _currentFilter = searchText;
        _query = QueryParser.Parse(searchText);
        _filteredIndices.Clear();

        if (string.IsNullOrEmpty(searchText))
            return;

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
        int length = (int)(end - start);

        if (length <= 0) return string.Empty;

        using var accessor = _mmf.CreateViewAccessor(start, length, MemoryMappedFileAccess.Read);
        byte[] buffer = new byte[length];
        accessor.ReadArray(0, buffer, 0, length);

        return Encoding.UTF8.GetString(buffer).TrimEnd('\r', '\n');
    }

    public void Dispose()
    {
        _mmf?.Dispose();
        _mmf = null;
        _lineOffsets.Clear();
    }
}
