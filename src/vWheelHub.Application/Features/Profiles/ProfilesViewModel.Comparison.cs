using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using CommunityToolkit.Mvvm.Input;
using SimRacingHub.Application.Features.Shell;
using SimRacingHub.Core;
using SimRacingHub.Models;
using SimRacingHub.Services;

namespace SimRacingHub.Application.Features.Profiles;
public partial class ProfilesViewModel
{
    private ProfileComparisonBaseline? _comparisonBaseline;
    private string? _comparisonContextKey;
    public ObservableCollection<ProfileDifferenceGroup> DifferenceGroups { get; } = new();
    public bool HasProfileDifferences => DifferenceGroups.Count > 0;
    public bool IsComparisonAvailable => _comparisonBaseline?.IsAvailable == true;
    public bool ShowsMatchingProfile => IsComparisonAvailable && !HasProfileDifferences;
    public string ComparisonBaseText => string.Format(_localizationManager.GetString("Summary_ComparedWith"), _comparisonBaseline?.Context?.DisplayPath ?? _localizationManager.GetString("Summary_FactoryDefaults"));

    public event Action<PageId>? OpenSettingsRequested;
    private static readonly PropertyInfo[] ComparisonProperties = typeof(ResolvedProfile).GetProperties().Where(p => p.CanRead && p.CanWrite && p.Name != nameof(Profile.Name) && typeof(Profile).GetProperty(p.Name)is { CanWrite: true } stored && !Attribute.IsDefined(stored, typeof(Newtonsoft.Json.JsonIgnoreAttribute))).ToArray();
    private static (string Id, string Label, PageId Page) DifferenceSection(string name)
    {
        if (name is "PauseControlsOnFocusLoss" or "ReleaseCursorOnFocusLoss") return ("Focus", "Overview_WindowFocus", PageId.Dashboard);
        ;
        ;
        if (name.StartsWith("Out", StringComparison.Ordinal) || name.StartsWith("VJoy", StringComparison.Ordinal) || name is "UseHubVJoyDevice" or "VirtualButtons" || name.StartsWith("Invert", StringComparison.Ordinal) || name == "PollingRate")
            return ("Output", "Nav_VJoyAxes", PageId.Output);
        ;
        ;
        ;
        ;
        if (name.StartsWith("Key", StringComparison.Ordinal) && name.EndsWith('1') || name.StartsWith("Key", StringComparison.Ordinal) && name.EndsWith('2'))
            return ("Keys", "Nav_KeyBindings", PageId.KeyBindings);
        return ("Steering", "Nav_Steering", PageId.Steering);
    }

    private static object? DisplaySetting(Profile profile, PropertyInfo property)
    {
        var seconds = typeof(Profile).GetProperty(property.Name + "Seconds");
        return seconds != null ? seconds.GetValue(profile) : typeof(Profile).GetProperty(property.Name)!.GetValue(profile);
    }

    private static bool SameSetting(object? current, object? baseline)
    {
        if (current is double a && baseline is double b)
            return a.Equals(b) || Math.Abs(a - b) <= 1e-8 * Math.Max(1, Math.Max(Math.Abs(a), Math.Abs(b)));
        return Equals(current, baseline);
    }

    private string FormatSetting(string property, object? value)
    {
        var culture = CultureInfo.GetCultureInfo(_localizationManager.CurrentCultureCode);
        if (value is bool flag)
            return _localizationManager.GetString(flag ? "Summary_Enabled" : "Summary_Disabled");
        if (property.StartsWith("Key", StringComparison.Ordinal) && (property.EndsWith('1') || property.EndsWith('2')) && value is int code)
            return code == 0 ? _localizationManager.GetString("Summary_Unassigned") : KeyNameFormatter.Format(code);
        ;
        string result = value?.GetType().IsEnum == true ? value.ToString()! : value is IFormattable number ? number.ToString("0.###", culture) : value?.ToString() ?? "—";
        if (typeof(Profile).GetProperty(property + "Seconds") != null)
            return string.Format(culture, _localizationManager.GetString("Summary_Seconds"), result);
        if (property == "PollingRate" || property.EndsWith("Freq", StringComparison.Ordinal))
            return string.Format(culture, _localizationManager.GetString("Ui_HertzFormat"), result);
        if (property.EndsWith("Ms", StringComparison.Ordinal) || property.EndsWith("Dur", StringComparison.Ordinal))
            return string.Format(culture, _localizationManager.GetString("Summary_Milliseconds"), result);
        if (property is "CarSteeringLock" or "GlobalSteeringRange")
            return result + "°";
        return result;
    }

    private void RefreshComparison(bool reloadBaseline = false)
    {
        var key = BrowserKey(CurrentContext);
        if (reloadBaseline || _comparisonBaseline == null || _comparisonContextKey != key)
        {
            _comparisonBaseline = _profileManager.GetComparisonBaseline(CurrentContext);
            _comparisonContextKey = key;
        }

        var expansion = DifferenceGroups.ToDictionary(g => g.Id, g => g.IsExpanded);
        DifferenceGroups.Clear();
        if (_comparisonBaseline.IsAvailable)
        {
            var current = ResolvedProfile.ToProfile("Comparison");
            var baseline = _comparisonBaseline.Resolved.ToProfile("Comparison");
            var differences = ComparisonProperties.Select(p => (Property: p, Current: DisplaySetting(current, p), Base: DisplaySetting(baseline, p))).Where(d => !SameSetting(d.Current, d.Base)).Select(d => new ProfileSettingDifference(d.Property.Name, _localizationManager.GetString("ProfileSetting_" + d.Property.Name), FormatSetting(d.Property.Name, d.Current), FormatSetting(d.Property.Name, d.Base))).ToList();
            ;
            string[] order =
            {
                "Steering",
                "Gas",
                "Brake",
                "Trail",
                "Keys",
                "Output",
                "Menu"
            };
            foreach (var group in differences.GroupBy(d => DifferenceSection(d.PropertyName)).OrderBy(g => Array.IndexOf(order, g.Key.Id)))
            {
                var rows = group.ToArray();
                var first = rows[0];
                var summary = rows.Length == 1 ? string.Format(_localizationManager.GetString("Summary_OneDifference"), first.Label, first.CurrentValue, first.BaselineValue) : string.Format(_localizationManager.GetString("Summary_DifferenceCount"), rows.Length);
                DifferenceGroups.Add(new ProfileDifferenceGroup(group.Key.Id, _localizationManager.GetString(group.Key.Label), summary, group.Key.Page, rows) { IsExpanded = expansion.GetValueOrDefault(group.Key.Id) });
            }
        }

        OnPropertyChanged(nameof(HasProfileDifferences));
        OnPropertyChanged(nameof(IsComparisonAvailable));
        OnPropertyChanged(nameof(ShowsMatchingProfile));
        OnPropertyChanged(nameof(ComparisonBaseText));
    }

    [RelayCommand]
    public void OpenDifferenceSettings(ProfileDifferenceGroup? group)
    {
        if (group != null)
            OpenSettingsRequested?.Invoke(group.Page);
    }
}
