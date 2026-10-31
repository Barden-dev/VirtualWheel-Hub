using System.Collections.Generic;
using SimRacingHub.Application.Contracts;
using SimRacingHub.Models;

namespace SimRacingHub.Linux
{
    public sealed class LinuxPlatformCapabilities : IPlatformCapabilities
    {
        public static LinuxPlatformCapabilities Instance { get; } = new();
        public bool SupportsVJoy => false;
        public bool SupportsViGEm => false;
        public bool SupportsUInput => true;
        public bool SupportsWindowsDriverTools => false;
        public bool SupportsGameInstallers => false;
        public IReadOnlyList<OutputDeviceMode> SupportedOutputModes { get; } = new[]
        {
            OutputDeviceMode.VJoy,
            OutputDeviceMode.Xbox360
        };
        public bool SupportsProcessDetection => true;
        public bool SupportsProtonBridge => true;
    }
}
