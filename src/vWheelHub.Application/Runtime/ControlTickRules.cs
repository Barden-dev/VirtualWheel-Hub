using SimRacingHub.Core;

namespace SimRacingHub.Application.Runtime;

/// <summary>Shared input and control operations; callers own scheduling, delta consumption and suspension policy.</summary>
internal static class ControlTickRules
{
    internal static bool EitherDown(IInputSource input, int first, int second) =>
        (first != 0 && input.IsKeyDown(first)) || (second != 0 && input.IsKeyDown(second));

    internal static bool BothDown(IInputSource input, int first, int second) =>
        (first != 0 && input.IsKeyDown(first)) && (second != 0 && input.IsKeyDown(second));

    internal static bool IsMenuSuspended(bool pauseInMenu, bool supportsMenuControls, bool isDriving) =>
        supportsMenuControls && pauseInMenu && !isDriving;

}
