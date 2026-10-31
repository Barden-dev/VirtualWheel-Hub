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
            throw new InvalidOperationException("The reviewed configuration is already saved. Restart the driver or Windows, then check it; no repeat configuration is needed.");
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
                return new(false, recovered.Status == VJoyChangeStatus.Restored, intent.Id, "Output_VJoySetupStopped", recovered.Detail);
            }
            if (!_devices.HasWriteReceipt(intent.Id, result.BeforeHash, result.Expected.Fingerprint()) ||
                _devices.Read(result.DeviceId).Fingerprint() != result.Expected.Fingerprint())
                return new(false, false, intent.Id, "Output_VJoySetupStopped", "Setup commit receipt or current state did not match.");
            _selection.AcceptApplied(result, _journal, _devices);
            if (result.Purpose == "wheel-remove") return new(true, true, intent.Id);
            var active = _capabilities(result.DeviceId);
            bool ready = active.Status is 0 or 1 && active.MeetsRequirements(true);
            if (ready && _selection.Read()?.DeviceId == result.DeviceId) _selection.SelectExisting(result.Expected, _devices);
            return new(true, !ready, intent.Id);
        }
        catch (Win32Exception error) when (error.NativeErrorCode == 1223)
        {
            var result = _journal.Recover(intent.Id, _devices);
            ReleaseRestoredReservation(result);
            return new(false, result.Status == VJoyChangeStatus.Restored, intent.Id, "Output_VJoyElevationDeclined", result.Detail);
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

    private void ReleaseRestoredReservation(VJoyChangeRecord record)
    {
        // A failed/conflicting rollback may leave a committed configuration.
        // Keep its number reserved until that operation is explicitly resolved.
        if (record.Status is VJoyChangeStatus.Restored or VJoyChangeStatus.NoChange &&
            _devices.Read(record.DeviceId).Fingerprint() == record.BeforeHash)
            _selection.CancelReservation(record.Id, record);
    }

    internal static Task<int> RunElevated(string operationId, CancellationToken ct) => RunElevated(operationId, "--apply-vjoy", ct);

    internal static async Task<int> RunElevated(string operationId, string command, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!Guid.TryParseExact(operationId, "N", out _)) throw new InvalidDataException("Invalid vJoy operation ID.");
        var start = new ProcessStartInfo(Environment.ProcessPath ?? throw new IOException("Cannot locate the application executable."))
        { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden };
        if (Path.GetFileNameWithoutExtension(start.FileName).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(Assembly.GetEntryAssembly()?.Location ?? throw new IOException("Cannot locate the application entry assembly."));
        if (command is not ("--apply-vjoy" or "--restore-vjoy")) throw new InvalidDataException("Invalid vJoy helper command.");
        start.ArgumentList.Add(command);
        start.ArgumentList.Add(operationId);
        using var process = Process.Start(start) ?? throw new IOException("Cannot start vJoy setup helper.");
        // Once started, observe its actual exit before permitting another
        // configuration operation. Cancelling a wait cannot cancel a commit.
        await process.WaitForExitAsync().ConfigureAwait(false);
        return process.ExitCode;
    }
}
