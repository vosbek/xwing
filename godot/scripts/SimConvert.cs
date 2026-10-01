using Godot;
using N = System.Numerics;

namespace XWing.Client;

/// <summary>The sim uses Godot's axis conventions, so conversion is a straight copy.</summary>
public static class SimConvert
{
    public static Vector3 ToGodot(this N.Vector3 v) => new(v.X, v.Y, v.Z);
    public static Quaternion ToGodot(this N.Quaternion q) => new Quaternion(q.X, q.Y, q.Z, q.W).Normalized();
}
