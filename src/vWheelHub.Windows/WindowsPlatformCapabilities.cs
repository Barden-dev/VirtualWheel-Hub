using SimRacingHub.Application.Contracts;

namespace SimRacingHub.Windows
{
    public sealed class WindowsPlatformCapabilities : IPlatformCapabilities
    {
        public bool SupportsVJoy => true;
        public bool SupportsViGEm => false;
        public bool SupportsUInput => false;
        public bool SupportsWindowsDriverTools => true;
        public bool SupportsGameInstallers => true;
    }
}
