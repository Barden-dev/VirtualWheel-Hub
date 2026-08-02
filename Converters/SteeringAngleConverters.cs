using System;
using System.Globalization;
using System.Windows.Data;

namespace SimRacingHub.Converters
{
    public class HalfSteeringLockConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int lockDegrees)
            {
                return lockDegrees / 2;
            }
            if (value != null && int.TryParse(value.ToString(), out int parsed))
            {
                return parsed / 2;
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double halfDegrees)
            {
                return (int)(halfDegrees * 2);
            }
            if (value is int halfInt)
            {
                return halfInt * 2;
            }
            return 0;
        }
    }

    public class NegativeHalfSteeringLockConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int lockDegrees)
            {
                return -(lockDegrees / 2);
            }
            if (value != null && int.TryParse(value.ToString(), out int parsed))
            {
                return -(parsed / 2);
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double negativeHalf)
            {
                return (int)(-negativeHalf * 2);
            }
            if (value is int negativeInt)
            {
                return -negativeInt * 2;
            }
            return 0;
        }
    }
}
