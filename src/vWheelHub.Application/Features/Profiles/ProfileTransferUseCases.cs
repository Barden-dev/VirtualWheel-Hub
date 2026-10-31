using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SimRacingHub.Application.Contracts;
using SimRacingHub.Core;
using SimRacingHub.Models;

namespace SimRacingHub.Application.Features.Profiles
{
    /// <summary>
    /// Application use cases for exporting and importing profiles to/from files and the system clipboard.
    /// Manages file streams asynchronously, ensuring handles and streams are safely disposed.
    /// </summary>
    public class ProfileTransferUseCases
    {
        private readonly IFilePickerService _filePickerService;
        private readonly IClipboardService _clipboardService;
        private readonly IUserDialogService _dialogService;
        private readonly ProfilePackageCodec _codec;

        public ProfilePackageCodec Codec => _codec;

        public ProfileTransferUseCases(
            IFilePickerService filePickerService,
            IClipboardService clipboardService,
            IUserDialogService dialogService,
            ProfilePackageCodec? codec = null)
        {
            _filePickerService = filePickerService ?? throw new ArgumentNullException(nameof(filePickerService));
            _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _codec = codec ?? new ProfilePackageCodec();
        }

        /// <summary>
        /// Exports a profile to a file using the file picker port.
        /// Returns false if the user cancelled the picker dialog.
        /// </summary>
        public async Task<bool> ExportToFileAsync(
            ProfileExportRequest request,
            ProfileContext? context = null,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(request.Profile);
            ct.ThrowIfCancellationRequested();

            var package = BuildSharePackage(request, context);
            string defaultFileName = _codec.GenerateDefaultFileName(package.TargetContext, package.Name);

            PickedFile? pickedFile;
            if (!string.IsNullOrWhiteSpace(request.DestinationFilePath))
            {
                pickedFile = PickedFile.FromFile(request.DestinationFilePath);
            }
            else
            {
                var filters = new[]
                {
                    new FileFilterItem("vWheel Hub Preset (*.vwh)", new[] { ".vwh" }),
                    new FileFilterItem("JSON File (*.json)", new[] { ".json" }),
                    new FileFilterItem("All Files (*.*)", new[] { ".*" })
                };

                var saveOptions = new FilePickerSaveOptions
                {
                    Title = "Export Profile Preset",
                    DefaultExtension = ".vwh",
                    Filters = filters,
                    SuggestedFileName = defaultFileName
                };

                pickedFile = await _filePickerService.SaveFileAsync(saveOptions, ct);
                if (pickedFile == null)
                {
                    // User cancelled the file picker dialog
                    return false;
                }
            }

            try
            {
                string json = _codec.EncodeJson(package, indented: true);
                byte[] utf8Bytes = Encoding.UTF8.GetBytes(json);

                await using var stream = await pickedFile.OpenWriteAsync();
                await stream.WriteAsync(utf8Bytes, ct);
                await stream.FlushAsync(ct);

                AppLogger.Instance.LogInfo($"Successfully exported profile preset '{package.Name}' to {pickedFile.FileName}");
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError($"Failed to export profile preset to {pickedFile.FileName}", ex);
                await _dialogService.ShowNoticeAsync(
                    "Export Failed",
                    $"Could not save profile file: {ex.Message}",
                    DialogSeverity.Error,
                    ct);
                return false;
            }
        }

        /// <summary>
        /// Exports a profile to the system clipboard as a compressed share code.
        /// Returns the share code string, or null on error.
        /// </summary>
        public async Task<string?> ExportToClipboardAsync(
            ProfileExportRequest request,
            ProfileContext? context = null,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(request.Profile);
            ct.ThrowIfCancellationRequested();

            try
            {
                var package = BuildSharePackage(request, context);
                string shareCode = _codec.EncodeShareCode(package);

                await _clipboardService.SetTextAsync(shareCode, ct);
                AppLogger.Instance.LogInfo($"Copied profile preset '{package.Name}' share code to clipboard.");
                return shareCode;
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError("Failed to export profile preset to clipboard", ex);
                await _dialogService.ShowNoticeAsync(
                    "Export Failed",
                    $"Could not copy share code: {ex.Message}",
                    DialogSeverity.Error,
                    ct);
                return null;
            }
        }

        /// <summary>
        /// Imports a profile from a file using the file picker port.
        /// Returns ProfileImportResult with Success = false if cancelled or corrupted.
        /// </summary>
        public async Task<ProfileImportResult> ImportFromFileAsync(
            string? sourceFilePath = null,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            PickedFile? pickedFile;
            if (!string.IsNullOrWhiteSpace(sourceFilePath))
            {
                pickedFile = PickedFile.FromFile(sourceFilePath);
            }
            else
            {
                var filters = new[]
                {
                    new FileFilterItem("vWheel Hub Preset (*.vwh;*.json)", new[] { ".vwh", ".json" }),
                    new FileFilterItem("All Files (*.*)", new[] { ".*" })
                };

                var openOptions = new FilePickerOpenOptions
                {
                    Title = "Import Profile Preset",
                    DefaultExtension = ".vwh",
                    Filters = filters
                };

                pickedFile = await _filePickerService.OpenFileAsync(openOptions, ct);
                if (pickedFile == null)
                {
                    // User cancelled the file picker dialog
                    return ProfileImportResult.Failed("User cancelled file selection.");
                }
            }

            try
            {
                string text;
                await using (var stream = await pickedFile.OpenReadAsync())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    text = await reader.ReadToEndAsync(ct);
                }

                var result = ImportFromText(text);
                if (result.Success)
                {
                    AppLogger.Instance.LogInfo($"Successfully imported profile preset from {pickedFile.FileName}");
                }
                else
                {
                    AppLogger.Instance.LogWarning($"Failed to import profile preset from {pickedFile.FileName}: {result.ErrorMessage}");
                }

                return result;
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError($"Failed to read file {pickedFile.FileName}", ex);
                return ProfileImportResult.Failed($"Error reading file: {ex.Message}");
            }
        }

        /// <summary>
        /// Imports a profile from the text contents of the system clipboard.
        /// </summary>
        public async Task<ProfileImportResult> ImportFromClipboardAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                string? clipboardText = await _clipboardService.GetTextAsync(ct);
                if (string.IsNullOrWhiteSpace(clipboardText))
                {
                    return ProfileImportResult.Failed("Clipboard is empty or does not contain text.");
                }

                var result = ImportFromText(clipboardText);
                if (result.Success)
                {
                    AppLogger.Instance.LogInfo("Successfully imported profile preset from clipboard.");
                }
                else
                {
                    AppLogger.Instance.LogWarning($"Failed to import profile preset from clipboard: {result.ErrorMessage}");
                }

                return result;
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError("Failed to access clipboard during import", ex);
                return ProfileImportResult.Failed($"Error accessing clipboard: {ex.Message}");
            }
        }

        /// <summary>
        /// Decodes a string representing either a compressed share code or a JSON envelope.
        /// </summary>
        public ProfileImportResult ImportFromText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return ProfileImportResult.Failed("Import string is empty.");
            }

            string trimmed = text.Trim();
            if (trimmed.StartsWith(ProfilePackageCodec.CodecPrefixV1, StringComparison.OrdinalIgnoreCase))
            {
                if (_codec.TryDecodeShareCode(trimmed, out var pkg, out var error))
                {
                    var meta = new ProfileMetadata(pkg!.Name, pkg.Author, pkg.Description, pkg.CreatedAtUtc);
                    return ProfileImportResult.Succeeded(pkg.Settings, meta, pkg);
                }
                return ProfileImportResult.Failed(error ?? "Invalid share code.");
            }

            if (_codec.TryDecodeJson(trimmed, out var jsonPkg, out var jsonError))
            {
                var meta = new ProfileMetadata(jsonPkg!.Name, jsonPkg.Author, jsonPkg.Description, jsonPkg.CreatedAtUtc);
                return ProfileImportResult.Succeeded(jsonPkg.Settings, meta, jsonPkg);
            }

            return ProfileImportResult.Failed(jsonError ?? "Invalid profile format.");
        }

        private static ProfileSharePackage BuildSharePackage(ProfileExportRequest request, ProfileContext? context)
        {
            string name = !string.IsNullOrWhiteSpace(request.Metadata?.Name)
                ? request.Metadata.Name.Trim()
                : (!string.IsNullOrWhiteSpace(request.Profile.Name) ? request.Profile.Name.Trim() : "Preset");

            var targetCtx = context ?? new ProfileContext("Universal");

            return new ProfileSharePackage
            {
                SchemaVersion = ProfilePackageCodec.SupportedSchemaVersion,
                Name = name,
                Author = request.Metadata?.Author?.Trim() ?? string.Empty,
                Description = request.Metadata?.Description?.Trim() ?? string.Empty,
                CreatedAtUtc = request.Metadata?.CreatedAtUtc ?? DateTime.UtcNow,
                TargetContext = new ProfileContext
                {
                    Game = targetCtx.Game,
                    CarClass = targetCtx.CarClass,
                    Car = targetCtx.Car
                },
                Settings = request.Profile
            };
        }
    }
}
