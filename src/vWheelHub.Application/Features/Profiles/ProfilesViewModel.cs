using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimRacingHub.Application.Common;
using SimRacingHub.Application.Contracts;
using SimRacingHub.Application.Localization;
using SimRacingHub.Core;
using SimRacingHub.Models;
using SimRacingHub.Services;

namespace SimRacingHub.Application.Features.Profiles
{
    public partial class ProfilesViewModel : ViewModelBase
    {
        private readonly ProfileSession _profileSession;
        private readonly ProfileManager _profileManager;
        private readonly ProfileTransferUseCases _transferUseCases;
        private readonly IUserDialogService _dialogService;
        private readonly IFilePickerService _filePickerService;
        private readonly IClipboardService _clipboardService;
        private readonly ILocalizationManager _localizationManager;
        private bool _adoptingContext;
        public ProfileSession ProfileSession => _profileSession;
        public ProfileManager ProfileManager => _profileManager;
        public ProfileTransferUseCases TransferUseCases => _transferUseCases;
        public IUserDialogService DialogService => _dialogService;
        public IFilePickerService FilePickerService => _filePickerService;
        public IClipboardService ClipboardService => _clipboardService;
        public ILocalizationManager LocalizationManager => _localizationManager;
        public ObservableCollection<ProfileItemViewModel> AvailableProfiles { get; } = new();

        [ObservableProperty]
        private ProfileItemViewModel? _selectedProfile;
        [ObservableProperty]
        private ProfileContext _currentContext = new("Universal");
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasPendingContext))]
        private ProfileContext? _pendingContext;
        public bool HasPendingContext => PendingContext != null;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasImportPreview))]
        private ProfileSharePackage? _importPreview;
        public bool HasImportPreview => ImportPreview != null;
        public Profile CurrentProfile => _profileSession.CurrentProfile;
        public ResolvedProfile ResolvedProfile => _profileSession.ResolvedProfile;
        public bool HasUnsavedChanges => _profileSession.HasUnsavedChanges;
        public bool CanUndo => _profileSession.CanUndo;
        public bool IsNewProfile => _profileSession.IsNewProfile;
        public string ActiveProfileDisplayName => SelectedProfile?.IsDefault == true ? _localizationManager.GetString("Common_Universal", "Universal") : CurrentProfile.Name ?? string.Empty;
        public string ContextDisplayPath => CurrentContext.DisplayPath == "Universal" ? _localizationManager.GetString("Common_Universal", "Universal") : DetectedProfileNames.Path(CurrentContext, CurrentProfile.Name);
        public ObservableCollection<string> AvailableGames { get; } = new();
        public ObservableCollection<string> AvailableClasses { get; } = new();
        public ObservableCollection<string> AvailableCars { get; } = new();

        public ProfilesViewModel(ProfileSession profileSession, ProfileManager profileManager, ProfileTransferUseCases transferUseCases, IUserDialogService dialogService, IFilePickerService filePickerService, IClipboardService clipboardService, ILocalizationManager localizationManager)
        {
            _profileSession = profileSession ?? throw new ArgumentNullException(nameof(profileSession));
            _profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));
            _transferUseCases = transferUseCases ?? throw new ArgumentNullException(nameof(transferUseCases));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _filePickerService = filePickerService ?? throw new ArgumentNullException(nameof(filePickerService));
            _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
            _localizationManager = localizationManager ?? throw new ArgumentNullException(nameof(localizationManager));
            _profileSession.UndoStateChanged += OnSessionUndoStateChanged;
            _localizationManager.LanguageChanged += _ => RefreshLocalizedLabels();
            AvailableProfiles.CollectionChanged += (_, _) => RebuildBrowser();
            if (_profileSession.CurrentProfile.Name == null)
                _profileSession.LoadContext(CurrentContext);
            RefreshAvailableProfiles();
            RefreshDropdownCollections();
        }

        private void OnSessionUndoStateChanged(object? sender, EventArgs e)
        {
            RefreshComparison();
            OnPropertyChanged(nameof(ProfileChangesText));
            OnPropertyChanged(nameof(ActiveProfileDisplayName));
            OnPropertyChanged(nameof(CurrentProfile));
            OnPropertyChanged(nameof(ResolvedProfile));
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(IsNewProfile));
            RevertProfileCommand.NotifyCanExecuteChanged();
        }

        partial void OnSelectedProfileChanged(ProfileItemViewModel? oldValue, ProfileItemViewModel? newValue)
        {
            if (_adoptingContext)
                return;
            if (newValue != null)
            {
                CurrentContext = newValue.Context;
                _profileSession.LoadContext(newValue.Context);
                ExpandSelectedBranch();
                UpdateBrowserRows();
                RefreshDropdownCollections();
                NotifyProfileProperties();
            }
        }

        public void AdoptLoadedContext(ProfileContext context)
        {
            _adoptingContext = true;
            try
            {
                CurrentContext = context;
                SelectedProfile = AvailableProfiles.FirstOrDefault(p => p.Context.Equals(context));
                ExpandSelectedBranch();
                UpdateBrowserRows();
                RefreshDropdownCollections();
                NotifyProfileProperties();
            }
            finally
            {
                _adoptingContext = false;
            }
        }

        private void NotifyProfileProperties()
        {
            RefreshComparison(reloadBaseline: true);
            OnPropertyChanged(nameof(ProfileChangesText));
            OnPropertyChanged(nameof(ActiveProfileDisplayName));
            OnPropertyChanged(nameof(ContextDisplayPath));
            OnPropertyChanged(nameof(CurrentProfile));
            OnPropertyChanged(nameof(ResolvedProfile));
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(IsNewProfile));
            RevertProfileCommand.NotifyCanExecuteChanged();
        }

        private void RefreshLocalizedLabels()
        {
            RefreshComparison();
            OnPropertyChanged(nameof(ProfileChangesText));
            var label = _localizationManager.GetString("Common_Universal", "Universal");
            foreach (var item in AvailableProfiles)
                item.SystemProfileLabel = label;
            RebuildBrowser();
            OnPropertyChanged(nameof(ActiveProfileDisplayName));
            OnPropertyChanged(nameof(ContextDisplayPath));
        }

        public void RefreshAvailableProfiles(ProfileContext? contextToSelect = null)
        {
            if (contextToSelect != null)
                ProfileSearchQuery = string.Empty;
            var items = new List<ProfileItemViewModel>();
            var universalContext = new ProfileContext("Universal");
            items.Add(new ProfileItemViewModel("Universal", universalContext, isDefault: true, canDelete: false, canRename: false));
            string universalDir = Path.Combine(_profileManager.ProfileDirectory, "Universal");
            if (Directory.Exists(universalDir))
            {
                foreach (var file in Directory.GetFiles(universalDir, "*.json"))
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    if (!name.Equals("Default", StringComparison.OrdinalIgnoreCase) && !name.Contains(".corrupt-", StringComparison.OrdinalIgnoreCase) && !name.EndsWith(".schema-v1.bak", StringComparison.OrdinalIgnoreCase))
                    {
                        var ctx = new ProfileContext("Universal", "", name);
                        items.Add(new ProfileItemViewModel(name, ctx, isDefault: false, canDelete: true, canRename: true));
                    }
                }
            }

            foreach (var game in _profileManager.GetAvailableGames())
            {
                var gameCtx = new ProfileContext(game);
                items.Add(new ProfileItemViewModel(game, gameCtx, isDefault: false, canDelete: true, canRename: true));
                foreach (var car in _profileManager.GetAvailableCars(game, ""))
                    items.Add(new ProfileItemViewModel(car, new ProfileContext(game, "", car)));
                foreach (var carClass in _profileManager.GetAvailableClasses(game))
                {
                    var classCtx = new ProfileContext(game, carClass);
                    items.Add(new ProfileItemViewModel($"{game} - {carClass}", classCtx, isDefault: false, canDelete: true, canRename: true));
                    foreach (var car in _profileManager.GetAvailableCars(game, carClass))
                    {
                        var carCtx = new ProfileContext(game, carClass, car);
                        items.Add(new ProfileItemViewModel(DisplayNameForDetectedCar(carCtx), carCtx, isDefault: false, canDelete: true, canRename: true));
                    }
                }
            }

            _refreshingProfiles = true;
            AvailableProfiles.Clear();
            foreach (var item in items)
            {
                item.SystemProfileLabel = _localizationManager.GetString("Common_Universal", "Universal");
                AvailableProfiles.Add(item);
            }

            _refreshingProfiles = false;
            RebuildBrowser();
            if (contextToSelect != null)
            {
                SelectedProfile = AvailableProfiles.FirstOrDefault(p => string.Equals(p.Context.Game, contextToSelect.Game, StringComparison.OrdinalIgnoreCase) && string.Equals(p.Context.CarClass, contextToSelect.CarClass, StringComparison.OrdinalIgnoreCase) && string.Equals(p.Context.Car, contextToSelect.Car, StringComparison.OrdinalIgnoreCase)) ?? AvailableProfiles.FirstOrDefault(p => string.Equals(p.Name, contextToSelect.Game, StringComparison.OrdinalIgnoreCase)) ?? AvailableProfiles.FirstOrDefault();
            }
            else
                AdoptLoadedContext(CurrentContext);
        }

        public void RefreshDropdownCollections()
        {
            var newGames = new List<string>
            {
                "Universal"
            };
            newGames.AddRange(_profileManager.GetAvailableGames());
            if (!AvailableGames.SequenceEqual(newGames))
            {
                AvailableGames.Clear();
                foreach (var g in newGames)
                    AvailableGames.Add(g);
            }

            var newClasses = new List<string>
            {
                ""
            };
            newClasses.AddRange(_profileManager.GetAvailableClasses(CurrentContext.Game));
            if (!AvailableClasses.SequenceEqual(newClasses))
            {
                AvailableClasses.Clear();
                foreach (var c in newClasses)
                    AvailableClasses.Add(c);
            }

            var newCars = new List<string>
            {
                ""
            };
            newCars.AddRange(_profileManager.GetAvailableCars(CurrentContext.Game, CurrentContext.CarClass));
            if (!AvailableCars.SequenceEqual(newCars))
            {
                AvailableCars.Clear();
                foreach (var car in newCars)
                    AvailableCars.Add(car);
            }
        }

#region Profile Commands (Create, Rename, Delete, Duplicate)
        /// <summary>
        /// Creates a new profile with the given name and parent context.
        /// </summary>
        [RelayCommand]
        public async Task<ProfileItemViewModel?> CreateProfileAsync(string? name = null)
        {
            string finalName = !string.IsNullOrWhiteSpace(name) ? ProfileManager.SanitizeName(name.Trim()) : GenerateUniqueProfileName();
            if (string.IsNullOrWhiteSpace(finalName))
            {
                await _dialogService.ShowNoticeAsync(_localizationManager.GetString("Ui_Message_InvalidProfileName", "Invalid profile name"), _localizationManager.GetString("Ui_Message_EnterProfileName", "Enter a valid profile name."), DialogSeverity.Warning);
                return null;
            }

            ProfileContext targetContext;
            if (SelectedProfile != null && !string.IsNullOrEmpty(SelectedProfile.Context.CarClass))
            {
                targetContext = new ProfileContext(SelectedProfile.Context.Game, SelectedProfile.Context.CarClass, finalName);
            }
            else if (SelectedProfile != null && !string.IsNullOrEmpty(SelectedProfile.Context.Game) && !SelectedProfile.Context.Game.Equals("Universal", StringComparison.OrdinalIgnoreCase))
            {
                targetContext = new ProfileContext(SelectedProfile.Context.Game, "General", finalName);
            }
            else
            {
                targetContext = new ProfileContext(finalName);
            }

            if (File.Exists(_profileManager.GetProfilePath(targetContext)))
            {
                await _dialogService.ShowNoticeAsync(_localizationManager.GetString("Ui_Message_InvalidProfileName", "Invalid profile name"), string.Format(_localizationManager.GetString("Ui_ProfileExistsFormat", "A profile named '{0}' already exists."), finalName), DialogSeverity.Warning);
                return null;
            }

            var newProfile = ProfileDefaults.CreateDefault(targetContext.Game ?? "Universal");
            newProfile.Name = finalName;
            bool saved = _profileManager.SaveContext(targetContext, newProfile);
            if (!saved)
            {
                await _dialogService.ShowNoticeAsync(_localizationManager.GetString("Ui_Message_1a05fda18b", "Creation Failed"), _localizationManager.GetString("Ui_Message_880328aec7", "Could not save the new profile."), DialogSeverity.Error);
                return null;
            }

            RefreshAvailableProfiles(targetContext);
            return SelectedProfile;
        }

        /// <summary>
        /// Renames the currently selected profile.
        /// </summary>
        [RelayCommand]
        public async Task<bool> RenameProfileAsync(string newName)
        {
            if (SelectedProfile == null)
                return false;
            if (!SelectedProfile.CanRename)
            {
                await _dialogService.ShowNoticeAsync(_localizationManager.GetString("Ui_Message_269c7045f7", "Cannot Rename"), _localizationManager.GetString("Ui_Message_d4c84c795f", "The default profile cannot be renamed."), DialogSeverity.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(newName))
            {
                await _dialogService.ShowNoticeAsync(_localizationManager.GetString("Ui_Message_13e348fdd0", "Invalid Name"), _localizationManager.GetString("Ui_Message_fda3dbb681", "Profile name cannot be empty."), DialogSeverity.Warning);
                return false;
            }

            string cleanNewName = ProfileManager.SanitizeName(newName);
            if (string.IsNullOrEmpty(cleanNewName))
            {
                await _dialogService.ShowNoticeAsync(_localizationManager.GetString("Ui_Message_13e348fdd0", "Invalid Name"), _localizationManager.GetString("Ui_Message_cd0e64d3e9", "Profile name contains only invalid characters."), DialogSeverity.Warning);
                return false;
            }

            string oldPath = _profileManager.GetProfilePath(SelectedProfile.Context);
            ProfileContext newContext;
            if (!string.IsNullOrEmpty(SelectedProfile.Context.Car))
            {
                newContext = new ProfileContext(SelectedProfile.Context.Game, SelectedProfile.Context.CarClass, cleanNewName);
            }
            else if (!string.IsNullOrEmpty(SelectedProfile.Context.CarClass))
            {
                newContext = new ProfileContext(SelectedProfile.Context.Game, cleanNewName);
            }
            else
            {
                newContext = new ProfileContext(cleanNewName);
            }

            string newPath = _profileManager.GetProfilePath(newContext);
            if (File.Exists(newPath) && !string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
            {
                await _dialogService.ShowNoticeAsync(_localizationManager.GetString("Ui_Message_b89e58be8a", "Rename Failed"), string.Format(System.Globalization.CultureInfo.CurrentUICulture, _localizationManager.GetString("Ui_Message_2fdef742b6", "A profile with name '{0}' already exists."), cleanNewName), DialogSeverity.Warning);
                return false;
            }

            CurrentProfile.Name = cleanNewName;
            bool saved = _profileManager.SaveContext(newContext, CurrentProfile);
            if (!saved)
            {
                await _dialogService.ShowNoticeAsync(_localizationManager.GetString("Ui_Message_b89e58be8a", "Rename Failed"), _localizationManager.GetString("Ui_Message_50363249c1", "Failed to save renamed profile."), DialogSeverity.Error);
                return false;
            }

            if (File.Exists(oldPath) && !string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    File.Delete(oldPath);
                    string? oldDir = Path.GetDirectoryName(oldPath);
                    if (oldDir != null && Directory.Exists(oldDir) && !Directory.EnumerateFileSystemEntries(oldDir).Any())
                    {
                        Directory.Delete(oldDir);
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Instance.LogError($"Failed to delete old profile file after rename: {oldPath}", ex);
                }
            }

            RefreshAvailableProfiles(newContext);
            return true;
        }

        /// <summary>
        /// Deletes the specified profile, or the currently selected profile.
        /// </summary>
        [RelayCommand]
        public async Task<bool> DeleteProfileAsync(ProfileItemViewModel? profile = null)
        {
            var target = profile ?? SelectedProfile;
            if (target == null)
                return false;
            if (!target.CanDelete)
            {
                await _dialogService.ShowNoticeAsync(_localizationManager.GetString("Ui_Message_cfe1f0c0c8", "Cannot Delete"), _localizationManager.GetString("Ui_Message_c992320e77", "Default system profiles cannot be deleted."), DialogSeverity.Warning);
                return false;
            }

            bool confirmed = await _dialogService.ShowConfirmAsync(_localizationManager.GetString("Ui_Message_4e5fc6f52f", "Delete Profile"), string.Format(System.Globalization.CultureInfo.CurrentUICulture, _localizationManager.GetString("Ui_Message_785fb55893", "Are you sure you want to delete profile '{0}'?"), target.Name), confirmText: "Delete", cancelText: "Cancel");
            if (!confirmed)
                return false;
            string path = _profileManager.GetProfilePath(target.Context);
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    string? dir = Path.GetDirectoryName(path);
                    if (dir != null && Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                    {
                        Directory.Delete(dir);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError($"Failed to delete profile file {path}", ex);
                await _dialogService.ShowNoticeAsync(_localizationManager.GetString("Ui_Message_6e455f706d", "Delete Failed"), string.Format(System.Globalization.CultureInfo.CurrentUICulture, _localizationManager.GetString("Ui_Message_f28cbdfe56", "Could not delete profile file: {0}"), ex.Message), DialogSeverity.Error);
                return false;
            }

            AvailableProfiles.Remove(target);
            if (SelectedProfile == target)
            {
                SelectedProfile = AvailableProfiles.FirstOrDefault();
            }

            return true;
        }

        /// <summary>
        /// Duplicates the current profile under a new name.
        /// </summary>
        [RelayCommand]
        public async Task<ProfileItemViewModel?> DuplicateProfileAsync(string? newName = null)
        {
            if (SelectedProfile == null)
                return null;
            bool explicitName = !string.IsNullOrWhiteSpace(newName);
            string baseName = ProfileManager.SanitizeName(explicitName ? newName!.Trim() : string.Format(System.Globalization.CultureInfo.CurrentUICulture, _localizationManager.GetString("Ui_Message_e94edb2b38", "{0} (Copy)"), SelectedProfile.Name));
            if (string.IsNullOrWhiteSpace(baseName))
            {
                await _dialogService.ShowNoticeAsync(_localizationManager.GetString("Ui_Message_InvalidProfileName"), _localizationManager.GetString("Ui_Message_EnterProfileName"), DialogSeverity.Warning);
                return null;
            }

            ProfileContext CopyContext(string name) => !string.IsNullOrEmpty(SelectedProfile.Context.Car) ? new(SelectedProfile.Context.Game, SelectedProfile.Context.CarClass, name) : !string.IsNullOrEmpty(SelectedProfile.Context.CarClass) ? new(SelectedProfile.Context.Game, SelectedProfile.Context.CarClass, name) : new(name);
            string finalName = baseName;
            var duplicateContext = CopyContext(finalName);
            for (int suffix = 2; File.Exists(_profileManager.GetProfilePath(duplicateContext)); suffix++)
            {
                if (explicitName)
                {
                    await _dialogService.ShowNoticeAsync(_localizationManager.GetString("Ui_Message_2d6ed53e7f"), string.Format(_localizationManager.GetString("Ui_ProfileExistsFormat"), finalName), DialogSeverity.Warning);
                    return null;
                }

                string ending = $" ({suffix})";
                finalName = baseName[..Math.Min(baseName.Length, 100 - ending.Length)] + ending;
                duplicateContext = CopyContext(finalName);
            }

            var clone = new Profile();
            clone.CopyFrom(CurrentProfile);
            clone.Name = finalName;
            bool saved = _profileManager.SaveContext(duplicateContext, clone);
            if (!saved)
            {
                await _dialogService.ShowNoticeAsync(_localizationManager.GetString("Ui_Message_2d6ed53e7f", "Duplicate Failed"), _localizationManager.GetString("Ui_Message_4701ba3995", "Failed to save duplicated profile."), DialogSeverity.Error);
                return null;
            }

            RefreshAvailableProfiles(duplicateContext);
            return SelectedProfile;
        }

#endregion
#region Save / Revert
        /// <summary>
        /// Saves the active profile modifications to disk.
        /// </summary>
        [RelayCommand]
        public void SaveProfile()
        {
            var ctx = SelectedProfile?.Context ?? CurrentContext;
            if (_profileSession.SaveProfile(ctx))
            {
                NotifyProfileProperties();
            }
        }

        /// <summary>
        /// Reverts the latest change made to the active profile.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanUndo))]
        public void RevertProfile()
        {
            _profileSession.RevertProfile();
            NotifyProfileProperties();
        }

#endregion
#region Transfer Commands (Export & Import)
        /// <summary>
        /// Exports the active profile to a file using the file picker.
        /// Gracefully handles dialog cancellation without modifying profile state.
        /// </summary>
        [RelayCommand]
        public async Task<bool> ExportToFileAsync()
        {
            SetBusy(true, _localizationManager.GetString("Ui_Message_8678e199a7", "Exporting profile to file..."));
            try
            {
                var metadata = new ProfileMetadata(Name: SelectedProfile?.Name ?? "Preset", CreatedAtUtc: DateTime.UtcNow);
                var request = new ProfileExportRequest(CurrentProfile, metadata);
                bool exported = await _transferUseCases.ExportToFileAsync(request, SelectedProfile?.Context ?? CurrentContext);
                return exported;
            }
            finally
            {
                SetBusy(false);
            }
        }

        /// <summary>
        /// Exports the active profile to the clipboard as a compressed share code.
        /// </summary>
        [RelayCommand]
        public async Task<string?> ExportToClipboardAsync()
        {
            SetBusy(true, _localizationManager.GetString("Ui_Message_38d03c629a", "Exporting profile to clipboard..."));
            try
            {
                var metadata = new ProfileMetadata(Name: SelectedProfile?.Name ?? "Preset", CreatedAtUtc: DateTime.UtcNow);
                var request = new ProfileExportRequest(CurrentProfile, metadata);
                string? code = await _transferUseCases.ExportToClipboardAsync(request, SelectedProfile?.Context ?? CurrentContext);
                if (code != null)
                {
                    await _dialogService.ShowNoticeAsync(_localizationManager.GetString("Ui_Message_a7e6dbd845", "Export Successful"), _localizationManager.GetString("Ui_Message_147304a111", "Share code copied to clipboard! You can paste it anywhere."), DialogSeverity.Info);
                }

                return code;
            }
            finally
            {
                SetBusy(false);
            }
        }

        /// <summary>
        /// Imports a profile from a file using the file picker.
        /// Handles cancel picker without modifying the active profile.
        /// Prompts confirmation if overwriting unsaved changes.
        /// </summary>
        [RelayCommand]
        public async Task<bool> ImportFromFileAsync()
        {
            SetBusy(true, _localizationManager.GetString("Ui_Message_bcebda692c", "Selecting file to import..."));
            try
            {
                var result = await _transferUseCases.ImportFromFileAsync();
                if (!result.Success)
                {
                    if (result.ErrorMessage != null && !result.ErrorMessage.Contains("cancelled", StringComparison.OrdinalIgnoreCase))
                    {
                        await _dialogService.ShowNoticeAsync(_localizationManager.GetString("Ui_Message_09876dabd9", "Import Failed"), result.ErrorMessage, DialogSeverity.Error);
                    }

                    return false;
                }

                return await ApplyImportResultAsync(result);
            }
            finally
            {
                SetBusy(false);
            }
        }

        /// <summary>
        /// Imports a profile from the system clipboard.
        /// Prompts confirmation if overwriting unsaved changes.
        /// </summary>
        [RelayCommand]
        public async Task<bool> ImportFromClipboardAsync()
        {
            SetBusy(true, _localizationManager.GetString("Ui_Message_d826e1f97e", "Reading clipboard..."));
            try
            {
                var result = await _transferUseCases.ImportFromClipboardAsync();
                if (!result.Success)
                {
                    await _dialogService.ShowNoticeAsync(_localizationManager.GetString("Ui_Message_09876dabd9", "Import Failed"), result.ErrorMessage ?? _localizationManager.GetString("Ui_Message_e7286e0417", "Clipboard does not contain valid preset data."), DialogSeverity.Warning);
                    return false;
                }

                return await ApplyImportResultAsync(result);
            }
            finally
            {
                SetBusy(false);
            }
        }

        public async Task<bool> SaveImportedToTargetAsync(ProfileSharePackage package)
        {
            if (package?.Settings == null || package.TargetContext == null || string.IsNullOrWhiteSpace(package.TargetContext.Game)) return false;
            var target = new ProfileContext(package.TargetContext.Game, package.TargetContext.CarClass, package.TargetContext.Car);
            if (File.Exists(_profileManager.GetProfilePath(target)) &&
                !await _dialogService.ShowConfirmAsync(_localizationManager.GetString("Import_TargetExistsTitle"),
                    string.Format(_localizationManager.GetString("Import_TargetExists"), target.DisplayPath),
                    _localizationManager.GetString("Import_Replace"), _localizationManager.GetString("Common_Cancel"))) return false;
            var imported = new Profile();
            imported.CopyFrom(package.Settings);
            imported.Name = !string.IsNullOrEmpty(target.Car) ? target.Car :
                !string.IsNullOrEmpty(target.CarClass) ? target.CarClass : target.Game;
            if (!_profileManager.SaveContext(target, imported))
            {
                await _dialogService.ShowNoticeAsync(_localizationManager.GetString("Ui_Message_09876dabd9"),
                    _localizationManager.GetString("Import_SaveFailed"), DialogSeverity.Error);
                return false;
            }
            RefreshAvailableProfiles(target);
            return true;
        }

        private async Task<bool> ApplyImportResultAsync(ProfileImportResult result)
        {
            if (result.ImportedProfile == null)
                return false;
            if (HasUnsavedChanges)
            {
                bool overwrite = await _dialogService.ShowConfirmAsync(_localizationManager.GetString("Ui_Message_04fee11023", "Overwrite Changes"), _localizationManager.GetString("Ui_Message_03c467e5ac", "You have unsaved changes in the current profile. Overwrite with the imported preset?"), confirmText: "Overwrite", cancelText: "Cancel");
                if (!overwrite)
                {
                    return false;
                }
            }

            _profileSession.ApplySettings(result.ImportedProfile, "ImportProfile");
            ImportPreview = result.Package;
            NotifyProfileProperties();
            await _dialogService.ShowNoticeAsync(_localizationManager.GetString("Ui_Message_fd8c48f36d", "Import Successful"), $"Successfully imported preset '{result.Metadata?.Name ?? result.Package?.Name ?? "Preset"}'.", DialogSeverity.Info);
            return true;
        }

        [RelayCommand]
        public void ClearImportPreview()
        {
            ImportPreview = null;
        }

        [RelayCommand]
        public void ApplyImportPreview()
        {
            if (ImportPreview?.Settings != null)
            {
                _profileSession.ApplySettings(ImportPreview.Settings, "ImportProfile");
                ImportPreview = null;
                NotifyProfileProperties();
            }
        }

#endregion
#region Pending Context
        [RelayCommand]
        public void SwitchToPendingContext()
        {
            if (PendingContext != null)
            {
                var target = PendingContext;
                PendingContext = null;
                CurrentContext = target;
                _profileSession.LoadContext(target);
                RefreshAvailableProfiles(target);
            }
        }

        [RelayCommand]
        public void SaveAndSwitchToPendingContext()
        {
            if (PendingContext != null)
            {
                if (!_profileSession.SaveProfile(SelectedProfile?.Context ?? CurrentContext))
                    return;
                NotifyProfileProperties();
                var target = PendingContext;
                PendingContext = null;
                CurrentContext = target;
                _profileSession.LoadContext(target);
                RefreshAvailableProfiles(target);
            }
        }

        [RelayCommand]
        public void IgnorePendingContext()
        {
            PendingContext = null;
        }

#endregion
        private string GenerateUniqueProfileName()
        {
            int counter = 1;
            while (true)
            {
                string candidate = string.Format(System.Globalization.CultureInfo.CurrentUICulture, _localizationManager.GetString("Ui_Message_3e1a431d86", "New Profile {0}"), counter);
                if (!AvailableProfiles.Any(p => string.Equals(p.Name, candidate, StringComparison.OrdinalIgnoreCase)))
                {
                    return candidate;
                }

                counter++;
            }
        }
    }
}
