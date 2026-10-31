using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimRacingHub.Application.Common;
using SimRacingHub.Application.Contracts;
using SimRacingHub.Application.Localization;
using SimRacingHub.Application.Updates;
using SimRacingHub.Core;

namespace SimRacingHub.Application.Features.AppSettings;

/// <summary>Owns update checks and banner state shared by both editions.</summary>
public partial class UpdateStatusViewModel : ViewModelBase
{
    private readonly IAppMetadata _appMetadata;
    private readonly ILocalizationManager _localizationManager;
    private readonly IExternalLauncher _externalLauncher;
    private readonly IUpdateCheckService _updateCheckService;
    private readonly Func<string> _releaseUrl;
    private string TargetReleaseUrl => _releaseUrl();

    public UpdateStatusViewModel(IAppMetadata appMetadata, ILocalizationManager localizationManager,
        IExternalLauncher externalLauncher, Func<string> releaseUrl, IUpdateCheckService updateCheckService)
    {
        _appMetadata = appMetadata;
        _localizationManager = localizationManager;
        _externalLauncher = externalLauncher;
        _releaseUrl = releaseUrl;
        _updateCheckService = updateCheckService;
    }

    public Func<CancellationToken, Task<UpdateCheckResult?>>? UpdateChecker { get; set; }

    [ObservableProperty]
    private bool _isUpdateAvailable;

    [ObservableProperty]
    private string? _latestVersion;

    [ObservableProperty]
    private string? _updateUrl;

    [ObservableProperty]
    private bool _isCheckingForUpdates;

    [ObservableProperty]
    private string? _updateStatusMessage;


    [RelayCommand]
    public async Task<bool> CheckForUpdatesAsync(CancellationToken ct = default)
    {
        IsCheckingForUpdates = true;
        UpdateStatusMessage = _localizationManager.GetString("Msg_Updates_Checking", "Checking for updates...");

        try
        {
            ct.ThrowIfCancellationRequested();

            UpdateCheckResult? result;
            if (UpdateChecker != null)
            {
                result = await UpdateChecker(ct);
            }
            else
            {
                result = await _updateCheckService.CheckAsync(_appMetadata.Version, TargetReleaseUrl, ct);
            }

            if (result == null)
            {
                UpdateStatusMessage = _localizationManager.GetString("Msg_Updates_Failed", "Failed to check for updates.");
                return false;
            }
            if (result.IsUpdateAvailable)
            {
                LatestVersion = result.LatestVersion;
                UpdateUrl = !string.IsNullOrWhiteSpace(result.UpdateUrl) ? result.UpdateUrl : TargetReleaseUrl;
                IsUpdateAvailable = true;
                UpdateStatusMessage = string.Format(
                    _localizationManager.GetString("Msg_Updates_NewVersionAvailableFormat", "A new version {0} is available!"),
                    LatestVersion);
                return true;
            }
            else
            {
                IsUpdateAvailable = false;
                UpdateStatusMessage = _localizationManager.GetString("Msg_Updates_UpToDate", "Application is up to date.");
                return false;
            }
        }
        catch (OperationCanceledException)
        {
            UpdateStatusMessage = _localizationManager.GetString("Msg_Updates_Cancelled", "Update check cancelled.");
            throw;
        }
        catch (Exception ex)
        {
            AppLogger.Instance.LogError("Failed to check for updates", ex);
            UpdateStatusMessage = _localizationManager.GetString("Msg_Updates_Failed", "Failed to check for updates.");
            return false;
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }

    [RelayCommand]
    public async Task OpenUpdateUrlAsync(CancellationToken ct = default)
    {
        string target = !string.IsNullOrWhiteSpace(UpdateUrl) ? UpdateUrl : TargetReleaseUrl;
        if (Uri.TryCreate(target, UriKind.Absolute, out var uri))
        {
            await _externalLauncher.OpenUriAsync(uri, ct);
        }
    }

    [RelayCommand]
    public void DismissUpdateBanner()
    {
        IsUpdateAvailable = false;
    }


}
