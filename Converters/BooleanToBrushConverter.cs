using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace LaserCutHMI.Prototype.Converters
{
    public class BooleanToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush OkBrush = new SolidColorBrush(Colors.LimeGreen);
        private static readonly SolidColorBrush FailBrush = new SolidColorBrush(Colors.IndianRed);
        private static readonly SolidColorBrush Unknown = new SolidColorBrush(Colors.Gray);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b) return b ? OkBrush : FailBrush;
            return Unknown;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
