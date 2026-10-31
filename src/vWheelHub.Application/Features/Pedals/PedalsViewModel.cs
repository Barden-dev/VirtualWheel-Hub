using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimRacingHub.Application.Common;
using SimRacingHub.Application.Contracts;
using SimRacingHub.Application.Localization;
using SimRacingHub.Models;
using SimRacingHub.Services;

namespace SimRacingHub.Application.Features.Pedals;

public partial class PedalsViewModel : ViewModelBase, IDisposable
{
    private readonly ProfileSession _profileSession;
    private readonly ProfileSettingsViewModel[] _sections;
    private bool _disposed;
    public ProfileSession ProfileSession => _profileSession;
    public Profile CurrentProfile => _profileSession.CurrentProfile;
    public ResolvedProfile ResolvedProfile => _profileSession.ResolvedProfile;
    public ThrottleSettingsViewModel Throttle { get; }
    public BrakeSettingsViewModel Brake { get; }
    public PedalOutputSettingsViewModel Output { get; }

    public PedalsViewModel(ProfileSession profileSession, ILocalizationManager localizationManager)
    {
        _profileSession = profileSession ?? throw new ArgumentNullException(nameof(profileSession));
        ArgumentNullException.ThrowIfNull(localizationManager);
        Throttle = new ThrottleSettingsViewModel(profileSession);
        Brake = new BrakeSettingsViewModel(profileSession);
        Output = new PedalOutputSettingsViewModel(profileSession);
        _sections = new ProfileSettingsViewModel[] { Throttle, Brake, Output };
        foreach (var section in _sections)
        {
            section.PropertyChanged += SectionChanged;
            section.PropertyChanging += SectionChanging;
        }
    }

    public int EffectiveOutGasMin => Output.EffectiveOutGasMin;
    public int EffectiveOutGasMax => Output.EffectiveOutGasMax;
    public int EffectiveOutBrakeMin => Output.EffectiveOutBrakeMin;
    public int EffectiveOutBrakeMax => Output.EffectiveOutBrakeMax;
    public bool? CombinedPedals { get => Output.CombinedPedals; set => Output.CombinedPedals = value; }
    public bool EffectiveCombinedPedals { get => Output.EffectiveCombinedPedals; set => Output.EffectiveCombinedPedals = value; }
    public bool IsCombinedPedalsOverridden => Output.IsCombinedPedalsOverridden;
    public bool? InvertThrottle { get => Throttle.InvertThrottle; set => Throttle.InvertThrottle = value; }
    public bool? InvertGas { get => Throttle.InvertGas; set => Throttle.InvertGas = value; }
    public bool EffectiveInvertThrottle { get => Throttle.EffectiveInvertThrottle; set => Throttle.EffectiveInvertThrottle = value; }
    public bool EffectiveInvertGas => Throttle.EffectiveInvertGas;
    public bool IsInvertThrottleOverridden => Throttle.IsInvertThrottleOverridden;
    public bool IsInvertGasOverridden => Throttle.IsInvertGasOverridden;
    public bool? InvertBrake { get => Brake.InvertBrake; set => Brake.InvertBrake = value; }
    public bool EffectiveInvertBrake { get => Brake.EffectiveInvertBrake; set => Brake.EffectiveInvertBrake = value; }
    public bool IsInvertBrakeOverridden => Brake.IsInvertBrakeOverridden;
    public double? ThrottleLinearity { get => Throttle.ThrottleLinearity; set => Throttle.ThrottleLinearity = value; }
    public double? GasGamma { get => Throttle.GasGamma; set => Throttle.GasGamma = value; }
    public double EffectiveThrottleLinearity { get => Throttle.EffectiveThrottleLinearity; set => Throttle.EffectiveThrottleLinearity = value; }
    public double EffectiveGasGamma => Throttle.EffectiveGasGamma;
    public bool IsThrottleLinearityOverridden => Throttle.IsThrottleLinearityOverridden;
    public bool IsGasGammaOverridden => Throttle.IsGasGammaOverridden;
    public double? BrakeLinearity { get => Brake.BrakeLinearity; set => Brake.BrakeLinearity = value; }
    public double? BrakeGamma { get => Brake.BrakeGamma; set => Brake.BrakeGamma = value; }
    public double EffectiveBrakeLinearity { get => Brake.EffectiveBrakeLinearity; set => Brake.EffectiveBrakeLinearity = value; }
    public double EffectiveBrakeGamma => Brake.EffectiveBrakeGamma;
    public bool IsBrakeLinearityOverridden => Brake.IsBrakeLinearityOverridden;
    public bool IsBrakeGammaOverridden => Brake.IsBrakeGammaOverridden;
    public double? ThrottleDeadzone { get => Throttle.ThrottleDeadzone; set => Throttle.ThrottleDeadzone = value; }
    public double? GasThreshold { get => Throttle.GasThreshold; set => Throttle.GasThreshold = value; }
    public double EffectiveThrottleDeadzone => Throttle.EffectiveThrottleDeadzone;
    public double EffectiveGasThreshold => Throttle.EffectiveGasThreshold;
    public bool IsThrottleDeadzoneOverridden => Throttle.IsThrottleDeadzoneOverridden;
    public bool IsGasThresholdOverridden => Throttle.IsGasThresholdOverridden;
    public double? BrakeDeadzone { get => Brake.BrakeDeadzone; set => Brake.BrakeDeadzone = value; }
    public double? BrakeThreshold { get => Brake.BrakeThreshold; set => Brake.BrakeThreshold = value; }
    public double EffectiveBrakeDeadzone => Brake.EffectiveBrakeDeadzone;
    public double EffectiveBrakeThreshold => Brake.EffectiveBrakeThreshold;
    public bool IsBrakeDeadzoneOverridden => Brake.IsBrakeDeadzoneOverridden;
    public bool IsBrakeThresholdOverridden => Brake.IsBrakeThresholdOverridden;
    public double? ThrottleTurbo { get => Throttle.ThrottleTurbo; set => Throttle.ThrottleTurbo = value; }
    public double? GasAttackTurboSeconds { get => Throttle.GasAttackTurboSeconds; set => Throttle.GasAttackTurboSeconds = value; }
    public double? GasAttackTurbo { get => Throttle.GasAttackTurbo; set => Throttle.GasAttackTurbo = value; }
    public double EffectiveThrottleTurbo => Throttle.EffectiveThrottleTurbo;
    public double EffectiveGasAttackTurboSeconds => Throttle.EffectiveGasAttackTurboSeconds;
    public bool IsThrottleTurboOverridden => Throttle.IsThrottleTurboOverridden;
    public bool IsGasAttackTurboOverridden => Throttle.IsGasAttackTurboOverridden;
    public double? BrakeTurbo { get => Brake.BrakeTurbo; set => Brake.BrakeTurbo = value; }
    public double EffectiveBrakeTurbo => Brake.EffectiveBrakeTurbo;
    public bool IsBrakeTurboOverridden => Brake.IsBrakeTurboOverridden;
    public double? GasAttackFastSeconds { get => Throttle.GasAttackFastSeconds; set => Throttle.GasAttackFastSeconds = value; }
    public double EffectiveGasAttackFastSeconds => Throttle.EffectiveGasAttackFastSeconds;
    public bool IsGasAttackFastOverridden => Throttle.IsGasAttackFastOverridden;
    public double? GasAttackSlowSeconds { get => Throttle.GasAttackSlowSeconds; set => Throttle.GasAttackSlowSeconds = value; }
    public double EffectiveGasAttackSlowSeconds => Throttle.EffectiveGasAttackSlowSeconds;
    public bool IsGasAttackSlowOverridden => Throttle.IsGasAttackSlowOverridden;
    public double? GasEstimatedFullPressSeconds => Throttle.GasEstimatedFullPressSeconds;
    public double EffectiveGasEstimatedFullPressSeconds => Throttle.EffectiveGasEstimatedFullPressSeconds;
    public double? GasDecaySeconds { get => Throttle.GasDecaySeconds; set => Throttle.GasDecaySeconds = value; }
    public double EffectiveGasDecaySeconds => Throttle.EffectiveGasDecaySeconds;
    public bool IsGasDecayOverridden => Throttle.IsGasDecayOverridden;
    public double? GasInstantCutSeconds { get => Throttle.GasInstantCutSeconds; set => Throttle.GasInstantCutSeconds = value; }
    public double EffectiveGasInstantCutSeconds => Throttle.EffectiveGasInstantCutSeconds;
    public bool IsGasInstantCutOverridden => Throttle.IsGasInstantCutOverridden;
    public double? BrakeAttackFastSeconds { get => Brake.BrakeAttackFastSeconds; set => Brake.BrakeAttackFastSeconds = value; }
    public double EffectiveBrakeAttackFastSeconds => Brake.EffectiveBrakeAttackFastSeconds;
    public bool IsBrakeAttackFastOverridden => Brake.IsBrakeAttackFastOverridden;
    public double? BrakeAttackSlowSeconds { get => Brake.BrakeAttackSlowSeconds; set => Brake.BrakeAttackSlowSeconds = value; }
    public double EffectiveBrakeAttackSlowSeconds => Brake.EffectiveBrakeAttackSlowSeconds;
    public bool IsBrakeAttackSlowOverridden => Brake.IsBrakeAttackSlowOverridden;
    public double? BrakeEstimatedFullPressSeconds => Brake.BrakeEstimatedFullPressSeconds;
    public double EffectiveBrakeEstimatedFullPressSeconds => Brake.EffectiveBrakeEstimatedFullPressSeconds;
    public double? BrakeDecaySeconds { get => Brake.BrakeDecaySeconds; set => Brake.BrakeDecaySeconds = value; }
    public double EffectiveBrakeDecaySeconds => Brake.EffectiveBrakeDecaySeconds;
    public bool IsBrakeDecayOverridden => Brake.IsBrakeDecayOverridden;
    public double ThrottleValue { get => Output.ThrottleValue; set => Output.ThrottleValue = value; }
    public double ThrottleInput { get => Output.ThrottleInput; set => Output.ThrottleInput = value; }
    public int ThrottleRaw { get => Output.ThrottleRaw; set => Output.ThrottleRaw = value; }
    public double BrakeValue { get => Output.BrakeValue; set => Output.BrakeValue = value; }
    public double BrakeInput { get => Output.BrakeInput; set => Output.BrakeInput = value; }
    public int BrakeRaw { get => Output.BrakeRaw; set => Output.BrakeRaw = value; }
    public void ClearCombinedPedalsOverride() => Output.ClearCombinedPedalsOverride();
    public IRelayCommand ClearCombinedPedalsOverrideCommand => Output.ClearCombinedPedalsOverrideCommand;
    public void ClearInvertThrottleOverride() => Throttle.ClearInvertThrottleOverride();
    public IRelayCommand ClearInvertThrottleOverrideCommand => Throttle.ClearInvertThrottleOverrideCommand;
    public void ClearInvertGasOverride() => Throttle.ClearInvertGasOverride();
    public IRelayCommand ClearInvertGasOverrideCommand => Throttle.ClearInvertGasOverrideCommand;
    public void ClearInvertBrakeOverride() => Brake.ClearInvertBrakeOverride();
    public IRelayCommand ClearInvertBrakeOverrideCommand => Brake.ClearInvertBrakeOverrideCommand;
    public void ClearThrottleLinearityOverride() => Throttle.ClearThrottleLinearityOverride();
    public IRelayCommand ClearThrottleLinearityOverrideCommand => Throttle.ClearThrottleLinearityOverrideCommand;
    public void ClearGasGammaOverride() => Throttle.ClearGasGammaOverride();
    public IRelayCommand ClearGasGammaOverrideCommand => Throttle.ClearGasGammaOverrideCommand;
    public void ClearBrakeLinearityOverride() => Brake.ClearBrakeLinearityOverride();
    public IRelayCommand ClearBrakeLinearityOverrideCommand => Brake.ClearBrakeLinearityOverrideCommand;
    public void ClearBrakeGammaOverride() => Brake.ClearBrakeGammaOverride();
    public IRelayCommand ClearBrakeGammaOverrideCommand => Brake.ClearBrakeGammaOverrideCommand;
    public void ClearThrottleDeadzoneOverride() => Throttle.ClearThrottleDeadzoneOverride();
    public IRelayCommand ClearThrottleDeadzoneOverrideCommand => Throttle.ClearThrottleDeadzoneOverrideCommand;
    public void ClearGasThresholdOverride() => Throttle.ClearGasThresholdOverride();
    public IRelayCommand ClearGasThresholdOverrideCommand => Throttle.ClearGasThresholdOverrideCommand;
    public void ClearBrakeDeadzoneOverride() => Brake.ClearBrakeDeadzoneOverride();
    public IRelayCommand ClearBrakeDeadzoneOverrideCommand => Brake.ClearBrakeDeadzoneOverrideCommand;
    public void ClearBrakeThresholdOverride() => Brake.ClearBrakeThresholdOverride();
    public IRelayCommand ClearBrakeThresholdOverrideCommand => Brake.ClearBrakeThresholdOverrideCommand;
    public void ClearThrottleTurboOverride() => Throttle.ClearThrottleTurboOverride();
    public IRelayCommand ClearThrottleTurboOverrideCommand => Throttle.ClearThrottleTurboOverrideCommand;
    public void ClearGasAttackTurboOverride() => Throttle.ClearGasAttackTurboOverride();
    public IRelayCommand ClearGasAttackTurboOverrideCommand => Throttle.ClearGasAttackTurboOverrideCommand;
    public void ClearBrakeTurboOverride() => Brake.ClearBrakeTurboOverride();
    public IRelayCommand ClearBrakeTurboOverrideCommand => Brake.ClearBrakeTurboOverrideCommand;
    public void ClearGasAttackFastOverride() => Throttle.ClearGasAttackFastOverride();
    public IRelayCommand ClearGasAttackFastOverrideCommand => Throttle.ClearGasAttackFastOverrideCommand;
    public void ClearGasAttackSlowOverride() => Throttle.ClearGasAttackSlowOverride();
    public IRelayCommand ClearGasAttackSlowOverrideCommand => Throttle.ClearGasAttackSlowOverrideCommand;
    public void ClearGasDecayOverride() => Throttle.ClearGasDecayOverride();
    public IRelayCommand ClearGasDecayOverrideCommand => Throttle.ClearGasDecayOverrideCommand;
    public void ClearGasInstantCutOverride() => Throttle.ClearGasInstantCutOverride();
    public IRelayCommand ClearGasInstantCutOverrideCommand => Throttle.ClearGasInstantCutOverrideCommand;
    public void ClearBrakeAttackFastOverride() => Brake.ClearBrakeAttackFastOverride();
    public IRelayCommand ClearBrakeAttackFastOverrideCommand => Brake.ClearBrakeAttackFastOverrideCommand;
    public void ClearBrakeAttackSlowOverride() => Brake.ClearBrakeAttackSlowOverride();
    public IRelayCommand ClearBrakeAttackSlowOverrideCommand => Brake.ClearBrakeAttackSlowOverrideCommand;
    public void ClearBrakeDecayOverride() => Brake.ClearBrakeDecayOverride();
    public IRelayCommand ClearBrakeDecayOverrideCommand => Brake.ClearBrakeDecayOverrideCommand;

    [RelayCommand]
    public void ClearOverride(string? propertyName)
    {
        TryClearOverride(propertyName);
    }


    public bool TryClearOverride(string? propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
            return false;
        switch (propertyName)
        {
            case nameof(CombinedPedals):
                ClearCombinedPedalsOverride();
                return true;
            case nameof(InvertThrottle):
            case nameof(InvertGas):
                ClearInvertThrottleOverride();
                return true;
            case nameof(InvertBrake):
                ClearInvertBrakeOverride();
                return true;
            case nameof(ThrottleLinearity):
            case nameof(GasGamma):
                ClearThrottleLinearityOverride();
                return true;
            case nameof(BrakeLinearity):
            case nameof(BrakeGamma):
                ClearBrakeLinearityOverride();
                return true;
            case nameof(ThrottleDeadzone):
            case nameof(GasThreshold):
                ClearThrottleDeadzoneOverride();
                return true;
            case nameof(BrakeDeadzone):
            case nameof(BrakeThreshold):
                ClearBrakeDeadzoneOverride();
                return true;
            case nameof(ThrottleTurbo):
            case nameof(GasAttackTurboSeconds):
            case nameof(GasAttackTurbo):
                ClearThrottleTurboOverride();
                return true;
            case nameof(BrakeTurbo):
                ClearBrakeTurboOverride();
                return true;
            case nameof(GasAttackFastSeconds):
                ClearGasAttackFastOverride();
                return true;
            case nameof(GasAttackSlowSeconds):
                ClearGasAttackSlowOverride();
                return true;
            case nameof(GasDecaySeconds):
                ClearGasDecayOverride();
                return true;
            case nameof(GasInstantCutSeconds):
                ClearGasInstantCutOverride();
                return true;
            case nameof(BrakeAttackFastSeconds):
                ClearBrakeAttackFastOverride();
                return true;
            case nameof(BrakeAttackSlowSeconds):
                ClearBrakeAttackSlowOverride();
                return true;
            case nameof(BrakeDecaySeconds):
                ClearBrakeDecayOverride();
                return true;
            default:
                return false;
        }
    }


    [RelayCommand]
    public void ClearAllPedalOverrides()
    {
        using var edit = _profileSession.BeginEditGroup(nameof(ClearAllPedalOverrides));
        ClearCombinedPedalsOverride();
        ClearInvertThrottleOverride();
        ClearInvertBrakeOverride();
        ClearThrottleLinearityOverride();
        ClearBrakeLinearityOverride();
        ClearThrottleDeadzoneOverride();
        ClearBrakeDeadzoneOverride();
        ClearThrottleTurboOverride();
        ClearBrakeTurboOverride();
        ClearGasAttackFastOverride();
        ClearGasAttackSlowOverride();
        ClearGasDecayOverride();
        ClearGasInstantCutOverride();
        ClearBrakeAttackFastOverride();
        ClearBrakeAttackSlowOverride();
        ClearBrakeDecayOverride();
        ;
        ;
        ;
        ;
        ;
        ;
        ;
        ;
        ;
        ;
        ;
        ;
    }


    [RelayCommand]
    public void ResetPedals()
    {
        ClearAllPedalOverrides();
        NotifyAllPedalProperties();
    }

    public void NotifyAllPedalProperties()
    {
        Throttle.NotifyAllPedalProperties();
        Brake.NotifyAllPedalProperties();
        Output.NotifyAllPedalProperties();
    }
    private void SectionChanged(object? sender, PropertyChangedEventArgs e) => OnPropertyChanged(e);
    private void SectionChanging(object? sender, PropertyChangingEventArgs e) => OnPropertyChanging(e);
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var section in _sections)
        {
            section.PropertyChanged -= SectionChanged;
            section.PropertyChanging -= SectionChanging;
            section.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}
