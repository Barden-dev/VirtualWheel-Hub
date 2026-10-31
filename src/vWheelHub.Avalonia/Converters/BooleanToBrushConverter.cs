using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SimRacingHub.Avalonia.Converters
{
    /// <summary>
    /// Value converter that converts a boolean to an IBrush.
    /// Default values provide status indication (True = green #4ADE80, False = slate #64748B or transparent).
    /// </summary>
    public sealed class BooleanToBrushConverter : IValueConverter
    {
        public static readonly BooleanToBrushConverter Instance = new();

        public IBrush TrueBrush { get; set; } = new SolidColorBrush(Color.FromRgb(74, 222, 128)); // #4ADE80

        public IBrush FalseBrush { get; set; } = new SolidColorBrush(Color.FromRgb(100, 116, 139)); // #64748B

        public bool IsInverted { get; set; }

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool isTrue = false;
            if (value is bool b)
            {
                isTrue = b;
            }
            else if (value != null)
            {
                isTrue = true;
            }

            if (IsInverted)
            {
                isTrue = !isTrue;
            }

            if (parameter is string paramStr && paramStr.Equals("invert", StringComparison.OrdinalIgnoreCase))
            {
                isTrue = !isTrue;
            }

            return isTrue ? TrueBrush : FalseBrush;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is IBrush brush)
            {
                return Equals(brush, TrueBrush);
            }

            return false;
        }
    }
}
