namespace SimRacingHub.Core;

internal enum VJoyChangeStatus
{
    BackupOnly, Prepared, Applied, NoChange, Unverified, Failed,
    Restoring, Restored, Conflict, RestoreFailed
}

internal sealed record VJoyChangeRecord(
    string Id, DateTimeOffset CreatedAt, uint DeviceId, string BeforeHash,
    VJoyChangeStatus Status, VJoyConfigurationSnapshot? Expected = null,
    VJoyConfigurationSnapshot? Observed = null, string? Detail = null, bool ConfirmedWrite = false, bool RecoveryClosed = false,
    string? Purpose = null, DateTimeOffset? ApprovedAt = null, bool AllowCreatedRollback = false, int? BeforeDeviceStatus = null,
    string? PreviousSelectionFingerprint = null, HubVJoySelection? PreviousSelection = null,
    bool ActivationRequiresRestart = false, string? ActivationError = null, string? HelperError = null);

/// <summary>
/// Per-operation, verified write-ahead records. A snapshot observed after an
/// opaque external tool is never treated as proof that the hub wrote it.
/// </summary>
internal sealed class VJoyConfigurationJournal
{
    internal string DirectoryPath { get; }
    internal VJoyConfigurationJournal(string? directory = null) =>
        DirectoryPath = Path.GetFullPath(directory ?? Path.Combine(StoragePaths.LocalDataDirectory, "vjoy-recovery"));

    private string PathFor(string id, string suffix)
    {
        if (!Guid.TryParseExact(id, "N", out _)) throw new InvalidDataException("Invalid vJoy recovery operation ID.");
        return Path.Combine(DirectoryPath, id + suffix);
    }

    private FileStream Lock(string id)
    {
        Directory.CreateDirectory(DirectoryPath);
        return new FileStream(PathFor(id, ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    internal string BackupPath(string id) => PathFor(id, ".before.json");

    internal VJoyChangeRecord Prepare(VJoyConfigurationSnapshot before, VJoyConfigurationSnapshot? expected = null, bool backupOnly = false,
        string? operationId = null, string? purpose = null, bool allowCreatedRollback = false, int? beforeDeviceStatus = null,
        string? previousSelectionFingerprint = null, HubVJoySelection? previousSelection = null)
    {
        before.Validate();
        expected?.Validate();
        if (expected != null && (expected.DeviceId != before.DeviceId || expected.Driver != before.Driver))
            throw new InvalidDataException("vJoy change cannot switch device or driver.");
        string id = operationId ?? Guid.NewGuid().ToString("N");
        using var guard = Lock(id);
        if (File.Exists(PathFor(id, ".json"))) throw new IOException("vJoy operation ID is already in use.");
        // The immutable original must be durable and read back before an intent
        // can be saved, much less before any driver change is attempted.
        VJoyRecoveryFiles.Write(BackupPath(id), before);
        var verified = VJoyRecoveryFiles.Read<VJoyConfigurationSnapshot>(BackupPath(id));
        if (verified.Fingerprint() != before.Fingerprint()) throw new IOException("vJoy backup verification failed.");
        var record = new VJoyChangeRecord(id, DateTimeOffset.UtcNow, before.DeviceId,
            before.Fingerprint(), backupOnly ? VJoyChangeStatus.BackupOnly : VJoyChangeStatus.Prepared, expected,
            Purpose: purpose, AllowCreatedRollback: allowCreatedRollback, BeforeDeviceStatus: beforeDeviceStatus,
            PreviousSelectionFingerprint: previousSelectionFingerprint, PreviousSelection: previousSelection);
        Save(record);
        return Read(id);
    }

    internal VJoyChangeRecord Read(string id)
    {
        var record = VJoyRecoveryFiles.Read<VJoyChangeRecord>(PathFor(id, ".json"));
        var before = VJoyRecoveryFiles.Read<VJoyConfigurationSnapshot>(BackupPath(id));
        before.Validate();
        record.Expected?.Validate();
        record.Observed?.Validate();
        if (record.Id != id || record.DeviceId != before.DeviceId || record.BeforeHash != before.Fingerprint() ||
            !Enum.IsDefined(record.Status) ||
            (record.ConfirmedWrite && record.Expected == null) ||
            (record.Status is VJoyChangeStatus.Applied or VJoyChangeStatus.Restoring or VJoyChangeStatus.Restored && !record.ConfirmedWrite) ||
            (record.Expected != null && (record.Expected.DeviceId != before.DeviceId || record.Expected.Driver != before.Driver)) ||
            (record.Observed != null && record.Observed.DeviceId != before.DeviceId))
            throw new InvalidDataException("vJoy recovery record does not match its original backup.");
        return record;
    }

    private void Save(VJoyChangeRecord record) => VJoyRecoveryFiles.Write(PathFor(record.Id, ".json"), record, replace: true);

    internal VJoyConfigurationSnapshot Original(string id)
    {
        _ = Read(id);
        return VJoyRecoveryFiles.Read<VJoyConfigurationSnapshot>(BackupPath(id));
    }

    internal bool MatchesPlan(VJoyChangeRecord record, IVJoyConfigurationStore store) =>
        VJoyWheelPreset.MatchesPlan(record, Original(record.Id)) ||
        VJoyDeviceOwnership.MatchesRecreation(record, Original(record.Id), this, store) ||
        VJoyDeviceOwnership.MatchesRemoval(record, Original(record.Id), this, store);

    internal VJoyConfigurationSnapshot FindExpected(uint id, string fingerprint)
    {
        VJoyConfigurationSnapshot? found = null;
        foreach (string path in Directory.EnumerateFiles(DirectoryPath, "*.json").Take(10000))
        {
            string operation = Path.GetFileNameWithoutExtension(path);
            if (!Guid.TryParseExact(operation, "N", out _)) continue;
            var record = Read(operation);
            if (record.DeviceId != id || record.Expected?.Fingerprint() != fingerprint) continue;
            if (found != null) throw new InvalidDataException("Ambiguous device history.");
            found = record.Expected;
        }
        return found ?? throw new InvalidDataException("The selected device's verified history is missing.");
    }

    internal VJoyChangeRecord CloseAtOriginal(string id, IVJoyConfigurationStore store)
    {
        using var writes = new FileStream(Path.Combine(DirectoryPath, "write.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        using var guard = Lock(id);
        var record = Read(id);
        if (record.Purpose is not ("wheel-create" or "wheel-recreate" or "wheel-reconfigure" or "wheel-remove") || record.ApprovedAt == null || store.Read(record.DeviceId).Fingerprint() != record.BeforeHash)
            throw new IOException("Original configuration could not be verified. Reservation retained.");
        bool committed = record.ConfirmedWrite || record.Expected != null &&
            store.HasWriteReceipt(id, record.BeforeHash, record.Expected.Fingerprint());
        record = record with { Status = VJoyChangeStatus.NoChange, RecoveryClosed = true, ConfirmedWrite = committed,
            Detail = "Explicit cancellation: original configuration verified; no additional device write." };
        Save(record);
        return record;
    }

    internal VJoyChangeRecord RecoverExclusive(string id, IVJoyConfigurationStore store)
    {
        using var writes = new FileStream(Path.Combine(DirectoryPath, "write.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        return Recover(id, store);
    }

    internal VJoyChangeRecord Approve(string id, string expectedFingerprint)
    {
        using var guard = Lock(id);
        var record = Read(id);
        if (record.Status != VJoyChangeStatus.Prepared || record.ApprovedAt != null || record.Expected?.Fingerprint() != expectedFingerprint ||
            DateTimeOffset.UtcNow - record.CreatedAt > TimeSpan.FromMinutes(30))
            throw new InvalidOperationException("This vJoy preview is stale or has already been used. Preview again.");
        record = record with { ApprovedAt = DateTimeOffset.UtcNow };
        Save(record);
        return record;
    }

    internal VJoyChangeRecord ApplyPrepared(string id, IVJoyConfigurationStore store)
    {
        using var writeLock = new FileStream(Path.Combine(DirectoryPath, "write.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var record = Read(id);
        var original = Original(id);
        if (!record.RecoveryClosed && record.Status is not (VJoyChangeStatus.Restored or VJoyChangeStatus.NoChange) &&
            record.ApprovedAt != null && record.Expected != null && MatchesPlan(record, store) &&
            store.HasWriteReceipt(id, original.Fingerprint(), record.Expected.Fingerprint()) &&
            !store.HasWriteReceipt(id, record.Expected.Fingerprint(), original.Fingerprint(), restoring: true))
            return store.Read(record.DeviceId).Fingerprint() == record.Expected.Fingerprint()
                ? Mark(id, VJoyChangeStatus.Applied, "Verified durable commit receipt; no setup replayed.", confirmedWrite: true)
                : Mark(id, VJoyChangeStatus.Conflict, "A completed setup was changed afterwards. Preview again; no setup replayed.");
        if (record.Status != VJoyChangeStatus.Prepared || record.ApprovedAt == null || record.CreatedAt > DateTimeOffset.UtcNow.AddSeconds(5) ||
            record.ApprovedAt < record.CreatedAt || DateTimeOffset.UtcNow - record.CreatedAt > TimeSpan.FromMinutes(30) ||
            !MatchesPlan(record, store))
            throw new InvalidOperationException("No current, approved vJoy preset preview is available.");
        try
        {
            if (store.GetStatus(record.DeviceId) != record.BeforeDeviceStatus)
                return Mark(id, VJoyChangeStatus.Conflict, "Device occupancy or presence changed after preview. Preview again.");
            if (!store.TryReplace(original, record.Expected!, id))
                return Mark(id, VJoyChangeStatus.Conflict, "Device changed or is occupied. No setup performed.");
            record = Mark(id, VJoyChangeStatus.Applied, "Previewed preset committed atomically; live activation has not yet been checked.", confirmedWrite: true);
            var actual = store.Read(record.DeviceId);
            return actual.Fingerprint() == record.Expected!.Fingerprint() ? record
                : Mark(id, VJoyChangeStatus.Conflict, "Device changed after setup. No further changes performed.");
        }
        catch (Exception error)
        {
            var recovered = Recover(id, store);
            return Mark(id, recovered.Status, error.Message + " " + recovered.Detail);
        }
    }

    internal VJoyChangeRecord RecordInterrupted(string id, string detail) =>
        Mark(id, VJoyChangeStatus.Unverified, detail);

    internal void RecordHelperError(string id, string detail)
    {
        // An invalid/missing request must not create even a journal directory.
        _ = Read(id);
        using var guard = Lock(id);
        var record = Read(id);
        Save(record with { HelperError = detail.Length > 16000 ? detail[..16000] : detail });
    }

    internal VJoyChangeRecord Observe(string id, VJoyConfigurationSnapshot actual, string? detail = null)
    {
        using var guard = Lock(id);
        var record = Read(id);
        actual.Validate();
        if (record.Status != VJoyChangeStatus.Prepared || actual.DeviceId != record.DeviceId)
            throw new InvalidOperationException("This vJoy operation cannot accept another result.");
        var status = actual.Fingerprint() == record.BeforeHash ? VJoyChangeStatus.NoChange : VJoyChangeStatus.Unverified;
        record = record with { Status = status, Observed = actual, Detail = detail };
        Save(record);
        return record;
    }

    internal VJoyChangeRecord Apply(IVJoyConfigurationStore store, VJoyConfigurationSnapshot desired)
    {
        desired.Validate();
        var record = Prepare(store.Read(desired.DeviceId), desired);
        desired = record.Expected!; // Use the immutable-on-disk, verified plan.
        try
        {
            var original = VJoyRecoveryFiles.Read<VJoyConfigurationSnapshot>(BackupPath(record.Id));
            // Existing devices stay blocked at the UI. This path is for a future
            // explicit preview with a fully specified descriptor, not CLI output.
            if (!store.TryReplace(original, desired))
                return Mark(record.Id, VJoyChangeStatus.Conflict, "Device changed or is occupied; no replacement performed.");
            // TryReplace is an atomic, guarded store operation. Its successful
            // receipt, unlike a CLI exit code, proves a completed hub write.
            record = Mark(record.Id, VJoyChangeStatus.Applied, "Atomic hub write completed.", confirmedWrite: true);
            var actual = store.Read(desired.DeviceId);
            return actual.Fingerprint() == desired.Fingerprint() ? record
                : Mark(record.Id, VJoyChangeStatus.Conflict, "Configuration changed after the completed hub write.");
        }
        catch (Exception error)
        {
            // Recovery may only use the predeclared expected result. A partial
            // or externally changed state must never be claimed as hub-owned.
            var recovered = Recover(record.Id, store);
            if (recovered.Status == VJoyChangeStatus.Restored) return recovered;
            return Mark(record.Id, recovered.Status, error.Message + " " + recovered.Detail);
        }
    }

    internal VJoyChangeRecord Activate(string id, IVJoyConfigurationStore store, bool restoring = false)
    {
        using var writes = new FileStream(Path.Combine(DirectoryPath, "write.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var record = Read(id);
        var desired = restoring ? Original(id) : record.Expected;
        if (record.ApprovedAt == null || record.Expected == null || desired == null || !record.ConfirmedWrite ||
            (!restoring && (record.RecoveryClosed || record.Status != VJoyChangeStatus.Applied || !MatchesPlan(record, store))) ||
            (restoring && record.Status is not (VJoyChangeStatus.Restored or VJoyChangeStatus.NoChange)) ||
            !store.HasWriteReceipt(id, restoring ? record.Expected.Fingerprint() : record.BeforeHash,
                restoring ? record.BeforeHash : record.Expected.Fingerprint(), restoring) ||
            store.Read(record.DeviceId).Fingerprint() != desired.Fingerprint())
            throw new IOException("No unchanged, verified commit is available for live activation.");
        // Activation failure must never roll back a valid durable commit.
        VJoyActivationResult activation;
        try { activation = store.Refresh(desired, restoring); }
        catch (Exception error) { activation = new(false, Error: error.Message); }
        record = record with { ActivationRequiresRestart = activation.RequiresRestart,
            ActivationError = activation.Ready ? null : activation.Error ?? "The saved vJoy layout is awaiting live activation." };
        Save(record);
        return record;
    }

    private VJoyChangeRecord Mark(string id, VJoyChangeStatus status, string detail, bool confirmedWrite = false)
    {
        using var guard = Lock(id);
        var original = Read(id);
        var record = original with { Status = status, Detail = detail, ConfirmedWrite = original.ConfirmedWrite || confirmedWrite };
        Save(record);
        return record;
    }

    internal VJoyChangeRecord Recover(string id, IVJoyConfigurationStore store)
    {
        using var guard = Lock(id);
        var record = Read(id);
        if (record.Status is VJoyChangeStatus.BackupOnly or VJoyChangeStatus.Unverified)
            return record;
        var original = VJoyRecoveryFiles.Read<VJoyConfigurationSnapshot>(BackupPath(id));
        try
        {
            var current = store.Read(record.DeviceId);
            // The operation marker is written in the same registry transaction
            // as the exact approved descriptor. It is a durable commit receipt
            // if the process died between commit and updating its disk journal.
            bool supportedPlan = record.ApprovedAt != null && record.Expected != null && record.Purpose is "wheel-create" or "wheel-recreate" or "wheel-reconfigure" or "wheel-remove";
            bool applyReceipt = supportedPlan && store.HasWriteReceipt(id, original.Fingerprint(), record.Expected!.Fingerprint());
            if (supportedPlan && !applyReceipt && current.Fingerprint() != original.Fingerprint())
            {
                record = record with { Status = record.Status == VJoyChangeStatus.Conflict ? VJoyChangeStatus.Conflict : VJoyChangeStatus.Unverified,
                    ConfirmedWrite = false, Observed = current,
                    Detail = "No protected registry commit receipt. Backup retained; nothing overwritten." };
                Save(record);
                return record;
            }
            if (supportedPlan && store.HasWriteReceipt(id, record.Expected!.Fingerprint(), original.Fingerprint(), restoring: true))
            {
                record = record with { ConfirmedWrite = true, RecoveryClosed = true, Status = VJoyChangeStatus.Restored };
                Save(record);
            }
            if (!record.ConfirmedWrite && applyReceipt)
            {
                record = record with { ConfirmedWrite = true };
                Save(record);
            }
            if (current.Fingerprint() == original.Fingerprint())
            {
                record = record with
                {
                    Status = record.Status == VJoyChangeStatus.Restored ? VJoyChangeStatus.Restored : VJoyChangeStatus.NoChange,
                    Observed = current, RecoveryClosed = record.RecoveryClosed || record.ConfirmedWrite,
                    Detail = "Original state verified; no restoration needed."
                };
            }
            else if (record.RecoveryClosed || record.Status is VJoyChangeStatus.Restored or VJoyChangeStatus.NoChange)
            {
                record = record with { Status = VJoyChangeStatus.Conflict, Observed = current, RecoveryClosed = true,
                    Detail = "This operation was already completed or reverted. Its record cannot authorize replacing a later configuration." };
            }
            else if (!record.ConfirmedWrite)
            {
                record = record with { Status = VJoyChangeStatus.Unverified, Observed = current, Detail = "An intent is not proof of a completed hub write. Backup retained; nothing overwritten." };
            }
            else if (record.Expected == null || current.Fingerprint() != record.Expected.Fingerprint())
            {
                record = record with { Status = VJoyChangeStatus.Conflict, Observed = current, Detail = "Current state differs from the hub result. Backup retained; nothing overwritten." };
            }
            else
            {
                // Do not remove a newly created device as implicit rollback.
                // Cleanup of created devices needs a separate explicit choice.
                if (original.Device == null && original.ForceFeedbackDevice == null && !record.AllowCreatedRollback)
                {
                    record = record with { Detail = "Created device retained. Removing a hub-created device requires a separate explicit cleanup choice." };
                    Save(record);
                    return record;
                }
                record = record with { Status = VJoyChangeStatus.Restoring };
                Save(record);
                bool replaced = supportedPlan ? store.TryReplace(record.Expected, original, id, restoring: true) : store.TryReplace(record.Expected, original);
                var verified = store.Read(record.DeviceId);
                record = record with
                {
                    Status = !replaced ? VJoyChangeStatus.Conflict : verified.Fingerprint() == original.Fingerprint()
                        ? VJoyChangeStatus.Restored : VJoyChangeStatus.RestoreFailed,
                    Observed = verified,
                    RecoveryClosed = replaced && verified.Fingerprint() == original.Fingerprint(),
                    Detail = !replaced ? "Device changed, is occupied, or its driver changed. Backup retained; nothing overwritten."
                        : "Persisted configuration checked. A driver/OS restart may be needed before using the device. Backup retained."
                };
            }
        }
        catch (Exception error)
        {
            record = record with { Status = VJoyChangeStatus.RestoreFailed, Detail = "Backup retained at " + BackupPath(id) + ". " + error.Message };
        }
        Save(record);
        return record;
    }
}
