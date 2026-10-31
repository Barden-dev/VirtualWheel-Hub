using System;
using SimRacingHub.Core;

namespace SimRacingHub.Application.Runtime;

public partial class HubRuntimeEngine : IOutputTestSession
{
    internal Func<long> OutputTestClock { get => _outputTest.Clock; set => _outputTest.Clock = value; }
    public OutputTestCapabilities OutputTestCapabilities => _outputTest.OutputTestCapabilities;
    public bool IsOutputTestActive => _outputTest.IsOutputTestActive;
    public long OutputTestLifecycleVersion => _outputTest.OutputTestLifecycleVersion;
    public double OutputTestSecondsRemaining => _outputTest.OutputTestSecondsRemaining;
    public void StartOutputTest(OutputTestAction action, int button = 1, bool repeat = false)
    {
        _outputTest.StartOutputTest(action, button, repeat);
    }
    public void StopOutputTest() => _outputTest.StopOutputTest();
    private bool WriteOutputTestIfNeeded() => _outputTest.WriteIfNeeded();
    private bool SuppressDrivingForOutputTest() => _outputTest.ShouldSuppress();
    private void CancelOutputTestForLifecycle() => _outputTest.CancelForLifecycle();
}
