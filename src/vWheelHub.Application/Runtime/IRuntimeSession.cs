using System;
using System.Threading;
using System.Threading.Tasks;
using SimRacingHub.Models;

namespace SimRacingHub.Application.Runtime
{
    /// <summary>
    /// Contract managing the lifecycle of the hub processing runtime and virtual device feeding.
    /// </summary>
    public interface IRuntimeSession : IAsyncDisposable, IDisposable
    {
        /// <summary>
        /// Indicates whether the runtime processing loop is actively running.
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Indicates whether cursor grab/lock is currently engaged.
        /// </summary>
        bool IsLocked { get; }

        /// <summary>
        /// Indicates whether a runtime fault occurred.
        /// </summary>
        bool HasFault { get; }

        /// <summary>
        /// Detailed description of runtime fault, if any.
        /// </summary>
        string? FaultMessage { get; }

        /// <summary>Whether the last failed start was blocked by the output device, rather than input or processing.</summary>
        bool LastStartFailureWasOutput => false;

        /// <summary>
        /// Starts the input processing loop and attaches virtual controller feeder.
        /// </summary>
        Task StartAsync(RuntimeStartOptions options, CancellationToken ct = default);

        /// <summary>
        /// Stops the processing loop, releases cursor grab, and neutralizes outputs.
        /// </summary>
        Task StopAsync(CancellationToken ct = default);

        /// <summary>While stopped, releases retained output and prevents starts during a configuration preview/commit.</summary>
        IDisposable? BeginOutputSetup() => null;

        /// <summary>
        /// Updates profile parameters atomically for the running loop.
        /// </summary>
        void UpdateProfile(Profile profile, ResolvedProfile resolved);

        /// <summary>
        /// Updates resolved profile parameters atomically for the running loop.
        /// </summary>
        void UpdateProfile(ResolvedProfile resolved);

        void UpdateProfile(ResolvedProfile resolved, ProfileContext context) => UpdateProfile(resolved);

        /// <summary>
        /// Retrieves the latest immutable runtime snapshot.
        /// </summary>
        RuntimeSnapshot GetSnapshot();

        /// <summary>
        /// Event fired when a new snapshot is published.
        /// </summary>
        event Action<RuntimeSnapshot>? SnapshotUpdated;

        /// <summary>
        /// Event fired when an unhandled runtime error or hardware disconnection occurs.
        /// </summary>
        event Action<string>? FaultOccurred;
    }
}
