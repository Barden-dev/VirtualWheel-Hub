using System.Diagnostics;
using SimRacingHub.Application.Contracts;
using SimRacingHub.Application.Localization;
using SimRacingHub.Core;

namespace SimRacingHub.Linux;

public static class LinuxDevicePreparation
{
    public static bool CanElevate => OperatingSystem.IsLinux() && File.Exists("/usr/bin/pkexec");

    public static async Task<bool> ApplyPermissionsAsync(CancellationToken ct, bool rollback = false)
    {
        if (!CanElevate) throw new GameSetupException("Prep_Error_ElevationUnavailable");
        using var stream = typeof(LinuxDevicePreparation).Assembly.GetManifestResourceStream("vWheelHub.PrepareDevices")
            ?? throw new GameSetupException("Prep_Error_Component", "Missing resource: vWheelHub.PrepareDevices");
        using var reader = new StreamReader(stream);
        var info = new ProcessStartInfo("/usr/bin/pkexec") { UseShellExecute = false, RedirectStandardError = true };
        info.ArgumentList.Add("/bin/sh");
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add((await reader.ReadToEndAsync(ct)).Replace("\r\n", "\n"));
        info.ArgumentList.Add("vWheelHub");
        info.ArgumentList.Add(rollback ? "rollback" : "apply");
        using var process = Process.Start(info);
        if (process == null) throw new GameSetupException("Prep_Error_Process", "/usr/bin/pkexec");
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().ConfigureAwait(false);
        string details = $"pkexec exit {process.ExitCode}\n" + await stderr.ConfigureAwait(false);
        if (process.ExitCode is 126 or 127) throw new GameSetupException("Prep_Error_ElevationDenied", details);
        if (process.ExitCode != 0) throw new GameSetupException("Prep_Error_DevicePermissions", details);
        return true;
    }

    public static async Task<bool> SelectDevicesAsync(IUserDialogService dialogs, ILocalizationManager language, CancellationToken ct)
    {
        var settings = AppSettings.Load();
        var result = LinuxInputDeviceResolver.ResolveDevices(settings);
        async Task<string?> Select(IReadOnlyList<DiscoveredDevice> devices, string? selected)
        {
            if (selected != null) return selected;
            if (devices.Count == 1 && !result.IsDisconnected) return devices[0].Path;
            foreach (var device in devices)
            {
                if (await dialogs.ShowConfirmAsync(language.GetString("Prep_SelectDevice"), device.Name + "\n" + device.Path,
                    language.GetString("Common_Select"), language.GetString("Prep_NextDevice"), DialogSeverity.Info, ct).ConfigureAwait(false))
                    return device.Path;
            }
            return null;
        }
        var mouse = await Select(result.AvailableMice, result.MousePath);
        var keyboard = await Select(result.AvailableKeyboards, result.KeyboardPath);
        if (mouse == null || keyboard == null)
        {
            await dialogs.ShowNoticeAsync(language.GetString("Prep_SelectDevice"), language.GetString("Prep_InputSelectionRequired"), DialogSeverity.Info, ct);
            return false;
        }
        settings.LinuxSelectedMousePath = mouse;
        settings.LinuxSelectedKeyboardPath = keyboard;
        settings.LinuxSelectedMouseIdentity = LinuxInputDeviceResolver.GetIdentity(mouse);
        settings.LinuxSelectedKeyboardIdentity = LinuxInputDeviceResolver.GetIdentity(keyboard);
        settings.Save();
        return true;
    }
}
