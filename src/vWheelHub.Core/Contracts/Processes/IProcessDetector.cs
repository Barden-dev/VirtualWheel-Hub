using System.Collections.Generic;

namespace SimRacingHub.Core
{
    /// <summary>
    /// Abstraction for platform-specific process detection (e.g. Windows Win32 vs Linux /proc).
    /// </summary>
    public interface IProcessDetector
    {
        /// <summary>
        /// Checks whether a process with the specified name is currently running.
        /// Matches exact binary names, names with or without .exe, truncated comm names,
        /// and Windows executables running under Wine/Proton.
        /// </summary>
        bool IsRunning(string processName);

        /// <summary>
        /// Checks whether any of the specified process names are currently running.
        /// Returns true if at least one match is found, with matchedProcess set to the matching process name.
        /// </summary>
        bool IsAnyRunning(IEnumerable<string> processNames, out string? matchedProcess);
    }
}
