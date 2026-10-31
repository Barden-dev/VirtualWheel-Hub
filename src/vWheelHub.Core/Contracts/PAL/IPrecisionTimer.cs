using System;

namespace SimRacingHub.Core
{
    /// <summary>
    /// Represents a high-resolution waiter capable of sub-millisecond sleep pacing.
    /// </summary>
    public interface IPrecisionWaiter : IDisposable
    {
        /// <summary>
        /// Pauses the calling thread for the specified duration in seconds.
        /// </summary>
        /// <param name="seconds">Duration in seconds to wait.</param>
        /// <returns>
        /// True if the precision wait completed successfully;
        /// false if parameters were invalid or waiting is unsupported, indicating fallback should be used.
        /// </returns>
        bool Wait(double seconds);
    }

    /// <summary>
    /// Contract for managing platform timer resolution and allocating high-precision loop waiters.
    /// </summary>
    public interface IPrecisionTimer
    {
        /// <summary>
        /// Requests the operating system to increase timer resolution to the specified millisecond granularity.
        /// Reference-counted across concurrent callers.
        /// </summary>
        /// <param name="ms">Target resolution in milliseconds (default: 1).</param>
        void BeginPeriod(uint ms = 1);

        /// <summary>
        /// Decrements the timer resolution request and restores the OS default when count reaches zero.
        /// </summary>
        /// <param name="ms">Target resolution in milliseconds (default: 1).</param>
        void EndPeriod(uint ms = 1);

        /// <summary>
        /// Creates an independent waiter instance for sub-millisecond loop pacing.
        /// </summary>
        /// <returns>A new <see cref="IPrecisionWaiter"/> instance.</returns>
        IPrecisionWaiter CreateWaiter();
    }
}
