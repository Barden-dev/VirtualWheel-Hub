using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimRacingHub.Application.Common;
using SimRacingHub.Application.Contracts;
using SimRacingHub.Application.Localization;
using SimRacingHub.Models;
using SimRacingHub.Services;

namespace SimRacingHub.Application.Features.Pedals;

public partial class BrakeSettingsViewModel : ProfileSettingsViewModel
{
    private double? _brakeTurbo;
    public BrakeSettingsViewModel(ProfileSession profileSession) : base(profileSession) => StartObserving();

    /// <summary>
    /// Gets or sets whether brake input is inverted.
    /// </summary>
    public bool? InvertBrake
    {
        get => CurrentProfile?.InvertBrake;
        set
        {
            if (CurrentProfile != null && CurrentProfile.InvertBrake != value)
            {
                CurrentProfile.InvertBrake = value;
                NotifyInvertBrakeProperties();
            }
        }
    }

    public bool EffectiveInvertBrake { get => CurrentProfile?.InvertBrake ?? ResolvedProfile?.InvertBrake ?? false; set => InvertBrake = value; }

    public bool IsInvertBrakeOverridden => CurrentProfile?.InvertBrake.HasValue ?? false;

    /// <summary>
    /// Gets or sets the brake response linearity / gamma exponent (0.2 to 3.0).
    /// </summary>
    public double? BrakeLinearity
    {
        get => CurrentProfile?.BrakeGamma;
        set
        {
            if (CurrentProfile == null) return;
            double? clamped = value.HasValue ? Math.Clamp(value.Value, 0.2, 3.0) : null;
            if (CurrentProfile.BrakeGamma != clamped)
            {
                CurrentProfile.BrakeGamma = clamped;
                NotifyBrakeLinearityProperties();
            }
        }
    }

    public double? BrakeGamma
    {
        get => BrakeLinearity;
        set => BrakeLinearity = value;
    }

    public double EffectiveBrakeLinearity { get => CurrentProfile?.BrakeGamma ?? ResolvedProfile?.BrakeGamma ?? 1.0; set => BrakeLinearity = value; }

    public double EffectiveBrakeGamma => EffectiveBrakeLinearity;

    public bool IsBrakeLinearityOverridden => CurrentProfile?.BrakeGamma.HasValue ?? false;

    public bool IsBrakeGammaOverridden => IsBrakeLinearityOverridden;

    /// <summary>
    /// Gets or sets the initial deadzone / fast threshold for brake (0.0 to 1.0).
    /// </summary>
    public double? BrakeDeadzone
    {
        get => CurrentProfile?.BrakeThreshold;
        set
        {
            if (CurrentProfile == null) return;
            double? clamped = value.HasValue ? Math.Clamp(value.Value, 0.0, 1.0) : null;
            if (CurrentProfile.BrakeThreshold != clamped)
            {
                CurrentProfile.BrakeThreshold = clamped;
                NotifyBrakeDeadzoneProperties();
            }
        }
    }

    public double? BrakeThreshold
    {
        get => BrakeDeadzone;
        set => BrakeDeadzone = value;
    }

    public double EffectiveBrakeDeadzone { get => CurrentProfile?.BrakeThreshold ?? ResolvedProfile?.BrakeThreshold ?? 0.0; set { if (value != EffectiveBrakeDeadzone) BrakeDeadzone = value; } }

    public double EffectiveBrakeThreshold => EffectiveBrakeDeadzone;

    public bool IsBrakeDeadzoneOverridden => CurrentProfile?.BrakeThreshold.HasValue ?? false;

    public bool IsBrakeThresholdOverridden => IsBrakeDeadzoneOverridden;

    /// <summary>
    /// Gets or sets the turbo duration for brake.
    /// </summary>
    public double? BrakeTurbo
    {
        get => _brakeTurbo;
        set
        {
            if (_brakeTurbo != value)
            {
                _brakeTurbo = value;
                NotifyBrakeTurboProperties();
            }
        }
    }

    public double EffectiveBrakeTurbo => _brakeTurbo ?? 0.0;

    public bool IsBrakeTurboOverridden => _brakeTurbo.HasValue;

    public double? BrakeAttackFastSeconds
    {
        get => CurrentProfile?.BrakeAttackFastSeconds;
        set
        {
            if (CurrentProfile != null && CurrentProfile.BrakeAttackFastSeconds != value)
            {
                CurrentProfile.BrakeAttackFastSeconds = value;
                NotifyBrakeAttackFastProperties();
            }
        }
    }

    public double EffectiveBrakeAttackFastSeconds { get => CurrentProfile?.BrakeAttackFastSeconds ??
        (ResolvedProfile != null && ResolvedProfile.BrakeAttackFast > 0 && ResolvedProfile.BrakeThreshold > 0
            ? Math.Round(65536.0 * ResolvedProfile.BrakeThreshold / ResolvedProfile.BrakeAttackFast, 3)
            : 0.0); set { if (value != EffectiveBrakeAttackFastSeconds) BrakeAttackFastSeconds = value; } }

    public bool IsBrakeAttackFastOverridden => CurrentProfile?.BrakeAttackFast.HasValue ?? false;

    public double? BrakeAttackSlowSeconds
    {
        get => CurrentProfile?.BrakeAttackSlowSeconds;
        set
        {
            if (CurrentProfile != null && CurrentProfile.BrakeAttackSlowSeconds != value)
            {
                CurrentProfile.BrakeAttackSlowSeconds = value;
                NotifyBrakeAttackSlowProperties();
            }
        }
    }

    public double EffectiveBrakeAttackSlowSeconds { get => CurrentProfile?.BrakeAttackSlowSeconds ??
        (ResolvedProfile != null && ResolvedProfile.BrakeAttackSlow > 0
            ? Math.Round(65536.0 * Math.Max(0.0, 1.0 - ResolvedProfile.BrakeThreshold) / ResolvedProfile.BrakeAttackSlow, 3)
            : 0.0); set { if (value != EffectiveBrakeAttackSlowSeconds) BrakeAttackSlowSeconds = value; } }

    public bool IsBrakeAttackSlowOverridden => CurrentProfile?.BrakeAttackSlow.HasValue ?? false;

    public double? BrakeEstimatedFullPressSeconds => CurrentProfile?.BrakeEstimatedFullPressSeconds;

    public double EffectiveBrakeEstimatedFullPressSeconds => EffectiveBrakeAttackFastSeconds + EffectiveBrakeAttackSlowSeconds;

    public double? BrakeDecaySeconds
    {
        get => CurrentProfile?.BrakeDecaySeconds;
        set
        {
            if (CurrentProfile != null && CurrentProfile.BrakeDecaySeconds != value)
            {
                CurrentProfile.BrakeDecaySeconds = value;
                NotifyBrakeDecayProperties();
            }
        }
    }

    public double EffectiveBrakeDecaySeconds { get => CurrentProfile?.BrakeDecaySeconds ??
        (ResolvedProfile != null && ResolvedProfile.BrakeDecay > 0
            ? Math.Round(65536.0 / ResolvedProfile.BrakeDecay, 3)
            : 0.0); set { if (value != EffectiveBrakeDecaySeconds) BrakeDecaySeconds = value; } }

    public bool IsBrakeDecayOverridden => CurrentProfile?.BrakeDecay.HasValue ?? false;

    [RelayCommand]
    public void ClearInvertBrakeOverride() => InvertBrake = null;

    [RelayCommand]
    public void ClearBrakeLinearityOverride() => BrakeLinearity = null;

    [RelayCommand]
    public void ClearBrakeGammaOverride() => BrakeGamma = null;

    [RelayCommand]
    public void ClearBrakeDeadzoneOverride() => BrakeDeadzone = null;

    [RelayCommand]
    public void ClearBrakeThresholdOverride() => BrakeThreshold = null;

    [RelayCommand]
    public void ClearBrakeTurboOverride() => BrakeTurbo = null;

    [RelayCommand]
    public void ClearBrakeAttackFastOverride() => BrakeAttackFastSeconds = null;

    [RelayCommand]
    public void ClearBrakeAttackSlowOverride() => BrakeAttackSlowSeconds = null;

    [RelayCommand]
    public void ClearBrakeDecayOverride() => BrakeDecaySeconds = null;

    private void NotifyInvertBrakeProperties()
    {
        OnPropertyChanged(nameof(InvertBrake));
        OnPropertyChanged(nameof(EffectiveInvertBrake));
        OnPropertyChanged(nameof(IsInvertBrakeOverridden));
    }

    private void NotifyBrakeLinearityProperties()
    {
        OnPropertyChanged(nameof(BrakeLinearity));
        OnPropertyChanged(nameof(BrakeGamma));
        OnPropertyChanged(nameof(EffectiveBrakeLinearity));
        OnPropertyChanged(nameof(EffectiveBrakeGamma));
        OnPropertyChanged(nameof(IsBrakeLinearityOverridden));
        OnPropertyChanged(nameof(IsBrakeGammaOverridden));
    }

    private void NotifyBrakeDeadzoneProperties()
    {
        NotifyBrakeAttackFastProperties();
        NotifyBrakeAttackSlowProperties();
        OnPropertyChanged(nameof(BrakeDeadzone));
        OnPropertyChanged(nameof(BrakeThreshold));
        OnPropertyChanged(nameof(EffectiveBrakeDeadzone));
        OnPropertyChanged(nameof(EffectiveBrakeThreshold));
        OnPropertyChanged(nameof(IsBrakeDeadzoneOverridden));
        OnPropertyChanged(nameof(IsBrakeThresholdOverridden));
    }

    private void NotifyBrakeTurboProperties()
    {
        OnPropertyChanged(nameof(BrakeTurbo));
        OnPropertyChanged(nameof(EffectiveBrakeTurbo));
        OnPropertyChanged(nameof(IsBrakeTurboOverridden));
    }

    private void NotifyBrakeAttackFastProperties()
    {
        OnPropertyChanged(nameof(BrakeAttackFastSeconds));
        OnPropertyChanged(nameof(EffectiveBrakeAttackFastSeconds));
        OnPropertyChanged(nameof(IsBrakeAttackFastOverridden));
        OnPropertyChanged(nameof(BrakeEstimatedFullPressSeconds));
        OnPropertyChanged(nameof(EffectiveBrakeEstimatedFullPressSeconds));
    }

    private void NotifyBrakeAttackSlowProperties()
    {
        OnPropertyChanged(nameof(BrakeAttackSlowSeconds));
        OnPropertyChanged(nameof(EffectiveBrakeAttackSlowSeconds));
        OnPropertyChanged(nameof(IsBrakeAttackSlowOverridden));
        OnPropertyChanged(nameof(BrakeEstimatedFullPressSeconds));
        OnPropertyChanged(nameof(EffectiveBrakeEstimatedFullPressSeconds));
    }

    private void NotifyBrakeDecayProperties()
    {
        OnPropertyChanged(nameof(BrakeDecaySeconds));
        OnPropertyChanged(nameof(EffectiveBrakeDecaySeconds));
        OnPropertyChanged(nameof(IsBrakeDecayOverridden));
    }

    protected override void OnCurrentProfilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName)) { NotifyAllPedalProperties(); return; }
        switch (e.PropertyName)
        {
            case nameof(Profile.InvertBrake):
                NotifyInvertBrakeProperties();
                break;
            case nameof(Profile.BrakeGamma):
                NotifyBrakeLinearityProperties();
                break;
            case nameof(Profile.BrakeThreshold):
                NotifyBrakeDeadzoneProperties();
                break;
            case nameof(Profile.BrakeAttackFast):
            case nameof(Profile.BrakeAttackFastSeconds):
                NotifyBrakeAttackFastProperties();
                break;
            case nameof(Profile.BrakeAttackSlow):
            case nameof(Profile.BrakeAttackSlowSeconds):
                NotifyBrakeAttackSlowProperties();
                break;
            case nameof(Profile.BrakeDecay):
            case nameof(Profile.BrakeDecaySeconds):
                NotifyBrakeDecayProperties();
                break;
        }
    }

    protected override void OnResolvedProfilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName)) { NotifyAllPedalProperties(); return; }
        switch (e.PropertyName)
        {
            case nameof(ResolvedProfile.InvertBrake):
                NotifyInvertBrakeProperties();
                break;
            case nameof(ResolvedProfile.BrakeGamma):
                NotifyBrakeLinearityProperties();
                break;
            case nameof(ResolvedProfile.BrakeThreshold):
                NotifyBrakeDeadzoneProperties();
                break;
            case nameof(ResolvedProfile.BrakeAttackFast):
                NotifyBrakeAttackFastProperties();
                break;
            case nameof(ResolvedProfile.BrakeAttackSlow):
                NotifyBrakeAttackSlowProperties();
                break;
            case nameof(ResolvedProfile.BrakeDecay):
                NotifyBrakeDecayProperties();
                break;
        }
    }

    protected override void RefreshSettings() => NotifyAllPedalProperties();
    public void NotifyAllPedalProperties()
    {
        NotifyInvertBrakeProperties();
        NotifyBrakeLinearityProperties();
        NotifyBrakeDeadzoneProperties();
        NotifyBrakeTurboProperties();
        NotifyBrakeAttackFastProperties();
        NotifyBrakeAttackSlowProperties();
        NotifyBrakeDecayProperties();
    }
}
