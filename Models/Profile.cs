using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SimRacingHub.Models
{
    public partial class Profile : ObservableObject
    {
        [ObservableProperty] private string _name;
        
        [ObservableProperty] private double? _mouseSensitivity;
        [ObservableProperty] private int? _steeringOffset;
        [ObservableProperty] private double? _steeringGamma = 1.0;
        
        // Speed-Sensitive Steering
        [ObservableProperty] private bool? _enableSpeedSensSteering;
        [ObservableProperty] private double? _speedSensMinSpeed = 30.0;
        [ObservableProperty] private double? _speedSensMaxSpeed = 200.0;
        [ObservableProperty] private double? _speedSensMinMultiplier = 0.5;
        
        [ObservableProperty] private int? _pollingRate;
        
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
        [ObservableProperty] private double? _gasGamma = 1.0;
        
        [ObservableProperty] private bool? _useTcHelper;
        [ObservableProperty] private bool? _enableTcAudio;
        [ObservableProperty] private int? _tcAudioFreq = 500;
        [ObservableProperty] private int? _tcAudioDur = 60;
        
        // Brake
        [ObservableProperty] private double? _brakeThreshold;
        [ObservableProperty] private double? _brakeAttackFast;
        [ObservableProperty] private double? _brakeAttackSlow;
        [ObservableProperty] private double? _brakeDecay;
        [ObservableProperty] private double? _brakeGamma = 1.0;
        
        [ObservableProperty] private bool? _useAbsHelper;
        [ObservableProperty] private bool? _enableSlipAudio;

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
        
        [ObservableProperty] private int? _slipAudioUnderFreq = 650;
        [ObservableProperty] private int? _slipAudioUnderDur = 80;
        [ObservableProperty] private int? _slipAudioOverFreq = 280;
        [ObservableProperty] private int? _slipAudioOverDur = 100;
        
        // Steering Lock Clamp
        // Dynamic steering & lock options
        [ObservableProperty] private bool? _lockSteerWhenUnlocked;
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
        [ObservableProperty] private double? _keySteerRampUpMs = 200.0;
        [ObservableProperty] private double? _keySteerRampDownMs = 150.0;
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

            return new Profile
            {
                Name = name,
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
                EnableTcAudio = false,
                TcAudioFreq = 500,
                TcAudioDur = 60,
                BrakeThreshold = 0.90,
                BrakeAttackFast = 1480.0,
                BrakeAttackSlow = 148.0,
                BrakeDecay = 1295.0,
                BrakeGamma = 1.0,
                UseAbsHelper = false,
                EnableSlipAudio = false,
                SlipAudioUnderFreq = 650,
                SlipAudioUnderDur = 80,
                SlipAudioOverFreq = 280,
                SlipAudioOverDur = 100,
                LockSteerWhenUnlocked = false,
                UseSteeringLockClamp = false,
                CarSteeringLock = 540,
                GlobalSteeringRange = 900,
                OutSteerMin = -32768,
                OutSteerMax = 32768,
                OutGasMin = -32768,
                OutGasMax = 32768,
                OutBrakeMin = -32768,
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
                KeyToggleLock2 = 0
            };
        }

        public static Profile CreateLmuDefault(string name = "Le Mans Ultimate")
        {
            return new Profile
            {
                Name = name,
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
                EnableTcAudio = false,
                TcAudioFreq = 500,
                TcAudioDur = 60,
                BrakeThreshold = 0.90,
                BrakeAttackFast = 1480.0,
                BrakeAttackSlow = 148.0,
                BrakeDecay = 1295.0,
                BrakeGamma = 1.0,
                UseAbsHelper = false,
                EnableSlipAudio = false,
                SlipAudioUnderFreq = 650,
                SlipAudioUnderDur = 80,
                SlipAudioOverFreq = 280,
                SlipAudioOverDur = 100,
                LockSteerWhenUnlocked = false,
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
                KeyToggleLock2 = 0
            };
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
    }
}
