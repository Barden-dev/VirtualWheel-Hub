using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimRacingHub.Application.Common;
using SimRacingHub.Application.Contracts;
using SimRacingHub.Application.Features.AppSettings;
using SimRacingHub.Application.Features.Dashboard;
using SimRacingHub.Application.Features.KeyBindings;
using SimRacingHub.Application.Features.Output;
using SimRacingHub.Application.Features.Pedals;
using SimRacingHub.Application.Features.Profiles;
using SimRacingHub.Application.Features.Steering;
using SimRacingHub.Application.Features.Supporters;
using SimRacingHub.Application.Localization;
using SimRacingHub.Application.Runtime;
using SimRacingHub.Core;
using SimRacingHub.Models;
using SimRacingHub.Services;

namespace SimRacingHub.Application.Features.Shell
{
    public partial class AppShellViewModel : ViewModelBase, IAsyncDisposable, IDisposable
    {
        private readonly IRuntimeSession _runtimeSession;
        private readonly ProfileSession _profileSession;
        private readonly IUiDispatcher _uiDispatcher;
        private readonly ILocalizationManager _localizationManager;
        private readonly IAppMetadata _appMetadata;
        private readonly IPlatformCapabilities _platformCapabilities;
        private readonly IUserDialogService _dialogService;
        private readonly ProfileSaveCoordinator _profileSave;
        private readonly ProfileContextChangeCoordinator _contextChanges;
        private bool _eventsSubscribed;
        private bool _adoptingContext;
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


        [ObservableProperty]
        private bool _isInitialized;
        [ObservableProperty]
        private bool _isPreviewMode;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsProfilePage))]
        [NotifyPropertyChangedFor(nameof(IsControlSection))]
        [NotifyPropertyChangedFor(nameof(IsAxesPage))]
        [NotifyPropertyChangedFor(nameof(IsApplicationSection))]
        [NotifyPropertyChangedFor(nameof(IsGameConnectionPage))]
        private PageId _currentPageId = PageId.Dashboard;
        private PageId _lastControlPage = PageId.Steering;
        private PageId _lastAxesPage = PageId.Steering;
        [ObservableProperty] private int _selectedPedalTabIndex;
        public bool IsAxesPage => CurrentPageId is PageId.Steering or PageId.Pedals;
        public AxisControlTab SelectedAxesTab => _lastAxesPage == PageId.Steering ? AxisControlTab.Steering :
            SelectedPedalTabIndex == 0 ? AxisControlTab.Throttle : AxisControlTab.Brake;
        public bool IsSteeringAxisSelected => IsAxesPage && SelectedAxesTab == AxisControlTab.Steering;
        public bool IsThrottleAxisSelected => IsAxesPage && SelectedAxesTab == AxisControlTab.Throttle;
        public bool IsBrakeAxisSelected => IsAxesPage && SelectedAxesTab == AxisControlTab.Brake;

        private void NotifyAxesSelection()
        {
            OnPropertyChanged(nameof(SelectedAxesTab));
            OnPropertyChanged(nameof(IsSteeringAxisSelected));
            OnPropertyChanged(nameof(IsThrottleAxisSelected));
            OnPropertyChanged(nameof(IsBrakeAxisSelected));
        }
        partial void OnSelectedPedalTabIndexChanged(int value) => NotifyAxesSelection();
        [RelayCommand] public void NavigateAxes() => NavigateTo(_lastAxesPage);
        [RelayCommand]
        public void SelectAxesTab(AxisControlTab tab)
        {
            if (tab != AxisControlTab.Steering) SelectedPedalTabIndex = tab == AxisControlTab.Brake ? 1 : 0;
            NavigateTo(tab == AxisControlTab.Steering ? PageId.Steering : PageId.Pedals);
        }

        private PageId _lastApplicationPage = PageId.Settings;
        public bool IsControlSection => CurrentPageId is PageId.Steering or PageId.Pedals or PageId.KeyBindings or PageId.Output or PageId.ControlAssignment or PageId.InputDevices or PageId.SoundPreview or PageId.TrailBraking;
        public bool IsApplicationSection => CurrentPageId is PageId.Settings or PageId.About or PageId.GameDataPreview;
        public bool IsGameConnectionPage => false;

        partial void OnCurrentPageIdChanged(PageId value)
        {
            if (IsAxesPage) _lastAxesPage = value;
            NotifyAxesSelection();
            if (IsControlSection)
                _lastControlPage = value;
            if (IsApplicationSection)
                _lastApplicationPage = value;
            if (value == PageId.About)
                Supporters?.RefreshCommand.Execute(null);
        }

        [ObservableProperty]
        private SupportersViewModel? _supporters;
        [RelayCommand]
        public void NavigateSection(NavigationSection section)
        {
            switch (section)
            {
                case NavigationSection.Dashboard:
                    NavigateTo(PageId.Dashboard);
                    break;
                case NavigationSection.Profiles:
                    NavigateTo(PageId.Profiles);
                    break;
                case NavigationSection.Controls:
                    NavigateTo(_lastControlPage);
                    break;
                case NavigationSection.Application:
                    NavigateTo(_lastApplicationPage);
                    break;
            }
        }

        [ObservableProperty]
        private ProfileContext _currentGameContext;
        public ProfileContext? PendingGameContext => _contextChanges.PendingContext;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HubStatusText))]
        [NotifyPropertyChangedFor(nameof(RuntimeStatusText))]
        private bool _isHubRunning;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RuntimeStatusText))]
        private bool _hasFault;
        [ObservableProperty]
        private string? _faultMessage;
        [ObservableProperty]
        private RuntimeSnapshot? _latestSnapshot;
        [ObservableProperty]
        private bool _isLocked;
        [ObservableProperty]
        private double _wheelAxis;
        [ObservableProperty]
        private double _gasAxis;
        [ObservableProperty]
        private double _brakeAxis;
        [ObservableProperty]
        private string? _gameTitle;
        [ObservableProperty]
        private string? _statusMessage;
        [ObservableProperty]
        private SteeringViewModel? _steering;
        [ObservableProperty]
        private PedalsViewModel? _pedals;
        [ObservableProperty]
        private DashboardViewModel? _dashboard;
        [ObservableProperty]
        private ProfilesViewModel? _profiles;
        partial void OnProfilesChanged(ProfilesViewModel? oldValue, ProfilesViewModel? newValue)
        {
            if (oldValue != null) { oldValue.OpenSettingsRequested -= OnOpenProfileSettings; oldValue.ForzaCatalogChanged -= OnForzaCatalogChanged; }
            if (newValue != null) { newValue.OpenSettingsRequested += OnOpenProfileSettings; newValue.ForzaCatalogChanged += OnForzaCatalogChanged; }
        }

        private void OnForzaCatalogChanged() => OnPropertyChanged(nameof(ProfilePath));

        private void OnOpenProfileSettings(PageId page) => NavigateTo(page);
        [ObservableProperty]
        private OutputViewModel? _output;
        [ObservableProperty]
        private KeyBindingsViewModel? _keyBindings;
        [ObservableProperty]
        private AppSettingsViewModel? _appSettings;
        public event Action<PageId, PageId>? Navigated;
        public IReadOnlyList<PageId> AvailablePages { get; } = Enum.GetValues<PageId>();
        public bool HasPendingContext => PendingGameContext != null;
        public ProfileSession ProfileSession => _profileSession;
        public Profile CurrentProfile => _profileSession.CurrentProfile;
        public ResolvedProfile ResolvedProfile => _profileSession.ResolvedProfile;
        public bool HasUnsavedChanges => _profileSave.HasUnsavedChanges;
        public bool HasProfileSaveError => _profileSave.HasSaveError;
        public bool IsNewProfile => _profileSession.IsNewProfile;
        public bool CanUndo => _profileSave.CanUndo;
        public bool CanSaveProfile => _profileSave.CanSave;
        public bool IsProfilePage => !IsApplicationSection;
        public string ProfilePath => CurrentGameContext?.DisplayPath is null or "Universal" ? _localizationManager.GetString("Common_Universal", "Universal") : DetectedProfileNames.Path(CurrentGameContext, CurrentProfile?.Name);
        public string ProfileStatusText => _localizationManager.GetString(HasUnsavedChanges ? "Ui_ProfileModified" : IsNewProfile ? "Ui_ProfileNew" : "Ui_ProfileSaved");
        public string HubStatusText => _localizationManager.GetString(IsHubRunning ? "Status_HubRunning" : "Status_HubStopped", IsHubRunning ? "Hub running" : "Hub stopped");
        public string RuntimeStatusText => IsPreviewMode ? _localizationManager.GetString("Ui_PreviewMode") : HasFault ? _localizationManager.GetString("Status_Error", "Error") : HubStatusText;

        [RelayCommand]
        public void DiscardAllChanges()
        {
            _profileSave.DiscardAll(CurrentGameContext);
        }

        public string BindingConflictMessage => _profileSession.BindingConflictMessage;
        public string ProfileName => CurrentProfile?.Name ?? CurrentGameContext?.Game ?? "Universal";
        public ILocalizationManager LocalizationManager => _localizationManager;
        private static readonly string[] OfferKeys = ["Offer_Abs", "Offer_Tc", "Offer_TireSound", "Offer_TcSound", "Offer_Speed", "Offer_Menu"];
        public int OfferCount => OfferKeys.Length;
        public string OfferDetails => string.Join("; ", OfferKeys.Select(key => _localizationManager.GetString(key)));
        public TrailBrakingViewModel? TrailBraking { get; set; }

        public string CurrentCultureCode => _localizationManager.CurrentCultureCode;
        public string CurrentLanguageCode => _localizationManager.CurrentLanguageCode;
        public LanguageInfo CurrentLanguage => _localizationManager.CurrentLanguage;
        public IReadOnlyList<LanguageInfo> AvailableLanguages => _localizationManager.AvailableLanguages;
        public IAppMetadata AppMetadata => _appMetadata;
        public string ApplicationName => _appMetadata.ApplicationName;
        public string Version => _appMetadata.Version;
        public string Edition => _appMetadata.Edition;
        public string Copyright => _appMetadata.Copyright;
        public string WindowTitle => $"{_appMetadata.ApplicationName} v{_appMetadata.Version}";
        public IPlatformCapabilities PlatformCapabilities => _platformCapabilities;
        public IUserDialogService DialogService => _dialogService;
        public IUiDispatcher UiDispatcher => _uiDispatcher;
        public IRuntimeSession RuntimeSession => _runtimeSession;

        public AppShellViewModel(IRuntimeSession runtimeSession, ProfileSession profileSession, IUiDispatcher uiDispatcher, ILocalizationManager localizationManager, IAppMetadata appMetadata, IPlatformCapabilities platformCapabilities, IUserDialogService dialogService)
        {
            _runtimeSession = runtimeSession ?? throw new ArgumentNullException(nameof(runtimeSession));
            _profileSession = profileSession ?? throw new ArgumentNullException(nameof(profileSession));
            _uiDispatcher = uiDispatcher ?? throw new ArgumentNullException(nameof(uiDispatcher));
            _localizationManager = localizationManager ?? throw new ArgumentNullException(nameof(localizationManager));
            _appMetadata = appMetadata ?? throw new ArgumentNullException(nameof(appMetadata));
            _platformCapabilities = platformCapabilities ?? throw new ArgumentNullException(nameof(platformCapabilities));
            ;
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _profileSave = new ProfileSaveCoordinator(_profileSession, _runtimeSession);
            _contextChanges = new ProfileContextChangeCoordinator(_profileSave, _dialogService, _localizationManager);
            _profileSave.Changed += ProfileSaveChanged;
            _contextChanges.PendingContextChanged += PendingContextChanged;
            _currentGameContext = new ProfileContext("Universal");
        }

        public Task InitializeAsync(CancellationToken ct = default)
        {
            if (IsInitialized)
            {
                return Task.CompletedTask;
            }

            SubscribeEvents();
            var snapshot = _runtimeSession.GetSnapshot();
            UpdateFromSnapshot(snapshot);
            IsHubRunning = _runtimeSession.IsRunning;
            HasFault = _runtimeSession.HasFault;
            FaultMessage = _runtimeSession.FaultMessage;
            LoadCurrentContext();
            IsInitialized = true;
            return Task.CompletedTask;
        }

        public async Task ShutdownAsync(CancellationToken ct = default)
        {
            if (!IsInitialized)
            {
                return;
            }

            if (_runtimeSession.IsRunning)
            {
                try
                {
                    await _runtimeSession.StopAsync(ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    AppLogger.Instance.LogError($"Failed to stop runtime during shutdown: {ex.Message}");
                }
            }

            UnsubscribeEvents();
            IsInitialized = false;
        }

        public bool NavigateTo(PageId page)
        {
            if (CurrentPageId == page)
            {
                return false;
            }

            var previous = CurrentPageId;
            CurrentPageId = page;
            Navigated?.Invoke(previous, page);
            return true;
        }

        [RelayCommand]
        public void Navigate(PageId page)
        {
            NavigateTo(page);
        }

        [RelayCommand]
        public async Task OpenDeviceDiagnosticsAsync()
        {
            NavigateTo(PageId.Dashboard);
            if (Dashboard != null) Dashboard.IsDeviceSetupOpen = true;
            if (Output != null)
            {
                if (Output.ShowVJoySettings) await Output.InspectVJoyAsync();
                await Output.CheckDiagnosticAsync();
            }
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
                var options = new RuntimeStartOptions(ResolvedProfile.OutputMode)
                {
                    Profile = ResolvedProfile,
                    Context = CurrentGameContext
                };
                await _runtimeSession.StartAsync(options, ct).ConfigureAwait(false);
                RunOnUi(() =>
                {
                    IsHubRunning = _runtimeSession.IsRunning;
                    HasFault = _runtimeSession.HasFault;
                    FaultMessage = _runtimeSession.FaultMessage;
                });
            }
            catch (Exception ex)when (ex is not OperationCanceledException)
            {
                RunOnUi(() =>
                {
                    _outputStartFailureMessage = _runtimeSession.LastStartFailureWasOutput ? ex.Message : null;
                    HasFault = true;
                    FaultMessage = ex.Message;
                });
                await _dialogService.ShowNoticeAsync(new DialogNoticeRequest(_localizationManager.GetString("Title_StartError", "Start Failed"), ex.Message, DialogSeverity.Error), ct).ConfigureAwait(false);
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
                });
            }
            catch (Exception ex)when (ex is not OperationCanceledException)
            {
                RunOnUi(() =>
                {
                    HasFault = true;
                    FaultMessage = ex.Message;
                });
            }
            finally
            {
                SetBusy(false);
            }
        }

        [RelayCommand(CanExecute = nameof(CanSaveProfile))]
        public async Task SaveProfile()
        {
            await ExecuteSaveProfileAsync();
        }

        public bool ExecuteSaveProfile()
        {
            return _profileSave.SaveSynchronously(CurrentGameContext);
        }

        [RelayCommand(CanExecute = nameof(CanUndo))]
        public void RevertProfile()
        {
            _profileSave.Revert(CurrentGameContext);
        }

        public void SetPendingContext(ProfileContext context)
        {
            _contextChanges.SetPendingContext(CurrentGameContext, context, ApplyContext);
        }

        [RelayCommand]
        public void SwitchToPendingContext()
        {
            _contextChanges.SwitchToPendingContext(ApplyContext);
        }

        [RelayCommand]
        public async Task SaveAndSwitchToPendingContext()
        {
            _contextChanges.SaveAndSwitchToPendingContextSynchronously(CurrentGameContext, ApplyContext);
        }

        [RelayCommand]
        public void IgnorePendingContext()
        {
            _contextChanges.IgnorePendingContext();
        }

        public async Task<bool> RequestChangeContextAsync(ProfileContext newContext, CancellationToken ct = default)
        {
            return await _contextChanges.RequestChangeContextAsync(
                CurrentGameContext,
                newContext,
                ProfileName,
                ApplyContext,
                ct);
        }

        public void ApplyContext(ProfileContext newContext)
        {
            ArgumentNullException.ThrowIfNull(newContext);
            CurrentGameContext = newContext;
        }

        public void AdoptLoadedContext(ProfileContext context)
        {
            _contextChanges.ClearPendingContext();
            _adoptingContext = true;
            try
            {
                CurrentGameContext = context;
            }
            finally
            {
                _adoptingContext = false;
            }

            if (_runtimeSession.IsRunning)
                _runtimeSession.UpdateProfile(ResolvedProfile, context);
            ;
            NotifyProfileProperties();
        }

        public void LoadCurrentContext()
        {
            if (CurrentGameContext == null)
                return;
            if (string.IsNullOrWhiteSpace(CurrentGameContext.Game))
            {
                CurrentGameContext.Game = "Universal";
            };
            _profileSession.LoadContext(CurrentGameContext);
            if (_runtimeSession.IsRunning)
            {
                _runtimeSession.UpdateProfile(ResolvedProfile, CurrentGameContext);
            };
            NotifyProfileProperties();
        }

        [RelayCommand]
        public void SetLanguage(string cultureCode)
        {
            ArgumentNullException.ThrowIfNull(cultureCode);
            _localizationManager.SetLanguage(cultureCode, saveToSettings: true);
        }

        [RelayCommand]
        public void ToggleLanguage()
        {
            if (AppSettings != null)
                AppSettings.ToggleLanguage();
            else
                _localizationManager.ToggleLanguage(saveToSettings: !IsPreviewMode);
        }

        partial void OnCurrentGameContextChanged(ProfileContext? oldValue, ProfileContext newValue)
        {
            if (oldValue != null)
            {
                oldValue.PropertyChanged -= OnCurrentGameContextPropertyChanged;
            }

            if (newValue != null)
            {
                newValue.PropertyChanged += OnCurrentGameContextPropertyChanged;
            }

            OnPropertyChanged(nameof(ProfileName));
            if (!_adoptingContext)
                LoadCurrentContext();
        }

        private void OnCurrentGameContextPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(ProfileName));
            LoadCurrentContext();
        }

        private void SubscribeEvents()
        {
            if (_eventsSubscribed)
                return;
            _runtimeSession.SnapshotUpdated += OnRuntimeSnapshotUpdated;
            _runtimeSession.FaultOccurred += OnRuntimeFaultOccurred;
            _profileSession.PropertyChanged += OnProfileSessionPropertyChanged;
            _profileSession.UndoStateChanged += OnProfileSessionUndoStateChanged;
            _profileSession.BindingConflictChanged += OnProfileSessionBindingConflictChanged;
            _localizationManager.LanguageChanged += OnLocalizationLanguageChanged;
            _localizationManager.PropertyChanged += OnLocalizationPropertyChanged;
            if (CurrentGameContext != null)
            {
                CurrentGameContext.PropertyChanged += OnCurrentGameContextPropertyChanged;
            }

            _eventsSubscribed = true;
        }

        private void UnsubscribeEvents()
        {
            if (!_eventsSubscribed)
                return;
            _runtimeSession.SnapshotUpdated -= OnRuntimeSnapshotUpdated;
            _runtimeSession.FaultOccurred -= OnRuntimeFaultOccurred;
            _profileSession.PropertyChanged -= OnProfileSessionPropertyChanged;
            _profileSession.UndoStateChanged -= OnProfileSessionUndoStateChanged;
            _profileSession.BindingConflictChanged -= OnProfileSessionBindingConflictChanged;
            _localizationManager.LanguageChanged -= OnLocalizationLanguageChanged;
            _localizationManager.PropertyChanged -= OnLocalizationPropertyChanged;
            if (CurrentGameContext != null)
            {
                CurrentGameContext.PropertyChanged -= OnCurrentGameContextPropertyChanged;
            }

            _eventsSubscribed = false;
        }

        private void OnRuntimeSnapshotUpdated(RuntimeSnapshot snapshot)
        {
            RunOnUi(() =>
            {
                UpdateFromSnapshot(snapshot);
            });
        }

        private void OnRuntimeFaultOccurred(string faultMessage)
        {
            RunOnUi(() =>
            {
                _outputStartFailureMessage = null;
                HasFault = true;
                FaultMessage = faultMessage;
                _ = _dialogService.ShowNoticeAsync(new DialogNoticeRequest(_localizationManager.GetString("Title_RuntimeFault", "Runtime Fault"), faultMessage, DialogSeverity.Error));
            });
        }

        private void UpdateFromSnapshot(RuntimeSnapshot snapshot)
        {
            LatestSnapshot = snapshot;
            IsHubRunning = snapshot.IsRunning;
            HasFault = snapshot.HasFault;
            FaultMessage = snapshot.FaultMessage;
            IsLocked = snapshot.IsLocked;
            WheelAxis = snapshot.WheelAxis;
            GasAxis = snapshot.GasAxis;
            BrakeAxis = snapshot.BrakeAxis;
            ;
            ;
            ;
            ;
            GameTitle = snapshot.GameTitle;
            StatusMessage = snapshot.StatusMessage;
            double wheel = snapshot.IsRunning ? snapshot.WheelAxis : 0;
            double gas = snapshot.IsRunning ? snapshot.GasAxis : 0;
            double brake = snapshot.IsRunning ? snapshot.BrakeAxis : 0;
            if (Steering != null)
            {
                Steering.Mouse.SteerValue = (int)Math.Round((wheel + 1) * 16384);
                Steering.InputValue = snapshot.IsRunning ? snapshot.Inputs?.Wheel ?? wheel : 0;
                Steering.Mouse.LiveOutput = wheel;
            }

            if (Pedals != null)
            {
                Pedals.Output.ThrottleInput = snapshot.IsRunning ? snapshot.Inputs?.Gas ?? gas : 0;
                Pedals.Output.BrakeInput = snapshot.IsRunning ? snapshot.Inputs?.Brake ?? brake : 0;
                Pedals.Output.ThrottleValue = gas;
                Pedals.Output.BrakeValue = brake;
                Pedals.Output.ThrottleRaw = (int)Math.Round(gas * 32768);
                Pedals.Output.BrakeRaw = (int)Math.Round(brake * 32768);
            }
        }

        private void OnProfileSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            RunOnUi(() =>
            {
                NotifyEditingLabels();
                if (e.PropertyName == nameof(ProfileSession.HasSaveError))
                {
                    OnPropertyChanged(nameof(HasProfileSaveError));
                }
                else if (e.PropertyName == nameof(ProfileSession.HasUnsavedChanges))
                {
                    OnPropertyChanged(nameof(HasUnsavedChanges));
                    OnPropertyChanged(nameof(CanUndo));
                    SaveProfileCommand.NotifyCanExecuteChanged();
                    RevertProfileCommand.NotifyCanExecuteChanged();
                }
                else if (e.PropertyName == nameof(ProfileSession.CanUndo))
                {
                    OnPropertyChanged(nameof(CanUndo));
                    RevertProfileCommand.NotifyCanExecuteChanged();
                }
                else if (e.PropertyName == nameof(ProfileSession.IsNewProfile))
                {
                    OnPropertyChanged(nameof(IsNewProfile));
                }
                else if (e.PropertyName == nameof(ProfileSession.CurrentProfile))
                {
                    OnPropertyChanged(nameof(CurrentProfile));
                    OnPropertyChanged(nameof(ProfileName));
                    ;
                }
                else if (e.PropertyName == nameof(ProfileSession.ResolvedProfile))
                {
                    OnPropertyChanged(nameof(ResolvedProfile));
                    ;
                }
            });
        }

        private void OnProfileSessionUndoStateChanged(object? sender, EventArgs e)
        {
            RunOnUi(() =>
            {
                ;
                NotifyEditingLabels();
                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(HasUnsavedChanges));
                RevertProfileCommand.NotifyCanExecuteChanged();
                SaveProfileCommand.NotifyCanExecuteChanged();
            });
        }

        private void OnProfileSessionBindingConflictChanged(object? sender, EventArgs e)
        {
            RunOnUi(() =>
            {
                OnPropertyChanged(nameof(BindingConflictMessage));
            });
        }

        private void OnLocalizationLanguageChanged(string cultureCode)
        {
            RunOnUi(() =>
            {
                NotifyEditingLabels();
                OnPropertyChanged(nameof(CurrentCultureCode));
                OnPropertyChanged(nameof(OfferDetails));
                OnPropertyChanged(nameof(CurrentLanguageCode));
                OnPropertyChanged(nameof(CurrentLanguage));
                OnPropertyChanged(nameof(WindowTitle));
            });
        }

        private void OnLocalizationPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            RunOnUi(() =>
            {
                NotifyEditingLabels();
                OnPropertyChanged(nameof(CurrentCultureCode));
                OnPropertyChanged(nameof(OfferDetails));
                OnPropertyChanged(nameof(CurrentLanguageCode));
                OnPropertyChanged(nameof(CurrentLanguage));
                OnPropertyChanged(nameof(WindowTitle));
            });
        }

        private void NotifyProfileProperties()
        {
            RunOnUi(() =>
            {
                NotifyEditingLabels();
                OnPropertyChanged(nameof(CurrentProfile));
                OnPropertyChanged(nameof(ResolvedProfile));
                OnPropertyChanged(nameof(HasUnsavedChanges));
                OnPropertyChanged(nameof(IsNewProfile));
                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(BindingConflictMessage));
                OnPropertyChanged(nameof(ProfileName));
                SaveProfileCommand.NotifyCanExecuteChanged();
                RevertProfileCommand.NotifyCanExecuteChanged();
            });
        }

        private void NotifyEditingLabels()
        {
            NotifyVehicleStatus();
            OnPropertyChanged(nameof(CanSaveProfile));
            OnPropertyChanged(nameof(ProfileStatusText));
            OnPropertyChanged(nameof(ProfilePath));
            OnPropertyChanged(nameof(HubStatusText));
            OnPropertyChanged(nameof(RuntimeStatusText));
        }

        private void RunOnUi(Action action)
        {
            if (_uiDispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                _uiDispatcher.Post(action);
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (Profiles != null)
                Profiles.OpenSettingsRequested -= OnOpenProfileSettings;
            TrailBraking?.Dispose();
            Dashboard?.Dispose();
            ;
            Output?.Dispose();
            KeyBindings?.Dispose();
            AppSettings?.Dispose();
            _profileSave.Changed -= ProfileSaveChanged;
            _contextChanges.PendingContextChanged -= PendingContextChanged;
            _profileSave.Dispose();
            await ShutdownAsync().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }

        public void Dispose()
        {
            if (Profiles != null)
                Profiles.OpenSettingsRequested -= OnOpenProfileSettings;
            TrailBraking?.Dispose();
            Dashboard?.Dispose();
            ;
            Output?.Dispose();
            KeyBindings?.Dispose();
            AppSettings?.Dispose();
            _profileSave.Changed -= ProfileSaveChanged;
            _contextChanges.PendingContextChanged -= PendingContextChanged;
            _profileSave.Dispose();
            UnsubscribeEvents();
            GC.SuppressFinalize(this);
        }
    }
}
