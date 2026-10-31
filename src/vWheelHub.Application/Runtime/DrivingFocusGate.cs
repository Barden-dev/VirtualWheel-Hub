using SimRacingHub.Core;
using SimRacingHub.Models;

namespace SimRacingHub.Application.Runtime;

/// <summary>Known focus loss neutralizes driving until focus and released inputs are observed.</summary>
public sealed class DrivingFocusGate
{
    private bool _awaitRelease;
    public bool IsSuspended { get; private set; }
    public void Reset() { _awaitRelease = false; IsSuspended = false; }
    public bool Update(bool? focused, ResolvedProfile profile, IInputSource input, bool enabled = true)
    {
        // A disabled policy must not retain a previously armed release latch.
        if (!enabled) { Reset(); return false; }
        if (focused == false) { _awaitRelease = true; return IsSuspended = true; }
        if (focused == null && IsSuspended) return true;
        if (_awaitRelease)
        {
            foreach (int key in profile.GetReservedVirtualButtonKeys())
                if (key != 0 && input.IsKeyDown(key)) return IsSuspended = true;
            _awaitRelease = false;
        }
        return IsSuspended = false;
    }
}
