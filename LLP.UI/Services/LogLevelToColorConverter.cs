using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace LLP.UI.Services;

public class LogLevelToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string level = (value as string ?? "").Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(level)) return Brushes.Black;

        if (level.Contains("ERROR") || level.Contains("CRITICAL") || level.Contains("FATAL") || level.Contains("ERR")) return Brushes.Red;
        if (level.Contains("WARNING") || level.Contains("WARN")) return Brushes.Orange;
        if (level.Contains("INFO")) return Brushes.Blue;
        if (level.Contains("DEBUG")) return Brushes.Gray;
        if (level.Contains("TRACE")) return Brushes.LightGray;

        return Brushes.Black;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
