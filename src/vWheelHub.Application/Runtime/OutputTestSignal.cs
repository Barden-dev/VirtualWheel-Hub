using System;
using SimRacingHub.Core;

namespace SimRacingHub.Application.Runtime;

public enum OutputTestAction { SteeringBoth, SteeringLeft, SteeringRight, Gas, Brake, Button = 6, ShiftDown, ShiftUp }

public interface IOutputTestSession
{
    OutputTestCapabilities OutputTestCapabilities { get; }
    bool IsOutputTestActive { get; }
    /// <summary>Changes when runtime lifecycle invalidates ownership of a test and its temporary runtime.</summary>
    long OutputTestLifecycleVersion => 0;
    double OutputTestSecondsRemaining { get; }
    void StartOutputTest(OutputTestAction action, int button = 1, bool repeat = false);
    void StopOutputTest();
}

public static class OutputTestSignal
{
    public const double CountdownSeconds = 5;
    public const double TransitionSeconds = 0.5;
    public const double ButtonPressSeconds = 0.12;

    public static double SingleDuration(OutputTestAction action) => action switch
    {
        OutputTestAction.Button or OutputTestAction.ShiftDown or OutputTestAction.ShiftUp => ButtonPressSeconds,
        OutputTestAction.SteeringBoth => 1.5,
        _ => 1
    };

    public static OutputTestReport SampleSingle(OutputTestAction action, double seconds, int button = 1)
    {
        if (seconds >= SingleDuration(action)) return OutputTestReport.Neutral;
        if (action == OutputTestAction.SteeringBoth && seconds >= 1)
            return new OutputTestReport(Steer: (int)Math.Round(32768 - (seconds - 1) * 32768));
        return Sample(action, seconds, button);
    }

    public static OutputTestReport Sample(OutputTestAction action, double seconds, int button = 1)
    {
        if (!Enum.IsDefined(action) || !double.IsFinite(seconds) || seconds < 0 || button < 1)
            throw new ArgumentOutOfRangeException();
        // Both-direction steering first travels from centre to left, then alternates extremes.
        double phase = seconds % 1.0;
        double pedal = phase <= 0.5 ? phase * 2 : (1 - phase) * 2;
        double swingPhase = Math.Max(0, seconds - 0.5) % 1.0;
        double steer = action switch
        {
            OutputTestAction.SteeringLeft => -pedal,
            OutputTestAction.SteeringRight => pedal,
            OutputTestAction.SteeringBoth when seconds < 0.5 => -seconds * 2,
            OutputTestAction.SteeringBoth => swingPhase <= 0.5 ? -1 + swingPhase * 4 : 3 - swingPhase * 4,
            _ => 0
        };
        int value = (int)Math.Round(pedal * 32768);
        return new OutputTestReport((int)Math.Round(16384 + steer * 16384),
            action == OutputTestAction.Gas ? value : 0,
            action == OutputTestAction.Brake ? value : 0,
            phase < 0.5 ? action switch
            {
                OutputTestAction.ShiftDown => 1,
                OutputTestAction.ShiftUp => 2,
                OutputTestAction.Button => button,
                _ => 0
            } : 0);
    }
}
