using System.Numerics;
using XWing.Sim.Core;

namespace XWing.Sim.Combat;

/// <summary>
/// Shields absorb damage on the side that was hit (front/rear hemisphere); overflow goes to hull.
/// Friendly fire is on. [SPEC C-01..C-03]
/// </summary>
public static class DamageModel
{
    /// <param name="impactPoint">World-space point where the damage arrived.</param>
    public static void Apply(World world, Ship ship, float damage, Vector3 impactPoint, int? attackerId)
    {
        if (!ship.Alive || damage <= 0f) return;

        Vector3 local = SimMath.ToLocal(ship.Orientation, impactPoint - ship.Position);
        bool front = local.Z <= 0f;
        ref float shield = ref (front ? ref ship.ShieldFront : ref ship.ShieldRear);

        float toShield = MathF.Min(shield, damage);
        shield -= toShield;
        float toHull = damage - toShield;
        ship.Hull -= toHull;
        ship.LastHitTime = world.Time;
        ship.LastAttackerId = attackerId;

        world.Emit(new ShipHit(world.Time, ship.Id, attackerId, toShield, toHull));

        if (ship.Hull <= 0f)
        {
            ship.Hull = 0f;
            ship.Speed = 0f;
            world.Emit(new ShipDestroyed(world.Time, ship.Id, ship.Callsign, attackerId));
        }
    }
}
