using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimRacingHub.Models;

namespace SimRacingHub.Application.Features.Profiles;

public partial class ProfilesViewModel
{
    private readonly List<ProfileBrowserRow> _browserRoots = new();
    private readonly Dictionary<string, ProfileBrowserRow> _searchRows = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _expandedGroups = new(StringComparer.Ordinal);
    private bool _refreshingProfiles;
    public ObservableCollection<ProfileBrowserRow> BrowserRows { get; } = new();
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSearchingProfiles))]
    private string _profileSearchQuery = string.Empty;
    [ObservableProperty] private ProfileBrowserRow? _selectedBrowserRow;
    public bool IsSearchingProfiles => !string.IsNullOrWhiteSpace(ProfileSearchQuery);
    public bool HasBrowserResults => BrowserRows.Count > 0;
    public string ProfileChangesText => _localizationManager.GetString(HasUnsavedChanges ? "Profiles_UnsavedYes" : "Profiles_UnsavedNo");
    public string BrowserSummary => string.Format(_localizationManager.GetString(
        IsSearchingProfiles ? "Profiles_SearchCount" : "Profiles_TotalCount"),
        IsSearchingProfiles ? BrowserRows.Count : AvailableProfiles.Count);

    private static string BrowserKey(ProfileContext c) => $"{c.Game}\u001f{c.CarClass}\u001f{c.Car}";
    private string BrowserTitle(ProfileItemViewModel p) => p.IsDefault ? p.LocalizedName
        : !string.IsNullOrEmpty(p.Context.Car) ? DetectedProfileNames.Car(p.Context, p.Name)!
        : !string.IsNullOrEmpty(p.Context.CarClass) ? DetectedProfileNames.Class(p.Context)!
        : p.Context.Game ?? p.LocalizedName;
    private string BrowserKind(ProfileItemViewModel p) => _localizationManager.GetString(
        p.IsDefault ? "Profiles_BaseProfile" : !string.IsNullOrEmpty(p.Context.Car) ? "Profiles_CarProfile"
        : !string.IsNullOrEmpty(p.Context.CarClass) ? "Profiles_ClassProfile" : "Profiles_GameProfile");

    private void RebuildBrowser()
    {
        if (_refreshingProfiles) return;
        _browserRoots.Clear();
        _searchRows.Clear();
        var nodes = AvailableProfiles.DistinctBy(p => BrowserKey(p.Context), StringComparer.Ordinal).ToDictionary(p => BrowserKey(p.Context), p =>
            new ProfileBrowserRow(p, BrowserKey(p.Context), BrowserTitle(p), string.Empty, BrowserKind(p),
                !string.IsNullOrEmpty(p.Context.Car) ? (string.IsNullOrEmpty(p.Context.CarClass) ? 1 : 2)
                : !string.IsNullOrEmpty(p.Context.CarClass) ? 1 : 0), StringComparer.Ordinal);
        var groups = new Dictionary<string, ProfileBrowserRow>(StringComparer.Ordinal);
        ProfileBrowserRow Group(ProfileBrowserRow game, string key, string title)
        {
            if (groups.TryGetValue(key, out var existing)) return existing;
            var group = new ProfileBrowserRow(game.Profile, key, title, "", "", 1) { IsNavigationGroup = true };
            groups.Add(key, group); game.Children.Add(group); return group;
        }
        foreach (var node in nodes.Values)
        {
            var c = node.Profile.Context;
            if (string.Equals(c.Game, "Forza Motorsport", StringComparison.OrdinalIgnoreCase) &&
                nodes.TryGetValue(BrowserKey(new ProfileContext(c.Game)), out var forzaRoot))
            {
                if (node.IsCar)
                {
                    node.Depth = 2;
                    string make = ForzaCarCatalog.Default.Find(c.Car)?.Make ?? _localizationManager.GetString("Profiles_UnknownManufacturer", "Other cars");
                    Group(forzaRoot, "forza-make:" + make, make).Children.Add(node);
                    continue;
                }
                if (node.IsClass)
                {
                    node.Depth = 2;
                    Group(forzaRoot, "forza-legacy-classes", _localizationManager.GetString("Profiles_ClassDefaults", "Class settings")).Children.Add(node);
                    continue;
                }
            }
            string? parentKey = !string.IsNullOrEmpty(c.Car) ? BrowserKey(new ProfileContext(c.Game, c.CarClass))
                : !string.IsNullOrEmpty(c.CarClass) ? BrowserKey(new ProfileContext(c.Game)) : null;
            if (parentKey != null && nodes.TryGetValue(parentKey, out var parent)) parent.Children.Add(node);
            else _browserRoots.Add(node);
        }
        void SortAndCount(List<ProfileBrowserRow> siblings)
        {
            siblings.Sort((a, b) => a.IsDefault != b.IsDefault ? (a.IsDefault ? -1 : 1)
                : StringComparer.CurrentCultureIgnoreCase.Compare(a.Title, b.Title));
            foreach (var node in siblings)
            {
                SortAndCount(node.Children);
                node.CarProfileCount = node.Children.Sum(c => (c.IsCar ? 1 : 0) + c.CarProfileCount);
                node.IsExpanded = _expandedGroups.GetValueOrDefault(node.Key);
            }
        }
        SortAndCount(_browserRoots);
        ExpandSelectedBranch();
        UpdateBrowserRows();
    }

    private void ExpandSelectedBranch()
    {
        var c = SelectedProfile?.Context ?? CurrentContext;
        foreach (var root in _browserRoots)
        {
            if (!string.Equals(root.Profile.Context.Game, c.Game, StringComparison.Ordinal)) continue;
            if (!string.IsNullOrEmpty(c.CarClass) || !string.IsNullOrEmpty(c.Car))
            { root.IsExpanded = true; _expandedGroups[root.Key] = true; }
            foreach (var child in root.Children.Where(n => n.IsNavigationGroup
                ? n.Children.Any(car => car.Profile.Context.Equals(c))
                : n.IsClass && n.Profile.Context.CarClass == c.CarClass))
                if (!string.IsNullOrEmpty(c.Car)) { child.IsExpanded = true; _expandedGroups[child.Key] = true; }
        }
    }

    private void UpdateBrowserRows()
    {
        var visible = new List<ProfileBrowserRow>();
        var tokens = ProfileSearchQuery.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        bool Matches(ProfileItemViewModel p)
        {
            var game = p.Context.Game ?? string.Empty;
            var alias = game.ToUpperInvariant() switch
            {
                "ASSETTO CORSA" => "AC", "ASSETTO CORSA COMPETIZIONE" => "ACC",
                "ASSETTO CORSA EVO" => "ACE", "LE MANS ULTIMATE" => "LMU", _ => string.Empty
            };
            var text = $"{p.Name} {game} {alias} {p.Context.CarClass} {p.Context.Car} {p.LocalizedName} {ForzaCarCatalog.Default.Find(p.Context.Car)?.Make}";
            return tokens.All(t => text.Contains(t, StringComparison.OrdinalIgnoreCase));
        }
        void AddRows(IEnumerable<ProfileBrowserRow> nodes)
        {
            foreach (var node in nodes)
            {
                if (IsSearchingProfiles)
                {
                    if (!node.IsNavigationGroup && Matches(node.Profile))
                    {
                        // Selection must not replace search results underneath the ListBox selection model.
                        // Keep each projection for the lifetime of its underlying profile list.
                        if (!_searchRows.TryGetValue(node.Key, out var result))
                        {
                            var c = node.Profile.Context;
                            var path = DetectedProfileNames.Path(new ProfileContext(c.Game, c.CarClass, c.Car));
                            result = new ProfileBrowserRow(node.Profile, node.Key, node.Title,
                                node.IsDefault ? node.Kind : path, node.Kind, 0);
                            _searchRows.Add(node.Key, result);
                        }
                        visible.Add(result);
                    }
                    AddRows(node.Children);
                }
                else { visible.Add(node); if (node.IsExpanded) AddRows(node.Children); }
            }
        }
        AddRows(_browserRoots);
        // Keep unchanged containers and scroll position when a branch opens/closes.
        int prefix = 0, suffix = 0;
        while (prefix < BrowserRows.Count && prefix < visible.Count && ReferenceEquals(BrowserRows[prefix], visible[prefix])) prefix++;
        while (suffix < BrowserRows.Count - prefix && suffix < visible.Count - prefix &&
            ReferenceEquals(BrowserRows[BrowserRows.Count - 1 - suffix], visible[visible.Count - 1 - suffix])) suffix++;
        for (int i = BrowserRows.Count - suffix - 1; i >= prefix; i--) BrowserRows.RemoveAt(i);
        for (int i = prefix; i < visible.Count - suffix; i++) BrowserRows.Insert(i, visible[i]);
        SynchronizeBrowserSelection();
        OnPropertyChanged(nameof(BrowserSummary));
        OnPropertyChanged(nameof(HasBrowserResults));
    }

    private void SynchronizeBrowserSelection() => SelectedBrowserRow = SelectedProfile == null ? null
        : BrowserRows.FirstOrDefault(r => r.Key == BrowserKey(SelectedProfile.Context));
    partial void OnProfileSearchQueryChanged(string value) => UpdateBrowserRows();

    [RelayCommand]
    public void ToggleProfileGroup(ProfileBrowserRow? row)
    {
        if (row?.CanExpand != true || IsSearchingProfiles) return;
        row.IsExpanded = !row.IsExpanded;
        _expandedGroups[row.Key] = row.IsExpanded;
        UpdateBrowserRows();
    }
    [RelayCommand]
    public void CollapseProfileGroups()
    {
        _expandedGroups.Clear();
        void Collapse(IEnumerable<ProfileBrowserRow> nodes)
        { foreach (var node in nodes) { node.IsExpanded = false; Collapse(node.Children); } }
        Collapse(_browserRoots);
        UpdateBrowserRows();
    }
    [RelayCommand] public void ClearProfileSearch() => ProfileSearchQuery = string.Empty;
    [RelayCommand]
    public void RevealSelectedProfile()
    {
        ProfileSearchQuery = string.Empty;
        ExpandSelectedBranch();
        UpdateBrowserRows();
    }
}
