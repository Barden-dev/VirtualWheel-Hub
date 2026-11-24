using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using SimRacingHub.Application.Runtime;

namespace SimRacingHub.Avalonia.Converters;

/// <summary>
/// Converts a <see cref="RuntimeStatusTone"/> to a footer indicator brush.
/// </summary>
public sealed class RuntimeStatusToneConverter : IValueConverter
{
    private static readonly IBrush NeutralBrush = new SolidColorBrush(Color.Parse("#64748B"));
    private static readonly IBrush SuccessBrush = new SolidColorBrush(Color.Parse("#4ADE80"));
    private static readonly IBrush WarningBrush = new SolidColorBrush(Color.Parse("#FBBF24"));
    private static readonly IBrush ErrorBrush = new SolidColorBrush(Color.Parse("#F87171"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        RuntimeStatusTone.Success => SuccessBrush,
        RuntimeStatusTone.Warning => WarningBrush,
        RuntimeStatusTone.Error => ErrorBrush,
        _ => NeutralBrush
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
