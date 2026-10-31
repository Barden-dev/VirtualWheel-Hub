namespace SimRacingHub.Services.Games
{
    public interface IGameDetector
    {
        string GameName { get; }

        bool IsRunning { get; }
        bool? IsForeground => null;

        // New integrations inherit waiting/confirmed reporting; process-only detectors opt out.
        bool SupportsVehicleIdentification => true;
        bool HasCurrentVehicleIdentity => !string.IsNullOrWhiteSpace(GetCurrentCar());
        string? GetCurrentClass();
        string? GetCurrentCar();
        void Update();
    }
}
