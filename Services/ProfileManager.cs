using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;
using SimRacingHub.Models;
using System.Linq;
using SimRacingHub.Core;

namespace SimRacingHub.Services
{
    public class ProfileManager
    {
        private readonly string _profileDirectory = StoragePaths.ProfilesDirectory;

        public ProfileManager()
        {
            if (!Directory.Exists(_profileDirectory))
            {
                Directory.CreateDirectory(_profileDirectory);
            }

            MigrateLegacyUniversalProfile();
            MigrateLegacyBeamNgProfile();
        }

        private void MigrateLegacyBeamNgProfile()
        {
            try
            {
                string legacyPath = Path.Combine(_profileDirectory, "Universal", "BeamNG", "Default.json");
                string targetPath = Path.Combine(_profileDirectory, "BeamNG.drive", "Default.json");
                if (!File.Exists(legacyPath) || File.Exists(targetPath)) return;

                var profile = JsonConvert.DeserializeObject<Profile>(File.ReadAllText(legacyPath));
                if (profile == null) return;

                int fallbackRate = profile.PollingRate is > 0 ? profile.PollingRate.Value : 200;
                profile.MigrateToCurrentSchema(fallbackRate);
                profile.Name = "BeamNG.drive (Default)";
                SaveFile(targetPath, profile);
                AppLogger.Instance.LogInfo("Migrated legacy 'Universal/BeamNG' profile to 'BeamNG.drive/Default.json'. The original profile was kept as a fallback.");
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError("Failed to migrate legacy BeamNG profile", ex);
            }
        }

        private void MigrateLegacyUniversalProfile()
        {
            try
            {
                string legacyPath = Path.Combine(_profileDirectory, "Universal.json");
                if (File.Exists(legacyPath))
                {
                    string targetDir = Path.Combine(_profileDirectory, "Universal");
                    if (!Directory.Exists(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                    }

                    string targetPath = Path.Combine(targetDir, "Default.json");
                    if (!File.Exists(targetPath))
                    {
                        File.Move(legacyPath, targetPath);
                        AppLogger.Instance.LogInfo("Migrated legacy 'Universal.json' to 'Universal/Default.json'");
                    }
                    else
                    {
                        File.Delete(legacyPath);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError("Failed to migrate legacy Universal.json", ex);
            }
        }

        private const int MaxSegmentLength = 100;

        private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        public static string SanitizeName(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            var invalidChars = Path.GetInvalidFileNameChars().Concat(Path.GetInvalidPathChars()).Distinct();
            string clean = new string(input.Where(c => !invalidChars.Contains(c)).ToArray()).Trim();
            while (clean.Contains("..")) clean = clean.Replace("..", "");

            if (clean.Length > MaxSegmentLength) clean = clean.Substring(0, MaxSegmentLength);

            clean = clean.TrimEnd('.', ' ');
            if (clean.Length == 0) return string.Empty;

            int dot = clean.IndexOf('.');
            string baseName = dot >= 0 ? clean.Substring(0, dot) : clean;
            if (ReservedDeviceNames.Contains(baseName))
            {
                clean = dot >= 0 ? baseName + "_" + clean.Substring(dot) : clean + "_";
            }

            return clean;
        }

        public string GetProfilePath(ProfileContext? context)
        {
            string game = SanitizeName(context?.Game);
            if (string.IsNullOrEmpty(game)) game = "Universal";
            string carClass = SanitizeName(context?.CarClass);
            string car = SanitizeName(context?.Car);

            if (string.IsNullOrEmpty(carClass))
            {
                return Path.Combine(_profileDirectory, game, "Default.json");
            }
            
            if (string.IsNullOrEmpty(car))
                return Path.Combine(_profileDirectory, game, carClass, "Default.json");
            
            return Path.Combine(_profileDirectory, game, carClass, $"{car}.json");
        }

        private (Profile profile, bool isNew, bool wasCorrupt) LoadFile(string path)
        {
            if (!File.Exists(path)) return (new Profile(), true, false);

            try
            {
                string json = File.ReadAllText(path);
                var profile = JsonConvert.DeserializeObject<Profile>(json) ?? new Profile();
                if (profile.OutSteerMin.HasValue && profile.OutSteerMin.Value < 0) profile.OutSteerMin = 0;
                if (profile.OutGasMin.HasValue && profile.OutGasMin.Value < 0) profile.OutGasMin = 0;
                if (profile.OutBrakeMin.HasValue && profile.OutBrakeMin.Value < 0) profile.OutBrakeMin = 0;

                return (profile, false, false);
            }
            catch (Exception ex)
            {
                bool preserved = TryPreserveCorruptFile(path, ex);

                return (Profile.CreateDefault(), preserved, true);
            }
        }

        private static bool TryPreserveCorruptFile(string path, Exception cause)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            AppLogger.Instance.LogError($"Profile '{name}' could not be read", cause);

            try
            {
                string? directory = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(directory))
                {
                    AppLogger.Instance.LogError($"Profile '{name}' was unreadable and its folder could not be determined. The file is left untouched.");
                    return false;
                }

                string target = Path.Combine(directory, $"{name}.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}.json");
                File.Move(path, target, overwrite: false);
                AppLogger.Instance.LogWarning(
                    $"Profile '{name}' was unreadable and has been renamed to '{Path.GetFileName(target)}'. Default settings are used instead.");
                return true;
            }
            catch (Exception moveError)
            {
                AppLogger.Instance.LogError(
                    $"Profile '{name}' was unreadable and could not be moved aside. The file is left untouched and default settings are used for this session.", moveError);
                return false;
            }
        }

        public (Profile active, ResolvedProfile resolved) LoadContext(ProfileContext context)
        {
            var resolved = new ResolvedProfile();
            // Start with hardcoded defaults so we don't have zeros if a profile is empty
            resolved.UpdateFrom(Profile.CreateDefault("Base"));
            
            Profile activeProfile;

            string game = SanitizeName(context?.Game);
            if (string.IsNullOrEmpty(game)) game = "Universal";
            string carClass = SanitizeName(context?.CarClass);
            string car = SanitizeName(context?.Car);

            // 1. Game Level
            string gameDefaultPath = Path.Combine(_profileDirectory, game, "Default.json");
            var (gameProf, gameIsNew, gameWasCorrupt) = LoadFile(gameDefaultPath);
            if (gameIsNew || (!gameWasCorrupt && !gameProf.MouseSensitivity.HasValue))
            {
                gameProf = Profile.CreateDefault(game);
                if (game.Equals("Universal", StringComparison.OrdinalIgnoreCase))
                {
                    gameProf.Name = "Universal";
                }
                else
                {
                    gameProf.Name = $"{game} (Default)";
                }
                SaveFile(gameDefaultPath, gameProf);
            }
            else if (string.IsNullOrEmpty(gameProf.Name))
            {
                gameProf.Name = game.Equals("Universal", StringComparison.OrdinalIgnoreCase) ? "Universal" : $"{game} (Default)";
            }

            int gameRate = Profile.CreateDefault(game).PollingRate ?? 1000;
            bool gameMigrated = !gameIsNew && !gameWasCorrupt && gameProf.MigrateToCurrentSchema(gameRate);
            if (gameMigrated) PersistMigratedProfile(gameDefaultPath, gameProf);
            resolved.UpdateFrom(gameProf);
            activeProfile = gameProf;

            // 2. Class Level
            if (!string.IsNullOrEmpty(carClass))
            {
                string classPath = Path.Combine(_profileDirectory, game, carClass, "Default.json");
                var (classProf, classIsNew, classWasCorrupt) = LoadFile(classPath);
                if (classIsNew)
                {
                    classProf = resolved.ToProfile($"{carClass} (Default)");
                }
                else
                {
                    bool classMigrated = !classWasCorrupt && classProf.MigrateToCurrentSchema(resolved.PollingRate > 0 ? resolved.PollingRate : gameRate);
                    if (classMigrated) PersistMigratedProfile(classPath, classProf);
                    resolved.UpdateFrom(classProf);
                    FillMissingFromResolved(classProf, resolved);
                }

                // Polling rate is a game-wide setting: always enforce game-level rate
                classProf.PollingRate = gameProf.PollingRate;
                resolved.PollingRate = gameProf.PollingRate ?? gameRate;
                activeProfile = classProf;

                // 3. Car Level
                if (!string.IsNullOrEmpty(car))
                {
                    string carPath = Path.Combine(_profileDirectory, game, carClass, $"{car}.json");
                    var (carProf, carIsNew, carWasCorrupt) = LoadFile(carPath);
                    if (carIsNew)
                    {
                        carProf = resolved.ToProfile(car);
                    }
                    else
                    {
                        bool carMigrated = !carWasCorrupt && carProf.MigrateToCurrentSchema(resolved.PollingRate > 0 ? resolved.PollingRate : gameRate);
                        if (carMigrated) PersistMigratedProfile(carPath, carProf);
                        resolved.UpdateFrom(carProf);
                        FillMissingFromResolved(carProf, resolved);
                    }

                    // Polling rate is a game-wide setting: always enforce game-level rate
                    carProf.PollingRate = gameProf.PollingRate;
                    resolved.PollingRate = gameProf.PollingRate ?? gameRate;
                    activeProfile = carProf;
                }
            }

            return (activeProfile, resolved);
        }

        private static void FillMissingFromResolved(Profile prof, ResolvedProfile resolved)
        {
            var defaults = resolved.ToProfile(prof.Name);
            var props = typeof(Profile).GetProperties();
            foreach (var prop in props)
            {
                if (prop.CanWrite && prop.Name != "Name")
                {
                    var val = prop.GetValue(prof);
                    if (val == null)
                    {
                        prop.SetValue(prof, prop.GetValue(defaults));
                    }
                }
            }
        }

        private bool SaveFile(string path, Profile profile)
        {
            string tmp = path + ".tmp";
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = JsonConvert.SerializeObject(profile, Formatting.Indented);
                File.WriteAllText(tmp, json);
                File.Move(tmp, path, overwrite: true);
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError($"Failed to save profile '{Path.GetFileNameWithoutExtension(path)}'", ex);

                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                return false;
            }
        }

        /// <summary>
        /// Persists a one-time schema migration without destroying the original
        /// profile. The backup is kept next to the JSON and is never overwritten.
        /// If the backup cannot be created, the migrated profile is used only in
        /// memory and the source file is left untouched.
        /// </summary>
        private bool PersistMigratedProfile(string path, Profile profile)
        {
            if (!File.Exists(path)) return SaveFile(path, profile);

            string backupPath = path + ".schema-v1.bak";
            try
            {
                if (!File.Exists(backupPath))
                {
                    File.Copy(path, backupPath, overwrite: false);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError(
                    $"Profile '{Path.GetFileNameWithoutExtension(path)}' was upgraded in memory, but its migration backup could not be created. The original JSON was not changed.", ex);
                return false;
            }

            if (!SaveFile(path, profile)) return false;

            AppLogger.Instance.LogInfo(
                $"Migrated profile '{Path.GetFileNameWithoutExtension(path)}' to schema {Profile.CurrentSchemaVersion}. Backup: '{Path.GetFileName(backupPath)}'.");
            return true;
        }

        public bool SaveContext(ProfileContext? context, Profile profile)
        {
            int fallbackRate = Profile.CreateDefault(context?.Game ?? "Universal").PollingRate ?? 1000;
            if (profile.MigrateToCurrentSchema(fallbackRate))
            {
                AppLogger.Instance.LogInfo($"Migrated profile '{profile.Name}' to schema {Profile.CurrentSchemaVersion}.");
            }

            string game = SanitizeName(context?.Game);
            if (string.IsNullOrEmpty(game)) game = "Universal";

            // If saving a Class or Car profile, synchronize the game-level PollingRate so all vehicles share it
            if (profile.PollingRate.HasValue && !string.IsNullOrEmpty(context?.CarClass))
            {
                string gameDefaultPath = Path.Combine(_profileDirectory, game, "Default.json");
                var (gameProf, gameIsNew, gameWasCorrupt) = LoadFile(gameDefaultPath);
                if (!gameWasCorrupt && gameProf.PollingRate != profile.PollingRate)
                {
                    if (gameIsNew || !gameProf.MouseSensitivity.HasValue)
                    {
                        gameProf = Profile.CreateDefault(game);
                        gameProf.Name = game.Equals("Universal", StringComparison.OrdinalIgnoreCase) ? "Universal" : $"{game} (Default)";
                    }
                    gameProf.PollingRate = profile.PollingRate;
                    SaveFile(gameDefaultPath, gameProf);
                }
            }

            string path = GetProfilePath(context);
            return SaveFile(path, profile);
        }

        public List<string> GetAvailableGames()
        {
            var games = new List<string>();
            if (Directory.Exists(_profileDirectory))
            {
                foreach (var dir in Directory.GetDirectories(_profileDirectory))
                {
                    var name = Path.GetFileName(dir);
                    if (!name.Equals("Universal", StringComparison.OrdinalIgnoreCase))
                    {
                        games.Add(name);
                    }
                }
            }
            return games;
        }

        public List<string> GetAvailableClasses(string? game)
        {
            var classes = new List<string>();
            if (string.IsNullOrEmpty(game)) return classes;
            
            var path = Path.Combine(_profileDirectory, game);
            if (Directory.Exists(path))
            {
                foreach (var dir in Directory.GetDirectories(path))
                {
                    classes.Add(Path.GetFileName(dir));
                }
            }
            return classes;
        }

        public List<string> GetAvailableCars(string? game, string? carClass)
        {
            var cars = new List<string>();
            if (string.IsNullOrEmpty(game) || string.IsNullOrEmpty(carClass)) return cars;
            
            var path = Path.Combine(_profileDirectory, game, carClass);
            if (Directory.Exists(path))
            {
                foreach (var file in Directory.GetFiles(path, "*.json"))
                {
                    var name = Path.GetFileNameWithoutExtension(file);

                    if (name != "Default" && !name.Contains(".corrupt-", StringComparison.Ordinal))
                    {
                        cars.Add(name);
                    }
                }
            }
            return cars;
        }
    }
}
