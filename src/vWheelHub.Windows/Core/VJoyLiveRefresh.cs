using System.Diagnostics;
using Microsoft.Win32;

namespace SimRacingHub.Core;

internal sealed record VJoyActivationResult(bool Ready, bool RequiresRestart = false, string? Error = null);

/// <summary>Activates an already committed layout; never writes a device layout or reboots Windows.</summary>
internal static class VJoyLiveRefresh
{
    internal static VJoyActivationResult Run(IVJoyConfigurationStore store, VJoyConfigurationSnapshot desired, bool restoring = false,
        Func<uint, SimRacingHub.Application.Contracts.VJoyDeviceInfo>? probe = null,
        Func<int>? refresh = null, Action? wait = null)
    {
        probe ??= VJoyInventoryService.NativeCapabilities;
        wait ??= () => Thread.Sleep(100);
        bool Ready()
        {
            var live = probe(desired.DeviceId);
            return desired.Device == null && desired.ForceFeedbackDevice == null ? live.Status == VJoyWrapper.VJD_STAT_MISS
                : live.Status is 0 or 1 && (restoring || live.MeetsRequirements(true));
        }
        try
        {
            if (store.Read(desired.DeviceId).Fingerprint() != desired.Fingerprint())
                return new(false, Error: "The saved vJoy layout changed before activation. Nothing refreshed.");
            if (Ready()) return new(true);
            var before = Enumerable.Range(1, 16).Select(id => store.Read((uint)id)).ToArray();
            // Restarting the shared PnP node briefly reconnects every vJoy output.
            // Never interrupt an aircraft/other feeder to activate a new wheel.
            if (Enumerable.Range(1, 16).Any(id => store.GetStatus((uint)id) is 0 or 2))
                return new(false, Error: "The configuration is saved. Close other applications using vJoy, then run Fix again to activate it.");
            int code = (refresh ?? RefreshInstalledNode)();
            if (code == 3010) return new(false, true, "Windows reported that a restart is required to activate vJoy.");
            if (code != 0) return new(false, Error: $"The configuration is saved, but Windows could not refresh vJoy (exit {code}). Run Fix again after resolving the error.");
            for (int attempt = 0; attempt < 100; attempt++)
            {
                try
                {
                    if (Ready())
                    {
                        if (before.Any(snapshot => store.Read(snapshot.DeviceId).Fingerprint() != snapshot.Fingerprint()))
                            return new(false, Error: "A vJoy configuration changed during activation. Check device diagnostics; no further changes were made.");
                        return new(true);
                    }
                }
                catch (IOException) { /* PnP enumeration is temporarily unavailable. */ }
                wait();
            }
            return new(false, Error: "The configuration is saved, but vJoy has not exposed the expected device after refresh. Check device diagnostics and run Fix again.");
        }
        catch (Exception error)
        {
            return new(false, Error: "The configuration is saved; activation failed: " + error.Message);
        }
    }

    private static int RefreshInstalledNode()
    {
        using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var root = machine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\ROOT\HIDCLASS", false)
            ?? throw new IOException("The installed vJoy PnP node was not found.");
        var nodes = root.GetSubKeyNames().Where(name =>
        {
            using var node = root.OpenSubKey(name, false);
            return name.Length == 4 && name.All(char.IsAsciiDigit) &&
                string.Equals(node?.GetValue("Service") as string, "vjoy", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(node?.GetValue("ClassGUID") as string, "{745a17a0-74d3-11d0-b6fe-00a0c90f57da}", StringComparison.OrdinalIgnoreCase);
        }).ToArray();
        if (nodes.Length != 1) throw new IOException("The installed vJoy PnP node is ambiguous; nothing refreshed.");
        var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "pnputil.exe"))
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("/restart-device");
        start.ArgumentList.Add(@"ROOT\HIDCLASS\" + nodes[0]);
        // No /reboot, /force, driver installation, deletion or broad device filter.
        using var process = Process.Start(start) ?? throw new IOException("Cannot refresh the installed vJoy node.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Task.WaitAll(output, error);
        AppLogger.Instance.LogInfo("vJoy live refresh: " + output.Result + error.Result);
        return process.ExitCode;
    }
}
