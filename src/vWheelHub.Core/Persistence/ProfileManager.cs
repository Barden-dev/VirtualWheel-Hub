using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using SimRacingHub.Core;
using SimRacingHub.Models;

namespace SimRacingHub.Services;

/// <summary>Public entry point coordinating profile persistence and inheritance.</summary>
public class ProfileManager
{
    private readonly ProfileFileStore _store;
    private readonly ProfileResolver _resolver;
    public string ProfileDirectory => _store.RootDirectory;
    public ProfileManager() : this(StoragePaths.ProfilesDirectory) { }
    public ProfileManager(IAppStorage appStorage) : this(appStorage?.ProfilesDirectory ?? StoragePaths.ProfilesDirectory) { }
    public ProfileManager(string profileDirectory)
    {
        _store = new ProfileFileStore(profileDirectory);
        _resolver = new ProfileResolver(_store, materializeMissingValues: false);
    }
    public static string SanitizeName(string? input) => ProfileFileStore.SanitizeName(input);
    internal static string ResolveExistingDirectory(string parent, string? input) => ProfileFileStore.ResolveExistingDirectory(parent, input);
    public string GetProfilePath(ProfileContext? context) => _store.GetProfilePath(context);
    public (Profile active, ResolvedProfile resolved) LoadContext(ProfileContext context) => _resolver.LoadContext(context);
    public ProfileComparisonBaseline GetComparisonBaseline(ProfileContext context) => _resolver.GetComparisonBaseline(context);
    public bool SaveContext(ProfileContext? context, Profile profile) => _resolver.SaveContext(context, profile);
    public List<string> GetAvailableGames() => _store.GetAvailableGames();
    public List<string> GetAvailableClasses(string? game) => _store.GetAvailableClasses(game);
    public List<string> GetAvailableCars(string? game, string? carClass) => _store.GetAvailableCars(game, carClass);
}
