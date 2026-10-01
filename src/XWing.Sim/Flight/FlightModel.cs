using System.Numerics;
using XWing.Sim.Core;
using XWing.Sim.Data;

namespace XWing.Sim.Flight;

/// <summary>
/// The X-Wing (1993) flight model as currently understood: an "aircraft in invisible air" model
/// inherited from Secret Weapons of the Luftwaffe, not Newtonian physics.
///  - Velocity is always along the nose. No drift, no inertia in direction. [SPEC F-01]
///  - Throttle sets a target speed; speed moves toward it at fixed accel/decel. [SPEC F-02]
///  - Turn rates are scaled by a speed-dependent maneuver curve. [SPEC F-03]
/// </summary>
public static class FlightModel
{
    public static void Step(Ship ship, SimRules rules, float dt)
    {
        ShipClass cls = ship.Class;
        ShipControls c = ship.Controls;

        float targetSpeed = SimMath.Clamp01(c.Throttle) * cls.MaxSpeed * Combat.EnergySystem.EngineFactor(ship, rules);
        float rate = targetSpeed > ship.Speed ? cls.Acceleration : cls.Deceleration;
        ship.Speed = SimMath.MoveTowards(ship.Speed, targetSpeed, rate * dt);

        float speedFraction = cls.MaxSpeed > 0f ? ship.Speed / cls.MaxSpeed : 0f;
        float maneuver = rules.ManeuverCurve.Evaluate(speedFraction);

        // Local axes: +X pitch-up, +Y yaw-left, +Z roll-left (right-handed, nose = -Z).
        var commanded = new Vector3(
            SimMath.Clamp(c.Pitch, -1f, 1f) * cls.PitchRate,
            -SimMath.Clamp(c.Yaw, -1f, 1f) * cls.YawRate,
            -SimMath.Clamp(c.Roll, -1f, 1f) * cls.RollRate) * (maneuver * SimMath.Deg2Rad);

        if (rules.AngularResponseTime <= 0f)
        {
            ship.AngularVelocity = commanded;
        }
        else
        {
            float k = 1f - MathF.Exp(-dt / rules.AngularResponseTime);
            ship.AngularVelocity += (commanded - ship.AngularVelocity) * k;
        }

        Vector3 w = ship.AngularVelocity * dt;
        float angle = w.Length();
        if (angle > 1e-9f)
        {
            Quaternion delta = Quaternion.CreateFromAxisAngle(w / angle, angle);
            ship.Orientation = Quaternion.Normalize(ship.Orientation * delta);
        }

        ship.Position += ship.Forward * (ship.Speed * dt);
    }
}
