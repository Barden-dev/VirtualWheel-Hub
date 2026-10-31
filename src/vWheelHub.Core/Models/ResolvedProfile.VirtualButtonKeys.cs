namespace SimRacingHub.Models;

public partial class ResolvedProfile
{
    public int[] GetReservedVirtualButtonKeys() =>
    [
        KeySteerLeft1, KeySteerLeft2, KeySteerRight1, KeySteerRight2, KeyGas1, KeyGas2, KeyBrake1, KeyBrake2,
        KeyShiftUp1, KeyShiftUp2, KeyShiftDown1, KeyShiftDown2, KeyCenterSteering1, KeyCenterSteering2,
        KeyToggleHub1, KeyToggleHub2, KeyToggleLock1, KeyToggleLock2, KeyUnlockCursor1, KeyUnlockCursor2
    ];
}
