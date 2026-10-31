using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using SimRacingHub.Services;

namespace SimRacingHub.Core;

/// <summary>Current application paths and platform-specific storage construction.</summary>
public class AppStorage : IAppStorage
{
    public string AppDataDirectory { get; }

    public string LocalDataDirectory { get; }

    public string ProfilesDirectory { get; }

    public string LogsDirectory { get; }

    public string CacheDirectory { get; }

    public string SettingsFilePath { get; }

    public string WindowSettingsPath { get; }

    public AppStorage(
        string appDataDirectory,
        string localDataDirectory,
        string profilesDirectory,
        string logsDirectory,
        string cacheDirectory,
        string settingsFilePath,
        string windowSettingsPath)
    {
        AppDataDirectory = appDataDirectory ?? throw new ArgumentNullException(nameof(appDataDirectory));
        LocalDataDirectory = localDataDirectory ?? throw new ArgumentNullException(nameof(localDataDirectory));
        ProfilesDirectory = profilesDirectory ?? throw new ArgumentNullException(nameof(profilesDirectory));
        LogsDirectory = logsDirectory ?? throw new ArgumentNullException(nameof(logsDirectory));
        CacheDirectory = cacheDirectory ?? throw new ArgumentNullException(nameof(cacheDirectory));
        SettingsFilePath = settingsFilePath ?? throw new ArgumentNullException(nameof(settingsFilePath));
        WindowSettingsPath = windowSettingsPath ?? throw new ArgumentNullException(nameof(windowSettingsPath));
    }

    public string GetGameCacheDirectory(string gameName)
    {
        string cleanGame = ProfileManager.ResolveExistingDirectory(CacheDirectory, gameName);
        if (string.IsNullOrWhiteSpace(cleanGame)) cleanGame = "Universal";
        string gameCacheDir = Path.Combine(CacheDirectory, cleanGame);
        try
        {
            if (!Directory.Exists(gameCacheDir))
            {
                Directory.CreateDirectory(gameCacheDir);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AppStorage could not create game cache directory '{gameCacheDir}': {ex.Message}");
        }
        return gameCacheDir;
    }

    public void EnsureDirectories()
    {
        try
        {
            if (!Directory.Exists(AppDataDirectory)) Directory.CreateDirectory(AppDataDirectory);
            if (!Directory.Exists(LocalDataDirectory)) Directory.CreateDirectory(LocalDataDirectory);
            if (!Directory.Exists(ProfilesDirectory)) Directory.CreateDirectory(ProfilesDirectory);
            if (!Directory.Exists(CacheDirectory)) Directory.CreateDirectory(CacheDirectory);
            if (!Directory.Exists(LogsDirectory)) Directory.CreateDirectory(LogsDirectory);

            string? settingsDir = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrEmpty(settingsDir) && !Directory.Exists(settingsDir)) Directory.CreateDirectory(settingsDir);

            string? windowDir = Path.GetDirectoryName(WindowSettingsPath);
            if (!string.IsNullOrEmpty(windowDir) && !Directory.Exists(windowDir)) Directory.CreateDirectory(windowDir);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AppStorage EnsureDirectories exception: {ex.Message}");
        }
    }

    public static AppStorage CreateDefault()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var linuxStorage = CreateLinuxStorage();
            linuxStorage.EnsureDirectories();
            linuxStorage.MigrateLinuxLegacyData();
            return linuxStorage;
        }
        else
        {
            var windowsStorage = CreateWindowsStorage();
            windowsStorage.EnsureDirectories();
            windowsStorage.MigrateWindowsLegacyData();
            return windowsStorage;
        }
    }

    public static AppStorage CreateWindowsStorage(
        string? myDocuments = null,
        string? localAppData = null)
    {
        string docs = !string.IsNullOrWhiteSpace(myDocuments)
            ? myDocuments
            : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string appDataDir = Path.Combine(docs, "vWheel Hub");

        string localApp = !string.IsNullOrWhiteSpace(localAppData)
            ? localAppData
            : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string localDataDir = string.IsNullOrWhiteSpace(localApp)
            ? appDataDir
            : Path.Combine(localApp, "vWheel Hub");

        string profilesDir = Path.Combine(appDataDir, "Profiles");
        string cacheDir = Path.Combine(localDataDir, "Cache");
        string logsDir = Path.Combine(localDataDir, "Logs");
        string settingsPath = Path.Combine(appDataDir, "appsettings.json");
        string windowSettingsPath = Path.Combine(appDataDir, "window.json");

        return new AppStorage(
            appDataDir,
            localDataDir,
            profilesDir,
            logsDir,
            cacheDir,
            settingsPath,
            windowSettingsPath);
    }

    public static AppStorage CreateLinuxStorage(
        string? homeDir = null,
        string? xdgConfigHome = null,
        string? xdgDataHome = null,
        string? xdgCacheHome = null,
        string? xdgStateHome = null,
        bool readEnvironment = true)
    {
        string home = !string.IsNullOrWhiteSpace(homeDir)
            ? homeDir
            : (readEnvironment ? Environment.GetEnvironmentVariable("HOME") : null)
              ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (string.IsNullOrWhiteSpace(home))
        {
            home = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }
        if (string.IsNullOrWhiteSpace(home))
        {
            home = "/tmp";
        }

        string? configEnv = !string.IsNullOrWhiteSpace(xdgConfigHome)
            ? xdgConfigHome
            : (readEnvironment ? Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") : null);
        string configBase = !string.IsNullOrWhiteSpace(configEnv)
            ? configEnv
            : Path.Combine(home, ".config");
        string configDir = Path.Combine(configBase, "vWheelHub");

        string? dataEnv = !string.IsNullOrWhiteSpace(xdgDataHome)
            ? xdgDataHome
            : (readEnvironment ? Environment.GetEnvironmentVariable("XDG_DATA_HOME") : null);
        string dataBase = !string.IsNullOrWhiteSpace(dataEnv)
            ? dataEnv
            : Path.Combine(home, ".local", "share");
        string dataDir = Path.Combine(dataBase, "vWheelHub");

        string? cacheEnv = !string.IsNullOrWhiteSpace(xdgCacheHome)
            ? xdgCacheHome
            : (readEnvironment ? Environment.GetEnvironmentVariable("XDG_CACHE_HOME") : null);
        string cacheBase = !string.IsNullOrWhiteSpace(cacheEnv)
            ? cacheEnv
            : Path.Combine(home, ".cache");
        string cacheDir = Path.Combine(cacheBase, "vWheelHub");

        string? stateEnv = !string.IsNullOrWhiteSpace(xdgStateHome)
            ? xdgStateHome
            : (readEnvironment ? Environment.GetEnvironmentVariable("XDG_STATE_HOME") : null);
        string stateBase = !string.IsNullOrWhiteSpace(stateEnv)
            ? stateEnv
            : Path.Combine(home, ".local", "state");
        string stateDir = Path.Combine(stateBase, "vWheelHub");
        string logsDir = Path.Combine(stateDir, "Logs");

        string profilesDir = Path.Combine(dataDir, "Profiles");
        string settingsPath = Path.Combine(configDir, "appsettings.json");
        string windowSettingsPath = Path.Combine(configDir, "window.json");

        return new AppStorage(
            dataDir,
            dataDir,
            profilesDir,
            logsDir,
            cacheDir,
            settingsPath,
            windowSettingsPath);
    }

    public static AppStorage CreateTempStorage(string? rootDir = null, string? prefix = null)
    {
        string root = !string.IsNullOrWhiteSpace(rootDir)
            ? rootDir
            : Path.Combine(Path.GetTempPath(), (prefix ?? "vWheelHub_Temp_") + Guid.NewGuid().ToString("N"));

        string appDataDir = Path.Combine(root, "AppData");
        string localDataDir = Path.Combine(root, "LocalData");
        string profilesDir = Path.Combine(root, "Profiles");
        string logsDir = Path.Combine(root, "Logs");
        string cacheDir = Path.Combine(root, "Cache");
        string settingsPath = Path.Combine(root, "appsettings.json");
        string windowSettingsPath = Path.Combine(root, "window.json");

        var storage = new AppStorage(
            appDataDir,
            localDataDir,
            profilesDir,
            logsDir,
            cacheDir,
            settingsPath,
            windowSettingsPath);

        storage.EnsureDirectories();
        return storage;
    }

    public void MigrateWindowsLegacyData() => new LegacyStorageMigrator(this).MigrateWindowsLegacyData();

    public void MigrateLinuxLegacyData(string? homeDir = null) => new LegacyStorageMigrator(this).MigrateLinuxLegacyData(homeDir);

}
