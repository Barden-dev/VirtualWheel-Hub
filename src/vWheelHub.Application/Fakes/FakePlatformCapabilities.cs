using SimRacingHub.Application.Contracts;

namespace SimRacingHub.Application.Fakes
{
    /// <summary>
    /// Test and preview fake for IPlatformCapabilities with configurable feature flags and platform presets.
    /// </summary>
    public sealed partial class FakePlatformCapabilities : IPlatformCapabilities
    {
        public bool SupportsVJoy { get; set; } = true;
        public bool SupportsViGEm { get; set; } = true;
        public bool SupportsUInput { get; set; } = false;
        public bool SupportsWindowsDriverTools { get; set; } = true;

        public static FakePlatformCapabilities Windows()
        {
            var caps = new FakePlatformCapabilities
            {
                SupportsVJoy = true,
                SupportsViGEm = false,
                SupportsUInput = false,
                SupportsWindowsDriverTools = true
            };
            ConfigureWindowsExtras(caps);
            return caps;
        }

        public static FakePlatformCapabilities Linux()
        {
            var caps = new FakePlatformCapabilities
            {
                SupportsVJoy = false,
                SupportsViGEm = false,
                SupportsUInput = true,
                SupportsWindowsDriverTools = false
            };
            ConfigureLinuxExtras(caps);
            return caps;
        }

        static partial void ConfigureWindowsExtras(FakePlatformCapabilities caps);
        static partial void ConfigureLinuxExtras(FakePlatformCapabilities caps);
    }
}
