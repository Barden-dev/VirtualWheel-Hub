using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using SimRacingHub.Services;

namespace SimRacingHub.Core;

/// <summary>Copies supported legacy data layouts without changing the current path policy.</summary>
public sealed class LegacyStorageMigrator
{
    private readonly IAppStorage _storage;
    public LegacyStorageMigrator(IAppStorage storage) => _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    private const string WindowsMigrationMarkerName = ".migrated_from_documents";

    private const string LinuxMigrationMarkerName = ".migrated_linux_xdg";

    private const string MovedNoticeName = "README_MOVED.txt";

    public void MigrateWindowsLegacyData()
    {
        try
        {
            MigrateFromDocuments();

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // 1. Migrate Profiles from App directory if present
            string legacyProfiles = Path.Combine(baseDir, "Profiles");
            if (Directory.Exists(legacyProfiles) && !string.Equals(legacyProfiles, _storage.ProfilesDirectory, StringComparison.OrdinalIgnoreCase))
            {
                CopyDirectoryContent(legacyProfiles, _storage.ProfilesDirectory);
            }

            // 2. Migrate Cache from App directory if present
            string legacyCache = Path.Combine(baseDir, "Cache");
            if (Directory.Exists(legacyCache) && !string.Equals(legacyCache, _storage.CacheDirectory, StringComparison.OrdinalIgnoreCase))
            {
                MigrateLegacyCacheFiles(legacyCache);
            }

            // 3. Migrate loose cache files in base dir if any
            MigrateLegacyCacheFiles(baseDir);

            // 4. Migrate appsettings.json
            string legacyAppSettings = Path.Combine(baseDir, "appsettings.json");
            if (File.Exists(legacyAppSettings) && !File.Exists(_storage.SettingsFilePath))
            {
                File.Copy(legacyAppSettings, _storage.SettingsFilePath, overwrite: false);
            }

            // 5. Migrate window.json
            string legacyWindow = Path.Combine(baseDir, "window.json");
            if (File.Exists(legacyWindow) && !File.Exists(_storage.WindowSettingsPath))
            {
                File.Copy(legacyWindow, _storage.WindowSettingsPath, overwrite: false);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AppStorage Windows migration exception: {ex.Message}");
        }
    }

    private void MigrateFromDocuments()
    {
        if (string.Equals(_storage.LocalDataDirectory, _storage.AppDataDirectory, StringComparison.OrdinalIgnoreCase)) return;

        string marker = Path.Combine(_storage.LocalDataDirectory, WindowsMigrationMarkerName);
        if (File.Exists(marker)) return;

        string legacyCache = Path.Combine(_storage.AppDataDirectory, "Cache");
        string legacyLogs = Path.Combine(_storage.AppDataDirectory, "Logs");

        CopyDirectoryContent(legacyCache, _storage.CacheDirectory);
        CopyDirectoryContent(legacyLogs, _storage.LogsDirectory);

        WriteMovedNotice(legacyCache, _storage.CacheDirectory);
        WriteMovedNotice(legacyLogs, _storage.LogsDirectory);

        try
        {
            File.WriteAllText(marker, $"Cache and Logs were copied from \"{_storage.AppDataDirectory}\" on {DateTime.Now:yyyy-MM-dd HH:mm:ss}.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AppStorage marker exception: {ex.Message}");
        }
    }

    public void MigrateLinuxLegacyData(string? homeDir = null)
    {
        try
        {
            string marker = Path.Combine(_storage.LocalDataDirectory, LinuxMigrationMarkerName);
            if (File.Exists(marker)) return;

            string home = !string.IsNullOrWhiteSpace(homeDir)
                ? homeDir
                : (Environment.GetEnvironmentVariable("HOME") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

            if (string.IsNullOrWhiteSpace(home)) return;

            // Known legacy Linux locations:
            // 1. Old SpecialFolder.MyDocuments path: ~/vWheel Hub
            // 2. Old SpecialFolder.LocalApplicationData path: ~/.local/share/vWheel Hub
            // 3. Old hidden home directory: ~/.vwheelhub
            string[] knownLegacyRoots = new[]
            {
                Path.Combine(home, "vWheel Hub"),
                Path.Combine(home, ".local", "share", "vWheel Hub"),
                Path.Combine(home, ".vwheelhub")
            };

            bool copiedAny = false;

            foreach (string legacyRoot in knownLegacyRoots)
            {
                if (!Directory.Exists(legacyRoot)) continue;

                string legacyProfiles = Path.Combine(legacyRoot, "Profiles");
                if (Directory.Exists(legacyProfiles))
                {
                    CopyDirectoryContent(legacyProfiles, _storage.ProfilesDirectory);
                    copiedAny = true;
                }

                string legacyCache = Path.Combine(legacyRoot, "Cache");
                if (Directory.Exists(legacyCache))
                {
                    CopyDirectoryContent(legacyCache, _storage.CacheDirectory);
                    copiedAny = true;
                }

                string legacyLogs = Path.Combine(legacyRoot, "Logs");
                if (Directory.Exists(legacyLogs))
                {
                    CopyDirectoryContent(legacyLogs, _storage.LogsDirectory);
                    copiedAny = true;
                }

                string legacySettings = Path.Combine(legacyRoot, "appsettings.json");
                if (File.Exists(legacySettings) && !File.Exists(_storage.SettingsFilePath))
                {
                    string? dir = Path.GetDirectoryName(_storage.SettingsFilePath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    File.Copy(legacySettings, _storage.SettingsFilePath, overwrite: false);
                    copiedAny = true;
                }

                string legacyWindow = Path.Combine(legacyRoot, "window.json");
                if (File.Exists(legacyWindow) && !File.Exists(_storage.WindowSettingsPath))
                {
                    string? dir = Path.GetDirectoryName(_storage.WindowSettingsPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    File.Copy(legacyWindow, _storage.WindowSettingsPath, overwrite: false);
                    copiedAny = true;
                }
            }

            // Check AppDomain base directory legacy profiles (non-destructive)
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string baseProfiles = Path.Combine(baseDir, "Profiles");
            if (Directory.Exists(baseProfiles) && !string.Equals(baseProfiles, _storage.ProfilesDirectory, StringComparison.OrdinalIgnoreCase))
            {
                CopyDirectoryContent(baseProfiles, _storage.ProfilesDirectory);
                copiedAny = true;
            }

            try
            {
                if (!Directory.Exists(_storage.LocalDataDirectory)) Directory.CreateDirectory(_storage.LocalDataDirectory);
                File.WriteAllText(marker, $"Migrated from legacy Linux directories on {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC. Copied: {copiedAny}");
            }
            catch (Exception markerEx)
            {
                System.Diagnostics.Debug.WriteLine($"AppStorage Linux marker exception: {markerEx.Message}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AppStorage Linux migration exception: {ex.Message}");
        }
    }

    private static void WriteMovedNotice(string legacyDir, string newDir)
    {
        if (!Directory.Exists(legacyDir)) return;

        try
        {
            string notice = Path.Combine(legacyDir, MovedNoticeName);
            if (File.Exists(notice)) return;

            File.WriteAllText(notice,
                "This folder is no longer used by vWheel Hub." + Environment.NewLine +
                "Its contents have been copied to:" + Environment.NewLine +
                newDir + Environment.NewLine + Environment.NewLine +
                "The files here are kept only as a backup and can be deleted safely.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AppStorage notice exception: {ex.Message}");
        }
    }

    private void MigrateLegacyCacheFiles(string sourceDir)
    {
        if (!Directory.Exists(sourceDir)) return;

        foreach (var file in Directory.GetFiles(sourceDir, "*_cache*.json"))
        {
            string filename = Path.GetFileName(file);
            string targetFolder = _storage.CacheDirectory;
            if (filename.StartsWith("acc_", StringComparison.OrdinalIgnoreCase))
            {
                targetFolder = _storage.GetGameCacheDirectory("Assetto Corsa Competizione");
            }
            else if (filename.StartsWith("ac_", StringComparison.OrdinalIgnoreCase))
            {
                targetFolder = _storage.GetGameCacheDirectory("Assetto Corsa");
            }
            else if (filename.StartsWith("lmu_", StringComparison.OrdinalIgnoreCase))
            {
                targetFolder = _storage.GetGameCacheDirectory("Le Mans Ultimate");
            }

            string targetFile = Path.Combine(targetFolder, filename);
            if (!File.Exists(targetFile))
            {
                try
                {
                    File.Copy(file, targetFile, overwrite: false);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"AppStorage could not migrate cache file '{filename}': {ex.Message}");
                }
            }
        }
    }

    private static void CopyDirectoryContent(string sourceDir, string targetDir)
    {
        if (!Directory.Exists(sourceDir)) return;
        if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceDir, file);
            string destFile = Path.Combine(targetDir, relativePath);
            string? destDir = Path.GetDirectoryName(destFile);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
            if (!File.Exists(destFile))
            {
                try
                {
                    File.Copy(file, destFile, overwrite: false);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"AppStorage could not copy '{relativePath}' to '{targetDir}': {ex.Message}");
                }
            }
        }
    }

}
