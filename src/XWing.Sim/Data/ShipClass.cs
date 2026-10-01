using System.Numerics;

namespace XWing.Sim.Data;

public enum ShipCategory
{
    Fighter,
    Bomber,
    Capital,
}

/// <summary>
/// Static per-class parameters. Every number here is a claim about the original game and is
/// tracked in docs/SPEC.md with its provenance (placeholder / community / measured / disassembled).
/// </summary>
public sealed class ShipClass
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public ShipCategory Category { get; init; }

    /// <summary>Where these numbers came from. See docs/SPEC.md "Provenance levels".</summary>
    public string Provenance { get; init; } = "placeholder";

    // Flight. Speeds in m/s, accelerations in m/s², rates in deg/s at maneuver-curve multiplier 1.0.
    public float MaxSpeed { get; init; }
    public float Acceleration { get; init; }
    public float Deceleration { get; init; }
    public float PitchRate { get; init; }
    public float YawRate { get; init; }
    public float RollRate { get; init; }

    // Survivability. Shield capacity is per side (front and rear); 0 = unshielded.
    public float Hull { get; init; }
    public float ShieldCapacity { get; init; }
    /// <summary>Shield points per second at recharge level 1 ("normal").</summary>
    public float ShieldRechargeRate { get; init; }
    public float CollisionRadius { get; init; } = 6f;

    // Forward lasers. Charge is per cannon in [0,1].
    public List<Vector3> LaserMounts { get; init; } = new();
    public float LaserDamage { get; init; }
    public float LaserBoltSpeed { get; init; }
    public float LaserRange { get; init; }
    /// <summary>Charge consumed per bolt (fraction of one cannon's capacity).</summary>
    public float LaserShotCost { get; init; }
    /// <summary>Charge per second per cannon at recharge level 1.</summary>
    public float LaserRechargeRate { get; init; }
    /// <summary>Minimum seconds between trigger pulls.</summary>
    public float LaserCooldown { get; init; }

    public TurretSpec? Turrets { get; init; }

    public bool HasHyperdrive { get; init; }
    public float HyperspaceChargeTime { get; init; } = 3f;

    public int LaserCount => LaserMounts.Count;
    public bool HasShields => ShieldCapacity > 0f;
}

/// <summary>Capital-ship point defense. Turrets have no firing arcs yet (open question in SPEC).</summary>
public sealed class TurretSpec
{
    public List<Vector3> Mounts { get; init; } = new();
    public float Damage { get; init; }
    public float BoltSpeed { get; init; }
    public float Range { get; init; }
    public float Cooldown { get; init; }
    /// <summary>1-sigma-ish aim error in degrees, applied as a uniform cone.</summary>
    public float AimErrorDeg { get; init; }
}
