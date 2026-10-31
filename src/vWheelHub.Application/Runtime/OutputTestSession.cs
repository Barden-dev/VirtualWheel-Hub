using System;
using System.Diagnostics;
using SimRacingHub.Core;
using SimRacingHub.Models;

namespace SimRacingHub.Application.Runtime;

/// <summary>Owns test signal state and release gating while using the runtime's sole output connection.</summary>
internal sealed class OutputTestSession : IOutputTestSession
{
    private readonly object _stateLock;
    private readonly OutputConnection _output;
    private readonly IInputSource _inputSource;
    private readonly Func<bool> _isRunning;
    private readonly Func<ResolvedProfile?> _readProfile;
    private readonly Action<bool> _updateMouseLock;
    private readonly Action _neutralize;
    private readonly Action _neutralizeMath;
    private readonly Action<string> _raiseFault;
    internal OutputTestSession(object stateLock, OutputConnection output, IInputSource input,
        Func<bool> isRunning, Func<ResolvedProfile?> readProfile, Action<bool> updateMouseLock,
        Action neutralize, Action neutralizeMath, Action<string> raiseFault)
    {
        _stateLock = stateLock; _output = output; _inputSource = input;
        _isRunning = isRunning; _readProfile = readProfile; _updateMouseLock = updateMouseLock;
        _neutralize = neutralize; _neutralizeMath = neutralizeMath; _raiseFault = raiseFault;
    }
    internal bool Preparing { get { lock (_stateLock) return _outputTestPreparing; } }
    internal bool AwaitRelease { get { lock (_stateLock) return _outputTestAwaitRelease; } }
    internal void PrepareForStart(bool suppressDriving)
    {
        lock (_stateLock)
        {
            _temporaryRuntime = suppressDriving;
            _outputTestAwaitRelease = _outputTestPreparing = suppressDriving;
        }
    }
    // A runtime started solely for a test must stay neutral until its owner stops it.
    // Releasing the test's input gate must not resume calibrated driving during the UI timer delay.
    private bool _temporaryRuntime;
    private bool _outputTestActive;

    private bool _outputTestPreparing;

    internal Func<long> Clock { get; set; } = Stopwatch.GetTimestamp;

    private double OutputTestElapsed => (Clock() - _outputTestStarted) / (double)Stopwatch.Frequency;

    private bool _outputTestAwaitRelease;

    private long _outputTestStarted;

    private OutputTestAction _outputTestAction;

    private int _outputTestButton;

    private bool _outputTestRepeat;

    private long _lifecycleVersion;
    public long OutputTestLifecycleVersion { get { lock (_stateLock) return _lifecycleVersion; } }

    public OutputTestCapabilities OutputTestCapabilities
    {
        get
        {
            lock (_stateLock)
            {
                var profile = _readProfile();
                return _output.GetTestCapabilities(profile?.OutputMode ?? OutputDeviceMode.VJoy,
                    profile == null ? 1 : _output.ResolveVJoyDeviceId(profile));
            }
        }
    }

    public bool IsOutputTestActive { get { lock (_stateLock) return _outputTestActive; } }

    public double OutputTestSecondsRemaining
    {
        get { lock (_stateLock) return _outputTestActive ? Math.Max(0, OutputTestSignal.CountdownSeconds - OutputTestElapsed) : 0; }
    }

    public void StartOutputTest(OutputTestAction action, int button = 1, bool repeat = false)
    {
        lock (_stateLock)
        {
            if (!_isRunning() || !_output.HasFeeder) throw new InvalidOperationException("Start the hub before testing output.");
            var capabilities = _output.TestCapabilities;
            if (!Enum.IsDefined(action) ||
                (action == OutputTestAction.ShiftDown && capabilities.ButtonCount < 1) ||
                (action == OutputTestAction.ShiftUp && capabilities.ButtonCount < 2) ||
                (action == OutputTestAction.Button && (button < 1 || button > capabilities.ButtonCount)))
                throw new ArgumentOutOfRangeException(nameof(action));
            _outputTestAction = action;
            _outputTestButton = button;
            _outputTestRepeat = repeat;
            _outputTestStarted = Clock();
            _outputTestPreparing = false;
            _outputTestActive = true;
            _outputTestAwaitRelease = true;
            _updateMouseLock(false);
            _neutralize();
            if (!_output.UpdateTestReport(OutputTestReport.Neutral))
                _raiseFault("Output test failed: controller rejected the neutral report.");
        }
    }

    public void StopOutputTest()
    {
        lock (_stateLock)
        {
            if (!_outputTestActive) return;
            _outputTestActive = false;
            _neutralize();
            if (_output.HasFeeder && !_output.UpdateTestReport(OutputTestReport.Neutral))
                _raiseFault("Output test failed: controller could not be neutralized.");
        }
    }

    // Called under _stateLock by the sole output writer. Escape is global through IInputSource.
    internal bool WriteIfNeeded()
    {
        if (_outputTestActive && _inputSource.IsKeyDown(0x1B)) StopOutputTest();
        if (!_outputTestActive && !_outputTestAwaitRelease && !_temporaryRuntime) return false;
        var report = OutputTestReport.Neutral;
        if (_outputTestActive)
        {
            double seconds = OutputTestElapsed - OutputTestSignal.CountdownSeconds;
            if (seconds >= 0)
            {
                if (!_outputTestRepeat && seconds >= OutputTestSignal.SingleDuration(_outputTestAction)) StopOutputTest();
                else report = _outputTestRepeat ? OutputTestSignal.Sample(_outputTestAction, seconds, _outputTestButton)
                    : OutputTestSignal.SampleSingle(_outputTestAction, seconds, _outputTestButton);
            }
        }
        else if (!_outputTestPreparing && _readProfile() is { } profile && !AnyDrivingBindingDown(profile))
        {
            _outputTestAwaitRelease = false;
        }
        if (_output.UpdateTestReport(report) != true)
            _raiseFault("Output test stopped: controller rejected the report.");
        return true;
    }

    private bool AnyDrivingBindingDown(ResolvedProfile p)
    {
        bool Down(int key) => key != 0 && _inputSource.IsKeyDown(key);
        return Down(p.KeyGas1) || Down(p.KeyGas2) || Down(p.KeyBrake1) || Down(p.KeyBrake2) ||
            Down(p.KeyShiftDown1) || Down(p.KeyShiftDown2) || Down(p.KeyShiftUp1) || Down(p.KeyShiftUp2) ||
            Down(p.KeySteerLeft1) || Down(p.KeySteerLeft2) || Down(p.KeySteerRight1) || Down(p.KeySteerRight2) ||
            Down(p.KeyCenterSteering1) || Down(p.KeyCenterSteering2) || _inputSource.IsKeyDown(0x1B);
    }

    internal bool ShouldSuppress()
    {
        lock (_stateLock)
        {
            if (!_outputTestActive && !_outputTestAwaitRelease && !_temporaryRuntime) return false;
            _neutralizeMath();
            return true;
        }
    }

    internal void CancelForLifecycle()
    {
        lock (_stateLock)
        {
            _lifecycleVersion++;
            _temporaryRuntime = false;
            if (_outputTestActive || _outputTestAwaitRelease)
            {
                _outputTestPreparing = false;
                _outputTestActive = false;
                _outputTestAwaitRelease = false;
                try { _output.UpdateTestReport(OutputTestReport.Neutral); }
                catch (Exception ex) { AppLogger.Instance.LogError("Could not reset test output", ex); }
                _neutralize();
            }
        }
    }

}
