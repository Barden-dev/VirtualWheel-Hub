using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SimRacingHub.Services;

namespace SimRacingHub.Avalonia.Dialogs;
public partial class ProfileNameDialog : Window
{
    public ProfileNameDialog() => InitializeComponent();
    public ProfileNameDialog(string name) : this()
    {
        ProfileNameBox.Text = name;
        Opened += (_, _) => { ProfileNameBox.Focus(); ProfileNameBox.SelectAll(); };
    }
    private void OnNameChanged(object? sender, TextChangedEventArgs e)
    {
        if (ConfirmNameButton != null) ConfirmNameButton.IsEnabled = !string.IsNullOrWhiteSpace(ProfileManager.SanitizeName(ProfileNameBox.Text));
    }
    private void OnConfirm(object? sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(ProfileManager.SanitizeName(ProfileNameBox.Text))) Close(ProfileNameBox.Text!.Trim());
    }
    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Close(null); e.Handled = true; }
        else if (e.Key == Key.Enter) { OnConfirm(sender, e); e.Handled = true; }
    }
}
