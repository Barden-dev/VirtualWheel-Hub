using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using SimRacingHub.Core;
using SimRacingHub.Models;

namespace SimRacingHub.Services;

/// <summary>Owns profile files, portable paths, recovery backups, and atomic replacement.</summary>
internal sealed class ProfileFileStore
{
    internal string RootDirectory => _profileDirectory;
    internal Profile? ReadExisting(string path)
    {
        if (!File.Exists(path)) return null;
        return JsonConvert.DeserializeObject<Profile>(File.ReadAllText(path))
            ?? throw new JsonException("Empty profile document.");
    }
    private readonly string _profileDirectory;

    internal ProfileFileStore(string profileDirectory)
    {
        _profileDirectory = string.IsNullOrWhiteSpace(profileDirectory)
            ? StoragePaths.ProfilesDirectory
            : profileDirectory;

        try
        {
            if (!Directory.Exists(_profileDirectory))
            {
                Directory.CreateDirectory(_profileDirectory);
            }
        }
        catch (Exception ex)
        {
            AppLogger.Instance.LogError($"ProfileManager could not create directory '{_profileDirectory}'", ex);
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

    public static string SanitizeName(string? input) => SanitizeName(input, portable: true);

    private static string SanitizeName(string? input, bool portable)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var invalidChars = Path.GetInvalidFileNameChars().Concat(Path.GetInvalidPathChars())
            .Concat(portable ? @"<>:""/\|?*" : string.Empty).Distinct();
        string clean = new string(input.Where(c => (!portable || !char.IsControl(c)) && !invalidChars.Contains(c)).ToArray()).Trim();
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

    // Read and save existing Linux-only names in place; new names are portable to Windows.
    internal static string ResolveExistingDirectory(string parent, string? input)
    {
        string clean = SanitizeName(input), legacy = SanitizeName(input, portable: false);
        return legacy != clean && legacy.Length > 0 && Directory.Exists(Path.Combine(parent, legacy)) ? legacy : clean;
    }

    private static string ResolveExistingFile(string parent, string? input)
    {
        string clean = SanitizeName(input), legacy = SanitizeName(input, portable: false);
        return legacy != clean && legacy.Length > 0 && File.Exists(Path.Combine(parent, legacy + ".json")) ? legacy : clean;
    }

    public string GetProfilePath(ProfileContext? context)
    {
        string game = ResolveExistingDirectory(_profileDirectory, context?.Game);
        if (string.IsNullOrEmpty(game)) game = "Universal";
        string parent = Path.Combine(_profileDirectory, game);
        string carClass = ResolveExistingDirectory(parent, context?.CarClass);
        if (!string.IsNullOrEmpty(carClass)) parent = Path.Combine(parent, carClass);
        string car = ResolveExistingFile(parent, context?.Car);
        return Path.Combine(parent, string.IsNullOrEmpty(car) ? "Default.json" : car + ".json");
    }

    internal (Profile profile, bool isNew, bool wasCorrupt) LoadFile(string path)
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

            return (ProfileDefaults.CreateDefault(), preserved, true);
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

    internal bool SaveFile(string path, Profile profile)
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
    internal bool PersistMigratedProfile(string path, Profile profile)
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

        var path = Path.Combine(_profileDirectory, ResolveExistingDirectory(_profileDirectory, game));
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
        if (string.IsNullOrEmpty(game)) return cars;

        var gamePath = Path.Combine(_profileDirectory, ResolveExistingDirectory(_profileDirectory, game));
        var path = Path.Combine(gamePath, ResolveExistingDirectory(gamePath, carClass));
        if (Directory.Exists(path))
        {
            foreach (var file in Directory.GetFiles(path, "*.json"))
            {
                var name = Path.GetFileNameWithoutExtension(file);

                if (!name.Equals("Default", StringComparison.OrdinalIgnoreCase) && !name.Contains(".corrupt-", StringComparison.OrdinalIgnoreCase))
                {
                    cars.Add(name);
                }
            }
        }
        return cars;
    }

}
