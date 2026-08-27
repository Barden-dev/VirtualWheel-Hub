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
        private const int MaxConsecutiveErrors = 30;

        private const int MaxVJoyFailuresBeforeReacquire = 10;

        private const int PollingTaskStopTimeoutMs = 2000;
        private const int IdleSleepMs = 10;
        private const int ErrorLogIntervalMs = 5000;

        private readonly GameIntegrationRegistry _registry;
        private readonly WindowManager _windowManager;

        private RawInputWorker? _rawInputWorker;
        private VJoyWrapper? _vJoy;
        private MathEngine? _mathEngine;
        private readonly AxisState _axisState;

        private CancellationTokenSource? _cts;

        private ResolvedProfile? _currentProfile;
        private bool _lockMouse;
        private bool _holdMouseUnlocked;
        private Action<int, int, int>? _outputCallback;

        private readonly object _stateLock = new object();
        private IGameIntegration? _activeIntegration;

        private Task? _pollingTask;
        private Task? _vJoyTask;

        private int _latestSteer;
        private int _latestGas = -32768;
        private int _latestBrake = -32768;
        private int _latestShiftDown;
        private int _latestShiftUp;
        private volatile bool _neutralizeRequested;

        private int _consecutiveErrors;
        private long _lastLoopErrorLog;
        private volatile bool _faultStop;
        private bool _wasGameRunning;
        private long _lastDetectorCheckTicks;

        public event Action<bool>? OnMouseLockStateChanged;

        public event Action<string>? OnFault;

        public HubRuntime(GameIntegrationRegistry registry, WindowManager windowManager)
        {
            _registry = registry;
            _windowManager = windowManager;
            _axisState = new AxisState();

        }

        public void UpdateRuntimeProfile(ResolvedProfile profile, ProfileContext? context)
        {
            bool wasRunning = _cts != null && !_cts.IsCancellationRequested;

            if (wasRunning)
            {
                StopPollingTask("profile update");
            }

            lock (_stateLock)
            {
                _currentProfile = profile;
                _currentProfile.EnableTrailBraking = false;
                _mathEngine = new MathEngine(profile, _axisState);
                _activeIntegration = _registry.GetIntegration(context?.Game);
                _wasGameRunning = _activeIntegration?.Detector?.IsRunning ?? false;
            }

            if (wasRunning)
            {
                _cts = new CancellationTokenSource();
                _faultStop = false;
                CancellationToken token = _cts.Token;
                _pollingTask = StartRuntimeTask(() => PollingLoop(token), token);
                _vJoyTask = StartRuntimeTask(() => VJoyLoop(token), token);
            }
        }

        private void StopPollingTask(string reason)
        {
            var task = _pollingTask;
            var vJoyTask = _vJoyTask;
            var cts = _cts;

            try { cts?.Cancel(); }
            catch (Exception ex) { AppLogger.Instance.LogError($"Could not cancel the polling loop ({reason})", ex); }

            if (task != null)
            {
                try
                {
                    if (!task.Wait(PollingTaskStopTimeoutMs))
                    {
                        AppLogger.Instance.LogWarning($"Polling task did not stop within {PollingTaskStopTimeoutMs} ms ({reason})");
                    }
                }
                catch (AggregateException ex) when (ex.InnerException is OperationCanceledException) { }
                catch (Exception ex) { AppLogger.Instance.LogError($"Exception while waiting for polling task ({reason})", ex); }
            }

            if (vJoyTask != null)
            {
                try
                {
                    if (!vJoyTask.Wait(PollingTaskStopTimeoutMs))
                    {
                        AppLogger.Instance.LogWarning($"Runtime task did not stop within {PollingTaskStopTimeoutMs} ms ({reason})");
                    }
                }
                catch (AggregateException ex) when (ex.InnerException is OperationCanceledException) { }
                catch (Exception ex) { AppLogger.Instance.LogError($"Runtime task error during {reason}", ex); }
            }

            _pollingTask = null;
            _vJoyTask = null;
            cts?.Dispose();
            _cts = null;
        }

        public bool Start(ResolvedProfile profile, bool lockMouse, ProfileContext? context, Action<int, int, int>? outputCallback)
        {
            Stop();

            if (profile == null)
            {
                AppLogger.Instance.LogError("Cannot start vWheel Hub: resolved profile is null");
                return false;
            }

            try
            {
                lock (_stateLock)
                {
                    _currentProfile = profile;
                    _currentProfile.EnableTrailBraking = false;
                    _lockMouse = lockMouse;
                    _outputCallback = outputCallback;
                    _mathEngine = new MathEngine(profile, _axisState);
                    _activeIntegration = _registry.GetIntegration(context?.Game);
                }

                _vJoy = new VJoyWrapper(1);
                if (!_vJoy.Acquire())
                {
                    _vJoy.Dispose();
                    _vJoy = null;
                    return false;
                }

                _axisState.Reset(-32768.0);
                Volatile.Write(ref _latestSteer, 0);
                Volatile.Write(ref _latestGas, -32768);
                Volatile.Write(ref _latestBrake, -32768);
                Volatile.Write(ref _latestShiftDown, 0);
                Volatile.Write(ref _latestShiftUp, 0);
                _neutralizeRequested = false;

                _rawInputWorker = new RawInputWorker();
                if (!_rawInputWorker.Start())
                {
                    AppLogger.Instance.LogError("Mouse capture could not be initialised, vWheel Hub was not started");

                    _rawInputWorker.Dispose();
                    _rawInputWorker = null;

                    _vJoy.Release();
                    _vJoy.Dispose();
                    _vJoy = null;
                    return false;
                }

                if (_lockMouse)
                {
                    _windowManager.LockCursorAtCurrentPosition();
                }

                NativeTimer.BeginPeriod(1);
                _cts = new CancellationTokenSource();
                _faultStop = false;
                CancellationToken token = _cts.Token;
                _pollingTask = StartRuntimeTask(() => PollingLoop(token), token);
                _vJoyTask = StartRuntimeTask(() => VJoyLoop(token), token);

                AppLogger.Instance.LogInfo("vWheel Hub started successfully");
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError("Failed to start vWheel Hub", ex);
                try { Stop(); }
                catch (Exception cleanupEx) { AppLogger.Instance.LogError("Failed to clean up an incomplete hub start", cleanupEx); }
                return false;
            }
        }

        public void Stop()
        {
            StopPollingTask("stop");
            NativeTimer.EndPeriod(1);

            _rawInputWorker?.Stop();
            _rawInputWorker?.Dispose();
            _rawInputWorker = null;

            _windowManager.UnlockCursor();
            _holdMouseUnlocked = false;

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
            _holdMouseUnlocked = false;
            if (_lockMouse) _windowManager.LockCursorAtCurrentPosition();
            else _windowManager.UnlockCursor();
            OnMouseLockStateChanged?.Invoke(_lockMouse);
        }

        public void ReapplyMouseLock()
        {
            if (_lockMouse)
            {
                _windowManager.ReapplyLock();
            }
        }

        private static Task StartRuntimeTask(Action action, CancellationToken token)
        {
            return Task.Factory.StartNew(action, token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        private void PollingLoop(CancellationToken token)
        {
            long nextDeadlineTicks = 0;
            using var paceWaiter = NativeTimer.CreateHighResolutionWaiter();
            long previousIterationTicks = 0;
            _consecutiveErrors = 0;
            long idleTargetTicks = (long)(Stopwatch.Frequency * (IdleSleepMs / 1000.0));

            AppLogger.Instance.LogInfo("Polling loop started", showInStatusBar: false);

            try
            {
                while (!token.IsCancellationRequested && !_faultStop)
                {
                    long iterationTicks = Stopwatch.GetTimestamp();
                    double actualDtSeconds = previousIterationTicks == 0
                        ? 0.0
                        : (iterationTicks - previousIterationTicks) / (double)Stopwatch.Frequency;
                    previousIterationTicks = iterationTicks;
                    long targetTicks = idleTargetTicks;

                    try
                    {
                        IGameIntegration? integration;
                        lock (_stateLock)
                        {
                            integration = _activeIntegration;
                        }

                        if (integration?.Detector != null)
                        {
                            long now = Stopwatch.GetTimestamp();
                            if (now - _lastDetectorCheckTicks > (Stopwatch.Frequency / 4))
                            {
                                _lastDetectorCheckTicks = now;
                                integration.Detector.Update();
                                bool isRunning = integration.Detector.IsRunning;
                                if (_wasGameRunning && !isRunning)
                                {
                                    if (_lockMouse)
                                    {
                                        _windowManager.UnlockCursor();
                                        _lockMouse = false;
                                        _holdMouseUnlocked = false;
                                        OnMouseLockStateChanged?.Invoke(false);
                                        AppLogger.Instance.LogInfo("Game process closed — mouse cursor unlocked.");
                                    }
                                }
                                _wasGameRunning = isRunning;
                            }
                        }

                        targetTicks = RunIteration(actualDtSeconds);
                        _consecutiveErrors = 0;
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _consecutiveErrors++;
                        LogIterationErrorThrottled(ex);

                        Neutralize();

                        if (_consecutiveErrors > MaxConsecutiveErrors)
                        {
                            RaiseFault($"Runtime error, hub stopped: {ex.Message}");
                        }
                    }
                    finally
                    {
                        PaceIteration(ref nextDeadlineTicks, targetTicks, paceWaiter);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError("Polling loop terminated unexpectedly", ex);
                RaiseFault($"Runtime loop terminated: {ex.Message}");
            }
            finally
            {
                AppLogger.Instance.LogInfo("Polling loop finished", showInStatusBar: false);
            }
        }

        private long RunIteration(double actualDtSeconds)
        {
            ResolvedProfile? profile;
            MathEngine? mathEngine;

            lock (_stateLock)
            {
                profile = _currentProfile;
                mathEngine = _mathEngine;
            }

            if (profile == null || mathEngine == null)
            {
                return (long)(Stopwatch.Frequency * (IdleSleepMs / 1000.0));
            }

            int rate = Math.Clamp(profile.PollingRate > 0 ? profile.PollingRate : 1000, 50, 1000);
            long targetTicks = (long)(Stopwatch.Frequency / (double)rate);
            double nominalDtSeconds = 1.0 / rate;
            double dtSeconds = !double.IsFinite(actualDtSeconds) || actualDtSeconds <= 0.0
                ? nominalDtSeconds
                : actualDtSeconds;

            bool isGasPressed = (profile.KeyGas1 != 0 && GlobalKeyboard.IsKeyDown(profile.KeyGas1)) ||
                                (profile.KeyGas2 != 0 && GlobalKeyboard.IsKeyDown(profile.KeyGas2));
            bool isBrakePressed = (profile.KeyBrake1 != 0 && GlobalKeyboard.IsKeyDown(profile.KeyBrake1)) ||
                                  (profile.KeyBrake2 != 0 && GlobalKeyboard.IsKeyDown(profile.KeyBrake2));

            bool isTurboGas = (profile.KeyGas1 != 0 && GlobalKeyboard.IsKeyDown(profile.KeyGas1)) &&
                              (profile.KeyGas2 != 0 && GlobalKeyboard.IsKeyDown(profile.KeyGas2));
            bool isTurboBrake = (profile.KeyBrake1 != 0 && GlobalKeyboard.IsKeyDown(profile.KeyBrake1)) &&
                                (profile.KeyBrake2 != 0 && GlobalKeyboard.IsKeyDown(profile.KeyBrake2));

            bool shiftDown = (profile.KeyShiftDown1 != 0 && GlobalKeyboard.IsKeyDown(profile.KeyShiftDown1)) ||
                             (profile.KeyShiftDown2 != 0 && GlobalKeyboard.IsKeyDown(profile.KeyShiftDown2));
            bool shiftUp = (profile.KeyShiftUp1 != 0 && GlobalKeyboard.IsKeyDown(profile.KeyShiftUp1)) ||
                           (profile.KeyShiftUp2 != 0 && GlobalKeyboard.IsKeyDown(profile.KeyShiftUp2));

            int gas, brake;
            lock (_stateLock)
            {
                mathEngine.UpdatePedals(isGasPressed, isBrakePressed, isTurboGas, isTurboBrake, dtSeconds);

                var outputs = mathEngine.GetMappedOutputs();
                gas = outputs.gas;
                brake = outputs.brake;
            }

            Volatile.Write(ref _latestGas, gas);
            Volatile.Write(ref _latestBrake, brake);
            Volatile.Write(ref _latestShiftDown, shiftDown ? 1 : 0);
            Volatile.Write(ref _latestShiftUp, shiftUp ? 1 : 0);
            return targetTicks;
        }

        private void VJoyLoop(CancellationToken token)
        {
            var rawInput = _rawInputWorker;
            if (rawInput == null) return;
            long previousTicks = Stopwatch.GetTimestamp();
            long nextDeadlineTicks = 0;
            using var paceWaiter = NativeTimer.CreateHighResolutionWaiter();

            try
            {
                while (!token.IsCancellationRequested && !_faultStop)
                {
                    ResolvedProfile? profile;
                    MathEngine? mathEngine;
                    lock (_stateLock)
                    {
                        profile = _currentProfile;
                        mathEngine = _mathEngine;
                    }

                    int rate = Math.Clamp(profile?.PollingRate > 0 ? profile.PollingRate : 1000, 50, 1000);
                    long targetTicks = Math.Max(1, (long)(Stopwatch.Frequency / (double)rate));

                    long nowTicks = Stopwatch.GetTimestamp();
                    double dtSeconds = (nowTicks - previousTicks) / (double)Stopwatch.Frequency;
                    previousTicks = nowTicks;
                    dtSeconds = Math.Clamp(dtSeconds, 0.00025, 0.02);

                    int deltaX = rawInput.ConsumeDeltaX();
                    if (profile != null && mathEngine != null)
                    {
                        bool centerSteer = (profile.KeyCenterSteering1 != 0 && GlobalKeyboard.IsKeyDown(profile.KeyCenterSteering1)) ||
                                           (profile.KeyCenterSteering2 != 0 && GlobalKeyboard.IsKeyDown(profile.KeyCenterSteering2));
                        bool isSteerLeft = (profile.KeySteerLeft1 != 0 && GlobalKeyboard.IsKeyDown(profile.KeySteerLeft1)) ||
                                           (profile.KeySteerLeft2 != 0 && GlobalKeyboard.IsKeyDown(profile.KeySteerLeft2));
                        bool isSteerRight = (profile.KeySteerRight1 != 0 && GlobalKeyboard.IsKeyDown(profile.KeySteerRight1)) ||
                                            (profile.KeySteerRight2 != 0 && GlobalKeyboard.IsKeyDown(profile.KeySteerRight2));

                        bool isHoldUnlock = (profile.KeyUnlockCursor1 != 0 && GlobalKeyboard.IsKeyDown(profile.KeyUnlockCursor1)) ||
                                            (profile.KeyUnlockCursor2 != 0 && GlobalKeyboard.IsKeyDown(profile.KeyUnlockCursor2));

                        if (isHoldUnlock)
                        {
                            if (_lockMouse && !_holdMouseUnlocked)
                            {
                                _windowManager.UnlockCursor();
                                _holdMouseUnlocked = true;
                            }
                        }
                        else if (_holdMouseUnlocked)
                        {
                            if (_lockMouse)
                            {
                                _windowManager.LockCursorAtCurrentPosition();
                            }
                            _holdMouseUnlocked = false;
                        }

                        lock (_stateLock)
                        {
                            if (!isHoldUnlock)
                            {
                                if (deltaX != 0) mathEngine.ProcessSteering(deltaX);
                                if (profile.UseKeyboardOnlyMode) mathEngine.UpdateKeyboardSteering(isSteerLeft, isSteerRight, dtSeconds);
                                if (centerSteer) mathEngine.CenterSteering();
                                Volatile.Write(ref _latestSteer, mathEngine.GetMappedSteering());
                            }
                        }
                    }

                    if (_neutralizeRequested)
                    {
                        _neutralizeRequested = false;
                        if (WriteToVJoy(0, -32768, -32768, false, false))
                        {
                            _outputCallback?.Invoke(0, -32768, -32768);
                        }
                    }
                    else
                    {
                        int steer = Volatile.Read(ref _latestSteer);
                        int gas = Volatile.Read(ref _latestGas);
                        int brake = Volatile.Read(ref _latestBrake);
                        bool shiftDown = Volatile.Read(ref _latestShiftDown) != 0;
                        bool shiftUp = Volatile.Read(ref _latestShiftUp) != 0;

                        if (WriteToVJoy(steer, gas, brake, shiftDown, shiftUp))
                        {
                            _outputCallback?.Invoke(steer, gas, brake);
                        }
                    }

                    PaceIteration(ref nextDeadlineTicks, targetTicks, paceWaiter);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError("vJoy output loop terminated unexpectedly", ex);
                RaiseFault($"vJoy output loop terminated: {ex.Message}");
            }
        }

        private bool WriteToVJoy(int steer, int gas, int brake, bool shiftDown, bool shiftUp)
        {
            var vJoy = _vJoy;
            if (vJoy == null) return true;

            if (vJoy.UpdateAxesAndButtons(steer, gas, brake, shiftDown, shiftUp)) return true;

            if (vJoy.ConsecutiveFailures <= MaxVJoyFailuresBeforeReacquire) return true;

            AppLogger.Instance.LogWarning($"vJoy device stopped accepting data ({vJoy.ConsecutiveFailures} failed attempts), trying to re-acquire it");
            if (vJoy.LastError != null)
            {
                AppLogger.Instance.LogError("Last error reported by vJoyInterface", vJoy.LastError);
            }

            if (vJoy.TryReacquire())
            {
                AppLogger.Instance.LogInfo("vJoy device re-acquired, vWheel Hub continues to run");
                return true;
            }

            Neutralize();
            RaiseFault("vJoy device lost - vWheel Hub stopped. Another application may have taken over Device #1, or the vJoy driver was restarted.");
            return false;
        }

        private static void PaceIteration(
            ref long nextDeadlineTicks,
            long intervalTicks,
            NativeTimer.HighResolutionWaiter waiter)
        {
            intervalTicks = Math.Max(1, intervalTicks);
            long nowTicks = Stopwatch.GetTimestamp();

            if (nextDeadlineTicks == 0)
            {
                nextDeadlineTicks = nowTicks + intervalTicks;
            }
            else
            {
                nextDeadlineTicks += intervalTicks;
                if (nowTicks - nextDeadlineTicks >= intervalTicks)
                {
                    nextDeadlineTicks = nowTicks + intervalTicks;
                }
            }

            long remainingTicks = nextDeadlineTicks - nowTicks;
            if (remainingTicks <= 0) return;

            double remainingSeconds = remainingTicks / (double)Stopwatch.Frequency;
            if (waiter.Wait(remainingSeconds)) return;

            while ((remainingTicks = nextDeadlineTicks - Stopwatch.GetTimestamp()) > 0)
            {
                double remainingMs = remainingTicks * 1000.0 / Stopwatch.Frequency;
                if (remainingMs > 1.5)
                {
                    Thread.Sleep(Math.Max(1, (int)Math.Floor(remainingMs - 0.5)));
                    continue;
                }

                Thread.SpinWait(32);
            }
        }

        private void LogIterationErrorThrottled(Exception ex)
        {
            long now = Environment.TickCount64;
            if (now - _lastLoopErrorLog <= ErrorLogIntervalMs) return;

            _lastLoopErrorLog = now;
            AppLogger.Instance.LogError($"Runtime iteration error ({_consecutiveErrors} in a row)", ex);
        }

        private void Neutralize()
        {
            try
            {
                lock (_stateLock) _axisState.Reset(-32768.0);
                Volatile.Write(ref _latestSteer, 0);
                Volatile.Write(ref _latestGas, -32768);
                Volatile.Write(ref _latestBrake, -32768);
                Volatile.Write(ref _latestShiftDown, 0);
                Volatile.Write(ref _latestShiftUp, 0);
                _neutralizeRequested = true;
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError("Could not neutralize the axes", ex);
            }
        }

        private void RaiseFault(string message)
        {
            _faultStop = true;
            AppLogger.Instance.LogError(message);

            var handler = OnFault;
            if (handler == null) return;

            try
            {
                handler(message);
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError("OnFault handler failed", ex);
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
