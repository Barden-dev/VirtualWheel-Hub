using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SimRacingHub.Application.Features.Profiles;

/// <summary>One row in the virtualized navigator. Expanding never loads a profile.</summary>
public sealed partial class ProfileBrowserRow : ObservableObject
{
    public ProfileItemViewModel Profile { get; }
    public string Key { get; }
    public string Title { get; }
    public string Subtitle { get; }
    public string Kind { get; }
    public int Depth { get; internal set; }
    public bool IsNavigationGroup { get; internal set; }
    public bool IsCar => !IsNavigationGroup && !string.IsNullOrEmpty(Profile.Context.Car);
    public bool IsGame => !IsNavigationGroup && !IsCar && string.IsNullOrEmpty(Profile.Context.CarClass);
    public bool IsClass => !IsCar && !IsGame;
    public bool IsDefault => Profile.IsDefault;
    public bool ShowsSubtitle => !string.IsNullOrEmpty(Subtitle);
    public List<ProfileBrowserRow> Children { get; } = new();
    public bool CanExpand => Children.Count > 0;
    public int CarProfileCount { get; internal set; }
    public bool HasCarProfiles => CarProfileCount > 0;
    [ObservableProperty] private bool _isExpanded;

    internal ProfileBrowserRow(ProfileItemViewModel profile, string key, string title, string subtitle, string kind, int depth)
    { Profile = profile; Key = key; Title = title; Subtitle = subtitle; Kind = kind; Depth = depth; }
}
