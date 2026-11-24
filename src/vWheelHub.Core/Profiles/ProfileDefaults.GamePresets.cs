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
            MouseSensitivity = 3.7,
            SteeringOffset = 0,
            SteeringGamma = 1.0,
            PollingRate = 1000,
            EnableTrailBraking = true,
            TbIntensity = 1.0,
            TbMinLimit = 0.20,
            TbDecay = Profile.DefaultTbDecay,
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
            ClutchThreshold = 0.90,
            ClutchAttackFast = 1480.0,
            ClutchAttackSlow = 148.0,
            ClutchDecay = 1295.0,
            ClutchGamma = 1.0,
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
            OutClutchMin = 0,
            OutClutchMax = 32768,
            InvertSteering = false,
            InvertGas = false,
            InvertBrake = false,
            InvertClutch = false,
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
            KeyClutch1 = 0,
            KeyClutch2 = 0,
            KeyShiftUp1 = 0x45,
            KeyShiftUp2 = 0x06,
            KeyShiftDown1 = 0x51,
            KeyShiftDown2 = 0x05,
            KeyCenterSteering1 = 0x20,
            KeyCenterSteering2 = 0,
            KeyToggleHub1 = 0,
            KeyToggleHub2 = 0,
            KeyToggleLock1 = 0, // Unassigned; existing saved game bindings are preserved.
            KeyToggleLock2 = 0,
            KeyUnlockCursor1 = 0,
            KeyUnlockCursor2 = 0
        };
        ApplyBaseExtras(profile);
        ProfileSchemaMigrator.ScalePedalRates(profile, 1000);
        return profile;
    }

    public static Profile CreateLmuDefault(string name = "Le Mans Ultimate")
    {
        var profile = new Profile
        {
            Name = name,
            SchemaVersion = Profile.CurrentSchemaVersion,
            MouseSensitivity = 4.0,
            SteeringOffset = 0,
            SteeringGamma = 1.0,
            PollingRate = 200,
            EnableTrailBraking = true,
            TbIntensity = 1.2,
            TbMinLimit = 0.20,
            TbDecay = Profile.DefaultTbDecay,
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
            ClutchThreshold = 0.90,
            ClutchAttackFast = 1480.0,
            ClutchAttackSlow = 148.0,
            ClutchDecay = 1295.0,
            ClutchGamma = 1.0,
            UseSteeringLockClamp = true,
            CarSteeringLock = 580,
            GlobalSteeringRange = 900,
            OutSteerMin = 0,
            OutSteerMax = 32768,
            OutGasMin = 16384,
            OutGasMax = 32768,
            OutBrakeMin = 0,
            OutBrakeMax = 16384,
            OutClutchMin = 0,
            OutClutchMax = 32768,
            InvertSteering = false,
            InvertGas = false,
            InvertBrake = true,
            InvertClutch = false,
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
            KeyClutch1 = 0,
            KeyClutch2 = 0,
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
        ApplyLmuExtras(profile);
        ProfileSchemaMigrator.ScalePedalRates(profile, 200);
        return profile;
    }

    public static Profile CreateAcDefault(string name = "Assetto Corsa")
    {
        var p = CreateBaseDefault(name);
        ApplyAcExtras(p);
        return p;
    }

    public static Profile CreateAccDefault(string name = "Assetto Corsa Competizione")
    {
        var p = CreateBaseDefault(name);
        p.PollingRate = 400;
        ProfileSchemaMigrator.ScalePedalRates(p, 0.4);
        ApplyAccExtras(p);
        return p;
    }

    public static Profile CreateAceDefault(string name = "Assetto Corsa EVO")
    {
        var p = CreateBaseDefault(name);
        ApplyAceExtras(p);
        return p;
    }

    static partial void ApplyBaseExtras(Profile p);
    static partial void ApplyLmuExtras(Profile p);
    static partial void ApplyAcExtras(Profile p);
    static partial void ApplyAccExtras(Profile p);
    static partial void ApplyAceExtras(Profile p);
}
