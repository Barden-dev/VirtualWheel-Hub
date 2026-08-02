using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using SimRacingHub.Models;

namespace SimRacingHub.Services.Telemetry
{
    public partial class GameDetectionService : ObservableObject
    {
        private readonly GameIntegrationRegistry _registry;
        private CancellationTokenSource _cts;
        
        public event Action<ProfileContext> OnContextChanged;
        
        private ProfileContext _lastContext = new ProfileContext("Universal");
        
        [ObservableProperty]
        private bool _autoDetectEnabled = true;

        public GameDetectionService(GameIntegrationRegistry registry)
        {
            _registry = registry;
        }

        public void Start()
        {
            if (_cts != null && !_cts.IsCancellationRequested) return;
            _cts = new CancellationTokenSource();
            Task.Run(() => PollingLoop(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
            _cts = null;
        }

        partial void OnAutoDetectEnabledChanged(bool value)
        {
            if (value)
            {
                Start();
            }
            else
            {
                Stop();
            }
        }

        private void PollingLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (AutoDetectEnabled)
                {
                    bool gameFound = false;
                    
                    foreach (var integration in _registry.GetAll())
                    {
                        var detector = integration.Detector;
                        detector.Update();
                        
                        if (detector.IsRunning)
                        {
                            var newContext = new ProfileContext(
                                detector.GameName,
                                detector.GetCurrentClass(),
                                detector.GetCurrentCar()
                            );
                            
                            if (!newContext.Equals(_lastContext))
                            {
                                _lastContext = newContext;
                                OnContextChanged?.Invoke(newContext);
                            }
                            
                            gameFound = true;
                            break; // Stop after finding the first running game
                        }
                    }
                    
                    if (!gameFound && !string.Equals(_lastContext.Game, "Universal", StringComparison.OrdinalIgnoreCase))
                    {
                        // Game exited, revert to Universal
                        _lastContext = new ProfileContext("Universal");
                        OnContextChanged?.Invoke(_lastContext);
                    }
                }
                
                Thread.Sleep(2000); // Check every 2 seconds
            }
        }

        public TelemetryCapabilities GetCapabilities(string gameName)
        {
            if (string.IsNullOrEmpty(gameName)) return TelemetryCapabilities.None;
            var integration = _registry.GetIntegration(gameName);
            return integration?.Detector.Capabilities ?? TelemetryCapabilities.None;
        }
    }

    public class ProcessGameDetector : IGameDetector
    {
        private readonly string _processName;
        public string GameName { get; }
        public bool IsRunning { get; private set; }
        public TelemetryCapabilities Capabilities => TelemetryCapabilities.None;

        public ProcessGameDetector(string gameName, string processName)
        {
            GameName = gameName;
            _processName = processName;
        }

        public void Update()
        {
            IsRunning = Process.GetProcessesByName(_processName).Length > 0;
        }

        public string GetCurrentClass() => null; // Fallback detector doesn't know class
        public string GetCurrentCar() => null;   // Fallback detector doesn't know car
    }
}
