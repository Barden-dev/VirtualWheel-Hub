using System;

namespace SimRacingHub.Application.Runtime
{
    public class TickState
    {
        public VirtualButtonProcessor VirtualButtons { get; } = new();
        public DrivingFocusGate FocusGate { get; } = new();
        public bool LockMouse { get; set; } = true;
        public bool HoldMouseUnlocked { get; set; }
        public bool WasLockTogglePressed { get; set; }
        public bool WasHubTogglePressed { get; set; }
        public bool WasGameActive { get; set; }
        public bool IsHubActive { get; set; } = true;
        public bool BypassInternalLockToggle { get; set; }
    }
}
