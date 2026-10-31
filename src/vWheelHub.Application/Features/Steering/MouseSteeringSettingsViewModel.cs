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

public partial class MouseSteeringSettingsViewModel : ProfileSettingsViewModel
{
    public MouseSteeringSettingsViewModel(ProfileSession session) : base(session)
    {
        StartObserving();
        UpdateCurveGeometry();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InputValue))]
    [NotifyPropertyChangedFor(nameof(NormalizedOutput))]
    private double _normalizedSteer;

    [ObservableProperty]
    private int _steerValue = 16384;

    [ObservableProperty]
    private double? _liveOutput;

    partial void OnSteerValueChanged(int value)
    {
        NormalizedSteer = Math.Clamp((value - 16384.0) / 16384.0, -1.0, 1.0);
    }

    public double InputValue
    {
        get => NormalizedSteer;
        set => NormalizedSteer = Math.Clamp(value, -1.0, 1.0);
    }

    public double NormalizedOutput => EvaluateCurve(NormalizedSteer);

    public IReadOnlyList<SteeringCurvePoint> CurvePoints { get; private set; } = Array.Empty<SteeringCurvePoint>();

    public string CurveGammaText => $"γ = {EffectiveSteeringGamma.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}";

    public double? MouseSensitivity
    {
        get => CurrentProfile?.MouseSensitivity;
        set
        {
            if (CurrentProfile == null) return;
            double? clamped = value.HasValue ? Math.Clamp(value.Value, 0.01, 100.0) : null;
            if (CurrentProfile.MouseSensitivity != clamped)
            {
                CurrentProfile.MouseSensitivity = clamped;
                NotifySensitivityProperties();
            }
        }
    }

    public double? SteeringSensitivity
    {
        get => MouseSensitivity;
        set => MouseSensitivity = value;
    }

    public double EffectiveMouseSensitivity { get => CurrentProfile?.MouseSensitivity ?? ResolvedProfile?.MouseSensitivity ?? 3.7; set { if (value != EffectiveMouseSensitivity) MouseSensitivity = value; } }

    public double EffectiveSteeringSensitivity => EffectiveMouseSensitivity;

    public bool IsMouseSensitivityOverridden => CurrentProfile?.MouseSensitivity.HasValue ?? false;

    public bool IsSteeringSensitivityOverridden => IsMouseSensitivityOverridden;

    [RelayCommand]
    public void ClearMouseSensitivityOverride() => MouseSensitivity = null;

    public void ClearSteeringSensitivityOverride() => ClearMouseSensitivityOverride();

    public double? SteeringGamma
    {
        get => CurrentProfile?.SteeringGamma;
        set
        {
            if (CurrentProfile == null) return;
            double? clamped = value.HasValue ? Math.Clamp(value.Value, 0.1, 5.0) : null;
            if (CurrentProfile.SteeringGamma != clamped)
            {
                CurrentProfile.SteeringGamma = clamped;
                UpdateCurveGeometry();
                NotifyLinearityProperties();
            }
        }
    }

    public double? SteeringLinearity
    {
        get => SteeringGamma;
        set => SteeringGamma = value;
    }

    public double EffectiveSteeringGamma { get => CurrentProfile?.SteeringGamma ?? ResolvedProfile?.SteeringGamma ?? 1.0; set => SteeringGamma = value; }

    public double EffectiveSteeringLinearity => EffectiveSteeringGamma;

    public bool IsSteeringGammaOverridden => CurrentProfile?.SteeringGamma.HasValue ?? false;

    public bool IsSteeringLinearityOverridden => IsSteeringGammaOverridden;

    [RelayCommand]
    public void ClearSteeringGammaOverride() => SteeringGamma = null;

    public void ClearSteeringLinearityOverride() => ClearSteeringGammaOverride();

    public int? SteeringOffset
    {
        get => CurrentProfile?.SteeringOffset;
        set
        {
            if (CurrentProfile == null) return;
            int? clamped = value.HasValue ? Math.Clamp(value.Value, -32768, 32768) : null;
            if (CurrentProfile.SteeringOffset != clamped)
            {
                CurrentProfile.SteeringOffset = clamped;
                NotifyOffsetProperties();
            }
        }
    }

    public int? SteeringDeadzone
    {
        get => SteeringOffset;
        set => SteeringOffset = value;
    }

    public int EffectiveSteeringOffset { get => CurrentProfile?.SteeringOffset ?? ResolvedProfile?.SteeringOffset ?? 0; set { if (value != EffectiveSteeringOffset) SteeringOffset = value; } }

    public int EffectiveSteeringDeadzone => EffectiveSteeringOffset;

    public bool IsSteeringOffsetOverridden => CurrentProfile?.SteeringOffset.HasValue ?? false;

    public bool IsSteeringDeadzoneOverridden => IsSteeringOffsetOverridden;

    [RelayCommand]
    public void ClearSteeringOffsetOverride() => SteeringOffset = null;

    public void ClearSteeringDeadzoneOverride() => ClearSteeringOffsetOverride();

    private void NotifySensitivityProperties()
    {
        OnPropertyChanged(nameof(MouseSensitivity));
        OnPropertyChanged(nameof(SteeringSensitivity));
        OnPropertyChanged(nameof(EffectiveMouseSensitivity));
        OnPropertyChanged(nameof(EffectiveSteeringSensitivity));
        OnPropertyChanged(nameof(IsMouseSensitivityOverridden));
        OnPropertyChanged(nameof(IsSteeringSensitivityOverridden));
    }

    private void NotifyLinearityProperties()
    {
        OnPropertyChanged(nameof(SteeringGamma));
        OnPropertyChanged(nameof(SteeringLinearity));
        OnPropertyChanged(nameof(EffectiveSteeringGamma));
        OnPropertyChanged(nameof(EffectiveSteeringLinearity));
        OnPropertyChanged(nameof(IsSteeringGammaOverridden));
        OnPropertyChanged(nameof(IsSteeringLinearityOverridden));
    }

    private void NotifyOffsetProperties()
    {
        OnPropertyChanged(nameof(SteeringOffset));
        OnPropertyChanged(nameof(SteeringDeadzone));
        OnPropertyChanged(nameof(EffectiveSteeringOffset));
        OnPropertyChanged(nameof(EffectiveSteeringDeadzone));
        OnPropertyChanged(nameof(IsSteeringOffsetOverridden));
        OnPropertyChanged(nameof(IsSteeringDeadzoneOverridden));
    }

    public double EvaluateCurve(double input) => SteeringCurve.Evaluate(input, EffectiveSteeringGamma);
    private void UpdateCurveGeometry()
    {
        CurvePoints = SteeringCurve.CreatePoints(EffectiveSteeringGamma);
        OnPropertyChanged(nameof(CurvePoints));
        OnPropertyChanged(nameof(CurveGammaText));
        OnPropertyChanged(nameof(NormalizedOutput));
    }

    protected override void OnCurrentProfilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName)) { NotifyAllSteeringProperties(); return; }
        switch (e.PropertyName)
        {
            case nameof(Profile.MouseSensitivity):
                NotifySensitivityProperties();
                break;
            case nameof(Profile.SteeringGamma):
                UpdateCurveGeometry();
                NotifyLinearityProperties();
                break;
            case nameof(Profile.SteeringOffset):
                NotifyOffsetProperties();
                break;
        }
    }

    protected override void OnResolvedProfilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName)) { NotifyAllSteeringProperties(); return; }
        switch (e.PropertyName)
        {
            case nameof(ResolvedProfile.MouseSensitivity):
                NotifySensitivityProperties();
                break;
            case nameof(ResolvedProfile.SteeringGamma):
                UpdateCurveGeometry();
                NotifyLinearityProperties();
                break;
            case nameof(ResolvedProfile.SteeringOffset):
                NotifyOffsetProperties();
                break;
        }
    }

    protected override void RefreshSettings()
    {
        UpdateCurveGeometry();
        NotifyAllSteeringProperties();
    }

    public void NotifyAllSteeringProperties()
    {
        NotifySensitivityProperties();
        NotifyLinearityProperties();
        NotifyOffsetProperties();
    }
}
