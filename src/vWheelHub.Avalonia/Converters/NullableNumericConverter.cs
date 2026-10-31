using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace SimRacingHub.Avalonia.Converters
{
    /// <summary>
    /// Value converter to convert between decimal? (used by Avalonia NumericUpDown.Value)
    /// and int?, double?, or other numeric types.
    /// Preserves null values for inheritance/override mechanisms.
    /// </summary>
    public sealed class NullableNumericConverter : IValueConverter
    {
        public static readonly NullableNumericConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null) return null;
            if (value is decimal dec) return dec;
            if (value is double d) return (decimal)d;
            if (value is float f) return (decimal)f;
            if (value is int i) return (decimal)i;
            if (value is long l) return (decimal)l;
            return System.Convert.ToDecimal(value, culture ?? CultureInfo.InvariantCulture);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null) return null;
            var targetUnderlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (value is decimal dec)
            {
                if (targetUnderlying == typeof(double)) return (double)dec;
                if (targetUnderlying == typeof(float)) return (float)dec;
                if (targetUnderlying == typeof(int)) return (int)Math.Round(dec);
                if (targetUnderlying == typeof(long)) return (long)Math.Round(dec);
                if (targetUnderlying == typeof(decimal)) return dec;
            }

            return System.Convert.ChangeType(value, targetUnderlying, culture ?? CultureInfo.InvariantCulture);
        }
    }
}
