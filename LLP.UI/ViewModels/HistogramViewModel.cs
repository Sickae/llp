using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LLP.UI.ViewModels;

public class HistogramViewModel : INotifyPropertyChanged
{
    private ISeries[] _series = Array.Empty<ISeries>();
    private Axis[] _xAxes = Array.Empty<Axis>();
    private Axis[] _yAxes = Array.Empty<Axis>();

    public ISeries[] Series
    {
        get => _series;
        set { _series = value; OnPropertyChanged(); }
    }

    public Axis[] XAxes
    {
        get => _xAxes;
        set { _xAxes = value; OnPropertyChanged(); }
    }

    public Axis[] YAxes
    {
        get => _yAxes;
        set { _yAxes = value; OnPropertyChanged(); }
    }

    public void UpdateData(IEnumerable<DateTime> timestamps)
    {
        if (!timestamps.Any())
        {
            Series = Array.Empty<ISeries>();
            return;
        }

        var min = timestamps.Min();
        var max = timestamps.Max();
        var duration = max - min;

        // Determine bucket size based on duration
        TimeSpan bucketSize;
        if (duration.TotalMinutes < 1) bucketSize = TimeSpan.FromSeconds(1);
        else if (duration.TotalHours < 1) bucketSize = TimeSpan.FromMinutes(1);
        else if (duration.TotalDays < 1) bucketSize = TimeSpan.FromMinutes(10);
        else bucketSize = TimeSpan.FromHours(1);

        var buckets = timestamps
            .GroupBy(t => new DateTime((t.Ticks / bucketSize.Ticks) * bucketSize.Ticks))
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.Count());

        // Fill gaps
        var allBuckets = new List<double>();
        var labels = new List<string>();
        for (var current = min; current <= max; current += bucketSize)
        {
            var rounded = new DateTime((current.Ticks / bucketSize.Ticks) * bucketSize.Ticks);
            allBuckets.Add(buckets.TryGetValue(rounded, out var count) ? count : 0);
            labels.Add(rounded.ToString("HH:mm:ss"));
        }

        Series = new ISeries[]
        {
            new ColumnSeries<double>
            {
                Values = allBuckets,
                Fill = new SolidColorPaint(SKColors.CornflowerBlue)
            }
        };

        XAxes = new Axis[]
        {
            new Axis
            {
                Labels = labels,
                LabelsRotation = 45,
                SeparatorsPaint = new SolidColorPaint(SKColors.LightGray)
            }
        };

        YAxes = new Axis[]
        {
            new Axis
            {
                SeparatorsPaint = new SolidColorPaint(SKColors.LightGray)
            }
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
