using XWing.Sim.Core;
using XWing.Sim.Data;

namespace XWing.Sim.Combat;

/// <summary>
/// Engine / laser / shield power distribution.
/// Model: lasers and shields each have a recharge level (Off, Normal, Increased, Maximum).
/// Every level above Normal costs engine power; Off on a system gives power back. [SPEC E-01..E-05]
/// </summary>
public static class EnergySystem
{
    public static float EngineFactor(Ship ship, SimRules rules)
    {
        int levels = 0;
        if (ship.Class.LaserCount > 0) levels += (int)ship.LaserRecharge - (int)RechargeLevel.Normal;
        if (ship.Class.HasShields) levels += (int)ship.ShieldRecharge - (int)RechargeLevel.Normal;
        return SimMath.Clamp(1f - levels * rules.EngineCostPerLevel, rules.EngineFactorMin, rules.EngineFactorMax);
    }

    public static void Step(Ship ship, SimRules rules, float dt)
    {
        ShipClass cls = ship.Class;

        float laserGain = cls.LaserRechargeRate * (int)ship.LaserRecharge * dt;
        for (int i = 0; i < ship.LaserCharge.Length; i++)
            ship.LaserCharge[i] = MathF.Min(1f, ship.LaserCharge[i] + laserGain);

        if (!cls.HasShields) return;

        float gain = cls.ShieldRechargeRate * (int)ship.ShieldRecharge * dt;
        switch (ship.ShieldFocus)
        {
            case ShieldFocus.Even:
                AddShield(ship, gain * 0.5f, front: true);
                AddShield(ship, gain * 0.5f, front: false);
                break;
            case ShieldFocus.Front:
                AddShield(ship, gain, front: true);
                MoveShield(ship, rules.ShieldFocusTransferRate * dt, toFront: true);
                break;
            case ShieldFocus.Rear:
                AddShield(ship, gain, front: false);
                MoveShield(ship, rules.ShieldFocusTransferRate * dt, toFront: false);
                break;
        }
    }

    /// <summary>Adds shield points to one side; anything that does not fit spills to the other side.</summary>
    private static void AddShield(Ship ship, float amount, bool front)
    {
        float cap = ship.Class.ShieldCapacity;
        ref float primary = ref (front ? ref ship.ShieldFront : ref ship.ShieldRear);
        ref float other = ref (front ? ref ship.ShieldRear : ref ship.ShieldFront);
        float fit = MathF.Min(amount, cap - primary);
        primary += fit;
        other = MathF.Min(cap, other + (amount - fit));
    }

    private static void MoveShield(Ship ship, float amount, bool toFront)
    {
        float cap = ship.Class.ShieldCapacity;
        ref float to = ref (toFront ? ref ship.ShieldFront : ref ship.ShieldRear);
        ref float from = ref (toFront ? ref ship.ShieldRear : ref ship.ShieldFront);
        float moved = MathF.Min(amount, MathF.Min(from, cap - to));
        from -= moved;
        to += moved;
    }

    public static void TransferLasersToShields(Ship ship, SimRules rules)
    {
        if (!ship.Class.HasShields || ship.LaserCharge.Length == 0) return;
        float room = 2f * ship.Class.ShieldCapacity - ship.ShieldFront - ship.ShieldRear;
        float maxCharge = MathF.Min(rules.TransferChunk, room / rules.TransferShieldPerCharge / ship.LaserCharge.Length);
        float taken = 0f;
        for (int i = 0; i < ship.LaserCharge.Length; i++)
        {
            float t = MathF.Min(maxCharge, ship.LaserCharge[i]);
            ship.LaserCharge[i] -= t;
            taken += t;
        }
        float shield = taken * rules.TransferShieldPerCharge;
        AddShield(ship, shield * 0.5f, front: true);
        AddShield(ship, shield * 0.5f, front: false);
    }

    public static void TransferShieldsToLasers(Ship ship, SimRules rules)
    {
        if (!ship.Class.HasShields || ship.LaserCharge.Length == 0) return;
        float want = 0f;
        foreach (float c in ship.LaserCharge) want += MathF.Min(rules.TransferChunk, 1f - c);
        float shieldNeeded = want * rules.TransferShieldPerCharge;
        float available = ship.ShieldFront + ship.ShieldRear;
        float used = MathF.Min(shieldNeeded, available);
        if (used <= 0f) return;

        float frontShare = available > 0 ? ship.ShieldFront / available : 0.5f;
        ship.ShieldFront -= used * frontShare;
        ship.ShieldRear -= used * (1f - frontShare);

        float scale = used / shieldNeeded;
        for (int i = 0; i < ship.LaserCharge.Length; i++)
            ship.LaserCharge[i] = MathF.Min(1f, ship.LaserCharge[i] + MathF.Min(rules.TransferChunk, 1f - ship.LaserCharge[i]) * scale);
    }

    public static RechargeLevel Next(RechargeLevel level) => (RechargeLevel)(((int)level + 1) % 4);
}
