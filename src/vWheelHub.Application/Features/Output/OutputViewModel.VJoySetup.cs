using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimRacingHub.Application.Contracts;

namespace SimRacingHub.Application.Features.Output;

public sealed record VJoyDeviceOption(VJoyDeviceInfo Device, string Summary);

public partial class OutputViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWheelSetupMessage))]
    private string? _wheelSetupMessage;
    public bool HasWheelSetupMessage => !string.IsNullOrEmpty(WheelSetupMessage);

    // Normal setup chooses the output. Device maintenance never runs implicitly.
    [RelayCommand]
    public async Task ConnectWheelAsync(CancellationToken ct = default)
    {
        if (!ShowVJoySettings || IsBusy) return;
        if (_runtime?.IsRunning == true) { StatusMessage = _localizationManager.GetString("Output_WheelStopFirst"); return; }
        bool needsSetup = false;
        bool needsRepair = false;
        bool needsRestore = false;
        try
        {
            SetBusy(true);
            using var setup = _runtime?.BeginOutputSetup();
            var inventory = await _outputDiagnostics.InspectVJoyAsync(ct);
            DisplayVJoyInventory(inventory);
            if (inventory.SelectionError != null)
            {
                StatusMessage = VJoySelectionSummary;
                needsRestore = inventory.PendingOperationId == null && inventory.Devices.Any(device =>
                    device.DeviceId == inventory.SelectedDeviceId && device.IsAbsent);
                if (!needsRestore) return;
            }
            if (inventory.SelectedDeviceId != null && !needsRestore)
            {
                var selected = inventory.Devices.FirstOrDefault(device => device.DeviceId == inventory.SelectedDeviceId);
                if (selected is { IsUsable: true } && !selected.MeetsRequirements(true))
                {
                    SelectedVJoyDevice = VJoyDevices.First(option => option.Device.DeviceId == selected.DeviceId);
                    needsRepair = true;
                }
                else { StatusMessage = VirtualDeviceStateText; return; }
            }
            var compatible = inventory.SelectedDeviceId != null ? null : inventory.Devices.Where(device => device.IsUsable && device.MeetsRequirements(true))
                .OrderByDescending(device => device.DeviceId == VJoyDeviceId).ThenBy(device => device.DeviceId).FirstOrDefault();
            if (compatible != null)
            {
                if (!await _outputDiagnostics.SelectVJoyAsync(compatible, true, ct))
                { StatusMessage = _localizationManager.GetString("Output_VJoySelectionIssue"); return; }
                DisplayVJoyInventory(await _outputDiagnostics.InspectVJoyAsync(ct));
                StatusMessage = VirtualDeviceStateText;
            }
            else if (!needsRepair && !needsRestore) needsSetup = true;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { StatusMessage = _localizationManager.GetString("Output_VJoySelectionIssue") + " " + error.Message; }
        finally { SetBusy(false); WheelSetupMessage = StatusMessage; }
        if (needsRestore) await RestoreMissingVJoyAsync(ct);
        else if (needsRepair) await ChangeSelectedVJoyAsync(ct);
        else if (needsSetup) await PrepareWheelAsync(ct);
        WheelSetupMessage = StatusMessage;
    }

    private VJoyDeviceInventory? _vJoyInventory;
    public ObservableCollection<VJoyDeviceOption> VJoyDevices { get; } = new();
    public bool HasPendingVJoySetup => _vJoyInventory?.PendingOperationId != null;
    [ObservableProperty] private VJoyDeviceOption? _selectedVJoyDevice;

    public bool UseHubVJoyDevice
    {
        get => CurrentProfile?.UseHubVJoyDevice ?? ResolvedProfile?.UseHubVJoyDevice ?? false;
        set { if (CurrentProfile is { } profile) profile.UseHubVJoyDevice = value; }
    }

    public string VJoySelectionSummary => _vJoyInventory?.SelectionError is { } error
        ? _localizationManager.GetString("Output_VJoySelectionIssue") + " " + error
        : _vJoyInventory?.SelectedDeviceId is { } id
            ? string.Format(_localizationManager.GetString("Output_VJoySharedSelected"), id)
            : _localizationManager.GetString("Output_VJoyNotSelected");

    public string VJoyPrepareLabel => _vJoyInventory?.SelectedDeviceId != null
        ? _localizationManager.GetString("Output_VJoyCheck") : _localizationManager.GetString("Output_WheelPrepare");

    private void DisplayVJoyInventory(VJoyDeviceInventory inventory)
    {
        int? previous = SelectedVJoyDevice?.Device.DeviceId;
        _vJoyInventory = inventory;
        VJoyDevices.Clear();
        foreach (var device in inventory.Devices)
        {
            string key = device.Error != null ? "Output_VJoyUnknown" : device.Status switch
            {
                0 or 1 => "Output_VJoyExisting",
                2 => "Output_VJoyBusy",
                3 when device.ConfigurationExists == false => "Output_VJoyUnconfigured",
                3 => "Output_VJoyPersisted",
                _ => "Output_VJoyUnknown"
            };
            string text = string.Format(_localizationManager.GetString("Output_VJoyInventoryRow"), device.DeviceId,
                _localizationManager.GetString(key), device.Axes.Length, device.Buttons, device.DiscretePovs, device.ContinuousPovs);
            VJoyDevices.Add(new(device, text));
        }
        SelectedVJoyDevice = VJoyDevices.FirstOrDefault(option => option.Device.DeviceId == (previous ?? inventory.SelectedDeviceId ?? VJoyDeviceId))
            ?? VJoyDevices.FirstOrDefault(option => option.Device.IsUsable && option.Device.MeetsRequirements(VJoyWheelCompatibility));
        OnPropertyChanged(nameof(VJoySelectionSummary));
        OnPropertyChanged(nameof(VJoyPrepareLabel));
        OnPropertyChanged(nameof(HasPendingVJoySetup));
        OnPropertyChanged(nameof(VirtualButtonLimit));
        RefreshVirtualButtons();
        OnPropertyChanged(nameof(VirtualDeviceStateText));
    }

    [RelayCommand]
    public Task ResumePendingVJoyAsync(CancellationToken ct = default) => ResolvePendingVJoyAsync(true, ct);

    [RelayCommand]
    public Task CancelPendingVJoyAsync(CancellationToken ct = default) => ResolvePendingVJoyAsync(false, ct);

    private async Task ResolvePendingVJoyAsync(bool resume, CancellationToken ct)
    {
        if (!ShowVJoySettings || IsBusy || _dialogs == null) return;
        if (_runtime?.IsRunning == true) { StatusMessage = _localizationManager.GetString("Output_WheelStopFirst"); return; }
        try
        {
            SetBusy(true);
            using var setup = _runtime?.BeginOutputSetup();
            var pending = await _outputDiagnostics.InspectPendingVJoyAsync(ct);
            if (pending == null) { DisplayVJoyInventory(await _outputDiagnostics.InspectVJoyAsync(ct)); return; }
            if (!(resume ? pending.CanResume : pending.CanCancel))
            { StatusMessage = _localizationManager.GetString("Output_VJoyPendingConflict"); return; }
            string action = _localizationManager.GetString(resume ? "Output_VJoyResume" : "Output_VJoyCancelSetup");
            string body = string.Format(_localizationManager.GetString(resume ? pending.Purpose == "wheel-remove" ? "Output_VJoyResumeRemovalConfirm" : "Output_VJoyResumeConfirm" :
                pending.NeedsRegistryRestore || pending.CancelRemovesConfiguration ?
                    pending.CancelRemovesConfiguration ? "Output_VJoyCancelRestoreConfirm" : "Output_VJoyCancelRepairConfirm" : "Output_VJoyCancelUnwrittenConfirm"),
                pending.DeviceId, pending.BackupPath);
            if (!await _dialogs.ShowConfirmAsync(action, body, action, _localizationManager.GetString("Btn_Cancel"), ct: ct)) return;
            var result = await _outputDiagnostics.ResolvePendingVJoyAsync(pending, resume, ct);
            DisplayVJoyInventory(await _outputDiagnostics.InspectVJoyAsync(ct));
            if (resume && pending.Purpose == "wheel-remove" && result.Success) StatusMessage = _localizationManager.GetString("Output_VJoyRemoved");
            else if (resume) ShowVJoySetupResult(result);
            else StatusMessage = result.Success ? _localizationManager.GetString(result.RequiresRestart ?
                "Output_VJoyCancelledRestart" : "Output_VJoyCancelled") : _localizationManager.GetString(result.ErrorKey ?? "Output_VJoySetupStopped") + " " + result.Detail;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { StatusMessage = _localizationManager.GetString("Output_VJoySelectionIssue") + " " + error.Message; }
        finally { SetBusy(false); }
    }

    [RelayCommand]
    public async Task RemoveHubVJoyAsync(CancellationToken ct = default)
    {
        if (!ShowVJoySettings || IsBusy || _dialogs == null) return;
        if (_runtime?.IsRunning == true) { StatusMessage = _localizationManager.GetString("Output_WheelStopFirst"); return; }
        try
        {
            SetBusy(true);
            using var setup = _runtime?.BeginOutputSetup();
            var target = SelectedVJoyDevice?.Device;
            var preview = target != null && target.DeviceId != _vJoyInventory?.SelectedDeviceId
                ? await _outputDiagnostics.PreviewVJoyRemovalAsync(target, ct)
                : await _outputDiagnostics.PreviewVJoyRemovalAsync(ct);
            if (preview == null) return;
            string title = _localizationManager.GetString("Output_VJoyRemove");
            string body = string.Format(_localizationManager.GetString("Output_VJoyRemoveConfirm"), preview.Before.DeviceId,
                VJoyLayout(preview.Before), preview.BackupPath);
            if (!await _dialogs.ShowConfirmAsync(title, body, title, _localizationManager.GetString("Btn_Cancel"), ct: ct)) return;
            var result = await _outputDiagnostics.ApplyVJoyAsync(preview, ct);
            DisplayVJoyInventory(await _outputDiagnostics.InspectVJoyAsync(ct));
            if (result.Success) StatusMessage = _localizationManager.GetString("Output_VJoyRemoved");
            else ShowVJoySetupResult(result);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { StatusMessage = _localizationManager.GetString("Output_VJoySelectionIssue") + " " + error.Message; }
        finally { SetBusy(false); }
    }

    [RelayCommand]
    public async Task RestoreMissingVJoyAsync(CancellationToken ct = default)
    {
        if (!ShowVJoySettings || IsBusy || _dialogs == null) return;
        if (_runtime?.IsRunning == true) { StatusMessage = _localizationManager.GetString("Output_WheelStopFirst"); return; }
        try
        {
            SetBusy(true);
            using var setup = _runtime?.BeginOutputSetup();
            var preview = await _outputDiagnostics.PreviewMissingVJoyRestoreAsync(ct);
            if (preview == null) return;
            if (!await ConfirmVJoyPreviewAsync(preview, ct, "Output_VJoyRestoreMissing", "Output_VJoyRestoreMissingHint")) return;
            var result = await _outputDiagnostics.ApplyVJoyAsync(preview, ct);
            DisplayVJoyInventory(await _outputDiagnostics.InspectVJoyAsync(ct));
            ShowVJoySetupResult(result);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { StatusMessage = _localizationManager.GetString("Output_VJoySelectionIssue") + " " + error.Message; }
        finally { SetBusy(false); }
    }

    [RelayCommand]
    public async Task InspectVJoyAsync(CancellationToken ct = default)
    {
        if (!ShowVJoySettings || IsBusy) return;
        try
        {
            SetBusy(true, _localizationManager.GetString("Output_VJoyInspect"));
            DisplayVJoyInventory(await _outputDiagnostics.InspectVJoyAsync(ct));
            StatusMessage = VJoySelectionSummary;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { StatusMessage = _localizationManager.GetString("Output_VJoySelectionIssue") + " " + error.Message; }
        finally { SetBusy(false); }
    }

    [RelayCommand]
    public async Task ChangeSelectedVJoyAsync(CancellationToken ct = default)
    {
        if (!ShowVJoySettings || IsBusy || _dialogs == null || SelectedVJoyDevice == null || CurrentProfile == null) return;
        if (_runtime?.IsRunning == true) { StatusMessage = _localizationManager.GetString("Output_WheelStopFirst"); return; }
        var selected = SelectedVJoyDevice.Device;
        var profile = CurrentProfile;
        try
        {
            SetBusy(true, _localizationManager.GetString("Output_VJoyPreview"));
            using var setup = _runtime?.BeginOutputSetup();
            var preview = await _outputDiagnostics.PreviewVJoyAsync(selected, changeExisting: true, ct);
            if (preview == null) { StatusMessage = _localizationManager.GetString("Output_VJoyUseSetupCard"); return; }
            if (!preview.NeedsChange) { StatusMessage = _localizationManager.GetString("Output_VJoyReadyUnchanged"); return; }
            // Creating the shared output uses the dedicated first-setup action.
            if (!preview.ChangesExistingDevice) { StatusMessage = _localizationManager.GetString("Output_VJoyUseSetupCard"); return; }
            bool confirmed = await ConfirmVJoyPreviewAsync(preview, ct);
            if (!confirmed || !ReferenceEquals(profile, CurrentProfile) || _runtime?.IsRunning == true) return;
            var result = await _outputDiagnostics.ApplyVJoyAsync(preview, ct);
            DisplayVJoyInventory(await _outputDiagnostics.InspectVJoyAsync(ct));
            ShowVJoySetupResult(result);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { StatusMessage = _localizationManager.GetString("Output_VJoySelectionIssue") + " " + error.Message; }
        finally { SetBusy(false); }
    }

    private string VJoyLayout(VJoyDeviceInfo info) => string.Format(_localizationManager.GetString("Output_VJoyLayout"),
        info.Axes.Length, info.Buttons, info.DiscretePovs < 0 ? "?" : info.DiscretePovs.ToString(), info.ContinuousPovs < 0 ? "?" : info.ContinuousPovs.ToString(),
        _localizationManager.GetString(info.WheelCapabilities ? "Summary_Enabled" : "Summary_Disabled"));

    private Task<bool> ConfirmVJoyPreviewAsync(VJoySetupPreview preview, CancellationToken ct, string? titleKey = null, string? noticeKey = null)
    {
        string title = _localizationManager.GetString(titleKey ?? (preview.ChangesExistingDevice ? "Output_VJoyChangeExisting" : "Output_VJoyCreate"));
        return _dialogs!.ShowConfirmAsync(title,
            (noticeKey == null ? "" : _localizationManager.GetString(noticeKey) + Environment.NewLine + Environment.NewLine) +
            string.Format(_localizationManager.GetString("Output_VJoyExactPreview"), preview.Before.DeviceId,
                VJoyLayout(preview.Before), VJoyLayout(preview.Target), preview.BackupPath,
                _localizationManager.GetString(preview.ChangesExistingDevice ? "Output_VJoyExistingOrigin" : "Output_VJoyNewOrigin")),
            _localizationManager.GetString("Output_VJoyApplyChanges"), _localizationManager.GetString("Btn_Cancel"), ct: ct);
    }

    private void ShowVJoySetupResult(VJoySetupResult result)
    {
        StatusMessage = result.Success
            ? _localizationManager.GetString(result.RequiresRestart ? "Output_VJoySavedRestart" : "Output_VJoyReadyUnchanged")
            : _localizationManager.GetString(result.ErrorKey ?? "Output_VJoySetupStopped") + " " + result.Detail;
    }

    [RelayCommand]
    public async Task UseExistingVJoyAsync(CancellationToken ct = default)
    {
        if (!ShowVJoySettings || IsBusy || _dialogs == null || SelectedVJoyDevice == null || CurrentProfile == null) return;
        if (_runtime?.IsRunning == true) { StatusMessage = _localizationManager.GetString("Output_WheelStopFirst"); return; }
        var selected = SelectedVJoyDevice.Device;
        bool wheel = VJoyWheelCompatibility;
        var profile = CurrentProfile;
        if (!selected.IsUsable || !selected.MeetsRequirements(wheel))
        { StatusMessage = _localizationManager.GetString("Output_VJoySelectCompatible"); return; }
        try
        {
            SetBusy(true);
            bool confirmed = await _dialogs.ShowConfirmAsync(_localizationManager.GetString("Output_VJoyUseExisting"),
                string.Format(_localizationManager.GetString("Output_VJoyAdoptConfirm"), selected.DeviceId, VJoyDeviceId),
                _localizationManager.GetString("Output_VJoyUseExisting"), _localizationManager.GetString("Btn_Cancel"), ct: ct);
            if (!confirmed || !ReferenceEquals(profile, CurrentProfile) || _runtime?.IsRunning == true || wheel != VJoyWheelCompatibility) return;
            if (!await _outputDiagnostics.SelectVJoyAsync(selected, wheel, ct))
            { StatusMessage = _localizationManager.GetString("Output_VJoySelectionIssue"); return; }
            if (ReferenceEquals(profile, CurrentProfile))
            {
                using var edit = _profileSession.BeginEditGroup(nameof(UseExistingVJoyAsync));
                profile.UseHubVJoyDevice = true;
            }
            DisplayVJoyInventory(await _outputDiagnostics.InspectVJoyAsync(ct));
            StatusMessage = string.Format(_localizationManager.GetString("Output_VJoyAdopted"), selected.DeviceId);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { StatusMessage = _localizationManager.GetString("Output_VJoySelectionIssue") + " " + error.Message; }
        finally { SetBusy(false); }
    }

    private async Task<bool> TrySetupSharedVJoyAsync(CancellationToken ct)
    {
        var profile = CurrentProfile;
        try
        {
            SetBusy(true, _localizationManager.GetString("Output_VJoyInspect"));
            using var setup = _runtime?.BeginOutputSetup();
            var inventory = await _outputDiagnostics.InspectVJoyAsync(ct);
            // Older/preview adapters implement the existing per-device action.
            if (inventory.Devices.Count == 0) return false;
            DisplayVJoyInventory(inventory);
            if (inventory.SelectionError != null) { StatusMessage = VJoySelectionSummary; return true; }
            if (inventory.SelectedDeviceId is { } existing)
            {
                var device = inventory.Devices.Single(item => item.DeviceId == existing);
                StatusMessage = device.IsUsable && device.MeetsRequirements(true)
                    ? _localizationManager.GetString("Output_VJoyReadyUnchanged") : _localizationManager.GetString("Output_VJoySelectCompatible");
                return true;
            }
            if (inventory.SuggestedNewDeviceId is not { } id)
            { StatusMessage = _localizationManager.GetString("Output_VJoyNoUnusedNumbers"); return true; }
            if (_dialogs == null || profile == null) return true;
            var candidate = inventory.Devices.Single(device => device.DeviceId == id);
            var preview = await _outputDiagnostics.PreviewVJoyAsync(candidate, changeExisting: false, ct);
            if (preview != null)
            {
                bool accepted = await ConfirmVJoyPreviewAsync(preview, ct);
                if (!accepted || !ReferenceEquals(profile, CurrentProfile) || _runtime?.IsRunning == true) return true;
                var result = await _outputDiagnostics.ApplyVJoyAsync(preview, ct);
                if (result.Success && ReferenceEquals(profile, CurrentProfile))
                {
                    using var edit = _profileSession.BeginEditGroup(nameof(PrepareWheelAsync));
                    profile.UseHubVJoyDevice = true;
                    profile.VJoyWheelCompatibility = true;
                }
                DisplayVJoyInventory(await _outputDiagnostics.InspectVJoyAsync(ct));
                ShowVJoySetupResult(result);
                return true;
            }
            string retained = string.Join(", ", inventory.Devices.Where(device => !device.IsAbsent).Select(device => device.DeviceId));
            bool confirmed = await _dialogs.ShowConfirmAsync(_localizationManager.GetString("Output_WheelPrepare"),
                string.Format(_localizationManager.GetString("Output_VJoyCreateConfirm"), id, retained.Length == 0 ? "—" : retained),
                _localizationManager.GetString("Output_VJoyCreate"), _localizationManager.GetString("Btn_Cancel"), ct: ct);
            if (!confirmed || !ReferenceEquals(profile, CurrentProfile) || _runtime?.IsRunning == true) return true;
            if (!await _outputDiagnostics.CreateVJoyAsync(inventory.Devices.Single(device => device.DeviceId == id), ct))
            { StatusMessage = _localizationManager.GetString("Output_WheelFailed"); return true; }
            if (!ReferenceEquals(profile, CurrentProfile) || _runtime?.IsRunning == true) return true;
            var updated = await _outputDiagnostics.InspectVJoyAsync(ct);
            var created = updated.Devices.Single(device => device.DeviceId == id);
            if (!await _outputDiagnostics.SelectVJoyAsync(created, true, ct))
            { StatusMessage = _localizationManager.GetString("Output_VJoySelectionIssue"); return true; }
            if (ReferenceEquals(profile, CurrentProfile))
            {
                using var edit = _profileSession.BeginEditGroup(nameof(PrepareWheelAsync));
                profile.UseHubVJoyDevice = true;
                profile.VJoyWheelCompatibility = true;
            }
            DisplayVJoyInventory(await _outputDiagnostics.InspectVJoyAsync(ct));
            StatusMessage = string.Format(_localizationManager.GetString("Output_VJoyAdopted"), id);
            return true;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error)
        { StatusMessage = _localizationManager.GetString("Output_VJoySelectionIssue") + " " + error.Message; return true; }
        finally { SetBusy(false); }
    }
}
