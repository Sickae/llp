using LLP.UI.ViewModels;
using System.Collections.Specialized;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using LiveChartsCore.SkiaSharpView.WPF;

namespace LLP.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        if (DataContext is MainViewModel vm)
        {
            vm.LogLines.CollectionChanged += LogLines_CollectionChanged;
            SetupHistogram(vm);
        }
    }

    private void SetupHistogram(MainViewModel vm)
    {
        try
        {
            var chart = new CartesianChart
            {
                Margin = new Thickness(5)
            };

            BindingOperations.SetBinding(chart, CartesianChart.SeriesProperty, new Binding("Histogram.Series"));
            BindingOperations.SetBinding(chart, CartesianChart.XAxesProperty, new Binding("Histogram.XAxes"));
            BindingOperations.SetBinding(chart, CartesianChart.YAxesProperty, new Binding("Histogram.YAxes"));

            HistogramContainer.Child = chart;
            HistogramContainer.Background = Brushes.Transparent;
        }
        catch (Exception ex)
        {
            // Fallback if assembly loading still fails at runtime
            System.Diagnostics.Debug.WriteLine($"Failed to load LiveCharts: {ex.Message}");
        }
    }

    private void LogLines_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.IsTailEnabled)
        {
            if (LogListBox.Items.Count > 0)
            {
                LogListBox.ScrollIntoView(LogListBox.Items[LogListBox.Items.Count - 1]);
            }
        }
    }
}
