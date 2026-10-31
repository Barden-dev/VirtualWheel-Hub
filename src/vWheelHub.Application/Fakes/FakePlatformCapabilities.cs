using SimRacingHub.Application.Contracts;

namespace SimRacingHub.Application.Fakes
{
    public sealed class FakePlatformCapabilities : IPlatformCapabilities
    {
        public bool SupportsVJoy { get; set; } = true;
        public bool SupportsViGEm { get; set; } = true;
        public bool SupportsUInput { get; set; } = false;
        public bool SupportsWindowsDriverTools { get; set; } = true;
        public bool SupportsGameInstallers { get; set; } = true;

        public static FakePlatformCapabilities Windows() => new()
        {
            SupportsVJoy = true,
            SupportsViGEm = true,
            SupportsUInput = false,
            SupportsWindowsDriverTools = true,
            SupportsGameInstallers = true
        };
        public static FakePlatformCapabilities Linux() => new()
        {
            SupportsVJoy = false,
            SupportsViGEm = false,
            SupportsUInput = true,
            SupportsWindowsDriverTools = false,
            SupportsGameInstallers = false
        };
    }
}
