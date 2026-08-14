using System;
using System.IO;
using SimRacingHub.Services;

namespace SimRacingHub.Core
{
    public static class StoragePaths
    {
        private static readonly string _appDataDir;
        private static readonly string _profilesDir;
        private static readonly string _cacheDir;
        private static readonly string _logsDir;
        private static readonly string _appSettingsPath;
        private static readonly string _windowSettingsPath;

        public static string AppDataDirectory => _appDataDir;
        public static string ProfilesDirectory => _profilesDir;
        public static string CacheDirectory => _cacheDir;
        public static string LogsDirectory => _logsDir;
        public static string AppSettingsPath => _appSettingsPath;
        public static string WindowSettingsPath => _windowSettingsPath;

        static StoragePaths()
        {
            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            _appDataDir = Path.Combine(docs, "vWheel Hub");
            _profilesDir = Path.Combine(_appDataDir, "Profiles");
            _cacheDir = Path.Combine(_appDataDir, "Cache");
            _logsDir = Path.Combine(_appDataDir, "Logs");
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
                if (!Directory.Exists(_profilesDir)) Directory.CreateDirectory(_profilesDir);
                if (!Directory.Exists(_cacheDir)) Directory.CreateDirectory(_cacheDir);
                if (!Directory.Exists(_logsDir)) Directory.CreateDirectory(_logsDir);

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
                    try { File.Copy(file, targetFile, overwrite: false); } catch { }
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
                string destDir = Path.GetDirectoryName(destFile);
                if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
                if (!File.Exists(destFile))
                {
                    try { File.Copy(file, destFile, overwrite: false); } catch { }
                }
            }
        }
    }
}
