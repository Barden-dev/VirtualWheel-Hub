namespace SimRacingHub.Application.Runtime
{
    /// <summary>
    /// Immutable output values produced by a single input processing tick.
    /// </summary>
    public readonly struct TickOutput
    {
        public int Steer { get; }
        public int Steering => Steer;
        public int Gas { get; }
        public int Brake { get; }
        public bool ShiftDown { get; }
        public bool ShiftUp { get; }
        public bool IsControlSuspended { get; }
        public bool HubToggleTriggered { get; }
        public bool LockToggleTriggered { get; }

        public TickOutput(
            int steer,
            int gas,
            int brake,
            bool shiftDown,
            bool shiftUp,
            bool isControlSuspended,
            bool hubToggleTriggered = false,
            bool lockToggleTriggered = false)
        {
            Steer = steer;
            Gas = gas;
            Brake = brake;
            ShiftDown = shiftDown;
            ShiftUp = shiftUp;
            IsControlSuspended = isControlSuspended;
            HubToggleTriggered = hubToggleTriggered;
            LockToggleTriggered = lockToggleTriggered;
        }
    }
}
