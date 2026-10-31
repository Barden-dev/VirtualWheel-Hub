using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using SimRacingHub.Application.Contracts;
using SimRacingHub.Linux.Native;
using SimRacingHub.Models;

namespace SimRacingHub.Linux
{
    public enum UInputAccessStatus
    {
        Available,
        PermissionDenied,
        NodeNotFound,
        ModuleNotLoaded,
        Error
    }

    public sealed record UInputProbeResult(
        UInputAccessStatus Status,
        int Errno = 0,
        string? Message = null);

    public sealed record InputDeviceNodeInfo(
        string Path,
        bool IsReadable,
        int Errno = 0,
        bool IsMouse = false,
        bool IsKeyboard = false);

    public sealed record LinuxInputDiagnosticsSnapshot(
        bool IsAvailable,
        bool HasReadableMouse,
        bool HasReadableKeyboard,
        int TotalDeviceCount,
        int ReadableDeviceCount,
        string StatusText,
        string? ErrorDetails = null,
        IReadOnlyList<string>? RecommendedActions = null);

    /// <summary>
    /// Abstraction for probing Linux filesystem nodes and native devices.
    /// Allows deterministic unit testing of missing device, permission denied, and success scenarios.
    /// </summary>
    public interface ILinuxDiagnosticsProbe
    {
        UInputProbeResult ProbeUInput();
        bool InputDirectoryExists();
        IReadOnlyList<string> EnumerateEventDevicePaths();
        InputDeviceNodeInfo ProbeEventDevice(string path);
    }

    /// <summary>
    /// Default probe executing live Linux filesystem and native ioctl checks.
    /// </summary>
    public sealed class DefaultLinuxDiagnosticsProbe : ILinuxDiagnosticsProbe
    {
        private readonly SimRacingHub.Application.Localization.ILocalizationManager _localization;
        private string L(string key, params object?[] args) => string.Format(System.Globalization.CultureInfo.GetCultureInfo(_localization.CurrentCultureCode), _localization.GetString(key), args);

        public DefaultLinuxDiagnosticsProbe(SimRacingHub.Application.Localization.ILocalizationManager? localization = null) => _localization = localization ?? new SimRacingHub.Application.Localization.LocalizationManager();

        private const string UInputPath = "/dev/uinput";
        private const string InputDirectory = "/dev/input";
        private const string UInputSysfs = "/sys/class/misc/uinput";
        private const string ProcModules = "/proc/modules";

        public UInputProbeResult ProbeUInput()
        {
            if (!File.Exists(UInputPath))
            {
                bool moduleLoaded = Directory.Exists(UInputSysfs);
                if (!moduleLoaded && File.Exists(ProcModules))
                {
                    try
                    {
                        string modules = File.ReadAllText(ProcModules);
                        moduleLoaded = modules.Contains("uinput", StringComparison.OrdinalIgnoreCase);
                    }
                    catch
                    {
                        // Fall back to false if /proc/modules is not readable
                    }
                }

                if (!moduleLoaded)
                {
                    return new UInputProbeResult(
                        UInputAccessStatus.ModuleNotLoaded,
                        Errno: 2,
                        Message: L("DiagPlatform_LinuxOutputDiagnostics_138"));
                }

                return new UInputProbeResult(
                    UInputAccessStatus.NodeNotFound,
                    Errno: 2,
                    Message: L("DiagPlatform_LinuxOutputDiagnostics_139"));
            }

            int fd = -1;
            try
            {
                fd = LinuxNative.Open(UInputPath, LinuxNative.O_WRONLY | LinuxNative.O_NONBLOCK);
                if (fd >= 0)
                {
                    return new UInputProbeResult(UInputAccessStatus.Available);
                }

                int errno = Marshal.GetLastWin32Error();
                if (errno == 13 || errno == 1) // 13 = EACCES, 1 = EPERM
                {
                    return new UInputProbeResult(
                        UInputAccessStatus.PermissionDenied,
                        Errno: errno,
                        Message: L("DiagPlatform_LinuxOutputDiagnostics_140"));
                }

                return new UInputProbeResult(
                    UInputAccessStatus.Error,
                    Errno: errno,
                    Message: L("DiagPlatform_LinuxOutputDiagnostics_141", errno));
            }
            catch (Exception ex)
            {
                return new UInputProbeResult(
                    UInputAccessStatus.Error,
                    Errno: -1,
                    Message: L("DiagPlatform_LinuxOutputDiagnostics_142", ex.Message));
            }
            finally
            {
                if (fd >= 0)
                {
                    try { LinuxNative.Close(fd); } catch { }
                }
            }
        }

        public bool InputDirectoryExists()
        {
            return Directory.Exists(InputDirectory);
        }

        public IReadOnlyList<string> EnumerateEventDevicePaths()
        {
            var result = new List<string>();
            if (!Directory.Exists(InputDirectory))
            {
                return result;
            }

            for (int i = 0; i < 32; i++)
            {
                string path = Path.Combine(InputDirectory, $"event{i}");
                if (File.Exists(path))
                {
                    result.Add(path);
                }
            }

            return result;
        }

        public InputDeviceNodeInfo ProbeEventDevice(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return new InputDeviceNodeInfo(path ?? string.Empty, IsReadable: false, Errno: 2);
            }

            int fd = -1;
            try
            {
                fd = LinuxNative.Open(path, LinuxNative.O_RDONLY | LinuxNative.O_NONBLOCK);
                if (fd < 0)
                {
                    int errno = Marshal.GetLastWin32Error();
                    return new InputDeviceNodeInfo(path, IsReadable: false, Errno: errno);
                }

                bool isMouse = false;
                bool isKeyboard = false;

                byte[] relBits = new byte[4];
                int relRet = LinuxNative.Ioctl(fd, LinuxNative.EVIOCGBIT_REL_4, relBits);
                if (relRet >= 0 && (relBits[0] & (1 << LinuxNative.REL_X)) != 0)
                {
                    isMouse = true;
                }

                byte[] keyBits = new byte[96];
                int keyRet = LinuxNative.Ioctl(fd, LinuxNative.EVIOCGBIT_KEY_96, keyBits);
                if (keyRet >= 0 && keyBits.Length > 3)
                {
                    // KEY_A = 30, byte 3, bit 6
                    if ((keyBits[3] & (1 << 6)) != 0)
                    {
                        isKeyboard = true;
                    }
                }

                return new InputDeviceNodeInfo(path, IsReadable: true, Errno: 0, IsMouse: isMouse, IsKeyboard: isKeyboard);
            }
            catch (Exception)
            {
                return new InputDeviceNodeInfo(path, IsReadable: false, Errno: -1);
            }
            finally
            {
                if (fd >= 0)
                {
                    try { LinuxNative.Close(fd); } catch { }
                }
            }
        }
    }

    /// <summary>
    /// Linux implementation of IOutputDiagnostics providing actionable diagnostic messages
    /// for uinput virtual output and evdev input permissions without destructive scripts or root escalations.
    /// </summary>
    public sealed class LinuxOutputDiagnostics : IOutputDiagnostics, IInputReadinessDiagnostics
    {
        private readonly SimRacingHub.Application.Localization.ILocalizationManager _localization;
        private string L(string key, params object?[] args) => string.Format(System.Globalization.CultureInfo.GetCultureInfo(_localization.CurrentCultureCode), _localization.GetString(key), args);

        private readonly ILinuxDiagnosticsProbe _probe;

        public LinuxOutputDiagnostics(ILinuxDiagnosticsProbe? probe = null, SimRacingHub.Application.Localization.ILocalizationManager? localization = null)
        {
            _localization = localization ?? new SimRacingHub.Application.Localization.LocalizationManager();
            _probe = probe ?? new DefaultLinuxDiagnosticsProbe(_localization);
        }

        public Task<bool> RequestRestartAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("/usr/bin/systemctl", "reboot") { UseShellExecute = false });
            return Task.FromResult(process != null);
        }
        public bool RequiresRestart { get; private set; }
        public bool CanResumeWhenAccessible => true;
        public string PreparationDescription => _localization.GetString("Prep_LinuxChanges");
        public bool CanRollbackRepair => File.Exists(PermissionMarker);
        private static string PermissionMarker => Path.Combine(SimRacingHub.Core.StoragePaths.LocalDataDirectory, "linux-permissions-prepared");
        public async Task<bool> RollbackRepairAsync(CancellationToken ct = default)
        {
            if (!CanRollbackRepair) return true;
            if (!await LinuxDevicePreparation.ApplyPermissionsAsync(ct, rollback: true).ConfigureAwait(false)) return false;
            File.Delete(PermissionMarker);
            return true;
        }
        public async Task<bool> TryRepairAsync(OutputDeviceMode mode, CancellationToken ct = default)
        {
            RequiresRestart = false;
            if ((await CheckAsync(mode, ct).ConfigureAwait(false)).IsAvailable && CheckInputDevices(ct).IsAvailable) return true;
            Directory.CreateDirectory(Path.GetDirectoryName(PermissionMarker)!);
            File.WriteAllText(PermissionMarker, "prepared");
            if (!await LinuxDevicePreparation.ApplyPermissionsAsync(ct).ConfigureAwait(false)) return false;
            RequiresRestart = !(await CheckAsync(mode, ct).ConfigureAwait(false)).IsAvailable || !CheckInputDevices(ct).IsAvailable;
            return true;
        }
        public Task<bool> PrepareInputAsync(IUserDialogService dialogs, SimRacingHub.Application.Localization.ILocalizationManager localization, CancellationToken ct = default)
            => LinuxDevicePreparation.SelectDevicesAsync(dialogs, localization, ct);

        public Task<InputReadinessSnapshot> CheckInputAsync(CancellationToken ct = default)
            => Task.Run(() => CheckInputCore(ct), ct);

        private InputReadinessSnapshot CheckInputCore(CancellationToken ct)
        {
            var result = CheckInputDevices(ct);
            if (OperatingSystem.IsLinux() && _probe is DefaultLinuxDiagnosticsProbe)
            {
                var selection = LinuxInputDeviceResolver.ResolveDevices(SimRacingHub.Core.AppSettings.Load());
                if (!selection.IsUnambiguous || selection.IsDisconnected ||
                    !_probe.ProbeEventDevice(selection.MousePath!).IsReadable || !_probe.ProbeEventDevice(selection.KeyboardPath!).IsReadable)
                    return new InputReadinessSnapshot(false, _localization.GetString("Prep_InputSelectionRequired"));
            }
            string? details = result.ErrorDetails;
            if (result.RecommendedActions is { Count: > 0 })
                details = string.Join("\n", result.RecommendedActions);
            return new InputReadinessSnapshot(
                result.IsAvailable && result.HasReadableMouse && result.HasReadableKeyboard,
                result.HasReadableKeyboard ? result.StatusText : result.StatusText + L("DiagPlatform_LinuxOutputDiagnostics_143"), details);
        }

        public Task<DeviceDiagnosticSnapshot> CheckAsync(OutputDeviceMode mode, CancellationToken ct = default)
            => Task.Run(() => CheckOutputCore(mode, ct), ct);

        private DeviceDiagnosticSnapshot CheckOutputCore(OutputDeviceMode mode, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var probeResult = _probe.ProbeUInput();

            switch (probeResult.Status)
            {
                case UInputAccessStatus.Available:
                {
                    string modeDesc = mode == OutputDeviceMode.Xbox360
                        ? L("DiagPlatform_LinuxOutputDiagnostics_144")
                        : L("DiagPlatform_LinuxOutputDiagnostics_145");

                    return DeviceDiagnosticSnapshot.Available(mode, modeDesc) with { CanRepair = OperatingSystem.IsLinux() };
                }

                case UInputAccessStatus.ModuleNotLoaded:
                {
                    return DeviceDiagnosticSnapshot.Unavailable(
                        mode,
                        statusText: L("DiagPlatform_LinuxOutputDiagnostics_146"),
                        errorDetails: probeResult.Message ?? L("DiagPlatform_LinuxOutputDiagnostics_147"),
                        recommendedActions: new[]
                        {
                            L("DiagPlatform_LinuxOutputDiagnostics_148"),
                            L("DiagPlatform_LinuxOutputDiagnostics_149"),
                            L("DiagPlatform_LinuxOutputDiagnostics_150")
                        }, canRepair: LinuxDevicePreparation.CanElevate);
                }

                case UInputAccessStatus.NodeNotFound:
                {
                    return DeviceDiagnosticSnapshot.Unavailable(
                        mode,
                        statusText: L("DiagPlatform_LinuxOutputDiagnostics_151"),
                        errorDetails: probeResult.Message ?? L("DiagPlatform_LinuxOutputDiagnostics_152"),
                        recommendedActions: new[]
                        {
                            L("DiagPlatform_LinuxOutputDiagnostics_153"),
                            L("DiagPlatform_LinuxOutputDiagnostics_154")
                        }, canRepair: LinuxDevicePreparation.CanElevate);
                }

                case UInputAccessStatus.PermissionDenied:
                {
                    return DeviceDiagnosticSnapshot.Unavailable(
                        mode,
                        statusText: L("DiagPlatform_LinuxOutputDiagnostics_155"),
                        errorDetails: L("DiagPlatform_LinuxOutputDiagnostics_156", probeResult.Errno),
                        recommendedActions: new[]
                        {
                            L("DiagPlatform_LinuxOutputDiagnostics_157"),
                            L("DiagPlatform_LinuxOutputDiagnostics_158"),
                            L("DiagPlatform_LinuxOutputDiagnostics_159")
                        }, canRepair: LinuxDevicePreparation.CanElevate);
                }

                case UInputAccessStatus.Error:
                default:
                {
                    return DeviceDiagnosticSnapshot.Unavailable(
                        mode,
                        statusText: L("DiagPlatform_LinuxOutputDiagnostics_160", probeResult.Errno),
                        errorDetails: probeResult.Message ?? L("DiagPlatform_LinuxOutputDiagnostics_161", probeResult.Errno),
                        recommendedActions: new[]
                        {
                            L("DiagPlatform_LinuxOutputDiagnostics_162")
                        }, canRepair: LinuxDevicePreparation.CanElevate);
                }
            }
        }

        /// <summary>
        /// Inspects the availability and read permissions of physical input devices in /dev/input.
        /// </summary>
        public LinuxInputDiagnosticsSnapshot CheckInputDevices(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            if (!_probe.InputDirectoryExists())
            {
                return new LinuxInputDiagnosticsSnapshot(
                    IsAvailable: false,
                    HasReadableMouse: false,
                    HasReadableKeyboard: false,
                    TotalDeviceCount: 0,
                    ReadableDeviceCount: 0,
                    StatusText: L("DiagPlatform_LinuxOutputDiagnostics_163"),
                    ErrorDetails: L("DiagPlatform_LinuxOutputDiagnostics_164"),
                    RecommendedActions: new[]
                    {
                        L("DiagPlatform_LinuxOutputDiagnostics_165"),
                        L("DiagPlatform_LinuxOutputDiagnostics_166")
                    });
            }

            var eventPaths = _probe.EnumerateEventDevicePaths();
            if (eventPaths.Count == 0)
            {
                return new LinuxInputDiagnosticsSnapshot(
                    IsAvailable: false,
                    HasReadableMouse: false,
                    HasReadableKeyboard: false,
                    TotalDeviceCount: 0,
                    ReadableDeviceCount: 0,
                    StatusText: L("DiagPlatform_LinuxOutputDiagnostics_167"),
                    ErrorDetails: L("DiagPlatform_LinuxOutputDiagnostics_168"),
                    RecommendedActions: new[]
                    {
                        L("DiagPlatform_LinuxOutputDiagnostics_169"),
                        L("DiagPlatform_LinuxOutputDiagnostics_170")
                    });
            }

            int readableCount = 0;
            int permissionDeniedCount = 0;
            bool hasMouse = false;
            bool hasKeyboard = false;

            foreach (var path in eventPaths)
            {
                var info = _probe.ProbeEventDevice(path);
                if (info.IsReadable)
                {
                    readableCount++;
                    if (info.IsMouse) hasMouse = true;
                    if (info.IsKeyboard) hasKeyboard = true;
                }
                else if (info.Errno == 13 || info.Errno == 1)
                {
                    permissionDeniedCount++;
                }
            }

            if (readableCount == 0 && permissionDeniedCount > 0)
            {
                return new LinuxInputDiagnosticsSnapshot(
                    IsAvailable: false,
                    HasReadableMouse: false,
                    HasReadableKeyboard: false,
                    TotalDeviceCount: eventPaths.Count,
                    ReadableDeviceCount: 0,
                    StatusText: L("DiagPlatform_LinuxOutputDiagnostics_171"),
                    ErrorDetails: L("DiagPlatform_LinuxOutputDiagnostics_172", eventPaths.Count),
                    RecommendedActions: new[]
                    {
                        L("DiagPlatform_LinuxOutputDiagnostics_173"),
                        L("DiagPlatform_LinuxOutputDiagnostics_174")
                    });
            }

            if (!hasMouse)
            {
                return new LinuxInputDiagnosticsSnapshot(
                    IsAvailable: false,
                    HasReadableMouse: false,
                    HasReadableKeyboard: hasKeyboard,
                    TotalDeviceCount: eventPaths.Count,
                    ReadableDeviceCount: readableCount,
                    StatusText: L("DiagPlatform_LinuxOutputDiagnostics_175"),
                    ErrorDetails: L("DiagPlatform_LinuxOutputDiagnostics_176", readableCount),
                    RecommendedActions: new[]
                    {
                        L("DiagPlatform_LinuxOutputDiagnostics_177")
                    });
            }

            return new LinuxInputDiagnosticsSnapshot(
                IsAvailable: true,
                HasReadableMouse: true,
                HasReadableKeyboard: hasKeyboard,
                TotalDeviceCount: eventPaths.Count,
                ReadableDeviceCount: readableCount,
                StatusText: L("DiagPlatform_LinuxOutputDiagnostics_178", readableCount, eventPaths.Count));
        }
    }
}
