namespace SimRacingHub.Core;

/// <summary>Raw controller values, independent of driving calibration and assists.</summary>
public readonly record struct OutputTestReport(int Steer = 16384, int Gas = 0, int Brake = 0, int Button = 0)
{
    public OutputTestReport() : this(16384, 0, 0, 0) { }
    public static OutputTestReport Neutral => new(16384, 0, 0, 0);
}

public readonly record struct OutputTestCapabilities(int ButtonCount);
