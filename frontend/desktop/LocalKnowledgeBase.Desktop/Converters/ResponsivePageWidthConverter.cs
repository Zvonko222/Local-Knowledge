using System.Globalization;
using System.Windows.Data;

namespace LocalKnowledgeBase.Desktop.Converters;

public sealed class ResponsivePageWidthConverter : IValueConverter
{
    public double HorizontalMargin { get; set; } = 68;
    public double MaxWidth { get; set; } = 960;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double viewportWidth || double.IsNaN(viewportWidth))
            return double.NaN;

        return Math.Max(320, Math.Min(viewportWidth - HorizontalMargin, MaxWidth));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
