using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace SimRacingHub.Avalonia.Converters
{
    /// <summary>
    /// Value converter that converts a normalized double (-1.0..+1.0 or 0.0..1.0) into a formatted percentage string.
    /// </summary>
    public sealed class DoubleToPercentConverter : IValueConverter
    {
        public static readonly DoubleToPercentConverter Instance = new();

        public int DecimalPlaces { get; set; } = 1;

        public bool TreatAsAlreadyPercentage { get; set; }

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return "0%";
            }

            double num;
            try
            {
                num = System.Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return "0%";
            }

            if (!TreatAsAlreadyPercentage && Math.Abs(num) <= 1.0)
            {
                num *= 100.0;
            }

            int decimals = DecimalPlaces;
            if (parameter is int d)
            {
                decimals = d;
            }
            else if (parameter is string paramStr && int.TryParse(paramStr, out int parsed))
            {
                decimals = parsed;
            }

            string format = decimals <= 0 ? "F0" : $"F{decimals}";
            return $"{num.ToString(format, culture ?? CultureInfo.InvariantCulture)}%";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                string cleaned = str.Replace("%", "").Trim();
                if (double.TryParse(cleaned, NumberStyles.Float, culture ?? CultureInfo.InvariantCulture, out double parsed))
                {
                    return TreatAsAlreadyPercentage ? parsed : parsed / 100.0;
                }
            }

            return 0.0;
        }
    }
}
