using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using SimRacingHub.Core;
using SimRacingHub.Models;
using SimRacingHub.Services;

namespace SimRacingHub.Application.Features.Output;

public partial class OutputViewModel
{
    [ObservableProperty] private int _migrationSourceVJoyId = 1;
    internal string? ProfileMigrationBackupDirectory { get; set; }
    [RelayCommand]
    public async Task MigrateVJoyProfilesAsync(CancellationToken ct = default)
    {
        if (!ShowVJoySettings || IsBusy || _dialogs == null) return;
        if (_runtime?.IsRunning == true) { StatusMessage = _localizationManager.GetString("Output_WheelStopFirst"); return; }
        if (_profileSession.HasUnsavedChanges) { StatusMessage = _localizationManager.GetString("Output_MigrationSaveFirst"); return; }
        var context = _profileSession.LoadedContext;
        if (context == null) return;
        try
        {
            SetBusy(true);
            using var setup = _runtime?.BeginOutputSetup();
            var inventory = await _outputDiagnostics.InspectVJoyAsync(ct);
            DisplayVJoyInventory(inventory);
            if (inventory.SelectedDeviceId is not { } id || inventory.SelectionError != null)
            { StatusMessage = _localizationManager.GetString("Output_VJoySelectCompatible"); return; }
            var device = inventory.Devices.Single(device => device.DeviceId == id);
            if (!device.IsUsable || !device.MeetsRequirements(true))
            { StatusMessage = _localizationManager.GetString("Output_VJoySelectCompatible"); return; }
            var service = new VJoyProfileMigration(_profileSession.ProfileManager.ProfileDirectory,
                ProfileMigrationBackupDirectory ?? Path.Combine(StoragePaths.LocalDataDirectory, "profile-migrations"));
            var preview = service.Preview(MigrationSourceVJoyId, id, device.Fingerprint!);
            if (preview.Changes.Length == 0) { StatusMessage = _localizationManager.GetString("Output_MigrationNoChanges"); return; }
            string rows = string.Join(Environment.NewLine, preview.Changes.Select(row => string.Format(
                _localizationManager.GetString(row.JoinSharedDevice ? "Output_MigrationJoin" : "Output_MigrationKeep"), row.Path, row.PreviousDeviceId, id)));
            string title = _localizationManager.GetString("Output_MigrationTitle");
            string body = string.Format(_localizationManager.GetString("Output_MigrationConfirm"), preview.PreviousDeviceId, id, rows, preview.BackupDirectory);
            if (!await _dialogs.ShowConfirmAsync(title, body, title, _localizationManager.GetString("Btn_Cancel"), ct: ct)) return;
            if (_profileSession.HasUnsavedChanges || _profileSession.LoadedContext?.Equals(context) != true)
            { StatusMessage = _localizationManager.GetString("Output_MigrationSaveFirst"); return; }
            var checkedInventory = await _outputDiagnostics.InspectVJoyAsync(ct);
            if (checkedInventory.SelectedDeviceId != id || checkedInventory.SelectionError != null ||
                checkedInventory.Devices.Single(device => device.DeviceId == id).Fingerprint != preview.SharedFingerprint)
                throw new IOException("The selected shared vJoy output changed after preview. No profiles were migrated.");
            ct.ThrowIfCancellationRequested();
            // Once file writes start, finish or restore before releasing the setup lease.
            var result = await Task.Run(() => service.Apply(preview));
            if (result.Success) _profileSession.ReloadLoadedContext();
            StatusMessage = result.Success ? string.Format(_localizationManager.GetString("Output_MigrationDone"), result.BackupDirectory)
                : _localizationManager.GetString("Output_MigrationFailed") + " " + result.Error + " " + result.BackupDirectory;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { StatusMessage = _localizationManager.GetString("Output_MigrationFailed") + " " + error.Message; }
        finally { SetBusy(false); }
    }
}
