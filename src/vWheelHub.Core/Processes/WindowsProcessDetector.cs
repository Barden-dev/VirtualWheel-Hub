using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SimRacingHub.Core
{
    /// <summary>
    /// Windows implementation of <see cref="IProcessDetector"/> using standard Win32 Process APIs.
    /// Preserves exact existing Windows behavior with zero regression.
    /// </summary>
    public class WindowsProcessDetector : IProcessDetector
    {
        public bool IsRunning(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName)) return false;

            if (processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                processName = processName[..^4];
            }

            Process[]? processes = null;
            try
            {
                processes = Process.GetProcessesByName(processName);
                return processes.Length > 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowsProcessDetector] failed to enumerate '{processName}': {ex.Message}");
                return false;
            }
            finally
            {
                if (processes != null)
                {
                    foreach (var process in processes)
                    {
                        try { process.Dispose(); } catch { }
                    }
                }
            }
        }

        public bool IsAnyRunning(IEnumerable<string> processNames, out string? matchedProcess)
        {
            if (processNames != null)
            {
                foreach (var name in processNames)
                {
                    if (IsRunning(name))
                    {
                        matchedProcess = name;
                        return true;
                    }
                }
            }

            matchedProcess = null;
            return false;
        }
    }
}
