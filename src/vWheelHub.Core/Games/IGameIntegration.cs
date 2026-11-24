namespace SimRacingHub.Services.Games;

public partial interface IGameIntegration
{
    string GameId { get; }
    string DisplayName { get; }
    IGameDetector Detector { get; }
}
