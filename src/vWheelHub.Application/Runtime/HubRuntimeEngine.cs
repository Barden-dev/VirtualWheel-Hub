using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SimRacingHub.Core;
using SimRacingHub.Models;
using SimRacingHub.Services;
using SimRacingHub.Services.Games;

namespace SimRacingHub.Application.Runtime
{
    /// <summary>Coordinates runtime tasks and the shared owners of input, output and control state.</summary>
    public partial class HubRuntimeEngine : IRuntimeSession, IRuntimeCursorControl, IDisposable
    {
        private readonly OutputConnection _output;
        private readonly OutputTestSession _outputTest;
        private const int MaxConsecutiveErrors = 30;

        private const int PollingTaskStopTimeoutMs = 2000;

        private const int IdleSleepMs = 10;

        private const int ErrorLogIntervalMs = 5000;

        private const int SnapshotIntervalMs = 30;

        private readonly GameIntegrationRegistry _registry;

        private readonly CursorLockController _cursorLockService;

        private readonly IInputSource _inputSource;

        private readonly bool _ownsInputSource;

        private readonly IPrecisionTimer _precisionTimer;

        private readonly ProfileSession? _profileSession;

        private MathEngine? _mathEngine;

        private readonly AxisState _axisState = new();

        private CancellationTokenSource? _cts;

        private ResolvedProfile? _currentProfile;

        private ProfileContext? _currentContext;



        private Action<int, int, int>? _outputCallback;

        private readonly object _stateLock = new();

        private IGameIntegration? _activeIntegration;

        private Task? _pollingTask;

        private Task? _vJoyTask;

        private int _latestSteer = 16384;

        private int _latestGas = -32768;

        private int _latestBrake = -32768;

        private readonly VirtualButtonProcessor _userButtons = new();
        private readonly DrivingFocusGate _focusGate = new();
        private long _lastUserButtonTicks;
        private bool _validVirtualButtons = true;
        private int _latestShiftDown;

        private int _latestShiftUp;

        private int _consecutiveErrors;

        private long _lastLoopErrorLog;

        private volatile bool _faultStop;

        private string? _faultMessage;

        private volatile bool _neutralizeRequested;

        private long _lastDetectorCheckTicks;

        private long _lastSnapshotTicks;

        private RuntimeSnapshot _latestSnapshot = RuntimeSnapshot.Stopped();

        private bool _disposed;

        private bool _retainOutputConnection;

        private readonly TickState _tickState = new();

        public event Action<RuntimeSnapshot>? SnapshotUpdated;

        public event Action<string>? FaultOccurred;

        public event Action<bool>? IsRunningChanged;

        public event Action<bool>? OnMouseLockStateChanged;

        public event Action<int, int, int>? AxisUpdated;

        public bool IsRunning => _cts != null && !_cts.IsCancellationRequested && !_faultStop;

        public bool IsLocked => IsRunning && _cursorLockService.IsCursorLocked;

        public bool HasFault => _faultStop;

        public string? FaultMessage => _faultMessage;

        private Exception? _startFailure;
        public bool LastStartFailureWasOutput { get; private set; }

        internal int LatestSteer => Volatile.Read(ref _latestSteer);

        internal int LatestGas => Volatile.Read(ref _latestGas);

        internal int LatestBrake => Volatile.Read(ref _latestBrake);

        internal bool NeutralizeRequested => _neutralizeRequested;

        internal bool LockMouseState => _cursorLockService.Requested;

        internal bool WasGameRunning => _activeIntegration?.Detector.IsRunning ?? false;

        internal AxisState AxisState => _axisState;

        public HubRuntimeEngine(IInputSource inputSource, IOutputFeederFactory feederFactory, ICursorLockService cursorLockService, IPrecisionTimer precisionTimer, ProfileSession? profileSession = null, GameIntegrationRegistry? registry = null, bool ownsInputSource = false)
        {
            _inputSource = inputSource ?? new HeadlessInputSource();
            _output = new OutputConnection(feederFactory ?? new HeadlessOutputFeederFactory(), _stateLock);
            _cursorLockService = new CursorLockController(cursorLockService ?? new HeadlessCursorLockService());
            _precisionTimer = precisionTimer ?? new HeadlessPrecisionTimer();
            ;
            _profileSession = profileSession;
            _registry = registry ?? new GameIntegrationRegistry(registerDefaults: false);
            _ownsInputSource = ownsInputSource;
            _outputTest = new OutputTestSession(_stateLock, _output, _inputSource,
                () => IsRunning, () => _currentProfile, UpdateMouseLock, Neutralize, () => _mathEngine?.Neutralize(), RaiseFault);
            ;
            ;
            ;
            ;
            ;
            ;
            ;
            ;
            ;
            ;
            ;
            ;
        }

        public HubRuntimeEngine(IInputSource inputSource, IOutputFeeder outputFeeder, ICursorLockService cursorLockService, IPrecisionTimer precisionTimer, ProfileSession? profileSession = null, GameIntegrationRegistry? registry = null) : this(inputSource, new SingleFeederFactory(outputFeeder), cursorLockService, precisionTimer, profileSession, registry)
        {
        }

        public HubRuntimeEngine(GameIntegrationRegistry registry, ICursorLockService cursorLockService, IInputSource? inputSource = null, IPrecisionTimer? precisionTimer = null, IOutputFeederFactory? feederFactory = null, ProfileSession? profileSession = null, bool ownsInputSource = false) : this(inputSource ?? new HeadlessInputSource(), feederFactory ?? new HeadlessOutputFeederFactory(), cursorLockService, precisionTimer ?? new HeadlessPrecisionTimer(), profileSession, registry, ownsInputSource)
        {
        }

        public void UpdateRuntimeProfile(ResolvedProfile profile, ProfileContext? context)
        {
            bool wasRunning = _cts != null && !_cts.IsCancellationRequested;
            if (wasRunning)
            {
                StopPollingTask("profile update");
            }

            CancelOutputTestForLifecycle();
            OutputDeviceMode prevMode;
            lock (_stateLock)
            {
                prevMode = _currentProfile?.OutputMode ?? OutputDeviceMode.VJoy;
                _userButtons.Reset();
                _tickState.VirtualButtons.Reset();
                _lastUserButtonTicks = 0;
                _focusGate.Reset();
                _tickState.FocusGate.Reset();
                _validVirtualButtons = VirtualButtonProcessor.Validate(profile.VirtualButtons, 128, profile.GetReservedVirtualButtonKeys()) == null;
                if (_output.HasFeeder) _output.UpdateReport(new(16384, 0, 0, default));
                _currentProfile = profile;
                _currentContext = context;
                _mathEngine = new MathEngine(profile, _axisState);
                IGameIntegration? nextIntegration = _registry.GetIntegration(context?.Game);
                ;
                ;
                _activeIntegration = nextIntegration;
                _cursorLockService.FocusSuspended = profile.ReleaseCursorOnFocusLoss && _activeIntegration?.Detector?.IsForeground == false;
                ;
                ;
                ;
                ;
            }

            bool synchronized;
            lock (_outputSetupGate)
                synchronized = _outputSetupLeases > 0 || _output.SynchronizeProfile(profile.OutputMode, prevMode, wasRunning, _output.ResolveVJoyDeviceId(profile));
            if (!synchronized)
            {
                Stop();
                return;
            }
            if (wasRunning)
            {
                _cts = new CancellationTokenSource();
                _faultStop = false;
                _faultMessage = null;
                CancellationToken token = _cts.Token;
                _pollingTask = StartRuntimeTask(() => PollingLoop(token), token);
                _vJoyTask = StartRuntimeTask(() => VJoyLoop(token), token);
                ;
            }

            PublishSnapshot();
        }

        private void StopPollingTask(string reason)
        {
            var task = _pollingTask;
            ;
            var vJoyTask = _vJoyTask;
            var cts = _cts;
            try
            {
                cts?.Cancel();
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError($"Could not cancel the polling loop ({reason})", ex);
            }

            if (task != null)
            {
                try
                {
                    if (!task.Wait(PollingTaskStopTimeoutMs))
                    {
                        AppLogger.Instance.LogWarning($"Polling loop did not stop within {PollingTaskStopTimeoutMs} ms ({reason})");
                    }
                }
                catch (AggregateException ex)when (ex.InnerException is OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    AppLogger.Instance.LogError($"Polling loop error during {reason}", ex);
                }
            }

            foreach (var backgroundTask in new[]
            {
                vJoyTask
            }

            )
            {
                if (backgroundTask == null)
                    continue;
                try
                {
                    if (!backgroundTask.Wait(PollingTaskStopTimeoutMs))
                    {
                        AppLogger.Instance.LogWarning($"Runtime task did not stop within {PollingTaskStopTimeoutMs} ms ({reason})");
                    }
                }
                catch (AggregateException ex)when (ex.InnerException is OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    AppLogger.Instance.LogError($"Runtime task error during {reason}", ex);
                }
            }

            _pollingTask = null;
            ;
            _vJoyTask = null;
            cts?.Dispose();
            _cts = null;
        }

        private readonly object _outputSetupGate = new();
        private int _outputSetupLeases;
        private bool _outputStarting;

        public IDisposable BeginOutputSetup()
        {
            lock (_outputSetupGate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(HubRuntimeEngine));
                if (IsRunning || _outputStarting) throw new InvalidOperationException("Stop hub controls before configuring output.");
                _output.ReleaseAndDispose();
                _outputSetupLeases++;
                return new OutputSetupLease(this);
            }
        }

        private sealed class OutputSetupLease(HubRuntimeEngine owner) : IDisposable
        {
            private HubRuntimeEngine? _owner = owner;
            public void Dispose() => Interlocked.Exchange(ref _owner, null)?.EndOutputSetup();
        }

        private void EndOutputSetup()
        {
            lock (_outputSetupGate)
            {
                if (--_outputSetupLeases == 0 && !_disposed && _currentProfile is { } profile)
                    _output.SynchronizeProfile(profile.OutputMode, profile.OutputMode, false, _output.ResolveVJoyDeviceId(profile));
            }
        }

        public bool Start(ResolvedProfile profile, bool lockMouse, ProfileContext? context = null, Action<int, int, int>? outputCallback = null, bool suppressDrivingOnStart = false)
        {
            lock (_outputSetupGate)
            {
                if (_disposed || _outputSetupLeases != 0 || _outputStarting)
                {
                    _startFailure = new InvalidOperationException("Output setup or another start is in progress. Finish or cancel it before starting controls.");
                    return false;
                }
                _outputStarting = true;
            }
            try { return StartCore(profile, lockMouse, context, outputCallback, suppressDrivingOnStart); }
            finally { lock (_outputSetupGate) _outputStarting = false; }
        }

        private bool StartCore(ResolvedProfile profile, bool lockMouse, ProfileContext? context = null, Action<int, int, int>? outputCallback = null, bool suppressDrivingOnStart = false)
        {
            Stop();
            _startFailure = null;
            LastStartFailureWasOutput = false;
            if (profile == null)
            {
                AppLogger.Instance.LogError("Cannot start vWheel Hub: resolved profile is null");
                return false;
            }

            try
            {
                lock (_stateLock)
                {
                    _userButtons.Reset();
                _tickState.VirtualButtons.Reset();
                _lastUserButtonTicks = 0;
                _focusGate.Reset();
                _tickState.FocusGate.Reset();
                _validVirtualButtons = VirtualButtonProcessor.Validate(profile.VirtualButtons, 128, profile.GetReservedVirtualButtonKeys()) == null;
                if (_output.HasFeeder) _output.UpdateReport(new(16384, 0, 0, default));
                _currentProfile = profile;
                    _currentContext = context;
                    _cursorLockService.Requested = lockMouse;
                    _outputCallback = outputCallback;
                    _mathEngine = new MathEngine(profile, _axisState);
                    _activeIntegration = _registry.GetIntegration(context?.Game);
                    _cursorLockService.FocusSuspended = profile.ReleaseCursorOnFocusLoss && _activeIntegration?.Detector?.IsForeground == false;
                    ;
                    ;
                    ;
                    ;
                }

                try
                {
                    if (!_output.PrepareForStart(profile.OutputMode, _output.ResolveVJoyDeviceId(profile)))
                    {
                        LastStartFailureWasOutput = true;
                        _startFailure = _output.LastStartFailure;
                        return false;
                    }
                }
                catch { LastStartFailureWasOutput = true; throw; }

                string? buttonError = VirtualButtonProcessor.Validate(profile.VirtualButtons,
                    _output.TestCapabilities.ButtonCount, profile.GetReservedVirtualButtonKeys());
                if (buttonError != null)
                {
                    _startFailure = new InvalidOperationException(buttonError);
                    Stop();
                    return false;
                }
                _axisState.Reset(-32768.0);
                Volatile.Write(ref _latestSteer, 16384);
                Volatile.Write(ref _latestGas, 0);
                Volatile.Write(ref _latestBrake, 0);
                Volatile.Write(ref _latestShiftDown, 0);
                Volatile.Write(ref _latestShiftUp, 0);
                _neutralizeRequested = false;
                _outputTest.PrepareForStart(suppressDrivingOnStart);
                try
                {
                    _inputSource.Start();
                    if (!_inputSource.IsRunning)
                    {
                        _startFailure = new InvalidOperationException(SimRacingHub.Application.Localization.LocalizationManager.Instance.GetString("Readiness_InputUnavailable"));
                        AppLogger.Instance.LogError("Mouse capture could not be initialised, vWheel Hub was not started");
                        _inputSource.Stop();
                        _output.ReleaseAndDispose();
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    _startFailure = ex;
                    AppLogger.Instance.LogError("Mouse capture could not be initialised, vWheel Hub was not started", ex);
                    try
                    {
                        _inputSource.Stop();
                    }
                    catch
                    {
                    }

                    _output.ReleaseAndDispose();
                    return false;
                }

                if (_cursorLockService.Requested)
                {
                    _cursorLockService.LockCursorAtCurrentPosition();
                }

                _precisionTimer.BeginPeriod(1);
                _cts = new CancellationTokenSource();
                _faultStop = false;
                _faultMessage = null;
                CancellationToken token = _cts.Token;
                _pollingTask = StartRuntimeTask(() => PollingLoop(token), token);
                _vJoyTask = StartRuntimeTask(() => VJoyLoop(token), token);
                ;
                AppLogger.Instance.LogInfo("vWheel Hub started successfully");
                IsRunningChanged?.Invoke(true);
                PublishSnapshot();
                return true;
            }
            catch (Exception ex)
            {
                _startFailure = ex;
                AppLogger.Instance.LogError("Failed to start vWheel Hub", ex);
                try
                {
                    Stop();
                }
                catch (Exception cleanupEx)
                {
                    AppLogger.Instance.LogError("Failed to clean up an incomplete hub start", cleanupEx);
                }

                return false;
            }
        }

        public void Stop()
        {
            bool wasRunning = IsRunning;
            StopPollingTask("stop");
            CancelOutputTestForLifecycle();
            _precisionTimer.EndPeriod(1);
            _inputSource.Stop();
            ;
            _cursorLockService.UnlockCursor();
            _cursorLockService.Requested = false;
            ;
            _cursorLockService.HoldUnlocked = false;
            lock (_stateLock)
            {
                ;
                ;
            }

            _output.Stop(_retainOutputConnection);

            _outputCallback?.Invoke(16384, 0, 0);
            AxisUpdated?.Invoke(16384, 0, 0);
            ;
            if (wasRunning)
            {
                IsRunningChanged?.Invoke(false);
            }

            PublishSnapshot();
        }

        public void UpdateMouseLock(bool lockMouse)
        {
            _cursorLockService.SetRequested(lockMouse && IsRunning && !_outputTest.IsOutputTestActive && !_outputTest.Preparing);
            OnMouseLockStateChanged?.Invoke(_cursorLockService.Requested);
            PublishSnapshot();
        }

        public void ReapplyMouseLock()
        {
            if (_cursorLockService.Requested && IsRunning)
            {
                _cursorLockService.ReapplyLock();
            }
        }

        public Func<OutputDeviceMode, CancellationToken, Task<SimRacingHub.Application.Contracts.DeviceDiagnosticSnapshot>>? PrepareOutputAsync { get; set; }

        public async Task StartAsync(RuntimeStartOptions options, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(options);
            ct.ThrowIfCancellationRequested();
            _retainOutputConnection = options.RetainOutputConnection;
            ResolvedProfile? profile = null;
            ProfileContext? context = null;
            lock (_stateLock)
            {
                profile = options.Profile ?? _currentProfile;
                context = options.Context ?? _currentContext;
            }

            if (profile == null && _profileSession != null)
            {
                profile = _profileSession.ResolvedProfile;
                if (context == null && _profileSession.CurrentProfile != null)
                {
                    context = new ProfileContext(_profileSession.CurrentProfile.Name, null, null);
                }
            }

            if (profile == null)
            {
                var defaultProfile = ProfileDefaults.CreateDefault();
                profile = new ResolvedProfile();
                profile.UpdateFrom(defaultProfile);
            }

            profile.OutputMode = options.OutputMode;
            if (!IsRunning && options.AllowOutputSetup && PrepareOutputAsync != null)
            {
                try
                {
                    using var setup = BeginOutputSetup();
                    var ready = await PrepareOutputAsync(options.OutputMode, ct).ConfigureAwait(false);
                    if (!ready.IsAvailable) throw new InvalidOperationException(ready.ErrorDetails ?? ready.StatusText);
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    LastStartFailureWasOutput = true;
                    _startFailure = error;
                    throw;
                }
            }
            bool started = Start(profile, options.LockCursorOnStart, context, null, options.SuppressDrivingOnStart);
            if (!started)
            {
                throw new InvalidOperationException(_startFailure?.Message ?? "Failed to start hub runtime.", _startFailure);
            }

        }

        public Task StopAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            Stop();
            return Task.CompletedTask;
        }

        public void UpdateProfile(Profile profile, ResolvedProfile resolved)
        {
            ArgumentNullException.ThrowIfNull(resolved);
            var context = profile != null ? new ProfileContext(profile.Name, null, null) : null;
            UpdateRuntimeProfile(resolved, context);
        }

        public void UpdateProfile(ResolvedProfile resolved)
        {
            ArgumentNullException.ThrowIfNull(resolved);
            UpdateRuntimeProfile(resolved, _currentContext);
        }

        public void UpdateProfile(ResolvedProfile resolved, ProfileContext context) => UpdateRuntimeProfile(resolved, context);

        public RuntimeSnapshot GetSnapshot()
        {
            return BuildSnapshot();
        }

        public TickOutput ProcessTick(double dtSeconds = 0)
        {
            if (SuppressDrivingForOutputTest()) return new TickOutput(16384, 0, 0, false, false, true);
            TickOutput output;
            lock (_stateLock)
            {
                if (_currentProfile == null || _mathEngine == null)
                    return new TickOutput(16384, 0, 0, false, false, true);
                bool monitored = _activeIntegration?.Detector != null;
                bool present = _activeIntegration?.Detector.IsRunning ?? true;
                _tickState.LockMouse = _cursorLockService.Requested;
                _tickState.BypassInternalLockToggle = true;
                output = TickProcessor.ProcessTick(_currentProfile, monitored, present,
                    _inputSource, _cursorLockService, _mathEngine, _tickState, dtSeconds, isGameFocused: _activeIntegration?.Detector?.IsForeground);
                _cursorLockService.HoldUnlocked = _tickState.HoldMouseUnlocked;
                Volatile.Write(ref _latestSteer, output.Steer);
                Volatile.Write(ref _latestGas, output.Gas);
                Volatile.Write(ref _latestBrake, output.Brake);
                Volatile.Write(ref _latestShiftDown, output.ShiftDown ? 1 : 0);
                Volatile.Write(ref _latestShiftUp, output.ShiftUp ? 1 : 0);
            }
            _outputCallback?.Invoke(output.Steer, output.Gas, output.Brake);
            AxisUpdated?.Invoke(output.Steer, output.Gas, output.Brake);
            return output;
        }

        private RuntimeSnapshot BuildSnapshot()
        {
            bool isRunning = IsRunning;
            bool isLocked = IsLocked;
            int steer = Volatile.Read(ref _latestSteer);
            int gas = Volatile.Read(ref _latestGas);
            int brake = Volatile.Read(ref _latestBrake);
            ;
            double wheelNorm = Math.Clamp((steer - 16384.0) / 16384.0, -1.0, 1.0);
            double gasNorm = Math.Clamp(gas / 32768.0, 0.0, 1.0);
            double brakeNorm = Math.Clamp(brake / 32768.0, 0.0, 1.0);
            ;
            ;
            ;
            string? gameTitle;
            RuntimeInputSnapshot inputs;
            lock (_stateLock)
            {
                gameTitle = _activeIntegration?.Detector.IsRunning == true ? _activeIntegration.GameId : null;
                inputs = new RuntimeInputSnapshot(isRunning ? Math.Clamp(_axisState.Steering / 32768.0, -1, 1) : 0, isRunning ? Math.Clamp((_axisState.GasVal + 32768.0) / 65536.0, 0, 1) : 0, isRunning ? Math.Clamp((_axisState.BrakeVal + 32768.0) / 65536.0, 0, 1) : 0);
            }

            string statusMsg = _faultStop ? (_faultMessage ?? "Fault") : (isRunning ? "Running" : "Stopped");
            return new RuntimeSnapshot(IsRunning: isRunning, IsLocked: isLocked, WheelAxis: isRunning ? wheelNorm : 0.0, GasAxis: isRunning ? gasNorm : 0.0, BrakeAxis: isRunning ? brakeNorm : 0.0, GameTitle: gameTitle, StatusMessage: statusMsg, HasFault: _faultStop, FaultMessage: _faultMessage)
            {
                Inputs = inputs
            };
        }

        private void PublishSnapshot()
        {
            var snapshot = BuildSnapshot();
            _latestSnapshot = snapshot;
            try
            {
                SnapshotUpdated?.Invoke(snapshot);
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError("SnapshotUpdated handler failed", ex);
            }
        }

        private static Task StartRuntimeTask(Action action, CancellationToken token)
        {
            return Task.Factory.StartNew(action, token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        private void PollingLoop(CancellationToken token)
        {
            long nextDeadlineTicks = 0;
            using var paceWaiter = _precisionTimer.CreateWaiter();
            long previousIterationTicks = 0;
            _consecutiveErrors = 0;
            long idleTargetTicks = (long)(Stopwatch.Frequency * (IdleSleepMs / 1000.0));
            AppLogger.Instance.LogInfo("Polling loop started", showInStatusBar: false);
            try
            {
                while (!token.IsCancellationRequested && !_faultStop)
                {
                    long iterationTicks = Stopwatch.GetTimestamp();
                    double actualDtSeconds = previousIterationTicks == 0 ? 0.0 : (iterationTicks - previousIterationTicks) / (double)Stopwatch.Frequency;
                    previousIterationTicks = iterationTicks;
                    long targetTicks = idleTargetTicks;
                    try
                    {
                        targetTicks = RunBackgroundIteration(actualDtSeconds);
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

        private long RunBackgroundIteration(double actualDtSeconds)
        {
            lock (_stateLock)
            {
                if (SuppressDrivingForOutputTest())
                {
                    // Free computes input in this loop; the output writer never consumes mouse delta.
                    _inputSource.ConsumeDeltaX();
                    return Math.Max(1, Stopwatch.Frequency / 100);
                }
                ResolvedProfile? profile;
                IGameIntegration? integration;
                lock (_stateLock) { profile = _currentProfile; integration = _activeIntegration; }
                if (profile == null) return Math.Max(1, Stopwatch.Frequency / 100);
                long now = Stopwatch.GetTimestamp();
                if (integration != null && now - _lastDetectorCheckTicks >= Stopwatch.Frequency / 4)
                {
                    _lastDetectorCheckTicks = now;
                    lock (integration.Detector) integration.Detector.Update();
                }
                ProcessTick(actualDtSeconds);
                long currentMs = Environment.TickCount64;
                if (currentMs - _lastSnapshotTicks >= SnapshotIntervalMs)
                {
                    _lastSnapshotTicks = currentMs;
                    PublishSnapshot();
                }
                int rate = Math.Clamp(profile.PollingRate > 0 ? profile.PollingRate : 1000, 50, 1000);
                return Math.Max(1, Stopwatch.Frequency / rate);
            }
        }

        private void VJoyLoop(CancellationToken token)
        {
            long deadline = 0;
            using var waiter = _precisionTimer.CreateWaiter();
            try
            {
                while (!token.IsCancellationRequested && !_faultStop)
                {
                    if (!_inputSource.IsRunning)
                    {
                        RaiseFault("Input device disconnected. Check the mouse and keyboard, then restart the hub.");
                        return;
                    }
                    if (_neutralizeRequested)
                    {
                        _neutralizeRequested = false;
                        WriteToVJoy(16384, 0, 0, false, false, forceNeutral: true);
                    }
                    else
                        WriteToVJoy(Volatile.Read(ref _latestSteer), Volatile.Read(ref _latestGas),
                            Volatile.Read(ref _latestBrake), Volatile.Read(ref _latestShiftDown) != 0,
                            Volatile.Read(ref _latestShiftUp) != 0);
                    int rate = Math.Clamp(_currentProfile?.PollingRate > 0 ? _currentProfile.PollingRate : 1000, 50, 1000);
                    PaceIteration(ref deadline, Math.Max(1, Stopwatch.Frequency / rate), waiter);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError("Output loop terminated unexpectedly", ex);
                RaiseFault($"Output loop terminated: {ex.Message}");
            }
        }

        private bool WriteToVJoy(int steer, int gas, int brake, bool shiftDown, bool shiftUp, bool forceNeutral = false)
                {
                    lock (_stateLock)
                    {
                        if (_faultStop) return false;
                        if (WriteOutputTestIfNeeded()) return !_faultStop;
                        long now = Stopwatch.GetTimestamp();
                        double elapsed = _lastUserButtonTicks == 0 ? 0 : (now - _lastUserButtonTicks) / (double)Stopwatch.Frequency;
                        _lastUserButtonTicks = now;
                        var profile = _currentProfile;
                        bool focusSuspended = profile != null && _focusGate.Update(_activeIntegration?.Detector?.IsForeground, profile, _inputSource, enabled: profile.PauseControlsOnFocusLoss);
                        _cursorLockService.FocusSuspended = profile != null && profile.ReleaseCursorOnFocusLoss &&
                            (focusSuspended || _activeIntegration?.Detector?.IsForeground == false);
                        if (profile != null) _cursorLockService.UpdateHold(_cursorLockService.FocusSuspended ||
                            ControlTickRules.EitherDown(_inputSource, profile.KeyUnlockCursor1, profile.KeyUnlockCursor2));
                        if (focusSuspended)
                        {
                            steer = 16384; gas = brake = 0; shiftDown = shiftUp = false;
                            _mathEngine?.Neutralize();
                            Volatile.Write(ref _latestSteer, 16384);
                            Volatile.Write(ref _latestGas, 0);
                            Volatile.Write(ref _latestBrake, 0);
                            Volatile.Write(ref _latestShiftDown, 0);
                            Volatile.Write(ref _latestShiftUp, 0);
                        }
                        bool allowButtons = !forceNeutral && _validVirtualButtons && profile != null && _cursorLockService.Requested &&
                            !ControlTickRules.EitherDown(_inputSource, profile.KeyUnlockCursor1, profile.KeyUnlockCursor2) &&
                            _activeIntegration?.Detector?.IsRunning == true && (!profile.PauseControlsOnFocusLoss || _activeIntegration.Detector.IsForeground == true);
                        var buttons = _userButtons.Process(profile?.VirtualButtons ?? [], _inputSource, allowButtons,
                            true, elapsed, _output.TestCapabilities.ButtonCount);
                        if (_output.SendWithRecovery(steer, gas, brake, shiftDown, shiftUp, buttons))
                        {
                            if (_output.LastWriteReacquired)
                            {
                                _userButtons.Reset();
                                _tickState.VirtualButtons.Reset();
                                _output.UpdateAxesAndButtons(16384, 0, 0, false, false);
                            }
                            return true;
                        }
                        Neutralize();
                        RaiseFault($"{_output.Name} lost - vWheel Hub stopped.");
                        return false;
                    }
                }

        private static void PaceIteration(ref long nextDeadlineTicks, long intervalTicks, IPrecisionWaiter waiter)
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
            if (remainingTicks <= 0)
                return;
            double remainingSeconds = remainingTicks / (double)Stopwatch.Frequency;
            if (waiter.Wait(remainingSeconds))
                return;
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
            if (now - _lastLoopErrorLog <= ErrorLogIntervalMs)
                return;
            _lastLoopErrorLog = now;
            AppLogger.Instance.LogError($"Runtime iteration error ({_consecutiveErrors} in a row)", ex);
        }

        private void Neutralize()
        {
            try
            {
                lock (_stateLock)
                {
                    _axisState.Reset(-32768.0);
                    _userButtons.Reset();
                    _tickState.VirtualButtons.Reset();
                }

                Volatile.Write(ref _latestSteer, 16384);
                Volatile.Write(ref _latestGas, 0);
                Volatile.Write(ref _latestBrake, 0);
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
            CancelOutputTestForLifecycle();
            ;
            _faultStop = true;
            _faultMessage = message;
            Neutralize();
            try { _output.UpdateAxesAndButtons(16384, 0, 0, false, false); }
            catch (Exception ex) { AppLogger.Instance.LogError("Could not send neutral output after a fault", ex); }
            _cursorLockService.Requested = false;
            try { _cursorLockService.UnlockCursor(); }
            catch (Exception ex) { AppLogger.Instance.LogError("Could not release the cursor after a fault", ex); }
            AppLogger.Instance.LogError(message);
            try
            {
                FaultOccurred?.Invoke(message);
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError("FaultOccurred handler failed", ex);
            }

            PublishSnapshot();
        }

        public async ValueTask DisposeAsync()
        {
            Dispose();
            await Task.CompletedTask;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            Stop();
            if (_ownsInputSource)
            {
                _inputSource.Dispose();
            }

            _output.Dispose();

            GC.SuppressFinalize(this);
        }

        private sealed class SingleFeederFactory : IOutputFeederFactory
        {
            private readonly IOutputFeeder _feeder;
            public SingleFeederFactory(IOutputFeeder feeder) => _feeder = feeder ?? throw new ArgumentNullException(nameof(feeder));
            public IOutputFeeder CreateFeeder(OutputDeviceMode mode) => _feeder;
        }

        private sealed class HeadlessInputSource : IInputSource
        {
            public bool IsRunning { get; private set; }

            public void Start() => IsRunning = true;
            public void Stop() => IsRunning = false;
            public int ConsumeDeltaX() => 0;
            public bool IsKeyDown(int virtualKeyCode) => false;
            public void Dispose() => IsRunning = false;
        }

        private sealed class HeadlessPrecisionTimer : IPrecisionTimer
        {
            public void BeginPeriod(uint periodMs)
            {
            }

            public void EndPeriod(uint periodMs)
            {
            }

            public IPrecisionWaiter CreateWaiter() => new HeadlessPrecisionWaiter();
            private sealed class HeadlessPrecisionWaiter : IPrecisionWaiter
            {
                public bool Wait(double seconds)
                {
                    if (seconds > 0)
                        Thread.Sleep(Math.Max(1, (int)(seconds * 1000)));
                    return true;
                }

                public void Dispose()
                {
                }
            }
        }

        private sealed class HeadlessCursorLockService : ICursorLockService
        {
            public bool IsCursorLocked { get; private set; }

            public void LockCursorAtCurrentPosition() => IsCursorLocked = true;
            public void UnlockCursor() => IsCursorLocked = false;
            public void ReapplyLock()
            {
            }

            public void Dispose() => IsCursorLocked = false;
        }

        private sealed class HeadlessOutputFeederFactory : IOutputFeederFactory
        {
            public IOutputFeeder CreateFeeder(OutputDeviceMode mode) => new HeadlessOutputFeeder();
            private sealed class HeadlessOutputFeeder : IOutputFeeder
            {
                public string Name => "HeadlessFeeder";
                public bool IsConnected => true;
                public int ConsecutiveFailures => 0;
                public Exception? LastError => null;

                public bool Acquire() => true;
                public void Release()
                {
                }

                public bool UpdateAxesAndButtons(int steer, int gas, int brake, bool shiftDown, bool shiftUp) => true;
                public bool TryReacquire() => true;
                public void Dispose()
                {
                }
            }
        }

    }
}
