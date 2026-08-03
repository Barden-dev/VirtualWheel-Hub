using System.Collections.Generic;
namespace SimRacingHub.Services.Telemetry
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

        public string GameId => _gameId;
        public string DisplayName => _gameId;
        
        public IGameDetector Detector => _detector;
        
        public IReadOnlyCollection<FeatureDescriptor> Features => new List<FeatureDescriptor>();
    }
}
