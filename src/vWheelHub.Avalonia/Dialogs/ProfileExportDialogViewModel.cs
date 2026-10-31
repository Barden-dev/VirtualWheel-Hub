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
using FilePickerSaveOptions = SimRacingHub.Application.Contracts.FilePickerSaveOptions;

namespace SimRacingHub.Avalonia.Dialogs
{
    /// <summary>
    /// ViewModel for the profile export modal dialog, supporting export to compressed share-code
    /// via clipboard and structured file export via file picker.
    /// </summary>
    public partial class ProfileExportDialogViewModel : ObservableObject
    {
        private readonly Profile _profile;
        private readonly ProfileContext _context;
        private readonly IClipboardService _clipboardService;
        private readonly IFilePickerService _filePickerService;
        private readonly ILocalizationManager _localizationManager;
        private readonly IAppMetadata? _appMetadata;
        private readonly ProfilePackageCodec _codec;

        public Profile Profile => _profile;
        public ProfileContext Context => _context;

        [ObservableProperty]
        private string _presetName = "Preset";

        [ObservableProperty]
        private string _author = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
        private string? _statusMessage;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusColor))]
        private bool _isSuccess = true;

        public bool HasStatusMessage => !string.IsNullOrEmpty(StatusMessage);
        public string StatusColor => IsSuccess ? "#4ADE80" : "#F87171";

        public string ContextDisplayText => !string.IsNullOrEmpty(_context.DisplayPath)
            ? _context.DisplayPath
            : (_localizationManager?.GetString("Common_Universal", "Universal") ?? "Universal");

        public event Action? RequestClose;

        public ProfileExportDialogViewModel(
            Profile profile,
            ProfileContext context,
            IClipboardService clipboardService,
            IFilePickerService filePickerService,
            ILocalizationManager? localizationManager = null,
            IAppMetadata? appMetadata = null,
            ProfilePackageCodec? codec = null)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _context = context ?? new ProfileContext("Universal");
            _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
            _filePickerService = filePickerService ?? throw new ArgumentNullException(nameof(filePickerService));
            _localizationManager = localizationManager ?? new LocalizationManager();
            _appMetadata = appMetadata;
            _codec = codec ?? new ProfilePackageCodec();

            InitDefaultName();
        }

        private void InitDefaultName()
        {
            if (!string.IsNullOrEmpty(_context.Car))
            {
                PresetName = string.Format(System.Globalization.CultureInfo.CurrentUICulture, _localizationManager.GetString("Ui_Message_2bd3deb779", "{0} Setup"), _context.Car);
            }
            else if (!string.IsNullOrEmpty(_context.Game) && _context.Game != "Universal")
            {
                PresetName = string.Format(System.Globalization.CultureInfo.CurrentUICulture, _localizationManager.GetString("Ui_Message_2bd3deb779", "{0} Setup"), _context.Game);
            }
            else
            {
                PresetName = _localizationManager.GetString("Ui_Message_50f06ca758", "Universal Setup");
            }
        }

        public ProfileSharePackage BuildPackage()
        {
            return new ProfileSharePackage
            {
                SchemaVersion = ProfilePackageCodec.SupportedSchemaVersion,
                AppVersion = _appMetadata?.Version ?? "2.0.0",
                Name = !string.IsNullOrWhiteSpace(PresetName) ? PresetName.Trim() : "Preset",
                Author = !string.IsNullOrWhiteSpace(Author) ? Author.Trim() : string.Empty,
                Description = !string.IsNullOrWhiteSpace(Description) ? Description.Trim() : string.Empty,
                CreatedAtUtc = DateTime.UtcNow,
                TargetContext = new ProfileContext
                {
                    Game = _context.Game,
                    CarClass = _context.CarClass,
                    Car = _context.Car
                },
                Settings = _profile
            };
        }

        [RelayCommand]
        public async Task<bool> CopyCodeAsync()
        {
            try
            {
                var package = BuildPackage();
                string shareCode = _codec.EncodeShareCode(package);
                await _clipboardService.SetTextAsync(shareCode).ConfigureAwait(false);

                StatusMessage = _localizationManager.GetString("Ui_Message_8d35b49867", "Share Code copied to clipboard! Paste it anywhere (e.g. Discord).");
                IsSuccess = true;
                return true;
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(System.Globalization.CultureInfo.CurrentUICulture, _localizationManager.GetString("Ui_Message_a5126f46f8", "Failed to copy share code: {0}"), ex.Message);
                IsSuccess = false;
                return false;
            }
        }

        [RelayCommand]
        public async Task<bool> SaveFileAsync()
        {
            try
            {
                var package = BuildPackage();
                string suggestedFileName = $"{package.Name}.vwhub";

                var filters = new List<FileFilterItem>
                {
                    new FileFilterItem(_localizationManager.GetString("Ui_Message_e28e63462f", "vWheel Hub Preset (*.vwhub, *.json)"), new[] { ".vwhub", ".json" }),
                    new FileFilterItem(_localizationManager.GetString("Ui_Message_6ae05840ac", "All Files (*.*)"), new[] { "*.*" })
                };

                var options = new FilePickerSaveOptions(
                    title: _localizationManager.GetString("Ui_Message_07ad645ec6", "Export Profile Preset"),
                    defaultExtension: "vwhub",
                    filters: filters,
                    suggestedFileName: suggestedFileName);

                var pickedFile = await _filePickerService.SaveFileAsync(options).ConfigureAwait(false);
                if (pickedFile == null)
                {
                    // User cancelled picker
                    return false;
                }

                string json = _codec.EncodeJson(package, indented: true);
                await using (var stream = await pickedFile.OpenWriteAsync().ConfigureAwait(false))
                await using (var writer = new StreamWriter(stream))
                {
                    await writer.WriteAsync(json).ConfigureAwait(false);
                }

                StatusMessage = string.Format(System.Globalization.CultureInfo.CurrentUICulture, _localizationManager.GetString("Ui_Message_b803216545", "Saved file: {0}"), pickedFile.FileName);
                IsSuccess = true;
                return true;
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(System.Globalization.CultureInfo.CurrentUICulture, _localizationManager.GetString("Ui_Message_45f297da6c", "Failed to export file: {0}"), ex.Message);
                IsSuccess = false;
                return false;
            }
        }

        [RelayCommand]
        public void Close()
        {
            RequestClose?.Invoke();
        }
    }
}
