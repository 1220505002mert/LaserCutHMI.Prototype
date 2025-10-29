using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace LaserCutHMI.Prototype.Converters
{
    /// <summary>
    /// Basınç rengi: &lt;5 sarı, 5–12 yeşil, &gt;12 kırmızı.
    /// </summary>
    public class PressureToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush Yellow = new SolidColorBrush(Colors.Gold);
        private static readonly SolidColorBrush Green = new SolidColorBrush(Colors.LimeGreen);
        private static readonly SolidColorBrush Red = new SolidColorBrush(Colors.IndianRed);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d)
            {
                if (d < 5) return Yellow;
                if (d <= 12) return Green;
                return Red;
            }
            return Green; // default
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
