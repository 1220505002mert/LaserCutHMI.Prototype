using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LaserCutHMI.Prototype.Converters
{
    
 
    
    public class EnumToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return Visibility.Collapsed;

            
            var valueStr = value.ToString();
            var paramStr = parameter.ToString();

            return string.Equals(valueStr, paramStr, StringComparison.OrdinalIgnoreCase)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
