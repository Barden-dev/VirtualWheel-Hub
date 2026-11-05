using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimRacingHub.Application.Common;
using SimRacingHub.Application.Contracts;
using SimRacingHub.Application.Localization;
using SimRacingHub.Models;
using SimRacingHub.Services;

namespace SimRacingHub.Application.Features.Dashboard;

/// <summary>Read-only device checks. The dashboard displays a notice only when attention is needed.</summary>
public partial class DeviceReadinessViewModel : ViewModelBase, IDisposable
{
    private readonly ProfileSession _profiles;
    private readonly IOutputDiagnostics _diagnostics;
    private readonly IPlatformCapabilities _platform;
    private readonly ILocalizationManager _localization;
    private readonly IUiDispatcher _dispatcher;
    private readonly Action _openSettings;
    private readonly Func<Task>? _fixOutput;
    private readonly CancellationTokenSource _lifetime = new();
    private OutputDeviceMode _lastMode;
    private int _lastDeviceId;
    private bool _lastWheelCompatibility;
    private ResolvedProfile? _observedProfile;
    private int _revision;
    private bool _disposed, _failed;
    private DeviceDiagnosticSnapshot? _output;
    private InputReadinessSnapshot? _input;
    private Task? _activeCheck;

    [ObservableProperty] private bool _isChecking;

    public DeviceDiagnosticSnapshot? OutputSnapshot => _output;
    public InputReadinessSnapshot? InputSnapshot => _input;
    public bool HasChecked => _output != null || _failed;
    public bool IsOutputReady => _output?.IsAvailable == true && !IsChecking;
    public bool IsInputReady => _input?.IsAvailable == true && !IsChecking;
    public bool ShowAdministratorNotice => _platform.SupportsVJoy && _output?.IsAvailable != true;
    public bool NeedsAttention => !IsChecking && HasChecked &&
        (_failed || !IsOutputReady || _input?.IsAvailable == false);
    public string ProblemTitle => Text(_failed ? "Readiness_CheckFailed" :
        _output?.IsAvailable != true ? "Readiness_OutputUnavailable" : "Readiness_InputUnavailable");
    public string ProblemAdvice => Text(_failed ? "Readiness_RetryAdvice" :
        _output?.IsAvailable == true ? "Readiness_LinuxInputAdvice" :
        _platform.SupportsUInput ? "Readiness_LinuxOutputAdvice" : "Readiness_WindowsOutputAdvice");

    public DeviceReadinessViewModel(ProfileSession profiles, IOutputDiagnostics diagnostics,
        IPlatformCapabilities platform, ILocalizationManager localization, IUiDispatcher dispatcher,
        Action openSettings, Func<Task>? fixOutput = null)
    {
        _profiles = profiles; _diagnostics = diagnostics; _platform = platform;
        _localization = localization; _dispatcher = dispatcher; _openSettings = openSettings;
        _fixOutput = fixOutput;
        _lastMode = profiles.ResolvedProfile.OutputMode;
        _lastDeviceId = profiles.ResolvedProfile.VJoyDeviceId;
        _lastWheelCompatibility = profiles.ResolvedProfile.VJoyWheelCompatibility;
        ObserveProfile();
        _profiles.PropertyChanged += ProfileChanged;
        _localization.LanguageChanged += LanguageChanged;
    }

    private string Text(string key) => _localization.GetString(key);
    private void NotifyStatus()
    {
        foreach (string name in new[] { nameof(OutputSnapshot), nameof(InputSnapshot), nameof(HasChecked), nameof(IsOutputReady),
            nameof(IsInputReady), nameof(NeedsAttention), nameof(ProblemTitle), nameof(ProblemAdvice), nameof(ShowAdministratorNotice) })
            OnPropertyChanged(name);
    }
    private void LanguageChanged(string _) => _dispatcher.Post(() => { if (!_disposed) NotifyStatus(); });
    private void ObserveProfile()
    {
        if (ReferenceEquals(_observedProfile, _profiles.ResolvedProfile)) return;
        if (_observedProfile != null) _observedProfile.PropertyChanged -= ProfileChanged;
        _observedProfile = _profiles.ResolvedProfile;
        _observedProfile.PropertyChanged += ProfileChanged;
    }
    private void ProfileChanged(object? sender, PropertyChangedEventArgs e)
    {
        _dispatcher.Post(() => {
            if (_disposed) return;
            ObserveProfile();
            if (_lastMode == _profiles.ResolvedProfile.OutputMode &&
                _lastDeviceId == _profiles.ResolvedProfile.VJoyDeviceId &&
                _lastWheelCompatibility == _profiles.ResolvedProfile.VJoyWheelCompatibility) return;
            _lastMode = _profiles.ResolvedProfile.OutputMode;
            _lastDeviceId = _profiles.ResolvedProfile.VJoyDeviceId;
            _lastWheelCompatibility = _profiles.ResolvedProfile.VJoyWheelCompatibility;
            _revision++;
            _output = null; _input = null; _failed = false;
            NotifyStatus();
            if (!IsChecking) CheckReadinessCommand.Execute(null);
        });
    }

    [RelayCommand]
    public Task CheckReadinessAsync()
    {
        if (_disposed) return Task.CompletedTask;
        if (_activeCheck is { IsCompleted: false }) return _activeCheck;
        return _activeCheck = CheckCoreAsync();
    }

    private async Task CheckCoreAsync()
    {
        IsChecking = true;
        NotifyStatus();
        var cancellation = _lifetime.Token;
        try
        {
            bool current;
            do
            {
                int revision = _revision;
                var mode = _profiles.ResolvedProfile.OutputMode;
                int deviceId = _profiles.ResolvedProfile.VJoyDeviceId;
                bool wheelCompatibility = _profiles.ResolvedProfile.VJoyWheelCompatibility;
                DeviceDiagnosticSnapshot? output = null;
                InputReadinessSnapshot? input = null;
                bool failed = false;
                try
                {
                    output = await _diagnostics.CheckAsync(mode, cancellation).ConfigureAwait(false);
                    if (_diagnostics is IInputReadinessDiagnostics inputs)
                        input = await inputs.CheckInputAsync(cancellation).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { return; }
                catch (Exception ex)
                {
                    SimRacingHub.Core.AppLogger.Instance.LogError("Device readiness check failed", ex);
                    failed = true;
                }
                if (_disposed) return;
                current = await _dispatcher.InvokeAsync(() => {
                    if (_disposed || revision != _revision || mode != _profiles.ResolvedProfile.OutputMode ||
                        deviceId != _profiles.ResolvedProfile.VJoyDeviceId ||
                        wheelCompatibility != _profiles.ResolvedProfile.VJoyWheelCompatibility) return false;
                    _output = failed ? null : output;
                    _input = failed ? null : input;
                    _failed = failed;
                    return true;
                }).ConfigureAwait(false);
            } while (!current && !_disposed);
        }
        finally
        {
            if (!_disposed)
                await _dispatcher.InvokeAsync(() => { IsChecking = false; NotifyStatus(); }).ConfigureAwait(false);
        }
    }

    [RelayCommand] private void OpenSettings() => _openSettings();

    [RelayCommand]
    private async Task FixAsync()
    {
        if (_disposed || IsChecking) return;
        if (_platform.SupportsVJoy && !IsOutputReady && _fixOutput != null)
            await _fixOutput();
        else _openSettings();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetime.Cancel();
        _profiles.PropertyChanged -= ProfileChanged;
        if (_observedProfile != null) _observedProfile.PropertyChanged -= ProfileChanged;
        _localization.LanguageChanged -= LanguageChanged;
        _lifetime.Dispose();
    }
}
