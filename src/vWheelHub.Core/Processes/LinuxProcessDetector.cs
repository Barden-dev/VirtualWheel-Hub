using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SimRacingHub.Core;

namespace SimRacingHub.Linux
{
    /// <summary>
    /// Linux implementation of <see cref="IProcessDetector"/> scanning the virtual <c>/proc</c> filesystem.
    /// Supports native Linux ELF binaries, Wine/Proton processes (cmdline token inspection),
    /// 15-character comm truncation handling, and a Cached PID Fast-Path.
    /// </summary>
    public class LinuxProcessDetector : IProcessDetector
    {
        private readonly string _procRoot;
        private readonly ConcurrentDictionary<string, int> _cachedPids = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Default constructor scanning the host Linux <c>/proc</c> directory.
        /// </summary>
        public LinuxProcessDetector() : this("/proc")
        {
        }

        /// <summary>
        /// Constructor accepting a custom proc filesystem root directory, enabling
        /// deterministic synthetic unit tests without root permissions.
        /// </summary>
        /// <param name="procRoot">Absolute or relative path to the proc directory.</param>
        public LinuxProcessDetector(string procRoot)
        {
            _procRoot = procRoot ?? throw new ArgumentNullException(nameof(procRoot));
        }

        /// <summary>
        /// Checks whether a process with the specified name is currently running.
        /// </summary>
        public bool IsRunning(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName)) return false;

            string targetStem = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? processName[..^4]
                : processName;
            string targetExe = targetStem + ".exe";

            // 1. Cached PID Fast-Path: verify if the previously detected PID is still running this game.
            if (_cachedPids.TryGetValue(targetStem, out int cachedPid))
            {
                if (CheckPidMatches(cachedPid, targetStem, targetExe))
                {
                    return true;
                }

                // Invalidate dead or recycled PID
                _cachedPids.TryRemove(targetStem, out _);
            }

            // 2. Full /proc scan
            if (!Directory.Exists(_procRoot))
            {
                return false;
            }

            try
            {
                foreach (string pidDir in Directory.EnumerateDirectories(_procRoot))
                {
                    string dirName = Path.GetFileName(pidDir);
                    if (!int.TryParse(dirName, out int pid))
                    {
                        continue;
                    }

                    if (CheckPidMatches(pid, targetStem, targetExe))
                    {
                        _cachedPids[targetStem] = pid;
                        return true;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Disappearing or inaccessible proc entries
                return false;
            }

            _cachedPids.TryRemove(targetStem, out _);
            return false;
        }

        /// <summary>
        /// Checks whether any of the specified process names are currently running.
        /// Returns true if at least one match is found, with matchedProcess set to the matching process name.
        /// </summary>
        public bool IsAnyRunning(IEnumerable<string> processNames, out string? matchedProcess)
        {
            if (processNames != null)
            {
                foreach (string name in processNames)
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

        /// <summary>
        /// Checks if a cached PID exists for the given process name (for test verification).
        /// </summary>
        public bool HasCachedPid(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName)) return false;
            string stem = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? processName[..^4]
                : processName;
            return _cachedPids.ContainsKey(stem);
        }

        /// <summary>
        /// Gets the current number of cached PIDs.
        /// </summary>
        public int CachedPidCount => _cachedPids.Count;

        /// <summary>
        /// Clears all cached PIDs.
        /// </summary>
        public void ClearCache()
        {
            _cachedPids.Clear();
        }

        /// <summary>Checks one window owner without scanning or accepting another running process.</summary>
        public bool IsProcess(int pid, string processName)
        {
            if (pid <= 0 || string.IsNullOrWhiteSpace(processName)) return false;
            string stem = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? processName[..^4] : processName;
            return CheckPidMatches(pid, stem, stem + ".exe");
        }

        private bool CheckPidMatches(int pid, string targetStem, string targetExe)
        {
            string pidDir = Path.Combine(_procRoot, pid.ToString());
            if (!Directory.Exists(pidDir))
            {
                return false;
            }

            // A. Check /proc/[pid]/cmdline (null-separated arguments, Wine/Proton loaders, Windows paths)
            string cmdlinePath = Path.Combine(pidDir, "cmdline");
            try
            {
                if (File.Exists(cmdlinePath))
                {
                    byte[] bytes = File.ReadAllBytes(cmdlinePath);
                    if (bytes.Length > 0)
                    {
                        string cmdline = Encoding.UTF8.GetString(bytes);
                        string[] tokens = cmdline.Split('\0', StringSplitOptions.RemoveEmptyEntries);
                        foreach (string token in tokens)
                        {
                            if (string.IsNullOrWhiteSpace(token)) continue;

                            int lastSlash = token.LastIndexOf('/');
                            int lastBack = token.LastIndexOf('\\');
                            int lastSep = Math.Max(lastSlash, lastBack);
                            string fileName = lastSep >= 0 ? token[(lastSep + 1)..] : token;
                            fileName = fileName.Trim('"', '\'', ' ', '\t');

                            if (MatchesTarget(fileName, targetStem, targetExe))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Disappearing process or access denied
            }

            // B. Check /proc/[pid]/comm (thread/process name, handles 15-character truncation)
            string commPath = Path.Combine(pidDir, "comm");
            try
            {
                if (File.Exists(commPath))
                {
                    string comm = File.ReadAllText(commPath).TrimEnd('\r', '\n', ' ', '\t');
                    if (MatchesComm(comm, targetStem, targetExe))
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Disappearing process or access denied
            }

            // C. Check /proc/[pid]/exe (symbolic link to executable binary)
            string exePath = Path.Combine(pidDir, "exe");
            try
            {
                if (File.Exists(exePath))
                {
                    string? targetPath = null;
                    try
                    {
                        var linkTarget = File.ResolveLinkTarget(exePath, returnFinalTarget: true);
                        if (linkTarget != null)
                        {
                            targetPath = linkTarget.FullName;
                        }
                    }
                    catch
                    {
                        // File.ResolveLinkTarget might throw on non-symlink or some filesystems
                    }

                    // Fallback for mock test directories where exe is a plain text file containing the path
                    if (targetPath == null)
                    {
                        try
                        {
                            string content = File.ReadAllText(exePath).Trim();
                            if (content.Contains('/') || content.Contains('\\'))
                            {
                                targetPath = content;
                            }
                        }
                        catch { }
                    }

                    if (targetPath != null)
                    {
                        int lastSlash = targetPath.LastIndexOf('/');
                        int lastBack = targetPath.LastIndexOf('\\');
                        int lastSep = Math.Max(lastSlash, lastBack);
                        string fileName = lastSep >= 0 ? targetPath[(lastSep + 1)..] : targetPath;
                        fileName = fileName.Trim('"', '\'', ' ', '\t');

                        if (MatchesTarget(fileName, targetStem, targetExe))
                        {
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Disappearing process or access denied
            }

            return false;
        }

        private static bool MatchesTarget(string fileName, string targetStem, string targetExe)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return false;

            if (fileName.Equals(targetStem, StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals(targetExe, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string fileStem = fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? fileName[..^4]
                : fileName;

            return fileStem.Equals(targetStem, StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesComm(string comm, string targetStem, string targetExe)
        {
            if (string.IsNullOrWhiteSpace(comm)) return false;

            if (MatchesTarget(comm, targetStem, targetExe))
            {
                return true;
            }

            // Handle Linux kernel TASK_COMM_LEN truncation (15 characters)
            if (targetStem.Length > 15 && comm.Equals(targetStem[..15], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (targetExe.Length > 15 && comm.Equals(targetExe[..15], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }
    }
}
