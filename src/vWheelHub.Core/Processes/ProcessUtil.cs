using System;
using System.Collections.Generic;
using SimRacingHub.Linux;

namespace SimRacingHub.Core
{
    /// <summary>
    /// Utility facade for cross-platform process queries, delegating to <see cref="IProcessDetector.Current"/>.
    /// Defaults to <see cref="LinuxProcessDetector"/> on Linux and <see cref="WindowsProcessDetector"/> on Windows.
    /// Supports test injection and reset.
    /// </summary>
    public static class ProcessUtil
    {
        private static IProcessDetector? _customDetector;

        /// <summary>
        /// Gets or sets the active process detector instance.
        /// Defaults to the platform detector. Can be overridden for unit testing.
        /// </summary>
        public static IProcessDetector Current
        {
            get => _customDetector ?? DefaultDetector;
            set => _customDetector = value;
        }

        private static readonly IProcessDetector DefaultDetector = CreatePlatformDetector();

        private static IProcessDetector CreatePlatformDetector()
        {
            if (OperatingSystem.IsLinux())
            {
                return new LinuxProcessDetector();
            }
            return new WindowsProcessDetector();
        }

        /// <summary>
        /// Checks whether a process with the specified name is currently running.
        /// 100% backward compatible with existing callers.
        /// </summary>
        public static bool IsRunning(string? processName)
        {
            if (string.IsNullOrWhiteSpace(processName)) return false;
            return Current.IsRunning(processName);
        }

        /// <summary>
        /// Checks whether any of the specified process names are currently running.
        /// </summary>
        public static bool IsAnyRunning(IEnumerable<string> processNames, out string? matchedProcess)
        {
            if (processNames == null)
            {
                matchedProcess = null;
                return false;
            }
            return Current.IsAnyRunning(processNames, out matchedProcess);
        }

        /// <summary>
        /// Resets the active detector back to the default platform detector.
        /// </summary>
        public static void ResetToDefault()
        {
            _customDetector = null;
        }
    }
}
