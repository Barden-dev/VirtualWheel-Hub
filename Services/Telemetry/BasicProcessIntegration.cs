using System.Collections.Generic;
using SimRacingHub.Models;

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
        
        public ITelemetryProvider CreateTelemetryProvider()
        {
            return new NullTelemetryProvider();
        }
        
        public IReadOnlyCollection<FeatureDescriptor> Features => new List<FeatureDescriptor>();
    }
    
    public class NullTelemetryProvider : ITelemetryProvider
    {
        public void Update(TelemetryData data) { }
    }
}
