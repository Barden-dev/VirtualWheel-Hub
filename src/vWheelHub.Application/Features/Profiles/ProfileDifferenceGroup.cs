using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using SimRacingHub.Application.Features.Shell;

namespace SimRacingHub.Application.Features.Profiles;

public sealed record ProfileSettingDifference(string PropertyName, string Label, string CurrentValue, string BaselineValue);

public sealed partial class ProfileDifferenceGroup : ObservableObject
{
    public string Id { get; }
    public string Title { get; }
    public string Summary { get; }
    public PageId Page { get; }
    public IReadOnlyList<ProfileSettingDifference> Differences { get; }
    public bool HasMultiple => Differences.Count > 1;
    [ObservableProperty] private bool _isExpanded;
    public ProfileDifferenceGroup(string id, string title, string summary, PageId page, IReadOnlyList<ProfileSettingDifference> differences)
    { Id = id; Title = title; Summary = summary; Page = page; Differences = differences; }
}
