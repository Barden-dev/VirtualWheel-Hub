using System.Collections.Generic;
using SimRacingHub.Models;

namespace SimRacingHub.Services.Games
{
    public class BasicProcessIntegration : IGameIntegration
    {
        private readonly ProcessGameDetector _detector;
        private readonly string _gameId;
        public BasicProcessIntegration(string gameId, string processName)
        {
            _gameId = gameId;
            _detector = new ProcessGameDetector(gameId, processName);
        }

        public BasicProcessIntegration(string gameId, params string[] processNames)
        {
            _gameId = gameId;
            _detector = new ProcessGameDetector(gameId, processNames);
        }

        public string GameId => _gameId;
        public string DisplayName => _gameId;
        public IGameDetector Detector => _detector;
    }
}
