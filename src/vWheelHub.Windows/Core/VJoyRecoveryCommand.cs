namespace SimRacingHub.Core;

/// <summary>Explicit recovery entry point; never runs during ordinary startup.</summary>
public static class VJoyRecoveryCommand
{
    public static int Execute(string command, string operationId, int requesterPid, string dataDirectory, TextWriter output)
    {
        try
        {
            return VJoyHelperContext.Run(requesterPid, dataDirectory, (root, elevate) =>
            {
                var journal = new VJoyConfigurationJournal(Path.Combine(root, "vjoy-recovery"));
                var selection = new HubVJoyDeviceStore(Path.Combine(root, "vjoy-output.json"));
                using var capture = new StringWriter();
                int code = command switch
                {
                    "--apply-vjoy" => ApplyCore(operationId, capture, journal, selection, elevate),
                    "--refresh-vjoy" => RefreshCore(operationId, capture, journal, elevate),
                    "--restore-vjoy" => RunCore(operationId, capture, journal, elevate),
                    _ => throw new InvalidDataException("Invalid vJoy helper command.")
                };
                output.Write(capture.ToString());
                if (code != 0) journal.RecordHelperError(operationId, capture.ToString());
                return code;
            });
        }
        catch (System.ComponentModel.Win32Exception error)
        {
            output.WriteLine("Requester context stopped: " + error.Message);
            return 0x10000 + (error.NativeErrorCode & 0xFFFF);
        }
        catch (Exception error) { output.WriteLine("Requester context stopped: " + error.Message); return 1; }
    }

    public static int Apply(string operationId, TextWriter output) =>
        ApplyCore(operationId, output, new VJoyConfigurationJournal(), new HubVJoyDeviceStore(), store => store);

    private static int ApplyCore(string operationId, TextWriter output, VJoyConfigurationJournal journal,
        HubVJoyDeviceStore selection, Func<IVJoyConfigurationStore, IVJoyConfigurationStore> elevate)
    {
        try
        {
            var devices = elevate(new WindowsVJoyConfigurationStore());
            var intent = journal.Read(operationId);
            if (intent.ApprovedAt == null || intent.Expected == null || intent.RecoveryClosed ||
                intent.Status is not (VJoyChangeStatus.Prepared or VJoyChangeStatus.Applied) || !journal.MatchesPlan(intent, devices))
                throw new InvalidOperationException("No approved preset plan.");
            selection.ReserveCreation(intent);
            var result = journal.ApplyPrepared(operationId, devices);
            if (result.Status == VJoyChangeStatus.Applied && result.ConfirmedWrite)
            {
                selection.AcceptApplied(result, journal, devices);
                result = journal.Activate(operationId, devices);
            }
            else if (result.Status is VJoyChangeStatus.NoChange or VJoyChangeStatus.Restored &&
                devices.Read(result.DeviceId).Fingerprint() == result.BeforeHash) selection.CancelReservation(operationId, result);
            output.WriteLine(result.Status + ": " + result.Detail);
            return result.Status == VJoyChangeStatus.Applied && result.ConfirmedWrite ? 0 : 2;
        }
        catch (Exception error) { output.WriteLine("Setup stopped: " + error.Message); return 1; }
    }

    public static int Refresh(string operationId, TextWriter output) =>
        RefreshCore(operationId, output, new VJoyConfigurationJournal(), store => store);

    private static int RefreshCore(string operationId, TextWriter output, VJoyConfigurationJournal journal,
        Func<IVJoyConfigurationStore, IVJoyConfigurationStore> elevate)
    {
        try
        {
            var record = journal.Activate(operationId, elevate(new WindowsVJoyConfigurationStore()));
            output.WriteLine(record.ActivationError ?? "Live vJoy configuration checked.");
            // A verified saved layout remains valid even if activation is deferred.
            return 0;
        }
        catch (Exception error) { output.WriteLine("Activation stopped: " + error.Message); return 1; }
    }

    public static int Run(string operationId, TextWriter output) =>
        RunCore(operationId, output, new VJoyConfigurationJournal(), store => store);

    private static int RunCore(string operationId, TextWriter output, VJoyConfigurationJournal journal,
        Func<IVJoyConfigurationStore, IVJoyConfigurationStore> elevate)
    {
        try
        {
            // Validate all files before even constructing a Windows registry adapter.
            var record = journal.Read(operationId);
            if (record.Expected == null || record.ApprovedAt == null || record.Purpose is not ("wheel-create" or "wheel-recreate" or "wheel-reconfigure" or "wheel-remove") ||
                record.Status is VJoyChangeStatus.BackupOnly or VJoyChangeStatus.Unverified)
            {
                output.WriteLine("This record does not prove a hub change. Nothing restored. Backup: " + journal.BackupPath(operationId));
                return 2;
            }
            var devices = elevate(VJoyOfflineRecoveryStore.Create(record,journal.Original(operationId)));
            var restored = journal.RecoverExclusive(operationId, devices);
            if (restored.Status is VJoyChangeStatus.Restored or VJoyChangeStatus.NoChange && restored.ConfirmedWrite)
                restored = journal.Activate(operationId, devices, restoring: true);
            output.WriteLine(restored.Status + ": " + restored.Detail);
            output.WriteLine("Backup retained: " + journal.BackupPath(operationId));
            if (restored.ActivationError != null) output.WriteLine(restored.ActivationError);
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
