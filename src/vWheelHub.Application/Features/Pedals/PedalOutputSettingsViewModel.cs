using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimRacingHub.Application.Common;
using SimRacingHub.Application.Contracts;
using SimRacingHub.Application.Localization;
using SimRacingHub.Models;
using SimRacingHub.Services;

namespace SimRacingHub.Application.Features.Pedals;

public partial class PedalOutputSettingsViewModel : ProfileSettingsViewModel
{
    private bool? _combinedPedals;
    public PedalOutputSettingsViewModel(ProfileSession profileSession) : base(profileSession) => StartObserving();

    [ObservableProperty]
    private double _throttleValue;

    [ObservableProperty]
    private double _throttleInput;

    [ObservableProperty]
    private int _throttleRaw;

    [ObservableProperty]
    private double _brakeValue;

    [ObservableProperty]
    private double _brakeInput;

    [ObservableProperty]
    private int _brakeRaw;

    public int EffectiveOutGasMin => CurrentProfile?.OutGasMin ?? ResolvedProfile?.OutGasMin ?? 0;

    public int EffectiveOutGasMax => CurrentProfile?.OutGasMax ?? ResolvedProfile?.OutGasMax ?? 32768;

    public int EffectiveOutBrakeMin => CurrentProfile?.OutBrakeMin ?? ResolvedProfile?.OutBrakeMin ?? 0;

    public int EffectiveOutBrakeMax => CurrentProfile?.OutBrakeMax ?? ResolvedProfile?.OutBrakeMax ?? 32768;

    private void NotifyOutputAxisProperties()
    {
        OnPropertyChanged(nameof(EffectiveOutGasMin));
        OnPropertyChanged(nameof(EffectiveOutGasMax));
        OnPropertyChanged(nameof(EffectiveOutBrakeMin));
        OnPropertyChanged(nameof(EffectiveOutBrakeMax));
    }

    /// <summary>
    /// Gets or sets whether combined pedals mode (single-axis throttle and brake) is active.
    /// </summary>
    public bool? CombinedPedals
    {
        get => _combinedPedals;
        set
        {
            if (_combinedPedals != value)
            {
                _combinedPedals = value;
                NotifyCombinedPedalsProperties();
            }
        }
    }

    public bool EffectiveCombinedPedals { get => CombinedPedals ?? false; set => CombinedPedals = value; }

    public bool IsCombinedPedalsOverridden => CombinedPedals.HasValue;

    [RelayCommand]
    public void ClearCombinedPedalsOverride() => CombinedPedals = null;

    private void NotifyCombinedPedalsProperties()
    {
        OnPropertyChanged(nameof(CombinedPedals));
        OnPropertyChanged(nameof(EffectiveCombinedPedals));
        OnPropertyChanged(nameof(IsCombinedPedalsOverridden));
    }

    protected override void OnCurrentProfilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName)) { NotifyAllPedalProperties(); return; }
        switch (e.PropertyName)
        {
            case nameof(Profile.OutGasMin):
            case nameof(Profile.OutGasMax):
            case nameof(Profile.OutBrakeMin):
            case nameof(Profile.OutBrakeMax):
                NotifyOutputAxisProperties();
                break;
        }
    }

    protected override void OnResolvedProfilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName)) { NotifyAllPedalProperties(); return; }
        switch (e.PropertyName)
        {
            case nameof(ResolvedProfile.OutGasMin):
            case nameof(ResolvedProfile.OutGasMax):
            case nameof(ResolvedProfile.OutBrakeMin):
            case nameof(ResolvedProfile.OutBrakeMax):
                NotifyOutputAxisProperties();
                break;
        }
    }

    protected override void RefreshSettings() => NotifyAllPedalProperties();
    public void NotifyAllPedalProperties()
    {
        NotifyOutputAxisProperties();
        NotifyCombinedPedalsProperties();
    }
}
