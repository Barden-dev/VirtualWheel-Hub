using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimRacingHub.Core;
using SimRacingHub.Input;
using SimRacingHub.Models;
using SimRacingHub.Services;
using SimRacingHub.Services.Telemetry;
using SimRacingHub.Services.Plugins;
using System.Collections.Generic;
using SimRacingHub.Views.Dialogs;

namespace SimRacingHub.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly ProfileManager _profileManager;
        private readonly WindowManager _windowManager;
        private readonly GameIntegrationRegistry _registry;
        private readonly VJoyDiagnosticService _vJoyDiagnosticService;
        private readonly UpdateService _updateService;
        private readonly HubRuntime _hubRuntime;
        private readonly HotkeyService _hotkeyService;
        private readonly ProfileSharingService _profileSharingService = new ProfileSharingService();
        public ProfileSharingService ProfileSharingService => _profileSharingService;
        private GameDetectionService _gameDetection;
        public GameDetectionService GameDetection => _gameDetection;
        private readonly LmuInputFixService _lmuInputFixService = new LmuInputFixService();
        private string _lastWarnedLmuFixPath = string.Empty;

        [ObservableProperty]
        private LmuInputFixStatus _lmuInputFix = new LmuInputFixStatus();

        private readonly GamePluginManager _pluginManager = new GamePluginManager();

        [ObservableProperty]
        private GamePluginDiagnosticResult _gamePluginDiagnostic = new();

        [ObservableProperty]
        private VJoyDiagnosticResult _vJoyDiagnostic = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanRecheck))]
        [NotifyPropertyChangedFor(nameof(RecheckButtonText))]
        [NotifyPropertyChangedFor(nameof(RecheckButtonIcon))]
        private bool _isDiagnosticRunning;

        public bool CanRecheck => !IsDiagnosticRunning;
        public string RecheckButtonText => IsDiagnosticRunning ? "Checking..." : "Re-check";
        public Wpf.Ui.Controls.SymbolRegular RecheckButtonIcon => IsDiagnosticRunning ? Wpf.Ui.Controls.SymbolRegular.ArrowSync24 : Wpf.Ui.Controls.SymbolRegular.ArrowClockwise24;

        [ObservableProperty]
        private bool _isUpdateAvailable;

        [ObservableProperty]
        private string _latestVersion = string.Empty;

        [ObservableProperty]
        private string _updateUrl = string.Empty;

        public string CurrentVersion => UpdateService.CurrentVersion;
        public bool IsProVersion => UpdateService.IsProVersion;

        private readonly ProfileSession _profileSession;
        
        public Profile CurrentProfile => _profileSession.CurrentProfile;
        public ResolvedProfile ResolvedProfile => _profileSession.ResolvedProfile;
        
        public bool HasUnsavedChanges => _profileSession.HasUnsavedChanges;
        public bool IsNewProfile => _profileSession.IsNewProfile;
        public bool CanUndo => _profileSession.CanUndo;
        public string BindingConflictMessage => _profileSession.BindingConflictMessage;
        public bool HasBindingConflictMessage => !string.IsNullOrEmpty(BindingConflictMessage);
        
        [ObservableProperty]
        private ProfileContext _currentContext;

        [ObservableProperty]
        private bool _isOutputMonitorExpanded;

        public bool SupportsAbs => HasFeature("Abs");
        public bool SupportsTc => HasFeature("Tc");
        public bool SupportsSlipAudio => HasFeature("SlipAudio");
        public bool SupportsSpeedSens => HasFeature("SpeedSens");
        public bool SupportsMenuControls => HasFeature("MenuControls");
        public bool SupportsTcAudio => HasFeature("TcAudio") && ResolvedProfile != null && ResolvedProfile.UseTcHelper;

        public int TotalProFeaturesCount
        {
            get
            {
                var builtInProFeatures = new[] { "Abs", "Tc", "TcAudio", "SlipAudio", "SpeedSens", "TrailBraking", "MenuControls" };

                return _registry.GetAll()
                    .Where(i => i.Features != null)
                    .SelectMany(i => i.Features)
                    .Select(f => f.Id)
                    .Concat(builtInProFeatures)
                    .Distinct()
                    .Count();
            }
        }

        public string AbsSupportedGamesText => GetSupportedGamesText("Abs");
        public string TcSupportedGamesText => GetSupportedGamesText("Tc");
        public string SlipAudioSupportedGamesText => GetSupportedGamesText("SlipAudio");
        public string SpeedSensSupportedGamesText => GetSupportedGamesText("SpeedSens");
        public string MenuControlsSupportedGamesText => GetSupportedGamesText("MenuControls");

        public bool HasFeature(string featureId)
        {
            if (CurrentContext == null || string.IsNullOrEmpty(CurrentContext.Game)) return false;
            var integration = _registry.GetIntegration(CurrentContext.Game);
            if (integration == null || integration.Features == null) return false;
            return integration.Features.Any(f => f.Id == featureId && f.IsSupported);
        }

        public string GetSupportedGamesText(string featureId)
        {
            var games = _registry.GetAll()
                .Where(i => i.Features != null && i.Features.Any(f => f.Id == featureId && f.IsSupported))
                .Select(i => i.DisplayName)
                .ToList();

            if (games.Count == 0) return "Supported games: None";
            return $"Supported games: {string.Join(", ", games)}";
        }

        [ObservableProperty]
        private ObservableCollection<string> _availableGames = new();
        [ObservableProperty]
        private ObservableCollection<string> _availableClasses = new();
        [ObservableProperty]
        private ObservableCollection<string> _availableCars = new();


        
        private ProfileContext? _oldContext;
        partial void OnCurrentContextChanged(ProfileContext value)
        {
            if (_oldContext != null)
                _oldContext.PropertyChanged -= CurrentContext_PropertyChanged;
            
            if (value != null)
                value.PropertyChanged += CurrentContext_PropertyChanged;
                
            _oldContext = value;
            System.Windows.Application.Current?.Dispatcher.Invoke(() => 
            {
                LoadCurrentContext();
                RefreshGamePluginStatus();
                OnPropertyChanged(nameof(SupportsAbs));
                OnPropertyChanged(nameof(SupportsTc));
                OnPropertyChanged(nameof(SupportsSlipAudio));
                OnPropertyChanged(nameof(SupportsSpeedSens));
                OnPropertyChanged(nameof(SupportsMenuControls));
                OnPropertyChanged(nameof(SupportsTcAudio));
                OnPropertyChanged(nameof(MenuControlsSupportedGamesText));
            });
        }
        
        private void CurrentContext_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProfileContext.Game))
            {
                OnPropertyChanged(nameof(SupportsAbs));
                OnPropertyChanged(nameof(SupportsTc));
                OnPropertyChanged(nameof(SupportsSlipAudio));
                OnPropertyChanged(nameof(SupportsSpeedSens));
                OnPropertyChanged(nameof(SupportsMenuControls));
                OnPropertyChanged(nameof(SupportsTcAudio));
                OnPropertyChanged(nameof(MenuControlsSupportedGamesText));
            }
            System.Windows.Application.Current?.Dispatcher.Invoke(() => 
            {
                LoadCurrentContext();
                RefreshGamePluginStatus();
            });
        }

        private string _lastWarnedGameForMissingPlugin = string.Empty;

        public void RefreshGamePluginStatus()
        {
            if (CurrentContext != null && !string.IsNullOrEmpty(CurrentContext.Game))
            {
                GamePluginDiagnostic = _pluginManager.CheckPluginStatus(CurrentContext.Game);
                if (GamePluginDiagnostic.Status == PluginStatus.MissingPlugin ||
                    GamePluginDiagnostic.Status == PluginStatus.OutdatedPlugin ||
                    GamePluginDiagnostic.Status == PluginStatus.ConfigurationError)
                {
                    if (_lastWarnedGameForMissingPlugin != CurrentContext.Game)
                    {
                        _lastWarnedGameForMissingPlugin = CurrentContext.Game;

                        AudioUtil.PlayErrorSound();
                    }
                }
                else
                {
                    _lastWarnedGameForMissingPlugin = string.Empty;
                }
            }
            else
            {
                GamePluginDiagnostic = new SimRacingHub.Services.Plugins.GamePluginDiagnosticResult
                {
                    Status = SimRacingHub.Services.Plugins.PluginStatus.Installed,
                    StatusMessage = "No game context selected."
                };
                _lastWarnedGameForMissingPlugin = string.Empty;
            }
        }

        [RelayCommand]
        public void RefreshLmuInputFixStatus()
        {
            LmuInputFix = _lmuInputFixService.Check(CurrentContext?.Game);
        }

        [RelayCommand]
        public void ApplyLmuInputFix()
        {
            RefreshLmuInputFixStatus();

            if (!LmuInputFix.IsAvailable)
            {
                System.Windows.MessageBox.Show(
                    LmuInputFix.DetailText,
                    "LMU Input Fix",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (_pluginManager.IsGameRunning("Le Mans Ultimate"))
            {
                System.Windows.MessageBox.Show(
                    "Le Mans Ultimate is currently running. Close the game completely and try again. Its controls file must not be edited while the game is running.",
                    "Close LMU first",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            var confirmation = System.Windows.MessageBox.Show(
                "vWheel Hub will change only \"DirectInput Fallback\" in current controls.json.\n\nA backup will be created first. LMU must be restarted completely after the fix. Continue?",
                "Fix LMU input",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (confirmation != System.Windows.MessageBoxResult.Yes) return;

            var result = _lmuInputFixService.Apply();
            RefreshLmuInputFixStatus();

            if (result.Success && LmuInputFix.CanRevert)
            {
                RefreshGamePluginStatus();
                string telemetryNote = GamePluginDiagnostic.IsActionRequired
                    ? "\n\nThe optional LMU telemetry plugin still needs attention on the Dashboard. This does not undo the input fix; only telemetry-based helpers may be unavailable."
                    : string.Empty;
                var launch = System.Windows.MessageBox.Show(
                    result.Message + telemetryNote + "\n\nLaunch Le Mans Ultimate now?",
                    "LMU input fixed",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Information);

                if (launch == System.Windows.MessageBoxResult.Yes)
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo("steam://run/2399420") { UseShellExecute = true });
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Instance.LogError("Failed to launch LMU through Steam", ex);
                    }
                }
            }
            else
            {
                System.Windows.MessageBox.Show(
                    result.Message,
                    result.Success ? "LMU input settings" : "LMU input fix failed",
                    System.Windows.MessageBoxButton.OK,
                    result.Success ? System.Windows.MessageBoxImage.Information : System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        public void RevertLmuInputFix()
        {
            RefreshLmuInputFixStatus();

            if (!LmuInputFix.CanRevert)
            {
                System.Windows.MessageBox.Show(
                    "There is no safe backup created by vWheel Hub for the current LMU controls file.",
                    "LMU Input Fix",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return;
            }

            var confirmation = System.Windows.MessageBox.Show(
                "The current controls.json saved immediately before the fix will be restored.\n\nRestore is allowed only if LMU has not changed the file since the fix was applied. Continue?",
                "Revert LMU input fix",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (confirmation != System.Windows.MessageBoxResult.Yes) return;

            var result = _lmuInputFixService.Revert();
            RefreshLmuInputFixStatus();

            System.Windows.MessageBox.Show(
                result.Message,
                result.Success ? "LMU input fix reverted" : "LMU input revert failed",
                System.Windows.MessageBoxButton.OK,
                result.Success ? System.Windows.MessageBoxImage.Information : System.Windows.MessageBoxImage.Warning);
        }

        [RelayCommand]
        public void OpenLmuInputFixFile()
        {
            _lmuInputFixService.OpenControlsFile();
        }

        private bool? ConfirmCloseRunningGame(string gameId)
        {
            if (!_pluginManager.IsGameRunning(gameId)) return false;

            string displayName = _pluginManager.GetDisplayName(gameId);
            var answer = System.Windows.MessageBox.Show(
                $"{displayName} is running.\n\nA full game restart is required for telemetry setup. Close {displayName} now and install it?",
                "Telemetry Plugin Setup",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            return answer == System.Windows.MessageBoxResult.Yes ? true : null;
        }

        private bool ConfirmForceCloseGame(string displayName)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null) return false;

            return dispatcher.Invoke(() => System.Windows.MessageBox.Show(
                $"{displayName} did not close within 10 seconds.\n\nForce it to close now? Any unsaved progress in the game will be lost.",
                "Telemetry Plugin Setup",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning) == System.Windows.MessageBoxResult.Yes);
        }

        [RelayCommand]
        public void InstallGamePlugin()
        {
            if (CurrentContext == null || string.IsNullOrEmpty(CurrentContext.Game)) return;

            bool? autoClose = ConfirmCloseRunningGame(CurrentContext.Game);
            if (autoClose == null) return;

            var (success, msg) = _pluginManager.InstallPlugin(CurrentContext.Game, autoCloseGame: autoClose.Value);
            if (success)
            {
                AudioUtil.PlaySuccessSound();
                System.Windows.MessageBox.Show(msg, "Telemetry Plugin Setup", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            else
            {
                System.Windows.MessageBox.Show(msg, "Telemetry Plugin Setup Warning", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            RefreshGamePluginStatus();
        }

        [RelayCommand]
        public void LocateGameFolder()
        {
            if (CurrentContext == null || string.IsNullOrEmpty(CurrentContext.Game)) return;

            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = $"Select installation directory for {CurrentContext.Game}"
            };

            if (dialog.ShowDialog() == true)
            {
                string selectedFolder = dialog.FolderName;

                bool? autoClose = ConfirmCloseRunningGame(CurrentContext.Game);
                if (autoClose == null) return;

                var (success, msg) = _pluginManager.InstallPlugin(CurrentContext.Game, selectedFolder, autoCloseGame: autoClose.Value);
                if (success)
                {
                    AudioUtil.PlaySuccessSound();
                    System.Windows.MessageBox.Show(msg, "Telemetry Plugin Setup", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                else
                {
                    System.Windows.MessageBox.Show(msg, "Telemetry Plugin Setup Warning", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                }
                RefreshGamePluginStatus();
            }
        }

        [ObservableProperty]
        private bool _isActive;

        [ObservableProperty]
        private bool _lockMouse;

        public void ReapplyMouseLock()
        {
            if (LockMouse)
            {
                _hubRuntime?.ReapplyMouseLock();
            }
        }
        
        [ObservableProperty]
        private int _steerValue;
        [ObservableProperty]
        private int _gasValue;
        [ObservableProperty]
        private int _brakeValue;

        // HWND required for mouse locking
        public IntPtr WindowHandle { get; set; }

        [ObservableProperty]
        private bool _autoStartHub;
        
        partial void OnAutoStartHubChanged(bool value)
        {
            SaveAppSettings();
        }

        [ObservableProperty]
        private bool _autoDetectOnStartup = true;

        partial void OnAutoDetectOnStartupChanged(bool value)
        {
            SaveAppSettings();
        }

        public void SaveAppSettings()
        {
            new AppSettings
            {
                AutoStartHub = AutoStartHub,
                AutoDetectOnStartup = AutoDetectOnStartup
            }.Save();
        }

        [ObservableProperty]
        private ProfileContext? _pendingContext;

        public MainViewModel(ProfileManager profileManager, WindowManager windowManager, GameIntegrationRegistry registry, VJoyDiagnosticService vJoyDiagnosticService, UpdateService updateService)
        {
            _profileManager = profileManager;
            _windowManager = windowManager;
            _registry = registry;
            _vJoyDiagnosticService = vJoyDiagnosticService;
            _updateService = updateService;

            _pluginManager.ConfirmForceClose = ConfirmForceCloseGame;

            _ = RunVJoyDiagnosticAsync();
            _ = CheckForUpdatesAsync();
            
            _profileSession = new ProfileSession(profileManager);
            _profileSession.UndoStateChanged += (s, e) => 
            {
                OnPropertyChanged(nameof(CurrentProfile));
                OnPropertyChanged(nameof(ResolvedProfile));
                OnPropertyChanged(nameof(HasUnsavedChanges));
                OnPropertyChanged(nameof(IsNewProfile));
                OnPropertyChanged(nameof(CanUndo));
                RevertProfileCommand.NotifyCanExecuteChanged();
            };
            _profileSession.BindingConflictChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(BindingConflictMessage));
                OnPropertyChanged(nameof(HasBindingConflictMessage));
            };
            
            _hubRuntime = new HubRuntime(registry, windowManager);
            _hubRuntime.OnFault += OnHubFault;
            _hubRuntime.OnMouseLockStateChanged += (locked) =>
            {
                System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => LockMouse = locked);
            };

            _hotkeyService = new HotkeyService();

            _hotkeyService.OnToggleHub += () => 
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() => IsActive = !IsActive);
            };
            
            _hotkeyService.OnToggleLock += () => 
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() => LockMouse = !LockMouse);
            };
            
            _gameDetection = new GameDetectionService(registry);
            _gameDetection.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(GameDetectionService.AutoDetectEnabled))
                {
                    if (!_gameDetection.AutoDetectEnabled)
                    {
                        System.Windows.Application.Current?.Dispatcher.Invoke(() => PendingContext = null);
                    }
                }
            };
            _gameDetection.OnContextChanged += (ctx) => 
            {
                if (HasUnsavedChanges && CurrentContext != null && !CurrentContext.Equals(ctx))
                {
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        PendingContext = ctx;
                        AppLogger.Instance.LogInfo($"Auto-detected '{ctx.DisplayPath}', but you have unsaved changes.");
                        if (string.Equals(ctx.Game, "Universal", StringComparison.OrdinalIgnoreCase) && LockMouse)
                        {
                            LockMouse = false;
                        }
                    });
                }
                else
                {
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        PendingContext = null;
                        CurrentContext = ctx;
                        if (string.Equals(ctx.Game, "Universal", StringComparison.OrdinalIgnoreCase) && LockMouse)
                        {
                            LockMouse = false;
                        }
                    });
                }
            };
            
            CurrentContext = new ProfileContext("Universal");
            
            // By default false, bound to UI toggle
            LockMouse = false;
            
            var appSettings = AppSettings.Load();
            _autoStartHub = appSettings.AutoStartHub;
            _autoDetectOnStartup = appSettings.ResolvedAutoDetectOnStartup;
            
            _gameDetection.AutoDetectEnabled = AutoDetectOnStartup;
            if (AutoDetectOnStartup)
            {
                _gameDetection.Start();
            }

            _hotkeyService.Start();
        }

        partial void OnLockMouseChanged(bool value)
        {
            if (IsActive)
            {
                _hubRuntime.UpdateMouseLock(value);
            }
        }

        partial void OnIsActiveChanged(bool value)
        {
            if (value)
            {
                StartHub();
            }
            else
            {
                StopHub();
            }
        }

        private long _lastUiUpdateTimestamp;
        private CancellationTokenSource? _vJoyDiagnosticLoopCts;

        [RelayCommand]
        public async Task RunVJoyDiagnosticAsync()
        {
            if (IsDiagnosticRunning) return;

            IsDiagnosticRunning = true;
            try
            {
                var diag = await Task.Run(() => _vJoyDiagnosticService.PerformDiagnostic());
                VJoyDiagnostic = diag;

                if (!VJoyDiagnostic.IsValid)
                {
                    AppLogger.Instance.LogError($"vJoy Diagnostic Warning: {VJoyDiagnostic.SummaryMessage}");
                    EnsureVJoyPeriodicCheckRunning();
                }
                else
                {
                    StopVJoyPeriodicCheck();
                }
            }
            finally
            {
                IsDiagnosticRunning = false;
            }
        }

        private void EnsureVJoyPeriodicCheckRunning()
        {
            if (_vJoyDiagnosticLoopCts != null && !_vJoyDiagnosticLoopCts.IsCancellationRequested)
                return;

            _vJoyDiagnosticLoopCts = new CancellationTokenSource();
            var token = _vJoyDiagnosticLoopCts.Token;

            _ = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(5000, token);

                        var diag = _vJoyDiagnosticService.PerformDiagnostic();

                        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                        {
                            VJoyDiagnostic = diag;
                        });

                        if (diag.IsValid)
                        {
                            AppLogger.Instance.LogInfo("vJoy issue resolved. Stopping 5-second periodic auto-check.");
                            StopVJoyPeriodicCheck();
                            break;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Instance.LogError("Error in vJoy 5-second periodic check loop", ex);
                    }
                }
            }, token);
        }

        private void StopVJoyPeriodicCheck()
        {
            try
            {
                if (_vJoyDiagnosticLoopCts != null)
                {
                    _vJoyDiagnosticLoopCts.Cancel();
                    _vJoyDiagnosticLoopCts.Dispose();
                    _vJoyDiagnosticLoopCts = null;
                }
            }
            catch (ObjectDisposedException)
            {
                _vJoyDiagnosticLoopCts = null;
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError("Failed to stop the periodic vJoy check", ex);
            }
        }

        [RelayCommand]
        public void OpenVJoyDownload()
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://github.com/jshafer817/vJoy/releases") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError("Failed to open vJoy download link", ex);
            }
        }

        [RelayCommand]
        public void OpenVJoyConfig()
        {
            bool launched = VJoyDiagnosticService.TryLaunchVJoyConfig();
            if (!launched)
            {
                OpenVJoyDownload();
            }
        }

        [RelayCommand]
        public async Task AutoConfigureVJoyAsync()
        {
            var dialogResult = System.Windows.MessageBox.Show(
                "vWheel Hub will automatically configure vJoy Device #1:\n\n" +
                "• Axes: X (Steering), Y (Gas), Z (Brake)\n" +
                "• Buttons: 8 Buttons\n\n" +
                "⚠️ Windows Administrator Confirmation (UAC):\n" +
                "Windows will present a popup asking for Administrator permission.\n\n" +
                "If you do not wish to grant Administrator permission, click 'No' and use 'Open GUI' or 'Open Folder' to configure vJoy manually.\n\n" +
                "Do you want to proceed with Auto-Fix?",
                "⚡ Auto-Configure vJoy",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Information);

            if (dialogResult == System.Windows.MessageBoxResult.Yes)
            {
                var (success, msg) = await VJoyDiagnosticService.TryAutoConfigureVJoy(1);

                if (success)
                {
                    AppLogger.Instance.LogInfo(msg);
                }
                else
                {
                    AppLogger.Instance.LogWarning(msg);
                }

                await RunVJoyDiagnosticAsync();
            }
        }

        [RelayCommand]
        public void OpenVJoyFolder()
        {
            VJoyDiagnosticService.TryOpenVJoyFolder();
        }

        [RelayCommand]
        public void OpenDiscord()
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://discord.gg/YQn5eh2vfc") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError("Failed to open Discord link", ex);
            }
        }

        [RelayCommand]
        public void OpenBoosty()
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://boosty.to/barden_dev") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError("Failed to open Boosty link", ex);
            }
        }

        [RelayCommand]
        public void OpenGithub()
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://github.com/Barden-dev/vWheel-Hub") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError("Failed to open GitHub link", ex);
            }
        }

        [RelayCommand]
        public void OpenWiki()
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://github.com/Barden-dev/vWheel-Hub/wiki") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError("Failed to open Wiki link", ex);
            }
        }

        [RelayCommand]
        public async Task CheckForUpdatesAsync()
        {
            try
            {
                var result = await _updateService.CheckForUpdatesAsync();
                if (result != null && result.IsUpdateAvailable)
                {
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        LatestVersion = result.LatestVersion;
                        UpdateUrl = result.UpdateUrl;
                        IsUpdateAvailable = true;
                    });
                }
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError("Error in CheckForUpdatesAsync", ex);
            }
        }

        [RelayCommand]
        public void OpenUpdateUrl()
        {
            try
            {
                string targetUrl = string.IsNullOrWhiteSpace(UpdateUrl) ? _updateService.TargetUpdateUrl : UpdateUrl;
                Process.Start(new ProcessStartInfo(targetUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError("Failed to open update URL", ex);
            }
        }

        [RelayCommand]
        public void DismissUpdateBanner()
        {
            IsUpdateAvailable = false;
        }

        private void StartHub()
        {
            _lastUiUpdateTimestamp = 0;

            RefreshLmuInputFixStatus();
            if (LmuInputFix.NeedsFix &&
                !string.Equals(_lastWarnedLmuFixPath, LmuInputFix.ControlsPath, StringComparison.OrdinalIgnoreCase))
            {
                _lastWarnedLmuFixPath = LmuInputFix.ControlsPath;
                var continueWithoutFix = System.Windows.MessageBox.Show(
                    "LMU has DirectInput Fallback disabled. With vJoy, this may cause steering and pedal axes to stutter or jump.\n\nClose LMU and use 'Fix LMU input' on the Dashboard to enable the compatible input path. Continue without the fix for now?",
                    "LMU input compatibility",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (continueWithoutFix != System.Windows.MessageBoxResult.Yes)
                {
                    IsActive = false;
                    return;
                }
            }

            var diag = VJoyDiagnostic;
            if (diag == null || !diag.IsValid)
            {
                diag = _vJoyDiagnosticService.PerformDiagnostic();
                VJoyDiagnostic = diag;
            }

            if (!diag.IsValid)
            {
                IsActive = false;
                AppLogger.Instance.LogError($"Cannot start Hub: {diag.SummaryMessage}. {diag.ActionableAdvice}");

                _ = System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
                {
                    var uiBox = new Wpf.Ui.Controls.MessageBox
                    {
                        Title = "vJoy Configuration Error",
                        Content = $"{diag.SummaryMessage}\n\nInstructions:\n{diag.ActionableAdvice}",
                        CloseButtonText = "OK"
                    };
                    _ = uiBox.ShowDialogAsync();
                });
                return;
            }

            bool started = _hubRuntime.Start(ResolvedProfile, LockMouse, CurrentContext, ThrottledOutputCallback);

            if (!started)
            {
                IsActive = false;
            }
        }

        public double NormalizedSteer => Math.Max(-1.0, Math.Min(1.0, (SteerValue - 16384.0) / 16384.0));
        public double NormalizedGas => Math.Max(0.0, Math.Min(1.0, GasValue / 32768.0));
        public double NormalizedBrake => Math.Max(0.0, Math.Min(1.0, BrakeValue / 32768.0));

        private void ThrottledOutputCallback(int steer, int gas, int brake)
        {
            long now = Environment.TickCount64;
            if (now - _lastUiUpdateTimestamp > 33) // ~30 FPS UI update throttle
            {
                _lastUiUpdateTimestamp = now;
                System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
                {
                    SteerValue = steer;
                    GasValue = gas;
                    BrakeValue = brake;
                    OnPropertyChanged(nameof(NormalizedSteer));
                    OnPropertyChanged(nameof(NormalizedGas));
                    OnPropertyChanged(nameof(NormalizedBrake));
                }, System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private void StopHub()
        {
            _hubRuntime.Stop();
            SteerValue = 16384;
            GasValue = 0;
            BrakeValue = 0;
            OnPropertyChanged(nameof(NormalizedSteer));
            OnPropertyChanged(nameof(NormalizedGas));
            OnPropertyChanged(nameof(NormalizedBrake));
        }

        private void OnHubFault(string reason)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            _ = dispatcher.InvokeAsync(() =>
            {
                if (IsActive)
                {
                    IsActive = false;
                }
                else
                {
                    StopHub();
                }

                AppLogger.Instance.LogError($"vWheel Hub stopped: {reason}");
                AudioUtil.PlayErrorSound();

                var uiBox = new Wpf.Ui.Controls.MessageBox
                {
                    Title = "vWheel Hub stopped",
                    Content = $"{reason}\n\nCheck the log for details:\n{AppLogger.Instance.CurrentLogFilePath}",
                    CloseButtonText = "OK"
                };
                _ = uiBox.ShowDialogAsync();
            });
        }
        
        public void Shutdown()
        {
            try
            {
                StopHub();
                _gameDetection?.Stop();
                _hotkeyService?.Stop();
                _hotkeyService?.Dispose();
                _hubRuntime?.Dispose();
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError("Error during application shutdown", ex);
            }
        }

        public void LoadCurrentContext()
        {
            if (string.IsNullOrWhiteSpace(CurrentContext.Game))
            {
                CurrentContext.Game = "Universal";
            }

            string? origGame = CurrentContext.Game;
            string? origClass = CurrentContext.CarClass;
            string? origCar = CurrentContext.Car;

            string cleanGame = ProfileManager.SanitizeName(origGame);
            string cleanClass = ProfileManager.SanitizeName(origClass);
            string cleanCar = ProfileManager.SanitizeName(origCar);

            if ((origGame != null && origGame != cleanGame) ||
                (origClass != null && origClass != cleanClass) ||
                (origCar != null && origCar != cleanCar))
            {
                CurrentContext.Game = cleanGame;
                CurrentContext.CarClass = cleanClass;
                CurrentContext.Car = cleanCar;
                AppLogger.Instance.LogInfo("Profile path sanitized to remove invalid Windows path characters.");
            }

            // Enforce strict Game -> Class -> Car hierarchy
            if (string.IsNullOrEmpty(CurrentContext.CarClass) && !string.IsNullOrEmpty(CurrentContext.Car))
            {
                CurrentContext.Car = string.Empty;
            }
            
            _profileSession.LoadContext(CurrentContext, SupportsAbs, SupportsTc, SupportsSlipAudio);
            
            _hotkeyService.UpdateProfile(_profileSession.ResolvedProfile);

            if (IsActive)
            {
                _hubRuntime.UpdateRuntimeProfile(ResolvedProfile, CurrentContext);
            }
            
            RefreshDropdownCollections();
            RefreshLmuInputFixStatus();
        }

        private void RefreshDropdownCollections()
        {
            var newGames = new List<string> { "Universal" };
            newGames.AddRange(_profileManager.GetAvailableGames());
            if (!System.Linq.Enumerable.SequenceEqual(AvailableGames, newGames))
            {
                AvailableGames = new ObservableCollection<string>(newGames);
            }
            
            var newClasses = new List<string> { "" };
            newClasses.AddRange(_profileManager.GetAvailableClasses(CurrentContext.Game));
            if (!System.Linq.Enumerable.SequenceEqual(AvailableClasses, newClasses))
            {
                AvailableClasses = new ObservableCollection<string>(newClasses);
            }
            
            var newCars = new List<string> { "" };
            newCars.AddRange(_profileManager.GetAvailableCars(CurrentContext.Game, CurrentContext.CarClass));
            if (!System.Linq.Enumerable.SequenceEqual(AvailableCars, newCars))
            {
                AvailableCars = new ObservableCollection<string>(newCars);
            }
        }

        [RelayCommand]
        public void SaveProfile()
        {
            if (!_profileSession.SaveProfile(CurrentContext)) return;
            LoadCurrentContext();
        }

        [RelayCommand(CanExecute = nameof(CanUndo))]
        public void RevertProfile()
        {
            _profileSession.RevertProfile();
        }

        public void OnMonitorMarkerDragged(string markerName, double value)
        {
            if (CurrentProfile == null) return;

            switch (markerName)
            {
                case "VJoy Steer Min":
                    CurrentProfile.OutSteerMin = (int)value;
                    break;
                case "VJoy Steer Max":
                    CurrentProfile.OutSteerMax = (int)value;
                    break;
                case "Car Steering Lock":
                    CurrentProfile.CarSteeringLock = (int)value;
                    break;
                case "VJoy Gas Min":
                    CurrentProfile.OutGasMin = (int)value;
                    break;
                case "VJoy Gas Max":
                    CurrentProfile.OutGasMax = (int)value;
                    break;
                case "Fast Attack Threshold":
                    CurrentProfile.GasThreshold = value;
                    break;
                case "VJoy Brake Min":
                    CurrentProfile.OutBrakeMin = (int)value;
                    break;
                case "VJoy Brake Max":
                    CurrentProfile.OutBrakeMax = (int)value;
                    break;
                case "Brake Threshold (Fast/Slow Attack)":
                    CurrentProfile.BrakeThreshold = value;
                    break;
                case "Trail Braking Limit":
                    CurrentProfile.TbMinLimit = value;
                    break;
            }
        }

        [RelayCommand]
        public void SwitchToPendingContext()
        {
            if (PendingContext != null)
            {
                var target = PendingContext;
                PendingContext = null;
                CurrentContext = target;
            }
        }

        [RelayCommand]
        public void SaveAndSwitchToPendingContext()
        {
            if (PendingContext != null)
            {
                var target = PendingContext;
                SaveProfile();
                PendingContext = null;
                CurrentContext = target;
            }
        }

        [RelayCommand]
        public void IgnorePendingContext()
        {
            PendingContext = null;
        }

        [RelayCommand]
        public void ResetTbDecay()
        {
            if (CurrentProfile != null)
            {
                var defaults = Profile.CreateDefault(CurrentContext?.Game ?? "Universal");
                CurrentProfile.TbDecay = defaults.TbDecay ?? Profile.DefaultTbDecay * 1000.0;
            }
        }

        [RelayCommand]
        public void OpenExportDialog()
        {
            if (CurrentProfile == null) return;
            var dialog = new ProfileExportDialog(CurrentProfile, CurrentContext, _profileSharingService);
            dialog.Owner = System.Windows.Application.Current?.MainWindow;
            dialog.ShowDialog();
        }

        [RelayCommand]
        public void OpenImportDialog()
        {
            var dialog = new ProfileImportDialog(CurrentContext, _profileSharingService);
            dialog.Owner = System.Windows.Application.Current?.MainWindow;
            ExecuteImportDialog(dialog);
        }

        public void ProcessImportPackage(ProfileSharePackage package)
        {
            if (package == null || package.Settings == null) return;

            var dialog = new ProfileImportDialog(CurrentContext, _profileSharingService, package);
            dialog.Owner = System.Windows.Application.Current?.MainWindow;
            ExecuteImportDialog(dialog);
        }

        private void ExecuteImportDialog(ProfileImportDialog dialog)
        {
            if (dialog.ShowDialog() == true && dialog.Package?.Settings != null)
            {
                var package = dialog.Package;
                if (dialog.ResultAction == ProfileImportAction.SaveToTargetSlot && package.TargetContext != null)
                {
                    if (!_profileManager.SaveContext(package.TargetContext, package.Settings)) return;
                    AppLogger.Instance.LogInfo($"Saved profile preset to slot: {package.TargetContext.DisplayPath}");

                    // If user is currently looking at this context, reload
                    if (string.Equals(CurrentContext.DisplayPath, package.TargetContext.DisplayPath, StringComparison.OrdinalIgnoreCase))
                    {
                        LoadCurrentContext();
                    }
                    else
                    {
                        RefreshDropdownCollections();
                    }
                }
                else if (dialog.ResultAction == ProfileImportAction.ApplyToCurrentContext)
                {
                    CurrentProfile.CopyFrom(package.Settings);
                    AppLogger.Instance.LogInfo($"Applied profile preset to active configuration: {CurrentContext.DisplayPath}");
                }
            }
        }

        [RelayCommand]
        public void OpenProfilesFolder()
        {
            try
            {
                if (!System.IO.Directory.Exists(StoragePaths.ProfilesDirectory))
                {
                    System.IO.Directory.CreateDirectory(StoragePaths.ProfilesDirectory);
                }
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", StoragePaths.ProfilesDirectory) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError("Failed to open profiles folder", ex);
            }
        }

        public string AbsCachePath => SimRacingHub.Services.Telemetry.AdaptiveGridCache.RootDirectory(
            SimRacingHub.Services.Telemetry.HelperKind.Abs);

        public string TcCachePath => SimRacingHub.Services.Telemetry.AdaptiveGridCache.RootDirectory(
            SimRacingHub.Services.Telemetry.HelperKind.Tc);

        [RelayCommand]
        public void OpenAbsCacheFolder() => OpenLearningFolder(SimRacingHub.Services.Telemetry.HelperKind.Abs);

        [RelayCommand]
        public void OpenTcCacheFolder() => OpenLearningFolder(SimRacingHub.Services.Telemetry.HelperKind.Tc);

        [RelayCommand]
        public void ResetAbsCarLearning() => ResetCarLearning(SimRacingHub.Services.Telemetry.HelperKind.Abs);

        [RelayCommand]
        public void ResetTcCarLearning() => ResetCarLearning(SimRacingHub.Services.Telemetry.HelperKind.Tc);

        [RelayCommand]
        public void ResetAllAbsLearning() => ResetAllLearning(SimRacingHub.Services.Telemetry.HelperKind.Abs);

        [RelayCommand]
        public void ResetAllTcLearning() => ResetAllLearning(SimRacingHub.Services.Telemetry.HelperKind.Tc);

        private bool IsHelperAvailable(SimRacingHub.Services.Telemetry.HelperKind kind) =>
            kind == SimRacingHub.Services.Telemetry.HelperKind.Abs ? SupportsAbs : SupportsTc;

        private void OpenLearningFolder(SimRacingHub.Services.Telemetry.HelperKind kind)
        {
            if (!IsHelperAvailable(kind)) return;

            try
            {
                string path = SimRacingHub.Services.Telemetry.AdaptiveGridCache.RootDirectory(kind);
                if (!System.IO.Directory.Exists(path))
                {
                    System.IO.Directory.CreateDirectory(path);
                }
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError("Failed to open the learning cache folder", ex);
            }
        }

        private void ResetCarLearning(SimRacingHub.Services.Telemetry.HelperKind kind)
        {
            if (!IsHelperAvailable(kind)) return;

            try
            {
                string label = SimRacingHub.Services.Telemetry.AdaptiveGridCache.KindName(kind);
                string? carName = SimRacingHub.Services.Telemetry.AdaptiveGridCache.CurrentCarIfActive;

                if (string.IsNullOrEmpty(carName))
                {
                    System.Windows.MessageBox.Show(
                        "No car has been detected yet in this session." + Environment.NewLine + Environment.NewLine +
                        "Start the hub in a game, get into the car, then use this button. " +
                        $"You can also delete single files with \"Open {label} Cache Folder\".",
                        $"Reset {label} learning", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    return;
                }

                var confirm = System.Windows.MessageBox.Show(
                    $"Reset the learned {label} limits for \"{carName}\"?" + Environment.NewLine + Environment.NewLine +
                    "Both the dry and the wet data for this car are cleared. Other cars and the other helper are not affected." + Environment.NewLine + Environment.NewLine +
                    "This cannot be undone.",
                    $"Reset {label} learning", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);

                if (confirm != System.Windows.MessageBoxResult.Yes) return;

                if (SimRacingHub.Services.Telemetry.AdaptiveGridCache.ResetCurrentCarIfActive(kind))
                {
                    AudioUtil.PlaySuccessSound();
                }
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError("Failed to reset the learning data for the current car", ex);
            }
        }

        private void ResetAllLearning(SimRacingHub.Services.Telemetry.HelperKind kind)
        {
            if (!IsHelperAvailable(kind)) return;

            try
            {
                string label = SimRacingHub.Services.Telemetry.AdaptiveGridCache.KindName(kind);

                var confirm = System.Windows.MessageBox.Show(
                    $"Reset the learned {label} limits for every car?" + Environment.NewLine + Environment.NewLine +
                    $"All {label} data is deleted and the helper starts learning from scratch in every car. The other helper is not affected." + Environment.NewLine + Environment.NewLine +
                    "This cannot be undone.",
                    $"Reset all {label} learning", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);

                if (confirm != System.Windows.MessageBoxResult.Yes) return;

                SimRacingHub.Services.Telemetry.AdaptiveGridCache.ResetAll(kind);
                AudioUtil.PlaySuccessSound();
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError("Failed to reset all learning data", ex);
            }
        }
    }
}
