using System;
using System.Collections.Generic;
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

namespace SimRacingHub.Application.Features.Output
{
    /// <summary>
    /// ViewModel for virtual output device configuration, managing output feeder mode selection
    /// (vJoy DirectInput vs Xbox 360 ViGEm vs uinput), axis calibration min/max/inversion parameters,
    /// device diagnostics probing with cancellation support, and calibration reset.
    /// </summary>
    public partial class OutputViewModel : ViewModelBase, IDisposable
    {
        private readonly ProfileSession _profileSession;
        private readonly IOutputDiagnostics _outputDiagnostics;
        private readonly IPlatformCapabilities _platformCapabilities;
        private readonly ILocalizationManager _localizationManager;
        private readonly IUiDispatcher? _uiDispatcher;
        private readonly IUserDialogService? _dialogs;
        private readonly IExternalLauncher? _externalLauncher;
        public Action? OutputRecovered { get; set; }
        private readonly SimRacingHub.Application.Runtime.IRuntimeSession? _runtime;

        public bool ShowVJoySettings => IsVJoySupported && SelectedOutputMode == OutputDeviceMode.VJoy;
        public int VJoyDeviceId
        {
            get => Math.Clamp(CurrentProfile?.VJoyDeviceId ?? ResolvedProfile?.VJoyDeviceId ?? 1, 1, 16);
            set { if (CurrentProfile is { } profile) profile.VJoyDeviceId = Math.Clamp(value, 1, 16); }
        }
        public bool VJoyWheelCompatibility
        {
            get => CurrentProfile?.VJoyWheelCompatibility ?? ResolvedProfile?.VJoyWheelCompatibility ?? false;
            set { if (CurrentProfile is { } profile) profile.VJoyWheelCompatibility = value; }
        }

        private bool _isDisposed;
        private bool _eventsSubscribed;
        private Profile? _observedProfile;
        private ResolvedProfile? _observedResolved;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasDiagnosticSnapshot))]
        [NotifyPropertyChangedFor(nameof(HasDiagnosticErrorDetails))]
        [NotifyPropertyChangedFor(nameof(HasDiagnosticActions))]
        private DeviceDiagnosticSnapshot? _deviceDiagnosticSnapshot;

        public bool HasDiagnosticSnapshot => DeviceDiagnosticSnapshot != null;
        public bool HasDiagnosticErrorDetails => !string.IsNullOrWhiteSpace(DeviceDiagnosticSnapshot?.ErrorDetails);
        public bool HasDiagnosticActions => DeviceDiagnosticSnapshot?.RecommendedActions is { Count: > 0 };

        [ObservableProperty]
        private bool _isCheckingDiagnostic;

        [ObservableProperty]
        private string? _statusMessage;

        public SimRacingHub.Application.Features.Dashboard.DeviceReadinessViewModel? Readiness { get; set; }

        [ObservableProperty] private bool _hasInputAccessIssue;
        public string InputAccessAdvice => _localizationManager.GetString("Readiness_LinuxInputAdvice");

        private DateTimeOffset? _diagnosticCompletedAt;
        private bool _diagnosticSucceeded;
        public int PollingRate
        {
            get => CurrentProfile?.PollingRate ?? ResolvedProfile?.PollingRate ?? 1000;
            set
            {
                int rate = Math.Clamp(value, 50, 1000);
                if (CurrentProfile is { } profile && PollingRate != rate) profile.PollingRate = rate;
            }
        }

        private string CompletedDiagnosticText => string.Format(
            _localizationManager.GetString(_diagnosticSucceeded ? "Status_DeviceCheckReady" : "Status_DeviceCheckIssues"),
            _diagnosticCompletedAt?.ToLocalTime().ToString("HH:mm:ss"));

        public ProfileSession ProfileSession => _profileSession;
        public Profile? CurrentProfile => _profileSession.CurrentProfile;
        public ResolvedProfile? ResolvedProfile => _profileSession.ResolvedProfile;
        public IOutputDiagnostics OutputDiagnostics => _outputDiagnostics;
        public IPlatformCapabilities PlatformCapabilities => _platformCapabilities;
        public ILocalizationManager LocalizationManager => _localizationManager;

        public IReadOnlyList<OutputDeviceMode> AvailableOutputModes { get; } = new[]
        {
            OutputDeviceMode.VJoy,
            OutputDeviceMode.Xbox360
        };

        public bool IsVJoySupported => _platformCapabilities.SupportsVJoy;
        public bool IsViGEmSupported => _platformCapabilities.SupportsViGEm;
        public bool IsUInputSupported => _platformCapabilities.SupportsUInput;

        public OutputDeviceMode SelectedOutputMode
        {
            get => IsVJoySupported && !IsViGEmSupported ? OutputDeviceMode.VJoy
                : CurrentProfile?.OutputMode ?? ResolvedProfile?.OutputMode ?? OutputDeviceMode.VJoy;
            set
            {
                if (CurrentProfile != null && CurrentProfile.OutputMode != value)
                {
                    CurrentProfile.OutputMode = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ShowVJoySettings));
                    NotifyAllCalibrationProperties();
                }
            }
        }

        #region Steering Axis Calibration

        public int? OutSteerMin
        {
            get => CurrentProfile?.OutSteerMin;
            set
            {
                if (CurrentProfile != null && CurrentProfile.OutSteerMin != value)
                {
                    CurrentProfile.OutSteerMin = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(EffectiveOutSteerMin));
                }
            }
        }

        public int? OutSteerMax
        {
            get => CurrentProfile?.OutSteerMax;
            set
            {
                if (CurrentProfile != null && CurrentProfile.OutSteerMax != value)
                {
                    CurrentProfile.OutSteerMax = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(EffectiveOutSteerMax));
                }
            }
        }

        public bool? InvertSteering
        {
            get => CurrentProfile?.InvertSteering;
            set
            {
                if (CurrentProfile != null && CurrentProfile.InvertSteering != value)
                {
                    CurrentProfile.InvertSteering = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(EffectiveInvertSteering));
                }
            }
        }

        public int EffectiveOutSteerMin { get => CurrentProfile?.OutSteerMin ?? ResolvedProfile?.OutSteerMin ?? 0; set { if (value != EffectiveOutSteerMin) OutSteerMin = value; } }
        public int EffectiveOutSteerMax { get => CurrentProfile?.OutSteerMax ?? ResolvedProfile?.OutSteerMax ?? 32768; set { if (value != EffectiveOutSteerMax) OutSteerMax = value; } }
        public bool EffectiveInvertSteering { get => CurrentProfile?.InvertSteering ?? ResolvedProfile?.InvertSteering ?? false; set => InvertSteering = value; }

        #endregion

        #region Gas Axis Calibration

        public int? OutGasMin
        {
            get => CurrentProfile?.OutGasMin;
            set
            {
                if (CurrentProfile != null && CurrentProfile.OutGasMin != value)
                {
                    CurrentProfile.OutGasMin = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(EffectiveOutGasMin));
                }
            }
        }

        public int? OutGasMax
        {
            get => CurrentProfile?.OutGasMax;
            set
            {
                if (CurrentProfile != null && CurrentProfile.OutGasMax != value)
                {
                    CurrentProfile.OutGasMax = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(EffectiveOutGasMax));
                }
            }
        }

        public bool? InvertGas
        {
            get => CurrentProfile?.InvertGas;
            set
            {
                if (CurrentProfile != null && CurrentProfile.InvertGas != value)
                {
                    CurrentProfile.InvertGas = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(EffectiveInvertGas));
                }
            }
        }

        public int EffectiveOutGasMin { get => CurrentProfile?.OutGasMin ?? ResolvedProfile?.OutGasMin ?? 0; set { if (value != EffectiveOutGasMin) OutGasMin = value; } }
        public int EffectiveOutGasMax { get => CurrentProfile?.OutGasMax ?? ResolvedProfile?.OutGasMax ?? 32768; set { if (value != EffectiveOutGasMax) OutGasMax = value; } }
        public bool EffectiveInvertGas { get => CurrentProfile?.InvertGas ?? ResolvedProfile?.InvertGas ?? false; set => InvertGas = value; }

        #endregion

        #region Brake Axis Calibration

        public int? OutBrakeMin
        {
            get => CurrentProfile?.OutBrakeMin;
            set
            {
                if (CurrentProfile != null && CurrentProfile.OutBrakeMin != value)
                {
                    CurrentProfile.OutBrakeMin = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(EffectiveOutBrakeMin));
                }
            }
        }

        public int? OutBrakeMax
        {
            get => CurrentProfile?.OutBrakeMax;
            set
            {
                if (CurrentProfile != null && CurrentProfile.OutBrakeMax != value)
                {
                    CurrentProfile.OutBrakeMax = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(EffectiveOutBrakeMax));
                }
            }
        }

        public bool? InvertBrake
        {
            get => CurrentProfile?.InvertBrake;
            set
            {
                if (CurrentProfile != null && CurrentProfile.InvertBrake != value)
                {
                    CurrentProfile.InvertBrake = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(EffectiveInvertBrake));
                }
            }
        }

        public int EffectiveOutBrakeMin { get => CurrentProfile?.OutBrakeMin ?? ResolvedProfile?.OutBrakeMin ?? 0; set { if (value != EffectiveOutBrakeMin) OutBrakeMin = value; } }
        public int EffectiveOutBrakeMax { get => CurrentProfile?.OutBrakeMax ?? ResolvedProfile?.OutBrakeMax ?? 32768; set { if (value != EffectiveOutBrakeMax) OutBrakeMax = value; } }
        public bool EffectiveInvertBrake { get => CurrentProfile?.InvertBrake ?? ResolvedProfile?.InvertBrake ?? false; set => InvertBrake = value; }

        #endregion

        public OutputViewModel(
            ProfileSession profileSession,
            IOutputDiagnostics outputDiagnostics,
            IPlatformCapabilities platformCapabilities,
            ILocalizationManager localizationManager,
            IUiDispatcher? uiDispatcher = null,
            IUserDialogService? dialogs = null,
            SimRacingHub.Application.Runtime.IRuntimeSession? runtime = null,
            IExternalLauncher? externalLauncher = null)
        {
            _profileSession = profileSession ?? throw new ArgumentNullException(nameof(profileSession));
            _outputDiagnostics = outputDiagnostics ?? throw new ArgumentNullException(nameof(outputDiagnostics));
            _platformCapabilities = platformCapabilities ?? throw new ArgumentNullException(nameof(platformCapabilities));
            _localizationManager = localizationManager ?? throw new ArgumentNullException(nameof(localizationManager));
            _uiDispatcher = uiDispatcher;
            _dialogs = dialogs;
            _externalLauncher = externalLauncher;
            _runtime = runtime;

            StatusMessage = _localizationManager.GetString("Status_OutputReady", "Ready to diagnose output device");
            SubscribeEvents();
            ConnectOutputConnectionObserver();
            RefreshVirtualButtons();
        }

        private void SubscribeEvents()
        {
            if (_eventsSubscribed) return;

            _profileSession.UndoStateChanged += OnProfileSessionUndoStateChanged;
            _profileSession.PropertyChanged += OnProfileSessionPropertyChanged;
            _localizationManager.LanguageChanged += OnLanguageChanged;

            ObserveProfiles();

            _eventsSubscribed = true;
        }

        private void UnsubscribeEvents()
        {
            if (!_eventsSubscribed) return;

            _profileSession.UndoStateChanged -= OnProfileSessionUndoStateChanged;
            _profileSession.PropertyChanged -= OnProfileSessionPropertyChanged;
            _localizationManager.LanguageChanged -= OnLanguageChanged;

            if (_observedProfile != null) _observedProfile.PropertyChanged -= OnProfilePropertyChanged;
            if (_observedResolved != null) _observedResolved.PropertyChanged -= OnProfilePropertyChanged;
            _observedProfile = null;
            _observedResolved = null;

            _eventsSubscribed = false;
        }

        private void ObserveProfiles()
        {
            if (_observedProfile != null) _observedProfile.PropertyChanged -= OnProfilePropertyChanged;
            if (_observedResolved != null) _observedResolved.PropertyChanged -= OnProfilePropertyChanged;
            _observedProfile = CurrentProfile;
            _observedResolved = ResolvedProfile;
            if (_observedProfile != null) _observedProfile.PropertyChanged += OnProfilePropertyChanged;
            if (_observedResolved != null) _observedResolved.PropertyChanged += OnProfilePropertyChanged;
        }

        private void RunOnUi(Action action)
        {
            if (_uiDispatcher != null && !_uiDispatcher.CheckAccess())
            {
                _uiDispatcher.Post(action);
            }
            else
            {
                action();
            }
        }

        private void OnLanguageChanged(string culture) => RunOnUi(() =>
        {
            ValidateVirtualButtonRows();
            RefreshOutputConnectionText();
            OnPropertyChanged(nameof(InputAccessAdvice));
            if (_vJoyInventory != null) DisplayVJoyInventory(_vJoyInventory);
            else
            {
                OnPropertyChanged(nameof(VJoySelectionSummary));
                OnPropertyChanged(nameof(VJoyPrepareLabel));
            }
            if (_diagnosticCompletedAt.HasValue) StatusMessage = CompletedDiagnosticText;
            else if (!HasDiagnosticSnapshot)
                StatusMessage = _localizationManager.GetString("Status_OutputReady", "Ready to diagnose output device");
        });

        private void OnProfileSessionUndoStateChanged(object? sender, EventArgs e)
        {
            RunOnUi(NotifyAllCalibrationProperties);
        }

        private void OnProfileSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProfileSession.CurrentProfile) ||
                e.PropertyName == nameof(ProfileSession.ResolvedProfile))
            {
                RunOnUi(() => { ObserveProfiles(); NotifyAllCalibrationProperties(); });
            }
        }

        private void OnProfilePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            RunOnUi(() =>
            {
                switch (e.PropertyName)
                {
                    case nameof(Profile.PollingRate):
                        OnPropertyChanged(nameof(PollingRate));
                        break;
                    case nameof(Profile.OutputMode):
                        OnPropertyChanged(nameof(SelectedOutputMode));
                        OnPropertyChanged(nameof(ShowVJoySettings));
                        OnPropertyChanged(nameof(VirtualDeviceStateText));
                        RefreshVirtualButtons();
                        OnPropertyChanged(nameof(VirtualButtonLimit));
                        foreach (var row in VirtualButtonRows) row.RefreshTargetLabel();
                        break;
                    case nameof(Profile.VirtualButtons):
                        RefreshVirtualButtons();
                        break;
                    case nameof(Profile.VJoyDeviceId):
                        OnPropertyChanged(nameof(VJoyDeviceId));
                        OnPropertyChanged(nameof(VirtualDeviceStateText));
                        OnPropertyChanged(nameof(VirtualButtonLimit));
                        RefreshVirtualButtons();
                        break;
                    case nameof(Profile.VJoyWheelCompatibility):
                        OnPropertyChanged(nameof(VJoyWheelCompatibility));
                        OnPropertyChanged(nameof(VirtualDeviceStateText));
                        break;
                    case nameof(Profile.UseHubVJoyDevice):
                        OnPropertyChanged(nameof(UseHubVJoyDevice));
                        OnPropertyChanged(nameof(VirtualDeviceStateText));
                        OnPropertyChanged(nameof(VirtualButtonLimit));
                        RefreshVirtualButtons();
                        break;
                    case nameof(Profile.OutSteerMin):
                        OnPropertyChanged(nameof(OutSteerMin));
                        OnPropertyChanged(nameof(EffectiveOutSteerMin));
                        break;
                    case nameof(Profile.OutSteerMax):
                        OnPropertyChanged(nameof(OutSteerMax));
                        OnPropertyChanged(nameof(EffectiveOutSteerMax));
                        break;
                    case nameof(Profile.InvertSteering):
                        OnPropertyChanged(nameof(InvertSteering));
                        OnPropertyChanged(nameof(EffectiveInvertSteering));
                        break;
                    case nameof(Profile.OutGasMin):
                        OnPropertyChanged(nameof(OutGasMin));
                        OnPropertyChanged(nameof(EffectiveOutGasMin));
                        break;
                    case nameof(Profile.OutGasMax):
                        OnPropertyChanged(nameof(OutGasMax));
                        OnPropertyChanged(nameof(EffectiveOutGasMax));
                        break;
                    case nameof(Profile.InvertGas):
                        OnPropertyChanged(nameof(InvertGas));
                        OnPropertyChanged(nameof(EffectiveInvertGas));
                        break;
                    case nameof(Profile.OutBrakeMin):
                        OnPropertyChanged(nameof(OutBrakeMin));
                        OnPropertyChanged(nameof(EffectiveOutBrakeMin));
                        break;
                    case nameof(Profile.OutBrakeMax):
                        OnPropertyChanged(nameof(OutBrakeMax));
                        OnPropertyChanged(nameof(EffectiveOutBrakeMax));
                        break;
                    case nameof(Profile.InvertBrake):
                        OnPropertyChanged(nameof(InvertBrake));
                        OnPropertyChanged(nameof(EffectiveInvertBrake));
                        break;
                }
            });
        }

        private void NotifyAllCalibrationProperties()
        {
            RefreshOutputConnectionText();
            RefreshVirtualButtons();
            OnPropertyChanged(nameof(VirtualButtonLimit));
            OnPropertyChanged(nameof(PollingRate));
            OnPropertyChanged(nameof(SelectedOutputMode));
            OnPropertyChanged(nameof(ShowVJoySettings));
            OnPropertyChanged(nameof(VJoyDeviceId));
            OnPropertyChanged(nameof(VJoyWheelCompatibility));
            OnPropertyChanged(nameof(UseHubVJoyDevice));
            if (_vJoyInventory != null) DisplayVJoyInventory(_vJoyInventory);
            OnPropertyChanged(nameof(OutSteerMin));
            OnPropertyChanged(nameof(OutSteerMax));
            OnPropertyChanged(nameof(InvertSteering));
            OnPropertyChanged(nameof(EffectiveOutSteerMin));
            OnPropertyChanged(nameof(EffectiveOutSteerMax));
            OnPropertyChanged(nameof(EffectiveInvertSteering));

            OnPropertyChanged(nameof(OutGasMin));
            OnPropertyChanged(nameof(OutGasMax));
            OnPropertyChanged(nameof(InvertGas));
            OnPropertyChanged(nameof(EffectiveOutGasMin));
            OnPropertyChanged(nameof(EffectiveOutGasMax));
            OnPropertyChanged(nameof(EffectiveInvertGas));

            OnPropertyChanged(nameof(OutBrakeMin));
            OnPropertyChanged(nameof(OutBrakeMax));
            OnPropertyChanged(nameof(InvertBrake));
            OnPropertyChanged(nameof(EffectiveOutBrakeMin));
            OnPropertyChanged(nameof(EffectiveOutBrakeMax));
            OnPropertyChanged(nameof(EffectiveInvertBrake));
        }

        [RelayCommand]
        public async Task<DeviceDiagnosticSnapshot> CheckDiagnosticAsync(CancellationToken ct = default)
        {
            try
            {
                IsCheckingDiagnostic = true;
                DeviceDiagnosticSnapshot = null;
                HasInputAccessIssue = false;
                _diagnosticCompletedAt = null;
                StatusMessage = _localizationManager.GetString("Status_CheckingDiagnostics", "Checking device diagnostics...");
                SetBusy(true, StatusMessage);

                DeviceDiagnosticSnapshot snapshot;
                InputReadinessSnapshot? input;
                if (Readiness != null)
                {
                    await Readiness.CheckReadinessAsync().ConfigureAwait(false);
                    snapshot = Readiness.OutputSnapshot ?? DeviceDiagnosticSnapshot.Unavailable(SelectedOutputMode, Readiness.ProblemTitle);
                    input = Readiness.InputSnapshot;
                }
                else
                {
                    snapshot = await _outputDiagnostics.CheckAsync(SelectedOutputMode, ct).ConfigureAwait(false);
                    input = _outputDiagnostics is IInputReadinessDiagnostics inputs
                        ? await inputs.CheckInputAsync(ct).ConfigureAwait(false) : null;
                }

                RunOnUi(() =>
                {
                    DeviceDiagnosticSnapshot = snapshot;
                    _diagnosticCompletedAt = DateTimeOffset.Now;
                    _diagnosticSucceeded = snapshot.IsAvailable && input?.IsAvailable != false;
                    StatusMessage = CompletedDiagnosticText;
                    HasInputAccessIssue = input?.IsAvailable == false;
                    if (snapshot.IsAvailable) OutputRecovered?.Invoke();
                });

                return snapshot;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var errorSnapshot = DeviceDiagnosticSnapshot.Unavailable(
                    SelectedOutputMode,
                    _localizationManager.GetString("Status_DiagnosticFailed", "Diagnostic probe failed"),
                    ex.Message);

                RunOnUi(() =>
                {
                    DeviceDiagnosticSnapshot = errorSnapshot;
                    _diagnosticCompletedAt = DateTimeOffset.Now;
                    _diagnosticSucceeded = false;
                    StatusMessage = CompletedDiagnosticText;
                });

                return errorSnapshot;
            }
            catch (OperationCanceledException)
            {
                RunOnUi(() => StatusMessage = _localizationManager.GetString("Status_DeviceCheckCancelled"));
                throw;
            }
            finally
            {
                RunOnUi(() =>
                {
                    IsCheckingDiagnostic = false;
                    SetBusy(false);
                });
            }
        }

        [RelayCommand]
        public async Task BackupVJoyAsync(CancellationToken ct = default)
        {
            if (!ShowVJoySettings || IsBusy) return;
            int id = VJoyDeviceId;
            var profile = CurrentProfile;
            try
            {
                SetBusy(true, _localizationManager.GetString("Output_VJoyBackup"));
                string? path = await _outputDiagnostics.BackupVJoyAsync(id, ct);
                if (!ReferenceEquals(profile, CurrentProfile) || id != VJoyDeviceId) return;
                StatusMessage = path == null ? _localizationManager.GetString("Output_VJoyBackupFailed")
                    : string.Format(_localizationManager.GetString("Output_VJoyBackupSaved"), path);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception error)
            {
                if (ReferenceEquals(profile, CurrentProfile) && id == VJoyDeviceId)
                    StatusMessage = _localizationManager.GetString("Output_VJoyBackupFailed") + " " + error.Message;
            }
            finally { SetBusy(false); }
        }

        [RelayCommand]
        public async Task PrepareWheelAsync(CancellationToken ct = default)
        {
            if (!ShowVJoySettings || IsBusy || _dialogs == null || CurrentProfile == null) return;
            if (_runtime?.IsRunning == true)
            {
                StatusMessage = _localizationManager.GetString("Output_WheelStopFirst");
                return;
            }
            if (await TrySetupSharedVJoyAsync(ct)) return;
            int id = VJoyDeviceId;
            var profile = CurrentProfile;
            bool confirmed = await _dialogs.ShowConfirmAsync(
                _localizationManager.GetString("Output_WheelPrepare"),
                string.Format(_localizationManager.GetString("Output_WheelConfirm"), id),
                _localizationManager.GetString("Output_WheelPrepare"),
                _localizationManager.GetString("Btn_Cancel", "Cancel"), ct: ct);
            if (!confirmed || !ReferenceEquals(profile, CurrentProfile) || VJoyDeviceId != id || _runtime?.IsRunning == true) return;
            try
            {
                SetBusy(true, _localizationManager.GetString("Output_WheelPrepare"));
                bool success = await _outputDiagnostics.ConfigureWheelAsync(id, ct);
                if (!ReferenceEquals(profile, CurrentProfile) || VJoyDeviceId != id) return;
                if (success)
                {
                    using var edit = _profileSession.BeginEditGroup(nameof(PrepareWheelAsync));
                    profile.VJoyWheelCompatibility = true;
                    StatusMessage = _localizationManager.GetString("Output_WheelReady");
                }
                else StatusMessage = _localizationManager.GetString("Output_WheelFailed");
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception error)
            {
                SimRacingHub.Core.AppLogger.Instance.LogError("Wheel configuration failed", error);
                StatusMessage = _localizationManager.GetString("Output_WheelFailed") + " " + error.Message;
            }
            finally { SetBusy(false); }
        }

        [RelayCommand]
        public void ResetCalibration()
        {
            using var edit = _profileSession.BeginEditGroup(nameof(ResetCalibration));
            if (CurrentProfile == null) return;

            CurrentProfile.OutSteerMin = 0;
            CurrentProfile.OutSteerMax = 32768;
            CurrentProfile.InvertSteering = false;

            CurrentProfile.OutGasMin = 0;
            CurrentProfile.OutGasMax = 32768;
            CurrentProfile.InvertGas = false;

            CurrentProfile.OutBrakeMin = 0;
            CurrentProfile.OutBrakeMax = 32768;
            CurrentProfile.InvertBrake = false;

            NotifyAllCalibrationProperties();
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            DisconnectOutputConnectionObserver();
            UnsubscribeEvents();
            GC.SuppressFinalize(this);
        }
    }
}
