using System.Numerics;
using XWing.Sim.Core;

namespace XWing.Sim.Sensors;

public enum RadarScope
{
    Front,
    Rear,
}

/// <summary>
/// One blip. X/Y are in [-1, 1] on the scope: distance from scope center is the angle off the
/// scope's axis (nose for Front, tail for Rear) divided by 90°. [SPEC S-03]
/// </summary>
public readonly record struct RadarBlip(int ShipId, RadarScope Scope, float X, float Y, Iff Iff, bool IsTarget, bool IsCapital, float Distance);

public static class Radar
{
    public static List<RadarBlip> Scan(World world, Ship viewer, float maxRange = 20000f)
    {
        var blips = new List<RadarBlip>();
        foreach (Ship s in world.Ships)
        {
            if (!s.Alive || s.Id == viewer.Id) continue;
            Vector3 delta = s.Position - viewer.Position;
            float dist = delta.Length();
            if (dist > maxRange || dist < 1e-3f) continue;
            blips.Add(Project(s, viewer, SimMath.ToLocal(viewer.Orientation, delta / dist), dist));
        }
        return blips;
    }

    public static RadarBlip Project(Ship s, Ship viewer, Vector3 localDir, float dist)
    {
        bool front = localDir.Z <= 0f;
        // Angle from the scope's axis, and the screen-space direction of the offset.
        float axial = front ? -localDir.Z : localDir.Z;
        float angle = MathF.Acos(SimMath.Clamp(axial, -1f, 1f));
        // Rear scope is drawn as if looking backward over your shoulder: left/right mirror.
        float sx = front ? localDir.X : -localDir.X;
        float sy = localDir.Y;
        float len = MathF.Sqrt(sx * sx + sy * sy);
        float r = angle / (MathF.PI / 2f);
        float x = len > 1e-6f ? sx / len * r : 0f;
        float y = len > 1e-6f ? sy / len * r : 0f;
        return new RadarBlip(s.Id, front ? RadarScope.Front : RadarScope.Rear, x, y, s.Iff,
            viewer.TargetId == s.Id, s.Class.Category == Data.ShipCategory.Capital, dist);
    }
}
