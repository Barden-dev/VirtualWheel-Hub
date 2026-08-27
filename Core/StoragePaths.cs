using System;
using System.IO;
using SimRacingHub.Services;

namespace SimRacingHub.Core
{
    public static class StoragePaths
    {
        private const string MigrationMarkerName = ".migrated_from_documents";
        private const string MovedNoticeName = "README_MOVED.txt";

        private static readonly string _appDataDir;
        private static readonly string _localDataDir;
        private static readonly string _profilesDir;
        private static readonly string _cacheDir;
        private static readonly string _logsDir;
        private static readonly string _appSettingsPath;
        private static readonly string _windowSettingsPath;

        public static string AppDataDirectory => _appDataDir;
        public static string LocalDataDirectory => _localDataDir;

        public static string ProfilesDirectory => _profilesDir;
        public static string CacheDirectory => _cacheDir;
        public static string LogsDirectory => _logsDir;
        public static string AppSettingsPath => _appSettingsPath;
        public static string WindowSettingsPath => _windowSettingsPath;

        static StoragePaths()
        {
            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            _appDataDir = Path.Combine(docs, "vWheel Hub");

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _localDataDir = string.IsNullOrWhiteSpace(localAppData)
                ? _appDataDir
                : Path.Combine(localAppData, "vWheel Hub");

            _profilesDir = Path.Combine(_appDataDir, "Profiles");
            _cacheDir = Path.Combine(_localDataDir, "Cache");
            _logsDir = Path.Combine(_localDataDir, "Logs");
            _appSettingsPath = Path.Combine(_appDataDir, "appsettings.json");
            _windowSettingsPath = Path.Combine(_appDataDir, "window.json");

            EnsureDirectoriesAndMigrateLegacyData();
        }

        public static string GetGameCacheDirectory(string gameName)
        {
            string cleanGame = ProfileManager.SanitizeName(gameName);
            if (string.IsNullOrWhiteSpace(cleanGame)) cleanGame = "Universal";
            string gameCacheDir = Path.Combine(_cacheDir, cleanGame);
            if (!Directory.Exists(gameCacheDir))
            {
                Directory.CreateDirectory(gameCacheDir);
            }
            return gameCacheDir;
        }

        private static void EnsureDirectoriesAndMigrateLegacyData()
        {
            try
            {
                if (!Directory.Exists(_appDataDir)) Directory.CreateDirectory(_appDataDir);
                if (!Directory.Exists(_localDataDir)) Directory.CreateDirectory(_localDataDir);
                if (!Directory.Exists(_profilesDir)) Directory.CreateDirectory(_profilesDir);
                if (!Directory.Exists(_cacheDir)) Directory.CreateDirectory(_cacheDir);
                if (!Directory.Exists(_logsDir)) Directory.CreateDirectory(_logsDir);

                MigrateFromDocuments();

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;

                // 1. Migrate Profiles from App directory if present
                string legacyProfiles = Path.Combine(baseDir, "Profiles");
                if (Directory.Exists(legacyProfiles))
                {
                    CopyDirectoryContent(legacyProfiles, _profilesDir);
                }

                // 2. Migrate Cache from App directory if present
                string legacyCache = Path.Combine(baseDir, "Cache");
                if (Directory.Exists(legacyCache))
                {
                    MigrateLegacyCacheFiles(legacyCache);
                }

                // 3. Migrate loose cache files in base dir if any
                MigrateLegacyCacheFiles(baseDir);

                // 4. Migrate appsettings.json
                string legacyAppSettings = Path.Combine(baseDir, "appsettings.json");
                if (File.Exists(legacyAppSettings) && !File.Exists(_appSettingsPath))
                {
                    File.Copy(legacyAppSettings, _appSettingsPath, overwrite: false);
                }

                // 5. Migrate window.json
                string legacyWindow = Path.Combine(baseDir, "window.json");
                if (File.Exists(legacyWindow) && !File.Exists(_windowSettingsPath))
                {
                    File.Copy(legacyWindow, _windowSettingsPath, overwrite: false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"StoragePaths migration exception: {ex.Message}");
            }
        }

        private static void MigrateFromDocuments()
        {
            if (string.Equals(_localDataDir, _appDataDir, StringComparison.OrdinalIgnoreCase)) return;

            string marker = Path.Combine(_localDataDir, MigrationMarkerName);
            if (File.Exists(marker)) return;

            string legacyCache = Path.Combine(_appDataDir, "Cache");
            string legacyLogs = Path.Combine(_appDataDir, "Logs");

            CopyDirectoryContent(legacyCache, _cacheDir);
            CopyDirectoryContent(legacyLogs, _logsDir);

            WriteMovedNotice(legacyCache, _cacheDir);
            WriteMovedNotice(legacyLogs, _logsDir);

            try
            {
                File.WriteAllText(marker, $"Cache and Logs were copied from \"{_appDataDir}\" on {DateTime.Now:yyyy-MM-dd HH:mm:ss}.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"StoragePaths marker exception: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"StoragePaths notice exception: {ex.Message}");
            }
        }

        private static void MigrateLegacyCacheFiles(string sourceDir)
        {
            if (!Directory.Exists(sourceDir)) return;

            foreach (var file in Directory.GetFiles(sourceDir, "*_cache*.json"))
            {
                string filename = Path.GetFileName(file);
                string targetFolder = _cacheDir;
                if (filename.StartsWith("acc_", StringComparison.OrdinalIgnoreCase))
                {
                    targetFolder = GetGameCacheDirectory("Assetto Corsa Competizione");
                }
                else if (filename.StartsWith("lmu_", StringComparison.OrdinalIgnoreCase))
                {
                    targetFolder = GetGameCacheDirectory("Le Mans Ultimate");
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
                        System.Diagnostics.Debug.WriteLine($"StoragePaths could not migrate cache file '{filename}': {ex.Message}");
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
                        System.Diagnostics.Debug.WriteLine($"StoragePaths could not copy '{relativePath}' to '{targetDir}': {ex.Message}");
                    }
                }
            }
        }
    }
}
