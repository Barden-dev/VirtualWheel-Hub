using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using SimRacingHub.Core;
using SimRacingHub.Models;

namespace SimRacingHub.Services;

/// <summary>Resolves layered settings and comparison baselines using an explicit file store.</summary>
internal sealed class ProfileResolver
{
    private readonly ProfileFileStore _store;
    private readonly bool _materializeMissingValues;
    private readonly HashSet<string> _preserveInheritedNull;
    internal ProfileResolver(ProfileFileStore store, bool materializeMissingValues, params string[] preserveInheritedNull)
    {
        _store = store;
        _materializeMissingValues = materializeMissingValues;
        _preserveInheritedNull = new(preserveInheritedNull, StringComparer.Ordinal);
    }
    public (Profile active, ResolvedProfile resolved) LoadContext(ProfileContext context)
    {
        var resolved = new ResolvedProfile();
        // Start with hardcoded defaults so we don't have zeros if a profile is empty
        resolved.UpdateFrom(ProfileDefaults.CreateDefault("Base"));

        Profile activeProfile;

        string game = ProfileFileStore.ResolveExistingDirectory(_store.RootDirectory, context?.Game);
        if (string.IsNullOrEmpty(game)) game = "Universal";
        string carClass = ProfileFileStore.ResolveExistingDirectory(Path.Combine(_store.RootDirectory, game), context?.CarClass);
        string car = context?.Car ?? "";

        // 1. Game Level
        string gameDefaultPath = Path.Combine(_store.RootDirectory, game, "Default.json");
        var (gameProf, gameIsNew, gameWasCorrupt) = _store.LoadFile(gameDefaultPath);
        if (gameIsNew || (!gameWasCorrupt && !gameProf.MouseSensitivity.HasValue))
        {
            gameProf = ProfileDefaults.CreateDefault(game);
            if (game.Equals("Universal", StringComparison.OrdinalIgnoreCase))
            {
                gameProf.Name = "Universal";
            }
            else
            {
                gameProf.Name = $"{game} (Default)";
            }
            _store.SaveFile(gameDefaultPath, gameProf);
        }
        else if (string.IsNullOrEmpty(gameProf.Name))
        {
            gameProf.Name = game.Equals("Universal", StringComparison.OrdinalIgnoreCase) ? "Universal" : $"{game} (Default)";
        }

        int gameRate = ProfileDefaults.CreateDefault(game).PollingRate ?? 1000;
        bool gameMigrated = !gameIsNew && !gameWasCorrupt && gameProf.MigrateToCurrentSchema(gameRate);
        if (gameMigrated) _store.PersistMigratedProfile(gameDefaultPath, gameProf);
        resolved.UpdateFrom(gameProf);
        activeProfile = gameProf;

        // 2. Class Level
        if (!string.IsNullOrEmpty(carClass))
        {
            string classPath = Path.Combine(_store.RootDirectory, game, carClass, "Default.json");
            var (classProf, classIsNew, classWasCorrupt) = _store.LoadFile(classPath);
            if (classIsNew)
            {
                classProf = resolved.ToProfile($"{carClass} (Default)");
            }
            else
            {
                bool classMigrated = !classWasCorrupt && classProf.MigrateToCurrentSchema(resolved.PollingRate > 0 ? resolved.PollingRate : gameRate);
                if (classMigrated) _store.PersistMigratedProfile(classPath, classProf);
                resolved.UpdateFrom(classProf);
                FillMissingFromResolved(classProf, resolved);
            }

            // Polling rate is a game-wide setting: always enforce game-level rate
            classProf.PollingRate = gameProf.PollingRate;
            resolved.PollingRate = gameProf.PollingRate ?? gameRate;
            activeProfile = classProf;

        }

        // 3. Car level may be stored directly under the game, without a class.
        if (!string.IsNullOrEmpty(car))
        {
            string carPath = _store.GetProfilePath(new ProfileContext(game, carClass, car));
            var (carProf, carIsNew, carWasCorrupt) = _store.LoadFile(carPath);
            if (carIsNew)
            {
                carProf = resolved.ToProfile(car);
            }
            else
            {
                bool carMigrated = !carWasCorrupt && carProf.MigrateToCurrentSchema(resolved.PollingRate > 0 ? resolved.PollingRate : gameRate);
                if (carMigrated) _store.PersistMigratedProfile(carPath, carProf);
                resolved.UpdateFrom(carProf);
                FillMissingFromResolved(carProf, resolved);
            }

            // Polling rate is a game-wide setting: always enforce game-level rate
            carProf.PollingRate = gameProf.PollingRate;
            resolved.PollingRate = gameProf.PollingRate ?? gameRate;
            activeProfile = carProf;
        }

        return (activeProfile, resolved);
    }

    /// <summary>Reads a comparison baseline without creating, migrating or rewriting profile files.</summary>
    public ProfileComparisonBaseline GetComparisonBaseline(ProfileContext context)
    {
        var game = ProfileFileStore.ResolveExistingDirectory(_store.RootDirectory, context.Game);
        if (string.IsNullOrEmpty(game)) game = "Universal";
        var carClass = ProfileFileStore.ResolveExistingDirectory(Path.Combine(_store.RootDirectory, game), context.CarClass);
        bool hasParent = !string.IsNullOrEmpty(carClass) || !string.IsNullOrEmpty(context.Car);
        var resolved = new ResolvedProfile();
        resolved.UpdateFrom(ProfileDefaults.CreateDefault("Base"));
        resolved.UpdateFrom(ProfileDefaults.CreateDefault(game));
        if (!hasParent) return new(resolved, null, true);

        Profile? Read(string path, int rate)
        {
            var profile = _store.ReadExisting(path);
            if (profile == null) return null;
            profile.MigrateToCurrentSchema(rate); // In-memory conversion only.
            if (profile.OutSteerMin < 0) profile.OutSteerMin = 0;
            if (profile.OutGasMin < 0) profile.OutGasMin = 0;
            if (profile.OutBrakeMin < 0) profile.OutBrakeMin = 0;
            return profile;
        }
        var parent = new ProfileContext(game);
        try
        {
            var gameProfile = Read(_store.GetProfilePath(parent), resolved.PollingRate);
            if (gameProfile?.MouseSensitivity.HasValue == true) resolved.UpdateFrom(gameProfile);
            int gameRate = resolved.PollingRate;
            if (!string.IsNullOrEmpty(context.Car) && !string.IsNullOrEmpty(carClass))
            {
                parent = new ProfileContext(game, carClass);
                var classProfile = Read(_store.GetProfilePath(parent), gameRate);
                if (classProfile != null) resolved.UpdateFrom(classProfile);
                resolved.PollingRate = gameRate;
            }
            return new(resolved, parent, true);
        }
        catch (Exception ex)
        {
            AppLogger.Instance.LogError("Could not read profile comparison baseline", ex);
            return new(resolved, parent, false);
        }
    }

    private void FillMissingFromResolved(Profile prof, ResolvedProfile resolved)
    {
        if (!_materializeMissingValues) return;
        var defaults = resolved.ToProfile(prof.Name);
        var props = typeof(Profile).GetProperties();
        foreach (var prop in props)
        {
            if (prop.CanWrite && prop.Name != "Name" && !_preserveInheritedNull.Contains(prop.Name))
            {
                var val = prop.GetValue(prof);
                if (val == null)
                {
                    prop.SetValue(prof, prop.GetValue(defaults));
                }
            }
        }
    }

    public bool SaveContext(ProfileContext? context, Profile profile)
    {
        int fallbackRate = ProfileDefaults.CreateDefault(context?.Game ?? "Universal").PollingRate ?? 1000;
        if (profile.MigrateToCurrentSchema(fallbackRate))
        {
            AppLogger.Instance.LogInfo($"Migrated profile '{profile.Name}' to schema {Profile.CurrentSchemaVersion}.");
        }

        string game = ProfileFileStore.ResolveExistingDirectory(_store.RootDirectory, context?.Game);
        if (string.IsNullOrEmpty(game)) game = "Universal";

        // If saving a Class or Car profile, synchronize the game-level PollingRate so all vehicles share it
        if (profile.PollingRate.HasValue && (!string.IsNullOrEmpty(context?.CarClass) || !string.IsNullOrEmpty(context?.Car)))
        {
            string gameDefaultPath = Path.Combine(_store.RootDirectory, game, "Default.json");
            var (gameProf, gameIsNew, gameWasCorrupt) = _store.LoadFile(gameDefaultPath);
            if (!gameWasCorrupt && gameProf.PollingRate != profile.PollingRate)
            {
                if (gameIsNew || !gameProf.MouseSensitivity.HasValue)
                {
                    gameProf = ProfileDefaults.CreateDefault(game);
                    gameProf.Name = game.Equals("Universal", StringComparison.OrdinalIgnoreCase) ? "Universal" : $"{game} (Default)";
                }
                gameProf.PollingRate = profile.PollingRate;
                if (!_store.SaveFile(gameDefaultPath, gameProf)) return false;
            }
        }

        string path = _store.GetProfilePath(context);
        return _store.SaveFile(path, profile);
    }

}
