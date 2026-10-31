using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SimRacingHub.Models;

public partial class Profile : ObservableObject
{
    public const int CurrentSchemaVersion = 2;

    [ObservableProperty]
    private string? _name;

    [ObservableProperty]
    private double? _mouseSensitivity;

    [ObservableProperty]
    private int? _steeringOffset;

    [ObservableProperty]
    private double? _steeringGamma;

    [ObservableProperty]
    private int? _pollingRate;

    // Version 1 stored pedal coefficients as a per-iteration step. Version 2
    // stores them in units per second. This field is read only while upgrading
    // transitional profiles and is removed on the first save.
    [ObservableProperty]
    private int? _schemaVersion;

    [Newtonsoft.Json.JsonProperty("PedalReferenceRate", NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
    public int? PedalReferenceRate { get; set; }

    // Gas
    [ObservableProperty]
    private double? _gasThreshold;

    [ObservableProperty]
    private double? _gasAttackTurbo;

    [ObservableProperty]
    private double? _gasAttackFast;

    [ObservableProperty]
    private double? _gasAttackSlow;

    [ObservableProperty]
    private double? _gasDecay;

    [ObservableProperty]
    private double? _gasInstantCut;

    [ObservableProperty]
    private double? _gasGamma;

    // Brake
    [ObservableProperty]
    private double? _brakeThreshold;

    [ObservableProperty]
    private double? _brakeAttackFast;

    [ObservableProperty]
    private double? _brakeAttackSlow;

    [ObservableProperty]
    private double? _brakeDecay;

    [ObservableProperty]
    private double? _brakeGamma;

    // Trail braking
    [ObservableProperty] private bool? _enableTrailBraking;

    [ObservableProperty] private double? _tbIntensity;

    [ObservableProperty] private double? _tbMinLimit;

    [ObservableProperty] private double? _tbDecay;

    public const double DefaultTbDecay = 2960.0;

    private readonly System.Collections.Generic.Dictionary<string, (double Rate, double Fraction)> _savedPedalRates = new();

    // UI durations are rounded to milliseconds. Returning to the saved display
    // value must recover the exact rate, rather than accumulate conversion loss.
    internal void SetSavedPedalRates(ResolvedProfile saved)
    {
        _savedPedalRates.Clear();
        _savedPedalRates[nameof(TbDecay)] = (saved.TbDecay, 1);
        _savedPedalRates[nameof(GasAttackTurbo)] = (saved.GasAttackTurbo, 1);
        _savedPedalRates[nameof(GasAttackFast)] = (saved.GasAttackFast, saved.GasThreshold);
        _savedPedalRates[nameof(GasAttackSlow)] = (saved.GasAttackSlow, 1 - saved.GasThreshold);
        _savedPedalRates[nameof(GasDecay)] = (saved.GasDecay, 1);
        _savedPedalRates[nameof(GasInstantCut)] = (saved.GasInstantCut, 1);
        _savedPedalRates[nameof(BrakeAttackFast)] = (saved.BrakeAttackFast, saved.BrakeThreshold);
        _savedPedalRates[nameof(BrakeAttackSlow)] = (saved.BrakeAttackSlow, 1 - saved.BrakeThreshold);
        _savedPedalRates[nameof(BrakeDecay)] = (saved.BrakeDecay, 1);
    }

    private double? RateFromUiSeconds(string name, double? seconds, double? fraction = 1)
    {
        if (seconds is > 0 && fraction is > 0 && _savedPedalRates.TryGetValue(name, out var saved) &&
            Math.Abs(fraction.Value - saved.Fraction) < 1e-10 &&
            PedalTimingConversion.ToSegmentSeconds(saved.Rate, saved.Fraction) is double savedSeconds &&
            Math.Abs(seconds.Value - savedSeconds) < 1e-10)
            return saved.Rate;
        return PedalTimingConversion.FromSegmentSeconds(seconds, fraction);
    }

    private double? _inheritedGasThreshold;

    private double? _inheritedBrakeThreshold;

    private double? GasSegmentFraction => GasThreshold ?? _inheritedGasThreshold;

    private double? BrakeSegmentFraction => BrakeThreshold ?? _inheritedBrakeThreshold;

    internal void SetInheritedPedalThresholds(double gas, double brake)
    {
        _inheritedGasThreshold = gas;
        _inheritedBrakeThreshold = brake;
    }

    [Newtonsoft.Json.JsonIgnore]
    internal bool IsAdjustingPedalRatesForThreshold { get; private set; }

    [Newtonsoft.Json.JsonIgnore]
    public double? TbDecaySeconds
    {
        get => PedalTimingConversion.ToSeconds(TbDecay);
        set => TbDecay = RateFromUiSeconds(nameof(TbDecay), value);
    }

    [Newtonsoft.Json.JsonIgnore]
    public double? GasAttackTurboSeconds { get => PedalTimingConversion.ToSeconds(GasAttackTurbo); set => GasAttackTurbo = RateFromUiSeconds(nameof(GasAttackTurbo), value); }

    [Newtonsoft.Json.JsonIgnore]
    public double? GasAttackFastSeconds
    {
        get => PedalTimingConversion.ToSegmentSeconds(GasAttackFast, GasSegmentFraction);
        set
        {
            double? rate = RateFromUiSeconds(nameof(GasAttackFast), value, GasSegmentFraction);
            if (rate.HasValue)
                GasAttackFast = rate.Value;
        }
    }

    [Newtonsoft.Json.JsonIgnore]
    public double? GasAttackSlowSeconds
    {
        get => PedalTimingConversion.ToSegmentSeconds(GasAttackSlow, GasSegmentFraction is > 0 ? 1.0 - GasSegmentFraction.Value : null);
        set
        {
            double? rate = RateFromUiSeconds(nameof(GasAttackSlow), value, GasSegmentFraction is > 0 ? 1.0 - GasSegmentFraction.Value : null);
            if (rate.HasValue)
                GasAttackSlow = rate.Value;
        }
    }

    [Newtonsoft.Json.JsonIgnore]
    public double? GasDecaySeconds { get => PedalTimingConversion.ToSeconds(GasDecay); set => GasDecay = RateFromUiSeconds(nameof(GasDecay), value); }

    [Newtonsoft.Json.JsonIgnore]
    public double? GasInstantCutSeconds { get => PedalTimingConversion.ToSeconds(GasInstantCut); set => GasInstantCut = RateFromUiSeconds(nameof(GasInstantCut), value); }

    [Newtonsoft.Json.JsonIgnore]
    public double? BrakeAttackFastSeconds
    {
        get => PedalTimingConversion.ToSegmentSeconds(BrakeAttackFast, BrakeSegmentFraction);
        set
        {
            double? rate = RateFromUiSeconds(nameof(BrakeAttackFast), value, BrakeSegmentFraction);
            if (rate.HasValue)
                BrakeAttackFast = rate.Value;
        }
    }

    [Newtonsoft.Json.JsonIgnore]
    public double? BrakeAttackSlowSeconds
    {
        get => PedalTimingConversion.ToSegmentSeconds(BrakeAttackSlow, BrakeSegmentFraction is > 0 ? 1.0 - BrakeSegmentFraction.Value : null);
        set
        {
            double? rate = RateFromUiSeconds(nameof(BrakeAttackSlow), value, BrakeSegmentFraction is > 0 ? 1.0 - BrakeSegmentFraction.Value : null);
            if (rate.HasValue)
                BrakeAttackSlow = rate.Value;
        }
    }

    [Newtonsoft.Json.JsonIgnore]
    public double? BrakeDecaySeconds { get => PedalTimingConversion.ToSeconds(BrakeDecay); set => BrakeDecay = RateFromUiSeconds(nameof(BrakeDecay), value); }

    [Newtonsoft.Json.JsonIgnore]
    public double? GasEstimatedFullPressSeconds => PedalTimingConversion.SegmentDuration(GasAttackFast, GasSegmentFraction) + PedalTimingConversion.SegmentDuration(GasAttackSlow, GasSegmentFraction is > 0 ? 1.0 - GasSegmentFraction.Value : null);

    [Newtonsoft.Json.JsonIgnore]
    public double? BrakeEstimatedFullPressSeconds => PedalTimingConversion.SegmentDuration(BrakeAttackFast, BrakeSegmentFraction) + PedalTimingConversion.SegmentDuration(BrakeAttackSlow, BrakeSegmentFraction is > 0 ? 1.0 - BrakeSegmentFraction.Value : null);

    partial void OnGasAttackTurboChanged(double? value) => OnPropertyChanged(nameof(GasAttackTurboSeconds));

    partial void OnGasAttackFastChanged(double? value)
    {
        OnPropertyChanged(nameof(GasAttackFastSeconds));
        OnPropertyChanged(nameof(GasEstimatedFullPressSeconds));
    }

    partial void OnGasAttackSlowChanged(double? value)
    {
        OnPropertyChanged(nameof(GasAttackSlowSeconds));
        OnPropertyChanged(nameof(GasEstimatedFullPressSeconds));
    }

    partial void OnGasDecayChanged(double? value) => OnPropertyChanged(nameof(GasDecaySeconds));

    partial void OnGasInstantCutChanged(double? value) => OnPropertyChanged(nameof(GasInstantCutSeconds));

    private double? _gasFastSecondsBeforeThresholdChange;

    private double? _gasSlowSecondsBeforeThresholdChange;

    partial void OnGasThresholdChanging(double? oldValue, double? newValue)
    {
        oldValue ??= _inheritedGasThreshold;
        _gasFastSecondsBeforeThresholdChange = PedalTimingConversion.ExactSegmentSeconds(GasAttackFast, oldValue);
        _gasSlowSecondsBeforeThresholdChange = PedalTimingConversion.ExactSegmentSeconds(GasAttackSlow, oldValue is> 0 ? 1.0 - oldValue.Value : null);
    }

    partial void OnGasThresholdChanged(double? value)
    {
        value ??= _inheritedGasThreshold;
        double? fastRate = PedalTimingConversion.FromSegmentSeconds(_gasFastSecondsBeforeThresholdChange, value);
        double? slowRate = PedalTimingConversion.FromSegmentSeconds(_gasSlowSecondsBeforeThresholdChange, value is> 0 ? 1.0 - value.Value : null);
        IsAdjustingPedalRatesForThreshold = true;
        try
        {
            if (fastRate.HasValue)
                GasAttackFast = fastRate.Value;
            if (slowRate.HasValue)
                GasAttackSlow = slowRate.Value;
        }
        finally
        {
            IsAdjustingPedalRatesForThreshold = false;
        }

        OnPropertyChanged(nameof(GasAttackFastSeconds));
        OnPropertyChanged(nameof(GasAttackSlowSeconds));
        OnPropertyChanged(nameof(GasEstimatedFullPressSeconds));
    }

    partial void OnBrakeAttackFastChanged(double? value)
    {
        OnPropertyChanged(nameof(BrakeAttackFastSeconds));
        OnPropertyChanged(nameof(BrakeEstimatedFullPressSeconds));
    }

    partial void OnBrakeAttackSlowChanged(double? value)
    {
        OnPropertyChanged(nameof(BrakeAttackSlowSeconds));
        OnPropertyChanged(nameof(BrakeEstimatedFullPressSeconds));
    }

    partial void OnTbDecayChanged(double? value) => OnPropertyChanged(nameof(TbDecaySeconds));

    partial void OnBrakeDecayChanged(double? value) => OnPropertyChanged(nameof(BrakeDecaySeconds));

    private double? _brakeFastSecondsBeforeThresholdChange;

    private double? _brakeSlowSecondsBeforeThresholdChange;

    partial void OnBrakeThresholdChanging(double? oldValue, double? newValue)
    {
        oldValue ??= _inheritedBrakeThreshold;
        _brakeFastSecondsBeforeThresholdChange = PedalTimingConversion.ExactSegmentSeconds(BrakeAttackFast, oldValue);
        _brakeSlowSecondsBeforeThresholdChange = PedalTimingConversion.ExactSegmentSeconds(BrakeAttackSlow, oldValue is> 0 ? 1.0 - oldValue.Value : null);
    }

    partial void OnBrakeThresholdChanged(double? value)
    {
        value ??= _inheritedBrakeThreshold;
        double? fastRate = PedalTimingConversion.FromSegmentSeconds(_brakeFastSecondsBeforeThresholdChange, value);
        double? slowRate = PedalTimingConversion.FromSegmentSeconds(_brakeSlowSecondsBeforeThresholdChange, value is> 0 ? 1.0 - value.Value : null);
        IsAdjustingPedalRatesForThreshold = true;
        try
        {
            if (fastRate.HasValue)
                BrakeAttackFast = fastRate.Value;
            if (slowRate.HasValue)
                BrakeAttackSlow = slowRate.Value;
        }
        finally
        {
            IsAdjustingPedalRatesForThreshold = false;
        }

        OnPropertyChanged(nameof(BrakeAttackFastSeconds));
        OnPropertyChanged(nameof(BrakeAttackSlowSeconds));
        OnPropertyChanged(nameof(BrakeEstimatedFullPressSeconds));
    }

    [ObservableProperty]
    private bool? _useSteeringLockClamp;

    [ObservableProperty]
    private int? _carSteeringLock;

    [ObservableProperty]
    private int? _globalSteeringRange;

    // Virtual Output Device Mode (VJoy DirectInput vs Xbox360 ViGEm)
    // 0: continue controls, 1: hold mouse steering, 2: pause all controls. Null inherits.
    [ObservableProperty] private int? _cursorUnlockedMode;
    [ObservableProperty] private bool? _pauseControlsOnFocusLoss;
    [ObservableProperty] private bool? _releaseCursorOnFocusLoss;
    [ObservableProperty]
    private OutputDeviceMode? _outputMode;

    [ObservableProperty] private int? _vJoyDeviceId;
    [ObservableProperty] private bool? _vJoyWheelCompatibility;
    [ObservableProperty] private bool? _useHubVJoyDevice;

    // VJoy Output Limits
    [ObservableProperty]
    private int? _outSteerMin;

    [ObservableProperty]
    private int? _outSteerMax;

    [ObservableProperty]
    private int? _outGasMin;

    [ObservableProperty]
    private int? _outGasMax;

    [ObservableProperty]
    private int? _outBrakeMin;

    [ObservableProperty]
    private int? _outBrakeMax;

    [ObservableProperty]
    private bool? _invertSteering;

    [ObservableProperty]
    private bool? _invertGas;

    [ObservableProperty]
    private bool? _invertBrake;

    // Keyboard Steering Mode
    [ObservableProperty]
    private bool? _useKeyboardOnlyMode;

    [ObservableProperty]
    private double? _keySteerRampUpMs;

    [ObservableProperty]
    private double? _keySteerRampDownMs;

    [ObservableProperty]
    private bool? _keySteerSnapToFullLock;

    // Key Bindings (Virtual Key Codes)
    [ObservableProperty]
    private int? _keySteerLeft1;

    [ObservableProperty]
    private int? _keySteerLeft2;

    [ObservableProperty]
    private int? _keySteerRight1;

    [ObservableProperty]
    private int? _keySteerRight2;

    [ObservableProperty]
    private int? _keyGas1;

    [ObservableProperty]
    private int? _keyGas2;

    [ObservableProperty]
    private int? _keyBrake1;

    [ObservableProperty]
    private int? _keyBrake2;

    [ObservableProperty]
    private int? _keyShiftUp1;

    [ObservableProperty]
    private int? _keyShiftUp2;

    [ObservableProperty]
    private int? _keyShiftDown1;

    [ObservableProperty]
    private int? _keyShiftDown2;

    [ObservableProperty]
    private int? _keyCenterSteering1;

    [ObservableProperty]
    private int? _keyCenterSteering2;

    [ObservableProperty]
    private int? _keyToggleHub1;

    [ObservableProperty]
    private int? _keyToggleHub2;

    [ObservableProperty]
    private int? _keyToggleLock1;

    [ObservableProperty]
    private int? _keyToggleLock2;

    [ObservableProperty]
    private int? _keyUnlockCursor1;

    [ObservableProperty]
    private int? _keyUnlockCursor2;

    public Profile()
    {
    }

    public static Profile CreateDefault(string name = "Universal") => ProfileDefaults.CreateDefault(name);

    public static Profile CreateBaseDefault(string name = "Universal") => ProfileDefaults.CreateBaseDefault(name);

    public static Profile CreateLmuDefault(string name = "Le Mans Ultimate") => ProfileDefaults.CreateLmuDefault(name);

    public static Profile CreateAcDefault(string name = "Assetto Corsa") => ProfileDefaults.CreateAcDefault(name);

    public static Profile CreateAccDefault(string name = "Assetto Corsa Competizione") => ProfileDefaults.CreateAccDefault(name);

    public static Profile CreateAceDefault(string name = "Assetto Corsa EVO") => ProfileDefaults.CreateAceDefault(name);

    public static Profile CreateBeamNgDefault(string name = "BeamNG.drive") => ProfileDefaults.CreateBeamNgDefault(name);

    public static Profile CreateWrcGenerationsDefault(string name = "WRC Generations") => ProfileDefaults.CreateWrcGenerationsDefault(name);

    public static Profile CreateForzaMotorsportDefault(string name = "Forza Motorsport") => ProfileDefaults.CreateForzaMotorsportDefault(name);

    public static Profile CreateForzaHorizonDefault(string name = "Forza Horizon 5") => ProfileDefaults.CreateForzaHorizonDefault(name);

    public void CopyFrom(Profile source)
    {
        if (source == null)
            return;
        var props = this.GetType().GetProperties();
        foreach (var prop in props)
        {
            if (prop.CanWrite && prop.Name != "Name")
            {
                prop.SetValue(this, prop.GetValue(source));
            }
        }
    }

    public bool MigrateToCurrentSchema(int fallbackRate = 1000) => ProfileSchemaMigrator.Migrate(this, fallbackRate);

}
