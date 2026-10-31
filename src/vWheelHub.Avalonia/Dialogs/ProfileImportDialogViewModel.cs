using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimRacingHub.Application.Contracts;
using SimRacingHub.Application.Features.Profiles;
using SimRacingHub.Application.Localization;
using SimRacingHub.Models;

namespace SimRacingHub.Avalonia.Dialogs
{
    public partial class ProfileImportDialogViewModel : ObservableObject
    {
        private readonly ProfileContext _currentContext;
        private readonly IClipboardService _clipboardService;
        private readonly IFilePickerService _filePickerService;
        private readonly ILocalizationManager _localizationManager;
        private readonly IAppMetadata? _appMetadata;
        private readonly ProfilePackageCodec _codec;
        [ObservableProperty]
        private string _shareCodeInput = string.Empty;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasLoadedPackage))]
        [NotifyPropertyChangedFor(nameof(IsEmptyState))]
        [NotifyPropertyChangedFor(nameof(CanApply))]
        private ProfileSharePackage? _loadedPackage;
        public bool HasLoadedPackage => LoadedPackage != null;
        public bool IsEmptyState => LoadedPackage == null;
        public bool CanApply => LoadedPackage?.Settings != null;

        [ObservableProperty]
        private string _presetName = string.Empty;
        [ObservableProperty]
        private string _authorSubtitle = string.Empty;
        [ObservableProperty]
        private string _versionBadge = "v1.0.0";
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasDescription))]
        private string _description = string.Empty;
        public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

        [ObservableProperty]
        private string _targetContextText = "Universal";
        [ObservableProperty]
        private bool _isSpecificTarget;
        [ObservableProperty]
        private string _saveToTargetSlotButtonText = string.Empty;
        [ObservableProperty]
        private string _applyToCurrentContextButtonText = string.Empty;
        // Categorized parameter previews
        [ObservableProperty]
        private string _paramSteerSensText = string.Empty;
        [ObservableProperty]
        private string _paramSteerGammaText = string.Empty;
        [ObservableProperty]
        private string _paramSteerLockText = string.Empty;
        [ObservableProperty]
        private string _paramThrottleDynamicsText = string.Empty;
        [ObservableProperty]
        private string _paramThrottleGammaText = string.Empty;
        [ObservableProperty]
        private string _paramBrakeDynamicsText = string.Empty;
        [ObservableProperty]
        private string _paramBrakeGammaText = string.Empty;
        [ObservableProperty]
        private string _paramAbsText = string.Empty;
        [ObservableProperty]
        private string _paramTcText = string.Empty;
        // Input error or notification status
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasInputStatusText))]
        private string? _inputStatusText;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusColor))]
        private bool _isInputError;
        public bool HasInputStatusText => !string.IsNullOrEmpty(InputStatusText);
        public string StatusColor => IsInputError ? "#F87171" : "#4ADE80";
        public ProfileImportDialogResult Result { get; private set; } = ProfileImportDialogResult.Cancel;

        public event Action<ProfileImportDialogResult>? RequestClose;
        public ProfileImportDialogViewModel(ProfileContext? currentContext, IClipboardService clipboardService, IFilePickerService filePickerService, ILocalizationManager? localizationManager = null, IAppMetadata? appMetadata = null, ProfilePackageCodec? codec = null, ProfileSharePackage? initialPackage = null)
        {
            _currentContext = currentContext ?? new ProfileContext("Universal");
            _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
            _filePickerService = filePickerService ?? throw new ArgumentNullException(nameof(filePickerService));
            _localizationManager = localizationManager ?? new LocalizationManager();
            _appMetadata = appMetadata;
            _codec = codec ?? new ProfilePackageCodec();
            SaveToTargetSlotButtonText = _localizationManager.GetString("Ui_Message_baad7626ba", "Save to Target Slot");
            string curName = _currentContext.Car ?? _currentContext.CarClass ?? _currentContext.Game ?? "Current";
            ApplyToCurrentContextButtonText = string.Format(System.Globalization.CultureInfo.CurrentUICulture, _localizationManager.GetString("Ui_Message_ae4e70372c", "Apply to Current Configuration ({0})"), curName);
            if (initialPackage != null)
            {
                LoadPackage(initialPackage);
            }
        }

        partial void OnShareCodeInputChanged(string value)
        {
            string trimmed = value?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(trimmed))
            {
                InputStatusText = null;
                if (LoadedPackage == null)
                    ResetPreview();
                return;
            }

            var pkg = _codec.DecodeShareCode(trimmed);
            if (pkg != null)
            {
                LoadPackage(pkg);
                InputStatusText = null;
            }
            else
            {
                IsInputError = true;
                InputStatusText = _localizationManager.GetString("Ui_Message_74b84bb326", "Invalid Share Code format or corrupted data.");
                ResetPreview();
            }
        }

        public void LoadPackage(ProfileSharePackage package)
        {
            if (package == null || package.Settings == null)
            {
                ResetPreview();
                return;
            }

            LoadedPackage = package;
            PresetName = !string.IsNullOrWhiteSpace(package.Name) ? package.Name : _localizationManager.GetString("Ui_Message_53b338d45a", "Imported Preset");
            string dateStr = package.CreatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd");
            if (!string.IsNullOrWhiteSpace(package.Author))
            {
                AuthorSubtitle = string.Format(System.Globalization.CultureInfo.CurrentUICulture, _localizationManager.GetString("Ui_Message_b1a4c78acc", "Created by {0} • {1}"), package.Author.Trim(), dateStr);
            }
            else
            {
                AuthorSubtitle = string.Format(System.Globalization.CultureInfo.CurrentUICulture, _localizationManager.GetString("Ui_Message_471a26cb56", "Created on {0}"), dateStr);
            }

            VersionBadge = !string.IsNullOrWhiteSpace(package.AppVersion) ? $"v{package.AppVersion}" : "v1.0.0";
            Description = package.Description ?? string.Empty;
            string targetPath = package.TargetContext?.DisplayPath ?? "Universal";
            TargetContextText = targetPath;
            IsSpecificTarget = package.TargetContext != null && !string.IsNullOrEmpty(package.TargetContext.Game) && !package.TargetContext.Game.Equals("Universal", StringComparison.OrdinalIgnoreCase);
            if (IsSpecificTarget && package.TargetContext != null)
            {
                string targetCarName = !string.IsNullOrEmpty(package.TargetContext.Car) ? package.TargetContext.Car : (!string.IsNullOrEmpty(package.TargetContext.CarClass) ? package.TargetContext.CarClass : package.TargetContext.Game ?? string.Empty);
                SaveToTargetSlotButtonText = string.Format(System.Globalization.CultureInfo.CurrentUICulture, _localizationManager.GetString("Ui_Message_f88169d7ab", "Save to Target Slot ({0})"), targetCarName);
            }
            else
            {
                SaveToTargetSlotButtonText = _localizationManager.GetString("Ui_Message_baad7626ba", "Save to Target Slot");
            }

            var p = package.Settings;
            var inv = System.Globalization.CultureInfo.CurrentUICulture;
            ParamSteerSensText = string.Format(inv, _localizationManager.GetString("Ui_Message_5a839bae46", "Sensitivity: {0:0.00} ({1} Hz)"), p.MouseSensitivity ?? 1.0, p.PollingRate ?? 1000);
            string linearSuffix = (p.SteeringGamma ?? 1.0) == 1.0 ? _localizationManager.GetString("Ui_Message_568ed9dc8d", " (Linear)") : "";
            ParamSteerGammaText = string.Format(inv, _localizationManager.GetString("Ui_Message_fad8a5066b", "Steer Gamma: {0:0.00}{1}"), p.SteeringGamma ?? 1.0, linearSuffix);
            ;
            ParamSteerLockText = p.UseSteeringLockClamp == true ? string.Format(inv, _localizationManager.GetString("Ui_Message_5e62a701ab", "Steering Lock: {0:0}°"), p.CarSteeringLock ?? 900) : _localizationManager.GetString("Ui_Message_c41ab16c71", "Steering Lock: Auto");
            ParamThrottleDynamicsText = string.Format(inv, _localizationManager.GetString("Ui_Message_91590274ad", "Throttle: Fast {0:0.000}s / Slow {1:0.000}s"), p.GasAttackFastSeconds ?? 0, p.GasAttackSlowSeconds ?? 0);
            ParamThrottleGammaText = string.Format(inv, _localizationManager.GetString("Ui_Message_f2593f97e1", "Throttle Gamma: {0:0.00}"), p.GasGamma ?? 1.0);
            ParamBrakeDynamicsText = string.Format(inv, _localizationManager.GetString("Ui_Message_c9c47a6dc0", "Brake: Fast {0:0.000}s / Slow {1:0.000}s"), p.BrakeAttackFastSeconds ?? 0, p.BrakeAttackSlowSeconds ?? 0);
            ParamBrakeGammaText = string.Format(inv, _localizationManager.GetString("Ui_Message_d5d188263a", "Brake Gamma: {0:0.00}"), p.BrakeGamma ?? 1.0);
            ;
            ;
            ;
            ;
            ;
            ;
        }

        public void ResetPreview()
        {
            LoadedPackage = null;
            PresetName = string.Empty;
            AuthorSubtitle = string.Empty;
            Description = string.Empty;
            TargetContextText = "Universal";
            IsSpecificTarget = false;
        }

        [RelayCommand]
        public async Task<bool> PasteClipboardAsync()
        {
            try
            {
                string? text = await _clipboardService.GetTextAsync().ConfigureAwait(false);
                string? trimmed = text?.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    ShareCodeInput = trimmed;
                    return true;
                }

                IsInputError = true;
                InputStatusText = _localizationManager.GetString("Ui_Message_7aae6111b1", "Clipboard is empty or does not contain text.");
                return false;
            }
            catch (Exception ex)
            {
                IsInputError = true;
                InputStatusText = string.Format(System.Globalization.CultureInfo.CurrentUICulture, _localizationManager.GetString("Ui_Message_d3dae8fcb7", "Clipboard error: {0}"), ex.Message);
                return false;
            }
        }

        [RelayCommand]
        public async Task<bool> BrowseFileAsync()
        {
            try
            {
                var filters = new List<FileFilterItem>
                {
                    new FileFilterItem(_localizationManager.GetString("Ui_Message_e28e63462f", "vWheel Hub Preset (*.vwhub, *.json)"), new[] { ".vwhub", ".json" }),
                    new FileFilterItem(_localizationManager.GetString("Ui_Message_6ae05840ac", "All Files (*.*)"), new[] { "*.*" })
                };
                var options = new FilePickerOpenOptions(title: _localizationManager.GetString("Ui_Message_27fe4208cf", "Open vWheel Hub Preset File"), filters: filters);
                var picked = await _filePickerService.OpenFileAsync(options);
                if (picked == null)
                {
                    return false;
                }

                return await LoadFileAsync(picked);
            }
            catch (Exception ex)
            {
                IsInputError = true;
                InputStatusText = string.Format(System.Globalization.CultureInfo.CurrentUICulture, _localizationManager.GetString("Ui_Message_6ea8b29b14", "File error: {0}"), ex.Message);
                return false;
            }
        }

        public async Task<bool> LoadFileAsync(PickedFile picked)
        {
            ResetPreview();
            try
            {
                string content;
                await using (var stream = await picked.OpenReadAsync())
                using (var reader = new StreamReader(stream))
                {
                    content = await reader.ReadToEndAsync();
                }

                var pkg = _codec.DecodeShareCode(content);
                if (pkg != null)
                {
                    ShareCodeInput = string.Empty;
                    LoadPackage(pkg);
                    IsInputError = false;
                    InputStatusText = string.Format(System.Globalization.CultureInfo.CurrentUICulture, _localizationManager.GetString("Ui_Message_c19f1be820", "Loaded file: {0}"), picked.FileName);
                    return true;
                }

                IsInputError = true;
                InputStatusText = string.Format(System.Globalization.CultureInfo.CurrentUICulture, _localizationManager.GetString("Ui_Message_7be4905ac4", "Could not parse selected file '{0}' as a valid preset."), picked.FileName);
                return false;
            }
            catch (Exception ex)
            {
                IsInputError = true;
                InputStatusText = string.Format(System.Globalization.CultureInfo.CurrentUICulture, _localizationManager.GetString("Ui_Message_6ea8b29b14", "File error: {0}"), ex.Message);
                return false;
            }
        }

        [RelayCommand]
        public void SaveToTargetSlot()
        {
            Result = ProfileImportDialogResult.SaveToTargetSlot;
            RequestClose?.Invoke(Result);
        }

        [RelayCommand]
        public void ApplyToCurrent()
        {
            Result = ProfileImportDialogResult.ApplyToCurrentContext;
            RequestClose?.Invoke(Result);
        }

        [RelayCommand]
        public void Cancel()
        {
            Result = ProfileImportDialogResult.Cancel;
            RequestClose?.Invoke(Result);
        }
    }
}
