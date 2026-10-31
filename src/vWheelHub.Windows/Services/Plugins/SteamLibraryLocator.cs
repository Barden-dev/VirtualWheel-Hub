using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using SimRacingHub.Core;

namespace SimRacingHub.Services.Plugins
{
    public static class SteamLibraryLocator
    {
        public static string? FindSteamInstallPath()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                if (key != null)
                {
                    string? path = key.GetValue("SteamPath") as string;
                    if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                    {
                        return Path.GetFullPath(path);
                    }
                }

                using var hklmKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam");
                if (hklmKey != null)
                {
                    string? path = hklmKey.GetValue("InstallPath") as string;
                    if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                    {
                        return Path.GetFullPath(path);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError("Error looking up Steam installation path in Registry", ex);
            }

            return null;
        }

        public static List<string> GetAllSteamLibraries()
        {
            var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Check Main Steam Path from Registry
            string? mainSteamPath = FindSteamInstallPath();
            if (!string.IsNullOrEmpty(mainSteamPath) && Directory.Exists(mainSteamPath))
            {
                libraries.Add(mainSteamPath);

                string vdfPath = Path.Combine(mainSteamPath, "steamapps", "libraryfolders.vdf");
                if (File.Exists(vdfPath))
                {
                    try
                    {
                        string content = File.ReadAllText(vdfPath);
                        var matches = Regex.Matches(content, @"""path""\s+""([^""]+)""", RegexOptions.IgnoreCase);
                        foreach (Match match in matches)
                        {
                            if (match.Groups.Count > 1)
                            {
                                string rawPath = match.Groups[1].Value.Replace(@"\\", @"\").Replace('/', '\\');
                                if (Directory.Exists(rawPath))
                                {
                                    libraries.Add(Path.GetFullPath(rawPath));
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Instance.LogError("Error parsing libraryfolders.vdf", ex);
                    }
                }
            }

            // 2. Fallback: Scan drive roots for common SteamLibrary folders
            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (!drive.IsReady) continue;

                    string root = drive.RootDirectory.FullName;
                    string[] candidates = new[]
                    {
                        Path.Combine(root, "SteamLibrary"),
                        Path.Combine(root, "Steam"),
                        Path.Combine(root, "Games", "SteamLibrary"),
                        Path.Combine(root, "Games", "Steam"),
                        Path.Combine(root, "Program Files (x86)", "Steam"),
                        Path.Combine(root, "Program Files", "Steam")
                    };

                    foreach (var candidate in candidates)
                    {
                        if (Directory.Exists(candidate))
                        {
                            libraries.Add(Path.GetFullPath(candidate));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError("Error scanning system drives for Steam libraries", ex);
            }

            return libraries.ToList();
        }

        public static string? FindGamePath(string steamAppId, string relativeFolderName)
        {
            // 1. Try Windows Registry uninstall keys
            string? regPath = GetGamePathFromRegistry(steamAppId);
            if (!string.IsNullOrEmpty(regPath) && Directory.Exists(regPath))
            {
                return regPath;
            }

            // 2. Scan all Steam libraries
            var libraries = GetAllSteamLibraries();
            foreach (var lib in libraries)
            {
                string candidate = Path.Combine(lib, "steamapps", "common", relativeFolderName);
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }

            // 3. Direct drive search fallback
            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (!drive.IsReady) continue;
                    string driveRoot = drive.RootDirectory.FullName;
                    string directPath = Path.Combine(driveRoot, "SteamLibrary", "steamapps", "common", relativeFolderName);
                    if (Directory.Exists(directPath))
                    {
                        return directPath;
                    }
                }
            }
            catch { }

            return null;
        }

        private static string? GetGamePathFromRegistry(string steamAppId)
        {
            string keyPath = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App {steamAppId}";
            string keyPath64 = $@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Steam App {steamAppId}";

            foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
            {
                foreach (var path in new[] { keyPath, keyPath64 })
                {
                    try
                    {
                        using var key = root.OpenSubKey(path);
                        if (key != null)
                        {
                            string? loc = key.GetValue("InstallLocation") as string;
                            if (!string.IsNullOrEmpty(loc) && Directory.Exists(loc))
                            {
                                return Path.GetFullPath(loc);
                            }
                        }
                    }
                    catch { }
                }
            }

            return null;
        }
    }
}
