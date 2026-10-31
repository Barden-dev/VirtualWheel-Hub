using SimRacingHub.Services;

namespace SimRacingHub.Application.Runtime;

/// <summary>Owns runtime cursor intent and menu/hold transitions; the host owns the platform service.</summary>
internal sealed class CursorLockController(ICursorLockService platform, Func<long>? clock = null) : ICursorLockService
{
    private readonly object _gate = new();
    private readonly ICursorLockService _platform = platform;
    private readonly Func<long> _clock = clock ?? (() => Environment.TickCount64);
    private long _nextCaptureRetryAt;
    internal bool Requested { get; set; }
    internal bool MenuUnlocked { get; private set; }
    internal bool HoldUnlocked { get; set; }
    internal bool FocusSuspended { get; set; }
    private bool ShouldLock => Requested && !MenuUnlocked && !HoldUnlocked && !FocusSuspended;
    internal bool IsRequestedLockActive { get { lock (_gate) return ShouldLock && _platform.IsCursorLocked; } }
    public bool IsCursorLocked { get { lock (_gate) return _platform.IsCursorLocked; } }
    public void LockCursorAtCurrentPosition() { lock (_gate) { if (!FocusSuspended && !MenuUnlocked && !HoldUnlocked) _platform.LockCursorAtCurrentPosition(); } }
    public void UnlockCursor() { lock (_gate) _platform.UnlockCursor(); }
    public void ReapplyLock() { lock (_gate) { if (!FocusSuspended && !MenuUnlocked && !HoldUnlocked) _platform.ReapplyLock(); } }
    // Runtime does not own the platform service's lifetime.
    public void Dispose() { }

    internal void SetRequested(bool requested)
    {
        lock (_gate)
        {
            _nextCaptureRetryAt = 0;
            Requested = requested;
            MenuUnlocked = false;
            HoldUnlocked = false;
            if (Requested) LockCursorAtCurrentPosition();
            else UnlockCursor();
        }
    }

    internal void Reset()
    {
        lock (_gate)
        {
            _nextCaptureRetryAt = 0;
            Requested = false;
            MenuUnlocked = false;
            HoldUnlocked = false;
            UnlockCursor();
        }
    }

    internal void UpdateMenu(bool supportsMenuControls, bool autoUnlockInMenu, bool isDriving)
    {
        lock (_gate)
        {
            if (supportsMenuControls && autoUnlockInMenu && Requested)
            {
                if (!isDriving && !MenuUnlocked)
                {
                    MenuUnlocked = true;
                    _platform.UnlockCursor();
                }
                else if (isDriving && MenuUnlocked)
                {
                    MenuUnlocked = false;
                    LockCursorAtCurrentPosition();
                }
            }
            else if (MenuUnlocked)
            {
                MenuUnlocked = false;
                if (Requested)
                {
                    LockCursorAtCurrentPosition();
                }
            }
        }
    }

    internal void UpdateHold(bool isHoldUnlock)
    {
        lock (_gate)
        {
            if (isHoldUnlock)
            {
                if (Requested && !MenuUnlocked && !HoldUnlocked)
                {
                    HoldUnlocked = true;
                    _platform.UnlockCursor();
                }
            }
            else if (HoldUnlocked)
            {
                HoldUnlocked = false;
                if (Requested && !MenuUnlocked)
                {
                    LockCursorAtCurrentPosition();
                }
            }
            // Menu state can resume before the input/focus gate clears. Its first
            // capture is then skipped; reconcile again once all blockers are gone.
            if (ShouldLock && !_platform.IsCursorLocked && _clock() >= _nextCaptureRetryAt)
            {
                _nextCaptureRetryAt = _clock() + 100;
                LockCursorAtCurrentPosition();
            }
        }
    }
}
