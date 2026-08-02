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
        private GameDetectionService _gameDetection;
        public GameDetectionService GameDetection => _gameDetection;

        private readonly GamePluginManager _pluginManager = new GamePluginManager();

        [ObservableProperty]
        private GamePluginDiagnosticResult _gamePluginDiagnostic;

        [ObservableProperty]
        private VJoyDiagnosticResult _vJoyDiagnostic;

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
        
        [ObservableProperty]
        private ProfileContext _currentContext;

        [ObservableProperty]
        private bool _isOutputMonitorExpanded;

        public bool SupportsAbs => HasFeature("Abs");
        public bool SupportsTc => HasFeature("Tc");
        public bool SupportsSlipAudio => HasFeature("SlipAudio");
        public bool SupportsSpeedSens => HasFeature("SpeedSens");
        public bool SupportsTcAudio => HasFeature("TcAudio") && ResolvedProfile != null && ResolvedProfile.UseTcHelper;

        public int TotalProFeaturesCount
        {
            get
            {
                var builtInProFeatures = new[] { "Abs", "Tc", "TcAudio", "SlipAudio", "SpeedSens", "TrailBraking" };

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



        public int[] AvailablePollingRates { get; } = new[] { 125, 250, 500, 1000 };

        [ObservableProperty]
        private ObservableCollection<string> _availableGames = new();
        [ObservableProperty]
        private ObservableCollection<string> _availableClasses = new();
        [ObservableProperty]
        private ObservableCollection<string> _availableCars = new();


        
        private ProfileContext _oldContext;
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
                OnPropertyChanged(nameof(SupportsTcAudio));
            });
        }
        
        private void CurrentContext_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProfileContext.Game))
            {
                OnPropertyChanged(nameof(SupportsAbs));
                OnPropertyChanged(nameof(SupportsTc));
                OnPropertyChanged(nameof(SupportsSlipAudio));
                OnPropertyChanged(nameof(SupportsSpeedSens));
                OnPropertyChanged(nameof(SupportsTcAudio));
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
                if (GamePluginDiagnostic.Status == PluginStatus.MissingPlugin || GamePluginDiagnostic.Status == PluginStatus.OutdatedPlugin)
                {
                    if (_lastWarnedGameForMissingPlugin != CurrentContext.Game)
                    {
                        _lastWarnedGameForMissingPlugin = CurrentContext.Game;
                        try
                        {
                            AudioUtil.PlayErrorSound();
                        }
                        catch { }
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
        public void InstallGamePlugin()
        {
            if (CurrentContext == null || string.IsNullOrEmpty(CurrentContext.Game)) return;

            var (success, msg) = _pluginManager.InstallPlugin(CurrentContext.Game, autoCloseGame: true);
            if (success)
            {
                try
                {
                    AudioUtil.PlaySuccessSound();
                }
                catch { }
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
                var (success, msg) = _pluginManager.InstallPlugin(CurrentContext.Game, selectedFolder, autoCloseGame: true);
                if (success)
                {
                    try
                    {
                        AudioUtil.PlaySuccessSound();
                    }
                    catch { }
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
            try
            {
                var settings = new
                {
                    AutoStartHub = AutoStartHub,
                    AutoDetectOnStartup = AutoDetectOnStartup
                };
                System.IO.File.WriteAllText("appsettings.json", Newtonsoft.Json.JsonConvert.SerializeObject(settings, Newtonsoft.Json.Formatting.Indented));
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError("Failed to save app settings", ex);
            }
        }

        [ObservableProperty]
        private ProfileContext _pendingContext;

        public MainViewModel(ProfileManager profileManager, WindowManager windowManager, GameIntegrationRegistry registry, VJoyDiagnosticService vJoyDiagnosticService, UpdateService updateService)
        {
            _profileManager = profileManager;
            _windowManager = windowManager;
            _registry = registry;
            _vJoyDiagnosticService = vJoyDiagnosticService;
            _updateService = updateService;

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
            
            _hubRuntime = new HubRuntime(registry, windowManager);
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
                    });
                }
                else
                {
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        PendingContext = null;
                        CurrentContext = ctx;
                    });
                }
            };
            
            CurrentContext = new ProfileContext("Universal");
            
            // By default false, bound to UI toggle
            LockMouse = false;
            
            try
            {
                if (System.IO.File.Exists("appsettings.json"))
                {
                    var settings = Newtonsoft.Json.JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string, bool>>(System.IO.File.ReadAllText("appsettings.json"));
                    if (settings != null)
                    {
                        if (settings.TryGetValue("AutoStartHub", out bool autoStart))
                        {
                            AutoStartHub = autoStart;
                        }
                        if (settings.TryGetValue("AutoDetectOnStartup", out bool autoDetectStartup))
                        {
                            _autoDetectOnStartup = autoDetectStartup;
                        }
                        else if (settings.TryGetValue("EnableAutoDetect", out bool autoDetectLegacy))
                        {
                            _autoDetectOnStartup = autoDetectLegacy;
                        }
                    }
                }
            }
            catch (Exception ex) 
            { 
                AppLogger.Instance.LogError("Failed to load app settings", ex);
            }
            
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
            catch { }
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
                var (success, msg) = VJoyDiagnosticService.TryAutoConfigureVJoy(1);
                AppLogger.Instance.LogInfo($"Auto-configure vJoy result: {success} - {msg}");
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

            if (VJoyDiagnostic == null)
            {
                VJoyDiagnostic = _vJoyDiagnosticService.PerformDiagnostic();
            }

            if (!VJoyDiagnostic.IsValid)
            {
                IsActive = false;
                AppLogger.Instance.LogError($"Cannot start Hub: {VJoyDiagnostic.SummaryMessage}. {VJoyDiagnostic.ActionableAdvice}");

                _ = System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
                {
                    var uiBox = new Wpf.Ui.Controls.MessageBox
                    {
                        Title = "vJoy Configuration Error",
                        Content = $"{VJoyDiagnostic.SummaryMessage}\n\nInstructions:\n{VJoyDiagnostic.ActionableAdvice}",
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

        public double NormalizedSteer => Math.Max(-1.0, Math.Min(1.0, SteerValue / 32768.0));
        public double NormalizedGas => Math.Max(0.0, Math.Min(1.0, (GasValue - (-32768.0)) / 65536.0));
        public double NormalizedBrake => Math.Max(0.0, Math.Min(1.0, (BrakeValue - (-32768.0)) / 65536.0));

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
            SteerValue = 0;
            GasValue = -32768;
            BrakeValue = -32768;
            OnPropertyChanged(nameof(NormalizedSteer));
            OnPropertyChanged(nameof(NormalizedGas));
            OnPropertyChanged(nameof(NormalizedBrake));
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

            string origGame = CurrentContext.Game;
            string origClass = CurrentContext.CarClass;
            string origCar = CurrentContext.Car;

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
            newClasses.AddRange(_profileManager.GetAvailableClasses(CurrentContext.Game == "Universal" ? "" : CurrentContext.Game));
            if (!System.Linq.Enumerable.SequenceEqual(AvailableClasses, newClasses))
            {
                AvailableClasses = new ObservableCollection<string>(newClasses);
            }
            
            var newCars = new List<string> { "" };
            newCars.AddRange(_profileManager.GetAvailableCars(CurrentContext.Game == "Universal" ? "" : CurrentContext.Game, CurrentContext.CarClass));
            if (!System.Linq.Enumerable.SequenceEqual(AvailableCars, newCars))
            {
                AvailableCars = new ObservableCollection<string>(newCars);
            }
        }

        [RelayCommand]
        public void SaveProfile()
        {
            _profileSession.SaveProfile(CurrentContext);
            LoadCurrentContext();
        }

        [RelayCommand(CanExecute = nameof(CanUndo))]
        public void RevertProfile()
        {
            _profileSession.RevertProfile();
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
                CurrentProfile.TbDecay = Profile.DefaultTbDecay;
            }
        }
    }
}
