using System;
using System.Globalization;
using Avalonia.Data.Converters;
using SimRacingHub.Application.Features.Shell;

namespace SimRacingHub.Avalonia.Converters
{
    /// <summary>
    /// Value converter comparing a PageId with a target PageId parameter, returning a boolean.
    /// </summary>
    public sealed class PageIdEqualsConverter : IValueConverter
    {
        public static readonly PageIdEqualsConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not PageId current)
            {
                return false;
            }

            if (parameter is PageId target)
            {
                return current == target;
            }

            if (parameter is string paramStr && Enum.TryParse<PageId>(paramStr, true, out var parsed))
            {
                return current == parsed;
            }

            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
