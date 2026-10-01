using System.Globalization;
using System.Numerics;
using XWing.Sim.Core;
using XWing.Sim.Data;

namespace XWing.Sim.Research;

/// <summary>One number the engine claims about the original game.</summary>
public sealed record Measurement(string TestId, string Ship, string Condition, double Value, string Unit);

/// <summary>
/// A battery of isolated test-bench experiments that mirror exactly what we measure by hand in
/// DOSBox (see research/measurements/PROTOCOL.md). Same test ids on both sides means the
/// comparison is mechanical: <see cref="MeasurementComparer"/>.
/// </summary>
public static class FlightTests
{
    private const float Timeout = 600f;

    public static IEnumerable<Measurement> RunAll(ShipCatalog catalog, SimRules? rules = null)
    {
        rules ??= new SimRules();
        foreach (ShipClass cls in catalog.All.OrderBy(c => c.Id))
            foreach (Measurement m in RunFor(cls, rules))
                yield return m;
    }

    public static IEnumerable<Measurement> RunFor(ShipClass cls, SimRules rules)
    {
        string id = cls.Id;

        foreach (float throttle in new[] { 1f / 3f, 2f / 3f, 1f })
            yield return new("flight.max_speed", id, $"throttle={Fmt(throttle)}", MaxSpeed(cls, rules, throttle), "m/s");

        yield return new("flight.accel_0_to_full", id, "throttle=1", TimeToSpeed(cls, rules, from: 0f, toThrottle: 1f), "s");
        yield return new("flight.decel_full_to_0", id, "throttle=0", TimeToSpeed(cls, rules, from: 1f, toThrottle: 0f), "s");

        foreach (float throttle in new[] { 0f, 1f / 3f, 2f / 3f, 1f })
        {
            yield return new("flight.yaw_360", id, $"throttle={Fmt(throttle)}", Turn360(cls, rules, throttle, Axis.Yaw), "s");
            yield return new("flight.pitch_360", id, $"throttle={Fmt(throttle)}", Turn360(cls, rules, throttle, Axis.Pitch), "s");
            yield return new("flight.roll_360", id, $"throttle={Fmt(throttle)}", Turn360(cls, rules, throttle, Axis.Roll), "s");
        }

        if (cls.LaserCount > 0)
        {
            foreach (RechargeLevel level in Enum.GetValues<RechargeLevel>())
                yield return new("energy.max_speed_vs_laser_recharge", id, $"lasers={level}", MaxSpeed(cls, rules, 1f, lasers: level), "m/s");
            foreach (RechargeLevel level in new[] { RechargeLevel.Normal, RechargeLevel.Increased, RechargeLevel.Maximum })
                yield return new("lasers.recharge_empty_to_full", id, $"lasers={level}", LaserRecharge(cls, rules, level), "s");
            yield return new("lasers.shots_full_to_dry", id, "mode=Single", ShotsUntilDry(cls, rules), "shots");
        }

        if (cls.HasShields)
        {
            foreach (RechargeLevel level in Enum.GetValues<RechargeLevel>())
                yield return new("energy.max_speed_vs_shield_recharge", id, $"shields={level}", MaxSpeed(cls, rules, 1f, shields: level), "m/s");
            foreach (RechargeLevel level in new[] { RechargeLevel.Normal, RechargeLevel.Increased, RechargeLevel.Maximum })
                yield return new("shields.recharge_empty_to_full", id, $"shields={level}", ShieldRecharge(cls, rules, level), "s");
        }
    }

    private enum Axis { Yaw, Pitch, Roll }

    private static (World world, Ship ship) Bench(ShipClass cls, SimRules rules)
    {
        var world = new World(new ShipCatalog(new[] { cls }), rules);
        Ship ship = world.Spawn(cls.Id, Iff.Rebel, "Test", 0, Vector3.Zero, Quaternion.Identity);
        return (world, ship);
    }

    private static double MaxSpeed(ShipClass cls, SimRules rules, float throttle,
        RechargeLevel lasers = RechargeLevel.Normal, RechargeLevel shields = RechargeLevel.Normal)
    {
        var (world, ship) = Bench(cls, rules);
        if (cls.LaserCount > 0) ship.LaserRecharge = lasers;
        if (cls.HasShields) ship.ShieldRecharge = shields;
        ship.Controls.Throttle = throttle;
        world.Run(Timeout / 10f);
        return Round(ship.Speed);
    }

    private static double TimeToSpeed(ShipClass cls, SimRules rules, float from, float toThrottle)
    {
        var (world, ship) = Bench(cls, rules);
        ship.Speed = from * cls.MaxSpeed;
        ship.Controls.Throttle = toThrottle;
        float target = toThrottle * cls.MaxSpeed;
        while (MathF.Abs(ship.Speed - target) > 0.01f * cls.MaxSpeed && world.Time < Timeout) world.Step();
        return Round(world.Time);
    }

    private static double Turn360(ShipClass cls, SimRules rules, float throttle, Axis axis)
    {
        var (world, ship) = Bench(cls, rules);
        ship.Speed = throttle * cls.MaxSpeed;
        ship.Controls = new ShipControls
        {
            Throttle = throttle,
            Yaw = axis == Axis.Yaw ? 1f : 0f,
            Pitch = axis == Axis.Pitch ? 1f : 0f,
            Roll = axis == Axis.Roll ? 1f : 0f,
        };
        // Integrate the turned angle; robust for any rate, unlike watching the heading wrap.
        double turned = 0;
        Quaternion prev = ship.Orientation;
        while (turned < 2 * Math.PI && world.Time < Timeout)
        {
            world.Step();
            Quaternion delta = Quaternion.Conjugate(prev) * ship.Orientation;
            turned += 2 * Math.Acos(Math.Min(1.0, Math.Abs(delta.W)));
            prev = ship.Orientation;
        }
        return world.Time >= Timeout ? double.PositiveInfinity : Round(world.Time);
    }

    private static double LaserRecharge(ShipClass cls, SimRules rules, RechargeLevel level)
    {
        var (world, ship) = Bench(cls, rules);
        ship.LaserRecharge = level;
        Array.Fill(ship.LaserCharge, 0f);
        while (ship.LaserCharge.Min() < 1f && world.Time < Timeout) world.Step();
        return Round(world.Time);
    }

    private static double ShieldRecharge(ShipClass cls, SimRules rules, RechargeLevel level)
    {
        var (world, ship) = Bench(cls, rules);
        ship.ShieldRecharge = level;
        ship.ShieldFront = ship.ShieldRear = 0f;
        while (ship.ShieldFront + ship.ShieldRear < 2f * cls.ShieldCapacity - 1e-3f && world.Time < Timeout) world.Step();
        return Round(world.Time);
    }

    private static double ShotsUntilDry(ShipClass cls, SimRules rules)
    {
        var (world, ship) = Bench(cls, rules);
        ship.Controls.Fire = true;
        int shots = 0;
        while (world.Time < 120f)
        {
            world.Step();
            foreach (SimEvent e in world.TickEvents)
            {
                if (e is LaserFired) shots++;
                if (e is LaserDry) return shots;
            }
        }
        return double.PositiveInfinity;
    }

    private static double Round(double v) => Math.Round(v, 3);

    private static string Fmt(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
}
