namespace SimRacingHub.Core;

/// <summary>Proves creation through protected commits, never through a UI flag or marker alone.</summary>
internal static class VJoyDeviceOwnership
{
    internal static bool Verify(VJoyConfigurationSnapshot current, VJoyConfigurationJournal journal, IVJoyConfigurationStore devices)
    {
        string? creation = VJoyWheelPreset.Token(current, VJoyWheelPreset.OwnerValue);
        if (creation == null) return false;
        var visited = new HashSet<string>(StringComparer.Ordinal);
        for (int depth = 0; depth < 64; depth++)
        {
            string? id = VJoyWheelPreset.Token(current, VJoyWheelPreset.OperationValue);
            if (id == null || !visited.Add(id)) return false;
            var record = journal.Read(id);
            var before = journal.Original(id);
            if (record.ApprovedAt == null || record.RecoveryClosed || record.Expected?.Fingerprint() != current.Fingerprint() ||
                !devices.HasWriteReceipt(id, record.BeforeHash, current.Fingerprint()) ||
                devices.HasWriteReceipt(id, current.Fingerprint(), record.BeforeHash, restoring: true)) return false;
            if (id == creation)
                return record.Purpose is "wheel-create" or "wheel-recreate" && record.BeforeDeviceStatus == VJoyWrapper.VJD_STAT_MISS &&
                    before.Device == null && before.ForceFeedbackDevice == null;
            if (record.Purpose != "wheel-reconfigure" || VJoyWheelPreset.Token(before, VJoyWheelPreset.OwnerValue) != creation) return false;
            current = before;
        }
        return false;
    }

    internal static bool MatchesRemoval(VJoyChangeRecord record, VJoyConfigurationSnapshot before,
        VJoyConfigurationJournal journal, IVJoyConfigurationStore devices) => record.Purpose == "wheel-remove" &&
        record.BeforeDeviceStatus is VJoyWrapper.VJD_STAT_FREE or VJoyWrapper.VJD_STAT_MISS &&
        record.Expected?.Fingerprint() == (before with { Device = null, ForceFeedbackDevice = null }).Fingerprint() &&
        Verify(before, journal, devices);

    internal static bool MatchesRecreation(VJoyChangeRecord record, VJoyConfigurationSnapshot before,
        VJoyConfigurationJournal journal, IVJoyConfigurationStore devices)
    {
        if (record.Purpose != "wheel-recreate" || record.PreviousSelection is not { CreatedByHub: true, PendingOperationId: null } selected ||
            selected.DeviceId != record.DeviceId || selected.Fingerprint != record.PreviousSelectionFingerprint ||
            !VJoyWheelPreset.MatchesPlan(record with { Purpose = "wheel-create" }, before)) return false;
        var historical = journal.FindExpected(record.DeviceId, selected.Fingerprint);
        return historical.Driver == before.Driver && selected.CreationOperationId == VJoyWheelPreset.Token(historical, VJoyWheelPreset.OwnerValue) &&
            Verify(historical, journal, devices);
    }
}
