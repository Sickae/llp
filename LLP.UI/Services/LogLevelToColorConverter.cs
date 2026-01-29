using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace LLP.UI.Services;

public class LogLevelToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string? level = value as string;
        if (string.IsNullOrEmpty(level)) return Brushes.Black;

        return level.ToUpperInvariant() switch
        {
            "ERROR" or "CRITICAL" or "FATAL" => Brushes.Red,
            "WARNING" or "WARN" => Brushes.Orange,
            "INFO" or "INFORMATION" => Brushes.Blue,
            "DEBUG" => Brushes.Gray,
            "TRACE" => Brushes.LightGray,
            _ => Brushes.Black
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
