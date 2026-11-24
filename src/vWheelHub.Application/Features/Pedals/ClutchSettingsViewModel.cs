using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimRacingHub.Application.Common;
using SimRacingHub.Models;
using SimRacingHub.Services;

namespace SimRacingHub.Application.Features.Pedals;

public partial class ClutchSettingsViewModel : ProfileSettingsViewModel
{
    public ClutchSettingsViewModel(ProfileSession profileSession) : base(profileSession) => StartObserving();

    /// <summary>
    /// Gets or sets whether clutch input is inverted.
    /// </summary>
    public bool? InvertClutch
    {
        get => CurrentProfile?.InvertClutch;
        set
        {
            if (CurrentProfile != null && CurrentProfile.InvertClutch != value)
            {
                CurrentProfile.InvertClutch = value;
                NotifyInvertClutchProperties();
            }
        }
    }

    public bool EffectiveInvertClutch { get => CurrentProfile?.InvertClutch ?? ResolvedProfile?.InvertClutch ?? false; set => InvertClutch = value; }

    public bool IsInvertClutchOverridden => CurrentProfile?.InvertClutch.HasValue ?? false;

    /// <summary>
    /// Gets or sets the clutch response linearity / gamma exponent (0.2 to 3.0).
    /// </summary>
    public double? ClutchLinearity
    {
        get => CurrentProfile?.ClutchGamma;
        set
        {
            if (CurrentProfile == null) return;
            double? clamped = value.HasValue ? Math.Clamp(value.Value, 0.2, 3.0) : null;
            if (CurrentProfile.ClutchGamma != clamped)
            {
                CurrentProfile.ClutchGamma = clamped;
                NotifyClutchLinearityProperties();
            }
        }
    }

    public double? ClutchGamma
    {
        get => ClutchLinearity;
        set => ClutchLinearity = value;
    }

    public double EffectiveClutchLinearity { get => CurrentProfile?.ClutchGamma ?? ResolvedProfile?.ClutchGamma ?? 1.0; set => ClutchLinearity = value; }

    public double EffectiveClutchGamma => EffectiveClutchLinearity;

    public bool IsClutchLinearityOverridden => CurrentProfile?.ClutchGamma.HasValue ?? false;

    public bool IsClutchGammaOverridden => IsClutchLinearityOverridden;

    /// <summary>
    /// Gets or sets the press stage threshold for clutch (0.0 to 1.0).
    /// </summary>
    public double? ClutchDeadzone
    {
        get => CurrentProfile?.ClutchThreshold;
        set
        {
            if (CurrentProfile == null) return;
            double? clamped = value.HasValue ? Math.Clamp(value.Value, 0.0, 1.0) : null;
            if (CurrentProfile.ClutchThreshold != clamped)
            {
                CurrentProfile.ClutchThreshold = clamped;
                NotifyClutchDeadzoneProperties();
            }
        }
    }

    public double? ClutchThreshold
    {
        get => ClutchDeadzone;
        set => ClutchDeadzone = value;
    }

    public double EffectiveClutchDeadzone { get => CurrentProfile?.ClutchThreshold ?? ResolvedProfile?.ClutchThreshold ?? 0.90; set { if (value != EffectiveClutchDeadzone) ClutchDeadzone = value; } }

    public double EffectiveClutchThreshold => EffectiveClutchDeadzone;

    public bool IsClutchDeadzoneOverridden => CurrentProfile?.ClutchThreshold.HasValue ?? false;

    public bool IsClutchThresholdOverridden => IsClutchDeadzoneOverridden;

    public double? ClutchAttackFastSeconds
    {
        get => CurrentProfile?.ClutchAttackFastSeconds;
        set
        {
            if (CurrentProfile != null && CurrentProfile.ClutchAttackFastSeconds != value)
            {
                CurrentProfile.ClutchAttackFastSeconds = value;
                NotifyClutchAttackFastProperties();
            }
        }
    }

    public double EffectiveClutchAttackFastSeconds { get => CurrentProfile?.ClutchAttackFastSeconds ??
        (ResolvedProfile != null && ResolvedProfile.ClutchAttackFast > 0 && ResolvedProfile.ClutchThreshold > 0
            ? Math.Round(65536.0 * ResolvedProfile.ClutchThreshold / ResolvedProfile.ClutchAttackFast, 3)
            : 0.0); set { if (value != EffectiveClutchAttackFastSeconds) ClutchAttackFastSeconds = value; } }

    public bool IsClutchAttackFastOverridden => CurrentProfile?.ClutchAttackFast.HasValue ?? false;

    public double? ClutchAttackSlowSeconds
    {
        get => CurrentProfile?.ClutchAttackSlowSeconds;
        set
        {
            if (CurrentProfile != null && CurrentProfile.ClutchAttackSlowSeconds != value)
            {
                CurrentProfile.ClutchAttackSlowSeconds = value;
                NotifyClutchAttackSlowProperties();
            }
        }
    }

    public double EffectiveClutchAttackSlowSeconds { get => CurrentProfile?.ClutchAttackSlowSeconds ??
        (ResolvedProfile != null && ResolvedProfile.ClutchAttackSlow > 0
            ? Math.Round(65536.0 * Math.Max(0.0, 1.0 - ResolvedProfile.ClutchThreshold) / ResolvedProfile.ClutchAttackSlow, 3)
            : 0.0); set { if (value != EffectiveClutchAttackSlowSeconds) ClutchAttackSlowSeconds = value; } }

    public bool IsClutchAttackSlowOverridden => CurrentProfile?.ClutchAttackSlow.HasValue ?? false;

    public double? ClutchEstimatedFullPressSeconds => CurrentProfile?.ClutchEstimatedFullPressSeconds;

    public double EffectiveClutchEstimatedFullPressSeconds => EffectiveClutchAttackFastSeconds + EffectiveClutchAttackSlowSeconds;

    public double? ClutchDecaySeconds
    {
        get => CurrentProfile?.ClutchDecaySeconds;
        set
        {
            if (CurrentProfile != null && CurrentProfile.ClutchDecaySeconds != value)
            {
                CurrentProfile.ClutchDecaySeconds = value;
                NotifyClutchDecayProperties();
            }
        }
    }

    public double EffectiveClutchDecaySeconds { get => CurrentProfile?.ClutchDecaySeconds ??
        (ResolvedProfile != null && ResolvedProfile.ClutchDecay > 0
            ? Math.Round(65536.0 / ResolvedProfile.ClutchDecay, 3)
            : 0.0); set { if (value != EffectiveClutchDecaySeconds) ClutchDecaySeconds = value; } }

    public bool IsClutchDecayOverridden => CurrentProfile?.ClutchDecay.HasValue ?? false;

    [RelayCommand]
    public void ClearInvertClutchOverride() => InvertClutch = null;

    [RelayCommand]
    public void ClearClutchLinearityOverride() => ClutchLinearity = null;

    [RelayCommand]
    public void ClearClutchGammaOverride() => ClutchGamma = null;

    [RelayCommand]
    public void ClearClutchDeadzoneOverride() => ClutchDeadzone = null;

    [RelayCommand]
    public void ClearClutchThresholdOverride() => ClutchThreshold = null;

    [RelayCommand]
    public void ClearClutchAttackFastOverride() => ClutchAttackFastSeconds = null;

    [RelayCommand]
    public void ClearClutchAttackSlowOverride() => ClutchAttackSlowSeconds = null;

    [RelayCommand]
    public void ClearClutchDecayOverride() => ClutchDecaySeconds = null;

    private void NotifyInvertClutchProperties()
    {
        OnPropertyChanged(nameof(InvertClutch));
        OnPropertyChanged(nameof(EffectiveInvertClutch));
        OnPropertyChanged(nameof(IsInvertClutchOverridden));
    }

    private void NotifyClutchLinearityProperties()
    {
        OnPropertyChanged(nameof(ClutchLinearity));
        OnPropertyChanged(nameof(ClutchGamma));
        OnPropertyChanged(nameof(EffectiveClutchLinearity));
        OnPropertyChanged(nameof(EffectiveClutchGamma));
        OnPropertyChanged(nameof(IsClutchLinearityOverridden));
        OnPropertyChanged(nameof(IsClutchGammaOverridden));
    }

    private void NotifyClutchDeadzoneProperties()
    {
        NotifyClutchAttackFastProperties();
        NotifyClutchAttackSlowProperties();
        OnPropertyChanged(nameof(ClutchDeadzone));
        OnPropertyChanged(nameof(ClutchThreshold));
        OnPropertyChanged(nameof(EffectiveClutchDeadzone));
        OnPropertyChanged(nameof(EffectiveClutchThreshold));
        OnPropertyChanged(nameof(IsClutchDeadzoneOverridden));
        OnPropertyChanged(nameof(IsClutchThresholdOverridden));
    }

    private void NotifyClutchAttackFastProperties()
    {
        OnPropertyChanged(nameof(ClutchAttackFastSeconds));
        OnPropertyChanged(nameof(EffectiveClutchAttackFastSeconds));
        OnPropertyChanged(nameof(IsClutchAttackFastOverridden));
        OnPropertyChanged(nameof(ClutchEstimatedFullPressSeconds));
        OnPropertyChanged(nameof(EffectiveClutchEstimatedFullPressSeconds));
    }

    private void NotifyClutchAttackSlowProperties()
    {
        OnPropertyChanged(nameof(ClutchAttackSlowSeconds));
        OnPropertyChanged(nameof(EffectiveClutchAttackSlowSeconds));
        OnPropertyChanged(nameof(IsClutchAttackSlowOverridden));
        OnPropertyChanged(nameof(ClutchEstimatedFullPressSeconds));
        OnPropertyChanged(nameof(EffectiveClutchEstimatedFullPressSeconds));
    }

    private void NotifyClutchDecayProperties()
    {
        OnPropertyChanged(nameof(ClutchDecaySeconds));
        OnPropertyChanged(nameof(EffectiveClutchDecaySeconds));
        OnPropertyChanged(nameof(IsClutchDecayOverridden));
    }

    protected override void OnCurrentProfilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName)) { NotifyAllPedalProperties(); return; }
        switch (e.PropertyName)
        {
            case nameof(Profile.InvertClutch):
                NotifyInvertClutchProperties();
                break;
            case nameof(Profile.ClutchGamma):
                NotifyClutchLinearityProperties();
                break;
            case nameof(Profile.ClutchThreshold):
                NotifyClutchDeadzoneProperties();
                break;
            case nameof(Profile.ClutchAttackFast):
            case nameof(Profile.ClutchAttackFastSeconds):
                NotifyClutchAttackFastProperties();
                break;
            case nameof(Profile.ClutchAttackSlow):
            case nameof(Profile.ClutchAttackSlowSeconds):
                NotifyClutchAttackSlowProperties();
                break;
            case nameof(Profile.ClutchDecay):
            case nameof(Profile.ClutchDecaySeconds):
                NotifyClutchDecayProperties();
                break;
        }
    }

    protected override void OnResolvedProfilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName)) { NotifyAllPedalProperties(); return; }
        switch (e.PropertyName)
        {
            case nameof(ResolvedProfile.InvertClutch):
                NotifyInvertClutchProperties();
                break;
            case nameof(ResolvedProfile.ClutchGamma):
                NotifyClutchLinearityProperties();
                break;
            case nameof(ResolvedProfile.ClutchThreshold):
                NotifyClutchDeadzoneProperties();
                break;
            case nameof(ResolvedProfile.ClutchAttackFast):
                NotifyClutchAttackFastProperties();
                break;
            case nameof(ResolvedProfile.ClutchAttackSlow):
                NotifyClutchAttackSlowProperties();
                break;
            case nameof(ResolvedProfile.ClutchDecay):
                NotifyClutchDecayProperties();
                break;
        }
    }

    protected override void RefreshSettings() => NotifyAllPedalProperties();
    public void NotifyAllPedalProperties()
    {
        NotifyInvertClutchProperties();
        NotifyClutchLinearityProperties();
        NotifyClutchDeadzoneProperties();
        NotifyClutchAttackFastProperties();
        NotifyClutchAttackSlowProperties();
        NotifyClutchDecayProperties();
    }
}
