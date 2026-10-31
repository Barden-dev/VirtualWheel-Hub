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

/// <summary>Coordinates steering settings and grouped profile commands.</summary>
public partial class SteeringViewModel : ViewModelBase, IDisposable
{
    private readonly ProfileSession _profileSession;
    private readonly ProfileSettingsViewModel[] _sections;
    private bool _disposed;
    public ProfileSession ProfileSession => _profileSession;
    public Profile CurrentProfile => _profileSession.CurrentProfile;
    public ResolvedProfile ResolvedProfile => _profileSession.ResolvedProfile;
    public MouseSteeringSettingsViewModel Mouse { get; }
    public KeyboardSteeringSettingsViewModel Keyboard { get; }
    public SteeringOutputSettingsViewModel Output { get; }
    public SteeringViewModel(ProfileSession profileSession, ILocalizationManager localizationManager)
    {
        _profileSession = profileSession ?? throw new ArgumentNullException(nameof(profileSession));
        ArgumentNullException.ThrowIfNull(localizationManager);
        Mouse = new MouseSteeringSettingsViewModel(profileSession);
        Keyboard = new KeyboardSteeringSettingsViewModel(profileSession);
        Output = new SteeringOutputSettingsViewModel(profileSession, localizationManager);
        _sections = new ProfileSettingsViewModel[] { Mouse, Keyboard, Output };
        foreach (var section in _sections)
        {
            section.PropertyChanged += SectionChanged;
            section.PropertyChanging += SectionChanging;
        }
        _profileSession.UndoStateChanged += OnUndoStateChanged;
    }

    public double InputValue { get => Mouse.InputValue; set => Mouse.InputValue = value; }
    public double NormalizedOutput => Mouse.NormalizedOutput;
    public IReadOnlyList<SteeringCurvePoint> CurvePoints => Mouse.CurvePoints;
    public string CurveGammaText => Mouse.CurveGammaText;
    public double? MouseSensitivity { get => Mouse.MouseSensitivity; set => Mouse.MouseSensitivity = value; }
    public double? SteeringSensitivity { get => Mouse.SteeringSensitivity; set => Mouse.SteeringSensitivity = value; }
    public double EffectiveMouseSensitivity => Mouse.EffectiveMouseSensitivity;
    public double EffectiveSteeringSensitivity => Mouse.EffectiveSteeringSensitivity;
    public bool IsMouseSensitivityOverridden => Mouse.IsMouseSensitivityOverridden;
    public bool IsSteeringSensitivityOverridden => Mouse.IsSteeringSensitivityOverridden;
    public double? SteeringGamma { get => Mouse.SteeringGamma; set => Mouse.SteeringGamma = value; }
    public double? SteeringLinearity { get => Mouse.SteeringLinearity; set => Mouse.SteeringLinearity = value; }
    public double EffectiveSteeringGamma { get => Mouse.EffectiveSteeringGamma; set => Mouse.EffectiveSteeringGamma = value; }
    public double EffectiveSteeringLinearity => Mouse.EffectiveSteeringLinearity;
    public bool IsSteeringGammaOverridden => Mouse.IsSteeringGammaOverridden;
    public bool IsSteeringLinearityOverridden => Mouse.IsSteeringLinearityOverridden;
    public int? SteeringOffset { get => Mouse.SteeringOffset; set => Mouse.SteeringOffset = value; }
    public int? SteeringDeadzone { get => Mouse.SteeringDeadzone; set => Mouse.SteeringDeadzone = value; }
    public int EffectiveSteeringOffset => Mouse.EffectiveSteeringOffset;
    public int EffectiveSteeringDeadzone => Mouse.EffectiveSteeringDeadzone;
    public bool IsSteeringOffsetOverridden => Mouse.IsSteeringOffsetOverridden;
    public bool IsSteeringDeadzoneOverridden => Mouse.IsSteeringDeadzoneOverridden;
    public bool? UseKeyboardOnlyMode { get => Keyboard.UseKeyboardOnlyMode; set => Keyboard.UseKeyboardOnlyMode = value; }
    public bool EffectiveUseKeyboardOnlyMode { get => Keyboard.EffectiveUseKeyboardOnlyMode; set => Keyboard.EffectiveUseKeyboardOnlyMode = value; }
    public bool IsUseKeyboardOnlyModeOverridden => Keyboard.IsUseKeyboardOnlyModeOverridden;
    public double? KeySteerRampUpMs { get => Keyboard.KeySteerRampUpMs; set => Keyboard.KeySteerRampUpMs = value; }
    public double? KeyboardSteeringRampTime { get => Keyboard.KeyboardSteeringRampTime; set => Keyboard.KeyboardSteeringRampTime = value; }
    public double EffectiveKeySteerRampUpMs => Keyboard.EffectiveKeySteerRampUpMs;
    public double EffectiveKeyboardSteeringRampTime => Keyboard.EffectiveKeyboardSteeringRampTime;
    public bool IsKeySteerRampUpMsOverridden => Keyboard.IsKeySteerRampUpMsOverridden;
    public bool IsKeyboardSteeringRampTimeOverridden => Keyboard.IsKeyboardSteeringRampTimeOverridden;
    public double? KeySteerRampDownMs { get => Keyboard.KeySteerRampDownMs; set => Keyboard.KeySteerRampDownMs = value; }
    public double? CenterSteeringReturnSpeed { get => Keyboard.CenterSteeringReturnSpeed; set => Keyboard.CenterSteeringReturnSpeed = value; }
    public double EffectiveKeySteerRampDownMs => Keyboard.EffectiveKeySteerRampDownMs;
    public double EffectiveCenterSteeringReturnSpeed => Keyboard.EffectiveCenterSteeringReturnSpeed;
    public bool IsKeySteerRampDownMsOverridden => Keyboard.IsKeySteerRampDownMsOverridden;
    public bool IsCenterSteeringReturnSpeedOverridden => Keyboard.IsCenterSteeringReturnSpeedOverridden;
    public bool? KeySteerSnapToFullLock { get => Keyboard.KeySteerSnapToFullLock; set => Keyboard.KeySteerSnapToFullLock = value; }
    public bool? KeyboardSteeringSnapAngle { get => Keyboard.KeyboardSteeringSnapAngle; set => Keyboard.KeyboardSteeringSnapAngle = value; }
    public bool EffectiveKeySteerSnapToFullLock { get => Keyboard.EffectiveKeySteerSnapToFullLock; set => Keyboard.EffectiveKeySteerSnapToFullLock = value; }
    public bool EffectiveKeyboardSteeringSnapAngle => Keyboard.EffectiveKeyboardSteeringSnapAngle;
    public bool IsKeySteerSnapToFullLockOverridden => Keyboard.IsKeySteerSnapToFullLockOverridden;
    public bool IsKeyboardSteeringSnapAngleOverridden => Keyboard.IsKeyboardSteeringSnapAngleOverridden;
    public int? KeyCenterSteering1 { get => Keyboard.KeyCenterSteering1; set => Keyboard.KeyCenterSteering1 = value; }
    public int? KeyCenterSteering { get => Keyboard.KeyCenterSteering; set => Keyboard.KeyCenterSteering = value; }
    public int EffectiveKeyCenterSteering1 => Keyboard.EffectiveKeyCenterSteering1;
    public int EffectiveKeyCenterSteering => Keyboard.EffectiveKeyCenterSteering;
    public bool IsKeyCenterSteering1Overridden => Keyboard.IsKeyCenterSteering1Overridden;
    public bool IsKeyCenterSteeringOverridden => Keyboard.IsKeyCenterSteeringOverridden;
    public int? KeyCenterSteering2 { get => Keyboard.KeyCenterSteering2; set => Keyboard.KeyCenterSteering2 = value; }
    public int EffectiveKeyCenterSteering2 => Keyboard.EffectiveKeyCenterSteering2;
    public bool IsKeyCenterSteering2Overridden => Keyboard.IsKeyCenterSteering2Overridden;
    public bool? UseSteeringLockClamp { get => Output.UseSteeringLockClamp; set => Output.UseSteeringLockClamp = value; }
    public bool EffectiveUseSteeringLockClamp { get => Output.EffectiveUseSteeringLockClamp; set => Output.EffectiveUseSteeringLockClamp = value; }
    public bool IsUseSteeringLockClampOverridden => Output.IsUseSteeringLockClampOverridden;
    public int? CarSteeringLock { get => Output.CarSteeringLock; set => Output.CarSteeringLock = value; }
    public int? SteeringWheelRotationDeg { get => Output.SteeringWheelRotationDeg; set => Output.SteeringWheelRotationDeg = value; }
    public int EffectiveCarSteeringLock => Output.EffectiveCarSteeringLock;
    public int EffectiveSteeringWheelRotationDeg => Output.EffectiveSteeringWheelRotationDeg;
    public bool IsCarSteeringLockOverridden => Output.IsCarSteeringLockOverridden;
    public bool IsSteeringWheelRotationDegOverridden => Output.IsSteeringWheelRotationDegOverridden;
    public int? GlobalSteeringRange { get => Output.GlobalSteeringRange; set => Output.GlobalSteeringRange = value; }
    public int? MaxWheelAngle { get => Output.MaxWheelAngle; set => Output.MaxWheelAngle = value; }
    public int EffectiveGlobalSteeringRange => Output.EffectiveGlobalSteeringRange;
    public int EffectiveMaxWheelAngle => Output.EffectiveMaxWheelAngle;
    public bool IsGlobalSteeringRangeOverridden => Output.IsGlobalSteeringRangeOverridden;
    public bool IsMaxWheelAngleOverridden => Output.IsMaxWheelAngleOverridden;
    public bool? InvertSteering { get => Output.InvertSteering; set => Output.InvertSteering = value; }
    public bool EffectiveInvertSteering { get => Output.EffectiveInvertSteering; set => Output.EffectiveInvertSteering = value; }
    public bool IsInvertSteeringOverridden => Output.IsInvertSteeringOverridden;
    public int? OutSteerMin { get => Output.OutSteerMin; set => Output.OutSteerMin = value; }
    public int EffectiveOutSteerMin => Output.EffectiveOutSteerMin;
    public bool IsOutSteerMinOverridden => Output.IsOutSteerMinOverridden;
    public int? OutSteerMax { get => Output.OutSteerMax; set => Output.OutSteerMax = value; }
    public int EffectiveOutSteerMax => Output.EffectiveOutSteerMax;
    public bool IsOutSteerMaxOverridden => Output.IsOutSteerMaxOverridden;
    public double NormalizedSteer { get => Mouse.NormalizedSteer; set => Mouse.NormalizedSteer = value; }
    public int SteerValue { get => Mouse.SteerValue; set => Mouse.SteerValue = value; }
    public double? LiveOutput { get => Mouse.LiveOutput; set => Mouse.LiveOutput = value; }
    public void ClearMouseSensitivityOverride() => Mouse.ClearMouseSensitivityOverride();
    public IRelayCommand ClearMouseSensitivityOverrideCommand => Mouse.ClearMouseSensitivityOverrideCommand;
    public void ClearSteeringSensitivityOverride() => Mouse.ClearSteeringSensitivityOverride();
    public void ClearSteeringGammaOverride() => Mouse.ClearSteeringGammaOverride();
    public IRelayCommand ClearSteeringGammaOverrideCommand => Mouse.ClearSteeringGammaOverrideCommand;
    public void ClearSteeringLinearityOverride() => Mouse.ClearSteeringLinearityOverride();
    public void ClearSteeringOffsetOverride() => Mouse.ClearSteeringOffsetOverride();
    public IRelayCommand ClearSteeringOffsetOverrideCommand => Mouse.ClearSteeringOffsetOverrideCommand;
    public void ClearSteeringDeadzoneOverride() => Mouse.ClearSteeringDeadzoneOverride();
    public void ClearUseKeyboardOnlyModeOverride() => Keyboard.ClearUseKeyboardOnlyModeOverride();
    public IRelayCommand ClearUseKeyboardOnlyModeOverrideCommand => Keyboard.ClearUseKeyboardOnlyModeOverrideCommand;
    public void ClearKeySteerRampUpMsOverride() => Keyboard.ClearKeySteerRampUpMsOverride();
    public IRelayCommand ClearKeySteerRampUpMsOverrideCommand => Keyboard.ClearKeySteerRampUpMsOverrideCommand;
    public void ClearKeyboardSteeringRampTimeOverride() => Keyboard.ClearKeyboardSteeringRampTimeOverride();
    public void ClearKeySteerRampDownMsOverride() => Keyboard.ClearKeySteerRampDownMsOverride();
    public IRelayCommand ClearKeySteerRampDownMsOverrideCommand => Keyboard.ClearKeySteerRampDownMsOverrideCommand;
    public void ClearCenterSteeringReturnSpeedOverride() => Keyboard.ClearCenterSteeringReturnSpeedOverride();
    public void ClearKeySteerSnapToFullLockOverride() => Keyboard.ClearKeySteerSnapToFullLockOverride();
    public IRelayCommand ClearKeySteerSnapToFullLockOverrideCommand => Keyboard.ClearKeySteerSnapToFullLockOverrideCommand;
    public void ClearKeyboardSteeringSnapAngleOverride() => Keyboard.ClearKeyboardSteeringSnapAngleOverride();
    public void ClearKeyCenterSteering1Override() => Keyboard.ClearKeyCenterSteering1Override();
    public void ClearKeyCenterSteeringOverride() => Keyboard.ClearKeyCenterSteeringOverride();
    public void ClearKeyCenterSteering2Override() => Keyboard.ClearKeyCenterSteering2Override();
    public void ClearUseSteeringLockClampOverride() => Output.ClearUseSteeringLockClampOverride();
    public IRelayCommand ClearUseSteeringLockClampOverrideCommand => Output.ClearUseSteeringLockClampOverrideCommand;
    public void ClearCarSteeringLockOverride() => Output.ClearCarSteeringLockOverride();
    public IRelayCommand ClearCarSteeringLockOverrideCommand => Output.ClearCarSteeringLockOverrideCommand;
    public void ClearSteeringWheelRotationDegOverride() => Output.ClearSteeringWheelRotationDegOverride();
    public void ClearGlobalSteeringRangeOverride() => Output.ClearGlobalSteeringRangeOverride();
    public IRelayCommand ClearGlobalSteeringRangeOverrideCommand => Output.ClearGlobalSteeringRangeOverrideCommand;
    public void ClearMaxWheelAngleOverride() => Output.ClearMaxWheelAngleOverride();
    public void ClearInvertSteeringOverride() => Output.ClearInvertSteeringOverride();
    public IRelayCommand ClearInvertSteeringOverrideCommand => Output.ClearInvertSteeringOverrideCommand;
    public void ClearOutSteerMinOverride() => Output.ClearOutSteerMinOverride();
    public IRelayCommand ClearOutSteerMinOverrideCommand => Output.ClearOutSteerMinOverrideCommand;
    public void ClearOutSteerMaxOverride() => Output.ClearOutSteerMaxOverride();
    public IRelayCommand ClearOutSteerMaxOverrideCommand => Output.ClearOutSteerMaxOverrideCommand;
    public double EvaluateCurve(double input) => Mouse.EvaluateCurve(input);
    public void OnMonitorMarkerDragged(string markerName, double value) => Output.OnMonitorMarkerDragged(markerName, value);
    [RelayCommand]
    public void ResetSteering()
    {
        using var edit = _profileSession.BeginEditGroup(nameof(ResetSteering));
        if (CurrentProfile == null)
            return;
        var defaultProfile = ProfileDefaults.CreateDefault();
        MouseSensitivity = defaultProfile.MouseSensitivity;
        SteeringGamma = defaultProfile.SteeringGamma;
        SteeringOffset = defaultProfile.SteeringOffset;
        ;
        ;
        ;
        ;
        UseKeyboardOnlyMode = defaultProfile.UseKeyboardOnlyMode;
        KeySteerRampUpMs = defaultProfile.KeySteerRampUpMs;
        KeySteerRampDownMs = defaultProfile.KeySteerRampDownMs;
        KeySteerSnapToFullLock = defaultProfile.KeySteerSnapToFullLock;
        KeyCenterSteering1 = defaultProfile.KeyCenterSteering1;
        KeyCenterSteering2 = defaultProfile.KeyCenterSteering2;
        UseSteeringLockClamp = defaultProfile.UseSteeringLockClamp;
        CarSteeringLock = defaultProfile.CarSteeringLock;
        GlobalSteeringRange = defaultProfile.GlobalSteeringRange;
        InvertSteering = defaultProfile.InvertSteering;
        OutSteerMin = defaultProfile.OutSteerMin;
        OutSteerMax = defaultProfile.OutSteerMax;
    }

    [RelayCommand]
    public void ClearOverride(string? propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
            return;
        switch (propertyName)
        {
            case nameof(MouseSensitivity):
            case nameof(SteeringSensitivity):
                ClearMouseSensitivityOverride();
                break;
            case nameof(SteeringGamma):
            case nameof(SteeringLinearity):
                ClearSteeringGammaOverride();
                break;
            case nameof(SteeringOffset):
            case nameof(SteeringDeadzone):
                ClearSteeringOffsetOverride();
                break;
            case nameof(UseKeyboardOnlyMode):
                ClearUseKeyboardOnlyModeOverride();
                break;
            case nameof(KeySteerRampUpMs):
            case nameof(KeyboardSteeringRampTime):
                ClearKeySteerRampUpMsOverride();
                break;
            case nameof(KeySteerRampDownMs):
            case nameof(CenterSteeringReturnSpeed):
                ClearKeySteerRampDownMsOverride();
                break;
            case nameof(KeySteerSnapToFullLock):
            case nameof(KeyboardSteeringSnapAngle):
                ClearKeySteerSnapToFullLockOverride();
                break;
            case nameof(KeyCenterSteering1):
            case nameof(KeyCenterSteering):
                ClearKeyCenterSteering1Override();
                break;
            case nameof(KeyCenterSteering2):
                ClearKeyCenterSteering2Override();
                break;
            case nameof(UseSteeringLockClamp):
                ClearUseSteeringLockClampOverride();
                break;
            case nameof(CarSteeringLock):
            case nameof(SteeringWheelRotationDeg):
                ClearCarSteeringLockOverride();
                break;
            case nameof(GlobalSteeringRange):
            case nameof(MaxWheelAngle):
                ClearGlobalSteeringRangeOverride();
                break;
            case nameof(InvertSteering):
                ClearInvertSteeringOverride();
                break;
            case nameof(OutSteerMin):
                ClearOutSteerMinOverride();
                break;
            case nameof(OutSteerMax):
                ClearOutSteerMaxOverride();
                break;
        }
    }

    [RelayCommand]
    public void ClearAllOverrides()
    {
        ClearAllSteeringOverrides();
    }

    public void ClearAllSteeringOverrides()
    {
        using var edit = _profileSession.BeginEditGroup(nameof(ClearAllSteeringOverrides));
        ClearMouseSensitivityOverride();
        ClearSteeringGammaOverride();
        ClearSteeringOffsetOverride();
        ;
        ;
        ;
        ;
        ClearUseKeyboardOnlyModeOverride();
        ClearKeySteerRampUpMsOverride();
        ClearKeySteerRampDownMsOverride();
        ClearKeySteerSnapToFullLockOverride();
        ClearKeyCenterSteering1Override();
        ClearKeyCenterSteering2Override();
        ClearUseSteeringLockClampOverride();
        ClearCarSteeringLockOverride();
        ClearGlobalSteeringRangeOverride();
        ClearInvertSteeringOverride();
        ClearOutSteerMinOverride();
        ClearOutSteerMaxOverride();
    }

    [RelayCommand]
    public void RevertProfile()
    {
        _profileSession.RevertProfile();
    }

    public void NotifyAllSteeringProperties()
    {
        Mouse.NotifyAllSteeringProperties();
        Keyboard.NotifyAllSteeringProperties();
        Output.NotifyAllSteeringProperties();
    }
    private void OnUndoStateChanged(object? sender, EventArgs e) => NotifyAllSteeringProperties();
    private void SectionChanged(object? sender, PropertyChangedEventArgs e) => OnPropertyChanged(e);
    private void SectionChanging(object? sender, PropertyChangingEventArgs e) => OnPropertyChanging(e);
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _profileSession.UndoStateChanged -= OnUndoStateChanged;
        foreach (var section in _sections)
        {
            section.PropertyChanged -= SectionChanged;
            section.PropertyChanging -= SectionChanging;
            section.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}
