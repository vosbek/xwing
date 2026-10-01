using System.Numerics;
using XWing.Sim.Combat;
using XWing.Sim.Data;
using XWing.Sim.Flight;
using XWing.Sim.Missions;
using XWing.Sim.Sensors;

namespace XWing.Sim.Core;

/// <summary>
/// The whole simulation. Advances in fixed ticks and never touches rendering, audio or input
/// devices, so it runs identically inside Godot, in the headless runner and in unit tests.
/// </summary>
public sealed class World
{
    private readonly List<SimEvent> _tickEvents = new();
    private int _nextId = 1;

    public World(ShipCatalog catalog, SimRules? rules = null, ulong seed = 1)
    {
        Catalog = catalog;
        Rules = rules ?? new SimRules();
        Rng = new DeterministicRandom(seed);
    }

    public ShipCatalog Catalog { get; }
    public SimRules Rules { get; }
    public DeterministicRandom Rng { get; }
    public MissionRuntime? Mission { get; private set; }
    public Ship? Player { get; set; }

    public long Tick { get; private set; }
    public float Time { get; private set; }

    public List<Ship> Ships { get; } = new();
    public List<Projectile> Projectiles { get; } = new();

    /// <summary>Events raised during the most recent <see cref="Step"/>.</summary>
    public IReadOnlyList<SimEvent> TickEvents => _tickEvents;

    /// <summary>Every event since the world was created.</summary>
    public List<SimEvent> EventLog { get; } = new();

    public static World ForMission(MissionDefinition mission, ShipCatalog? catalog = null, SimRules? rules = null,
        ulong seed = 1, bool autopilotPlayer = false)
    {
        var world = new World(catalog ?? ShipCatalog.LoadBuiltIn(), rules, seed);
        world.Mission = new MissionRuntime(world, mission, autopilotPlayer);
        world.Mission.Update(0f); // spawn mission-start groups before the first frame
        return world;
    }

    public Ship Spawn(string classId, Iff iff, string flightGroup, int index, Vector3 position, Quaternion orientation)
    {
        var ship = new Ship(_nextId++, Catalog.Get(classId), iff, flightGroup, index)
        {
            Position = position,
            Orientation = orientation,
        };
        Ships.Add(ship);
        Emit(new ShipArrived(Time, ship.Id, ship.Callsign));
        return ship;
    }

    public Ship? FindShip(int? id)
    {
        if (id is null) return null;
        foreach (Ship s in Ships)
            if (s.Id == id) return s;
        return null;
    }

    public void SpawnProjectile(Projectile p) => Projectiles.Add(p);

    public void Emit(SimEvent e)
    {
        _tickEvents.Add(e);
        EventLog.Add(e);
    }

    public void Step()
    {
        _tickEvents.Clear();
        float dt = Rules.Dt;

        foreach (Ship ship in Ships)
        {
            if (!ship.Alive) { ship.PendingCommands.Clear(); continue; }
            while (ship.PendingCommands.TryDequeue(out ShipCommand cmd)) Apply(ship, cmd);
        }

        // Ships list can grow during the step (mission arrivals), so iterate by index.
        for (int i = 0; i < Ships.Count; i++)
        {
            Ship ship = Ships[i];
            if (!ship.Alive) continue;
            ship.Pilot?.Update(this, ship, dt);
            StepHyperspace(ship, dt);
            if (!ship.Alive) continue;
            FlightModel.Step(ship, Rules, dt);
            EnergySystem.Step(ship, Rules, dt);
            WeaponSystem.StepLasers(this, ship, dt);
            WeaponSystem.StepTurrets(this, ship, dt);
        }

        WeaponSystem.StepProjectiles(this, dt);
        WeaponSystem.StepCollisions(this);

        foreach (Ship ship in Ships)
            if (ship.TargetId is { } t && FindShip(t) is not { Alive: true })
                ship.TargetId = null;

        Mission?.Update(dt);

        Tick++;
        Time = Tick * dt;
    }

    public void Run(float seconds)
    {
        long ticks = (long)MathF.Round(seconds * Rules.TickRate);
        for (long i = 0; i < ticks; i++) Step();
    }

    private void Apply(Ship ship, ShipCommand cmd)
    {
        switch (cmd)
        {
            case ShipCommand.TargetNext: ship.TargetId = Targeting.Cycle(this, ship, +1); break;
            case ShipCommand.TargetPrevious: ship.TargetId = Targeting.Cycle(this, ship, -1); break;
            case ShipCommand.TargetNearestEnemy: ship.TargetId = Targeting.NearestEnemy(this, ship) ?? ship.TargetId; break;
            case ShipCommand.TargetNone: ship.TargetId = null; break;
            case ShipCommand.CycleFireMode: ship.FireMode = WeaponSystem.NextFireMode(ship); break;
            case ShipCommand.CycleLaserRecharge:
                if (ship.Class.LaserCount > 0) ship.LaserRecharge = EnergySystem.Next(ship.LaserRecharge);
                break;
            case ShipCommand.CycleShieldRecharge:
                if (ship.Class.HasShields) ship.ShieldRecharge = EnergySystem.Next(ship.ShieldRecharge);
                break;
            case ShipCommand.CycleShieldFocus:
                if (ship.Class.HasShields) ship.ShieldFocus = (ShieldFocus)(((int)ship.ShieldFocus + 1) % 3);
                break;
            case ShipCommand.TransferLasersToShields: EnergySystem.TransferLasersToShields(ship, Rules); break;
            case ShipCommand.TransferShieldsToLasers: EnergySystem.TransferShieldsToLasers(ship, Rules); break;
            case ShipCommand.Hyperspace:
                if (ship.Class.HasHyperdrive && ship.Hyperspace == HyperspaceState.None)
                {
                    ship.Hyperspace = HyperspaceState.Charging;
                    ship.HyperspaceTimer = ship.Class.HyperspaceChargeTime;
                }
                break;
        }
    }

    /// <summary>While charging the hyperdrive the ship flies straight at full throttle, then leaves. [SPEC M-03]</summary>
    private void StepHyperspace(Ship ship, float dt)
    {
        if (ship.Hyperspace != HyperspaceState.Charging) return;
        ship.Controls = new ShipControls { Throttle = 1f };
        ship.HyperspaceTimer -= dt;
        if (ship.HyperspaceTimer <= 0f)
        {
            ship.Hyperspace = HyperspaceState.Departed;
            Emit(new HyperspaceEntered(Time, ship.Id, ship.Callsign));
        }
    }

    /// <summary>
    /// FNV-1a over the full dynamic state. Two runs with the same seed and inputs must produce the
    /// same hash at every tick; used by determinism tests and replay verification.
    /// </summary>
    public ulong StateHash()
    {
        ulong h = 14695981039346656037UL;
        void Mix(int v) { h ^= (uint)v; h *= 1099511628211UL; }
        void MixF(float f) => Mix(BitConverter.SingleToInt32Bits(f));

        Mix((int)Tick);
        foreach (Ship s in Ships)
        {
            Mix(s.Id);
            MixF(s.Position.X); MixF(s.Position.Y); MixF(s.Position.Z);
            MixF(s.Orientation.X); MixF(s.Orientation.Y); MixF(s.Orientation.Z); MixF(s.Orientation.W);
            MixF(s.Speed); MixF(s.Hull); MixF(s.ShieldFront); MixF(s.ShieldRear);
            foreach (float c in s.LaserCharge) MixF(c);
            Mix((int)s.Hyperspace);
            Mix(s.TargetId ?? -1);
        }
        Mix(Projectiles.Count);
        foreach (Projectile p in Projectiles)
        {
            MixF(p.Position.X); MixF(p.Position.Y); MixF(p.Position.Z);
        }
        return h;
    }
}
