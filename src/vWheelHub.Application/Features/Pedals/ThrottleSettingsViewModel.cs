using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimRacingHub.Application.Common;
using SimRacingHub.Application.Contracts;
using SimRacingHub.Application.Localization;
using SimRacingHub.Models;
using SimRacingHub.Services;

namespace SimRacingHub.Application.Features.Pedals;

public partial class ThrottleSettingsViewModel : ProfileSettingsViewModel
{
    public ThrottleSettingsViewModel(ProfileSession profileSession) : base(profileSession) => StartObserving();

    /// <summary>
    /// Gets or sets whether throttle input is inverted.
    /// </summary>
    public bool? InvertThrottle
    {
        get => CurrentProfile?.InvertGas;
        set
        {
            if (CurrentProfile != null && CurrentProfile.InvertGas != value)
            {
                CurrentProfile.InvertGas = value;
                NotifyInvertThrottleProperties();
            }
        }
    }

    public bool? InvertGas
    {
        get => InvertThrottle;
        set => InvertThrottle = value;
    }

    public bool EffectiveInvertThrottle { get => CurrentProfile?.InvertGas ?? ResolvedProfile?.InvertGas ?? false; set => InvertThrottle = value; }

    public bool EffectiveInvertGas => EffectiveInvertThrottle;

    public bool IsInvertThrottleOverridden => CurrentProfile?.InvertGas.HasValue ?? false;

    public bool IsInvertGasOverridden => IsInvertThrottleOverridden;

    /// <summary>
    /// Gets or sets the throttle response linearity / gamma exponent (0.2 to 3.0).
    /// </summary>
    public double? ThrottleLinearity
    {
        get => CurrentProfile?.GasGamma;
        set
        {
            if (CurrentProfile == null) return;
            double? clamped = value.HasValue ? Math.Clamp(value.Value, 0.2, 3.0) : null;
            if (CurrentProfile.GasGamma != clamped)
            {
                CurrentProfile.GasGamma = clamped;
                NotifyThrottleLinearityProperties();
            }
        }
    }

    public double? GasGamma
    {
        get => ThrottleLinearity;
        set => ThrottleLinearity = value;
    }

    public double EffectiveThrottleLinearity { get => CurrentProfile?.GasGamma ?? ResolvedProfile?.GasGamma ?? 1.0; set => ThrottleLinearity = value; }

    public double EffectiveGasGamma => EffectiveThrottleLinearity;

    public bool IsThrottleLinearityOverridden => CurrentProfile?.GasGamma.HasValue ?? false;

    public bool IsGasGammaOverridden => IsThrottleLinearityOverridden;

    /// <summary>
    /// Gets or sets the initial deadzone / fast threshold for throttle (0.0 to 1.0).
    /// </summary>
    public double? ThrottleDeadzone
    {
        get => CurrentProfile?.GasThreshold;
        set
        {
            if (CurrentProfile == null) return;
            double? clamped = value.HasValue ? Math.Clamp(value.Value, 0.0, 1.0) : null;
            if (CurrentProfile.GasThreshold != clamped)
            {
                CurrentProfile.GasThreshold = clamped;
                NotifyThrottleDeadzoneProperties();
            }
        }
    }

    public double? GasThreshold
    {
        get => ThrottleDeadzone;
        set => ThrottleDeadzone = value;
    }

    public double EffectiveThrottleDeadzone { get => CurrentProfile?.GasThreshold ?? ResolvedProfile?.GasThreshold ?? 0.0; set { if (value != EffectiveThrottleDeadzone) ThrottleDeadzone = value; } }

    public double EffectiveGasThreshold => EffectiveThrottleDeadzone;

    public bool IsThrottleDeadzoneOverridden => CurrentProfile?.GasThreshold.HasValue ?? false;

    public bool IsGasThresholdOverridden => IsThrottleDeadzoneOverridden;

    /// <summary>
    /// Gets or sets the turbo attack duration in seconds for throttle.
    /// </summary>
    public double? ThrottleTurbo
    {
        get => CurrentProfile?.GasAttackTurboSeconds;
        set
        {
            if (CurrentProfile != null && CurrentProfile.GasAttackTurboSeconds != value)
            {
                CurrentProfile.GasAttackTurboSeconds = value;
                NotifyThrottleTurboProperties();
            }
        }
    }

    public double? GasAttackTurboSeconds
    {
        get => ThrottleTurbo;
        set => ThrottleTurbo = value;
    }

    public double? GasAttackTurbo
    {
        get => CurrentProfile?.GasAttackTurbo;
        set
        {
            if (CurrentProfile != null && CurrentProfile.GasAttackTurbo != value)
            {
                CurrentProfile.GasAttackTurbo = value;
                NotifyThrottleTurboProperties();
            }
        }
    }

    public double EffectiveThrottleTurbo { get => CurrentProfile?.GasAttackTurboSeconds ??
        (ResolvedProfile != null && ResolvedProfile.GasAttackTurbo > 0
            ? Math.Round(65536.0 / ResolvedProfile.GasAttackTurbo, 3)
            : 0.0); set { if (value != EffectiveThrottleTurbo) ThrottleTurbo = value; } }

    public double EffectiveGasAttackTurboSeconds => EffectiveThrottleTurbo;

    public bool IsThrottleTurboOverridden => CurrentProfile?.GasAttackTurbo.HasValue ?? false;

    public bool IsGasAttackTurboOverridden => IsThrottleTurboOverridden;

    public double? GasAttackFastSeconds
    {
        get => CurrentProfile?.GasAttackFastSeconds;
        set
        {
            if (CurrentProfile != null && CurrentProfile.GasAttackFastSeconds != value)
            {
                CurrentProfile.GasAttackFastSeconds = value;
                NotifyGasAttackFastProperties();
            }
        }
    }

    public double EffectiveGasAttackFastSeconds { get => CurrentProfile?.GasAttackFastSeconds ??
        (ResolvedProfile != null && ResolvedProfile.GasAttackFast > 0 && ResolvedProfile.GasThreshold > 0
            ? Math.Round(65536.0 * ResolvedProfile.GasThreshold / ResolvedProfile.GasAttackFast, 3)
            : 0.0); set { if (value != EffectiveGasAttackFastSeconds) GasAttackFastSeconds = value; } }

    public bool IsGasAttackFastOverridden => CurrentProfile?.GasAttackFast.HasValue ?? false;

    public double? GasAttackSlowSeconds
    {
        get => CurrentProfile?.GasAttackSlowSeconds;
        set
        {
            if (CurrentProfile != null && CurrentProfile.GasAttackSlowSeconds != value)
            {
                CurrentProfile.GasAttackSlowSeconds = value;
                NotifyGasAttackSlowProperties();
            }
        }
    }

    public double EffectiveGasAttackSlowSeconds { get => CurrentProfile?.GasAttackSlowSeconds ??
        (ResolvedProfile != null && ResolvedProfile.GasAttackSlow > 0
            ? Math.Round(65536.0 * Math.Max(0.0, 1.0 - ResolvedProfile.GasThreshold) / ResolvedProfile.GasAttackSlow, 3)
            : 0.0); set { if (value != EffectiveGasAttackSlowSeconds) GasAttackSlowSeconds = value; } }

    public bool IsGasAttackSlowOverridden => CurrentProfile?.GasAttackSlow.HasValue ?? false;

    public double? GasEstimatedFullPressSeconds => CurrentProfile?.GasEstimatedFullPressSeconds;

    public double EffectiveGasEstimatedFullPressSeconds => EffectiveGasAttackFastSeconds + EffectiveGasAttackSlowSeconds;

    public double? GasDecaySeconds
    {
        get => CurrentProfile?.GasDecaySeconds;
        set
        {
            if (CurrentProfile != null && CurrentProfile.GasDecaySeconds != value)
            {
                CurrentProfile.GasDecaySeconds = value;
                NotifyGasDecayProperties();
            }
        }
    }

    public double EffectiveGasDecaySeconds { get => CurrentProfile?.GasDecaySeconds ??
        (ResolvedProfile != null && ResolvedProfile.GasDecay > 0
            ? Math.Round(65536.0 / ResolvedProfile.GasDecay, 3)
            : 0.0); set { if (value != EffectiveGasDecaySeconds) GasDecaySeconds = value; } }

    public bool IsGasDecayOverridden => CurrentProfile?.GasDecay.HasValue ?? false;

    public double? GasInstantCutSeconds
    {
        get => CurrentProfile?.GasInstantCutSeconds;
        set
        {
            if (CurrentProfile != null && CurrentProfile.GasInstantCutSeconds != value)
            {
                CurrentProfile.GasInstantCutSeconds = value;
                NotifyGasInstantCutProperties();
            }
        }
    }

    public double EffectiveGasInstantCutSeconds { get => CurrentProfile?.GasInstantCutSeconds ??
        (ResolvedProfile != null && ResolvedProfile.GasInstantCut > 0
            ? Math.Round(65536.0 / ResolvedProfile.GasInstantCut, 3)
            : 0.0); set { if (value != EffectiveGasInstantCutSeconds) GasInstantCutSeconds = value; } }

    public bool IsGasInstantCutOverridden => CurrentProfile?.GasInstantCut.HasValue ?? false;

    [RelayCommand]
    public void ClearInvertThrottleOverride() => InvertThrottle = null;

    [RelayCommand]
    public void ClearInvertGasOverride() => InvertGas = null;

    [RelayCommand]
    public void ClearThrottleLinearityOverride() => ThrottleLinearity = null;

    [RelayCommand]
    public void ClearGasGammaOverride() => GasGamma = null;

    [RelayCommand]
    public void ClearThrottleDeadzoneOverride() => ThrottleDeadzone = null;

    [RelayCommand]
    public void ClearGasThresholdOverride() => GasThreshold = null;

    [RelayCommand]
    public void ClearThrottleTurboOverride() => ThrottleTurbo = null;

    [RelayCommand]
    public void ClearGasAttackTurboOverride() => GasAttackTurboSeconds = null;

    [RelayCommand]
    public void ClearGasAttackFastOverride() => GasAttackFastSeconds = null;

    [RelayCommand]
    public void ClearGasAttackSlowOverride() => GasAttackSlowSeconds = null;

    [RelayCommand]
    public void ClearGasDecayOverride() => GasDecaySeconds = null;

    [RelayCommand]
    public void ClearGasInstantCutOverride() => GasInstantCutSeconds = null;

    private void NotifyInvertThrottleProperties()
    {
        OnPropertyChanged(nameof(InvertThrottle));
        OnPropertyChanged(nameof(InvertGas));
        OnPropertyChanged(nameof(EffectiveInvertThrottle));
        OnPropertyChanged(nameof(EffectiveInvertGas));
        OnPropertyChanged(nameof(IsInvertThrottleOverridden));
        OnPropertyChanged(nameof(IsInvertGasOverridden));
    }

    private void NotifyThrottleLinearityProperties()
    {
        OnPropertyChanged(nameof(ThrottleLinearity));
        OnPropertyChanged(nameof(GasGamma));
        OnPropertyChanged(nameof(EffectiveThrottleLinearity));
        OnPropertyChanged(nameof(EffectiveGasGamma));
        OnPropertyChanged(nameof(IsThrottleLinearityOverridden));
        OnPropertyChanged(nameof(IsGasGammaOverridden));
    }

    private void NotifyThrottleDeadzoneProperties()
    {
        NotifyGasAttackFastProperties();
        NotifyGasAttackSlowProperties();
        OnPropertyChanged(nameof(ThrottleDeadzone));
        OnPropertyChanged(nameof(GasThreshold));
        OnPropertyChanged(nameof(EffectiveThrottleDeadzone));
        OnPropertyChanged(nameof(EffectiveGasThreshold));
        OnPropertyChanged(nameof(IsThrottleDeadzoneOverridden));
        OnPropertyChanged(nameof(IsGasThresholdOverridden));
    }

    private void NotifyThrottleTurboProperties()
    {
        OnPropertyChanged(nameof(ThrottleTurbo));
        OnPropertyChanged(nameof(GasAttackTurboSeconds));
        OnPropertyChanged(nameof(GasAttackTurbo));
        OnPropertyChanged(nameof(EffectiveThrottleTurbo));
        OnPropertyChanged(nameof(EffectiveGasAttackTurboSeconds));
        OnPropertyChanged(nameof(IsThrottleTurboOverridden));
        OnPropertyChanged(nameof(IsGasAttackTurboOverridden));
    }

    private void NotifyGasAttackFastProperties()
    {
        OnPropertyChanged(nameof(GasAttackFastSeconds));
        OnPropertyChanged(nameof(EffectiveGasAttackFastSeconds));
        OnPropertyChanged(nameof(IsGasAttackFastOverridden));
        OnPropertyChanged(nameof(GasEstimatedFullPressSeconds));
        OnPropertyChanged(nameof(EffectiveGasEstimatedFullPressSeconds));
    }

    private void NotifyGasAttackSlowProperties()
    {
        OnPropertyChanged(nameof(GasAttackSlowSeconds));
        OnPropertyChanged(nameof(EffectiveGasAttackSlowSeconds));
        OnPropertyChanged(nameof(IsGasAttackSlowOverridden));
        OnPropertyChanged(nameof(GasEstimatedFullPressSeconds));
        OnPropertyChanged(nameof(EffectiveGasEstimatedFullPressSeconds));
    }

    private void NotifyGasDecayProperties()
    {
        OnPropertyChanged(nameof(GasDecaySeconds));
        OnPropertyChanged(nameof(EffectiveGasDecaySeconds));
        OnPropertyChanged(nameof(IsGasDecayOverridden));
    }

    private void NotifyGasInstantCutProperties()
    {
        OnPropertyChanged(nameof(GasInstantCutSeconds));
        OnPropertyChanged(nameof(EffectiveGasInstantCutSeconds));
        OnPropertyChanged(nameof(IsGasInstantCutOverridden));
    }

    protected override void OnCurrentProfilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName)) { NotifyAllPedalProperties(); return; }
        switch (e.PropertyName)
        {
            case nameof(Profile.InvertGas):
                NotifyInvertThrottleProperties();
                break;
            case nameof(Profile.GasGamma):
                NotifyThrottleLinearityProperties();
                break;
            case nameof(Profile.GasThreshold):
                NotifyThrottleDeadzoneProperties();
                break;
            case nameof(Profile.GasAttackTurbo):
            case nameof(Profile.GasAttackTurboSeconds):
                NotifyThrottleTurboProperties();
                break;
            case nameof(Profile.GasAttackFast):
            case nameof(Profile.GasAttackFastSeconds):
                NotifyGasAttackFastProperties();
                break;
            case nameof(Profile.GasAttackSlow):
            case nameof(Profile.GasAttackSlowSeconds):
                NotifyGasAttackSlowProperties();
                break;
            case nameof(Profile.GasDecay):
            case nameof(Profile.GasDecaySeconds):
                NotifyGasDecayProperties();
                break;
            case nameof(Profile.GasInstantCut):
            case nameof(Profile.GasInstantCutSeconds):
                NotifyGasInstantCutProperties();
                break;
        }
    }

    protected override void OnResolvedProfilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName)) { NotifyAllPedalProperties(); return; }
        switch (e.PropertyName)
        {
            case nameof(ResolvedProfile.InvertGas):
                NotifyInvertThrottleProperties();
                break;
            case nameof(ResolvedProfile.GasGamma):
                NotifyThrottleLinearityProperties();
                break;
            case nameof(ResolvedProfile.GasThreshold):
                NotifyThrottleDeadzoneProperties();
                break;
            case nameof(ResolvedProfile.GasAttackTurbo):
                NotifyThrottleTurboProperties();
                break;
            case nameof(ResolvedProfile.GasAttackFast):
                NotifyGasAttackFastProperties();
                break;
            case nameof(ResolvedProfile.GasAttackSlow):
                NotifyGasAttackSlowProperties();
                break;
            case nameof(ResolvedProfile.GasDecay):
                NotifyGasDecayProperties();
                break;
            case nameof(ResolvedProfile.GasInstantCut):
                NotifyGasInstantCutProperties();
                break;
        }
    }

    protected override void RefreshSettings() => NotifyAllPedalProperties();
    public void NotifyAllPedalProperties()
    {
        NotifyInvertThrottleProperties();
        NotifyThrottleLinearityProperties();
        NotifyThrottleDeadzoneProperties();
        NotifyThrottleTurboProperties();
        NotifyGasAttackFastProperties();
        NotifyGasAttackSlowProperties();
        NotifyGasDecayProperties();
        NotifyGasInstantCutProperties();
    }
}
