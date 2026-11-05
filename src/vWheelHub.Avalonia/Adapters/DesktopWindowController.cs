using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using SimRacingHub.Application.Contracts;
using SimRacingHub.Application.Runtime;
using SimRacingHub.Avalonia.Composition;
using SimRacingHub.Core;

namespace SimRacingHub.Avalonia.Adapters;

/// <summary>Desktop-only startup and window policy; independent of shell initialization and preview.</summary>
public sealed class DesktopWindowController : IDisposable
{
    private readonly MainWindow _window;
    private readonly AvaloniaHostServices _host;
    private readonly IDesktopTray _tray;
    private readonly IDesktopSoundPlayer? _sounds;
    private bool _soundLocked;
    private readonly bool _allowAutoStart;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly NativeMenuItem _open = new(), _toggle = new(), _exit = new();
    private bool _started, _startupComplete, _disposed, _restoring, _restoreRequested;
    private WindowState _restoreState = WindowState.Normal;
    public NativeMenu Menu { get; } = new();
    public bool StartMinimizedRequested => _host.AppSettings.StartMinimized;
    public void SetRestoreState(WindowState state) => _restoreState = state == WindowState.Maximized ? WindowState.Maximized : WindowState.Normal;

    public DesktopWindowController(MainWindow window, AvaloniaHostServices host, IDesktopTray tray, bool allowAutoStart, IDesktopSoundPlayer? sounds = null)
    {
        _window = window; _host = host; _tray = tray; _allowAutoStart = allowAutoStart;
        _sounds = sounds; _soundLocked = host.ShellViewModel.IsLocked;
        Menu.Items.Add(_open); Menu.Items.Add(_toggle); Menu.Items.Add(new NativeMenuItemSeparator()); Menu.Items.Add(_exit);
        _open.Command = new RelayCommand(RestoreWindow);
        _toggle.Command = new AsyncRelayCommand(async () =>
        {
            if (_disposed || !_startupComplete || _host.ShellViewModel.IsBusy || _window.IsGettingStartedOpen) return;
            RestoreWindow();
            await _host.ShellViewModel.ToggleHubAsync();
        });
        _exit.Command = new RelayCommand(() => { if (!_disposed) _window.RequestAppExit(); });
        Menu.NeedsUpdate += (_, _) => RefreshMenu();
        _window.PropertyChanged += WindowChanged;
        _host.ShellViewModel.PropertyChanged += ShellChanged;
        _host.LocalizationManager.LanguageChanged += LanguageChanged;
        _window.Closed += WindowClosed;
        RefreshMenu();
    }

    public async Task StartAsync()
    {
        if (_started || _disposed) return;
        _started = true;
        try { await _tray.InitializeAsync(Menu, RestoreWindow, _lifetime.Token); }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { return; }
        catch (Exception ex) { AppLogger.Instance.LogError("System tray initialization failed; window remains accessible.", ex); }
        if (_disposed) return;
        RefreshMenu();
        bool failed = false;
        if (_allowAutoStart && _host.AppSettings.AutoStartHub && !_host.RuntimeSession.IsRunning)
        {
            bool wasEnabled = _window.IsEnabled;
            _window.IsEnabled = false;
            try
            {
                var shell = _host.ShellViewModel;
                await _host.RuntimeSession.StartAsync(new RuntimeStartOptions(shell.ResolvedProfile.OutputMode,
                    RetainOutputConnection: true, LockCursorOnStart: false)
                { Profile = shell.ResolvedProfile, Context = shell.CurrentGameContext, AllowOutputSetup = false }, _lifetime.Token);
                if (!_host.RuntimeSession.IsRunning || _host.RuntimeSession.HasFault)
                    throw new InvalidOperationException(_host.RuntimeSession.FaultMessage ?? "Hub runtime did not start.");
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                failed = true;
                try { await _host.RuntimeSession.StopAsync(); }
                catch (Exception cleanup) { AppLogger.Instance.LogError("Startup cleanup failed.", cleanup); }
                if (_disposed) return;
                _host.ShellViewModel.HasFault = true;
                _host.ShellViewModel.FaultMessage = ex.Message;
                _window.IsEnabled = wasEnabled;
                RestoreWindow();
                await _host.DialogService.ShowNoticeAsync(new DialogNoticeRequest(
                    _host.LocalizationManager.GetString("Title_StartError"), ex.Message, DialogSeverity.Error));
            }
            finally { _window.IsEnabled = wasEnabled; }
        }
        if (_disposed) return;
        _startupComplete = true;
        RefreshMenu();
        if (_restoreRequested)
        {
            _window.WindowState = _restoreState;
            RestoreWindow();
        }
        else if (!failed && StartMinimizedRequested) _window.WindowState = WindowState.Minimized;
        if (_window.WindowState == WindowState.Minimized) TryMinimizeToTray();
    }

    public void RestoreWindow()
    {
        if (_disposed) return;
        if (!_startupComplete) _restoreRequested = true;
        _restoring = true;
        try
        {
            _window.ShowInTaskbar = true;
            _window.Show();
            if (_window.WindowState == WindowState.Minimized) _window.WindowState = _restoreState;
            _window.Activate();
        }
        finally { _restoring = false; }
    }

    public bool TryCloseToTray(WindowClosingEventArgs e)
    {
        if (_disposed || !_started || !_host.AppSettings.CloseToTray || !_tray.IsAvailable ||
            e.CloseReason is WindowCloseReason.ApplicationShutdown or WindowCloseReason.OSShutdown or WindowCloseReason.OwnerWindowClosing) return false;
        e.Cancel = true;
        HideWindow();
        return true;
    }

    private void TryMinimizeToTray()
    {
        if (_startupComplete && !_restoring && _host.AppSettings.MinimizeToTray && _tray.IsAvailable) HideWindow();
    }

    private void HideWindow()
    {
        // The tray must already exist before removing the only visible window.
        if (!_tray.IsAvailable) return;
        if (!_startupComplete) _restoreRequested = false;
        AvaloniaWindowStateService.SaveWindowState(_window, _host.Storage.WindowSettingsPath, windowStateOverride: _restoreState);
        if (_host.RuntimeSession is IRuntimeCursorControl cursor) cursor.UpdateMouseLock(false);
        _window.Hide();
    }

    private void WindowChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != Window.WindowStateProperty) return;
        if (_window.WindowState == WindowState.Minimized) TryMinimizeToTray();
        else _restoreState = _window.WindowState;
    }
    private void ShellChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(_host.ShellViewModel.IsLocked))
        {
            bool locked = _host.ShellViewModel.IsLocked;
            if (locked != _soundLocked)
            {
                _soundLocked = locked;
                if (_host.AppSettings.MouseLockSoundsEnabled) _sounds?.PlayMouseLock(locked);
            }
        }
        if (e.PropertyName == nameof(_host.ShellViewModel.HasFault) && _host.ShellViewModel.HasFault)
            Dispatcher.UIThread.Post(() => { if (!_disposed) RestoreWindow(); });
        QueueMenuRefresh();
    }
    private void LanguageChanged(string culture) => QueueMenuRefresh();
    private void QueueMenuRefresh() => Dispatcher.UIThread.Post(() => { if (!_disposed) RefreshMenu(); });
    private void RefreshMenu()
    {
        var localization = _host.LocalizationManager;
        _open.Header = localization.GetString("Desktop_TrayOpen");
        _toggle.Header = localization.GetString(_host.RuntimeSession.IsRunning ? "Desktop_TrayStop" : "Desktop_TrayStart");
        _toggle.IsEnabled = _startupComplete && !_host.ShellViewModel.IsBusy && !_window.IsGettingStartedOpen;
        _exit.Header = localization.GetString("Desktop_TrayExit");
        _tray.SetToolTip(string.Format(localization.GetString("Desktop_TrayTooltipFormat"), _toggle.Header));
    }
    private void WindowClosed(object? sender, EventArgs e) => Dispose();
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetime.Cancel();
        _window.PropertyChanged -= WindowChanged;
        _window.Closed -= WindowClosed;
        _host.ShellViewModel.PropertyChanged -= ShellChanged;
        _host.LocalizationManager.LanguageChanged -= LanguageChanged;
        _sounds?.Dispose();
        _tray.Dispose();
        _lifetime.Dispose();
    }
}
