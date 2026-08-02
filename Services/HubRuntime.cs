using System;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using SimRacingHub.Models;
using SimRacingHub.Services.Telemetry;
using SimRacingHub.Input;
using SimRacingHub.Core;

namespace SimRacingHub.Services
{
    public class HubRuntime : IDisposable
    {
        private readonly GameIntegrationRegistry _registry;
        private readonly WindowManager _windowManager;
        
        private RawInputWorker? _rawInputWorker;
        private VJoyWrapper? _vJoy;
        private MathEngine? _mathEngine;
        private readonly AxisState _axisState;
        private readonly TelemetryData _telemetryData;
        private CancellationTokenSource? _cts;
        
        private ResolvedProfile? _currentProfile;
        private bool _lockMouse;
        private Action<int, int, int>? _outputCallback;

        private readonly object _stateLock = new object();
        private IGameIntegration? _activeIntegration;

        private Task? _pollingTask;

        public HubRuntime(GameIntegrationRegistry registry, WindowManager windowManager)
        {
            _registry = registry;
            _windowManager = windowManager;
            _axisState = new AxisState();
            _telemetryData = new TelemetryData();
        }

        public void UpdateRuntimeProfile(ResolvedProfile profile, ProfileContext? context)
        {
            bool wasRunning = _cts != null && !_cts.IsCancellationRequested;

            if (wasRunning)
            {
                _cts?.Cancel();
                try
                {
                    _pollingTask?.Wait();
                }
                catch (AggregateException ex) when (ex.InnerException is OperationCanceledException) { }
                catch (Exception ex)
                {
                    AppLogger.Instance.LogError("Polling task error during profile update", ex);
                }
            }

            lock (_stateLock)
            {
                _currentProfile = profile;
                _currentProfile.EnableTrailBraking = false;
                _mathEngine = new MathEngine(profile, _axisState);
                _activeIntegration = _registry.GetIntegration(context?.Game);
            }

            if (wasRunning)
            {
                _cts = new CancellationTokenSource();
                _pollingTask = Task.Run(() => PollingLoop(_cts.Token));
            }
        }

        public bool Start(ResolvedProfile profile, bool lockMouse, ProfileContext? context, Action<int, int, int>? outputCallback)
        {
            Stop();

            lock (_stateLock)
            {
                _currentProfile = profile;
                _currentProfile.EnableTrailBraking = false;
                _lockMouse = lockMouse;
                _outputCallback = outputCallback;
                _mathEngine = new MathEngine(profile, _axisState);
                _activeIntegration = _registry.GetIntegration(context?.Game);
            }

            try
            {
                _vJoy = new VJoyWrapper(1);
                if (!_vJoy.Acquire())
                {
                    return false;
                }

                _axisState.Reset(-32768.0);
                
                _rawInputWorker = new RawInputWorker();
                _rawInputWorker.OnMouseDelta += OnMouseDelta;
                _rawInputWorker.Start();

                if (_lockMouse)
                {
                    _windowManager.LockCursorToCenter();
                }

                _cts = new CancellationTokenSource();
                _pollingTask = Task.Run(() => PollingLoop(_cts.Token));

                AppLogger.Instance.LogInfo("vWheel Hub started successfully");
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError("Failed to start vWheel Hub", ex);
                return false;
            }
        }

        public void Stop()
        {
            _cts?.Cancel();
            try
            {
                _pollingTask?.Wait();
            }
            catch (AggregateException ex) when (ex.InnerException is OperationCanceledException) { }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError("Polling task error during stop", ex);
            }
            _pollingTask = null;
            _cts = null;

            _rawInputWorker?.Stop();
            _rawInputWorker?.Dispose();
            _rawInputWorker = null;
            
            _windowManager.UnlockCursor();
            
            lock (_stateLock)
            {
                _activeIntegration = null;
            }
            
            if (_vJoy != null)
            {
                _vJoy.UpdateAxesAndButtons(0, -32768, -32768, false, false);
                _vJoy.Release();
                _vJoy.Dispose();
                _vJoy = null;
            }

            _outputCallback?.Invoke(0, -32768, -32768);
        }
        
        public void UpdateMouseLock(bool lockMouse)
        {
            _lockMouse = lockMouse;
            if (_lockMouse) _windowManager.LockCursorToCenter();
            else _windowManager.UnlockCursor();
        }

        public void ReapplyMouseLock()
        {
            if (_lockMouse)
            {
                _windowManager.ReapplyLock();
            }
        }

        private void OnMouseDelta(int deltaX, int deltaY)
        {
            if (_lockMouse)
            {
                _windowManager.ReapplyLock();
            }
            lock (_stateLock)
            {
                if (_mathEngine != null)
                {
                    if (_currentProfile != null && _currentProfile.LockSteerWhenUnlocked && !_lockMouse)
                    {
                        return;
                    }
                    _mathEngine.ProcessSteering(deltaX, _telemetryData);
                }
            }
        }

        private void PollingLoop(CancellationToken token)
        {
            Stopwatch sw = Stopwatch.StartNew();
            
            while (!token.IsCancellationRequested)
            {
                sw.Restart();
                
                ResolvedProfile? profile;
                MathEngine? mathEngine;

                lock (_stateLock)
                {
                    profile = _currentProfile;
                    mathEngine = _mathEngine;
                }

                if (profile == null || mathEngine == null)
                {
                    Thread.Sleep(10);
                    continue;
                }

                bool isSteerLeft = (profile.KeySteerLeft1 != 0 && GlobalKeyboard.IsKeyDown(profile.KeySteerLeft1)) || 
                                   (profile.KeySteerLeft2 != 0 && GlobalKeyboard.IsKeyDown(profile.KeySteerLeft2));
                bool isSteerRight = (profile.KeySteerRight1 != 0 && GlobalKeyboard.IsKeyDown(profile.KeySteerRight1)) || 
                                    (profile.KeySteerRight2 != 0 && GlobalKeyboard.IsKeyDown(profile.KeySteerRight2));

                bool isGasPressed = (profile.KeyGas1 != 0 && GlobalKeyboard.IsKeyDown(profile.KeyGas1)) || 
                                    (profile.KeyGas2 != 0 && GlobalKeyboard.IsKeyDown(profile.KeyGas2));
                bool isBrakePressed = (profile.KeyBrake1 != 0 && GlobalKeyboard.IsKeyDown(profile.KeyBrake1)) || 
                                      (profile.KeyBrake2 != 0 && GlobalKeyboard.IsKeyDown(profile.KeyBrake2));

                bool isTurboGas = (profile.KeyGas1 != 0 && GlobalKeyboard.IsKeyDown(profile.KeyGas1)) && 
                                  (profile.KeyGas2 != 0 && GlobalKeyboard.IsKeyDown(profile.KeyGas2));
                bool isTurboBrake = (profile.KeyBrake1 != 0 && GlobalKeyboard.IsKeyDown(profile.KeyBrake1)) && 
                                    (profile.KeyBrake2 != 0 && GlobalKeyboard.IsKeyDown(profile.KeyBrake2));

                bool centerSteer = (profile.KeyCenterSteering1 != 0 && GlobalKeyboard.IsKeyDown(profile.KeyCenterSteering1)) || 
                                   (profile.KeyCenterSteering2 != 0 && GlobalKeyboard.IsKeyDown(profile.KeyCenterSteering2));
                
                bool shiftDown = (profile.KeyShiftDown1 != 0 && GlobalKeyboard.IsKeyDown(profile.KeyShiftDown1)) || 
                                 (profile.KeyShiftDown2 != 0 && GlobalKeyboard.IsKeyDown(profile.KeyShiftDown2));
                bool shiftUp = (profile.KeyShiftUp1 != 0 && GlobalKeyboard.IsKeyDown(profile.KeyShiftUp1)) || 
                               (profile.KeyShiftUp2 != 0 && GlobalKeyboard.IsKeyDown(profile.KeyShiftUp2));

                int steer, gas, brake;
                lock (_stateLock)
                {
                    if (profile.UseKeyboardOnlyMode)
                    {
                        double dtSeconds = 1.0 / (profile.PollingRate > 0 ? profile.PollingRate : 1000);
                        mathEngine.UpdateKeyboardSteering(isSteerLeft, isSteerRight, dtSeconds);
                    }

                    if (centerSteer)
                    {
                        mathEngine.CenterSteering();
                    }

                    mathEngine.UpdatePedals(isGasPressed, isBrakePressed, isTurboGas, isTurboBrake, _telemetryData);
                    
                    var outputs = mathEngine.GetMappedOutputs(_telemetryData);
                    steer = outputs.steer;
                    gas = outputs.gas;
                    brake = outputs.brake;
                }
                
                if (_vJoy != null)
                {
                    _vJoy.UpdateAxesAndButtons(steer, gas, brake, shiftDown, shiftUp);
                }

                _outputCallback?.Invoke(steer, gas, brake);

                int elapsed = (int)sw.ElapsedMilliseconds;
                int targetSleepTime = 1000 / (profile.PollingRate > 0 ? profile.PollingRate : 1000);
                int sleepTime = targetSleepTime - elapsed;
                
                if (sleepTime > 0)
                {
                    Thread.Sleep(sleepTime);
                }
            }
        }
        
        public void Dispose()
        {
            Stop();
        }
    }
}
