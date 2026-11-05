namespace SimRacingHub.Application.Contracts;

public sealed record VJoyDeviceInfo(int DeviceId, int Status, bool? ConfigurationExists,
    int[] Axes, int Buttons, int DiscretePovs, int ContinuousPovs, bool WheelCapabilities,
    string? Fingerprint, string? Error = null)
{
    public bool IsAbsent => Status == 3 && ConfigurationExists == false && Fingerprint != null && Error == null;
    public bool IsUsable => Status is 0 or 1 && Error == null && Fingerprint != null;
    public bool MeetsRequirements(bool wheel) => Buttons >= (wheel ? 128 : 2) &&
        (wheel ? Enumerable.Range(0x30, 8) : Enumerable.Range(0x30, 3)).All(axis => Axes.Contains(axis)) &&
        (!wheel || WheelCapabilities);
}

public sealed record VJoyDeviceInventory(IReadOnlyList<VJoyDeviceInfo> Devices, int? SelectedDeviceId, string? SelectionError = null,
    string? PendingOperationId = null, bool RequiresRestart = false)
{
    public int? SuggestedNewDeviceId => Devices.Where(device => device.DeviceId is >= 1 and <= 16 && device.IsAbsent).OrderBy(device => device.DeviceId)
        .Select(device => (int?)device.DeviceId).FirstOrDefault();
}
