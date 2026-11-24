namespace SimRacingHub.Application.Runtime
{
    /// <summary>
    /// Immutable point-in-time snapshot of the runtime session and controller outputs.
    /// Published to UI consumers at 30-60 Hz without locking the 1000 Hz input loop.
    /// </summary>
    public sealed partial record RuntimeSnapshot(
        bool IsRunning,
        bool IsLocked,
        double WheelAxis,
        double GasAxis,
        double BrakeAxis,
        string? GameTitle,
        string? StatusMessage,
        bool HasFault = false,
        string? FaultMessage = null,
        double ClutchAxis = 0.0)
    {
        /// <summary>Normalized axis state before gamma and output mapping, for live response curves.</summary>
        public RuntimeInputSnapshot? Inputs { get; init; }
        /// <summary>Focus/release gates still prevent physical controls from reaching the output.</summary>
        public bool ControlsSuspended { get; init; }
        /// <summary>Current control activity / suspension reason for status bar presentation.</summary>
        public RuntimeControlState ControlState { get; init; } = IsRunning ? RuntimeControlState.Active : RuntimeControlState.Stopped;
        /// <summary>Default stopped state snapshot.</summary>
        public static RuntimeSnapshot Stopped() => new(false, false, 0, 0, 0, null, "Stopped");
    }

    public enum RuntimeControlState
    {
        Stopped,
        Active,
        WaitingForGame,
        WindowUnfocused,
        CursorUnlockedPaused,
        SteeringPausedCursorUnlocked,
        CursorUnlockHeld
    }

    public enum RuntimeStatusTone
    {
        Neutral,
        Success,
        Warning,
        Error
    }

    public sealed record RuntimeInputSnapshot(double Wheel, double Gas, double Brake, double Clutch = 0);
}
