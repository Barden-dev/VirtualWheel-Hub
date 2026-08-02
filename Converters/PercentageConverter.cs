using System;
using System.Globalization;
using System.Windows.Data;

namespace SimRacingHub.Converters
{
    public class PercentageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d)
            {
                return d * 100.0;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d)
            {
                return d / 100.0;
            }
            if (value is string s && double.TryParse(s, NumberStyles.Any, culture, out double result))
            {
                return result / 100.0;
            }
            return value;
        }
    }
}
