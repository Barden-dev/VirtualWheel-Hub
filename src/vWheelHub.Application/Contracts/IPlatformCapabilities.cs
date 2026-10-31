namespace SimRacingHub.Application.Contracts
{
    public interface IPlatformCapabilities
    {
        bool SupportsVJoy { get; }

        bool SupportsViGEm { get; }

        bool SupportsUInput { get; }

        bool SupportsWindowsDriverTools { get; }

        bool SupportsGameInstallers { get; }
    }
}
