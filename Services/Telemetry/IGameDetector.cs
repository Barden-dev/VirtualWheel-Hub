namespace SimRacingHub.Services.Telemetry
{
    public interface IGameDetector
    {
        string GameName { get; }
        bool IsRunning { get; }
        TelemetryCapabilities Capabilities { get; }
        string GetCurrentClass();
        string GetCurrentCar();
        void Update();
    }
}
