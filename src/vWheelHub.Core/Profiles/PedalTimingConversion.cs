using System;
using SimRacingHub.Models;

namespace SimRacingHub.Models;

/// <summary>Pure conversions between pedal rates and UI durations; rounded and exact paths remain distinct.</summary>
public static class PedalTimingConversion
{
    public const double PedalAxisRange = 65536.0;
    public static double? ToSeconds(double? unitsPerSecond)
    {
        return unitsPerSecond is > 0 ? RoundUiSeconds(PedalAxisRange / unitsPerSecond.Value) : null;
    }

    public static double? ToSegmentSeconds(double? unitsPerSecond, double? segmentFraction)
    {
        return unitsPerSecond is > 0 && segmentFraction is > 0
            ? RoundUiSeconds(PedalAxisRange * segmentFraction.Value / unitsPerSecond.Value)
            : null;
    }

    public static double? ExactSegmentSeconds(double? unitsPerSecond, double? segmentFraction) =>
        unitsPerSecond is > 0 && segmentFraction is > 0
            ? PedalAxisRange * segmentFraction.Value / unitsPerSecond.Value : null;

    public static double RoundUiSeconds(double seconds)
    {
        // Millisecond precision is more than enough for pedal tuning and
        // keeps NumberBox values readable (0.079 instead of 0.078722).
        return Math.Round(seconds, 3, MidpointRounding.AwayFromZero);
    }

    public static double? FromSegmentSeconds(double? seconds, double? segmentFraction)
    {
        return seconds is > 0 && segmentFraction is > 0
            ? PedalAxisRange * segmentFraction.Value / seconds.Value
            : null;
    }

    public static double SegmentDuration(double? unitsPerSecond, double? segmentFraction)
    {
        return ToSegmentSeconds(unitsPerSecond, segmentFraction) ?? 0.0;
    }

    public static double? FromSeconds(double? seconds)
    {
        return seconds is > 0 ? PedalAxisRange / seconds.Value : null;
    }

}
