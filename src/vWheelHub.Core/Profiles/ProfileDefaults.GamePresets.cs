using System;
using SimRacingHub.Models;

namespace SimRacingHub.Models;

/// <summary>Game-specific profile presets, preserving each edition's supported settings.</summary>
public static partial class ProfileDefaults
{
    public static Profile CreateBaseDefault(string name = "Universal")
    {
        var profile = new Profile
        {
            Name = name,
            SchemaVersion = Profile.CurrentSchemaVersion,
            EnableTrailBraking = true,
            TbIntensity = 1.0,
            TbMinLimit = 0.20,
            TbDecay = Profile.DefaultTbDecay,
            MouseSensitivity = 3.7,
            SteeringOffset = 0,
            SteeringGamma = 1.0,
            PollingRate = 1000,
            GasThreshold = 0.80,
            GasAttackTurbo = 1850.0,
            GasAttackFast = 666.0,
            GasAttackSlow = 222.0,
            GasDecay = 1110.0,
            GasInstantCut = 7400.0,
            GasGamma = 1.0,
            BrakeThreshold = 0.90,
            BrakeAttackFast = 1480.0,
            BrakeAttackSlow = 148.0,
            BrakeDecay = 1295.0,
            BrakeGamma = 1.0,
            UseSteeringLockClamp = false,
            CarSteeringLock = 540,
            GlobalSteeringRange = 900,
            OutputMode = OutputDeviceMode.VJoy,
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
            KeySteerLeft1 = 0x41,
            KeySteerLeft2 = 0x25,
            KeySteerRight1 = 0x44,
            KeySteerRight2 = 0x27,
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
            KeyToggleLock1 = 0,
            KeyToggleLock2 = 0,
            KeyUnlockCursor1 = 0,
            KeyUnlockCursor2 = 0
        };
        ProfileSchemaMigrator.ScalePedalRates(profile, 1000);
        return profile;
    }

    public static Profile CreateLmuDefault(string name = "Le Mans Ultimate")
    {
        var profile = new Profile
        {
            Name = name,
            SchemaVersion = Profile.CurrentSchemaVersion,
            EnableTrailBraking = true,
            TbIntensity = 1.2,
            TbMinLimit = 0.20,
            TbDecay = Profile.DefaultTbDecay,
            MouseSensitivity = 4.0,
            SteeringOffset = 0,
            SteeringGamma = 1.0,
            PollingRate = 200,
            GasThreshold = 0.80,
            GasAttackTurbo = 1850.0,
            GasAttackFast = 660.0,
            GasAttackSlow = 220.0,
            GasDecay = 1110.0,
            GasInstantCut = 7400.0,
            GasGamma = 1.0,
            BrakeThreshold = 0.90,
            BrakeAttackFast = 1480.0,
            BrakeAttackSlow = 148.0,
            BrakeDecay = 1295.0,
            BrakeGamma = 1.0,
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
            KeySteerLeft1 = 0x41,
            KeySteerLeft2 = 0x25,
            KeySteerRight1 = 0x44,
            KeySteerRight2 = 0x27,
            KeyGas1 = 0x57,
            KeyGas2 = 0x01,
            KeyBrake1 = 0x53,
            KeyBrake2 = 0x02,
            KeyShiftUp1 = 0x44,
            KeyShiftUp2 = 0x06,
            KeyShiftDown1 = 0x41,
            KeyShiftDown2 = 0x05,
            KeyCenterSteering1 = 0x2E,
            KeyCenterSteering2 = 0,
            KeyToggleHub1 = 0,
            KeyToggleHub2 = 0,
            KeyToggleLock1 = 0x23,
            KeyToggleLock2 = 0,
            KeyUnlockCursor1 = 0,
            KeyUnlockCursor2 = 0
        };
        ProfileSchemaMigrator.ScalePedalRates(profile, 200);
        return profile;
    }

    public static Profile CreateAcDefault(string name = "Assetto Corsa")
    {
        var p = CreateBaseDefault(name);
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
        return p;
    }

    public static Profile CreateAccDefault(string name = "Assetto Corsa Competizione")
    {
        var p = CreateBaseDefault(name);
        p.PollingRate = 400;
        ProfileSchemaMigrator.ScalePedalRates(p, 0.4);
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
        return p;
    }

    public static Profile CreateAceDefault(string name = "Assetto Corsa EVO")
    {
        var p = CreateBaseDefault(name);
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
        return p;
    }

}
