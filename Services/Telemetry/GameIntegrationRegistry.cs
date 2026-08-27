using System;
using System.Collections.Generic;
using System.Linq;

namespace SimRacingHub.Services.Telemetry
{
    public class GameIntegrationRegistry
    {
        private readonly List<IGameIntegration> _integrations = new List<IGameIntegration>();
        
        public GameIntegrationRegistry()
        {
            _integrations.Add(new BasicProcessIntegration("Le Mans Ultimate", "Le Mans Ultimate"));
            _integrations.Add(new BasicProcessIntegration("iRacing", "iRacingSim64DX11"));
            _integrations.Add(new BasicProcessIntegration("Assetto Corsa Competizione", "AC2-Win64-Shipping"));
            _integrations.Add(new BasicProcessIntegration("Assetto Corsa EVO", "AssettoCorsaEVO"));
            _integrations.Add(new BasicProcessIntegration("BeamNG.drive", "BeamNG.drive.x64"));
            _integrations.Add(new BasicProcessIntegration("Assetto Corsa", "acs"));
            _integrations.Add(new BasicProcessIntegration("Dirt Rally 2.0", "dirtrally2"));
            _integrations.Add(new BasicProcessIntegration("EA WRC", "WRC"));
            _integrations.Add(new BasicProcessIntegration("Richard Burns Rally", "RichardBurnsRally_SSE"));
        }

        public void Register(IGameIntegration integration)
        {
            if (integration != null)
            {
                _integrations.Add(integration);
            }
        }

        public IGameIntegration? GetIntegration(string? gameId)
        {
            if (string.IsNullOrEmpty(gameId)) return null;
            return _integrations.FirstOrDefault(i => string.Equals(i.GameId, gameId, StringComparison.OrdinalIgnoreCase));
        }

        public IEnumerable<IGameIntegration> GetAll() => _integrations;
    }
}
