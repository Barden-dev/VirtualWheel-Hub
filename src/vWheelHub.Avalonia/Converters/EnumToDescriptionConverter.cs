using System;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using Avalonia.Data.Converters;

namespace SimRacingHub.Avalonia.Converters
{
    /// <summary>
    /// Value converter that maps an enum value to its DescriptionAttribute description,
    /// or falls back to its string representation.
    /// </summary>
    public sealed class EnumToDescriptionConverter : IValueConverter
    {
        public static readonly EnumToDescriptionConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return string.Empty;
            }

            var type = value.GetType();
            if (!type.IsEnum)
            {
                return value.ToString() ?? string.Empty;
            }

            string name = value.ToString() ?? string.Empty;
            var field = type.GetField(name);
            if (field != null)
            {
                var attr = field.GetCustomAttribute<DescriptionAttribute>();
                if (attr != null && !string.IsNullOrWhiteSpace(attr.Description))
                {
                    return attr.Description;
                }
            }

            return name;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string str && targetType.IsEnum)
            {
                foreach (var field in targetType.GetFields(BindingFlags.Public | BindingFlags.Static))
                {
                    var attr = field.GetCustomAttribute<DescriptionAttribute>();
                    if (attr != null && string.Equals(attr.Description, str, StringComparison.OrdinalIgnoreCase))
                    {
                        return field.GetValue(null);
                    }

                    if (string.Equals(field.Name, str, StringComparison.OrdinalIgnoreCase))
                    {
                        return field.GetValue(null);
                    }
                }

                if (Enum.TryParse(targetType, str, true, out var result))
                {
                    return result;
                }
            }

            return null;
        }
    }
}
