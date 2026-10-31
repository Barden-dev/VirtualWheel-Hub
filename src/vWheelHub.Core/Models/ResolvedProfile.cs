using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using SimRacingHub.Core;

namespace SimRacingHub.Models
{
    public partial class ResolvedProfile : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;
        [ObservableProperty]
        private double _mouseSensitivity;
        [ObservableProperty]
        private int _steeringOffset;
        [ObservableProperty]
        private double _steeringGamma = 1.0;
        [ObservableProperty] private bool _enableTrailBraking = true;
        [ObservableProperty] private double _tbIntensity;
        [ObservableProperty] private double _tbMinLimit;
        [ObservableProperty] private double _tbDecay;
        [ObservableProperty]
        private int _pollingRate;
        [ObservableProperty]
        private double _gasThreshold;
        [ObservableProperty]
        private double _gasAttackTurbo;
        [ObservableProperty]
        private double _gasAttackFast;
        [ObservableProperty]
        private double _gasAttackSlow;
        [ObservableProperty]
        private double _gasDecay;
        [ObservableProperty]
        private double _gasInstantCut;
        [ObservableProperty]
        private double _gasGamma = 1.0;
        [ObservableProperty]
        private double _brakeThreshold;
        [ObservableProperty]
        private double _brakeAttackFast;
        [ObservableProperty]
        private double _brakeAttackSlow;
        [ObservableProperty]
        private double _brakeDecay;
        [ObservableProperty]
        private double _brakeGamma = 1.0;
        [ObservableProperty]
        private bool _useSteeringLockClamp;
        [ObservableProperty]
        private int _carSteeringLock = 540;
        [ObservableProperty]
        private int _globalSteeringRange = 900;
        [ObservableProperty] private int _cursorUnlockedMode;
        [ObservableProperty] private bool _pauseControlsOnFocusLoss = true;
        [ObservableProperty] private bool _releaseCursorOnFocusLoss = true;
        [ObservableProperty]
        private OutputDeviceMode _outputMode = OutputDeviceMode.VJoy;

        [ObservableProperty] private int _vJoyDeviceId = 1;
        [ObservableProperty] private bool _vJoyWheelCompatibility;
        [ObservableProperty] private bool _useHubVJoyDevice;
        [ObservableProperty]
        private int _outSteerMin;
        [ObservableProperty]
        private int _outSteerMax;
        [ObservableProperty]
        private int _outGasMin;
        [ObservableProperty]
        private int _outGasMax;
        [ObservableProperty]
        private int _outBrakeMin;
        [ObservableProperty]
        private int _outBrakeMax;
        [ObservableProperty]
        private bool _invertSteering;
        [ObservableProperty]
        private bool _invertGas;
        [ObservableProperty]
        private bool _invertBrake;
        [ObservableProperty]
        private bool _useKeyboardOnlyMode;
        [ObservableProperty]
        private double _keySteerRampUpMs = 200.0;
        [ObservableProperty]
        private double _keySteerRampDownMs = 150.0;
        [ObservableProperty]
        private bool _keySteerSnapToFullLock;
        [ObservableProperty]
        private int _keySteerLeft1;
        [ObservableProperty]
        private int _keySteerLeft2;
        [ObservableProperty]
        private int _keySteerRight1;
        [ObservableProperty]
        private int _keySteerRight2;
        [ObservableProperty]
        private int _keyGas1;
        [ObservableProperty]
        private int _keyGas2;
        [ObservableProperty]
        private int _keyBrake1;
        [ObservableProperty]
        private int _keyBrake2;
        [ObservableProperty]
        private int _keyShiftUp1;
        [ObservableProperty]
        private int _keyShiftUp2;
        [ObservableProperty]
        private int _keyShiftDown1;
        [ObservableProperty]
        private int _keyShiftDown2;
        [ObservableProperty]
        private int _keyCenterSteering1;
        [ObservableProperty]
        private int _keyCenterSteering2;
        [ObservableProperty]
        private int _keyToggleHub1;
        [ObservableProperty]
        private int _keyToggleHub2;
        [ObservableProperty]
        private int _keyToggleLock1;
        [ObservableProperty]
        private int _keyToggleLock2;
        [ObservableProperty]
        private int _keyUnlockCursor1;
        [ObservableProperty]
        private int _keyUnlockCursor2;
        private static readonly HashSet<string> BindingPropertyNames = new(StringComparer.Ordinal)
        {
            nameof(VirtualButtons),
            nameof(KeySteerLeft1),
            nameof(KeySteerLeft2),
            nameof(KeySteerRight1),
            nameof(KeySteerRight2),
            nameof(KeyGas1),
            nameof(KeyGas2),
            nameof(KeyBrake1),
            nameof(KeyBrake2),
            nameof(KeyShiftUp1),
            nameof(KeyShiftUp2),
            nameof(KeyShiftDown1),
            nameof(KeyShiftDown2),
            nameof(KeyCenterSteering1),
            nameof(KeyCenterSteering2),
            nameof(KeyToggleHub1),
            nameof(KeyToggleHub2),
            nameof(KeyToggleLock1),
            nameof(KeyToggleLock2),
            nameof(KeyUnlockCursor1),
            nameof(KeyUnlockCursor2)
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
        public string KeyBindingConflictsText => string.Join(Environment.NewLine, GetKeyBindingConflicts().Select(conflict => $"{KeyNameFormatter.Format(conflict.KeyCode)}: {conflict.Actions}"));

        public bool TryGetKeyBindingConflict(int keyCode, string actionName, out string conflictingActions)
        {
            conflictingActions = string.Join(", ", GetKeyBindings().Where(binding => binding.KeyCode == keyCode && binding.Name != actionName).Select(binding => binding.Name).Distinct());
            return keyCode != 0 && conflictingActions.Length > 0;
        }

        private List<(int KeyCode, string Actions)> GetKeyBindingConflicts()
        {
            return GetKeyBindings().Where(binding => binding.KeyCode != 0).GroupBy(binding => binding.KeyCode).Select(group => new { group.Key, Actions = group.Select(binding => binding.Name).Distinct().ToList() }).Where(conflict => conflict.Actions.Count > 1).Select(conflict => (conflict.Key, string.Join(", ", conflict.Actions))).ToList();
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
            }.Concat(VirtualButtons.Where(binding => binding != null).Select(binding => (Name: "Virtual: " + binding.Name, KeyCode: binding.Key)));
        }

        public void UpdateFrom(Profile p)
        {
            ;
            if (p.EnableTrailBraking.HasValue) EnableTrailBraking = p.EnableTrailBraking.Value;
            if (p.TbIntensity.HasValue) TbIntensity = p.TbIntensity.Value;
            if (p.TbMinLimit.HasValue) TbMinLimit = p.TbMinLimit.Value;
            if (p.TbDecay.HasValue) TbDecay = p.TbDecay.Value;
            if (p.MouseSensitivity.HasValue)
                MouseSensitivity = p.MouseSensitivity.Value;
            if (p.SteeringOffset.HasValue)
                SteeringOffset = p.SteeringOffset.Value;
            if (p.SteeringGamma.HasValue)
                SteeringGamma = p.SteeringGamma.Value;
            ;
            ;
            ;
            ;
            if (p.PollingRate.HasValue)
                PollingRate = p.PollingRate.Value;
            ;
            ;
            ;
            ;
            if (p.GasThreshold.HasValue)
                GasThreshold = p.GasThreshold.Value;
            if (p.GasAttackTurbo.HasValue)
                GasAttackTurbo = p.GasAttackTurbo.Value;
            if (p.GasAttackFast.HasValue)
                GasAttackFast = p.GasAttackFast.Value;
            if (p.GasAttackSlow.HasValue)
                GasAttackSlow = p.GasAttackSlow.Value;
            if (p.GasDecay.HasValue)
                GasDecay = p.GasDecay.Value;
            if (p.GasInstantCut.HasValue)
                GasInstantCut = p.GasInstantCut.Value;
            if (p.GasGamma.HasValue)
                GasGamma = p.GasGamma.Value;
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
            if (p.BrakeThreshold.HasValue)
                BrakeThreshold = p.BrakeThreshold.Value;
            if (p.BrakeAttackFast.HasValue)
                BrakeAttackFast = p.BrakeAttackFast.Value;
            if (p.BrakeAttackSlow.HasValue)
                BrakeAttackSlow = p.BrakeAttackSlow.Value;
            if (p.BrakeDecay.HasValue)
                BrakeDecay = p.BrakeDecay.Value;
            if (p.BrakeGamma.HasValue)
                BrakeGamma = p.BrakeGamma.Value;
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
            ;
            ;
            ;
            ;
            ;
            if (p.UseSteeringLockClamp.HasValue)
                UseSteeringLockClamp = p.UseSteeringLockClamp.Value;
            if (p.CarSteeringLock.HasValue)
                CarSteeringLock = p.CarSteeringLock.Value;
            if (p.GlobalSteeringRange.HasValue)
                GlobalSteeringRange = p.GlobalSteeringRange.Value;
            if (p.CursorUnlockedMode.HasValue) CursorUnlockedMode = Math.Clamp(p.CursorUnlockedMode.Value, 0, 2);
            if (p.PauseControlsOnFocusLoss.HasValue) PauseControlsOnFocusLoss = p.PauseControlsOnFocusLoss.Value;
            if (p.ReleaseCursorOnFocusLoss.HasValue) ReleaseCursorOnFocusLoss = p.ReleaseCursorOnFocusLoss.Value;
            if (p.OutputMode.HasValue)
                OutputMode = p.OutputMode.Value;
            if (p.VJoyDeviceId.HasValue) VJoyDeviceId = Math.Clamp(p.VJoyDeviceId.Value, 1, 16);
            if (p.VJoyWheelCompatibility.HasValue) VJoyWheelCompatibility = p.VJoyWheelCompatibility.Value;
            if (p.UseHubVJoyDevice.HasValue) UseHubVJoyDevice = p.UseHubVJoyDevice.Value;
            if (p.VirtualButtons != null) VirtualButtons = p.VirtualButtons.ToArray();
            if (p.OutSteerMin.HasValue)
                OutSteerMin = p.OutSteerMin.Value;
            if (p.OutSteerMax.HasValue)
                OutSteerMax = p.OutSteerMax.Value;
            if (p.OutGasMin.HasValue)
                OutGasMin = p.OutGasMin.Value;
            if (p.OutGasMax.HasValue)
                OutGasMax = p.OutGasMax.Value;
            if (p.OutBrakeMin.HasValue)
                OutBrakeMin = p.OutBrakeMin.Value;
            if (p.OutBrakeMax.HasValue)
                OutBrakeMax = p.OutBrakeMax.Value;
            if (p.InvertSteering.HasValue)
                InvertSteering = p.InvertSteering.Value;
            if (p.InvertGas.HasValue)
                InvertGas = p.InvertGas.Value;
            if (p.InvertBrake.HasValue)
                InvertBrake = p.InvertBrake.Value;
            if (p.UseKeyboardOnlyMode.HasValue)
                UseKeyboardOnlyMode = p.UseKeyboardOnlyMode.Value;
            if (p.KeySteerRampUpMs.HasValue)
                KeySteerRampUpMs = p.KeySteerRampUpMs.Value;
            if (p.KeySteerRampDownMs.HasValue)
                KeySteerRampDownMs = p.KeySteerRampDownMs.Value;
            if (p.KeySteerSnapToFullLock.HasValue)
                KeySteerSnapToFullLock = p.KeySteerSnapToFullLock.Value;
            if (p.KeySteerLeft1.HasValue)
                KeySteerLeft1 = p.KeySteerLeft1.Value;
            if (p.KeySteerLeft2.HasValue)
                KeySteerLeft2 = p.KeySteerLeft2.Value;
            if (p.KeySteerRight1.HasValue)
                KeySteerRight1 = p.KeySteerRight1.Value;
            if (p.KeySteerRight2.HasValue)
                KeySteerRight2 = p.KeySteerRight2.Value;
            if (p.KeyGas1.HasValue)
                KeyGas1 = p.KeyGas1.Value;
            if (p.KeyGas2.HasValue)
                KeyGas2 = p.KeyGas2.Value;
            if (p.KeyBrake1.HasValue)
                KeyBrake1 = p.KeyBrake1.Value;
            if (p.KeyBrake2.HasValue)
                KeyBrake2 = p.KeyBrake2.Value;
            if (p.KeyShiftUp1.HasValue)
                KeyShiftUp1 = p.KeyShiftUp1.Value;
            if (p.KeyShiftUp2.HasValue)
                KeyShiftUp2 = p.KeyShiftUp2.Value;
            if (p.KeyShiftDown1.HasValue)
                KeyShiftDown1 = p.KeyShiftDown1.Value;
            if (p.KeyShiftDown2.HasValue)
                KeyShiftDown2 = p.KeyShiftDown2.Value;
            if (p.KeyCenterSteering1.HasValue)
                KeyCenterSteering1 = p.KeyCenterSteering1.Value;
            if (p.KeyCenterSteering2.HasValue)
                KeyCenterSteering2 = p.KeyCenterSteering2.Value;
            if (p.KeyToggleHub1.HasValue)
                KeyToggleHub1 = p.KeyToggleHub1.Value;
            if (p.KeyToggleHub2.HasValue)
                KeyToggleHub2 = p.KeyToggleHub2.Value;
            if (p.KeyToggleLock1.HasValue)
                KeyToggleLock1 = p.KeyToggleLock1.Value;
            if (p.KeyToggleLock2.HasValue)
                KeyToggleLock2 = p.KeyToggleLock2.Value;
            if (p.KeyUnlockCursor1.HasValue)
                KeyUnlockCursor1 = p.KeyUnlockCursor1.Value;
            if (p.KeyUnlockCursor2.HasValue)
                KeyUnlockCursor2 = p.KeyUnlockCursor2.Value;
        }

        public void UpdateFrom(ResolvedProfile p)
        {
            ;
            EnableTrailBraking = p.EnableTrailBraking;
            TbIntensity = p.TbIntensity;
            TbMinLimit = p.TbMinLimit;
            TbDecay = p.TbDecay;
            MouseSensitivity = p.MouseSensitivity;
            SteeringOffset = p.SteeringOffset;
            SteeringGamma = p.SteeringGamma;
            ;
            ;
            ;
            ;
            PollingRate = p.PollingRate;
            ;
            ;
            ;
            ;
            GasThreshold = p.GasThreshold;
            GasAttackTurbo = p.GasAttackTurbo;
            GasAttackFast = p.GasAttackFast;
            GasAttackSlow = p.GasAttackSlow;
            GasDecay = p.GasDecay;
            GasInstantCut = p.GasInstantCut;
            GasGamma = p.GasGamma;
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
            BrakeThreshold = p.BrakeThreshold;
            BrakeAttackFast = p.BrakeAttackFast;
            BrakeAttackSlow = p.BrakeAttackSlow;
            BrakeDecay = p.BrakeDecay;
            BrakeGamma = p.BrakeGamma;
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
            ;
            ;
            ;
            ;
            ;
            UseSteeringLockClamp = p.UseSteeringLockClamp;
            CarSteeringLock = p.CarSteeringLock;
            GlobalSteeringRange = p.GlobalSteeringRange;
            CursorUnlockedMode = Math.Clamp(p.CursorUnlockedMode, 0, 2);
            PauseControlsOnFocusLoss = p.PauseControlsOnFocusLoss;
            ReleaseCursorOnFocusLoss = p.ReleaseCursorOnFocusLoss;
            OutputMode = p.OutputMode;
            VJoyDeviceId = Math.Clamp(p.VJoyDeviceId, 1, 16);
            VJoyWheelCompatibility = p.VJoyWheelCompatibility;
            UseHubVJoyDevice = p.UseHubVJoyDevice;
            VirtualButtons = p.VirtualButtons.ToArray();
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
                EnableTrailBraking = EnableTrailBraking,
                TbIntensity = TbIntensity,
                TbMinLimit = TbMinLimit,
                TbDecay = TbDecay,
                MouseSensitivity = MouseSensitivity,
                SteeringOffset = SteeringOffset,
                SteeringGamma = SteeringGamma,
                PollingRate = PollingRate,
                GasThreshold = GasThreshold,
                GasAttackTurbo = GasAttackTurbo,
                GasAttackFast = GasAttackFast,
                GasAttackSlow = GasAttackSlow,
                GasDecay = GasDecay,
                GasInstantCut = GasInstantCut,
                GasGamma = GasGamma,
                BrakeThreshold = BrakeThreshold,
                BrakeAttackFast = BrakeAttackFast,
                BrakeAttackSlow = BrakeAttackSlow,
                BrakeDecay = BrakeDecay,
                BrakeGamma = BrakeGamma,
                UseSteeringLockClamp = UseSteeringLockClamp,
                CarSteeringLock = CarSteeringLock,
                GlobalSteeringRange = GlobalSteeringRange,
                CursorUnlockedMode = CursorUnlockedMode,
                PauseControlsOnFocusLoss = PauseControlsOnFocusLoss,
                ReleaseCursorOnFocusLoss = ReleaseCursorOnFocusLoss,
                OutputMode = OutputMode,
                VJoyDeviceId = VJoyDeviceId,
                VJoyWheelCompatibility = VJoyWheelCompatibility,
                UseHubVJoyDevice = UseHubVJoyDevice,
                VirtualButtons = VirtualButtons.ToArray(),
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
