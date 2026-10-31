using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace SimRacingHub.Avalonia.Converters
{
    /// <summary>
    /// Value converter that converts null or nullable values to boolean (true if not null, false if null).
    /// Supports inversion via property or parameter ("invert", "not", "!").
    /// </summary>
    public sealed class NullableToBooleanConverter : IValueConverter
    {
        public static readonly NullableToBooleanConverter Instance = new();

        public bool IsInverted { get; set; }

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool hasValue = value != null;

            if (value is string str)
            {
                hasValue = !string.IsNullOrEmpty(str);
            }

            bool invert = IsInverted;
            if (parameter is string paramStr &&
                (paramStr.Equals("invert", StringComparison.OrdinalIgnoreCase) ||
                 paramStr.Equals("not", StringComparison.OrdinalIgnoreCase) ||
                 paramStr == "!"))
            {
                invert = !invert;
            }
            else if (parameter is bool paramBool && paramBool)
            {
                invert = !invert;
            }

            return invert ? !hasValue : hasValue;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
