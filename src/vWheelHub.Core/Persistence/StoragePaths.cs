using System;

namespace SimRacingHub.Core
{
    /// <summary>
    /// Provides central storage path resolution for vWheel Hub with 100% backward compatibility
    /// for existing Windows properties, Linux XDG paths, and support for isolated test/preview configurations.
    /// </summary>
    public static class StoragePaths
    {
        private static IAppStorage _current;

        static StoragePaths()
        {
            _current = AppStorage.CreateDefault();
        }

        /// <summary>
        /// Gets or sets the active storage provider. Can be replaced with isolated temporary storage
        /// during unit tests or standalone preview mode.
        /// </summary>
        public static IAppStorage Current
        {
            get => _current;
            set => _current = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// Base application data directory.
        /// </summary>
        public static string AppDataDirectory => _current.AppDataDirectory;

        /// <summary>
        /// Local machine application data directory.
        /// </summary>
        public static string LocalDataDirectory => _current.LocalDataDirectory;

        /// <summary>
        /// Directory containing simulation profiles.
        /// </summary>
        public static string ProfilesDirectory => _current.ProfilesDirectory;

        /// <summary>
        /// Directory containing learning cache files.
        /// </summary>
        public static string CacheDirectory => _current.CacheDirectory;

        /// <summary>
        /// Directory containing log files.
        /// </summary>
        public static string LogsDirectory => _current.LogsDirectory;

        /// <summary>
        /// Full file path to appsettings.json.
        /// </summary>
        public static string AppSettingsPath => _current.SettingsFilePath;

        /// <summary>
        /// Full file path to window.json.
        /// </summary>
        public static string WindowSettingsPath => _current.WindowSettingsPath;

        /// <summary>
        /// Returns the cache subdirectory for a specific simulation game.
        /// </summary>
        public static string GetGameCacheDirectory(string gameName) => _current.GetGameCacheDirectory(gameName);

        /// <summary>
        /// Initializes the global storage paths with a specific <see cref="IAppStorage"/> provider.
        /// </summary>
        public static void Initialize(IAppStorage storage)
        {
            Current = storage;
        }

        /// <summary>
        /// Resets the global storage paths to the operating system default provider.
        /// </summary>
        public static void ResetToDefault()
        {
            Current = AppStorage.CreateDefault();
        }

        /// <summary>
        /// Creates an isolated temporary storage instance in the OS temp directory.
        /// </summary>
        public static IAppStorage CreateTemp(string? rootDir = null, string? prefix = null)
        {
            return AppStorage.CreateTempStorage(rootDir, prefix);
        }

        /// <summary>
        /// Creates an explicit Windows storage provider with optional custom roots.
        /// </summary>
        public static IAppStorage CreateWindows(string? myDocuments = null, string? localAppData = null)
        {
            return AppStorage.CreateWindowsStorage(myDocuments, localAppData);
        }

        /// <summary>
        /// Creates an explicit Linux XDG storage provider with optional custom roots.
        /// </summary>
        public static IAppStorage CreateLinux(
            string? homeDir = null,
            string? xdgConfigHome = null,
            string? xdgDataHome = null,
            string? xdgCacheHome = null,
            string? xdgStateHome = null,
            bool readEnvironment = true)
        {
            return AppStorage.CreateLinuxStorage(homeDir, xdgConfigHome, xdgDataHome, xdgCacheHome, xdgStateHome, readEnvironment);
        }
    }
}
