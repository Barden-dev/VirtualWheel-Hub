using System;
using SimRacingHub.Models;

namespace SimRacingHub.Models;

public static partial class ProfileSchemaMigrator
{
    internal static void ScalePedalRates(Profile profile, double factor)
    {
        profile.TbDecay = ScaleRate(profile.TbDecay, factor);
        profile.GasAttackTurbo = ScaleRate(profile.GasAttackTurbo, factor);
        profile.GasAttackFast = ScaleRate(profile.GasAttackFast, factor);
        profile.GasAttackSlow = ScaleRate(profile.GasAttackSlow, factor);
        profile.GasDecay = ScaleRate(profile.GasDecay, factor);
        profile.GasInstantCut = ScaleRate(profile.GasInstantCut, factor);
        profile.BrakeAttackFast = ScaleRate(profile.BrakeAttackFast, factor);
        profile.BrakeAttackSlow = ScaleRate(profile.BrakeAttackSlow, factor);
        profile.BrakeDecay = ScaleRate(profile.BrakeDecay, factor);
    }

}
