namespace SimRacingHub.Services.Telemetry
{
    public interface IGameDetector
    {
        string GameName { get; }
        bool IsRunning { get; }
        string? GetCurrentClass();
        string? GetCurrentCar();
        void Update();
    }
}
