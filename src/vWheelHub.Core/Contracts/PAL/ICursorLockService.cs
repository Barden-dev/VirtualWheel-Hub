using System;

namespace SimRacingHub.Services
{
    /// <summary>
    /// Contract for abstracting mouse cursor containment and lock behavior.
    /// </summary>
    public interface ICursorLockService : IDisposable
    {
        /// <summary>
        /// Gets a value indicating whether the cursor is currently locked.
        /// </summary>
        bool IsCursorLocked { get; }

        /// <summary>
        /// Captures and locks the cursor at its current physical screen position.
        /// </summary>
        void LockCursorAtCurrentPosition();

        /// <summary>
        /// Releases any active cursor lock.
        /// </summary>
        void UnlockCursor();

        /// <summary>
        /// Re-applies the cursor lock if currently active (e.g. after window focus restoration).
        /// </summary>
        void ReapplyLock();
    }
}
