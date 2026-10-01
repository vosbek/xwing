using System.Numerics;
using XWing.Sim.Combat;
using XWing.Sim.Core;
using XWing.Sim.Data;
using XWing.Sim.Sensors;

namespace XWing.Sim.AI;

public enum PilotState
{
    Cruise,
    Attack,
    Evade,
}

/// <summary>
/// Fighter AI: decides at a skill-dependent reaction interval, steers every tick.
/// It drives the same <see cref="ShipControls"/> a human does, so AI ships obey the same
/// flight model and energy rules as the player. [SPEC A-01]
/// </summary>
public sealed class PilotAI
{
    private float _thinkTimer;
    private float _evadeTimer;
    private Vector3 _evadeLocalDir;
    private Vector3 _aimOffset;
    private float _lastSeenHit = float.NegativeInfinity;
    private int _waypoint;
    private float _stalemate;

    public PilotAI(PilotOrders orders, SkillLevel skill)
    {
        Orders = orders;
        Skill = skill;
        Profile = SkillProfile.For(skill);
    }

    public PilotOrders Orders { get; }
    public SkillLevel Skill { get; }
    public SkillProfile Profile { get; }
    public PilotState State { get; private set; } = PilotState.Cruise;
    /// <summary>Set by the mission when the pilot should jump out (e.g. autopilot after objectives).</summary>
    public bool WantsHyperspace { get; set; }

    public void Update(World world, Ship ship, float dt)
    {
        if (ship.Hyperspace != HyperspaceState.None) return;

        _thinkTimer -= dt;
        if (_thinkTimer <= 0f)
        {
            Think(world, ship);
            _thinkTimer = Profile.ReactionTime * world.Rng.Range(0.8f, 1.2f);
        }

        ship.Controls = default;
        switch (State)
        {
            case PilotState.Evade:
                _evadeTimer -= dt;
                Steer(ship, _evadeLocalDir, gain: 4f, allowRoll: false);
                ship.Controls.Throttle = 1f;
                if (_evadeTimer <= 0f) State = PilotState.Attack;
                break;
            case PilotState.Attack when world.FindShip(ship.TargetId) is { Alive: true } target:
                Attack(world, ship, target);
                break;
            default:
                Cruise(world, ship);
                break;
        }
    }

    private void Think(World world, Ship ship)
    {
        if (WantsHyperspace && ship.Class.HasHyperdrive)
        {
            ship.Command(ShipCommand.Hyperspace);
            return;
        }

        bool attacks = ship.Class.LaserCount > 0 && Orders.Type is OrderType.AttackAny or OrderType.AttackFlightGroup;
        if (attacks && world.FindShip(ship.TargetId) is not { Alive: true })
            ship.TargetId = ChooseTarget(world, ship);

        if (attacks && ship.TargetId is not null)
        {
            if (State == PilotState.Cruise) State = PilotState.Attack;
            if (ship.FireMode == FireMode.Single && ship.Class.LaserCount >= 2) ship.FireMode = FireMode.Dual;
        }
        else if (State != PilotState.Evade)
        {
            State = PilotState.Cruise;
        }

        // Newly hit: maybe break off.
        if (ship.LastHitTime > _lastSeenHit)
        {
            _lastSeenHit = ship.LastHitTime;
            if (State != PilotState.Evade && world.Rng.Chance(Profile.EvadeChance))
                BeginEvade(world);
        }

        // Head-on pass: break before colliding.
        if (State == PilotState.Attack && world.FindShip(ship.TargetId) is { Alive: true } t)
        {
            Vector3 delta = t.Position - ship.Position;
            float closing = -Vector3.Dot(t.Velocity - ship.Velocity, Vector3.Normalize(delta));
            if (delta.Length() < 120f + t.Class.CollisionRadius && closing > 30f)
                BeginEvade(world);
        }

        // Turning circle stalemate (two fighters inside each other's turn radius, neither gaining):
        // extend away to open the range, then come back around.
        if (State == PilotState.Attack && world.FindShip(ship.TargetId) is { Alive: true } tgt)
        {
            Vector3 delta = tgt.Position - ship.Position;
            float off = SimMath.OffNoseAngle(SimMath.ToLocal(ship.Orientation, delta)) * SimMath.Rad2Deg;
            _stalemate = delta.Length() < 450f && off > 45f ? _stalemate + Profile.ReactionTime : 0f;
            if (_stalemate > 4f)
            {
                _stalemate = 0f;
                Vector3 away = SimMath.ToLocal(ship.Orientation, -Vector3.Normalize(delta));
                BeginEvade(world, Vector3.Normalize(new Vector3(away.X, away.Y, -1.5f)), world.Rng.Range(3f, 5f));
            }
        }

        // Re-roll aim error each decision; better pilots aim tighter.
        _aimOffset = WeaponSystem.Jitter(world.Rng, SimMath.LocalForward, Profile.AimErrorDeg * SimMath.Deg2Rad) - SimMath.LocalForward;
    }

    private void BeginEvade(World world)
    {
        float a = world.Rng.Range(0f, 2f * MathF.PI);
        BeginEvade(world, Vector3.Normalize(new Vector3(MathF.Cos(a), MathF.Sin(a), -0.3f)), world.Rng.Range(1.2f, 2.5f));
    }

    private void BeginEvade(World world, Vector3 localDirection, float seconds)
    {
        State = PilotState.Evade;
        _evadeTimer = seconds;
        _evadeLocalDir = localDirection;
    }

    private int? ChooseTarget(World world, Ship ship)
    {
        if (Orders.Type == OrderType.AttackFlightGroup && Orders.Target is { } fg)
        {
            Ship? best = null;
            float bestD = float.MaxValue;
            foreach (Ship s in world.Ships)
            {
                if (!s.Alive || s.FlightGroup != fg) continue;
                float d = Vector3.DistanceSquared(s.Position, ship.Position);
                if (d < bestD) { bestD = d; best = s; }
            }
            if (best is not null) return best.Id;
        }
        return Targeting.NearestEnemy(world, ship);
    }

    private void Attack(World world, Ship ship, Ship target)
    {
        ShipClass cls = ship.Class;
        float boltSpeed = cls.LaserBoltSpeed + ship.Speed;
        Vector3 lead = SimMath.LeadPoint(ship.Position, Vector3.Zero, target.Position, target.Velocity, boltSpeed);
        Vector3 toLead = lead - ship.Position;
        float dist = toLead.Length();
        if (dist < 1e-3f) return;

        Vector3 local = SimMath.ToLocal(ship.Orientation, toLead / dist);
        Vector3 aimLocal = Vector3.Normalize(local + _aimOffset);
        Steer(ship, aimLocal, Profile.SteeringGain, allowRoll: true);

        // Throttle: close fast, but don't overshoot a target we're sitting behind.
        // Behind the target: hold a ~250 m firing distance by matching its speed.
        bool behind = Vector3.Dot(target.Forward, ship.Position - target.Position) < 0f;
        ship.Controls.Throttle = behind && dist < 600f
            ? SimMath.Clamp((target.Speed + (dist - 250f) * 0.3f) / MathF.Max(cls.MaxSpeed, 1f), 0.33f, 1f)
            : 1f;
        if (target.Class.Category == ShipCategory.Capital && dist < 600f) ship.Controls.Throttle = 0.5f;

        // Target well off the nose in a close fight: cut to the best-turning throttle, as players do.
        float offNose = SimMath.OffNoseAngle(local) * SimMath.Rad2Deg;
        if (offNose > 50f && dist < 800f) ship.Controls.Throttle = MathF.Min(ship.Controls.Throttle, 0.33f);

        ship.Controls.Fire = offNose < Profile.FireConeDeg && dist < cls.LaserRange * 0.85f
                             && ship.AverageLaserCharge > cls.LaserShotCost;
    }

    private void Cruise(World world, Ship ship)
    {
        ship.Controls.Throttle = Orders.CruiseThrottle;
        if (Orders.Type != OrderType.FlyWaypoints || Orders.Waypoints.Count == 0) return;
        if (_waypoint >= Orders.Waypoints.Count)
        {
            if (Orders.Loop) _waypoint = 0;
            else
            {
                if (Orders.HyperspaceAtEnd) WantsHyperspace = true;
                return;
            }
        }

        Vector3 to = Orders.Waypoints[_waypoint] - ship.Position;
        float arrive = MathF.Max(150f, ship.Class.CollisionRadius * 3f);
        if (to.Length() < arrive)
        {
            _waypoint++;
            return;
        }
        Steer(ship, SimMath.ToLocal(ship.Orientation, Vector3.Normalize(to)), Profile.SteeringGain, allowRoll: false);
    }

    /// <summary>
    /// Proportional steering toward a local direction. For large errors, roll so the goal is
    /// "overhead" and pull up, like a WWII fighter pilot (and like SWOTL's AI presumably did).
    /// </summary>
    private static void Steer(Ship ship, Vector3 localDir, float gain, bool allowRoll)
    {
        float yawErr = MathF.Atan2(localDir.X, -localDir.Z);
        float pitchErr = MathF.Atan2(localDir.Y, MathF.Sqrt(localDir.X * localDir.X + localDir.Z * localDir.Z));
        ship.Controls.Yaw = SimMath.Clamp(yawErr * gain, -1f, 1f);
        ship.Controls.Pitch = SimMath.Clamp(pitchErr * gain, -1f, 1f);

        if (allowRoll && SimMath.OffNoseAngle(localDir) > 30f * SimMath.Deg2Rad)
        {
            // Angle of the goal around the nose, measured from "up" toward "right".
            float bank = MathF.Atan2(localDir.X, localDir.Y);
            if (MathF.Abs(bank) < 150f * SimMath.Deg2Rad)
                ship.Controls.Roll = SimMath.Clamp(bank * 1.5f, -1f, 1f);
        }
    }
}
