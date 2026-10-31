using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace SimRacingHub.Avalonia.Controls;

/// <summary>Keep stepping distinct from selecting a number for replacement.</summary>
public sealed class NumericInputBehavior : AvaloniaObject
{
    public static readonly AttachedProperty<bool> EnabledProperty =
        AvaloniaProperty.RegisterAttached<NumericInputBehavior, NumericUpDown, bool>("Enabled");

    static NumericInputBehavior() => EnabledProperty.Changed.AddClassHandler<NumericUpDown>((number, _) =>
    {
        number.Spinned -= Spinned;
        number.RemoveHandler(InputElement.KeyDownEvent, KeyDown);
        if (GetEnabled(number))
        {
            number.Spinned += Spinned;
            number.AddHandler(InputElement.KeyDownEvent, KeyDown, RoutingStrategies.Tunnel);
        }
    });

    public static bool GetEnabled(NumericUpDown number) => number.GetValue(EnabledProperty);
    public static void SetEnabled(NumericUpDown number, bool value) => number.SetValue(EnabledProperty, value);

    private static void KeyDown(object? sender, KeyEventArgs e)
    {
        // Arrow buttons are pointer-only. Keyboard stepping belongs to the editor.
        if (e.Handled || e.KeyModifiers != KeyModifiers.None || e.Key is not (Key.Up or Key.Down) ||
            sender is not NumericUpDown { AllowSpin: true, IsReadOnly: false } number ||
            number.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(t => t.Name == "PART_TextBox") is not { IsFocused: true } ||
            number.GetVisualDescendants().OfType<Spinner>().FirstOrDefault(s => s.Name == "PART_Spinner") is not { } spinner) return;
        var spin = new SpinEventArgs(e.Key == Key.Up ? SpinDirection.Increase : SpinDirection.Decrease) { RoutedEvent = Spinner.SpinEvent };
        spinner.RaiseEvent(spin);
        e.Handled = spin.Handled;
    }

    private static void Spinned(object? sender, SpinEventArgs e)
    {
        if (sender is NumericUpDown number &&
            number.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(t => t.Name == "PART_TextBox") is { } text)
        {
            // NumericUpDown selects all on initial focus, including an arrow click.
            // Only clear after an explicit spin: Ctrl+A, dragging and normal typing remain native.
            text.SelectionStart = text.SelectionEnd = text.CaretIndex;
        }
    }
}
