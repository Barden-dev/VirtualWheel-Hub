using System;

namespace SimRacingHub.Services.Telemetry
{
    [Flags]
    public enum TelemetryCapabilities
    {
        None = 0,
        Abs = 1 << 0,
        Tc = 1 << 1,
        SlipAudio = 1 << 2,
        SpeedSens = 1 << 3,
        All = Abs | Tc | SlipAudio | SpeedSens
    }
}
