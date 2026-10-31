using System;
using System.Collections.Generic;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimRacingHub.Application.Common;
using SimRacingHub.Application.Contracts;
using SimRacingHub.Application.Localization;
using SimRacingHub.Models;
using SimRacingHub.Services;

namespace SimRacingHub.Application.Features.Steering;

public partial class SteeringOutputSettingsViewModel : ProfileSettingsViewModel
{
    private readonly ILocalizationManager _localizationManager;
    public SteeringOutputSettingsViewModel(ProfileSession session, ILocalizationManager localizationManager) : base(session)
    {
        _localizationManager = localizationManager ?? throw new ArgumentNullException(nameof(localizationManager));
        StartObserving();
    }

    public bool? UseSteeringLockClamp
    {
        get => CurrentProfile?.UseSteeringLockClamp;
        set
        {
            if (CurrentProfile != null && CurrentProfile.UseSteeringLockClamp != value)
            {
                CurrentProfile.UseSteeringLockClamp = value;
                NotifyLockClampProperties();
            }
        }
    }

    public bool EffectiveUseSteeringLockClamp { get => CurrentProfile?.UseSteeringLockClamp ?? ResolvedProfile?.UseSteeringLockClamp ?? false; set => UseSteeringLockClamp = value; }

    public bool IsUseSteeringLockClampOverridden => CurrentProfile?.UseSteeringLockClamp.HasValue ?? false;

    [RelayCommand]
    public void ClearUseSteeringLockClampOverride() => UseSteeringLockClamp = null;

    public int? CarSteeringLock
    {
        get => CurrentProfile?.CarSteeringLock;
        set
        {
            if (CurrentProfile == null) return;
            int? clamped = value.HasValue ? Math.Clamp(value.Value, 90, 2520) : null;
            if (CurrentProfile.CarSteeringLock != clamped)
            {
                CurrentProfile.CarSteeringLock = clamped;
                NotifyCarLockProperties();
            }
        }
    }

    public int? SteeringWheelRotationDeg
    {
        get => CarSteeringLock;
        set => CarSteeringLock = value;
    }

    public int EffectiveCarSteeringLock { get => CurrentProfile?.CarSteeringLock ?? ResolvedProfile?.CarSteeringLock ?? 540; set { if (value != EffectiveCarSteeringLock) CarSteeringLock = value; } }

    public int EffectiveSteeringWheelRotationDeg => EffectiveCarSteeringLock;

    public bool IsCarSteeringLockOverridden => CurrentProfile?.CarSteeringLock.HasValue ?? false;

    public bool IsSteeringWheelRotationDegOverridden => IsCarSteeringLockOverridden;

    [RelayCommand]
    public void ClearCarSteeringLockOverride() => CarSteeringLock = null;

    public void ClearSteeringWheelRotationDegOverride() => ClearCarSteeringLockOverride();

    public int? GlobalSteeringRange
    {
        get => CurrentProfile?.GlobalSteeringRange;
        set
        {
            if (CurrentProfile == null) return;
            int? clamped = value.HasValue ? Math.Clamp(value.Value, 90, 2520) : null;
            if (CurrentProfile.GlobalSteeringRange != clamped)
            {
                CurrentProfile.GlobalSteeringRange = clamped;
                NotifyGlobalRangeProperties();
            }
        }
    }

    public int? MaxWheelAngle
    {
        get => GlobalSteeringRange;
        set => GlobalSteeringRange = value;
    }

    public int EffectiveGlobalSteeringRange { get => CurrentProfile?.GlobalSteeringRange ?? ResolvedProfile?.GlobalSteeringRange ?? 900; set { if (value != EffectiveGlobalSteeringRange) GlobalSteeringRange = value; } }

    public int EffectiveMaxWheelAngle => EffectiveGlobalSteeringRange;

    public bool IsGlobalSteeringRangeOverridden => CurrentProfile?.GlobalSteeringRange.HasValue ?? false;

    public bool IsMaxWheelAngleOverridden => IsGlobalSteeringRangeOverridden;

    [RelayCommand]
    public void ClearGlobalSteeringRangeOverride() => GlobalSteeringRange = null;

    public void ClearMaxWheelAngleOverride() => ClearGlobalSteeringRangeOverride();

    public bool? InvertSteering
    {
        get => CurrentProfile?.InvertSteering;
        set
        {
            if (CurrentProfile != null && CurrentProfile.InvertSteering != value)
            {
                CurrentProfile.InvertSteering = value;
                NotifyInvertSteeringProperties();
            }
        }
    }

    public bool EffectiveInvertSteering { get => CurrentProfile?.InvertSteering ?? ResolvedProfile?.InvertSteering ?? false; set => InvertSteering = value; }

    public bool IsInvertSteeringOverridden => CurrentProfile?.InvertSteering.HasValue ?? false;

    [RelayCommand]
    public void ClearInvertSteeringOverride() => InvertSteering = null;

    public int? OutSteerMin
    {
        get => CurrentProfile?.OutSteerMin;
        set
        {
            if (CurrentProfile == null) return;
            int? clamped = value.HasValue ? Math.Clamp(value.Value, -32768, 32768) : null;
            if (CurrentProfile.OutSteerMin != clamped)
            {
                CurrentProfile.OutSteerMin = clamped;
                NotifyOutSteerMinProperties();
            }
        }
    }

    public int EffectiveOutSteerMin => CurrentProfile?.OutSteerMin ?? ResolvedProfile?.OutSteerMin ?? 0;

    public bool IsOutSteerMinOverridden => CurrentProfile?.OutSteerMin.HasValue ?? false;

    [RelayCommand]
    public void ClearOutSteerMinOverride() => OutSteerMin = null;

    public int? OutSteerMax
    {
        get => CurrentProfile?.OutSteerMax;
        set
        {
            if (CurrentProfile == null) return;
            int? clamped = value.HasValue ? Math.Clamp(value.Value, -32768, 32768) : null;
            if (CurrentProfile.OutSteerMax != clamped)
            {
                CurrentProfile.OutSteerMax = clamped;
                NotifyOutSteerMaxProperties();
            }
        }
    }

    public int EffectiveOutSteerMax => CurrentProfile?.OutSteerMax ?? ResolvedProfile?.OutSteerMax ?? 32768;

    public bool IsOutSteerMaxOverridden => CurrentProfile?.OutSteerMax.HasValue ?? false;

    [RelayCommand]
    public void ClearOutSteerMaxOverride() => OutSteerMax = null;

    public void OnMonitorMarkerDragged(string markerName, double value)
    {
        if (CurrentProfile == null) return;

        if (IsMarkerMatch(markerName, "VJoy Steer Min", "Monitor_VJoySteerMin"))
        {
            OutSteerMin = (int)Math.Round(value);
        }
        else if (IsMarkerMatch(markerName, "VJoy Steer Max", "Monitor_VJoySteerMax"))
        {
            OutSteerMax = (int)Math.Round(value);
        }
        else if (IsMarkerMatch(markerName, "Car Steering Lock", "Monitor_CarSteeringLock"))
        {
            CarSteeringLock = (int)Math.Round(value);
        }
    }

    private bool IsMarkerMatch(string markerName, string enFallback, string resourceKey)
    {
        if (string.Equals(markerName, enFallback, StringComparison.OrdinalIgnoreCase)) return true;

        string localized = _localizationManager.GetString(resourceKey, enFallback);
        return string.Equals(markerName, localized, StringComparison.OrdinalIgnoreCase);
    }

    private void NotifyLockClampProperties()
    {
        OnPropertyChanged(nameof(UseSteeringLockClamp));
        OnPropertyChanged(nameof(EffectiveUseSteeringLockClamp));
        OnPropertyChanged(nameof(IsUseSteeringLockClampOverridden));
    }

    private void NotifyCarLockProperties()
    {
        OnPropertyChanged(nameof(CarSteeringLock));
        OnPropertyChanged(nameof(SteeringWheelRotationDeg));
        OnPropertyChanged(nameof(EffectiveCarSteeringLock));
        OnPropertyChanged(nameof(EffectiveSteeringWheelRotationDeg));
        OnPropertyChanged(nameof(IsCarSteeringLockOverridden));
        OnPropertyChanged(nameof(IsSteeringWheelRotationDegOverridden));
    }

    private void NotifyGlobalRangeProperties()
    {
        OnPropertyChanged(nameof(GlobalSteeringRange));
        OnPropertyChanged(nameof(MaxWheelAngle));
        OnPropertyChanged(nameof(EffectiveGlobalSteeringRange));
        OnPropertyChanged(nameof(EffectiveMaxWheelAngle));
        OnPropertyChanged(nameof(IsGlobalSteeringRangeOverridden));
        OnPropertyChanged(nameof(IsMaxWheelAngleOverridden));
    }

    private void NotifyInvertSteeringProperties()
    {
        OnPropertyChanged(nameof(InvertSteering));
        OnPropertyChanged(nameof(EffectiveInvertSteering));
        OnPropertyChanged(nameof(IsInvertSteeringOverridden));
    }

    private void NotifyOutSteerMinProperties()
    {
        OnPropertyChanged(nameof(OutSteerMin));
        OnPropertyChanged(nameof(EffectiveOutSteerMin));
        OnPropertyChanged(nameof(IsOutSteerMinOverridden));
    }

    private void NotifyOutSteerMaxProperties()
    {
        OnPropertyChanged(nameof(OutSteerMax));
        OnPropertyChanged(nameof(EffectiveOutSteerMax));
        OnPropertyChanged(nameof(IsOutSteerMaxOverridden));
    }

    protected override void OnCurrentProfilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName)) { NotifyAllSteeringProperties(); return; }
        switch (e.PropertyName)
        {
            case nameof(Profile.UseSteeringLockClamp):
                NotifyLockClampProperties();
                break;
            case nameof(Profile.CarSteeringLock):
                NotifyCarLockProperties();
                break;
            case nameof(Profile.GlobalSteeringRange):
                NotifyGlobalRangeProperties();
                break;
            case nameof(Profile.InvertSteering):
                NotifyInvertSteeringProperties();
                break;
            case nameof(Profile.OutSteerMin):
                NotifyOutSteerMinProperties();
                break;
            case nameof(Profile.OutSteerMax):
                NotifyOutSteerMaxProperties();
                break;
        }
    }

    protected override void OnResolvedProfilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName)) { NotifyAllSteeringProperties(); return; }
        switch (e.PropertyName)
        {
            case nameof(ResolvedProfile.UseSteeringLockClamp):
                NotifyLockClampProperties();
                break;
            case nameof(ResolvedProfile.CarSteeringLock):
                NotifyCarLockProperties();
                break;
            case nameof(ResolvedProfile.GlobalSteeringRange):
                NotifyGlobalRangeProperties();
                break;
            case nameof(ResolvedProfile.InvertSteering):
                NotifyInvertSteeringProperties();
                break;
            case nameof(ResolvedProfile.OutSteerMin):
                NotifyOutSteerMinProperties();
                break;
            case nameof(ResolvedProfile.OutSteerMax):
                NotifyOutSteerMaxProperties();
                break;
        }
    }

    protected override void RefreshSettings()
    {
        NotifyAllSteeringProperties();
    }

    public void NotifyAllSteeringProperties()
    {
        NotifyLockClampProperties();
        NotifyCarLockProperties();
        NotifyGlobalRangeProperties();
        NotifyInvertSteeringProperties();
        NotifyOutSteerMinProperties();
        NotifyOutSteerMaxProperties();
    }
}
