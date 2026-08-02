using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SimRacingHub.Core
{
    public class VJoyRequirements
    {
        public uint DeviceId { get; set; } = 1;
        
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
        public string DeviceStatusText { get; set; } = "Missing";
        public bool HasRequiredAxes { get; set; }
        public List<string> MissingAxes { get; set; } = new();
        public bool HasRequiredButtons { get; set; }
        public int ConfiguredButtonCount { get; set; }
        public int MinRequiredButtons { get; set; } = 2;
        public bool SystemDllSynced { get; set; }

        public bool IsValid => IsDllPresent && 
                               IsDriverInstalled && 
                               (DeviceStatusRaw == VJoyWrapper.VJD_STAT_FREE || DeviceStatusRaw == VJoyWrapper.VJD_STAT_OWN) && 
                               HasRequiredAxes && 
                               HasRequiredButtons;

        public bool IsNotValid => !IsValid;

        public bool IsDriverMissing => !IsDllPresent || !IsDriverInstalled;

        public bool CanConfigureDevice => IsDriverInstalled && IsDllPresent && IsNotValid;

        public string SummaryMessage { get; set; } = string.Empty;
        public string ActionableAdvice { get; set; } = string.Empty;
    }

    public class VJoyDiagnosticService
    {
        public VJoyDiagnosticResult PerformDiagnostic(VJoyRequirements? requirements = null)
        {
            requirements ??= new VJoyRequirements();
            var result = new VJoyDiagnosticResult
            {
                TargetDeviceId = requirements.DeviceId,
                MinRequiredButtons = requirements.MinRequiredButtons
            };

            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string localDllPath = Path.Combine(appDir, "vJoyInterface.dll");

            // 1. Smart System Sync for vJoyInterface.dll
            TrySyncSystemDll(localDllPath, result);

            result.IsDllPresent = File.Exists(localDllPath);
            if (!result.IsDllPresent)
            {
                result.SummaryMessage = "vJoyInterface.dll library was not found";
                result.ActionableAdvice = "Click 'Get vJoy' to download and install the driver first. After installation, copy vJoyInterface.dll into the application folder if needed.";
                return result;
            }

            // 2. Check if Driver is enabled in Windows
            result.IsDriverInstalled = VJoyWrapper.IsDriverEnabled();
            if (!result.IsDriverInstalled)
            {
                result.SummaryMessage = "vJoy driver is not installed or disabled in Windows";
                result.ActionableAdvice = "Click 'Get vJoy' to download and install the driver, and ensure the device is enabled in Windows Device Manager.";
                return result;
            }

            result.DriverVersion = VJoyWrapper.GetDriverVersion();

            // 3. Check vJoy Device Status
            result.DeviceStatusRaw = VJoyWrapper.GetDeviceStatus(requirements.DeviceId);
            switch (result.DeviceStatusRaw)
            {
                case VJoyWrapper.VJD_STAT_FREE:
                    result.DeviceStatusText = "Free";
                    break;
                case VJoyWrapper.VJD_STAT_OWN:
                    result.DeviceStatusText = "Owned";
                    break;
                case VJoyWrapper.VJD_STAT_BUSY:
                    result.DeviceStatusText = "Busy";
                    result.SummaryMessage = $"vJoy Device #{requirements.DeviceId} is currently used by another application";
                    result.ActionableAdvice = $"Close other applications (e.g. UCR, vJoyFeeder) using vJoy Device #{requirements.DeviceId}.";
                    return result;
                case VJoyWrapper.VJD_STAT_MISS:
                default:
                    result.DeviceStatusText = "Missing";
                    result.SummaryMessage = $"vJoy Device #{requirements.DeviceId} is not created";
                    result.ActionableAdvice = $"Run vJoyConfig (vJoyConf.exe), add Device #{requirements.DeviceId}, and click Apply.";
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

            // 6. Formulate final status message and advice
            if (!result.HasRequiredAxes && !result.HasRequiredButtons)
            {
                string missingStr = string.Join(", ", result.MissingAxes);
                result.SummaryMessage = $"vJoy Device #{requirements.DeviceId} is missing axes ({missingStr}) and needs buttons (min {requirements.MinRequiredButtons}, currently {result.ConfiguredButtonCount})";
                result.ActionableAdvice = $"Open vJoyConfig (vJoyConf.exe), enable axes (X, Y, Z) and set Number of Buttons to 8 for Device #{requirements.DeviceId}, then click Apply.";
            }
            else if (!result.HasRequiredAxes)
            {
                string missingStr = string.Join(", ", result.MissingAxes);
                result.SummaryMessage = $"vJoy Device #{requirements.DeviceId} is missing required axes: {missingStr}";
                result.ActionableAdvice = $"Open vJoyConfig (vJoyConf.exe), enable axes ({missingStr}) for Device #{requirements.DeviceId}, and click Apply.";
            }
            else if (!result.HasRequiredButtons)
            {
                result.SummaryMessage = $"vJoy Device #{requirements.DeviceId} does not have enough buttons (minimum {requirements.MinRequiredButtons} required, currently {result.ConfiguredButtonCount})";
                result.ActionableAdvice = $"Open vJoyConfig (vJoyConf.exe), set Number of Buttons to 8 for Device #{requirements.DeviceId}, and click Apply.";
            }
            else
            {
                result.SummaryMessage = $"vJoy Device #{requirements.DeviceId} is configured and ready ({result.ConfiguredButtonCount} buttons, axes: X, Y, Z)";
                result.ActionableAdvice = "Everything is configured properly.";
            }

            return result;
        }

        private void TrySyncSystemDll(string localDllPath, VJoyDiagnosticResult result)
        {
            try
            {
                string[] potentialSystemPaths = new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "vJoy", "x64", "vJoyInterface.dll"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "vJoy", "x64", "vJoyInterface.dll"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "vJoyInterface.dll")
                };

                foreach (var sysPath in potentialSystemPaths)
                {
                    if (File.Exists(sysPath))
                    {
                        var sysInfo = new FileInfo(sysPath);
                        bool needCopy = false;

                        if (!File.Exists(localDllPath))
                        {
                            needCopy = true;
                        }
                        else
                        {
                            var localInfo = new FileInfo(localDllPath);
                            if (sysInfo.Length != localInfo.Length || sysInfo.LastWriteTimeUtc > localInfo.LastWriteTimeUtc)
                            {
                                needCopy = true;
                            }
                        }

                        if (needCopy)
                        {
                            File.Copy(sysPath, localDllPath, overwrite: true);
                            result.SystemDllSynced = true;
                            AppLogger.Instance.LogInfo($"Synced system vJoyInterface.dll from '{sysPath}' to local folder.");
                        }
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError("Failed to sync system vJoyInterface.dll", ex);
            }
        }

        private string GetAxisName(uint axisUsage)
        {
            return axisUsage switch
            {
                VJoyWrapper.HID_USAGE_X => "X (Steer)",
                VJoyWrapper.HID_USAGE_Y => "Y (Gas)",
                VJoyWrapper.HID_USAGE_Z => "Z (Brake)",
                VJoyWrapper.HID_USAGE_RX => "Rx",
                VJoyWrapper.HID_USAGE_RY => "Ry",
                VJoyWrapper.HID_USAGE_RZ => "Rz (Clutch)",
                VJoyWrapper.HID_USAGE_SL0 => "Slider0 (Handbrake)",
                VJoyWrapper.HID_USAGE_SL1 => "Slider1",
                _ => $"Axis 0x{axisUsage:X}"
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

                string exePath = potentialPaths.FirstOrDefault(File.Exists);
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

                System.Diagnostics.Process.Start(psi);
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError("Failed to launch vJoyConf.exe", ex);
                return false;
            }
        }

        public static (bool Success, string Message) TryAutoConfigureVJoy(uint deviceId = 1)
        {
            try
            {
                string[] potentialPaths = new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "vJoy", "x64", "vJoyConfig.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "vJoy", "vJoyConfig.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "vJoy", "x64", "vJoyConfig.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "vJoy", "vJoyConfig.exe")
                };

                string exePath = potentialPaths.FirstOrDefault(File.Exists);
                if (exePath == null)
                {
                    exePath = "vJoyConfig.exe";
                }

                // Command syntax: vJoyConfig.exe 1 -f -a x y z -b 8
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = $"{deviceId} -f -a x y z -b 8",
                    UseShellExecute = true,
                    Verb = "runas" // Triggers Windows UAC prompt for Admin rights
                };

                var process = System.Diagnostics.Process.Start(psi);
                if (process != null)
                {
                    process.WaitForExit(10000); // wait up to 10 seconds
                    return (true, $"vJoy Device #{deviceId} configured successfully with X, Y, Z axes & 8 buttons.");
                }
                return (false, "Could not start vJoyConfig.exe process.");
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError("Failed to auto-configure vJoy via CLI", ex);
                return (false, ex.Message);
            }
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

                string folderPath = potentialFolderPaths.FirstOrDefault(Directory.Exists);
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
