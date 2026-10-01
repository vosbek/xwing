using System.Numerics;
using XWing.Sim.AI;
using XWing.Sim.Data;

namespace XWing.Sim.Core;

public enum RechargeLevel
{
    Off = 0,
    Normal = 1,
    Increased = 2,
    Maximum = 3,
}

public enum FireMode
{
    Single,
    Dual,
    Quad,
}

public enum ShieldFocus
{
    Even,
    Front,
    Rear,
}

public enum HyperspaceState
{
    None,
    Charging,
    Departed,
}

/// <summary>Continuous controls, sampled once per tick. Stick axes in [-1, 1], throttle in [0, 1].</summary>
public struct ShipControls
{
    public float Pitch;
    public float Yaw;
    public float Roll;
    public float Throttle;
    public bool Fire;
}

/// <summary>Discrete cockpit actions (key presses). Queued and applied at the start of the next tick.</summary>
public enum ShipCommand
{
    TargetNext,
    TargetPrevious,
    TargetNearestEnemy,
    TargetNone,
    CycleFireMode,
    CycleLaserRecharge,
    CycleShieldRecharge,
    CycleShieldFocus,
    TransferLasersToShields,
    TransferShieldsToLasers,
    Hyperspace,
}

public sealed class Ship
{
    public Ship(int id, ShipClass shipClass, Iff iff, string flightGroup, int indexInGroup)
    {
        Id = id;
        Class = shipClass;
        Iff = iff;
        FlightGroup = flightGroup;
        IndexInGroup = indexInGroup;
        Hull = shipClass.Hull;
        ShieldFront = shipClass.ShieldCapacity;
        ShieldRear = shipClass.ShieldCapacity;
        LaserCharge = Enumerable.Repeat(1f, shipClass.LaserCount).ToArray();
        TurretCooldowns = new float[shipClass.Turrets?.Mounts.Count ?? 0];
        LaserRecharge = shipClass.LaserCount > 0 ? RechargeLevel.Normal : RechargeLevel.Off;
        ShieldRecharge = shipClass.HasShields ? RechargeLevel.Normal : RechargeLevel.Off;
    }

    public int Id { get; }
    public ShipClass Class { get; }
    public Iff Iff { get; }
    public string FlightGroup { get; }
    public int IndexInGroup { get; }
    public string Callsign => $"{FlightGroup} {IndexInGroup + 1}";
    public bool IsPlayer { get; set; }

    // Kinematics. Velocity is always Forward * Speed: the X-Wing flight model has no drift. [SPEC F-01]
    public Vector3 Position;
    public Quaternion Orientation = Quaternion.Identity;
    public float Speed;
    /// <summary>Local-space angular velocity in rad/s (x = pitch, y = yaw-left, z = roll-left).</summary>
    public Vector3 AngularVelocity;

    public Vector3 Forward => Vector3.Transform(SimMath.LocalForward, Orientation);
    public Vector3 Up => Vector3.Transform(SimMath.LocalUp, Orientation);
    public Vector3 Right => Vector3.Transform(SimMath.LocalRight, Orientation);
    public Vector3 Velocity => Forward * Speed;

    // Damage model
    public float Hull;
    public float ShieldFront;
    public float ShieldRear;
    public bool Alive => Hull > 0f && Hyperspace != HyperspaceState.Departed;
    public bool Destroyed => Hull <= 0f;
    public float LastHitTime = float.NegativeInfinity;
    public int? LastAttackerId;

    // Energy & weapons
    public float[] LaserCharge;
    public int NextCannon;
    public float LaserCooldown;
    public FireMode FireMode;
    public RechargeLevel LaserRecharge;
    public RechargeLevel ShieldRecharge;
    public ShieldFocus ShieldFocus;
    public float[] TurretCooldowns;

    public HyperspaceState Hyperspace;
    public float HyperspaceTimer;

    public int? TargetId;

    public ShipControls Controls;
    public Queue<ShipCommand> PendingCommands { get; } = new();
    public PilotAI? Pilot { get; set; }

    public float ShieldFraction => Class.HasShields ? (ShieldFront + ShieldRear) / (2f * Class.ShieldCapacity) : 0f;
    public float HullFraction => Class.Hull > 0 ? MathF.Max(Hull, 0f) / Class.Hull : 0f;
    public float AverageLaserCharge => LaserCharge.Length == 0 ? 0f : LaserCharge.Average();

    public void Command(ShipCommand command) => PendingCommands.Enqueue(command);

    public override string ToString() => $"{Callsign} ({Class.Name}, #{Id})";
}
