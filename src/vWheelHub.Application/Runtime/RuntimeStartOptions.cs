using SimRacingHub.Models;

namespace SimRacingHub.Application.Runtime
{
    /// <summary>
    /// Configuration options supplied when starting the 1000 Hz hub runtime session.
    /// </summary>
    public sealed record RuntimeStartOptions(
        OutputDeviceMode OutputMode = OutputDeviceMode.VJoy,
        bool RetainOutputConnection = true,
        bool LockCursorOnStart = false)
    {
        /// <summary>Only explicit user starts may prepare or elevate the output. Desktop auto-start uses an existing output.</summary>
        public bool AllowOutputSetup { get; init; } = true;
        public bool SuppressDrivingOnStart { get; init; }
        public ResolvedProfile? Profile { get; init; }
        public ProfileContext? Context { get; init; }
    }
}
