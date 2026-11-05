using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SimRacingHub.Core
{
    public class VJoyRequirements
    {
        public uint DeviceId { get; set; } = 1;
        public bool WheelCompatibility { get; set; }

        public static VJoyRequirements ForProfile(int deviceId, bool wheelCompatibility) => new()
        {
            DeviceId = (uint)Math.Clamp(deviceId, 1, 16),
            WheelCompatibility = wheelCompatibility,
            MinRequiredButtons = wheelCompatibility ? 128 : 2,
            RequiredAxes = wheelCompatibility
                ? Enumerable.Range(0x30, 8).Select(value => (uint)value).ToList()
                : new() { VJoyWrapper.HID_USAGE_X, VJoyWrapper.HID_USAGE_Y, VJoyWrapper.HID_USAGE_Z }
        };
        
        // Required HID axis usages
        public List<uint> RequiredAxes { get; set; } = new()
        {
            VJoyWrapper.HID_USAGE_X, // Steer
            VJoyWrapper.HID_USAGE_Y, // Gas
            VJoyWrapper.HID_USAGE_Z  // Brake
        };

        public int MinRequiredButtons { get; set; } = 2; // ShiftDown & ShiftUp
    }

    public class VJoyDiagnosticResult
    {
        public bool IsDllPresent { get; set; }
        public bool IsDriverInstalled { get; set; }
        public short DriverVersion { get; set; }
        public uint TargetDeviceId { get; set; } = 1;
        public int DeviceStatusRaw { get; set; } = VJoyWrapper.VJD_STAT_MISS;
        public string DeviceStatusText { get; set; } = string.Empty;
        public bool HasRequiredAxes { get; set; }
        public List<string> MissingAxes { get; set; } = new();
        public bool HasRequiredButtons { get; set; }
        public bool HasRequiredWheelCapabilities { get; set; } = true;
        public int ConfiguredButtonCount { get; set; }
        public int MinRequiredButtons { get; set; } = 2;
        public bool SystemDllSynced { get; set; }
        public bool CanSyncDll { get; set; }
        public string? SystemDllPath { get; set; }
        // null means the persisted configuration could not be inspected safely.
        public bool? ConfigurationExists { get; set; }

        public string DllSyncAdvice { get; set; } = string.Empty;

        public bool IsValid => IsDllPresent && 
                               IsDriverInstalled && 
                               (DeviceStatusRaw == VJoyWrapper.VJD_STAT_FREE || DeviceStatusRaw == VJoyWrapper.VJD_STAT_OWN) && 
                               HasRequiredAxes && 
                               HasRequiredButtons && HasRequiredWheelCapabilities;

        public bool IsNotValid => !IsValid;

        public bool IsDriverMissing => !IsDllPresent || !IsDriverInstalled;

        public bool CanConfigureDevice => IsDriverInstalled && IsDllPresent && IsNotValid &&
            VJoyConfigurationSafety.CanCreate(DeviceStatusRaw, ConfigurationExists);

        public string SummaryMessage { get; set; } = string.Empty;
        public string ActionableAdvice { get; set; } = string.Empty;
    }

    public class VJoyDiagnosticService
    {
        private readonly SimRacingHub.Application.Localization.ILocalizationManager _localization;
        private string L(string key, params object?[] args) => string.Format(System.Globalization.CultureInfo.GetCultureInfo(_localization.CurrentCultureCode), _localization.GetString(key), args);
        public VJoyDiagnosticService(SimRacingHub.Application.Localization.ILocalizationManager? localization = null) => _localization = localization ?? new SimRacingHub.Application.Localization.LocalizationManager();

        public VJoyDiagnosticResult PerformDiagnostic(VJoyRequirements? requirements = null)
        {
            requirements ??= new VJoyRequirements();

            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string localDllPath = Path.Combine(appDir, "vJoyInterface.dll");

            // 1. Check for system vJoyInterface.dll without silent copying
            string dllAdvice = CheckSystemDll(localDllPath, out string? sysPath);

            var result = PerformDiagnosticCore(requirements, localDllPath);
            result.CanSyncDll = sysPath != null && !result.IsDllPresent;
            result.SystemDllPath = sysPath;
            result.DllSyncAdvice = dllAdvice;

            if (!string.IsNullOrEmpty(dllAdvice))
            {
                result.ActionableAdvice = string.IsNullOrEmpty(result.ActionableAdvice)
                    ? dllAdvice
                    : result.ActionableAdvice + " " + dllAdvice;
            }

            return result;
        }

        private VJoyDiagnosticResult PerformDiagnosticCore(VJoyRequirements requirements, string localDllPath)
        {
            var result = new VJoyDiagnosticResult
            {
                DeviceStatusText = L("DiagPlatform_VJoyDiagnosticService_92"),
                TargetDeviceId = requirements.DeviceId,
                MinRequiredButtons = requirements.MinRequiredButtons
            };

            result.IsDllPresent = File.Exists(localDllPath);
            if (!result.IsDllPresent)
            {
                result.SummaryMessage = L("DiagPlatform_VJoyDiagnosticService_93");
                result.ActionableAdvice = L("DiagPlatform_VJoyDiagnosticService_94");
                return result;
            }

            // 2. Check if Driver is enabled in Windows
            result.IsDriverInstalled = VJoyWrapper.IsDriverEnabled();
            if (!result.IsDriverInstalled)
            {
                result.SummaryMessage = L("DiagPlatform_VJoyDiagnosticService_95");
                result.ActionableAdvice = L("DiagPlatform_VJoyDiagnosticService_96");
                return result;
            }

            result.DriverVersion = VJoyWrapper.GetDriverVersion();
            result.ConfigurationExists = VJoyConfigurationSafety.ReadConfigurationExists(requirements.DeviceId);

            // 3. Check vJoy Device Status
            result.DeviceStatusRaw = VJoyWrapper.GetDeviceStatus(requirements.DeviceId);
            switch (result.DeviceStatusRaw)
            {
                case VJoyWrapper.VJD_STAT_FREE:
                    result.DeviceStatusText = L("DiagPlatform_VJoyDiagnosticService_97");
                    break;
                case VJoyWrapper.VJD_STAT_OWN:
                    result.DeviceStatusText = L("DiagPlatform_VJoyDiagnosticService_98");
                    break;
                case VJoyWrapper.VJD_STAT_BUSY:
                    result.DeviceStatusText = L("DiagPlatform_VJoyDiagnosticService_99");
                    result.SummaryMessage = L("DiagPlatform_VJoyDiagnosticService_100", requirements.DeviceId);
                    result.ActionableAdvice = L("DiagPlatform_VJoyDiagnosticService_101", requirements.DeviceId);
                    return result;
                case VJoyWrapper.VJD_STAT_MISS:
                    result.DeviceStatusText = L("DiagPlatform_VJoyDiagnosticService_102");
                    result.SummaryMessage = L("DiagPlatform_VJoyDiagnosticService_103", requirements.DeviceId);
                    result.ActionableAdvice = result.CanConfigureDevice
                        ? L("DiagPlatform_VJoyDiagnosticService_104", requirements.DeviceId)
                        : L(result.ConfigurationExists == true ? "Output_VJoyExistingProtected" : "Output_VJoyConfigurationUnknown", requirements.DeviceId);
                    return result;
                default:
                    result.DeviceStatusText = VJoyWrapper.DescribeStatus(result.DeviceStatusRaw);
                    result.SummaryMessage = L("Output_VJoyConfigurationUnknown", requirements.DeviceId);
                    result.ActionableAdvice = result.SummaryMessage;
                    return result;
            }

            // 4. Validate Required Axes (Extensible for Steering X, Gas Y, Brake Z, Clutch RZ, Handbrake SL0)
            result.MissingAxes = new List<string>();
            foreach (uint axis in requirements.RequiredAxes)
            {
                bool exists = VJoyWrapper.CheckAxisExists(requirements.DeviceId, axis);
                if (!exists)
                {
                    result.MissingAxes.Add(GetAxisName(axis));
                }
            }

            result.HasRequiredAxes = result.MissingAxes.Count == 0;

            // 5. Validate Button Count
            result.ConfiguredButtonCount = VJoyWrapper.GetButtonCount(requirements.DeviceId);
            result.HasRequiredButtons = result.ConfiguredButtonCount >= requirements.MinRequiredButtons;
            result.HasRequiredWheelCapabilities = !requirements.WheelCompatibility || VJoyWrapper.HasWheelCapabilities(requirements.DeviceId);

            // 6. Formulate final status message and advice
            if (!result.HasRequiredAxes && !result.HasRequiredButtons)
            {
                string missingStr = string.Join(", ", result.MissingAxes);
                result.SummaryMessage = L("DiagPlatform_VJoyDiagnosticService_105", requirements.DeviceId, missingStr, requirements.MinRequiredButtons, result.ConfiguredButtonCount);
                result.ActionableAdvice = L("DiagPlatform_VJoyDiagnosticService_106", requirements.DeviceId);
            }
            else if (!result.HasRequiredAxes)
            {
                string missingStr = string.Join(", ", result.MissingAxes);
                result.SummaryMessage = L("DiagPlatform_VJoyDiagnosticService_107", requirements.DeviceId, missingStr);
                result.ActionableAdvice = L("DiagPlatform_VJoyDiagnosticService_108", missingStr, requirements.DeviceId);
            }
            else if (!result.HasRequiredButtons)
            {
                result.SummaryMessage = L("DiagPlatform_VJoyDiagnosticService_109", requirements.DeviceId, requirements.MinRequiredButtons, result.ConfiguredButtonCount);
                result.ActionableAdvice = L("DiagPlatform_VJoyDiagnosticService_110", requirements.DeviceId);
            }
            else if (!result.HasRequiredWheelCapabilities)
            {
                result.SummaryMessage = L("Output_WheelIncomplete", requirements.DeviceId);
                result.ActionableAdvice = L("Output_WheelPrepareHint");
            }
            else
            {
                result.SummaryMessage = L("DiagPlatform_VJoyDiagnosticService_111", requirements.DeviceId, result.ConfiguredButtonCount);
                result.ActionableAdvice = L("DiagPlatform_VJoyDiagnosticService_112");
            }

            return result;
        }

        private string CheckSystemDll(string localDllPath, out string? availableSysPath)
        {
            availableSysPath = null;
            try
            {
                string[] potentialSystemPaths = new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "vJoy", "x64", "vJoyInterface.dll"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "vJoy", "x64", "vJoyInterface.dll"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "vJoyInterface.dll")
                };

                string? sysPath = potentialSystemPaths.FirstOrDefault(File.Exists);
                if (sysPath == null) return string.Empty;

                if (!File.Exists(localDllPath))
                {
                    availableSysPath = sysPath;
                    return L("DiagPlatform_VJoyDiagnosticService_113", L("DiagPlatform_VJoyDiagnosticService_93"), L("DiagPlatform_VJoyDiagnosticService_97"), sysPath);
                }

                string localVersion = DescribeDllVersion(localDllPath);
                string systemVersion = DescribeDllVersion(sysPath);
                if (string.Equals(localVersion, systemVersion, StringComparison.OrdinalIgnoreCase)) return string.Empty;

                string advice = L("DiagPlatform_VJoyDiagnosticService_113", localVersion, systemVersion, sysPath);
                AppLogger.Instance.LogWarning(advice);
                return advice;
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogWarning($"Could not compare vJoyInterface.dll with the system copy: {ex.Message}");
                return string.Empty;
            }
        }

        public static (bool Success, string Message) SyncSystemDll(string? localDllPath = null, SimRacingHub.Application.Localization.ILocalizationManager? localization = null)
        {
            var language = localization ?? new SimRacingHub.Application.Localization.LocalizationManager();
            string L(string key, params object?[] args) => string.Format(System.Globalization.CultureInfo.GetCultureInfo(language.CurrentCultureCode), language.GetString(key), args);

            try
            {
                string target = localDllPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "vJoyInterface.dll");
                string[] potentialSystemPaths = new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "vJoy", "x64", "vJoyInterface.dll"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "vJoy", "x64", "vJoyInterface.dll"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "vJoyInterface.dll")
                };

                string? sysPath = potentialSystemPaths.FirstOrDefault(File.Exists);
                if (sysPath == null) return (false, L("DiagPlatform_VJoyDiagnosticService_94"));

                if (Directory.Exists(target)) target = Path.Combine(target, "vJoyInterface.dll");
                byte[] sourceBytes = File.ReadAllBytes(sysPath);
                string temporary = target + ".vwh-sync-" + Guid.NewGuid().ToString("N");
                try
                {
                    File.WriteAllBytes(temporary, sourceBytes);
                    if (!System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(temporary)).SequenceEqual(System.Security.Cryptography.SHA256.HashData(sourceBytes)))
                        throw new IOException("vJoy DLL hash verification failed.");
                    File.Move(temporary, target, overwrite: true);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
                AppLogger.Instance.LogInfo($"Copied vJoyInterface.dll from '{sysPath}' to '{target}'.");
                return (true, L("DiagPlatform_VJoyDiagnosticService_111", 1, 0));
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError("Failed to copy vJoyInterface.dll", ex);
                return (false, ex.Message);
            }
        }

        private string DescribeDllVersion(string path)
        {
            try
            {
                string? version = System.Diagnostics.FileVersionInfo.GetVersionInfo(path).FileVersion;
                if (!string.IsNullOrWhiteSpace(version)) return $"v{version}";

                return L("DiagPlatform_VJoyBytesFormat", new FileInfo(path).Length);
            }
            catch
            {
                return L("DiagPlatform_VJoyDiagnosticService_114");
            }
        }

        private string GetAxisName(uint axisUsage)
        {
            return axisUsage switch
            {
                VJoyWrapper.HID_USAGE_X => L("DiagPlatform_VJoyDiagnosticService_115"),
                VJoyWrapper.HID_USAGE_Y => L("DiagPlatform_VJoyDiagnosticService_116"),
                VJoyWrapper.HID_USAGE_Z => L("DiagPlatform_VJoyDiagnosticService_117"),
                VJoyWrapper.HID_USAGE_RX => "Rx",
                VJoyWrapper.HID_USAGE_RY => "Ry",
                VJoyWrapper.HID_USAGE_RZ => L("DiagPlatform_VJoyDiagnosticService_120"),
                VJoyWrapper.HID_USAGE_SL0 => L("DiagPlatform_VJoyHandbrakeAxis"),
                VJoyWrapper.HID_USAGE_SL1 => "Slider1",
                _ => L("DiagPlatform_VJoyUnknownAxisFormat", axisUsage)
            };
        }
        public static bool TryLaunchVJoyConfig()
        {
            try
            {
                string[] potentialPaths = new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "vJoy", "x64", "vJoyConf.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "vJoy", "vJoyConf.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "vJoy", "x64", "vJoyConf.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "vJoy", "vJoyConf.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "vJoy", "x64", "vJoyConfig.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "vJoy", "vJoyConfig.exe")
                };

                string? exePath = potentialPaths.FirstOrDefault(File.Exists);
                if (exePath == null)
                {
                    exePath = "vJoyConf.exe";
                }

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true,
                    Verb = "runas" // Triggers Windows UAC prompt for Admin rights
                };

                System.Diagnostics.Process.Start(psi)?.Dispose();
                return true;
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                AppLogger.Instance.LogWarning("Administrator permission was declined, so vJoyConf.exe was not started.");
                return false;
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError("Failed to launch vJoyConf.exe", ex);
                return false;
            }
        }

        internal static string ConfigurationArguments(uint deviceId, bool wheelCompatibility)
        {
            if (deviceId is < 1 or > 16) throw new ArgumentOutOfRangeException(nameof(deviceId));
            return wheelCompatibility
                ? $"{deviceId} -a X Y Z Rx Ry Rz Sl0 Sl1 -b 128 -s 1 -e all"
                : $"{deviceId} -a x y z -b 8";
        }

        [Obsolete("Use the explicit vJoy preview and apply workflow.")]
        public static System.Threading.Tasks.Task<(bool Success, string Message)> TryAutoConfigureVJoy(uint deviceId = 1, SimRacingHub.Application.Localization.ILocalizationManager? localization = null, bool wheelCompatibility = false)
        {
            _ = ConfigurationArguments(deviceId, wheelCompatibility);
            var language = localization ?? new SimRacingHub.Application.Localization.LocalizationManager();
            return System.Threading.Tasks.Task.FromResult((false, language.GetString("Output_VJoyUseSetupCard")));
        }

        public static bool TryOpenVJoyFolder()
        {
            try
            {
                string[] potentialFolderPaths = new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "vJoy", "x64"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "vJoy"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "vJoy", "x64"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "vJoy")
                };

                string? folderPath = potentialFolderPaths.FirstOrDefault(Directory.Exists);
                if (folderPath != null)
                {
                    System.Diagnostics.Process.Start("explorer.exe", folderPath);
                    return true;
                }
                
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                System.Diagnostics.Process.Start("explorer.exe", programFiles);
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError("Failed to open vJoy folder", ex);
                return false;
            }
        }
    }
}
