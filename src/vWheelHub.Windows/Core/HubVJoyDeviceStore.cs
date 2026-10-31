using SimRacingHub.Models;

namespace SimRacingHub.Core;

// Selection is explicit and installation-wide. It is not proof of ownership:
// adopting an existing device never authorizes changing or removing it.
internal sealed record HubVJoySelection(int DeviceId, string Fingerprint, bool CreatedByHub = false, string? CreationOperationId = null,
    bool RequiresRestart = false, string? PendingOperationId = null, string? PendingPurpose = null, int? PendingDeviceId = null);

internal sealed class HubVJoyDeviceStore
{
    private readonly string _path;
    internal HubVJoyDeviceStore(string? path = null) =>
        _path = Path.GetFullPath(path ?? Path.Combine(StoragePaths.LocalDataDirectory, "vjoy-output.json"));

    internal HubVJoySelection? Read()
    {
        if (!File.Exists(_path)) return null;
        var selected = VJoyRecoveryFiles.Read<HubVJoySelection>(_path);
        if (selected.DeviceId is < 1 or > 16 || !VJoyRecoveryFiles.IsHash(selected.Fingerprint) ||
            (selected.PendingDeviceId is { } target && (target is < 1 or > 16 || target == selected.DeviceId ||
                selected.PendingOperationId == null || selected.PendingPurpose != "wheel-remove")) ||
            (selected.CreatedByHub && !Guid.TryParseExact(selected.CreationOperationId, "N", out _)) ||
            (selected.PendingOperationId != null && (!Guid.TryParseExact(selected.PendingOperationId, "N", out _) ||
                selected.PendingPurpose is not (null or "wheel-create" or "wheel-recreate" or "wheel-reconfigure" or "wheel-remove") ||
                selected.CreatedByHub && selected.PendingPurpose is not ("wheel-reconfigure" or "wheel-remove"))))
            throw new InvalidDataException("Invalid shared vJoy output selection. Re-select the device explicitly.");
        return selected;
    }

    internal void ReserveCreation(VJoyChangeRecord record)
    {
        if (record.Purpose is "wheel-reconfigure" or "wheel-remove")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            using var setupGuard = new FileStream(_path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            var saved = Read();
            if (record.Purpose == "wheel-remove" && saved?.DeviceId != record.DeviceId)
            {
                if (saved == null && record.ApprovedAt != null && record.PreviousSelection == null && record.PreviousSelectionFingerprint == null)
                {
                    VJoyRecoveryFiles.Write(_path, new HubVJoySelection((int)record.DeviceId, record.BeforeHash,
                        PendingOperationId: record.Id, PendingPurpose: record.Purpose), replace: false);
                    return;
                }
                if (saved == null || record.ApprovedAt == null || record.PreviousSelection == null ||
                    ClearPending(saved) != record.PreviousSelection ||
                    saved.PendingOperationId != null && saved.PendingOperationId != record.Id)
                    throw new IOException("The shared output changed before cleanup. Its selection was not overwritten.");
                VJoyRecoveryFiles.Write(_path, saved with { PendingOperationId = record.Id, PendingPurpose = record.Purpose,
                    PendingDeviceId = (int)record.DeviceId }, replace: true);
                return;
            }
            if (saved?.DeviceId != record.DeviceId) return; // Explicit advanced edit of another existing device.
            if (record.ApprovedAt == null || saved.Fingerprint != record.BeforeHash || saved.PendingOperationId != null && saved.PendingOperationId != record.Id)
                throw new IOException("The selected output changed before setup. Its selection was not overwritten.");
            VJoyRecoveryFiles.Write(_path, saved with { PendingOperationId = record.Id, PendingPurpose = record.Purpose }, replace: true);
            return;
        }
        if (record.Purpose is not ("wheel-create" or "wheel-recreate")) return;
        if (record.ApprovedAt == null || record.Expected == null || record.BeforeDeviceStatus != VJoyWrapper.VJD_STAT_MISS)
            throw new InvalidOperationException("No approved creation to reserve.");
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        using var guard = new FileStream(_path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var previous = Read();
        if (previous != null)
        {
            if (previous.PendingOperationId != null && previous.PendingOperationId != record.Id)
                throw new IOException("Another saved operation still owns this output reservation.");
            if (previous.DeviceId != record.DeviceId || previous.PendingOperationId != record.Id &&
                previous.Fingerprint != record.Expected.Fingerprint() && previous.Fingerprint != record.PreviousSelectionFingerprint)
                throw new IOException("The shared output selection changed. No replacement number will be selected.");
            if (record.Purpose == "wheel-recreate" && previous.PendingOperationId != record.Id)
                VJoyRecoveryFiles.Write(_path, new HubVJoySelection((int)record.DeviceId, record.Expected.Fingerprint(),
                    RequiresRestart: true, PendingOperationId: record.Id, PendingPurpose: record.Purpose), replace: true);
            return;
        }
        // Persist the explicitly chosen number before an elevated helper can
        // commit. A parent crash must not cause first setup to choose another ID.
        VJoyRecoveryFiles.Write(_path, new HubVJoySelection((int)record.DeviceId, record.Expected.Fingerprint(),
            RequiresRestart: true, PendingOperationId: record.Id, PendingPurpose: record.Purpose), replace: false);
    }

    internal void CancelReservation(string operationId, VJoyChangeRecord? record = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        using var guard = new FileStream(_path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var current = Read();
        if (current?.PendingOperationId != operationId) return;
        if (current.PendingPurpose == "wheel-remove" && (record?.Id != operationId || record.Purpose != "wheel-remove" ||
            record.DeviceId != (current.PendingDeviceId ?? current.DeviceId)))
            throw new IOException("The approved cleanup record is required to release its reservation.");
        if (current.PendingPurpose == "wheel-recreate")
        {
            if (record?.Id != operationId || record.PreviousSelection is not { } previous || previous.DeviceId != current.DeviceId ||
                previous.Fingerprint != record.PreviousSelectionFingerprint) throw new IOException("Keep the reserved output until its previous selection can be verified.");
            VJoyRecoveryFiles.Write(_path, previous, replace: true);
        }
        else if (current.PendingPurpose == "wheel-remove" && record?.Purpose == "wheel-remove" &&
            record.PreviousSelection == null && record.PreviousSelectionFingerprint == null)
            File.Delete(_path); // Cleanup reserved an orphan; there was no shared choice to restore.
        else if (current.PendingPurpose is "wheel-reconfigure" or "wheel-remove")
            VJoyRecoveryFiles.Write(_path, ClearPending(current), replace: true);
        else if (!current.CreatedByHub) File.Delete(_path);
    }

    internal void SelectExisting(VJoyConfigurationSnapshot inspected, IVJoyConfigurationStore devices, bool onlyIfUnselected = false)
    {
        inspected.Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        using var guard = new FileStream(_path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        if (onlyIfUnselected && Read() != null) return;
        var current = devices.Read(inspected.DeviceId);
        if (current.Fingerprint() != inspected.Fingerprint() || devices.GetStatus(inspected.DeviceId) is not (VJoyWrapper.VJD_STAT_FREE or VJoyWrapper.VJD_STAT_OWN))
            throw new IOException("The selected vJoy device changed or is occupied. Check devices again.");
        var previous = Read();
        if (previous?.PendingOperationId != null)
            throw new InvalidOperationException("Resume or cancel the pending vJoy setup before selecting an output device.");
        var selected = previous?.DeviceId == inspected.DeviceId && previous.Fingerprint == inspected.Fingerprint()
            ? previous with { RequiresRestart = false } : new HubVJoySelection((int)inspected.DeviceId, inspected.Fingerprint());
        VJoyRecoveryFiles.Write(_path, selected, replace: true);
    }

    internal void AcceptApplied(VJoyChangeRecord record, VJoyConfigurationJournal journal, IVJoyConfigurationStore devices)
    {
        var before = journal.Original(record.Id);
        if (!record.ConfirmedWrite || record.Status != VJoyChangeStatus.Applied || record.Expected == null ||
            record.ApprovedAt == null || record.Purpose is not ("wheel-create" or "wheel-recreate" or "wheel-reconfigure" or "wheel-remove") ||
            !devices.HasWriteReceipt(record.Id, record.BeforeHash, record.Expected.Fingerprint()) ||
            devices.Read(record.DeviceId).Fingerprint() != record.Expected.Fingerprint())
            throw new InvalidOperationException("Cannot select an unverified vJoy setup result.");
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        using var guard = new FileStream(_path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var previous = Read();
        if (record.Purpose == "wheel-remove")
        {
            if (previous == null) return;
            if (previous.DeviceId != record.DeviceId)
            {
                if (record.PreviousSelection == null || ClearPending(previous) != record.PreviousSelection ||
                    previous.PendingOperationId != null && (previous.PendingOperationId != record.Id || previous.PendingDeviceId != record.DeviceId))
                    throw new IOException("The shared output changed during cleanup. Its selection was preserved.");
                if (previous.PendingOperationId == record.Id) VJoyRecoveryFiles.Write(_path, ClearPending(previous), replace: true);
                return;
            }
            if (previous.DeviceId != record.DeviceId || previous.Fingerprint != record.BeforeHash ||
                previous.CreatedByHub && previous.CreationOperationId != VJoyWheelPreset.Token(before, VJoyWheelPreset.OwnerValue) ||
                !VJoyDeviceOwnership.Verify(before, journal, devices))
                throw new IOException("The shared output selection changed. It was not cleared.");
            File.Delete(_path);
        }
        else if (record.Purpose is "wheel-create" or "wheel-recreate")
        {
            if (previous != null && (previous.DeviceId != record.DeviceId || previous.PendingOperationId != record.Id &&
                previous.Fingerprint != record.Expected.Fingerprint() && previous.Fingerprint != record.PreviousSelectionFingerprint))
                throw new IOException("The shared output selection changed. It was not overwritten.");
            if (before.Device != null || before.ForceFeedbackDevice != null || record.BeforeDeviceStatus != VJoyWrapper.VJD_STAT_MISS ||
                VJoyWheelPreset.Token(record.Expected, VJoyWheelPreset.OwnerValue) != record.Id)
                throw new InvalidOperationException("This operation does not prove creation of an absent device.");
            VJoyRecoveryFiles.Write(_path, new HubVJoySelection((int)record.DeviceId, record.Expected.Fingerprint(), true, record.Id, true), replace: true);
        }
        else if (previous?.DeviceId == record.DeviceId)
        {
            if (previous.Fingerprint == record.Expected.Fingerprint()) return; // Idempotent child/parent acknowledgement.
            if (previous.Fingerprint != record.BeforeHash && previous.Fingerprint != record.Expected.Fingerprint() &&
                previous.Fingerprint != record.PreviousSelectionFingerprint)
                throw new IOException("The shared output selection changed. It was not overwritten.");
            VJoyRecoveryFiles.Write(_path, previous with { Fingerprint = record.Expected.Fingerprint(), RequiresRestart = true,
                CreatedByHub = previous.CreatedByHub && previous.Fingerprint == record.BeforeHash,
                CreationOperationId = previous.Fingerprint == record.BeforeHash ? previous.CreationOperationId : null,
                PendingOperationId = null, PendingPurpose = null, PendingDeviceId = null }, replace: true);
        }
    }

    internal static HubVJoySelection ClearPending(HubVJoySelection value) => value with
        { PendingOperationId = null, PendingPurpose = null, PendingDeviceId = null };

    internal int Resolve(ResolvedProfile profile) => !profile.UseHubVJoyDevice ? profile.VJoyDeviceId
        : Read()?.DeviceId ?? throw new InvalidOperationException("Select the shared vJoy device before starting hub controls.");

    internal string? ValidateForAcquire(int id, IVJoyConfigurationStore devices)
    {
        try
        {
            var selected = Read();
            if (selected?.DeviceId != id) return null;
            if (selected.PendingOperationId != null)
                return "An approved vJoy operation has not completed. Resume or cancel it before using the shared output; no replacement number will be used.";
            var current = devices.Read((uint)id);
            if (current.Fingerprint() != selected.Fingerprint)
                return "The shared vJoy device configuration or driver has changed. Check it and explicitly re-select it; no replacement device will be used.";
            if (selected.RequiresRestart && !VJoyInventoryService.NativeCapabilities((uint)id).MeetsRequirements(true))
                return "vJoy configuration was saved. Restart the driver or Windows, then check the same device; no replacement device will be used.";
            return null;
        }
        catch (Exception error) { return "Cannot verify the selected shared vJoy device: " + error.Message; }
    }
}
