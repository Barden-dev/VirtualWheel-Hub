using System;

namespace SimRacingHub.Core
{
    /// <summary>
    /// Abstraction for platform-specific hardware input capture, including
    /// relative mouse displacement (steering) and asynchronous key state polling.
    /// </summary>
    public interface IInputSource : IDisposable
    {
        /// <summary>
        /// Gets whether the input acquisition worker/subsystem is currently running.
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Starts the input acquisition subsystem (message pump or evdev reader).
        /// Throws an exception if startup fails.
        /// </summary>
        void Start();

        /// <summary>
        /// Stops the input acquisition subsystem and joins worker threads.
        /// </summary>
        void Stop();

        /// <summary>
        /// Consumes and resets the accumulated relative X mouse delta since last call.
        /// This method is called at high frequency (1000 Hz) and must be lock-free.
        /// </summary>
        /// <returns>Accumulated mouse delta on X axis.</returns>
        int ConsumeDeltaX();

        /// <summary>
        /// Queries whether the physical key or button associated with the given key code is currently held down.
        /// </summary>
        /// <param name="keyCode">Virtual key code or platform scancode.</param>
        /// <returns>True if pressed; otherwise false.</returns>
        bool IsKeyDown(int keyCode);
    }
}
