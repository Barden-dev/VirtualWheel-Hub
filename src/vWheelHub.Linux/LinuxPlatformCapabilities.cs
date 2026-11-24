using System.Collections.Generic;
using SimRacingHub.Application.Contracts;
using SimRacingHub.Models;

namespace SimRacingHub.Linux
{
    /// <summary>
    /// Linux host platform capabilities describing supported output modes and PAL hardware backends.
    /// </summary>
    public sealed partial class LinuxPlatformCapabilities : IPlatformCapabilities
    {
        public static LinuxPlatformCapabilities Instance { get; } = new();

        public bool SupportsVJoy => false;
        public bool SupportsViGEm => false;
        public bool SupportsUInput => true;
        public bool SupportsWindowsDriverTools => false;

        public IReadOnlyList<OutputDeviceMode> SupportedOutputModes { get; } = new[]
        {
            OutputDeviceMode.VJoy,
            OutputDeviceMode.Xbox360
        };

        public bool SupportsProcessDetection => true;
    }
}
