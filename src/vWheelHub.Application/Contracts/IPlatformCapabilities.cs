namespace SimRacingHub.Application.Contracts
{
    /// <summary>
    /// Immutable capabilities of the host platform and operating system environment.
    /// Distinguishes between OS capabilities (e.g. Windows driver tools vs Linux uinput).
    /// </summary>
    public partial interface IPlatformCapabilities
    {
        /// <summary>
        /// Indicates whether vJoy virtual joystick device is supported on this platform.
        /// </summary>
        bool SupportsVJoy { get; }

        /// <summary>
        /// Indicates whether ViGEm virtual Xbox 360 controller is supported on this platform.
        /// </summary>
        bool SupportsViGEm { get; }

        /// <summary>
        /// Indicates whether Linux /dev/uinput virtual device is supported on this platform.
        /// </summary>
        bool SupportsUInput { get; }

        /// <summary>
        /// Indicates whether Windows driver installer tools (e.g. vJoy/ViGEm installers) are executable.
        /// </summary>
        bool SupportsWindowsDriverTools { get; }
    }
}
