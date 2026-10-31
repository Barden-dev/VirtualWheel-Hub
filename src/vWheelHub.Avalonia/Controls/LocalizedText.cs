using System.Globalization;
using Avalonia;
using Avalonia.Controls;

namespace SimRacingHub.Avalonia.Controls;

/// <summary>Formats a live value with a dynamic resource, so language changes
/// update the label even when the underlying value has not changed.</summary>
public sealed class LocalizedText : AvaloniaObject
{
    public static readonly AttachedProperty<object?> ValueProperty = AvaloniaProperty.RegisterAttached<LocalizedText, TextBlock, object?>("Value");
    public static readonly AttachedProperty<string?> FormatProperty = AvaloniaProperty.RegisterAttached<LocalizedText, TextBlock, string?>("Format");
    public static readonly AttachedProperty<string> TrueTextProperty = AvaloniaProperty.RegisterAttached<LocalizedText, TextBlock, string>("TrueText", string.Empty);
    public static readonly AttachedProperty<string> FalseTextProperty = AvaloniaProperty.RegisterAttached<LocalizedText, TextBlock, string>("FalseText", string.Empty);
    static LocalizedText()
    {
        ValueProperty.Changed.AddClassHandler<TextBlock>((text, _) =>
        {
            if (!text.IsSet(TrueTextProperty)) text.Bind(TrueTextProperty, new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("Common_Yes"));
            if (!text.IsSet(FalseTextProperty)) text.Bind(FalseTextProperty, new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("Common_No"));
            Refresh(text);
        });
        TrueTextProperty.Changed.AddClassHandler<TextBlock>((text, _) => Refresh(text));
        FalseTextProperty.Changed.AddClassHandler<TextBlock>((text, _) => Refresh(text));
        FormatProperty.Changed.AddClassHandler<TextBlock>((text, _) => Refresh(text));
    }
    public static object? GetValue(TextBlock target) => target.GetValue(ValueProperty);
    public static void SetValue(TextBlock target, object? value) => target.SetValue(ValueProperty, value);
    public static string? GetFormat(TextBlock target) => target.GetValue(FormatProperty);
    public static void SetFormat(TextBlock target, string? value) => target.SetValue(FormatProperty, value);
    internal static void Refresh(TextBlock target)
    {
        var format = GetFormat(target);
        if (string.IsNullOrEmpty(format)) return;
        var culture = CultureInfo.CurrentUICulture;
        var value = GetValue(target);
        if (value is bool flag) value = flag ? target.GetValue(TrueTextProperty) : target.GetValue(FalseTextProperty);
        target.Text = string.Format(culture, format, value);
    }
}
