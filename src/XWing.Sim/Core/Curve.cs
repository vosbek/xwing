namespace XWing.Sim.Core;

public readonly record struct CurvePoint(float X, float Y);

/// <summary>Piecewise-linear lookup table, clamped at the ends.</summary>
public sealed class Curve
{
    public List<CurvePoint> Points { get; init; } = new();

    public Curve() { }

    public Curve(params CurvePoint[] points) => Points = points.OrderBy(p => p.X).ToList();

    public float Evaluate(float x)
    {
        if (Points.Count == 0) return 1f;
        if (x <= Points[0].X) return Points[0].Y;
        for (int i = 1; i < Points.Count; i++)
        {
            CurvePoint b = Points[i];
            if (x <= b.X)
            {
                CurvePoint a = Points[i - 1];
                float t = (x - a.X) / (b.X - a.X);
                return a.Y + (b.Y - a.Y) * t;
            }
        }
        return Points[^1].Y;
    }
}
