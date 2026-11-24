using System;
using SimRacingHub.Models;

namespace SimRacingHub.Models;

/// <summary>Default-profile dispatch and presets shared by both editions.</summary>
public static partial class ProfileDefaults
{
    // For default profile initialization
    public static Profile CreateDefault(string name = "Universal")
    {
        if (string.Equals(name, "Le Mans Ultimate", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "LMU", StringComparison.OrdinalIgnoreCase))
        {
            return CreateLmuDefault(name);
        }
        if (string.Equals(name, "Assetto Corsa", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "AC", StringComparison.OrdinalIgnoreCase))
        {
            return CreateAcDefault(name);
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
        if (string.Equals(name, "WRC Generations", StringComparison.OrdinalIgnoreCase))
        {
            return CreateWrcGenerationsDefault(name);
        }
        if (string.Equals(name, "iRacing", StringComparison.OrdinalIgnoreCase))
        {
            return CreateIRacingDefault(name);
        }
        if (string.Equals(name, "Forza Motorsport", StringComparison.OrdinalIgnoreCase))
        {
            return CreateForzaMotorsportDefault(name);
        }
        if (string.Equals(name, "Forza Horizon 5", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "Forza Horizon 4", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "Forza Horizon", StringComparison.OrdinalIgnoreCase))
        {
            return CreateForzaHorizonDefault(name);
        }

        return CreateBaseDefault(name);
    }

    public static Profile CreateBeamNgDefault(string name = "BeamNG.drive")
    {
        var p = CreateBaseDefault(name);
        // Preserve the supplied BeamNG preset's 200 Hz control cadence.
        // Pedal coefficients below are converted to per-second units, so
        // changing the hub rate later does not change their physical speed.
        p.PollingRate = 200;
        ProfileSchemaMigrator.ScalePedalRates(p, 0.2);
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

    public static Profile CreateWrcGenerationsDefault(string name = "WRC Generations")
    {
        var p = CreateBaseDefault(name);
        p.CarSteeringLock = 540;
        p.UseSteeringLockClamp = false;
        return p;
    }

    public static Profile CreateIRacingDefault(string name = "iRacing")
    {
        var profile = CreateBaseDefault(name);
        profile.MouseSensitivity = 2.2;
        profile.GasAttackFastSeconds = 0.270;
        profile.GasAttackSlowSeconds = 0.240;
        profile.GasDecaySeconds = 0.240;
        profile.GasAttackTurboSeconds = 0.100;
        profile.GasInstantCutSeconds = 0.006;
        profile.BrakeAttackFastSeconds = 0.310;
        profile.BrakeAttackSlowSeconds = 0.290;
        profile.BrakeDecaySeconds = 0.190;
        profile.ClutchAttackFastSeconds = 0.120;
        profile.ClutchAttackSlowSeconds = 0.100;
        profile.ClutchDecaySeconds = 0.220;
        profile.CarSteeringLock = 90;
        profile.CursorUnlockedMode = 2;
        profile.PauseControlsOnFocusLoss = false;
        profile.ReleaseCursorOnFocusLoss = false;
        profile.OutputMode = null;
        profile.KeySteerLeft1 = 0;
        profile.KeySteerRight1 = 0;
        profile.KeyGas2 = 0;
        profile.KeyBrake2 = 0;
        profile.KeyShiftUp1 = 0x44;
        profile.KeyShiftDown1 = 0x41;
        profile.KeyCenterSteering1 = 0x2E;
        profile.KeyToggleLock1 = 0x23;
        profile.KeyUnlockCursor2 = null;
        ApplyIRacingEditionDefaults(profile);
        return profile;
    }

    static partial void ApplyIRacingEditionDefaults(Profile profile);

    public static Profile CreateForzaMotorsportDefault(string name = "Forza Motorsport")
    {
        var p = CreateBaseDefault(name);
        p.OutputMode = OutputDeviceMode.VJoy;
        p.VJoyWheelCompatibility = true;
        p.MouseSensitivity = 4.0;
        return p;
    }

    public static Profile CreateForzaHorizonDefault(string name = "Forza Horizon 5")
    {
        var p = CreateBaseDefault(name);
        p.OutputMode = OutputDeviceMode.VJoy;
        p.VJoyWheelCompatibility = true;
        p.MouseSensitivity = 4.0;
        return p;
    }

}
