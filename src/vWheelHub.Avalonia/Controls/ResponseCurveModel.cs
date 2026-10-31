using System.Collections;
using Avalonia;
using SimRacingHub.Application.Features.Steering;

namespace SimRacingHub.Avalonia.Controls;

internal readonly record struct ResponseCurvePoint(double Input, double Output);

internal static class ResponseCurvePointAdapter
{
    public static IReadOnlyList<ResponseCurvePoint> Normalize(IEnumerable? source)
    {
        if (source == null)
        {
            return Array.Empty<ResponseCurvePoint>();
        }

        var result = new List<ResponseCurvePoint>();
        foreach (object? item in source)
        {
            switch (item)
            {
                case SteeringCurvePoint steering:
                    result.Add(new ResponseCurvePoint(steering.Input, steering.Output));
                    break;
                case Point point:
                    result.Add(new ResponseCurvePoint(point.X, point.Y));
                    break;
            }
        }

        return result;
    }
}

internal readonly record struct ResponseCurveGeometry(Rect Plot, bool IsBipolar)
{
    public Point ToCanvas(ResponseCurvePoint point) => ToCanvas(point.Input, point.Output);

    public Point ToCanvas(double input, double output)
    {
        double x = InputToCanvasX(input);
        double minimum = IsBipolar ? -1.0 : 0.0;
        double clampedOutput = Math.Clamp(output, minimum, 1.0);
        double fraction = IsBipolar ? (clampedOutput + 1.0) / 2.0 : clampedOutput;
        return new Point(x, Plot.Bottom - fraction * Plot.Height);
    }

    public double InputToCanvasX(double input)
    {
        double minimum = IsBipolar ? -1.0 : 0.0;
        double clamped = Math.Clamp(input, minimum, 1.0);
        double fraction = IsBipolar ? (clamped + 1.0) / 2.0 : clamped;
        return Plot.Left + fraction * Plot.Width;
    }

    public double CanvasXToInput(double screenX)
    {
        if (Plot.Width <= 0)
        {
            return 0.0;
        }

        double fraction = (screenX - Plot.Left) / Plot.Width;
        return IsBipolar
            ? Math.Clamp(-1.0 + fraction * 2.0, -1.0, 1.0)
            : Math.Clamp(fraction, 0.0, 1.0);
    }
}

internal static class ResponseCurveEvaluator
{
    public static double Evaluate(
        double input,
        IReadOnlyList<ResponseCurvePoint> points,
        double gamma,
        bool isBipolar)
    {
        if (points.Count >= 2)
        {
            for (int index = 0; index < points.Count - 1; index++)
            {
                ResponseCurvePoint first = points[index];
                ResponseCurvePoint second = points[index + 1];
                if (input < first.Input || input > second.Input)
                {
                    continue;
                }

                double span = second.Input - first.Input;
                if (Math.Abs(span) < 1e-9)
                {
                    return first.Output;
                }

                double ratio = (input - first.Input) / span;
                return first.Output + ratio * (second.Output - first.Output);
            }

            return input <= points[0].Input ? points[0].Output : points[^1].Output;
        }

        double effectiveGamma = gamma <= 0 ? 1.0 : gamma;
        double minimum = isBipolar ? -1.0 : 0.0;
        double clamped = Math.Clamp(input, minimum, 1.0);
        return isBipolar
            ? Math.Sign(clamped) * Math.Pow(Math.Abs(clamped), effectiveGamma)
            : Math.Pow(clamped, effectiveGamma);
    }
}

internal sealed class ResponseCurveDragState
{
    public ResponseCurveMarkerType? ActiveMarker { get; private set; }

    public double OriginalValue { get; private set; }

    public void Begin(ResponseCurveMarkerType marker, double originalValue)
    {
        ActiveMarker = marker;
        OriginalValue = originalValue;
    }

    public void End() => ActiveMarker = null;
}
