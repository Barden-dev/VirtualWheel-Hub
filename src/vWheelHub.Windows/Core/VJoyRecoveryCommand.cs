namespace SimRacingHub.Core;

/// <summary>Explicit recovery entry point; never runs during ordinary startup.</summary>
public static class VJoyRecoveryCommand
{
    public static int Apply(string operationId, TextWriter output)
    {
        try
        {
            var journal = new VJoyConfigurationJournal();
            var selection = new HubVJoyDeviceStore();
            var devices = new WindowsVJoyConfigurationStore();
            var intent = journal.Read(operationId);
            if (intent.ApprovedAt == null || intent.Expected == null || !journal.MatchesPlan(intent, devices))
                throw new InvalidOperationException("No approved preset plan.");
            selection.ReserveCreation(intent);
            var result = journal.ApplyPrepared(operationId, devices);
            if (result.Status == VJoyChangeStatus.Applied && result.ConfirmedWrite) selection.AcceptApplied(result, journal, devices);
            else if (result.Status is VJoyChangeStatus.NoChange or VJoyChangeStatus.Restored &&
                devices.Read(result.DeviceId).Fingerprint() == result.BeforeHash) selection.CancelReservation(operationId, result);
            output.WriteLine(result.Status + ": " + result.Detail);
            return result.Status == VJoyChangeStatus.Applied && result.ConfirmedWrite ? 0 : 2;
        }
        catch (Exception error) { output.WriteLine("Setup stopped: " + error.Message); return 1; }
    }

    public static int Run(string operationId, TextWriter output)
    {
        try
        {
            var journal = new VJoyConfigurationJournal();
            // Validate all files before even constructing a Windows registry adapter.
            var record = journal.Read(operationId);
            if (record.Expected == null || record.ApprovedAt == null || record.Purpose is not ("wheel-create" or "wheel-recreate" or "wheel-reconfigure" or "wheel-remove") ||
                record.Status is VJoyChangeStatus.BackupOnly or VJoyChangeStatus.Unverified)
            {
                output.WriteLine("This record does not prove a hub change. Nothing restored. Backup: " + journal.BackupPath(operationId));
                return 2;
            }
            var restored = journal.RecoverExclusive(operationId, VJoyOfflineRecoveryStore.Create(record,journal.Original(operationId)));
            output.WriteLine(restored.Status + ": " + restored.Detail);
            output.WriteLine("Backup retained: " + journal.BackupPath(operationId));
            if (restored.Status == VJoyChangeStatus.Restored)
                output.WriteLine("Restart the vJoy driver or Windows before using the device. Persisted data was verified; live driver state was not.");
            return restored.Status is VJoyChangeStatus.Restored or VJoyChangeStatus.NoChange ? 0 : 2;
        }
        catch (Exception error)
        {
            output.WriteLine("Recovery stopped: " + error.Message);
            output.WriteLine("Keep the backup. Registry recovery requires administrator rights under the same Windows user. No unsafe fallback is used.");
            return 1;
        }
    }
}
