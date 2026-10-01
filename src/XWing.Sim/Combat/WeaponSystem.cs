using System.Numerics;
using XWing.Sim.Core;
using XWing.Sim.Data;

namespace XWing.Sim.Combat;

public static class WeaponSystem
{
    public static int CannonsPerShot(Ship ship) => ship.FireMode switch
    {
        FireMode.Dual => Math.Min(2, ship.Class.LaserCount),
        FireMode.Quad => Math.Min(4, ship.Class.LaserCount),
        _ => Math.Min(1, ship.Class.LaserCount),
    };

    public static FireMode NextFireMode(Ship ship)
    {
        int n = ship.Class.LaserCount;
        return ship.FireMode switch
        {
            FireMode.Single when n >= 2 => FireMode.Dual,
            FireMode.Dual when n >= 4 => FireMode.Quad,
            _ => FireMode.Single,
        };
    }

    /// <summary>
    /// Forward lasers. Cannons fire in rotation; linked modes fire 2 or 4 at once.
    /// A shot needs every participating cannon to have at least ShotCost charge. [SPEC W-01..W-04]
    /// </summary>
    public static void StepLasers(World world, Ship ship, float dt)
    {
        ship.LaserCooldown = MathF.Max(0f, ship.LaserCooldown - dt);
        ShipClass cls = ship.Class;
        if (!ship.Controls.Fire || cls.LaserCount == 0 || ship.LaserCooldown > 0f) return;
        if (ship.Hyperspace != HyperspaceState.None) return;

        int count = CannonsPerShot(ship);
        Span<int> cannons = stackalloc int[count];
        for (int i = 0; i < count; i++)
        {
            int idx = (ship.NextCannon + i) % cls.LaserCount;
            if (ship.LaserCharge[idx] < cls.LaserShotCost)
            {
                ship.LaserCooldown = cls.LaserCooldown;
                world.Emit(new LaserDry(world.Time, ship.Id));
                return;
            }
            cannons[i] = idx;
        }

        Vector3 forward = ship.Forward;
        foreach (int idx in cannons)
        {
            ship.LaserCharge[idx] -= cls.LaserShotCost;
            Vector3 muzzle = ship.Position + Vector3.Transform(cls.LaserMounts[idx], ship.Orientation);
            world.SpawnProjectile(new Projectile
            {
                OwnerId = ship.Id,
                OwnerIff = ship.Iff,
                Position = muzzle,
                Velocity = forward * (cls.LaserBoltSpeed + ship.Speed),
                Damage = cls.LaserDamage,
                TimeLeft = cls.LaserRange / cls.LaserBoltSpeed,
            });
        }
        ship.NextCannon = (ship.NextCannon + count) % cls.LaserCount;
        ship.LaserCooldown = cls.LaserCooldown;
        world.Emit(new LaserFired(world.Time, ship.Id, count));
    }

    /// <summary>Capital-ship turrets: each mount independently engages the nearest hostile in range.</summary>
    public static void StepTurrets(World world, Ship ship, float dt)
    {
        TurretSpec? spec = ship.Class.Turrets;
        if (spec is null || ship.Hyperspace != HyperspaceState.None) return;

        for (int t = 0; t < spec.Mounts.Count; t++)
        {
            ship.TurretCooldowns[t] = MathF.Max(0f, ship.TurretCooldowns[t] - dt);
            if (ship.TurretCooldowns[t] > 0f) continue;

            Vector3 mount = ship.Position + Vector3.Transform(spec.Mounts[t], ship.Orientation);
            Ship? target = null;
            float best = spec.Range * spec.Range;
            foreach (Ship other in world.Ships)
            {
                if (!other.Alive || !ship.Iff.IsHostileTo(other.Iff)) continue;
                float d2 = Vector3.DistanceSquared(mount, other.Position);
                if (d2 < best) { best = d2; target = other; }
            }
            if (target is null) continue;

            Vector3 aim = SimMath.LeadPoint(mount, ship.Velocity, target.Position, target.Velocity, spec.BoltSpeed);
            Vector3 dir = Vector3.Normalize(aim - mount);
            dir = Jitter(world.Rng, dir, spec.AimErrorDeg * SimMath.Deg2Rad);

            world.SpawnProjectile(new Projectile
            {
                OwnerId = ship.Id,
                OwnerIff = ship.Iff,
                Position = mount,
                Velocity = dir * spec.BoltSpeed + ship.Velocity,
                Damage = spec.Damage,
                TimeLeft = spec.Range / spec.BoltSpeed,
                Heavy = true,
            });
            ship.TurretCooldowns[t] = spec.Cooldown * world.Rng.Range(0.85f, 1.15f);
        }
    }

    /// <summary>Rotates a unit direction by a random angle up to <paramref name="maxAngle"/> radians.</summary>
    public static Vector3 Jitter(DeterministicRandom rng, Vector3 dir, float maxAngle)
    {
        if (maxAngle <= 0f) return dir;
        Vector3 axis = Vector3.Cross(dir, MathF.Abs(dir.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX);
        axis = Vector3.Normalize(axis);
        Quaternion spin = Quaternion.CreateFromAxisAngle(dir, rng.Range(0f, 2f * MathF.PI));
        axis = Vector3.Transform(axis, spin);
        return Vector3.Normalize(Vector3.Transform(dir, Quaternion.CreateFromAxisAngle(axis, rng.Range(0f, maxAngle))));
    }

    public static void StepProjectiles(World world, float dt)
    {
        foreach (Projectile p in world.Projectiles)
        {
            if (p.Spent) continue;
            Vector3 start = p.Position;
            Vector3 end = start + p.Velocity * dt;

            Ship? hit = null;
            float bestT = float.MaxValue;
            foreach (Ship ship in world.Ships)
            {
                if (!ship.Alive || ship.Id == p.OwnerId) continue;
                float? t = SimMath.SegmentSphere(start, end, ship.Position, ship.Class.CollisionRadius);
                if (t is { } tv && tv < bestT) { bestT = tv; hit = ship; }
            }

            if (hit is not null)
            {
                Vector3 impact = Vector3.Lerp(start, end, bestT);
                DamageModel.Apply(world, hit, p.Damage, impact, p.OwnerId);
                p.Spent = true;
                continue;
            }

            p.Position = end;
            p.TimeLeft -= dt;
            if (p.TimeLeft <= 0f) p.Spent = true;
        }
        world.Projectiles.RemoveAll(p => p.Spent);
    }

    /// <summary>Sphere-sphere ship collisions. Both take damage proportional to closing speed. [SPEC C-04]</summary>
    public static void StepCollisions(World world)
    {
        var ships = world.Ships;
        for (int i = 0; i < ships.Count; i++)
        {
            Ship a = ships[i];
            if (!a.Alive) continue;
            for (int j = i + 1; j < ships.Count; j++)
            {
                Ship b = ships[j];
                if (!b.Alive) continue;
                float r = a.Class.CollisionRadius + b.Class.CollisionRadius;
                Vector3 delta = b.Position - a.Position;
                float d2 = delta.LengthSquared();
                if (d2 >= r * r) continue;

                float dist = MathF.Sqrt(d2);
                Vector3 n = dist > 1e-4f ? delta / dist : Vector3.UnitY;
                float closing = MathF.Max(0f, Vector3.Dot(a.Velocity - b.Velocity, n));
                float damage = MathF.Max(5f, closing * world.Rules.CollisionDamagePerSpeed);

                // Push apart, lighter ship (smaller radius) moves more.
                float overlap = r - dist;
                float wa = b.Class.CollisionRadius / r;
                a.Position -= n * (overlap * wa);
                b.Position += n * (overlap * (1f - wa));

                world.Emit(new ShipCollision(world.Time, a.Id, b.Id, damage));
                Vector3 contact = a.Position + n * a.Class.CollisionRadius;
                DamageModel.Apply(world, a, damage, contact, b.Id);
                DamageModel.Apply(world, b, damage, contact, a.Id);
            }
        }
    }
}
