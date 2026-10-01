using System.Numerics;
using XWing.Sim.Core;
using XWing.Sim.Data;

namespace XWing.Sim.Sensors;

/// <summary>What the cockpit's targeting computer (CMD) displays.</summary>
public readonly record struct TargetInfo(
    int ShipId,
    string Callsign,
    string ClassName,
    Iff Iff,
    float Distance,
    float Speed,
    float ShieldFraction,
    float HullFraction,
    /// <summary>Target direction in the viewer's local frame, normalized.</summary>
    Vector3 LocalDirection);

public static class Targeting
{
    /// <summary>T / Y keys: step through every other live craft in id order. [SPEC S-01]</summary>
    public static int? Cycle(World world, Ship viewer, int direction)
    {
        List<Ship> candidates = world.Ships.Where(s => s.Alive && s.Id != viewer.Id).OrderBy(s => s.Id).ToList();
        if (candidates.Count == 0) return null;
        int current = viewer.TargetId is { } id ? candidates.FindIndex(s => s.Id == id) : -1;
        int next = current < 0
            ? (direction >= 0 ? 0 : candidates.Count - 1)
            : ((current + direction) % candidates.Count + candidates.Count) % candidates.Count;
        return candidates[next].Id;
    }

    /// <summary>R key: nearest hostile, fighters preferred. [SPEC S-02]</summary>
    public static int? NearestEnemy(World world, Ship viewer, bool fightersFirst = true)
    {
        Ship? best = null;
        float bestScore = float.MaxValue;
        foreach (Ship s in world.Ships)
        {
            if (!s.Alive || !viewer.Iff.IsHostileTo(s.Iff)) continue;
            float score = Vector3.DistanceSquared(viewer.Position, s.Position);
            if (fightersFirst && s.Class.Category == ShipCategory.Capital) score += 1e12f;
            if (score < bestScore) { bestScore = score; best = s; }
        }
        return best?.Id;
    }

    public static TargetInfo? Describe(World world, Ship viewer)
    {
        if (viewer.TargetId is not { } id || world.FindShip(id) is not { Alive: true } t) return null;
        Vector3 delta = t.Position - viewer.Position;
        float dist = delta.Length();
        Vector3 local = dist > 1e-4f ? SimMath.ToLocal(viewer.Orientation, delta / dist) : SimMath.LocalForward;
        return new TargetInfo(t.Id, t.Callsign, t.Class.Name, t.Iff, dist, t.Speed, t.ShieldFraction, t.HullFraction, local);
    }
}
