using SimRacingHub.Application.Contracts;

namespace SimRacingHub.Core;

internal sealed class VJoyInventoryService
{
    internal int ResolveAutomatic(SimRacingHub.Models.ResolvedProfile profile) => TryResolveAutomatic(profile)
        ?? throw new InvalidOperationException("Virtual wheel setup is required. Open Dashboard and device diagnostics.");

    internal int? TryResolveAutomatic(SimRacingHub.Models.ResolvedProfile profile)
    {
        // Preserve the installed output identity, including missing/pending states.
        // Only the selection file may be written here; never the driver configuration.
        if (_selection.Read() is { } saved) return saved.DeviceId;
        var inventory = Inspect();
        if (inventory.SelectionError != null) throw new InvalidOperationException(inventory.SelectionError);
        var candidate = inventory.Devices.Where(device => device.IsUsable && device.MeetsRequirements(true))
            .OrderByDescending(device => device.DeviceId == profile.VJoyDeviceId).ThenBy(device => device.DeviceId).FirstOrDefault();
        if (candidate == null) return null;
        Select(candidate, true, automatic: true);
        return _selection.Read()!.DeviceId;
    }

    private readonly IVJoyConfigurationStore _devices;
    private readonly HubVJoyDeviceStore _selection;
    private readonly Func<uint, VJoyDeviceInfo> _capabilities;
    internal VJoyInventoryService(IVJoyConfigurationStore? devices = null, HubVJoyDeviceStore? selection = null,
        Func<uint, VJoyDeviceInfo>? capabilities = null)
    {
        _devices = devices ?? new WindowsVJoyConfigurationStore();
        _selection = selection ?? new HubVJoyDeviceStore();
        _capabilities = capabilities ?? NativeCapabilities;
    }

    internal static VJoyDeviceInfo NativeCapabilities(uint id)
    {
        int status = VJoyWrapper.GetDeviceStatus(id);
        return ReadCapabilities(id, status, () => new((int)id, status, null,
            Enumerable.Range(0x30, 8).Where(axis => VJoyWrapper.CheckAxisExists(id, (uint)axis)).ToArray(),
            VJoyWrapper.GetButtonCount(id), VJoyWrapper.GetDiscretePovCount(id), VJoyWrapper.GetContinuousPovCount(id),
            VJoyWrapper.HasWheelCapabilities(id), null));
    }

    internal static VJoyDeviceInfo ReadCapabilities(uint id, int status, Func<VJoyDeviceInfo> readPresentDevice)
    {
        // The SDK repeatedly enumerates HID devices for every capability query
        // on a missing ID. A missing/unknown live device has no readable caps;
        // persisted registry configuration is checked independently by Inspect.
        return status is VJoyWrapper.VJD_STAT_MISS or VJoyWrapper.VJD_STAT_UNKN
            ? new((int)id, status, null, [], 0, 0, 0, false, null)
            : readPresentDevice();
    }

    internal VJoyDeviceInventory Inspect(CancellationToken ct = default)
    {
        var devices = new List<VJoyDeviceInfo>();
        for (uint id = 1; id <= 16; id++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var snapshot = _devices.Read(id);
                var capability = _capabilities(id);
                devices.Add(capability with
                {
                    ConfigurationExists = snapshot.Device != null || snapshot.ForceFeedbackDevice != null,
                    Fingerprint = snapshot.Fingerprint()
                });
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                devices.Add(new((int)id, VJoyWrapper.VJD_STAT_UNKN, null, [], 0, -1, -1, false, null, error.Message));
            }
        }
        try
        {
            var selected = _selection.Read();
            string? error = selected == null ? null : devices.Single(device => device.DeviceId == selected.DeviceId) switch
            {
                _ when selected.PendingOperationId != null => $"A vJoy operation for device #{selected.PendingDeviceId ?? selected.DeviceId} has not completed. Resume or cancel the saved operation; the hub will not choose another number.",
                { Fingerprint: var fingerprint } when fingerprint != selected.Fingerprint => "The selected shared vJoy device configuration or driver has changed.",
                { Status: VJoyWrapper.VJD_STAT_MISS } when selected.RequiresRestart => "vJoy configuration is saved. Restart the driver or Windows, then check the same device.",
                { Status: VJoyWrapper.VJD_STAT_MISS } => "The selected shared vJoy device is missing. Explicit recovery or setup is required.",
                { Status: VJoyWrapper.VJD_STAT_BUSY } => "The selected shared vJoy device is in use by another feeder. Release it; the hub will not switch device numbers.",
                _ => null
            };
            return new(devices, selected?.DeviceId, error, selected?.PendingOperationId);
        }
        catch (Exception error) { return new(devices, null, error.Message); }
    }

    internal bool Select(VJoyDeviceInfo inspected, bool wheel, bool automatic = false)
    {
        if (inspected.DeviceId is < 1 or > 16 || !inspected.IsUsable || !inspected.MeetsRequirements(wheel))
            throw new InvalidOperationException("Choose a compatible, accessible existing vJoy device.");
        var current = _devices.Read((uint)inspected.DeviceId);
        var capabilities = _capabilities((uint)inspected.DeviceId);
        if (current.Fingerprint() != inspected.Fingerprint || !capabilities.MeetsRequirements(wheel))
            throw new IOException("The vJoy configuration changed after inspection. Nothing was selected; check again.");
        _selection.SelectExisting(current, _devices, onlyIfUnselected: automatic);
        return true;
    }
}
