using System.Numerics;
using XWing.Sim.Core;

namespace XWing.Sim.Combat;

public sealed class Projectile
{
    public int OwnerId;
    public Iff OwnerIff;
    public Vector3 Position;
    public Vector3 Velocity;
    public float Damage;
    public float TimeLeft;
    public bool Spent;
    /// <summary>True for capital-ship turbolasers (rendered differently).</summary>
    public bool Heavy;
}
