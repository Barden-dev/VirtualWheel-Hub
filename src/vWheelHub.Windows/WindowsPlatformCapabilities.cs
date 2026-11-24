using SimRacingHub.Application.Contracts;

namespace SimRacingHub.Windows
{
    /// <summary>
    /// Windows host platform capabilities implementation of IPlatformCapabilities.
    /// </summary>
    public sealed partial class WindowsPlatformCapabilities : IPlatformCapabilities
    {
        public bool SupportsVJoy => true;
        public bool SupportsViGEm => false;
        public bool SupportsUInput => false;
        public bool SupportsWindowsDriverTools => true;
    }
}
