using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace SimRacingHub.Avalonia.Converters;

/// <summary>Displays profile ratios as ordinary 0–100 numbers without changing stored units.</summary>
public sealed class RatioPercentConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value == null ? null : System.Convert.ToDecimal(value, culture) * 100m;
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        NullableNumericConverter.Instance.ConvertBack(value == null ? null : System.Convert.ToDecimal(value, culture) / 100m, targetType, parameter, culture);
}
