using SimRacingHub.Models;

namespace SimRacingHub.Services.Telemetry
{
    public interface ITelemetryProvider
    {
        void Update(TelemetryData data);
    }
}
