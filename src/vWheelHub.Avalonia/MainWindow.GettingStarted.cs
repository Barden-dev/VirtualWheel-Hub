using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SimRacingHub.Application.Features.Shell;
using SimRacingHub.Avalonia.Onboarding;

namespace SimRacingHub.Avalonia;

public partial class MainWindow
{
    private bool _guideOpening;
    public bool IsGettingStartedOpen => _guideOpening || GettingStarted.IsOpen;

    private void InitializeGettingStarted()
    {
        // Do not let window shortcuts save/revert a profile while the tour owns keyboard focus.
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (!IsGettingStartedOpen) return;
            if (e.Key == Key.Escape) { GettingStarted.Finish(); e.Handled = true; }
            else if ((e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) != 0)
                e.Handled = true;
        }, RoutingStrategies.Tunnel);
        Closed += (_, _) => GettingStarted.Finish(remember: false);
    }

    private async void GettingStarted_Click(object? sender, RoutedEventArgs e) => await ShowGettingStartedAsync();

    public async Task ShowGettingStartedAsync()
    {
        if (IsGettingStartedOpen || DataContext is not AppShellViewModel shell || shell.IsBusy) return;
        _guideOpening = true;
        using var shortcuts = App.Services?.DesktopSession?.SuspendInputDeviceSelection();
        var fadesSuppressed = HubContent.GetValue(Controls.ScrollEdgeFade.SuppressedProperty);
        try
        {
            // A guide must not send real controls to a running game, including from its own buttons.
            if (shell.IsHubRunning) await shell.StopHubAsync();
            if (shell.IsHubRunning || _closeApproved || _exitRequested) return;
            // Page fades live in the window adorner layer, above ordinary overlay content.
            HubContent.SetCurrentValue(Controls.ScrollEdgeFade.SuppressedProperty, true);
            HubContent.IsHitTestVisible = false;
            await GettingStarted.OpenAsync(this, shell);
        }
        finally
        {
            HubContent.IsHitTestVisible = true;
            HubContent.SetCurrentValue(Controls.ScrollEdgeFade.SuppressedProperty, fadesSuppressed);
            _guideOpening = false;
            this.FindControl<Button>("SidebarGettingStarted")?.Focus();
        }
    }
}
