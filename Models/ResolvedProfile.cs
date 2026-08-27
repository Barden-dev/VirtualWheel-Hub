using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using SimRacingHub.Core;

namespace SimRacingHub.Models
{
    public partial class ResolvedProfile : ObservableObject
    {
        [ObservableProperty] private string _name = string.Empty;
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
        [ObservableProperty] private double _tcSlipThreshold = 0.05;
        [ObservableProperty] private double _tcMinScale = 0.85;
        [ObservableProperty] private double _tcDropAmount = 0.15;
        [ObservableProperty] private double _tcCutRate = 20.0;
        [ObservableProperty] private double _tcRecoverRate = 3.0;
        [ObservableProperty] private double _tcMinSpeed = 5.0;
        [ObservableProperty] private bool _enableTcAudio;
        [ObservableProperty] private int _tcAudioFreq;
        [ObservableProperty] private int _tcAudioDur;
        [ObservableProperty] private double _brakeThreshold;
        [ObservableProperty] private double _brakeAttackFast;
        [ObservableProperty] private double _brakeAttackSlow;
        [ObservableProperty] private double _brakeDecay;
        [ObservableProperty] private double _brakeGamma = 1.0;
        [ObservableProperty] private bool _useAbsHelper;
        [ObservableProperty] private double _absSlipThreshold = 0.10;
        [ObservableProperty] private double _absMinScale = 0.90;
        [ObservableProperty] private double _absDropAmount = 0.10;
        [ObservableProperty] private double _absCutRate = 25.0;
        [ObservableProperty] private double _absRecoverRate = 2.0;
        [ObservableProperty] private double _absMinSpeed = 10.0;
        [ObservableProperty] private bool _enableSlipAudio;
        [ObservableProperty] private double _slipAudioUnderThreshold = 2.0;
        [ObservableProperty] private double _slipAudioOverThreshold = 1.8;
        [ObservableProperty] private double _slipAudioMinSpeed = 50.0;

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
        [ObservableProperty] private bool _pauseControlsInMenu = true;
        [ObservableProperty] private bool _autoUnlockMouseInMenu = true;
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
        [ObservableProperty] private int _keyUnlockCursor1;
        [ObservableProperty] private int _keyUnlockCursor2;

        private static readonly HashSet<string> BindingPropertyNames = new(StringComparer.Ordinal)
        {
            nameof(KeySteerLeft1), nameof(KeySteerLeft2), nameof(KeySteerRight1), nameof(KeySteerRight2),
            nameof(KeyGas1), nameof(KeyGas2), nameof(KeyBrake1), nameof(KeyBrake2),
            nameof(KeyShiftUp1), nameof(KeyShiftUp2), nameof(KeyShiftDown1), nameof(KeyShiftDown2),
            nameof(KeyCenterSteering1), nameof(KeyCenterSteering2), nameof(KeyToggleHub1), nameof(KeyToggleHub2),
            nameof(KeyToggleLock1), nameof(KeyToggleLock2), nameof(KeyUnlockCursor1), nameof(KeyUnlockCursor2)
        };

        public ResolvedProfile()
        {
            PropertyChanged += (_, args) =>
            {
                if (args.PropertyName != null && BindingPropertyNames.Contains(args.PropertyName))
                {
                    OnPropertyChanged(nameof(HasKeyBindingConflicts));
                    OnPropertyChanged(nameof(KeyBindingConflictsText));
                }
            };
        }

        public bool HasKeyBindingConflicts => GetKeyBindingConflicts().Count > 0;

        public string KeyBindingConflictsText => string.Join(
            Environment.NewLine,
            GetKeyBindingConflicts().Select(conflict => $"{KeyNameFormatter.Format(conflict.KeyCode)}: {conflict.Actions}"));

        public bool TryGetKeyBindingConflict(int keyCode, string actionName, out string conflictingActions)
        {
            conflictingActions = string.Join(", ", GetKeyBindings()
                .Where(binding => binding.KeyCode == keyCode && binding.Name != actionName)
                .Select(binding => binding.Name)
                .Distinct());

            return keyCode != 0 && conflictingActions.Length > 0;
        }

        private List<(int KeyCode, string Actions)> GetKeyBindingConflicts()
        {
            return GetKeyBindings()
                .Where(binding => binding.KeyCode != 0)
                .GroupBy(binding => binding.KeyCode)
                .Select(group => new
                {
                    group.Key,
                    Actions = group.Select(binding => binding.Name).Distinct().ToList()
                })
                .Where(conflict => conflict.Actions.Count > 1)
                .Select(conflict => (conflict.Key, string.Join(", ", conflict.Actions)))
                .ToList();
        }

        private IEnumerable<(string Name, int KeyCode)> GetKeyBindings()
        {
            return new[]
            {
                (Name: "Steer Left", KeyCode: KeySteerLeft1),
                (Name: "Steer Left", KeyCode: KeySteerLeft2),
                (Name: "Steer Right", KeyCode: KeySteerRight1),
                (Name: "Steer Right", KeyCode: KeySteerRight2),
                (Name: "Throttle", KeyCode: KeyGas1),
                (Name: "Throttle", KeyCode: KeyGas2),
                (Name: "Brake", KeyCode: KeyBrake1),
                (Name: "Brake", KeyCode: KeyBrake2),
                (Name: "Shift Up", KeyCode: KeyShiftUp1),
                (Name: "Shift Up", KeyCode: KeyShiftUp2),
                (Name: "Shift Down", KeyCode: KeyShiftDown1),
                (Name: "Shift Down", KeyCode: KeyShiftDown2),
                (Name: "Center Steer", KeyCode: KeyCenterSteering1),
                (Name: "Center Steer", KeyCode: KeyCenterSteering2),
                (Name: "Toggle Hub", KeyCode: KeyToggleHub1),
                (Name: "Toggle Hub", KeyCode: KeyToggleHub2),
                (Name: "Toggle Lock", KeyCode: KeyToggleLock1),
                (Name: "Toggle Lock", KeyCode: KeyToggleLock2),
                (Name: "Unlock Cursor (Hold)", KeyCode: KeyUnlockCursor1),
                (Name: "Unlock Cursor (Hold)", KeyCode: KeyUnlockCursor2)
            };
        }

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
            if (p.TcSlipThreshold.HasValue) TcSlipThreshold = p.TcSlipThreshold.Value;
            if (p.TcMinScale.HasValue) TcMinScale = p.TcMinScale.Value;
            if (p.TcDropAmount.HasValue) TcDropAmount = p.TcDropAmount.Value;
            else if (p.TcMinScale.HasValue) TcDropAmount = Math.Clamp(1.0 - p.TcMinScale.Value, 0.01, 0.80);
            if (p.TcCutRate.HasValue) TcCutRate = p.TcCutRate.Value;
            if (p.TcRecoverRate.HasValue) TcRecoverRate = p.TcRecoverRate.Value;
            if (p.TcMinSpeed.HasValue) TcMinSpeed = p.TcMinSpeed.Value;
            if (p.EnableTcAudio.HasValue) EnableTcAudio = p.EnableTcAudio.Value;
            if (p.TcAudioFreq.HasValue) TcAudioFreq = p.TcAudioFreq.Value;
            if (p.TcAudioDur.HasValue) TcAudioDur = p.TcAudioDur.Value;
            if (p.BrakeThreshold.HasValue) BrakeThreshold = p.BrakeThreshold.Value;
            if (p.BrakeAttackFast.HasValue) BrakeAttackFast = p.BrakeAttackFast.Value;
            if (p.BrakeAttackSlow.HasValue) BrakeAttackSlow = p.BrakeAttackSlow.Value;
            if (p.BrakeDecay.HasValue) BrakeDecay = p.BrakeDecay.Value;
            if (p.BrakeGamma.HasValue) BrakeGamma = p.BrakeGamma.Value;
            if (p.UseAbsHelper.HasValue) UseAbsHelper = p.UseAbsHelper.Value;
            if (p.AbsSlipThreshold.HasValue) AbsSlipThreshold = p.AbsSlipThreshold.Value;
            if (p.AbsMinScale.HasValue) AbsMinScale = p.AbsMinScale.Value;
            if (p.AbsDropAmount.HasValue) AbsDropAmount = p.AbsDropAmount.Value;
            else if (p.AbsMinScale.HasValue) AbsDropAmount = Math.Clamp(1.0 - p.AbsMinScale.Value, 0.01, 0.80);
            if (p.AbsCutRate.HasValue) AbsCutRate = p.AbsCutRate.Value;
            if (p.AbsRecoverRate.HasValue) AbsRecoverRate = p.AbsRecoverRate.Value;
            if (p.AbsMinSpeed.HasValue) AbsMinSpeed = p.AbsMinSpeed.Value;
            if (p.EnableSlipAudio.HasValue) EnableSlipAudio = p.EnableSlipAudio.Value;
            if (p.SlipAudioUnderThreshold.HasValue) SlipAudioUnderThreshold = p.SlipAudioUnderThreshold.Value;
            if (p.SlipAudioOverThreshold.HasValue) SlipAudioOverThreshold = p.SlipAudioOverThreshold.Value;
            if (p.SlipAudioMinSpeed.HasValue) SlipAudioMinSpeed = p.SlipAudioMinSpeed.Value;
            if (p.SlipAudioUnderFreq.HasValue) SlipAudioUnderFreq = p.SlipAudioUnderFreq.Value;
            if (p.SlipAudioUnderDur.HasValue) SlipAudioUnderDur = p.SlipAudioUnderDur.Value;
            if (p.SlipAudioOverFreq.HasValue) SlipAudioOverFreq = p.SlipAudioOverFreq.Value;
            if (p.SlipAudioOverDur.HasValue) SlipAudioOverDur = p.SlipAudioOverDur.Value;
            if (p.PauseControlsInMenu.HasValue) PauseControlsInMenu = p.PauseControlsInMenu.Value;
            if (p.AutoUnlockMouseInMenu.HasValue) AutoUnlockMouseInMenu = p.AutoUnlockMouseInMenu.Value;
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
            if (p.KeyUnlockCursor1.HasValue) KeyUnlockCursor1 = p.KeyUnlockCursor1.Value;
            if (p.KeyUnlockCursor2.HasValue) KeyUnlockCursor2 = p.KeyUnlockCursor2.Value;
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
            TcSlipThreshold = p.TcSlipThreshold;
            TcMinScale = p.TcMinScale;
            TcDropAmount = p.TcDropAmount;
            TcCutRate = p.TcCutRate;
            TcRecoverRate = p.TcRecoverRate;
            TcMinSpeed = p.TcMinSpeed;
            EnableTcAudio = p.EnableTcAudio;
            TcAudioFreq = p.TcAudioFreq;
            TcAudioDur = p.TcAudioDur;
            BrakeThreshold = p.BrakeThreshold;
            BrakeAttackFast = p.BrakeAttackFast;
            BrakeAttackSlow = p.BrakeAttackSlow;
            BrakeDecay = p.BrakeDecay;
            BrakeGamma = p.BrakeGamma;
            UseAbsHelper = p.UseAbsHelper;
            AbsSlipThreshold = p.AbsSlipThreshold;
            AbsMinScale = p.AbsMinScale;
            AbsDropAmount = p.AbsDropAmount;
            AbsCutRate = p.AbsCutRate;
            AbsRecoverRate = p.AbsRecoverRate;
            AbsMinSpeed = p.AbsMinSpeed;
            EnableSlipAudio = p.EnableSlipAudio;
            SlipAudioUnderThreshold = p.SlipAudioUnderThreshold;
            SlipAudioOverThreshold = p.SlipAudioOverThreshold;
            SlipAudioMinSpeed = p.SlipAudioMinSpeed;
            SlipAudioUnderFreq = p.SlipAudioUnderFreq;
            SlipAudioUnderDur = p.SlipAudioUnderDur;
            SlipAudioOverFreq = p.SlipAudioOverFreq;
            SlipAudioOverDur = p.SlipAudioOverDur;
            PauseControlsInMenu = p.PauseControlsInMenu;
            AutoUnlockMouseInMenu = p.AutoUnlockMouseInMenu;
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
            KeyUnlockCursor1 = p.KeyUnlockCursor1;
            KeyUnlockCursor2 = p.KeyUnlockCursor2;
        }

        public Profile ToProfile(string? name = null)
        {
            return new Profile
            {
                Name = name ?? Name,
                SchemaVersion = Profile.CurrentSchemaVersion,
                MouseSensitivity = MouseSensitivity,
                SteeringOffset = SteeringOffset,
                SteeringGamma = SteeringGamma,
                EnableSpeedSensSteering = EnableSpeedSensSteering,
                SpeedSensMinSpeed = SpeedSensMinSpeed,
                SpeedSensMaxSpeed = SpeedSensMaxSpeed,
                SpeedSensMinMultiplier = SpeedSensMinMultiplier,
                PollingRate = PollingRate,
                EnableTrailBraking = EnableTrailBraking,
                TbIntensity = TbIntensity,
                TbMinLimit = TbMinLimit,
                TbDecay = TbDecay,
                GasThreshold = GasThreshold,
                GasAttackTurbo = GasAttackTurbo,
                GasAttackFast = GasAttackFast,
                GasAttackSlow = GasAttackSlow,
                GasDecay = GasDecay,
                GasInstantCut = GasInstantCut,
                GasGamma = GasGamma,
                UseTcHelper = UseTcHelper,
                TcSlipThreshold = TcSlipThreshold,
                TcMinScale = TcMinScale,
                TcDropAmount = TcDropAmount,
                TcCutRate = TcCutRate,
                TcRecoverRate = TcRecoverRate,
                TcMinSpeed = TcMinSpeed,
                EnableTcAudio = EnableTcAudio,
                TcAudioFreq = TcAudioFreq,
                TcAudioDur = TcAudioDur,
                BrakeThreshold = BrakeThreshold,
                BrakeAttackFast = BrakeAttackFast,
                BrakeAttackSlow = BrakeAttackSlow,
                BrakeDecay = BrakeDecay,
                BrakeGamma = BrakeGamma,
                UseAbsHelper = UseAbsHelper,
                AbsSlipThreshold = AbsSlipThreshold,
                AbsMinScale = AbsMinScale,
                AbsDropAmount = AbsDropAmount,
                AbsCutRate = AbsCutRate,
                AbsRecoverRate = AbsRecoverRate,
                AbsMinSpeed = AbsMinSpeed,
                EnableSlipAudio = EnableSlipAudio,
                SlipAudioUnderThreshold = SlipAudioUnderThreshold,
                SlipAudioOverThreshold = SlipAudioOverThreshold,
                SlipAudioMinSpeed = SlipAudioMinSpeed,
                SlipAudioUnderFreq = SlipAudioUnderFreq,
                SlipAudioUnderDur = SlipAudioUnderDur,
                SlipAudioOverFreq = SlipAudioOverFreq,
                SlipAudioOverDur = SlipAudioOverDur,
                PauseControlsInMenu = PauseControlsInMenu,
                AutoUnlockMouseInMenu = AutoUnlockMouseInMenu,
                UseSteeringLockClamp = UseSteeringLockClamp,
                CarSteeringLock = CarSteeringLock,
                GlobalSteeringRange = GlobalSteeringRange,
                OutSteerMin = OutSteerMin,
                OutSteerMax = OutSteerMax,
                OutGasMin = OutGasMin,
                OutGasMax = OutGasMax,
                OutBrakeMin = OutBrakeMin,
                OutBrakeMax = OutBrakeMax,
                InvertSteering = InvertSteering,
                InvertGas = InvertGas,
                InvertBrake = InvertBrake,
                UseKeyboardOnlyMode = UseKeyboardOnlyMode,
                KeySteerRampUpMs = KeySteerRampUpMs,
                KeySteerRampDownMs = KeySteerRampDownMs,
                KeySteerSnapToFullLock = KeySteerSnapToFullLock,
                KeySteerLeft1 = KeySteerLeft1,
                KeySteerLeft2 = KeySteerLeft2,
                KeySteerRight1 = KeySteerRight1,
                KeySteerRight2 = KeySteerRight2,
                KeyGas1 = KeyGas1,
                KeyGas2 = KeyGas2,
                KeyBrake1 = KeyBrake1,
                KeyBrake2 = KeyBrake2,
                KeyShiftUp1 = KeyShiftUp1,
                KeyShiftUp2 = KeyShiftUp2,
                KeyShiftDown1 = KeyShiftDown1,
                KeyShiftDown2 = KeyShiftDown2,
                KeyCenterSteering1 = KeyCenterSteering1,
                KeyCenterSteering2 = KeyCenterSteering2,
                KeyToggleHub1 = KeyToggleHub1,
                KeyToggleHub2 = KeyToggleHub2,
                KeyToggleLock1 = KeyToggleLock1,
                KeyToggleLock2 = KeyToggleLock2,
                KeyUnlockCursor1 = KeyUnlockCursor1,
                KeyUnlockCursor2 = KeyUnlockCursor2
            };
        }
    }
}
