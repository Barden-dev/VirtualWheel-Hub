using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using SimRacingHub.Core;
using SimRacingHub.Models;

namespace SimRacingHub.Services.Telemetry
{
    public partial class GameDetectionService : ObservableObject
    {
        private readonly GameIntegrationRegistry _registry;
        private CancellationTokenSource? _cts;
        
        public event Action<ProfileContext>? OnContextChanged;
        
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
            var cts = _cts;
            _cts = null;

            if (cts == null) return;
            try
            {
                cts.Cancel();
                cts.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }
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

        private const int ErrorLogThrottleMs = 60_000;
        private int _lastErrorLogTicks;
        private bool _hasLoggedError;

        private void LogThrottled(string message, Exception ex)
        {
            int now = Environment.TickCount;
            if (_hasLoggedError && unchecked(now - _lastErrorLogTicks) < ErrorLogThrottleMs) return;

            _lastErrorLogTicks = now;
            _hasLoggedError = true;
            AppLogger.Instance.LogError(message, ex);
        }

        private void PollingLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    PollOnce();
                }
                catch (Exception ex)
                {
                    LogThrottled("Game auto-detection cycle failed and was skipped", ex);
                }

                Thread.Sleep(2000); // Check every 2 seconds
            }
        }

        private void PollOnce()
        {
            if (!AutoDetectEnabled) return;

            bool gameFound = false;

            foreach (var integration in _registry.GetAll())
            {
                var detector = integration.Detector;

                try
                {
                    detector.Update();
                }
                catch (Exception ex)
                {
                    LogThrottled($"Detector for '{detector.GameName}' failed and was skipped", ex);
                    continue;
                }

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
    }

    public class ProcessGameDetector : IGameDetector
    {
        private readonly string _processName;
        public string GameName { get; }
        public bool IsRunning { get; private set; }

        public ProcessGameDetector(string gameName, string processName)
        {
            GameName = gameName;
            _processName = processName;
        }

        public void Update()
        {
            IsRunning = ProcessUtil.IsRunning(_processName);
        }

        public string? GetCurrentClass() => null; // Fallback detector doesn't know class
        public string? GetCurrentCar() => null;   // Fallback detector doesn't know car
    }
}
