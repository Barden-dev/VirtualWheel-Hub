using System;

namespace SimRacingHub.Core
{
    /// <summary>
    /// Abstraction for application storage paths and directory isolation.
    /// Supports Windows default directories, Linux XDG base directories,
    /// and temporary test/preview isolation.
    /// </summary>
    public interface IAppStorage
    {
        /// <summary>
        /// Base application data directory (Windows: Documents\vWheel Hub, Linux: $XDG_DATA_HOME/vWheelHub).
        /// </summary>
        string AppDataDirectory { get; }

        /// <summary>
        /// Local application data directory (Windows: %LOCALAPPDATA%\vWheel Hub, Linux: $XDG_DATA_HOME/vWheelHub).
        /// </summary>
        string LocalDataDirectory { get; }

        /// <summary>
        /// Directory containing simulator and vehicle profiles.
        /// </summary>
        string ProfilesDirectory { get; }

        /// <summary>
        /// Directory containing session and diagnostic logs.
        /// </summary>
        string LogsDirectory { get; }

        /// <summary>
        /// Directory containing learning and adaptive caches.
        /// </summary>
        string CacheDirectory { get; }

        /// <summary>
        /// Full file path to application configuration settings (appsettings.json).
        /// </summary>
        string SettingsFilePath { get; }

        /// <summary>
        /// Full file path to window position and size state (window.json).
        /// </summary>
        string WindowSettingsPath { get; }

        /// <summary>
        /// Returns the cache subdirectory for a specific simulation game.
        /// </summary>
        string GetGameCacheDirectory(string gameName);
    }
}
