using System;
using System.Diagnostics;

namespace SimRacingHub.Core
{
    public static class ProcessUtil
    {
        public static bool IsRunning(string? processName)
        {
            if (string.IsNullOrWhiteSpace(processName)) return false;

            Process[]? processes = null;
            try
            {
                processes = Process.GetProcessesByName(processName);
                return processes.Length > 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProcessUtil] failed to enumerate '{processName}': {ex.Message}");
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
    }
}
