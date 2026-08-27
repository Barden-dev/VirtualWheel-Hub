using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SimRacingHub.Models
{
    public partial class Profile : ObservableObject
    {
        public const int CurrentSchemaVersion = 2;

        [ObservableProperty] private string? _name;
        
        [ObservableProperty] private double? _mouseSensitivity;
        [ObservableProperty] private int? _steeringOffset;
        [ObservableProperty] private double? _steeringGamma;
        
        // Speed-Sensitive Steering
        [ObservableProperty] private bool? _enableSpeedSensSteering;
        [ObservableProperty] private double? _speedSensMinSpeed;
        [ObservableProperty] private double? _speedSensMaxSpeed;
        [ObservableProperty] private double? _speedSensMinMultiplier;
        
        [ObservableProperty] private int? _pollingRate;

        // Version 1 stored pedal coefficients as a per-iteration step. Version 2
        // stores them in units per second. This field is read only while upgrading
        // transitional profiles and is removed on the first save.
        [ObservableProperty] private int? _schemaVersion;
        [Newtonsoft.Json.JsonProperty("PedalReferenceRate", NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public int? PedalReferenceRate { get; set; }
        
        // Trail braking
        [ObservableProperty] private bool? _enableTrailBraking;
        [ObservableProperty] private double? _tbIntensity;
        [ObservableProperty] private double? _tbMinLimit;
        [ObservableProperty] private double? _tbDecay;
        
        // Gas
        [ObservableProperty] private double? _gasThreshold;
        [ObservableProperty] private double? _gasAttackTurbo;
        [ObservableProperty] private double? _gasAttackFast;
        [ObservableProperty] private double? _gasAttackSlow;
        [ObservableProperty] private double? _gasDecay;
        [ObservableProperty] private double? _gasInstantCut;
        [ObservableProperty] private double? _gasGamma;
        
        [ObservableProperty] private bool? _useTcHelper;
        [ObservableProperty] private double? _tcSlipThreshold;
        [ObservableProperty] private double? _tcMinScale;
        [ObservableProperty] private double? _tcDropAmount;
        [ObservableProperty] private double? _tcCutRate;
        [ObservableProperty] private double? _tcRecoverRate;
        [ObservableProperty] private double? _tcMinSpeed;
        [ObservableProperty] private bool? _enableTcAudio;
        [ObservableProperty] private int? _tcAudioFreq;
        [ObservableProperty] private int? _tcAudioDur;
        
        // Brake
        [ObservableProperty] private double? _brakeThreshold;
        [ObservableProperty] private double? _brakeAttackFast;
        [ObservableProperty] private double? _brakeAttackSlow;
        [ObservableProperty] private double? _brakeDecay;
        [ObservableProperty] private double? _brakeGamma;

        // UI exposes pedal dynamics as the time (in seconds) required to
        // traverse the selected pedal segment. Runtime values remain in axis
        // units per second so the dt-based math stays explicit and stable.
        private const double PedalAxisRange = 65536.0;
        [Newtonsoft.Json.JsonIgnore]
        internal bool IsAdjustingPedalRatesForThreshold { get; private set; }

        [Newtonsoft.Json.JsonIgnore]
        public double? TbDecaySeconds
        {
            get => ToSeconds(TbDecay);
            set => TbDecay = FromSeconds(value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public double? GasAttackTurboSeconds
        {
            get => ToSeconds(GasAttackTurbo);
            set => GasAttackTurbo = FromSeconds(value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public double? GasAttackFastSeconds
        {
            get => ToSegmentSeconds(GasAttackFast, GasThreshold);
            set
            {
                double? rate = FromSegmentSeconds(value, GasThreshold);
                if (rate.HasValue) GasAttackFast = rate.Value;
            }
        }

        [Newtonsoft.Json.JsonIgnore]
        public double? GasAttackSlowSeconds
        {
            get => ToSegmentSeconds(GasAttackSlow, GasThreshold is > 0 ? 1.0 - GasThreshold.Value : null);
            set
            {
                double? rate = FromSegmentSeconds(value, GasThreshold is > 0 ? 1.0 - GasThreshold.Value : null);
                if (rate.HasValue) GasAttackSlow = rate.Value;
            }
        }

        [Newtonsoft.Json.JsonIgnore]
        public double? GasDecaySeconds
        {
            get => ToSeconds(GasDecay);
            set => GasDecay = FromSeconds(value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public double? GasInstantCutSeconds
        {
            get => ToSeconds(GasInstantCut);
            set => GasInstantCut = FromSeconds(value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public double? BrakeAttackFastSeconds
        {
            get => ToSegmentSeconds(BrakeAttackFast, BrakeThreshold);
            set
            {
                double? rate = FromSegmentSeconds(value, BrakeThreshold);
                if (rate.HasValue) BrakeAttackFast = rate.Value;
            }
        }

        [Newtonsoft.Json.JsonIgnore]
        public double? BrakeAttackSlowSeconds
        {
            get => ToSegmentSeconds(BrakeAttackSlow, BrakeThreshold is > 0 ? 1.0 - BrakeThreshold.Value : null);
            set
            {
                double? rate = FromSegmentSeconds(value, BrakeThreshold is > 0 ? 1.0 - BrakeThreshold.Value : null);
                if (rate.HasValue) BrakeAttackSlow = rate.Value;
            }
        }

        [Newtonsoft.Json.JsonIgnore]
        public double? BrakeDecaySeconds
        {
            get => ToSeconds(BrakeDecay);
            set => BrakeDecay = FromSeconds(value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public double? GasEstimatedFullPressSeconds =>
            SegmentDuration(GasAttackFast, GasThreshold) +
            SegmentDuration(GasAttackSlow, GasThreshold is > 0 ? 1.0 - GasThreshold.Value : null);

        [Newtonsoft.Json.JsonIgnore]
        public double? BrakeEstimatedFullPressSeconds =>
            SegmentDuration(BrakeAttackFast, BrakeThreshold) +
            SegmentDuration(BrakeAttackSlow, BrakeThreshold is > 0 ? 1.0 - BrakeThreshold.Value : null);

        private static double? ToSeconds(double? unitsPerSecond)
        {
            return unitsPerSecond is > 0 ? RoundUiSeconds(PedalAxisRange / unitsPerSecond.Value) : null;
        }

        private static double? ToSegmentSeconds(double? unitsPerSecond, double? segmentFraction)
        {
            return unitsPerSecond is > 0 && segmentFraction is > 0
                ? RoundUiSeconds(PedalAxisRange * segmentFraction.Value / unitsPerSecond.Value)
                : null;
        }

        private static double RoundUiSeconds(double seconds)
        {
            // Millisecond precision is more than enough for pedal tuning and
            // keeps NumberBox values readable (0.079 instead of 0.078722).
            return Math.Round(seconds, 3, MidpointRounding.AwayFromZero);
        }

        private static double? FromSegmentSeconds(double? seconds, double? segmentFraction)
        {
            return seconds is > 0 && segmentFraction is > 0
                ? PedalAxisRange * segmentFraction.Value / seconds.Value
                : null;
        }

        private static double SegmentDuration(double? unitsPerSecond, double? segmentFraction)
        {
            return ToSegmentSeconds(unitsPerSecond, segmentFraction) ?? 0.0;
        }

        private static double? FromSeconds(double? seconds)
        {
            return seconds is > 0 ? PedalAxisRange / seconds.Value : null;
        }

        partial void OnTbDecayChanged(double? value) => OnPropertyChanged(nameof(TbDecaySeconds));
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
            _gasFastSecondsBeforeThresholdChange = ToSegmentSeconds(GasAttackFast, oldValue);
            _gasSlowSecondsBeforeThresholdChange = ToSegmentSeconds(GasAttackSlow, oldValue is > 0 ? 1.0 - oldValue.Value : null);
        }

        partial void OnGasThresholdChanged(double? value)
        {
            double? fastRate = FromSegmentSeconds(_gasFastSecondsBeforeThresholdChange, value);
            double? slowRate = FromSegmentSeconds(_gasSlowSecondsBeforeThresholdChange, value is > 0 ? 1.0 - value.Value : null);
            IsAdjustingPedalRatesForThreshold = true;
            try
            {
                if (fastRate.HasValue) GasAttackFast = fastRate.Value;
                if (slowRate.HasValue) GasAttackSlow = slowRate.Value;
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
        partial void OnBrakeDecayChanged(double? value) => OnPropertyChanged(nameof(BrakeDecaySeconds));
        private double? _brakeFastSecondsBeforeThresholdChange;
        private double? _brakeSlowSecondsBeforeThresholdChange;

        partial void OnBrakeThresholdChanging(double? oldValue, double? newValue)
        {
            _brakeFastSecondsBeforeThresholdChange = ToSegmentSeconds(BrakeAttackFast, oldValue);
            _brakeSlowSecondsBeforeThresholdChange = ToSegmentSeconds(BrakeAttackSlow, oldValue is > 0 ? 1.0 - oldValue.Value : null);
        }

        partial void OnBrakeThresholdChanged(double? value)
        {
            double? fastRate = FromSegmentSeconds(_brakeFastSecondsBeforeThresholdChange, value);
            double? slowRate = FromSegmentSeconds(_brakeSlowSecondsBeforeThresholdChange, value is > 0 ? 1.0 - value.Value : null);
            IsAdjustingPedalRatesForThreshold = true;
            try
            {
                if (fastRate.HasValue) BrakeAttackFast = fastRate.Value;
                if (slowRate.HasValue) BrakeAttackSlow = slowRate.Value;
            }
            finally
            {
                IsAdjustingPedalRatesForThreshold = false;
            }

            OnPropertyChanged(nameof(BrakeAttackFastSeconds));
            OnPropertyChanged(nameof(BrakeAttackSlowSeconds));
            OnPropertyChanged(nameof(BrakeEstimatedFullPressSeconds));
        }
        
        [ObservableProperty] private bool? _useAbsHelper;
        [ObservableProperty] private double? _absSlipThreshold;
        [ObservableProperty] private double? _absMinScale;
        [ObservableProperty] private double? _absDropAmount;
        [ObservableProperty] private double? _absCutRate;
        [ObservableProperty] private double? _absRecoverRate;
        [ObservableProperty] private double? _absMinSpeed;
        [ObservableProperty] private bool? _enableSlipAudio;
        [ObservableProperty] private double? _slipAudioUnderThreshold;
        [ObservableProperty] private double? _slipAudioOverThreshold;
        [ObservableProperty] private double? _slipAudioMinSpeed;

        partial void OnAbsDropAmountChanged(double? value)
        {
            if (value.HasValue)
            {
                _absMinScale = 1.0 - value.Value;
            }
        }

        partial void OnTcDropAmountChanged(double? value)
        {
            if (value.HasValue)
            {
                _tcMinScale = 1.0 - value.Value;
            }
        }

        partial void OnUseTcHelperChanged(bool? value)
        {
            if (value != true && EnableTcAudio == true)
            {
                EnableTcAudio = false;
            }
        }

        partial void OnEnableTcAudioChanged(bool? value)
        {
            if (value == true && UseTcHelper != true)
            {
                EnableTcAudio = false;
            }
        }

        partial void OnUseAbsHelperChanged(bool? value)
        {
            if (value == true && EnableTrailBraking == true)
            {
                EnableTrailBraking = false;
            }
        }

        partial void OnEnableTrailBrakingChanged(bool? value)
        {
            if (value == true && UseAbsHelper == true)
            {
                UseAbsHelper = false;
            }
        }
        
        [ObservableProperty] private int? _slipAudioUnderFreq;
        [ObservableProperty] private int? _slipAudioUnderDur;
        [ObservableProperty] private int? _slipAudioOverFreq;
        [ObservableProperty] private int? _slipAudioOverDur;
        
        // Steering Lock Clamp
        // Dynamic steering & lock options
        [ObservableProperty] private bool? _pauseControlsInMenu;
        [ObservableProperty] private bool? _autoUnlockMouseInMenu;
        [ObservableProperty] private bool? _useSteeringLockClamp;
        [ObservableProperty] private int? _carSteeringLock;
        [ObservableProperty] private int? _globalSteeringRange;
        
        // VJoy Output Limits
        [ObservableProperty] private int? _outSteerMin;
        [ObservableProperty] private int? _outSteerMax;
        [ObservableProperty] private int? _outGasMin;
        [ObservableProperty] private int? _outGasMax;
        [ObservableProperty] private int? _outBrakeMin;
        [ObservableProperty] private int? _outBrakeMax;
        
        [ObservableProperty] private bool? _invertSteering;
        [ObservableProperty] private bool? _invertGas;
        [ObservableProperty] private bool? _invertBrake;
        
        // Keyboard Steering Mode
        [ObservableProperty] private bool? _useKeyboardOnlyMode;
        [ObservableProperty] private double? _keySteerRampUpMs;
        [ObservableProperty] private double? _keySteerRampDownMs;
        [ObservableProperty] private bool? _keySteerSnapToFullLock;
        
        // Key Bindings (Virtual Key Codes)
        [ObservableProperty] private int? _keySteerLeft1;
        [ObservableProperty] private int? _keySteerLeft2;
        
        [ObservableProperty] private int? _keySteerRight1;
        [ObservableProperty] private int? _keySteerRight2;
        
        [ObservableProperty] private int? _keyGas1;
        [ObservableProperty] private int? _keyGas2;
        
        [ObservableProperty] private int? _keyBrake1;
        [ObservableProperty] private int? _keyBrake2;
        
        [ObservableProperty] private int? _keyShiftUp1;
        [ObservableProperty] private int? _keyShiftUp2;
        
        [ObservableProperty] private int? _keyShiftDown1;
        [ObservableProperty] private int? _keyShiftDown2;
        
        [ObservableProperty] private int? _keyCenterSteering1;
        [ObservableProperty] private int? _keyCenterSteering2;
        
        [ObservableProperty] private int? _keyToggleHub1;
        [ObservableProperty] private int? _keyToggleHub2;
        
        [ObservableProperty] private int? _keyToggleLock1;
        [ObservableProperty] private int? _keyToggleLock2;
        
        [ObservableProperty] private int? _keyUnlockCursor1;
        [ObservableProperty] private int? _keyUnlockCursor2;

        public Profile()
        {
        }
        
        public const double DefaultTbDecay = 2960.0;

        // For default profile initialization
        public static Profile CreateDefault(string name = "Universal")
        {
            if (string.Equals(name, "Le Mans Ultimate", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "LMU", StringComparison.OrdinalIgnoreCase))
            {
                return CreateLmuDefault(name);
            }
            if (string.Equals(name, "Assetto Corsa Competizione", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "ACC", StringComparison.OrdinalIgnoreCase))
            {
                return CreateAccDefault(name);
            }
            if (string.Equals(name, "Assetto Corsa EVO", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "Assetto Corsa Evo", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "AC EVO", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "ACE", StringComparison.OrdinalIgnoreCase))
            {
                return CreateAceDefault(name);
            }
            if (string.Equals(name, "BeamNG.drive", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "BeamNG", StringComparison.OrdinalIgnoreCase))
            {
                return CreateBeamNgDefault(name);
            }

            return CreateBaseDefault(name);
        }

        public static Profile CreateBaseDefault(string name = "Universal")
        {
            var profile = new Profile
            {
                Name = name,
                SchemaVersion = CurrentSchemaVersion,
                MouseSensitivity = 3.7,
                SteeringOffset = 0,
                SteeringGamma = 1.0,
                EnableSpeedSensSteering = false,
                SpeedSensMinSpeed = 30.0,
                SpeedSensMaxSpeed = 200.0,
                SpeedSensMinMultiplier = 0.5,
                PollingRate = 1000,
                EnableTrailBraking = true,
                TbIntensity = 1.0,
                TbMinLimit = 0.20,
                TbDecay = DefaultTbDecay,
                GasThreshold = 0.80,
                GasAttackTurbo = 1850.0,
                GasAttackFast = 666.0,
                GasAttackSlow = 222.0,
                GasDecay = 1110.0,
                GasInstantCut = 7400.0,
                GasGamma = 1.0,
                UseTcHelper = false,
                TcSlipThreshold = 0.05,
                TcMinScale = 0.85,
                TcDropAmount = 0.15,
                TcCutRate = 20.0,
                TcRecoverRate = 3.0,
                TcMinSpeed = 5.0,
                EnableTcAudio = false,
                TcAudioFreq = 500,
                TcAudioDur = 60,
                BrakeThreshold = 0.90,
                BrakeAttackFast = 1480.0,
                BrakeAttackSlow = 148.0,
                BrakeDecay = 1295.0,
                BrakeGamma = 1.0,
                UseAbsHelper = false,
                AbsSlipThreshold = 0.10,
                AbsMinScale = 0.90,
                AbsDropAmount = 0.10,
                AbsCutRate = 25.0,
                AbsRecoverRate = 2.0,
                AbsMinSpeed = 10.0,
                EnableSlipAudio = false,
                SlipAudioUnderThreshold = 2.0,
                SlipAudioOverThreshold = 1.8,
                SlipAudioMinSpeed = 50.0,
                SlipAudioUnderFreq = 650,
                SlipAudioUnderDur = 80,
                SlipAudioOverFreq = 280,
                SlipAudioOverDur = 100,
                PauseControlsInMenu = true,
                AutoUnlockMouseInMenu = true,
                UseSteeringLockClamp = false,
                CarSteeringLock = 540,
                GlobalSteeringRange = 900,
                OutSteerMin = 0,
                OutSteerMax = 32768,
                OutGasMin = 0,
                OutGasMax = 32768,
                OutBrakeMin = 0,
                OutBrakeMax = 32768,
                InvertSteering = false,
                InvertGas = false,
                InvertBrake = false,
                UseKeyboardOnlyMode = false,
                KeySteerRampUpMs = 200.0,
                KeySteerRampDownMs = 150.0,
                KeySteerSnapToFullLock = false,
                KeySteerLeft1 = 0x41, // 'A'
                KeySteerLeft2 = 0x25, // Left Arrow
                KeySteerRight1 = 0x44, // 'D'
                KeySteerRight2 = 0x27, // Right Arrow
                KeyGas1 = 0x57,
                KeyGas2 = 0x01,
                KeyBrake1 = 0x53,
                KeyBrake2 = 0x02,
                KeyShiftUp1 = 0x45,
                KeyShiftUp2 = 0x06,
                KeyShiftDown1 = 0x51,
                KeyShiftDown2 = 0x05,
                KeyCenterSteering1 = 0x20,
                KeyCenterSteering2 = 0,
                KeyToggleHub1 = 0,
                KeyToggleHub2 = 0,
                KeyToggleLock1 = 0x08, // Backspace
                KeyToggleLock2 = 0,
                KeyUnlockCursor1 = 0,
                KeyUnlockCursor2 = 0
            };
            profile.ConvertLegacyPedalRates(1000);
            return profile;
        }

        public static Profile CreateLmuDefault(string name = "Le Mans Ultimate")
        {
            var profile = new Profile
            {
                Name = name,
                SchemaVersion = CurrentSchemaVersion,
                MouseSensitivity = 4.0,
                SteeringOffset = 0,
                SteeringGamma = 1.0,
                EnableSpeedSensSteering = false,
                SpeedSensMinSpeed = 30.0,
                SpeedSensMaxSpeed = 200.0,
                SpeedSensMinMultiplier = 0.5,
                PollingRate = 200,
                EnableTrailBraking = true,
                TbIntensity = 1.2,
                TbMinLimit = 0.20,
                TbDecay = DefaultTbDecay,
                GasThreshold = 0.80,
                GasAttackTurbo = 1850.0,
                GasAttackFast = 660.0,
                GasAttackSlow = 220.0,
                GasDecay = 1110.0,
                GasInstantCut = 7400.0,
                GasGamma = 1.0,
                UseTcHelper = false,
                TcSlipThreshold = 0.05,
                TcMinScale = 0.85,
                TcDropAmount = 0.15,
                TcCutRate = 20.0,
                TcRecoverRate = 3.0,
                TcMinSpeed = 5.0,
                EnableTcAudio = false,
                TcAudioFreq = 500,
                TcAudioDur = 60,
                BrakeThreshold = 0.90,
                BrakeAttackFast = 1480.0,
                BrakeAttackSlow = 148.0,
                BrakeDecay = 1295.0,
                BrakeGamma = 1.0,
                UseAbsHelper = false,
                AbsSlipThreshold = 0.05,
                AbsMinScale = 0.90,
                AbsDropAmount = 0.10,
                AbsCutRate = 25.0,
                AbsRecoverRate = 2.0,
                AbsMinSpeed = 10.0,
                EnableSlipAudio = false,
                SlipAudioUnderThreshold = 2.0,
                SlipAudioOverThreshold = 1.8,
                SlipAudioMinSpeed = 54.0,
                SlipAudioUnderFreq = 650,
                SlipAudioUnderDur = 80,
                SlipAudioOverFreq = 280,
                SlipAudioOverDur = 100,
                PauseControlsInMenu = true,
                AutoUnlockMouseInMenu = true,
                UseSteeringLockClamp = true,
                CarSteeringLock = 580,
                GlobalSteeringRange = 900,
                OutSteerMin = 0,
                OutSteerMax = 32768,
                OutGasMin = 16384,
                OutGasMax = 32768,
                OutBrakeMin = 0,
                OutBrakeMax = 16384,
                InvertSteering = false,
                InvertGas = false,
                InvertBrake = true,
                UseKeyboardOnlyMode = false,
                KeySteerRampUpMs = 200.0,
                KeySteerRampDownMs = 150.0,
                KeySteerSnapToFullLock = false,
                KeySteerLeft1 = 0x41, // 'A'
                KeySteerLeft2 = 0x25, // Left Arrow
                KeySteerRight1 = 0x44, // 'D'
                KeySteerRight2 = 0x27, // Right Arrow
                KeyGas1 = 0x57, // 'W'
                KeyGas2 = 0x01, // LButton
                KeyBrake1 = 0x53, // 'S'
                KeyBrake2 = 0x02, // RButton
                KeyShiftUp1 = 0x44, // 'D'
                KeyShiftUp2 = 0x06, // XButton2
                KeyShiftDown1 = 0x41, // 'A'
                KeyShiftDown2 = 0x05, // XButton1
                KeyCenterSteering1 = 0x2E, // Delete
                KeyCenterSteering2 = 0,
                KeyToggleHub1 = 0,
                KeyToggleHub2 = 0,
                KeyToggleLock1 = 0x23, // End
                KeyToggleLock2 = 0,
                KeyUnlockCursor1 = 0,
                KeyUnlockCursor2 = 0
            };
            profile.ConvertLegacyPedalRates(200);
            return profile;
        }

        public static Profile CreateAccDefault(string name = "Assetto Corsa Competizione")
        {
            var p = CreateBaseDefault(name);
            p.PollingRate = 400;
            p.ScalePedalRates(0.4);
            p.AbsSlipThreshold = 0.14;
            p.AbsMinScale = 0.90;
            p.AbsDropAmount = 0.10;
            p.AbsCutRate = 25.0;
            p.AbsRecoverRate = 3.0;
            p.AbsMinSpeed = 10.0;
            p.TcSlipThreshold = 0.05;
            p.TcMinScale = 0.85;
            p.TcDropAmount = 0.15;
            p.TcCutRate = 15.0;
            p.TcRecoverRate = 5.0;
            p.TcMinSpeed = 5.0;
            p.SlipAudioUnderThreshold = 2.0;
            p.SlipAudioOverThreshold = 1.8;
            p.SlipAudioMinSpeed = 50.0;
            return p;
        }

        public static Profile CreateAceDefault(string name = "Assetto Corsa EVO")
        {
            var p = CreateBaseDefault(name);
            p.AbsSlipThreshold = 0.14;
            p.AbsMinScale = 0.90;
            p.AbsDropAmount = 0.10;
            p.AbsCutRate = 25.0;
            p.AbsRecoverRate = 3.0;
            p.AbsMinSpeed = 10.0;
            p.TcSlipThreshold = 0.05;
            p.TcMinScale = 0.85;
            p.TcDropAmount = 0.15;
            p.TcCutRate = 15.0;
            p.TcRecoverRate = 5.0;
            p.TcMinSpeed = 5.0;
            p.SlipAudioUnderThreshold = 2.0;
            p.SlipAudioOverThreshold = 1.8;
            p.SlipAudioMinSpeed = 50.0;
            return p;
        }

        public static Profile CreateBeamNgDefault(string name = "BeamNG.drive")
        {
            var p = CreateBaseDefault(name);
            // Preserve the supplied BeamNG preset's 200 Hz control cadence.
            // Pedal coefficients below are converted to per-second units, so
            // changing the hub rate later does not change their physical speed.
            p.PollingRate = 200;
            p.ScalePedalRates(0.2);
            p.SteeringOffset = 1000;
            p.UseSteeringLockClamp = false;
            p.OutSteerMin = 300;
            p.OutSteerMax = 32768;
            p.OutGasMin = 0;
            p.OutGasMax = 16384;
            p.OutBrakeMin = 0;
            p.OutBrakeMax = 16384;
            p.InvertBrake = false;
            p.KeyCenterSteering1 = 0x2E;
            p.KeyToggleLock1 = 0x23;
            return p;
        }

        public void CopyFrom(Profile source)
        {
            if (source == null) return;
            var props = this.GetType().GetProperties();
            foreach (var prop in props)
            {
                if (prop.CanWrite && prop.Name != "Name")
                {
                    prop.SetValue(this, prop.GetValue(source));
                }
            }
        }

        private static double? ScaleRate(double? value, double factor)
        {
            return value.HasValue ? value.Value * factor : null;
        }

        /// <summary>
        /// Upgrades legacy per-iteration pedal coefficients to units per second.
        /// The transitional dt build stored its tuning rate in PedalReferenceRate;
        /// older builds used the profile PollingRate.
        /// </summary>
        public bool MigrateToCurrentSchema(int fallbackRate = 1000)
        {
            if (SchemaVersion > CurrentSchemaVersion)
            {
                // A newer application may have introduced a different unit
                // system. Never reinterpret an unknown future profile here.
                return false;
            }

            if (SchemaVersion == CurrentSchemaVersion)
            {
                bool removedLegacyField = PedalReferenceRate.HasValue;
                PedalReferenceRate = null;
                return removedLegacyField;
            }

            int referenceRate = Math.Clamp(PedalReferenceRate ?? PollingRate ?? fallbackRate, 50, 1000);
            ConvertLegacyPedalRates(referenceRate);
            SchemaVersion = CurrentSchemaVersion;
            PedalReferenceRate = null;
            return true;
        }

        private void ConvertLegacyPedalRates(int referenceRate)
        {
            ScalePedalRates(referenceRate);
        }

        private void ScalePedalRates(double factor)
        {
            TbDecay = ScaleRate(TbDecay, factor);
            GasAttackTurbo = ScaleRate(GasAttackTurbo, factor);
            GasAttackFast = ScaleRate(GasAttackFast, factor);
            GasAttackSlow = ScaleRate(GasAttackSlow, factor);
            GasDecay = ScaleRate(GasDecay, factor);
            GasInstantCut = ScaleRate(GasInstantCut, factor);
            BrakeAttackFast = ScaleRate(BrakeAttackFast, factor);
            BrakeAttackSlow = ScaleRate(BrakeAttackSlow, factor);
            BrakeDecay = ScaleRate(BrakeDecay, factor);
        }
    }
}
