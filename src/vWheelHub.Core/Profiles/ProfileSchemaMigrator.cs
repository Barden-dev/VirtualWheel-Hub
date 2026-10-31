using System;
using SimRacingHub.Models;

namespace SimRacingHub.Models;

/// <summary>One-time, future-schema-safe conversion of persisted profile coefficients.</summary>
public static partial class ProfileSchemaMigrator
{
    /// <summary>
    /// Upgrades legacy per-iteration pedal coefficients to units per second.
    /// The transitional dt build stored its tuning rate in PedalReferenceRate;
    /// older builds used the profile PollingRate.
    /// </summary>
    public static bool Migrate(Profile profile, int fallbackRate = 1000)
    {
        if (profile.SchemaVersion > Profile.CurrentSchemaVersion)
        {
            // A newer application may have introduced a different unit
            // system. Never reinterpret an unknown future profile here.
            return false;
        }

        if (profile.SchemaVersion == Profile.CurrentSchemaVersion)
        {
            bool removedLegacyField = profile.PedalReferenceRate.HasValue;
            profile.PedalReferenceRate = null;
            return removedLegacyField;
        }

        int referenceRate = Math.Clamp(profile.PedalReferenceRate ?? profile.PollingRate ?? fallbackRate, 50, 1000);
        ScalePedalRates(profile, referenceRate);
        profile.SchemaVersion = Profile.CurrentSchemaVersion;
        profile.PedalReferenceRate = null;
        return true;
    }

    private static double? ScaleRate(double? value, double factor)
    {
        return value.HasValue ? value.Value * factor : null;
    }

}
