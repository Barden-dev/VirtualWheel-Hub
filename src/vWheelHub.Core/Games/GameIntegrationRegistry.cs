using System;
using System.Collections.Generic;
using System.Linq;

namespace SimRacingHub.Services.Games
{
    public class GameIntegrationRegistry : IDisposable
    {
        private readonly List<IGameIntegration> _integrations = new List<IGameIntegration>();
        public GameIntegrationRegistry() : this(true)
        {
        }

        public GameIntegrationRegistry(bool registerDefaults)
        {
            if (registerDefaults)
            {
                _integrations.Add(new BasicProcessIntegration("Le Mans Ultimate", "Le Mans Ultimate"));
                _integrations.Add(new BasicProcessIntegration("iRacing", "iRacingSim64DX11"));
                _integrations.Add(new BasicProcessIntegration("Assetto Corsa", "acs"));
                _integrations.Add(new BasicProcessIntegration("Assetto Corsa Competizione", "AC2-Win64-Shipping"));
                _integrations.Add(new BasicProcessIntegration("Assetto Corsa EVO", "AssettoCorsaEVO"));
                _integrations.Add(new BasicProcessIntegration("Automobilista 2", "AMS2AVX", "AMS2", "AMS2DemoAVX", "AMS2Demo"));
                _integrations.Add(new BasicProcessIntegration("BeamNG.drive", "BeamNG.drive.x64"));
                _integrations.Add(new BasicProcessIntegration("Dirt Rally 2.0", "dirtrally2"));
                _integrations.Add(new BasicProcessIntegration("EA WRC", "WRC"));
                _integrations.Add(new BasicProcessIntegration("Richard Burns Rally", "RichardBurnsRally_SSE"));
                _integrations.Add(new BasicProcessIntegration("WRC Generations", "WRCG"));
                _integrations.Add(new BasicProcessIntegration("Forza Motorsport", "forza_steamworks_release_final", "ForzaMotorsport"));
                _integrations.Add(new BasicProcessIntegration("Forza Horizon 5", "ForzaHorizon5"));
                _integrations.Add(new BasicProcessIntegration("Forza Horizon 4", "ForzaHorizon4"));
            }
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
            if (string.IsNullOrEmpty(gameId))
                return null;
            return _integrations.FirstOrDefault(i => string.Equals(i.GameId, gameId, StringComparison.OrdinalIgnoreCase));
        }

        public IEnumerable<IGameIntegration> GetAll() => _integrations;
        public void Dispose()
        {
            foreach (var integration in _integrations)
            {
                if (integration is IDisposable resource)
                    resource.Dispose();
                else if (integration.Detector is IDisposable detector)
                    detector.Dispose();
            }
        }
    }
}
