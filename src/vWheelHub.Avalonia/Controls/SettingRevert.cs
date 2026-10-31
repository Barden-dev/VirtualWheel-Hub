using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using SimRacingHub.Services;

namespace SimRacingHub.Avalonia.Controls;

/// <summary>A quiet per-setting undo tied to the saved snapshot, never to override presence.</summary>
public sealed class SettingRevert : AvaloniaObject
{
    public static readonly AttachedProperty<ProfileSession?> SessionProperty =
        AvaloniaProperty.RegisterAttached<SettingRevert, Button, ProfileSession?>("Session");
    public static readonly AttachedProperty<string?> PropertyNameProperty =
        AvaloniaProperty.RegisterAttached<SettingRevert, Button, string?>("PropertyName");
    private static readonly ConditionalWeakTable<Button, Subscription> Subscriptions = new();

    static SettingRevert()
    {
        SessionProperty.Changed.AddClassHandler<Button>((button, _) => Configure(button));
        PropertyNameProperty.Changed.AddClassHandler<Button>((button, _) => Configure(button));
    }
    public static ProfileSession? GetSession(Button button) => button.GetValue(SessionProperty);
    public static void SetSession(Button button, ProfileSession? value) => button.SetValue(SessionProperty, value);
    public static string? GetPropertyName(Button button) => button.GetValue(PropertyNameProperty);
    public static void SetPropertyName(Button button, string? value) => button.SetValue(PropertyNameProperty, value);
    private static void Configure(Button button) => Subscriptions.GetValue(button, b => new Subscription(b)).Bind();

    private sealed class Subscription
    {
        private readonly Button _button;
        private ProfileSession? _session;
        private readonly RelayCommand _restore;
        public Subscription(Button button)
        {
            _button = button;
            _restore = new RelayCommand(() => _session?.RestoreSavedProperty(GetPropertyName(_button) ?? ""),
                () => _session?.IsPropertyModified(GetPropertyName(_button) ?? "") == true);
            button.AttachedToVisualTree += (_, _) => Bind();
            button.DetachedFromVisualTree += (_, _) => Unbind();
        }
        public void Bind()
        {
            Unbind();
            _session = GetSession(_button);
            if (_session != null) _session.UndoStateChanged += Changed;
            _button.Command = _restore;
            Refresh();
        }
        private void Unbind()
        {
            if (_session != null) _session.UndoStateChanged -= Changed;
            _session = null;
        }
        private void Changed(object? sender, EventArgs e) => Refresh();
        private void Refresh()
        {
            // Reserve the same slot before and after editing. Collapsing this button
            // moved adjacent spinners under the pointer during repeated clicks.
            bool modified = _restore.CanExecute(null);
            _button.IsVisible = true;
            _button.Opacity = modified ? 1 : 0;
            _button.IsHitTestVisible = modified;
            _button.Focusable = modified;
            _restore.NotifyCanExecuteChanged();
        }
    }
}
