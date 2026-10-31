using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimRacingHub.Application.Common;
using SimRacingHub.Application.Contracts;
using SimRacingHub.Application.Fakes;
using SimRacingHub.Application.Localization;
using SimRacingHub.Application.Runtime;
using SimRacingHub.Core;
using SimRacingHub.Models;
using SimRacingHub.Services;

namespace SimRacingHub.Application.Features.Dashboard
{
    public partial class DashboardViewModel : ViewModelBase, IDisposable
    {
        private readonly IRuntimeSession _runtimeSession;
        private readonly ProfileSession _profileSession;
        private readonly IOutputDiagnostics _outputDiagnostics;
        private readonly ILocalizationManager _localizationManager;
        private readonly IUiDispatcher? _uiDispatcher;
        private string? _outputStartFailureMessage;
        public void AcknowledgeOutputRecovery()
        {
            if (IsHubRunning || _runtimeSession.HasFault || _outputStartFailureMessage == null ||
                FaultMessage != _outputStartFailureMessage) return;
            _outputStartFailureMessage = null;
            HasFault = false;
            FaultMessage = null;
            StatusMessage = _localizationManager.GetString("Status_Stopped", "Stopped");
        }

        private bool _isDisposed;
        private bool _eventsSubscribed;
        [ObservableProperty]
        private bool _isHubRunning;
        [ObservableProperty]
        private bool _isLocked;
        [ObservableProperty]
        private bool _hasFault;
        [ObservableProperty]
        private string? _faultMessage;
        [ObservableProperty]
        private string? _statusMessage;
        [ObservableProperty]
        private double _wheelAxis;
        [ObservableProperty]
        private double _gasAxis;
        [ObservableProperty]
        private double _brakeAxis;
        [ObservableProperty]
        private string? _gameTitle;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(GameContextDisplayName))]
        private ProfileContext _currentGameContext = new("Universal");
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasPendingContext))]
        private ProfileContext? _pendingGameContext;
        public bool HasPendingContext => PendingGameContext != null;

        [ObservableProperty]
        private RuntimeSnapshot? _latestSnapshot;
        [ObservableProperty]
        private DeviceDiagnosticSnapshot? _deviceDiagnostics;
        public double WheelPercent => Math.Round(WheelAxis * 100.0, 1);
        public double GasPercent => Math.Round(GasAxis * 100.0, 1);
        public double BrakePercent => Math.Round(BrakeAxis * 100.0, 1);
        public string GameContextDisplayName => CurrentGameContext?.Game is null or "Universal"
            ? _localizationManager.GetString("Common_Universal", "Universal") : CurrentGameContext.Game;

        public int PollingRate
        {
            get => _profileSession.CurrentProfile?.PollingRate ?? _profileSession.ResolvedProfile?.PollingRate ?? 1000;
            set
            {
                var rate = Math.Clamp(value, 50, 1000);
                if (_profileSession.CurrentProfile is { } profile && PollingRate != rate)
                    profile.PollingRate = rate;
            }
        }

        public int CursorUnlockedMode
        {
            get => Math.Clamp(_profileSession.CurrentProfile?.CursorUnlockedMode ?? _profileSession.ResolvedProfile.CursorUnlockedMode, 0, 2);
            set { if (_profileSession.CurrentProfile is {} profile && CursorUnlockedMode != value) profile.CursorUnlockedMode = Math.Clamp(value, 0, 2); }
        }

        public string CursorUnlockedModeLabel => _localizationManager.GetString(CursorUnlockedMode switch
        { 1 => "CursorUnlock_Steering", 2 => "CursorUnlock_All", _ => "CursorUnlock_Continue" });

        public bool ReleaseCursorOnFocusLoss
        {
            get => _profileSession.CurrentProfile?.ReleaseCursorOnFocusLoss ?? _profileSession.ResolvedProfile?.ReleaseCursorOnFocusLoss ?? true;
            set { if (_profileSession.CurrentProfile is { } profile && ReleaseCursorOnFocusLoss != value) profile.ReleaseCursorOnFocusLoss = value; }
        }

        public bool PauseControlsOnFocusLoss
        {
            get => _profileSession.CurrentProfile?.PauseControlsOnFocusLoss ?? _profileSession.ResolvedProfile?.PauseControlsOnFocusLoss ?? true;
            set { if (_profileSession.CurrentProfile is { } profile && PauseControlsOnFocusLoss != value) profile.PauseControlsOnFocusLoss = value; }
        }

        public OutputDeviceMode SelectedOutputMode => _profileSession.CurrentProfile?.OutputMode ?? _profileSession.ResolvedProfile?.OutputMode ?? OutputDeviceMode.VJoy;
        public IRuntimeSession RuntimeSession => _runtimeSession;
        public ProfileSession ProfileSession => _profileSession;
        public IOutputDiagnostics OutputDiagnostics => _outputDiagnostics;
        public ILocalizationManager LocalizationManager => _localizationManager;

        [ObservableProperty]
        private DeviceReadinessViewModel? _readiness;
        [ObservableProperty]
        private SimRacingHub.Application.Features.Output.OutputViewModel? _deviceSetup;
        [ObservableProperty]
        private bool _isDeviceSetupOpen;
        [RelayCommand]
        private void CloseDeviceSetup() => IsDeviceSetupOpen = false;

        public DashboardViewModel(IRuntimeSession runtimeSession, ProfileSession profileSession, IOutputDiagnostics outputDiagnostics, ILocalizationManager localizationManager, IUiDispatcher? uiDispatcher = null)
        {
            _runtimeSession = runtimeSession ?? throw new ArgumentNullException(nameof(runtimeSession));
            _profileSession = profileSession ?? throw new ArgumentNullException(nameof(profileSession));
            _outputDiagnostics = outputDiagnostics ?? throw new ArgumentNullException(nameof(outputDiagnostics));
            ;
            _localizationManager = localizationManager ?? throw new ArgumentNullException(nameof(localizationManager));
            _uiDispatcher = uiDispatcher;
            SyncFromRuntimeSession();
            SyncFromProfileSession();
            SubscribeEvents();
        }

        private void SubscribeEvents()
        {
            if (_eventsSubscribed)
                return;
            _runtimeSession.SnapshotUpdated += OnSnapshotUpdated;
            _runtimeSession.FaultOccurred += OnFaultOccurred;
            _profileSession.UndoStateChanged += OnProfileSessionUndoStateChanged;
            _profileSession.PropertyChanged += OnProfileSessionPropertyChanged;
            _localizationManager.LanguageChanged += OnLanguageChanged;
            _eventsSubscribed = true;
        }

        private void UnsubscribeEvents()
        {
            if (!_eventsSubscribed)
                return;
            _runtimeSession.SnapshotUpdated -= OnSnapshotUpdated;
            _runtimeSession.FaultOccurred -= OnFaultOccurred;
            _profileSession.UndoStateChanged -= OnProfileSessionUndoStateChanged;
            _profileSession.PropertyChanged -= OnProfileSessionPropertyChanged;
            _localizationManager.LanguageChanged -= OnLanguageChanged;
            _eventsSubscribed = false;
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

        private void SyncFromRuntimeSession()
        {
            IsHubRunning = _runtimeSession.IsRunning;
            IsLocked = _runtimeSession.IsLocked;
            HasFault = _runtimeSession.HasFault;
            FaultMessage = _runtimeSession.FaultMessage;
            StatusMessage = HasFault ? FaultMessage : (IsHubRunning ? _localizationManager.GetString("Status_Running", "Running") : _localizationManager.GetString("Status_Stopped", "Stopped"));
            var snapshot = _runtimeSession.GetSnapshot();
            if (snapshot != null)
            {
                ApplySnapshot(snapshot);
            }
        }

        private void SyncFromProfileSession()
        {
            if (CurrentGameContext == null && _profileSession.CurrentProfile != null)
            {
                var profileName = string.IsNullOrWhiteSpace(_profileSession.CurrentProfile.Name) ? "Universal" : _profileSession.CurrentProfile.Name;
                CurrentGameContext = new ProfileContext(profileName);
            };
            OnPropertyChanged(nameof(SelectedOutputMode));
            OnPropertyChanged(nameof(PollingRate));
            OnPropertyChanged(nameof(CursorUnlockedMode));
            OnPropertyChanged(nameof(CursorUnlockedModeLabel));
            OnPropertyChanged(nameof(PauseControlsOnFocusLoss));
            OnPropertyChanged(nameof(ReleaseCursorOnFocusLoss));
            OnPropertyChanged(nameof(GameContextDisplayName));
                OnPropertyChanged(nameof(CursorUnlockedModeLabel));
        }

        private void OnSnapshotUpdated(RuntimeSnapshot snapshot)
        {
            if (snapshot == null)
                return;
            RunOnUi(() => ApplySnapshot(snapshot));
        }

        private void ApplySnapshot(RuntimeSnapshot snapshot)
        {
            LatestSnapshot = snapshot;
            IsHubRunning = snapshot.IsRunning;
            IsLocked = snapshot.IsLocked;
            WheelAxis = snapshot.WheelAxis;
            GasAxis = snapshot.GasAxis;
            BrakeAxis = snapshot.BrakeAxis;
            ;
            ;
            ;
            ;
            GameTitle = snapshot.GameTitle;
            if (snapshot.HasFault)
            {
                HasFault = true;
                FaultMessage = snapshot.FaultMessage;
                StatusMessage = snapshot.FaultMessage ?? _localizationManager.GetString("Status_Fault", "Fault");
            }
            else if (!HasFault)
            {
                StatusMessage = snapshot.StatusMessage ?? (snapshot.IsRunning ? _localizationManager.GetString("Status_Running", "Running") : _localizationManager.GetString("Status_Stopped", "Stopped"));
            }

            OnPropertyChanged(nameof(WheelPercent));
            OnPropertyChanged(nameof(GasPercent));
            OnPropertyChanged(nameof(BrakePercent));
            OnPropertyChanged();
        }

        private void OnFaultOccurred(string message)
        {
            RunOnUi(() =>
            {
                _outputStartFailureMessage = null;
                HasFault = true;
                FaultMessage = message;
                StatusMessage = message;
            });
        }

        private void OnProfileSessionUndoStateChanged(object? sender, EventArgs e)
        {
            RunOnUi(SyncFromProfileSession);
        }

        private void OnProfileSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProfileSession.CurrentProfile) || e.PropertyName == nameof(ProfileSession.ResolvedProfile))
            {
                RunOnUi(SyncFromProfileSession);
            }
        }

        private void OnLanguageChanged(string language)
        {
            RunOnUi(() =>
            {
                OnPropertyChanged(nameof(GameContextDisplayName));
                OnPropertyChanged(nameof(CursorUnlockedModeLabel));
                if (!HasFault)
                {
                    StatusMessage = IsHubRunning ? _localizationManager.GetString("Status_Running", "Running") : _localizationManager.GetString("Status_Stopped", "Stopped");
                }
            });
        }

        [RelayCommand]
        public async Task ToggleHubAsync(CancellationToken ct = default)
        {
            if (IsHubRunning)
            {
                await StopHubAsync(ct).ConfigureAwait(false);
            }
            else
            {
                await StartHubAsync(ct).ConfigureAwait(false);
            }
        }

        [RelayCommand]
        public async Task StartHubAsync(CancellationToken ct = default)
        {
            try
            {
                SetBusy(true, _localizationManager.GetString("Status_StartingHub", "Starting Hub..."));
                var options = new RuntimeStartOptions(OutputMode: SelectedOutputMode, RetainOutputConnection: true, LockCursorOnStart: false)
                {
                    Profile = _profileSession.ResolvedProfile,
                    Context = CurrentGameContext
                };
                await _runtimeSession.StartAsync(options, ct).ConfigureAwait(false);
                RunOnUi(() =>
                {
                    IsHubRunning = _runtimeSession.IsRunning;
                    IsLocked = _runtimeSession.IsLocked;
                    HasFault = _runtimeSession.HasFault;
                    FaultMessage = _runtimeSession.FaultMessage;
                    StatusMessage = HasFault ? FaultMessage : _localizationManager.GetString("Status_Running", "Running");
                });
            }
            catch (Exception ex)when (ex is not OperationCanceledException)
            {
                RunOnUi(() =>
                {
                    _outputStartFailureMessage = _runtimeSession.LastStartFailureWasOutput ? ex.Message : null;
                    HasFault = true;
                    FaultMessage = ex.Message;
                    StatusMessage = ex.Message;
                });
            }
            finally
            {
                SetBusy(false);
            }
        }

        [RelayCommand]
        public async Task StopHubAsync(CancellationToken ct = default)
        {
            try
            {
                SetBusy(true, _localizationManager.GetString("Status_StoppingHub", "Stopping Hub..."));
                await _runtimeSession.StopAsync(ct).ConfigureAwait(false);
                RunOnUi(() =>
                {
                    IsHubRunning = _runtimeSession.IsRunning;
                    IsLocked = _runtimeSession.IsLocked;
                    StatusMessage = HasFault ? FaultMessage : _localizationManager.GetString("Status_Stopped", "Stopped");
                });
            }
            catch (Exception ex)when (ex is not OperationCanceledException)
            {
                RunOnUi(() =>
                {
                    HasFault = true;
                    FaultMessage = ex.Message;
                    StatusMessage = ex.Message;
                });
            }
            finally
            {
                SetBusy(false);
            }
        }

        [RelayCommand]
        public void ToggleLock()
        {
            if (_runtimeSession is IRuntimeCursorControl cursor)
            {
                cursor.UpdateMouseLock(!cursor.IsLocked);
                IsLocked = cursor.IsLocked;
            }
            else if (_runtimeSession is FakeRuntimeSession fake)
            {
                fake.IsLocked = !fake.IsLocked;
                fake.PublishSnapshot(fake.CurrentSnapshot with { IsLocked = fake.IsLocked });
                IsLocked = fake.IsLocked;
            }
            else
            {
                IsLocked = !IsLocked;
            }
        }

        public void SetCurrentContext(ProfileContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            CurrentGameContext = context;
        }

        public void SetPendingContext(ProfileContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            if (CurrentGameContext != null && CurrentGameContext.Equals(context))
            {
                return;
            }

            PendingGameContext = context;
        }

        [RelayCommand]
        public void ApplyPendingContext()
        {
            if (PendingGameContext == null)
                return;
            var target = PendingGameContext;
            PendingGameContext = null;
            CurrentGameContext = target;
            ;
            _profileSession.LoadContext(target);
            if (_runtimeSession.IsRunning && _profileSession.ResolvedProfile != null)
            {
                _runtimeSession.UpdateProfile(_profileSession.ResolvedProfile, target);
            };
            OnPropertyChanged(nameof(SelectedOutputMode));
        }

        [RelayCommand]
        public void DiscardPendingContext()
        {
            PendingGameContext = null;
        }

        [RelayCommand]
        public async Task<DeviceDiagnosticSnapshot> CheckDiagnosticsAsync(CancellationToken ct = default)
        {
            DeviceDiagnosticSnapshot result;
            if (Readiness != null)
            {
                await Readiness.CheckReadinessAsync().ConfigureAwait(false);
                result = Readiness.OutputSnapshot ?? DeviceDiagnosticSnapshot.Unavailable(SelectedOutputMode, Readiness.ProblemTitle);
            }
            else
                result = await _outputDiagnostics.CheckAsync(SelectedOutputMode, ct).ConfigureAwait(false);
            RunOnUi(() =>
            {
                DeviceDiagnostics = result;
            });
            return result;
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;
            _isDisposed = true;
            UnsubscribeEvents();
            GC.SuppressFinalize(this);
        }
    }
}
