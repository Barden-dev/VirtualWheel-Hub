using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using SimRacingHub.Application.Contracts;

namespace SimRacingHub.Core;

internal sealed class VJoySetupService
{
    private readonly IVJoyConfigurationStore _devices;
    private readonly VJoyConfigurationJournal _journal;
    private readonly HubVJoyDeviceStore _selection;
    private readonly Func<uint, VJoyDeviceInfo> _capabilities;
    private readonly Func<string, CancellationToken, Task<int>> _apply;

    internal VJoySetupService(IVJoyConfigurationStore? devices = null, VJoyConfigurationJournal? journal = null,
        HubVJoyDeviceStore? selection = null, Func<uint, VJoyDeviceInfo>? capabilities = null,
        Func<string, CancellationToken, Task<int>>? apply = null)
    {
        _devices = devices ?? new WindowsVJoyConfigurationStore();
        _journal = journal ?? new VJoyConfigurationJournal();
        _selection = selection ?? new HubVJoyDeviceStore();
        _capabilities = capabilities ?? VJoyInventoryService.NativeCapabilities;
        _apply = apply ?? RunElevated;
    }

    internal VJoySetupPreview Preview(VJoyDeviceInfo inspected, bool changeExisting)
    {
        if (inspected.DeviceId is < 1 or > 16 || inspected.Error != null || inspected.Fingerprint == null)
            throw new InvalidOperationException("Inspect the selected vJoy device first.");
        var before = _devices.Read((uint)inspected.DeviceId);
        if (before.Fingerprint() != inspected.Fingerprint) throw new IOException("vJoy configuration changed after inspection. Preview again.");
        var capabilities = _capabilities(before.DeviceId) with
        {
            Fingerprint = before.Fingerprint(), ConfigurationExists = before.Device != null || before.ForceFeedbackDevice != null
        };
        var selection = _selection.Read();
        if (selection?.DeviceId == before.DeviceId && selection.PendingOperationId != null)
            throw new InvalidOperationException("Resume or cancel the saved output setup before preparing another operation.");
        if (selection?.DeviceId == before.DeviceId && selection.RequiresRestart && selection.Fingerprint == before.Fingerprint() &&
            !capabilities.MeetsRequirements(true))
            throw new InvalidOperationException("The reviewed configuration is already saved. Run Fix to activate it; no repeat configuration is needed.");
        if (capabilities.IsUsable && capabilities.MeetsRequirements(true))
            return new(null, capabilities, capabilities, false, false, null, before.Fingerprint());
        bool create = capabilities.IsAbsent;
        if (!create && !changeExisting) throw new InvalidOperationException("Existing device configuration is protected. Choose the explicit change action to preview it.");
        if (create && (before.Device != null || before.ForceFeedbackDevice != null) ||
            !create && capabilities.Status != VJoyWrapper.VJD_STAT_FREE)
            throw new InvalidOperationException("Stop controls and release the device before changing its configuration.");
        if (capabilities.DiscretePovs > 4) throw new InvalidOperationException("Unsupported vJoy POV layout. Nothing will be changed.");
        int povs = Math.Clamp(capabilities.DiscretePovs, 1, 4);
        string id = Guid.NewGuid().ToString("N");
        var expected = VJoyWheelPreset.Build(before, povs, id, create);
        _devices.ValidateProposed(expected);
        // Exact desired bytes and original backup are verified before the user
        // approves, rather than guessed after an external configuration tool.
        _journal.Prepare(before, expected, operationId: id, purpose: create ? "wheel-create" : "wheel-reconfigure",
            allowCreatedRollback: true, beforeDeviceStatus: capabilities.Status, previousSelectionFingerprint: selection?.Fingerprint,
            previousSelection: create ? selection : null);
        var target = new VJoyDeviceInfo(inspected.DeviceId, VJoyWrapper.VJD_STAT_FREE, true,
            Enumerable.Range(0x30, 8).ToArray(), 128, povs, 0, true, expected.Fingerprint());
        return new(id, capabilities, target, true, !create, _journal.BackupPath(id), expected.Fingerprint());
    }

    internal async Task<VJoySetupResult> ApplyAsync(VJoySetupPreview preview, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!preview.NeedsChange)
        {
            var current = _devices.Read((uint)preview.Before.DeviceId);
            var capabilities = _capabilities(current.DeviceId);
            return new(current.Fingerprint() == preview.ExpectedFingerprint && capabilities.Status is 0 or 1 && capabilities.MeetsRequirements(true), false);
        }
        if (preview.OperationId == null) throw new InvalidOperationException("Missing vJoy setup operation.");
        var intent = _journal.Read(preview.OperationId);
        if (intent.DeviceId != preview.Before.DeviceId || intent.BeforeHash != preview.Before.Fingerprint ||
            intent.Expected?.Fingerprint() != preview.ExpectedFingerprint || !_journal.MatchesPlan(intent, _devices))
            throw new InvalidOperationException("vJoy preview does not match its saved operation.");
        intent = _journal.Approve(intent.Id, preview.ExpectedFingerprint);
        try
        {
            _selection.ReserveCreation(intent);
            int code = await _apply(intent.Id, ct).ConfigureAwait(false);
            var result = _journal.Read(intent.Id);
            if (code != 0 || result.Status != VJoyChangeStatus.Applied || !result.ConfirmedWrite || result.Expected == null)
            {
                var recovered = result.Status is VJoyChangeStatus.Prepared or VJoyChangeStatus.Restoring or VJoyChangeStatus.Applied
                    ? _journal.Recover(intent.Id, _devices) : result;
                ReleaseRestoredReservation(recovered);
                return new(false, recovered.Status == VJoyChangeStatus.Restored, intent.Id, "Output_VJoySetupStopped",
                    $"Helper exit {code}. {result.HelperError} {recovered.Detail}");
            }
            if (!_devices.HasWriteReceipt(intent.Id, result.BeforeHash, result.Expected.Fingerprint()) ||
                _devices.Read(result.DeviceId).Fingerprint() != result.Expected.Fingerprint())
                return new(false, false, intent.Id, "Output_VJoySetupStopped", "Setup commit receipt or current state did not match.");
            _selection.AcceptApplied(result, _journal, _devices);
            var active = _capabilities(result.DeviceId);
            var completed = Completion(result, active);
            if (completed.Success && !completed.RequiresRestart && result.Purpose != "wheel-remove" && _selection.Read()?.DeviceId == result.DeviceId)
                _selection.SelectExisting(result.Expected, _devices);
            return completed;
        }
        catch (Win32Exception error) when (error.NativeErrorCode == 1223)
        {
            var result = _journal.Recover(intent.Id, _devices);
            ReleaseRestoredReservation(result);
            return new(false, result.Status == VJoyChangeStatus.Restored, intent.Id, ElevationErrorKey(error), error.Message + " " + result.Detail);
        }
        catch (OperationCanceledException)
        {
            ReleaseRestoredReservation(_journal.Recover(intent.Id, _devices));
            throw;
        }
        catch (Exception error)
        {
            var result = _journal.Recover(intent.Id, _devices);
            ReleaseRestoredReservation(result);
            return new(false, result.Status == VJoyChangeStatus.Restored, intent.Id, "Output_VJoySetupStopped", error.Message + " " + result.Detail);
        }
    }

    internal static VJoySetupResult Completion(VJoyChangeRecord record, VJoyDeviceInfo live)
    {
        if (record.ActivationError != null && !record.ActivationRequiresRestart)
            return new(false, false, record.Id, "Output_VJoyActivationPending", record.ActivationError);
        bool ready = record.Purpose == "wheel-remove" ? live.Status == VJoyWrapper.VJD_STAT_MISS
            : live.Status is 0 or 1 && live.MeetsRequirements(true);
        return ready ? new(true, false, record.Id)
            : record.ActivationRequiresRestart ? new(true, true, record.Id, Detail: record.ActivationError)
            : new(false, false, record.Id, "Output_VJoyActivationPending", record.ActivationError);
    }

    internal async Task<VJoySetupResult> RefreshSelectedAsync(CancellationToken ct = default)
    {
        var selected = _selection.Read() ?? throw new IOException("No vJoy output is selected.");
        var snapshot = _devices.Read((uint)selected.DeviceId);
        if (selected.PendingOperationId != null || snapshot.Fingerprint() != selected.Fingerprint)
            throw new IOException("The saved output changed; check diagnostics before activation.");
        string id = VJoyWheelPreset.Token(snapshot, VJoyWheelPreset.OperationValue)
            ?? throw new IOException("No verified hub setup is available for this device.");
        var record = _journal.Read(id);
        if (record.Expected?.Fingerprint() != snapshot.Fingerprint() || !record.ConfirmedWrite || record.RecoveryClosed ||
            !_devices.HasWriteReceipt(id, record.BeforeHash, snapshot.Fingerprint()))
            throw new IOException("The saved output does not match a verified hub commit.");
        try
        {
            int code = await RunElevated(id, "--refresh-vjoy", ct).ConfigureAwait(false);
            record = _journal.Read(id);
            if (code != 0) return new(false, false, id, "Output_VJoyActivationPending", $"Helper exit {code}. {record.HelperError ?? record.ActivationError}");
            var result = Completion(record, _capabilities(record.DeviceId));
            if (result.Success && !result.RequiresRestart) _selection.SelectExisting(snapshot, _devices);
            return result;
        }
        catch (Win32Exception error) when (error.NativeErrorCode == 1223)
        { return new(false, false, id, ElevationErrorKey(error), error.Message); }
    }

    private void ReleaseRestoredReservation(VJoyChangeRecord record)
    {
        // A failed/conflicting rollback may leave a committed configuration.
        // Keep its number reserved until that operation is explicitly resolved.
        if (record.Status is VJoyChangeStatus.Restored or VJoyChangeStatus.NoChange &&
            _devices.Read(record.DeviceId).Fingerprint() == record.BeforeHash)
            _selection.CancelReservation(record.Id, record);
    }

    internal static Task<int> RunElevated(string operationId, CancellationToken ct) => RunElevated(operationId, "--apply-vjoy", ct);

    internal static int? ReadHelperDownloadZone(string executable)
    {
        // Read only the small origin marker, never URLs/cookies or file contents.
        // Its presence alone does not mean Windows will refuse execution.
        try
        {
            using var stream = new FileStream(executable + ":Zone.Identifier", FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length > 16384) return null;
            using var reader = new StreamReader(stream);
            bool section = false;
            while (reader.ReadLine() is { } line)
            {
                line = line.Trim();
                if (line.StartsWith('[')) { section = line.Equals("[ZoneTransfer]", StringComparison.OrdinalIgnoreCase); continue; }
                if (!section) continue;
                var parts = line.Split('=', 2);
                if (parts.Length == 2 && parts[0].Trim().Equals("ZoneId", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(parts[1].Trim(), out int zone) && zone is >= 0 and <= 4) return zone;
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException or NotSupportedException) { }
        return null;
    }

    private sealed class HelperLaunchFailure(Win32Exception original, string executable, int? zone)
        : Win32Exception(original.NativeErrorCode,
            $"{original.Message} Windows error {original.NativeErrorCode}; helper='{executable}'; downloadZone={zone?.ToString() ?? "unknown"}.")
    {
        internal int? DownloadZone { get; } = zone;
    }

    internal static Win32Exception DescribeHelperLaunchFailure(Win32Exception error, string executable) =>
        new HelperLaunchFailure(error, executable, ReadHelperDownloadZone(executable));

    internal static string ElevationErrorKey(Win32Exception error) =>
        error is HelperLaunchFailure { DownloadZone: >= 3 } ? "Output_VJoyDownloadedHelperBlocked" : "Output_VJoyElevationDeclined";

    internal static async Task<int> RunElevated(string operationId, string command, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!Guid.TryParseExact(operationId, "N", out _)) throw new InvalidDataException("Invalid vJoy operation ID.");
        var start = new ProcessStartInfo(Environment.ProcessPath ?? throw new IOException("Cannot locate the application executable."))
        { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden };
        if (Path.GetFileNameWithoutExtension(start.FileName).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(Assembly.GetEntryAssembly()?.Location ?? throw new IOException("Cannot locate the application entry assembly."));
        if (command is not ("--apply-vjoy" or "--restore-vjoy" or "--refresh-vjoy")) throw new InvalidDataException("Invalid vJoy helper command.");
        start.ArgumentList.Add(command);
        start.ArgumentList.Add(operationId);
        start.ArgumentList.Add("--requester-pid");
        start.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        start.ArgumentList.Add("--data-directory");
        start.ArgumentList.Add(StoragePaths.LocalDataDirectory);
        AppLogger.Instance.LogInfo($"vJoy helper launch: command={command}; operation={operationId}; requesterPid={Environment.ProcessId}; data='{StoragePaths.LocalDataDirectory}'; executable='{start.FileName}'", showInStatusBar: false);
        AppLogger.Instance.Flush();
        bool helperStarted = false;
        try
        {
            using var process = Process.Start(start) ?? throw new IOException("Cannot start vJoy setup helper.");
            helperStarted = true;
            AppLogger.Instance.LogInfo($"vJoy helper started: operation={operationId}; pid={process.Id}", showInStatusBar: false);
            // Once started, observe its actual exit before permitting another
            // configuration operation. Cancelling a wait cannot cancel a commit.
            await process.WaitForExitAsync().ConfigureAwait(false);
            AppLogger.Instance.LogInfo($"vJoy helper exited: operation={operationId}; exit={process.ExitCode}", showInStatusBar: false);
            AppLogger.Instance.Flush();
            if (process.ExitCode is >= 0x10000 and <= 0x1FFFF)
                throw new Win32Exception(process.ExitCode - 0x10000,
                    $"Windows could not access the requesting hub context (error {process.ExitCode - 0x10000}).");
            return process.ExitCode;
        }
        catch (Win32Exception error)
        {
            var described = helperStarted ? error : DescribeHelperLaunchFailure(error, start.FileName);
            AppLogger.Instance.LogError($"vJoy helper launch/context failed: operation={operationId}; started={helperStarted}; Win32={error.NativeErrorCode}; executable='{start.FileName}'", described);
            AppLogger.Instance.Flush();
            if (helperStarted) throw;
            throw described;
        }
    }
}
