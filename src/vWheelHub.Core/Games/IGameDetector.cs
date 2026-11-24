namespace SimRacingHub.Services.Games;

public partial interface IGameDetector
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
