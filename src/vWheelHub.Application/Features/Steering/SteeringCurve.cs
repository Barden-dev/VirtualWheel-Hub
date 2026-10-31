using System;
using System.Collections.Generic;

namespace SimRacingHub.Application.Features.Steering;

/// <summary>A point on the steering response curve used by the UI graph.</summary>
public readonly record struct SteeringCurvePoint(double Input, double Output);

/// <summary>Pure UI response-curve calculation, independent of profiles and event subscriptions.</summary>
public static class SteeringCurve
{
    public static double Evaluate(double input, double gamma)
    {
        double clamped = Math.Clamp(input, -1.0, 1.0);
        gamma = gamma <= 0 ? 1.0 : gamma;
        return Math.Sign(clamped) * Math.Pow(Math.Abs(clamped), gamma);
    }

    public static IReadOnlyList<SteeringCurvePoint> CreatePoints(double gamma)
    {
        const int stepCount = 100;
        var points = new SteeringCurvePoint[stepCount + 1];
        gamma = gamma <= 0 ? 1.0 : gamma;
        for (int i = 0; i <= stepCount; i++)
        {
            double t = i / (double)stepCount;
            double normIn = -1.0 + (t * 2.0);
            double normOut = Math.Sign(normIn) * Math.Pow(Math.Abs(normIn), gamma);
            points[i] = new SteeringCurvePoint(normIn, normOut);
        }
        return points;
    }
}
