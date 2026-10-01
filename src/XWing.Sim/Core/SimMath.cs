using System.Numerics;

namespace XWing.Sim.Core;

/// <summary>
/// Axis conventions (same as Godot so the client needs no conversion):
/// right = +X, up = +Y, forward (nose) = -Z. Distances in meters, time in seconds.
/// </summary>
public static class SimMath
{
    public const float Deg2Rad = MathF.PI / 180f;
    public const float Rad2Deg = 180f / MathF.PI;

    public static readonly Vector3 LocalForward = new(0, 0, -1);
    public static readonly Vector3 LocalUp = Vector3.UnitY;
    public static readonly Vector3 LocalRight = Vector3.UnitX;

    public static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
    public static float Clamp01(float v) => Clamp(v, 0f, 1f);

    public static float MoveTowards(float current, float target, float maxDelta)
    {
        if (MathF.Abs(target - current) <= maxDelta) return target;
        return current + MathF.Sign(target - current) * maxDelta;
    }

    /// <summary>Orientation from a compass heading (degrees, positive = right of -Z) and pitch (positive = nose up).</summary>
    public static Quaternion FromHeadingPitch(float headingDeg, float pitchDeg) =>
        Quaternion.Normalize(Quaternion.CreateFromYawPitchRoll(-headingDeg * Deg2Rad, pitchDeg * Deg2Rad, 0f));

    public static Vector3 ToLocal(Quaternion orientation, Vector3 worldDirection) =>
        Vector3.Transform(worldDirection, Quaternion.Conjugate(orientation));

    /// <summary>Angle in radians between the nose and a local-space direction.</summary>
    public static float OffNoseAngle(Vector3 localDirection)
    {
        float len = localDirection.Length();
        if (len < 1e-6f) return 0f;
        return MathF.Acos(Clamp(-localDirection.Z / len, -1f, 1f));
    }

    /// <summary>
    /// First-order lead: where to aim so a projectile of <paramref name="projectileSpeed"/> fired from
    /// <paramref name="shooter"/> meets a target moving at constant velocity. Two fixed-point iterations.
    /// </summary>
    public static Vector3 LeadPoint(Vector3 shooter, Vector3 shooterVelocity, Vector3 target, Vector3 targetVelocity, float projectileSpeed)
    {
        Vector3 relVel = targetVelocity - shooterVelocity;
        Vector3 aim = target;
        for (int i = 0; i < 2; i++)
        {
            float t = Vector3.Distance(shooter, aim) / MathF.Max(projectileSpeed, 1f);
            aim = target + relVel * t;
        }
        return aim;
    }

    /// <summary>
    /// Segment-vs-sphere test. Returns the fraction [0,1] along p0→p1 of first contact, or null.
    /// Used for bolts so fast projectiles cannot tunnel through small ships between ticks.
    /// </summary>
    public static float? SegmentSphere(Vector3 p0, Vector3 p1, Vector3 center, float radius)
    {
        Vector3 d = p1 - p0;
        Vector3 m = p0 - center;
        float c = Vector3.Dot(m, m) - radius * radius;
        if (c <= 0f) return 0f;
        float a = Vector3.Dot(d, d);
        if (a < 1e-9f) return null;
        float b = Vector3.Dot(m, d);
        if (b > 0f) return null;
        float disc = b * b - a * c;
        if (disc < 0f) return null;
        float t = (-b - MathF.Sqrt(disc)) / a;
        return t is >= 0f and <= 1f ? t : null;
    }
}
