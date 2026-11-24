namespace SimRacingHub.Application.Features.KeyBindings
{
    /// <summary>
    /// Identifiers for control actions supported by the key binding system.
    /// </summary>
    public enum KeyActionId
    {
        SteerLeft,
        SteerRight,
        Throttle,
        Brake,
        Clutch = 4,
        ShiftUp = 5,
        ShiftDown,
        CenterSteering,
        ToggleHub,
        ToggleLock,
        UnlockCursor
    }
}
