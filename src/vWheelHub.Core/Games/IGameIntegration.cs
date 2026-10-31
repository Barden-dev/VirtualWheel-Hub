using System.Collections.Generic;

namespace SimRacingHub.Services.Games
{
    public interface IGameIntegration
    {
        string GameId { get; }

        string DisplayName { get; }

        IGameDetector Detector { get; }

    }

}
