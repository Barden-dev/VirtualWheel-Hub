using System;
using System.Globalization;
using System.Windows.Data;

namespace SimRacingHub.Converters
{
    public class InputModeTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b)
            {
                return "Keyboard Only Mode";
            }
            return "Keyboard + Mouse Mode";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s && s == "Keyboard Only Mode")
            {
                return true;
            }
            return false;
        }
    }
}
