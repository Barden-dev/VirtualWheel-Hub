using System;
using CommunityToolkit.Mvvm.ComponentModel;
namespace SimRacingHub.Models
{
    public partial class ResolvedProfile : ObservableObject
    {
        [ObservableProperty] private string _name;
        [ObservableProperty] private double _mouseSensitivity;
        [ObservableProperty] private int _steeringOffset;
        [ObservableProperty] private double _steeringGamma = 1.0;
        [ObservableProperty] private bool _enableSpeedSensSteering;
        [ObservableProperty] private double _speedSensMinSpeed = 30.0;
        [ObservableProperty] private double _speedSensMaxSpeed = 200.0;
        [ObservableProperty] private double _speedSensMinMultiplier = 0.5;
        [ObservableProperty] private int _pollingRate;
        [ObservableProperty] private bool _enableTrailBraking = true;
        [ObservableProperty] private double _tbIntensity;
        [ObservableProperty] private double _tbMinLimit;
        [ObservableProperty] private double _tbDecay;
        [ObservableProperty] private double _gasThreshold;
        [ObservableProperty] private double _gasAttackTurbo;
        [ObservableProperty] private double _gasAttackFast;
        [ObservableProperty] private double _gasAttackSlow;
        [ObservableProperty] private double _gasDecay;
        [ObservableProperty] private double _gasInstantCut;
        [ObservableProperty] private double _gasGamma = 1.0;
        [ObservableProperty] private bool _useTcHelper;
        [ObservableProperty] private bool _enableTcAudio;
        [ObservableProperty] private int _tcAudioFreq;
        [ObservableProperty] private int _tcAudioDur;
        [ObservableProperty] private double _brakeThreshold;
        [ObservableProperty] private double _brakeAttackFast;
        [ObservableProperty] private double _brakeAttackSlow;
        [ObservableProperty] private double _brakeDecay;
        [ObservableProperty] private double _brakeGamma = 1.0;
        [ObservableProperty] private bool _useAbsHelper;
        [ObservableProperty] private bool _enableSlipAudio;

        partial void OnUseTcHelperChanged(bool value)
        {
            if (!value && EnableTcAudio)
            {
                EnableTcAudio = false;
            }
        }

        partial void OnEnableTcAudioChanged(bool value)
        {
            if (value && !UseTcHelper)
            {
                EnableTcAudio = false;
            }
        }

        partial void OnUseAbsHelperChanged(bool value)
        {
            if (value && EnableTrailBraking)
            {
                EnableTrailBraking = false;
            }
        }

        partial void OnEnableTrailBrakingChanged(bool value)
        {
            if (value && UseAbsHelper)
            {
                UseAbsHelper = false;
            }
        }
        [ObservableProperty] private int _slipAudioUnderFreq = 650;
        [ObservableProperty] private int _slipAudioUnderDur = 80;
        [ObservableProperty] private int _slipAudioOverFreq = 280;
        [ObservableProperty] private int _slipAudioOverDur = 100;
        [ObservableProperty] private bool _lockSteerWhenUnlocked;
        [ObservableProperty] private bool _useSteeringLockClamp;
        [ObservableProperty] private int _carSteeringLock = 540;
        [ObservableProperty] private int _globalSteeringRange = 900;
        [ObservableProperty] private int _outSteerMin;
        [ObservableProperty] private int _outSteerMax;
        [ObservableProperty] private int _outGasMin;
        [ObservableProperty] private int _outGasMax;
        [ObservableProperty] private int _outBrakeMin;
        [ObservableProperty] private int _outBrakeMax;
        [ObservableProperty] private bool _invertSteering;
        [ObservableProperty] private bool _invertGas;
        [ObservableProperty] private bool _invertBrake;
        [ObservableProperty] private bool _useKeyboardOnlyMode;
        [ObservableProperty] private double _keySteerRampUpMs = 200.0;
        [ObservableProperty] private double _keySteerRampDownMs = 150.0;
        [ObservableProperty] private bool _keySteerSnapToFullLock;
        [ObservableProperty] private int _keySteerLeft1;
        [ObservableProperty] private int _keySteerLeft2;
        [ObservableProperty] private int _keySteerRight1;
        [ObservableProperty] private int _keySteerRight2;
        [ObservableProperty] private int _keyGas1;
        [ObservableProperty] private int _keyGas2;
        [ObservableProperty] private int _keyBrake1;
        [ObservableProperty] private int _keyBrake2;
        [ObservableProperty] private int _keyShiftUp1;
        [ObservableProperty] private int _keyShiftUp2;
        [ObservableProperty] private int _keyShiftDown1;
        [ObservableProperty] private int _keyShiftDown2;
        [ObservableProperty] private int _keyCenterSteering1;
        [ObservableProperty] private int _keyCenterSteering2;
        [ObservableProperty] private int _keyToggleHub1;
        [ObservableProperty] private int _keyToggleHub2;
        [ObservableProperty] private int _keyToggleLock1;
        [ObservableProperty] private int _keyToggleLock2;
        public void UpdateFrom(Profile p)
        {
            if (p.MouseSensitivity.HasValue) MouseSensitivity = p.MouseSensitivity.Value;
            if (p.SteeringOffset.HasValue) SteeringOffset = p.SteeringOffset.Value;
            if (p.SteeringGamma.HasValue) SteeringGamma = p.SteeringGamma.Value;
            if (p.EnableSpeedSensSteering.HasValue) EnableSpeedSensSteering = p.EnableSpeedSensSteering.Value;
            if (p.SpeedSensMinSpeed.HasValue) SpeedSensMinSpeed = p.SpeedSensMinSpeed.Value;
            if (p.SpeedSensMaxSpeed.HasValue) SpeedSensMaxSpeed = p.SpeedSensMaxSpeed.Value;
            if (p.SpeedSensMinMultiplier.HasValue) SpeedSensMinMultiplier = p.SpeedSensMinMultiplier.Value;
            if (p.PollingRate.HasValue) PollingRate = p.PollingRate.Value;
            if (p.EnableTrailBraking.HasValue) EnableTrailBraking = p.EnableTrailBraking.Value;
            if (p.TbIntensity.HasValue) TbIntensity = p.TbIntensity.Value;
            if (p.TbMinLimit.HasValue) TbMinLimit = p.TbMinLimit.Value;
            if (p.TbDecay.HasValue) TbDecay = p.TbDecay.Value;
            if (p.GasThreshold.HasValue) GasThreshold = p.GasThreshold.Value;
            if (p.GasAttackTurbo.HasValue) GasAttackTurbo = p.GasAttackTurbo.Value;
            if (p.GasAttackFast.HasValue) GasAttackFast = p.GasAttackFast.Value;
            if (p.GasAttackSlow.HasValue) GasAttackSlow = p.GasAttackSlow.Value;
            if (p.GasDecay.HasValue) GasDecay = p.GasDecay.Value;
            if (p.GasInstantCut.HasValue) GasInstantCut = p.GasInstantCut.Value;
            if (p.GasGamma.HasValue) GasGamma = p.GasGamma.Value;
            if (p.UseTcHelper.HasValue) UseTcHelper = p.UseTcHelper.Value;
            if (p.EnableTcAudio.HasValue) EnableTcAudio = p.EnableTcAudio.Value;
            if (p.TcAudioFreq.HasValue) TcAudioFreq = p.TcAudioFreq.Value;
            if (p.TcAudioDur.HasValue) TcAudioDur = p.TcAudioDur.Value;
            if (p.BrakeThreshold.HasValue) BrakeThreshold = p.BrakeThreshold.Value;
            if (p.BrakeAttackFast.HasValue) BrakeAttackFast = p.BrakeAttackFast.Value;
            if (p.BrakeAttackSlow.HasValue) BrakeAttackSlow = p.BrakeAttackSlow.Value;
            if (p.BrakeDecay.HasValue) BrakeDecay = p.BrakeDecay.Value;
            if (p.BrakeGamma.HasValue) BrakeGamma = p.BrakeGamma.Value;
            if (p.UseAbsHelper.HasValue) UseAbsHelper = p.UseAbsHelper.Value;
            if (p.EnableSlipAudio.HasValue) EnableSlipAudio = p.EnableSlipAudio.Value;
            if (p.SlipAudioUnderFreq.HasValue) SlipAudioUnderFreq = p.SlipAudioUnderFreq.Value;
            if (p.SlipAudioUnderDur.HasValue) SlipAudioUnderDur = p.SlipAudioUnderDur.Value;
            if (p.SlipAudioOverFreq.HasValue) SlipAudioOverFreq = p.SlipAudioOverFreq.Value;
            if (p.SlipAudioOverDur.HasValue) SlipAudioOverDur = p.SlipAudioOverDur.Value;
            if (p.LockSteerWhenUnlocked.HasValue) LockSteerWhenUnlocked = p.LockSteerWhenUnlocked.Value;
            if (p.UseSteeringLockClamp.HasValue) UseSteeringLockClamp = p.UseSteeringLockClamp.Value;
            if (p.CarSteeringLock.HasValue) CarSteeringLock = p.CarSteeringLock.Value;
            if (p.GlobalSteeringRange.HasValue) GlobalSteeringRange = p.GlobalSteeringRange.Value;
            if (p.OutSteerMin.HasValue) OutSteerMin = p.OutSteerMin.Value;
            if (p.OutSteerMax.HasValue) OutSteerMax = p.OutSteerMax.Value;
            if (p.OutGasMin.HasValue) OutGasMin = p.OutGasMin.Value;
            if (p.OutGasMax.HasValue) OutGasMax = p.OutGasMax.Value;
            if (p.OutBrakeMin.HasValue) OutBrakeMin = p.OutBrakeMin.Value;
            if (p.OutBrakeMax.HasValue) OutBrakeMax = p.OutBrakeMax.Value;
            if (p.InvertSteering.HasValue) InvertSteering = p.InvertSteering.Value;
            if (p.InvertGas.HasValue) InvertGas = p.InvertGas.Value;
            if (p.InvertBrake.HasValue) InvertBrake = p.InvertBrake.Value;
            if (p.UseKeyboardOnlyMode.HasValue) UseKeyboardOnlyMode = p.UseKeyboardOnlyMode.Value;
            if (p.KeySteerRampUpMs.HasValue) KeySteerRampUpMs = p.KeySteerRampUpMs.Value;
            if (p.KeySteerRampDownMs.HasValue) KeySteerRampDownMs = p.KeySteerRampDownMs.Value;
            if (p.KeySteerSnapToFullLock.HasValue) KeySteerSnapToFullLock = p.KeySteerSnapToFullLock.Value;
            if (p.KeySteerLeft1.HasValue) KeySteerLeft1 = p.KeySteerLeft1.Value;
            if (p.KeySteerLeft2.HasValue) KeySteerLeft2 = p.KeySteerLeft2.Value;
            if (p.KeySteerRight1.HasValue) KeySteerRight1 = p.KeySteerRight1.Value;
            if (p.KeySteerRight2.HasValue) KeySteerRight2 = p.KeySteerRight2.Value;
            if (p.KeyGas1.HasValue) KeyGas1 = p.KeyGas1.Value;
            if (p.KeyGas2.HasValue) KeyGas2 = p.KeyGas2.Value;
            if (p.KeyBrake1.HasValue) KeyBrake1 = p.KeyBrake1.Value;
            if (p.KeyBrake2.HasValue) KeyBrake2 = p.KeyBrake2.Value;
            if (p.KeyShiftUp1.HasValue) KeyShiftUp1 = p.KeyShiftUp1.Value;
            if (p.KeyShiftUp2.HasValue) KeyShiftUp2 = p.KeyShiftUp2.Value;
            if (p.KeyShiftDown1.HasValue) KeyShiftDown1 = p.KeyShiftDown1.Value;
            if (p.KeyShiftDown2.HasValue) KeyShiftDown2 = p.KeyShiftDown2.Value;
            if (p.KeyCenterSteering1.HasValue) KeyCenterSteering1 = p.KeyCenterSteering1.Value;
            if (p.KeyCenterSteering2.HasValue) KeyCenterSteering2 = p.KeyCenterSteering2.Value;
            if (p.KeyToggleHub1.HasValue) KeyToggleHub1 = p.KeyToggleHub1.Value;
            if (p.KeyToggleHub2.HasValue) KeyToggleHub2 = p.KeyToggleHub2.Value;
            if (p.KeyToggleLock1.HasValue) KeyToggleLock1 = p.KeyToggleLock1.Value;
            if (p.KeyToggleLock2.HasValue) KeyToggleLock2 = p.KeyToggleLock2.Value;
            if (KeyToggleLock1 == 0 && KeyToggleLock2 == 0) KeyToggleLock1 = 0x08; // Backspace default
        }
        public void UpdateFrom(ResolvedProfile p)
        {
            MouseSensitivity = p.MouseSensitivity;
            SteeringOffset = p.SteeringOffset;
            SteeringGamma = p.SteeringGamma;
            EnableSpeedSensSteering = p.EnableSpeedSensSteering;
            SpeedSensMinSpeed = p.SpeedSensMinSpeed;
            SpeedSensMaxSpeed = p.SpeedSensMaxSpeed;
            SpeedSensMinMultiplier = p.SpeedSensMinMultiplier;
            PollingRate = p.PollingRate;
            EnableTrailBraking = p.EnableTrailBraking;
            TbIntensity = p.TbIntensity;
            TbMinLimit = p.TbMinLimit;
            TbDecay = p.TbDecay;
            GasThreshold = p.GasThreshold;
            GasAttackTurbo = p.GasAttackTurbo;
            GasAttackFast = p.GasAttackFast;
            GasAttackSlow = p.GasAttackSlow;
            GasDecay = p.GasDecay;
            GasInstantCut = p.GasInstantCut;
            GasGamma = p.GasGamma;
            UseTcHelper = p.UseTcHelper;
            EnableTcAudio = p.EnableTcAudio;
            TcAudioFreq = p.TcAudioFreq;
            TcAudioDur = p.TcAudioDur;
            BrakeThreshold = p.BrakeThreshold;
            BrakeAttackFast = p.BrakeAttackFast;
            BrakeAttackSlow = p.BrakeAttackSlow;
            BrakeDecay = p.BrakeDecay;
            BrakeGamma = p.BrakeGamma;
            UseAbsHelper = p.UseAbsHelper;
            EnableSlipAudio = p.EnableSlipAudio;
            SlipAudioUnderFreq = p.SlipAudioUnderFreq;
            SlipAudioUnderDur = p.SlipAudioUnderDur;
            SlipAudioOverFreq = p.SlipAudioOverFreq;
            SlipAudioOverDur = p.SlipAudioOverDur;
            LockSteerWhenUnlocked = p.LockSteerWhenUnlocked;
            UseSteeringLockClamp = p.UseSteeringLockClamp;
            CarSteeringLock = p.CarSteeringLock;
            GlobalSteeringRange = p.GlobalSteeringRange;
            OutSteerMin = p.OutSteerMin;
            OutSteerMax = p.OutSteerMax;
            OutGasMin = p.OutGasMin;
            OutGasMax = p.OutGasMax;
            OutBrakeMin = p.OutBrakeMin;
            OutBrakeMax = p.OutBrakeMax;
            InvertSteering = p.InvertSteering;
            InvertGas = p.InvertGas;
            InvertBrake = p.InvertBrake;
            UseKeyboardOnlyMode = p.UseKeyboardOnlyMode;
            KeySteerRampUpMs = p.KeySteerRampUpMs;
            KeySteerRampDownMs = p.KeySteerRampDownMs;
            KeySteerSnapToFullLock = p.KeySteerSnapToFullLock;
            KeySteerLeft1 = p.KeySteerLeft1;
            KeySteerLeft2 = p.KeySteerLeft2;
            KeySteerRight1 = p.KeySteerRight1;
            KeySteerRight2 = p.KeySteerRight2;
            KeyGas1 = p.KeyGas1;
            KeyGas2 = p.KeyGas2;
            KeyBrake1 = p.KeyBrake1;
            KeyBrake2 = p.KeyBrake2;
            KeyShiftUp1 = p.KeyShiftUp1;
            KeyShiftUp2 = p.KeyShiftUp2;
            KeyShiftDown1 = p.KeyShiftDown1;
            KeyShiftDown2 = p.KeyShiftDown2;
            KeyCenterSteering1 = p.KeyCenterSteering1;
            KeyCenterSteering2 = p.KeyCenterSteering2;
            KeyToggleHub1 = p.KeyToggleHub1;
            KeyToggleHub2 = p.KeyToggleHub2;
            KeyToggleLock1 = p.KeyToggleLock1;
            KeyToggleLock2 = p.KeyToggleLock2;
        }
    }
}
