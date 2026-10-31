namespace SimRacingHub.Application.Runtime;

/// <summary>Cursor commands available to desktop coordinators independently of the runtime implementation.</summary>
public interface IRuntimeCursorControl
{
    bool IsLocked { get; }
    void UpdateMouseLock(bool lockMouse);
}
