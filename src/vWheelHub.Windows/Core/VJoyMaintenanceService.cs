using System.ComponentModel;
using SimRacingHub.Application.Contracts;

namespace SimRacingHub.Core;

internal sealed class VJoyMaintenanceService
{
    private readonly IVJoyConfigurationStore _devices;
    private readonly VJoyConfigurationJournal _journal;
    private readonly HubVJoyDeviceStore _selection;
    private readonly Func<uint, VJoyDeviceInfo> _capabilities;
    private readonly Func<string, string, CancellationToken, Task<int>> _helper;

    internal VJoyMaintenanceService(IVJoyConfigurationStore? devices = null, VJoyConfigurationJournal? journal = null,
        HubVJoyDeviceStore? selection = null, Func<uint, VJoyDeviceInfo>? capabilities = null,
        Func<string, string, CancellationToken, Task<int>>? helper = null)
    {
        _devices = devices ?? new WindowsVJoyConfigurationStore();
        _journal = journal ?? new VJoyConfigurationJournal();
        _selection = selection ?? new HubVJoyDeviceStore();
        _capabilities = capabilities ?? VJoyInventoryService.NativeCapabilities;
        _helper = helper ?? VJoySetupService.RunElevated;
    }

    internal VJoySetupPreview PreviewRemoval(VJoyDeviceInfo? inspected = null)
    {
        var selected = _selection.Read();
        uint targetId = (uint)(inspected?.DeviceId ?? selected?.DeviceId ?? throw new InvalidOperationException("Select the device to inspect before cleanup."));
        bool shared = targetId == selected?.DeviceId;
        var before = _devices.Read(targetId);
        if (targetId is < 1 or > 16 || before.DeviceId != targetId || selected?.PendingOperationId != null ||
            (inspected != null && inspected.Fingerprint != before.Fingerprint()) ||
            (shared && (selected!.Fingerprint != before.Fingerprint() ||
                selected.CreatedByHub && selected.CreationOperationId != VJoyWheelPreset.Token(before, VJoyWheelPreset.OwnerValue))) ||
            !VJoyDeviceOwnership.Verify(before, _journal, _devices))
            throw new InvalidOperationException("Only an unchanged device with verified hub creation can be removed. Adopted or externally modified devices are protected.");
        var capabilities = _capabilities(before.DeviceId) with { Fingerprint = before.Fingerprint(), ConfigurationExists = true };
        if (capabilities.Status is not (VJoyWrapper.VJD_STAT_FREE or VJoyWrapper.VJD_STAT_MISS))
            throw new InvalidOperationException("Release the device before removing its configuration.");
        var expected = before with { Device = null, ForceFeedbackDevice = null };
        var record = _journal.Prepare(before, expected, purpose: "wheel-remove", beforeDeviceStatus: capabilities.Status,
            previousSelectionFingerprint: selected?.Fingerprint, previousSelection: shared ? null : selected);
        var target = new VJoyDeviceInfo((int)targetId, VJoyWrapper.VJD_STAT_MISS, false, [], 0, 0, 0, false, expected.Fingerprint());
        return new(record.Id, capabilities, target, true, true, _journal.BackupPath(record.Id), expected.Fingerprint());
    }

    internal VJoySetupPreview PreviewMissingRestore()
    {
        var selected = _selection.Read() ?? throw new InvalidOperationException("No shared device number is saved.");
        if (!selected.CreatedByHub || selected.PendingOperationId != null)
            throw new InvalidOperationException("Only a missing hub-created device with verified history can be restored. Existing adopted devices are protected.");
        var before = _devices.Read((uint)selected.DeviceId);
        if (before.Device != null || before.ForceFeedbackDevice != null || _devices.GetStatus(before.DeviceId) != VJoyWrapper.VJD_STAT_MISS)
            throw new InvalidOperationException("The saved device is not absent. Nothing will be replaced.");
        var historical = _journal.FindExpected(before.DeviceId, selected.Fingerprint);
        if (historical.Driver != before.Driver || selected.CreationOperationId != VJoyWheelPreset.Token(historical, VJoyWheelPreset.OwnerValue) ||
            !VJoyDeviceOwnership.Verify(historical, _journal, _devices)) throw new IOException("Device history or driver identity changed. Nothing restored.");
        byte[]? descriptor = historical.Device?.Values.SingleOrDefault(value => value.Name.Equals("HidReportDesctiptor", StringComparison.OrdinalIgnoreCase))?.Data;
        int povs = Enumerable.Range(1, 4).Single(value => descriptor != null && VJoyWheelPreset.Descriptor(selected.DeviceId, value).SequenceEqual(descriptor));
        string id = Guid.NewGuid().ToString("N");
        var expected = VJoyWheelPreset.Build(before, povs, id, creation: true);
        _devices.ValidateProposed(expected);
        _journal.Prepare(before, expected, operationId: id, purpose: "wheel-recreate", allowCreatedRollback: true,
            beforeDeviceStatus: VJoyWrapper.VJD_STAT_MISS, previousSelectionFingerprint: selected.Fingerprint, previousSelection: selected);
        var empty = new VJoyDeviceInfo(selected.DeviceId, VJoyWrapper.VJD_STAT_MISS, false, [], 0, 0, 0, false, before.Fingerprint());
        var target = new VJoyDeviceInfo(selected.DeviceId, VJoyWrapper.VJD_STAT_FREE, true, Enumerable.Range(0x30, 8).ToArray(), 128, povs, 0, true, expected.Fingerprint());
        return new(id, empty, target, true, false, _journal.BackupPath(id), expected.Fingerprint());
    }

    internal VJoyPendingSetup? InspectPending()
    {
        var selected = _selection.Read();
        if (selected?.PendingOperationId is not { } id) return null;
        var record = _journal.Read(id);
        bool otherRemoval = selected.PendingDeviceId is { } target && target != selected.DeviceId;
        if (record.Purpose is not ("wheel-create" or "wheel-recreate" or "wheel-reconfigure" or "wheel-remove") || record.ApprovedAt == null || record.Expected == null ||
            record.DeviceId != (selected.PendingDeviceId ?? selected.DeviceId) ||
            (otherRemoval ? record.Purpose != "wheel-remove" || record.PreviousSelection != HubVJoyDeviceStore.ClearPending(selected) :
                (record.Purpose is "wheel-create" or "wheel-recreate" ? record.Expected.Fingerprint() : record.BeforeHash) != selected.Fingerprint) ||
            !_journal.MatchesPlan(record, _devices))
            throw new InvalidDataException("The reserved output does not match its approved creation record. Keep the backup and reservation.");
        var current = _devices.Read(record.DeviceId);
        bool original = current.Fingerprint() == record.BeforeHash;
        bool receipt = _devices.HasWriteReceipt(id, record.BeforeHash, record.Expected.Fingerprint());
        bool restored = _devices.HasWriteReceipt(id, record.Expected.Fingerprint(), record.BeforeHash, restoring: true);
        bool committed = receipt && !restored && current.Fingerprint() == record.Expected.Fingerprint() && !record.RecoveryClosed;
        bool prepared = original && !receipt && !record.RecoveryClosed && record.Status == VJoyChangeStatus.Prepared &&
            DateTimeOffset.UtcNow - record.CreatedAt <= TimeSpan.FromMinutes(30) && _devices.GetStatus(record.DeviceId) == record.BeforeDeviceStatus;
        return new(id, (int)record.DeviceId, current.Fingerprint(), _journal.BackupPath(id), committed || prepared,
            original || committed, committed && record.Purpose is "wheel-create" or "wheel-recreate", committed, record.Purpose);
    }

    internal async Task<VJoySetupResult> ResolvePendingAsync(VJoyPendingSetup preview, bool resume, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var current = InspectPending();
        if (current != preview || !(resume ? current.CanResume : current.CanCancel))
            throw new IOException("Pending setup changed or cannot be resolved safely. Inspect it again; its number is retained.");
        try
        {
            var pendingRecord = _journal.Read(current.OperationId);
            var pendingLive = _capabilities(pendingRecord.DeviceId);
            bool alreadyActive = pendingRecord.Purpose == "wheel-remove" ? pendingLive.Status == VJoyWrapper.VJD_STAT_MISS
                : pendingLive.Status is 0 or 1 && pendingLive.MeetsRequirements(true);
            if (resume && current.NeedsRegistryRestore && alreadyActive)
            {
                // InspectPending verified a durable committed result. Acknowledge
                // it in-process; no second elevation or registry write is needed.
                var committed = _journal.ApplyPrepared(current.OperationId, _devices);
                if (committed.Status != VJoyChangeStatus.Applied || !committed.ConfirmedWrite)
                    return new(false, false, committed.Id, "Output_VJoySetupStopped", committed.Detail);
                _selection.AcceptApplied(committed, _journal, _devices);
                return VJoySetupService.Completion(committed, pendingLive);
            }
            if (!resume && !current.NeedsRegistryRestore)
            {
                var closed = _journal.CloseAtOriginal(current.OperationId, _devices);
                _selection.CancelReservation(current.OperationId, closed);
                return new(true, closed.ActivationRequiresRestart, current.OperationId);
            }
            int code = await _helper(current.OperationId, resume ? "--apply-vjoy" : "--restore-vjoy", ct).ConfigureAwait(false);
            var record = _journal.Read(current.OperationId);
            if (resume && code == 0 && record.Status == VJoyChangeStatus.Applied)
            {
                _selection.AcceptApplied(record, _journal, _devices);
                return VJoySetupService.Completion(record, _capabilities(record.DeviceId));
            }
            if (!resume && code == 0 && record.Status is VJoyChangeStatus.Restored or VJoyChangeStatus.NoChange &&
                _devices.Read(record.DeviceId).Fingerprint() == record.BeforeHash)
            {
                _selection.CancelReservation(record.Id, record);
                return record.ActivationError != null && !record.ActivationRequiresRestart
                    ? new(false, false, record.Id, "Output_VJoyActivationPending", record.ActivationError)
                    : new(true, record.ActivationRequiresRestart, record.Id);
            }
            return new(false, false, record.Id, "Output_VJoySetupStopped", $"Helper exit {code}. {record.HelperError} {record.Detail}");
        }
        catch (Win32Exception error) when (error.NativeErrorCode == 1223)
        { return new(false, false, current.OperationId, VJoySetupService.ElevationErrorKey(error), error.Message); }
    }
}
