using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using SimRacingHub.Core;
using SimRacingHub.Models;

namespace SimRacingHub.Services.Games
{
    public partial class GameDetectionService : ObservableObject
    {
        private readonly GameIntegrationRegistry _registry;
        private CancellationTokenSource? _cts;
        private Task? _worker;
        private readonly object _pollLock = new();
        public ProfileContext CurrentContext => _lastContext;

        public event Action<ProfileContext>? OnContextChanged;
        public event Action<string>? OnGameRunning;
        public event Action<GameVehicleStatus>? OnVehicleStatusChanged;
        public GameVehicleStatus VehicleStatus { get; private set; } = new(null, VehicleIdentityState.NotRunning);
        private ProfileContext _lastContext = new ProfileContext("Universal");
        [ObservableProperty]
        private bool _autoDetectEnabled = true;
        public GameDetectionService(GameIntegrationRegistry registry)
        {
            _registry = registry;
        }

        public void Start()
        {
            if (_cts != null && !_cts.IsCancellationRequested)
                return;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _worker = Task.Run(() => PollingLoop(token));
        }

        public void Stop()
        {
            var cts = _cts;
            _cts = null;
            if (cts == null)
                return;
            try
            {
                cts.Cancel();
                if (_worker != null && Task.CurrentId != _worker.Id)
                    _worker.Wait(TimeSpan.FromSeconds(3));
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
            if (_hasLoggedError && unchecked(now - _lastErrorLogTicks) < ErrorLogThrottleMs)
                return;
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

                if (token.WaitHandle.WaitOne(2000))
                    break;
            }
        }

        private void PollOnce()
        {
            lock (_pollLock)
                PollCore();
        }

        public void CheckNow() => PollOnce();
        private void PollCore()
        {
            if (!AutoDetectEnabled) return;

            ProfileContext? detectedContext = null;
            var vehicleStatus = new GameVehicleStatus(null, VehicleIdentityState.NotRunning);

            foreach (var integration in _registry.GetAll())
            {
                var detector = integration.Detector;
                lock (detector)
                {

                try
                {
                    detector.Update();
                }
                catch (Exception ex)
                {
                    LogThrottled($"Detector for '{detector.GameName}' failed and was skipped", ex);
                    continue;
                }

                if (detector.IsRunning) OnGameRunning?.Invoke(detector.GameName);

                if (detector.IsRunning && detectedContext == null)
                {
                    bool supported = detector.SupportsVehicleIdentification;
                    bool confirmed = supported && detector.HasCurrentVehicleIdentity;
                    string? car = confirmed || !supported ? detector.GetCurrentCar() : null;
                    string? carClass = confirmed || !supported ? detector.GetCurrentClass() : null;
                    vehicleStatus = new(detector.GameName, !supported ? VehicleIdentityState.Unsupported :
                        confirmed ? VehicleIdentityState.Confirmed : VehicleIdentityState.Waiting, car, carClass);
                    // Missing identity must not discard the last settings for this running game.
                    detectedContext = supported && !confirmed && _lastContext.Game == detector.GameName
                        ? new ProfileContext(_lastContext.Game, _lastContext.CarClass, _lastContext.Car)
                        : new ProfileContext(detector.GameName, carClass, car);
                }
                }
            }

            // Poll every detector even after selecting the first running game. A previously
            // active Linux integration must observe process exit and release its bridge socket
            // when switching to a game earlier in registry order.
            detectedContext ??= new ProfileContext("Universal");
            if (!detectedContext.Equals(_lastContext))
            {
                _lastContext = detectedContext;
                OnContextChanged?.Invoke(_lastContext);
            }
            if (VehicleStatus != vehicleStatus)
            {
                VehicleStatus = vehicleStatus;
                OnVehicleStatusChanged?.Invoke(vehicleStatus);
            }
        }

    }

    public class ProcessGameDetector : IGameDetector
    {
        private readonly string[] _processNames;
        public bool? IsForeground => GameFocus.Matches(_processNames);
        public string GameName { get; }
        public bool SupportsVehicleIdentification => false;
        public bool IsRunning { get; private set; }

        public ProcessGameDetector(string gameName, string processName) : this(gameName, new[] { processName })
        {
        }

        public ProcessGameDetector(string gameName, params string[] processNames)
        {
            GameName = gameName;
            _processNames = processNames ?? Array.Empty<string>();
        }

        public void Update()
        {
            IsRunning = _processNames.Any(p => ProcessUtil.IsRunning(p));
        }

        public string? GetCurrentClass() => null;
        public string? GetCurrentCar() => null;
    }
}
