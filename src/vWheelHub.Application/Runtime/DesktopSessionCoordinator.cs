using System;
using System.ComponentModel;
using SimRacingHub.Application.Contracts;
using SimRacingHub.Application.Features.Shell;
using SimRacingHub.Models;
using SimRacingHub.Services;
using SimRacingHub.Services.Games;

namespace SimRacingHub.Application.Runtime;
public sealed class DesktopSessionCoordinator : IDisposable
{
    private readonly AppShellViewModel _shell;
    private readonly IUiDispatcher _dispatcher;
    private readonly IRuntimeSession _runtime;
    private readonly HotkeyService? _hotkeys;
    private bool _started, _synchronizing, _togglePending;
    private int _inputSelectionDepth;
    public Func<bool>? IsInteractionBlocked { get; set; }
    public GameDetectionService Detection { get; }
    public Action? ForzaDetected { get; set; }
    private bool _forzaDetected;

    public IDisposable SuspendInputDeviceSelection()
    {
        if (_inputSelectionDepth++ == 0) _hotkeys?.Stop();
        return new InputSelectionLease(() =>
        {
            if (--_inputSelectionDepth != 0 || !_started || _hotkeys == null) return;
            _hotkeys.UpdateProfile(_shell.ResolvedProfile);
            _hotkeys.RequireKeyRelease();
            _hotkeys.Start(); // Reopens the independent reader using the newly saved selection.
        });
    }

    private sealed class InputSelectionLease(Action release) : IDisposable
    {
        private Action? _release = release;
        public void Dispose() => System.Threading.Interlocked.Exchange(ref _release, null)?.Invoke();
    }

    public DesktopSessionCoordinator(AppShellViewModel shell, IRuntimeSession runtime, GameIntegrationRegistry registry, IUiDispatcher dispatcher, HotkeyService? hotkeys = null)
    {
        _shell = shell;
        _runtime = runtime;
        _dispatcher = dispatcher;
        _hotkeys = hotkeys;
        Detection = new GameDetectionService(registry);
    }

    public void Start()
    {
        if (_started)
            return;
        _started = true;
        _shell.PropertyChanged += ShellChanged;
        if (_shell.Profiles != null)
            _shell.Profiles.PropertyChanged += ProfilesChanged;
        if (_shell.AppSettings != null)
            _shell.AppSettings.PropertyChanged += SettingsChanged;
        if (_shell.KeyBindings != null)
        {
            _shell.KeyBindings.PropertyChanged += BindingsChanged;
            _shell.KeyBindings.BindingAssigned += BindingAssigned;
        }

        _shell.ProfileSession.PropertyChanged += SessionChanged;
        Detection.OnContextChanged += Detected;
        Detection.OnGameRunning += GameRunning;
        Detection.OnVehicleStatusChanged += VehicleStatusChanged;
        if (_hotkeys != null)
        {
            _hotkeys.UpdateProfile(_shell.ResolvedProfile);
            _hotkeys.IsRuntimeActive = _runtime.IsRunning;
            _hotkeys.RequireKeyRelease();
            _hotkeys.OnToggleHub += ToggleHub;
            _hotkeys.OnToggleLock += ToggleLock;
            _hotkeys.Start();
        }

        SynchronizeContext(_shell.CurrentGameContext, contextAlreadyLoadedByProfiles: false);
        SetDetection();
    }

    private void Post(Action action) => _dispatcher.Post(() =>
    {
        if (_started)
            action();
    });
    private void Detected(ProfileContext context) => Post(() =>
    {
        if (_shell.AppSettings?.AutoDetectGame != true)
            return;
        if (context.Game == "Universal" && _runtime is IRuntimeCursorControl cursor)
            cursor.UpdateMouseLock(false);
        _shell.SetPendingContext(context);
    });
    private void GameRunning(string game) => Post(() =>
    {
        if (_forzaDetected || game != "Forza Motorsport" || ForzaDetected == null) return;
        _forzaDetected = true;
        ForzaDetected();
    });

    private void VehicleStatusChanged(GameVehicleStatus status) => Post(() =>
    {
        if (_shell.AppSettings?.AutoDetectGame == true) _shell.UpdateGameVehicleStatus(status);
    });

    private void SetDetection()
    {
        Detection.AutoDetectEnabled = _shell.AppSettings?.AutoDetectGame == true;
        _shell.UpdateGameVehicleStatus(Detection.AutoDetectEnabled ? Detection.VehicleStatus :
            new GameVehicleStatus(null, VehicleIdentityState.DetectionDisabled));
        if (Detection.AutoDetectEnabled)
            Detection.Start();
        else
            Detection.Stop();
    }

    private void SettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "AutoDetectGame")
            SetDetection();
    }

    private void ShellChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppShellViewModel.CurrentGameContext))
            SynchronizeContext(_shell.CurrentGameContext, contextAlreadyLoadedByProfiles: false);
        if (_hotkeys != null && e.PropertyName == nameof(AppShellViewModel.IsHubRunning))
            _hotkeys.IsRuntimeActive = _runtime.IsRunning;
    }

    private void SynchronizeContext(ProfileContext context, bool contextAlreadyLoadedByProfiles)
    {
        if (_synchronizing)
            return;
        _synchronizing = true;
        try
        {
            if (contextAlreadyLoadedByProfiles)
                _shell.AdoptLoadedContext(context);
            else
                _shell.Profiles?.AdoptLoadedContext(context);
            _shell.Dashboard?.SetCurrentContext(context);
        }
        finally
        {
            _synchronizing = false;
        }
    }

    private void ProfilesChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_synchronizing || e.PropertyName != "SelectedProfile" || _shell.Profiles?.SelectedProfile == null)
            return;
        SynchronizeContext(_shell.Profiles.CurrentContext, contextAlreadyLoadedByProfiles: true);
    }

    private void SessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ProfileSession.ResolvedProfile))
            return;
        _hotkeys?.UpdateProfile(_shell.ResolvedProfile);
        _hotkeys?.RequireKeyRelease();
    }

    private void BindingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_hotkeys != null && e.PropertyName == "IsCapturing")
        {
            _hotkeys.IsSuppressed = _shell.KeyBindings?.IsCapturing == true;
            _hotkeys.RequireKeyRelease();
        }
    }

    private void BindingAssigned(object? sender, EventArgs e) => _hotkeys?.RequireKeyRelease();
    private void ToggleHub() => Post(async () =>
    {
        if (_inputSelectionDepth != 0 || _togglePending || _shell.IsBusy || IsInteractionBlocked?.Invoke() == true || _shell.KeyBindings?.IsCapturing == true)
            return;
        _togglePending = true;
        try
        {
            await _shell.ToggleHubAsync();
        }
        finally
        {
            _togglePending = false;
        }
    });
    private void ToggleLock() => Post(() =>
    {
        if (_inputSelectionDepth == 0 && _runtime.IsRunning && IsInteractionBlocked?.Invoke() != true && _shell.KeyBindings?.IsCapturing != true && _runtime is IRuntimeCursorControl cursor)
            cursor.UpdateMouseLock(!cursor.IsLocked);
    });
    public void Dispose()
    {
        if (!_started)
            return;
        _started = false;
        Detection.OnContextChanged -= Detected;
        Detection.OnGameRunning -= GameRunning;
        Detection.OnVehicleStatusChanged -= VehicleStatusChanged;
        Detection.Stop();
        if (_hotkeys != null)
        {
            _hotkeys.OnToggleHub -= ToggleHub;
            _hotkeys.OnToggleLock -= ToggleLock;
            _hotkeys.Dispose();
        }

        _shell.PropertyChanged -= ShellChanged;
        _shell.ProfileSession.PropertyChanged -= SessionChanged;
        if (_shell.Profiles != null)
            _shell.Profiles.PropertyChanged -= ProfilesChanged;
        if (_shell.AppSettings != null)
            _shell.AppSettings.PropertyChanged -= SettingsChanged;
        if (_shell.KeyBindings != null)
        {
            _shell.KeyBindings.PropertyChanged -= BindingsChanged;
            _shell.KeyBindings.BindingAssigned -= BindingAssigned;
        }
    }
}
