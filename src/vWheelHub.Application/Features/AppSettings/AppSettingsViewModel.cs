using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimRacingHub.Application.Common;
using SimRacingHub.Application.Contracts;
using SimRacingHub.Application.Localization;
using SimRacingHub.Application.Updates;
using SimRacingHub.Core;
using SimRacingHub.Services.Games;

namespace SimRacingHub.Application.Features.AppSettings
{
    public static class AppSettingsUrls
    {
        public const string GitHubRepoUrl = "https://github.com/Barden-dev/vWheel-Hub";
        public const string GitHubReleasesUrl = "https://github.com/Barden-dev/vWheel-Hub/releases";
        public const string DocumentationUrl = "https://github.com/Barden-dev/vWheel-Hub/wiki";
        public const string DiscordUrl = "https://discord.gg/YQn5eh2vfc";
        public const string BoostyUrl = "https://boosty.to/barden_dev";
    }

    public partial class AppSettingsViewModel : ViewModelBase, IDisposable
    {
        private readonly IAppStorage _appStorage;
        private readonly IAppMetadata _appMetadata;
        private readonly ILocalizationManager _localizationManager;
        private readonly IExternalLauncher _externalLauncher;
        private readonly IUserDialogService _dialogService;
        private readonly IPlatformCapabilities _platformCapabilities;
        private readonly IUiDispatcher? _uiDispatcher;
        private readonly IInputCompatibilityService? _inputCompatibility;
        private SimRacingHub.Core.AppSettings _appSettings;
        private bool _isDisposed;
        #region Update Status Compatibility API

        public UpdateStatusViewModel Updates { get; }
        public bool IsUpdateAvailable { get => Updates.IsUpdateAvailable; set => Updates.IsUpdateAvailable = value; }
        public string? LatestVersion { get => Updates.LatestVersion; set => Updates.LatestVersion = value; }
        public string? UpdateUrl { get => Updates.UpdateUrl; set => Updates.UpdateUrl = value; }
        public bool IsCheckingForUpdates { get => Updates.IsCheckingForUpdates; set => Updates.IsCheckingForUpdates = value; }
        public string? UpdateStatusMessage { get => Updates.UpdateStatusMessage; set => Updates.UpdateStatusMessage = value; }
        public IAsyncRelayCommand CheckForUpdatesCommand => Updates.CheckForUpdatesCommand;
        public IAsyncRelayCommand OpenUpdateUrlCommand => Updates.OpenUpdateUrlCommand;
        public IRelayCommand DismissUpdateBannerCommand => Updates.DismissUpdateBannerCommand;
        public Task<bool> CheckForUpdatesAsync(CancellationToken ct = default) => Updates.CheckForUpdatesAsync(ct);
        public Task OpenUpdateUrlAsync(CancellationToken ct = default) => Updates.OpenUpdateUrlAsync(ct);
        public void DismissUpdateBanner() => Updates.DismissUpdateBanner();

        private void UpdatesChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => OnPropertyChanged(e);
        private void ChildSettingsChanging(object? sender, System.ComponentModel.PropertyChangingEventArgs e) => OnPropertyChanging(e);

        #endregion
#region Dependency Properties
        public IAppStorage AppStorage => _appStorage;
        public IAppMetadata AppMetadata => _appMetadata;
        public ILocalizationManager LocalizationManager => _localizationManager;
        public IExternalLauncher ExternalLauncher => _externalLauncher;
        public IUserDialogService DialogService => _dialogService;
        public IPlatformCapabilities PlatformCapabilities => _platformCapabilities;
        public IUiDispatcher? UiDispatcher => _uiDispatcher;
#endregion
#region Version & Metadata Properties
        public string Version => _appMetadata.Version;
        public string CurrentVersion => _appMetadata.Version;
        public string AppVersion => _appMetadata.Version;
        public string ApplicationName => _appMetadata.ApplicationName;
        public string Edition => _appMetadata.Edition;
        public string Copyright => _appMetadata.Copyright;
        public string FullVersionString => $"{_appMetadata.ApplicationName} v{_appMetadata.Version} ({_appMetadata.Edition})";

#endregion
#region General App Settings
        // Legacy property name; this controls the hub on application launch, never OS login.
        public bool AutoStartWithWindows
        {
            get => _appSettings.AutoStartHub;
            set
            {
                if (_appSettings.AutoStartHub != value)
                {
                    var latest = SimRacingHub.Core.AppSettings.Load(_appStorage.SettingsFilePath);
                _appSettings.WelcomeVersionSeen = latest.WelcomeVersionSeen;
                _appSettings.WindowsSelectedMousePath = latest.WindowsSelectedMousePath;
                _appSettings.WindowsSelectedKeyboardPath = latest.WindowsSelectedKeyboardPath;
                _appSettings.LinuxSelectedMousePath = latest.LinuxSelectedMousePath;
                _appSettings.LinuxSelectedKeyboardPath = latest.LinuxSelectedKeyboardPath;
                _appSettings.LinuxSelectedMouseIdentity = latest.LinuxSelectedMouseIdentity;
                _appSettings.LinuxSelectedKeyboardIdentity = latest.LinuxSelectedKeyboardIdentity;
                _appSettings.AutoStartHub = value;
                    SaveSettings();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AutoStartHub));
                }
            }
        }

        public bool AutoStartHub { get => AutoStartWithWindows; set => AutoStartWithWindows = value; }

        public bool StartMinimized
        {
            get => _appSettings.StartMinimized;
            set
            {
                if (_appSettings.StartMinimized == value) return;
                _appSettings.StartMinimized = value;
                SaveSettings();
                OnPropertyChanged();
            }
        }

        public bool MinimizeToTray
        {
            get => _appSettings.MinimizeToTray;
            set
            {
                if (_appSettings.MinimizeToTray == value) return;
                _appSettings.MinimizeToTray = value;
                SaveSettings();
                OnPropertyChanged();
            }
        }

        public bool CloseToTray
        {
            get => _appSettings.CloseToTray;
            set
            {
                if (_appSettings.CloseToTray == value) return;
                _appSettings.CloseToTray = value;
                SaveSettings();
                OnPropertyChanged();
            }
        }

        public bool MouseLockSoundsEnabled
        {
            get => _appSettings.MouseLockSoundsEnabled;
            set
            {
                if (_appSettings.MouseLockSoundsEnabled == value) return;
                _appSettings.MouseLockSoundsEnabled = value;
                SaveSettings();
                OnPropertyChanged();
            }
        }

        public bool AutoDetectGame
        {
            get => _appSettings.ResolvedAutoDetectOnStartup;
            set
            {
                if (_appSettings.ResolvedAutoDetectOnStartup != value)
                {
                    _appSettings.AutoDetectOnStartup = value;
                    _appSettings.EnableAutoDetect = value;
                    SaveSettings();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AutoDetectOnStartup));
                }
            }
        }

        public bool AutoDetectOnStartup { get => AutoDetectGame; set => AutoDetectGame = value; }

#endregion
#region Language Selection
        public string SelectedLanguage
        {
            get => _localizationManager.CurrentCultureCode;
            set
            {
                if (!string.IsNullOrWhiteSpace(value) && !string.Equals(_localizationManager.CurrentCultureCode, value, StringComparison.OrdinalIgnoreCase) && !string.Equals(_localizationManager.CurrentLanguageCode, value, StringComparison.OrdinalIgnoreCase))
                {
                    _localizationManager.SetLanguage(value, saveToSettings: false);
                    SaveSettings();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SelectedLanguageCode));
                    OnPropertyChanged(nameof(CurrentLanguage));
                    OnPropertyChanged(nameof(SelectedLanguageInfo));
                }
            }
        }

        public string SelectedLanguageCode => _localizationManager.CurrentLanguageCode;
        public LanguageInfo CurrentLanguage => _localizationManager.CurrentLanguage;
        public LanguageInfo? SelectedLanguageInfo
        {
            get => _localizationManager.CurrentLanguage;
            set { if (value != null) SelectedLanguage = value.CultureCode; }
        }

        public void RefreshLanguageChoices()
        {
            OnPropertyChanged(nameof(AvailableLanguages));
            OnPropertyChanged(nameof(AvailableLanguageCodes));
            OnPropertyChanged(nameof(SelectedLanguageInfo));
        }

        public IReadOnlyList<LanguageInfo> AvailableLanguages => _localizationManager.AvailableLanguages;
        public IReadOnlyList<string> AvailableLanguageCodes => AvailableLanguages.Select(l => l.DisplayCode).ToArray();
        public string CacheDirectory => _appStorage.CacheDirectory;
#endregion
#region Game Setup Properties
        public IReadOnlyList<string> SupportedGames { get; } = new[]
        {
            "LMU",
            "Assetto Corsa",
            "Assetto Corsa Competizione",
            "Assetto Corsa Evo",
            "Automobilista 2",
            "BeamNG.drive",
            "WRC Generations"
        };
#endregion
#region Update Checker Inversion
        public Func<CancellationToken, Task<UpdateCheckResult?>>? UpdateChecker { get => Updates.UpdateChecker; set => Updates.UpdateChecker = value; }
        public string TargetReleaseUrl => AppSettingsUrls.GitHubReleasesUrl;

#endregion
        public AppSettingsViewModel(IAppStorage appStorage, IAppMetadata appMetadata, ILocalizationManager localizationManager, IExternalLauncher externalLauncher, IUserDialogService dialogService, IPlatformCapabilities platformCapabilities, IUiDispatcher? uiDispatcher = null, IInputCompatibilityService? inputCompatibility = null, IUpdateCheckService? updateCheckService = null)
        {
            _appStorage = appStorage ?? throw new ArgumentNullException(nameof(appStorage));
            _appMetadata = appMetadata ?? throw new ArgumentNullException(nameof(appMetadata));
            _localizationManager = localizationManager ?? throw new ArgumentNullException(nameof(localizationManager));
            _externalLauncher = externalLauncher ?? throw new ArgumentNullException(nameof(externalLauncher));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _inputCompatibility = inputCompatibility;
            _platformCapabilities = platformCapabilities ?? throw new ArgumentNullException(nameof(platformCapabilities));
            _uiDispatcher = uiDispatcher;
            Updates = new UpdateStatusViewModel(_appMetadata, _localizationManager, _externalLauncher, () => TargetReleaseUrl,
                updateCheckService ?? new UpdateCheckService());
            Updates.PropertyChanging += ChildSettingsChanging;
            Updates.PropertyChanged += UpdatesChanged;
            _appSettings = SimRacingHub.Core.AppSettings.Load(_appStorage.SettingsFilePath);
            _localizationManager.LanguageChanged += OnLanguageChanged;
        }

#region Language Methods & Commands
        [RelayCommand]
        public void SetLanguage(string? cultureOrDisplayCode)
        {
            if (!string.IsNullOrWhiteSpace(cultureOrDisplayCode))
            {
                SelectedLanguage = cultureOrDisplayCode;
            }
        }

        [RelayCommand]
        public void ToggleLanguage()
        {
            _localizationManager.ToggleLanguage(saveToSettings: false);
            SaveSettings();
            OnPropertyChanged(nameof(SelectedLanguage));
            OnPropertyChanged(nameof(SelectedLanguageCode));
            OnPropertyChanged(nameof(CurrentLanguage));
        }

        private void OnLanguageChanged(string cultureCode)
        {
            void Update()
            {
                OnPropertyChanged(nameof(SelectedLanguage));
                OnPropertyChanged(nameof(SelectedLanguageCode));
                OnPropertyChanged(nameof(CurrentLanguage));
                OnPropertyChanged(nameof(SelectedLanguageInfo));
                OnPropertyChanged(nameof(InputCompatibilityStatusText));
            }

            if (_uiDispatcher != null && !_uiDispatcher.CheckAccess())
            {
                _uiDispatcher.Post(Update);
            }
            else
            {
                Update();
            }
        }

#endregion
#region App Settings Persistence
        public bool HasSeenWelcome => _appSettings.WelcomeVersionSeen >= 1;

        public void DismissWelcome()
        {
            var latest = SimRacingHub.Core.AppSettings.Load(_appStorage.SettingsFilePath);
            latest.WelcomeVersionSeen = 1;
            latest.Language = _localizationManager.CurrentCultureCode;
            latest.Save(_appStorage.SettingsFilePath);
            _appSettings.WelcomeVersionSeen = 1;
        }

        public void SaveSettings()
        {
            try
            {
                var latest = SimRacingHub.Core.AppSettings.Load(_appStorage.SettingsFilePath);
                _appSettings.WelcomeVersionSeen = latest.WelcomeVersionSeen;
                _appSettings.WindowsSelectedMousePath = latest.WindowsSelectedMousePath;
                _appSettings.WindowsSelectedKeyboardPath = latest.WindowsSelectedKeyboardPath;
                _appSettings.LinuxSelectedMousePath = latest.LinuxSelectedMousePath;
                _appSettings.LinuxSelectedKeyboardPath = latest.LinuxSelectedKeyboardPath;
                _appSettings.LinuxSelectedMouseIdentity = latest.LinuxSelectedMouseIdentity;
                _appSettings.LinuxSelectedKeyboardIdentity = latest.LinuxSelectedKeyboardIdentity;
                _appSettings.AutoStartHub = AutoStartWithWindows;
                _appSettings.AutoDetectOnStartup = AutoDetectGame;
                _appSettings.EnableAutoDetect = AutoDetectGame;
                _appSettings.Language = _localizationManager.CurrentCultureCode;
                _appSettings.Save(_appStorage.SettingsFilePath);
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError($"Failed to save application settings to '{_appStorage.SettingsFilePath}'", ex);
            }
        }

        public void ReloadSettings()
        {
            _appSettings = SimRacingHub.Core.AppSettings.Load(_appStorage.SettingsFilePath);
            OnPropertyChanged(nameof(StartMinimized));
            OnPropertyChanged(nameof(MinimizeToTray));
            OnPropertyChanged(nameof(CloseToTray));
            OnPropertyChanged(nameof(MouseLockSoundsEnabled));
            OnPropertyChanged(nameof(AutoStartWithWindows));
            OnPropertyChanged(nameof(AutoStartHub));
            OnPropertyChanged(nameof(AutoDetectGame));
            OnPropertyChanged(nameof(AutoDetectOnStartup));
            OnPropertyChanged(nameof(SelectedLanguage));
            OnPropertyChanged(nameof(SelectedLanguageCode));
            OnPropertyChanged(nameof(CurrentLanguage));
        }

#endregion
#region Update Banner Methods & Commands

        [RelayCommand]
        public async Task OpenCacheFolderAsync(CancellationToken ct = default) => await _externalLauncher.OpenDirectoryAsync(CacheDirectory, ct);
#endregion
#region External Resource Links Commands
        [RelayCommand]
        public async Task OpenGitHubAsync(CancellationToken ct = default) => await _externalLauncher.OpenUriAsync(new Uri(AppSettingsUrls.GitHubRepoUrl), ct);
        [RelayCommand]
        public async Task OpenDiscordAsync(CancellationToken ct = default) => await _externalLauncher.OpenUriAsync(new Uri(AppSettingsUrls.DiscordUrl), ct);
        [RelayCommand]
        public async Task OpenDocumentationAsync(CancellationToken ct = default) => await _externalLauncher.OpenUriAsync(new Uri(AppSettingsUrls.DocumentationUrl), ct);
        [RelayCommand]
        public async Task OpenWikiAsync(CancellationToken ct = default) => await OpenDocumentationAsync(ct);
        [RelayCommand]
        public async Task OpenBoostyAsync(CancellationToken ct = default) => await _externalLauncher.OpenUriAsync(new Uri(AppSettingsUrls.BoostyUrl), ct);
        [RelayCommand]
        public async Task OpenReleasesAsync(CancellationToken ct = default) => await _externalLauncher.OpenUriAsync(new Uri(AppSettingsUrls.GitHubReleasesUrl), ct);
        [RelayCommand]
        public async Task OpenProfilesFolderAsync(CancellationToken ct = default) => await _externalLauncher.OpenDirectoryAsync(_appStorage.ProfilesDirectory, ct);
        [RelayCommand]
        public async Task OpenLogsFolderAsync(CancellationToken ct = default) => await _externalLauncher.OpenDirectoryAsync(_appStorage.LogsDirectory, ct);
        [RelayCommand]
        public async Task OpenSettingsFolderAsync(CancellationToken ct = default)
        {
            string? dir = Path.GetDirectoryName(_appStorage.SettingsFilePath);
            if (!string.IsNullOrEmpty(dir))
            {
                await _externalLauncher.OpenDirectoryAsync(dir, ct);
            }
        }

#endregion
#region Input Compatibility
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(InputCompatibilityStatusText))]
        [NotifyPropertyChangedFor(nameof(CanApplyInputCompatibility))]
        private bool _isCheckingInputCompatibility;
        private InputCompatibilityStatus? _inputStatus;
        private bool _inputFailed;
        public bool SupportsInputCompatibility => _inputCompatibility != null && _platformCapabilities.SupportsWindowsDriverTools;
        public bool CanApplyInputCompatibility => SupportsInputCompatibility && !IsCheckingInputCompatibility && _inputStatus is { IsAvailable: true, IsApplied: false };
        public string InputCompatibilityStatusText => _localizationManager.GetString(_inputFailed ? "InputFix_Failed"
            : _inputStatus == null ? "InputFix_NotChecked" : !_inputStatus.IsAvailable ? "InputFix_NotFound"
            : _inputStatus.IsApplied ? "InputFix_Applied" : "InputFix_Ready");

        [RelayCommand]
        public async Task CheckInputCompatibilityAsync()
        {
            if (_inputCompatibility == null || IsCheckingInputCompatibility) return;
            IsCheckingInputCompatibility = true;
            try { _inputStatus = await _inputCompatibility.CheckAsync(); _inputFailed = false; }
            catch (Exception error) { _inputFailed = true; AppLogger.Instance.LogError("Input compatibility check failed", error); }
            finally
            {
                IsCheckingInputCompatibility = false;
                OnPropertyChanged(nameof(InputCompatibilityStatusText)); OnPropertyChanged(nameof(CanApplyInputCompatibility));
            }
        }

        [RelayCommand]
        public async Task ApplyInputCompatibilityAsync()
        {
            if (!CanApplyInputCompatibility) return;
            IsCheckingInputCompatibility = true;
            try
            {
                _inputFailed = !await _inputCompatibility!.ApplyAsync();
                _inputStatus = await _inputCompatibility.CheckAsync();
            }
            catch (Exception error) { _inputFailed = true; AppLogger.Instance.LogError("Input compatibility fix failed", error); }
            finally
            {
                IsCheckingInputCompatibility = false;
                OnPropertyChanged(nameof(InputCompatibilityStatusText)); OnPropertyChanged(nameof(CanApplyInputCompatibility));
            }
        }
#endregion
#region Disposal
        public void Dispose()
        {
            if (_isDisposed)
                return;
            _isDisposed = true;
            _localizationManager.LanguageChanged -= OnLanguageChanged;
            Updates.PropertyChanging -= ChildSettingsChanging;
            Updates.PropertyChanged -= UpdatesChanged;
        }
#endregion
    }
}
