using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using LLP.UI.Models;
using LLP.UI.Services;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace LLP.UI.ViewModels;

public class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly LogFileReader _logReader = new();
    private string _filePath = string.Empty;
    private string _searchText = string.Empty;
    private double _loadingProgress;
    private bool _isLoading;
    private int _lineCount;
    private ObservableCollection<FieldInfoViewModel> _fields = new();

    public MainViewModel()
    {
        OpenFileCommand = new RelayCommand(async _ => await OpenFileAsync());
        SearchCommand = new RelayCommand(_ => Search());
        ApplyFilterCommand = new RelayCommand(p => ApplyFilter(p as string));
        LogLines = new VirtualizingCollection(_logReader);
    }

    public ObservableCollection<FieldInfoViewModel> Fields => _fields;

    public ICommand ApplyFilterCommand { get; }

    public string FilePath
    {
        get => _filePath;
        set { _filePath = value; OnPropertyChanged(); }
    }

    public double LoadingProgress
    {
        get => _loadingProgress;
        set { _loadingProgress = value; OnPropertyChanged(); }
    }

    public string SearchText
    {
        get => _searchText;
        set { _searchText = value; OnPropertyChanged(); }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set { _isLoading = value; OnPropertyChanged(); }
    }

    public int LineCount
    {
        get => _lineCount;
        set { _lineCount = value; OnPropertyChanged(); }
    }

    public VirtualizingCollection LogLines { get; }

    public ICommand OpenFileCommand { get; }
    public ICommand SearchCommand { get; }

    private void Search()
    {
        _logReader.Search(SearchText);
        LogLines.Refresh();
        LineCount = _logReader.LineCount;
    }

    private void ApplyFilter(string? filter)
    {
        if (string.IsNullOrEmpty(filter)) return;
        
        if (string.IsNullOrEmpty(SearchText))
            SearchText = filter;
        else
            SearchText += " " + filter;
        
        Search();
    }

    private async Task OpenFileAsync()
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "Log files (*.log;*.txt)|*.log;*.txt|JSON files (*.json)|*.json|All files (*.*)|*.*"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            FilePath = openFileDialog.FileName;
            IsLoading = true;
            LoadingProgress = 0;
            Fields.Clear();

            var progress = new Progress<double>(p => LoadingProgress = p);
            await _logReader.OpenFileAsync(FilePath, progress);

            ExtractFields();
            LineCount = _logReader.LineCount;
            LogLines.Refresh();
            IsLoading = false;
        }
    }

    private void ExtractFields()
    {
        var fieldNames = new HashSet<string>();
        for (int i = 0; i < Math.Min(1000, _logReader.LineCount); i++)
        {
            var entry = _logReader.GetEntry(i);
            if (entry.Level != null) fieldNames.Add("level");
            if (entry.Fields != null)
            {
                foreach (var field in entry.Fields.Keys)
                    fieldNames.Add(field);
            }
        }

        foreach (var name in fieldNames.OrderBy(n => n))
        {
            Fields.Add(new FieldInfoViewModel { Name = name });
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void Dispose()
    {
        _logReader.Dispose();
    }
}

public class RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null) : ICommand
{
    public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => execute(parameter);
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}

public class VirtualizingCollection : System.Collections.IList, INotifyCollectionChanged, INotifyPropertyChanged
{
    private readonly LogFileReader _reader;

    public VirtualizingCollection(LogFileReader reader)
    {
        _reader = reader;
    }

    public void Refresh()
    {
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        OnPropertyChanged(nameof(Count));
    }

    public int Count => _reader.LineCount;

    public object? this[int index]
    {
        get => _reader.GetEntry(index);
        set => throw new NotSupportedException();
    }

    // IList implementation
    public bool IsFixedSize => false;
    public bool IsReadOnly => true;
    public int Add(object? value) => throw new NotSupportedException();
    public void Clear() => throw new NotSupportedException();
    public bool Contains(object? value) => false;
    public int IndexOf(object? value) => -1;
    public void Insert(int index, object? value) => throw new NotSupportedException();
    public void Remove(object? value) => throw new NotSupportedException();
    public void RemoveAt(int index) => throw new NotSupportedException();
    public void CopyTo(Array array, int index) { }
    public bool IsSynchronized => false;
    public object SyncRoot => this;
    public System.Collections.IEnumerator GetEnumerator()
    {
        for (int i = 0; i < Count; i++) yield return this[i];
    }

    public event NotifyCollectionChangedEventHandler? CollectionChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e) => CollectionChanged?.Invoke(this, e);
    protected virtual void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
