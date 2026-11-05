using SimRacingHub.Application.Contracts;
using SimRacingHub.Models;
using SimRacingHub.Application.Localization;

namespace SimRacingHub.Application.Features.Output;

/// <summary>Prepares a dedicated output without changing any existing device layout.</summary>
public sealed class AutomaticVJoySetup(IOutputDiagnostics diagnostics, ILocalizationManager? localization = null)
{
    private readonly ILocalizationManager _localization = localization ?? new LocalizationManager();
    private readonly SemaphoreSlim _gate = new(1, 1);
    public bool RequiresRestart { get; private set; }

    public async Task<DeviceDiagnosticSnapshot> EnsureAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            RequiresRestart = false;
            var inventory = await diagnostics.InspectVJoyAsync(ct).ConfigureAwait(false);
            if (inventory.PendingOperationId is { } operation)
            {
                var pending = await diagnostics.InspectPendingVJoyAsync(ct).ConfigureAwait(false);
                if (pending == null || pending.OperationId != operation || pending.Purpose is not ("wheel-create" or "wheel-recreate") ||
                    !(pending.CanResume && pending.NeedsRegistryRestore || pending.CanCancel && !pending.NeedsRegistryRestore))
                    throw new InvalidOperationException(inventory.SelectionError ?? "The saved vJoy operation cannot be completed automatically. Check device diagnostics.");
                // A proven commit only needs acknowledgement. An unchanged original
                // can close its old intent before a new plan is prepared. Ambiguous
                // or advanced maintenance operations remain reserved for recovery.
                var recovered = await diagnostics.ResolvePendingVJoyAsync(pending, resume: pending.NeedsRegistryRestore, ct).ConfigureAwait(false);
                if (!recovered.Success)
                    throw new GameSetupException(recovered.ErrorKey ?? "Output_VJoySetupStopped",
                        _localization.GetString(recovered.ErrorKey ?? "Output_VJoySetupStopped") + " " + recovered.Detail);
                inventory = await diagnostics.InspectVJoyAsync(ct).ConfigureAwait(false);
                if (recovered.RequiresRestart)
                {
                    RequiresRestart = true;
                    return DeviceDiagnosticSnapshot.Unavailable(OutputDeviceMode.VJoy,
                        _localization.GetString("Output_VJoySavedRestart"), _localization.GetString("Output_VJoySavedRestart"));
                }
            }
            if (inventory.PendingOperationId != null || inventory.SelectionError != null && inventory.SelectedDeviceId == null)
                throw new InvalidOperationException(inventory.SelectionError ?? "A saved vJoy operation must finish before automatic setup can continue.");

            var selected = inventory.Devices.FirstOrDefault(d => d.DeviceId == inventory.SelectedDeviceId);
            // A pending driver restart must never allocate another device on each launch.
            if (inventory.RequiresRestart && selected?.MeetsRequirements(true) != true)
            {
                var activated = await diagnostics.RefreshVJoyAsync(ct).ConfigureAwait(false);
                RequiresRestart = activated.RequiresRestart;
                if (!activated.Success)
                    throw new GameSetupException(activated.ErrorKey ?? "Output_VJoyActivationPending",
                        _localization.GetString(activated.ErrorKey ?? "Output_VJoyActivationPending") + " " + activated.Detail);
                if (RequiresRestart)
                    return DeviceDiagnosticSnapshot.Unavailable(OutputDeviceMode.VJoy,
                        _localization.GetString("Output_VJoySavedRestart"), _localization.GetString("Output_VJoySavedRestart"));
                return await diagnostics.CheckAsync(OutputDeviceMode.VJoy, ct).ConfigureAwait(false);
            }

            var compatible = inventory.Devices.Where(d => d.Error == null && d.IsUsable && d.MeetsRequirements(true) &&
                (d.Status == 1 || d.DeviceId == inventory.SelectedDeviceId))
                .OrderByDescending(d => d.DeviceId == inventory.SelectedDeviceId).ThenBy(d => d.DeviceId).FirstOrDefault();
            if (compatible != null)
            {
                if (!await diagnostics.SelectVJoyAsync(compatible, true, ct).ConfigureAwait(false))
                    throw new InvalidOperationException("Could not select the available vJoy device. Check device diagnostics.");
            }
            else
            {
                VJoySetupPreview? preview = null;
                var conflicts = new List<string>();
                foreach (var target in inventory.Devices.Where(d => d.IsAbsent).OrderBy(d => d.DeviceId))
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        preview = await diagnostics.PreviewVJoyAsync(target, changeExisting: false, ct).ConfigureAwait(false)
                            ?? throw new InvalidOperationException("Automatic vJoy setup is unavailable. Check device diagnostics.");
                        if (preview.ChangesExistingDevice || preview.Before.DeviceId != target.DeviceId || !preview.Before.IsAbsent)
                            throw new InvalidOperationException("Automatic setup refused to change an existing vJoy device.");
                        break;
                    }
                    catch (VJoyLayoutConflictException error) when (error.DeviceId == target.DeviceId)
                    {
                        // Only a proven pre-write layout conflict permits trying another ID.
                        // Never retry an ambiguous or partially committed setup operation.
                        conflicts.Add(error.Message);
                    }
                }
                if (preview == null)
                {
                    var readFailures = inventory.Devices.Where(d => d.Error != null).GroupBy(d => d.Error)
                        .Select(g => $"{string.Join(", ", g.Select(d => "#" + d.DeviceId))}: {g.Key}").ToArray();
                    string reason = readFailures.Length == 0 ? _localization.GetString("Output_VJoyNoUnusedNumbers")
                        : _localization.GetString("Output_VJoyInspectFailed") + " " + string.Join(Environment.NewLine, readFailures);
                    throw new InvalidOperationException(reason +
                        (conflicts.Count == 0 ? "" : Environment.NewLine + string.Join(Environment.NewLine, conflicts)));
                }
                var result = await diagnostics.ApplyVJoyAsync(preview, ct).ConfigureAwait(false);
                if (!result.Success)
                    throw new GameSetupException(result.ErrorKey ?? "Output_VJoySetupStopped",
                        _localization.GetString(result.ErrorKey ?? "Output_VJoySetupStopped") + " " + result.Detail);
                if (result.RequiresRestart)
                {
                    RequiresRestart = true;
                    return DeviceDiagnosticSnapshot.Unavailable(OutputDeviceMode.VJoy,
                        _localization.GetString("Output_VJoySavedRestart"), _localization.GetString("Output_VJoySavedRestart"));
                }
            }
            return await diagnostics.CheckAsync(OutputDeviceMode.VJoy, ct).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }
}
