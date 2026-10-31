using Microsoft.Win32;

namespace SimRacingHub.Core;

/// <summary>
/// A free feeder slot is not an unconfigured device. Until transactional backup
/// and restore exist, automatic configuration may only create an absent device.
/// </summary>
internal static class VJoyConfigurationSafety
{
    internal static bool CanCreate(int status, bool? configurationExists) =>
        CreationBlockReason(status, configurationExists) == null;

    internal static string? CreationBlockReason(int status, bool? configurationExists)
    {
        if (status is VJoyWrapper.VJD_STAT_BUSY or VJoyWrapper.VJD_STAT_OWN)
            return "DiagPlatform_VJoyDiagnosticService_101";
        if (configurationExists == true || status == VJoyWrapper.VJD_STAT_FREE)
            return "Output_VJoyExistingProtected";
        if (configurationExists != false || status != VJoyWrapper.VJD_STAT_MISS)
            return "Output_VJoyConfigurationUnknown";
        return null;
    }

    internal static bool? ReadConfigurationExists(uint deviceId)
    {
        if (deviceId is < 1 or > 16) throw new ArgumentOutOfRangeException(nameof(deviceId));
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var parameters = machine.OpenSubKey(@"SYSTEM\CurrentControlSet\services\vjoy\Parameters", writable: false);
            // A missing/unsupported driver layout is not evidence of an empty slot.
            if (parameters == null) return null;
            using var device = parameters.OpenSubKey($"Device{deviceId:00}", writable: false);
            return device != null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            AppLogger.Instance.LogWarning($"Cannot inspect persisted vJoy configuration: {error.Message}");
            return null;
        }
    }
}
