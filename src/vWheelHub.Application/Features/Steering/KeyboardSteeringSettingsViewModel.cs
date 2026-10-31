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

public partial class KeyboardSteeringSettingsViewModel : ProfileSettingsViewModel
{
    public KeyboardSteeringSettingsViewModel(ProfileSession session) : base(session)
    {
        StartObserving();
    }

    public bool? UseKeyboardOnlyMode
    {
        get => CurrentProfile?.UseKeyboardOnlyMode;
        set
        {
            if (CurrentProfile != null && CurrentProfile.UseKeyboardOnlyMode != value)
            {
                CurrentProfile.UseKeyboardOnlyMode = value;
                NotifyKeyboardOnlyProperties();
            }
        }
    }

    public bool EffectiveUseKeyboardOnlyMode { get => CurrentProfile?.UseKeyboardOnlyMode ?? ResolvedProfile?.UseKeyboardOnlyMode ?? false; set => UseKeyboardOnlyMode = value; }

    public bool IsUseKeyboardOnlyModeOverridden => CurrentProfile?.UseKeyboardOnlyMode.HasValue ?? false;

    [RelayCommand]
    public void ClearUseKeyboardOnlyModeOverride() => UseKeyboardOnlyMode = null;

    public double? KeySteerRampUpMs
    {
        get => CurrentProfile?.KeySteerRampUpMs;
        set
        {
            if (CurrentProfile == null) return;
            double? clamped = value.HasValue ? Math.Clamp(value.Value, 1.0, 5000.0) : null;
            if (CurrentProfile.KeySteerRampUpMs != clamped)
            {
                CurrentProfile.KeySteerRampUpMs = clamped;
                NotifyRampUpProperties();
            }
        }
    }

    public double? KeyboardSteeringRampTime
    {
        get => KeySteerRampUpMs;
        set => KeySteerRampUpMs = value;
    }

    public double EffectiveKeySteerRampUpMs { get => CurrentProfile?.KeySteerRampUpMs ?? ResolvedProfile?.KeySteerRampUpMs ?? 200.0; set { if (value != EffectiveKeySteerRampUpMs) KeySteerRampUpMs = value; } }

    public double EffectiveKeyboardSteeringRampTime => EffectiveKeySteerRampUpMs;

    public bool IsKeySteerRampUpMsOverridden => CurrentProfile?.KeySteerRampUpMs.HasValue ?? false;

    public bool IsKeyboardSteeringRampTimeOverridden => IsKeySteerRampUpMsOverridden;

    [RelayCommand]
    public void ClearKeySteerRampUpMsOverride() => KeySteerRampUpMs = null;

    public void ClearKeyboardSteeringRampTimeOverride() => ClearKeySteerRampUpMsOverride();

    public double? KeySteerRampDownMs
    {
        get => CurrentProfile?.KeySteerRampDownMs;
        set
        {
            if (CurrentProfile == null) return;
            double? clamped = value.HasValue ? Math.Clamp(value.Value, 1.0, 5000.0) : null;
            if (CurrentProfile.KeySteerRampDownMs != clamped)
            {
                CurrentProfile.KeySteerRampDownMs = clamped;
                NotifyRampDownProperties();
            }
        }
    }

    public double? CenterSteeringReturnSpeed
    {
        get => KeySteerRampDownMs;
        set => KeySteerRampDownMs = value;
    }

    public double EffectiveKeySteerRampDownMs { get => CurrentProfile?.KeySteerRampDownMs ?? ResolvedProfile?.KeySteerRampDownMs ?? 150.0; set { if (value != EffectiveKeySteerRampDownMs) KeySteerRampDownMs = value; } }

    public double EffectiveCenterSteeringReturnSpeed => EffectiveKeySteerRampDownMs;

    public bool IsKeySteerRampDownMsOverridden => CurrentProfile?.KeySteerRampDownMs.HasValue ?? false;

    public bool IsCenterSteeringReturnSpeedOverridden => IsKeySteerRampDownMsOverridden;

    [RelayCommand]
    public void ClearKeySteerRampDownMsOverride() => KeySteerRampDownMs = null;

    public void ClearCenterSteeringReturnSpeedOverride() => ClearKeySteerRampDownMsOverride();

    public bool? KeySteerSnapToFullLock
    {
        get => CurrentProfile?.KeySteerSnapToFullLock;
        set
        {
            if (CurrentProfile != null && CurrentProfile.KeySteerSnapToFullLock != value)
            {
                CurrentProfile.KeySteerSnapToFullLock = value;
                NotifySnapProperties();
            }
        }
    }

    public bool? KeyboardSteeringSnapAngle
    {
        get => KeySteerSnapToFullLock;
        set => KeySteerSnapToFullLock = value;
    }

    public bool EffectiveKeySteerSnapToFullLock { get => CurrentProfile?.KeySteerSnapToFullLock ?? ResolvedProfile?.KeySteerSnapToFullLock ?? false; set => KeySteerSnapToFullLock = value; }

    public bool EffectiveKeyboardSteeringSnapAngle => EffectiveKeySteerSnapToFullLock;

    public bool IsKeySteerSnapToFullLockOverridden => CurrentProfile?.KeySteerSnapToFullLock.HasValue ?? false;

    public bool IsKeyboardSteeringSnapAngleOverridden => IsKeySteerSnapToFullLockOverridden;

    [RelayCommand]
    public void ClearKeySteerSnapToFullLockOverride() => KeySteerSnapToFullLock = null;

    public void ClearKeyboardSteeringSnapAngleOverride() => ClearKeySteerSnapToFullLockOverride();

    public int? KeyCenterSteering1
    {
        get => CurrentProfile?.KeyCenterSteering1;
        set
        {
            if (CurrentProfile != null && CurrentProfile.KeyCenterSteering1 != value)
            {
                CurrentProfile.KeyCenterSteering1 = value;
                NotifyCenterKey1Properties();
            }
        }
    }

    public int? KeyCenterSteering
    {
        get => KeyCenterSteering1;
        set => KeyCenterSteering1 = value;
    }

    public int EffectiveKeyCenterSteering1 => CurrentProfile?.KeyCenterSteering1 ?? ResolvedProfile?.KeyCenterSteering1 ?? 0x20;

    public int EffectiveKeyCenterSteering => EffectiveKeyCenterSteering1;

    public bool IsKeyCenterSteering1Overridden => CurrentProfile?.KeyCenterSteering1.HasValue ?? false;

    public bool IsKeyCenterSteeringOverridden => IsKeyCenterSteering1Overridden;

    public void ClearKeyCenterSteering1Override() => KeyCenterSteering1 = null;

    public void ClearKeyCenterSteeringOverride() => ClearKeyCenterSteering1Override();

    public int? KeyCenterSteering2
    {
        get => CurrentProfile?.KeyCenterSteering2;
        set
        {
            if (CurrentProfile != null && CurrentProfile.KeyCenterSteering2 != value)
            {
                CurrentProfile.KeyCenterSteering2 = value;
                NotifyCenterKey2Properties();
            }
        }
    }

    public int EffectiveKeyCenterSteering2 => CurrentProfile?.KeyCenterSteering2 ?? ResolvedProfile?.KeyCenterSteering2 ?? 0;

    public bool IsKeyCenterSteering2Overridden => CurrentProfile?.KeyCenterSteering2.HasValue ?? false;

    public void ClearKeyCenterSteering2Override() => KeyCenterSteering2 = null;

    private void NotifyKeyboardOnlyProperties()
    {
        OnPropertyChanged(nameof(UseKeyboardOnlyMode));
        OnPropertyChanged(nameof(EffectiveUseKeyboardOnlyMode));
        OnPropertyChanged(nameof(IsUseKeyboardOnlyModeOverridden));
    }

    private void NotifyRampUpProperties()
    {
        OnPropertyChanged(nameof(KeySteerRampUpMs));
        OnPropertyChanged(nameof(KeyboardSteeringRampTime));
        OnPropertyChanged(nameof(EffectiveKeySteerRampUpMs));
        OnPropertyChanged(nameof(EffectiveKeyboardSteeringRampTime));
        OnPropertyChanged(nameof(IsKeySteerRampUpMsOverridden));
        OnPropertyChanged(nameof(IsKeyboardSteeringRampTimeOverridden));
    }

    private void NotifyRampDownProperties()
    {
        OnPropertyChanged(nameof(KeySteerRampDownMs));
        OnPropertyChanged(nameof(CenterSteeringReturnSpeed));
        OnPropertyChanged(nameof(EffectiveKeySteerRampDownMs));
        OnPropertyChanged(nameof(EffectiveCenterSteeringReturnSpeed));
        OnPropertyChanged(nameof(IsKeySteerRampDownMsOverridden));
        OnPropertyChanged(nameof(IsCenterSteeringReturnSpeedOverridden));
    }

    private void NotifySnapProperties()
    {
        OnPropertyChanged(nameof(KeySteerSnapToFullLock));
        OnPropertyChanged(nameof(KeyboardSteeringSnapAngle));
        OnPropertyChanged(nameof(EffectiveKeySteerSnapToFullLock));
        OnPropertyChanged(nameof(EffectiveKeyboardSteeringSnapAngle));
        OnPropertyChanged(nameof(IsKeySteerSnapToFullLockOverridden));
        OnPropertyChanged(nameof(IsKeyboardSteeringSnapAngleOverridden));
    }

    private void NotifyCenterKey1Properties()
    {
        OnPropertyChanged(nameof(KeyCenterSteering1));
        OnPropertyChanged(nameof(KeyCenterSteering));
        OnPropertyChanged(nameof(EffectiveKeyCenterSteering1));
        OnPropertyChanged(nameof(EffectiveKeyCenterSteering));
        OnPropertyChanged(nameof(IsKeyCenterSteering1Overridden));
        OnPropertyChanged(nameof(IsKeyCenterSteeringOverridden));
    }

    private void NotifyCenterKey2Properties()
    {
        OnPropertyChanged(nameof(KeyCenterSteering2));
        OnPropertyChanged(nameof(EffectiveKeyCenterSteering2));
        OnPropertyChanged(nameof(IsKeyCenterSteering2Overridden));
    }

    protected override void OnCurrentProfilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName)) { NotifyAllSteeringProperties(); return; }
        switch (e.PropertyName)
        {
            case nameof(Profile.UseKeyboardOnlyMode):
                NotifyKeyboardOnlyProperties();
                break;
            case nameof(Profile.KeySteerRampUpMs):
                NotifyRampUpProperties();
                break;
            case nameof(Profile.KeySteerRampDownMs):
                NotifyRampDownProperties();
                break;
            case nameof(Profile.KeySteerSnapToFullLock):
                NotifySnapProperties();
                break;
            case nameof(Profile.KeyCenterSteering1):
                NotifyCenterKey1Properties();
                break;
            case nameof(Profile.KeyCenterSteering2):
                NotifyCenterKey2Properties();
                break;
        }
    }

    protected override void OnResolvedProfilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName)) { NotifyAllSteeringProperties(); return; }
        switch (e.PropertyName)
        {
            case nameof(ResolvedProfile.UseKeyboardOnlyMode):
                NotifyKeyboardOnlyProperties();
                break;
            case nameof(ResolvedProfile.KeySteerRampUpMs):
                NotifyRampUpProperties();
                break;
            case nameof(ResolvedProfile.KeySteerRampDownMs):
                NotifyRampDownProperties();
                break;
            case nameof(ResolvedProfile.KeySteerSnapToFullLock):
                NotifySnapProperties();
                break;
            case nameof(ResolvedProfile.KeyCenterSteering1):
                NotifyCenterKey1Properties();
                break;
            case nameof(ResolvedProfile.KeyCenterSteering2):
                NotifyCenterKey2Properties();
                break;
        }
    }

    protected override void RefreshSettings()
    {
        NotifyAllSteeringProperties();
    }

    public void NotifyAllSteeringProperties()
    {
        NotifyKeyboardOnlyProperties();
        NotifyRampUpProperties();
        NotifyRampDownProperties();
        NotifySnapProperties();
        NotifyCenterKey1Properties();
        NotifyCenterKey2Properties();
    }
}
