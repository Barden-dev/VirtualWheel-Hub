using System;

namespace SimRacingHub.Application.Runtime
{
    /// <summary>
    /// Per-tick mutable state maintained across input loop iterations.
    /// Tracks mouse lock state, edge-triggered hotkey transitions, and active game presence.
    /// </summary>
    public partial class TickState
    {
        public VirtualButtonProcessor VirtualButtons { get; } = new();
        public DrivingFocusGate FocusGate { get; } = new();

        /// <summary>User's requested mouse lock toggle state (true = cursor lock &amp; steering active).</summary>
        public bool LockMouse { get; set; } = true;

        /// <summary>Set to true when cursor is temporarily unlocked by holding KeyUnlockCursor.</summary>
        public bool HoldMouseUnlocked { get; set; }

        /// <summary>Previous physical key state of KeyToggleLock for edge-triggered toggling.</summary>
        public bool WasLockTogglePressed { get; set; }

        /// <summary>Previous physical key state of KeyToggleHub for edge-triggered toggling.</summary>
        public bool WasHubTogglePressed { get; set; }

        /// <summary>Tracks whether game was active in the previous tick to detect exit/restart transitions.</summary>
        public bool WasGameActive { get; set; }

        /// <summary>Tracks whether the hub is active (for toggle hub edge-trigger).</summary>
        public bool IsHubActive { get; set; } = true;

        /// <summary>
        /// If true, internal edge-trigger of ToggleLock inside ProcessTick is bypassed,
        /// deferring single ownership to an external hotkey manager (e.g. HotkeyService).
        /// </summary>
        public bool BypassInternalLockToggle { get; set; }
    }
}
