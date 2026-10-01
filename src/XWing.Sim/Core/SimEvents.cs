namespace XWing.Sim.Core;

public abstract record SimEvent(float Time);

public sealed record ShipArrived(float Time, int ShipId, string Callsign) : SimEvent(Time);
public sealed record ShipHit(float Time, int ShipId, int? AttackerId, float ShieldDamage, float HullDamage) : SimEvent(Time);
public sealed record ShipDestroyed(float Time, int ShipId, string Callsign, int? KillerId) : SimEvent(Time);
public sealed record ShipCollision(float Time, int ShipA, int ShipB, float Damage) : SimEvent(Time);
public sealed record HyperspaceEntered(float Time, int ShipId, string Callsign) : SimEvent(Time);
public sealed record LaserFired(float Time, int ShipId, int Bolts) : SimEvent(Time);
public sealed record LaserDry(float Time, int ShipId) : SimEvent(Time);
public sealed record RadioMessage(float Time, string Text) : SimEvent(Time);
public sealed record GoalStateChanged(float Time, int GoalIndex, string Description, Missions.GoalState State) : SimEvent(Time);
public sealed record MissionEnded(float Time, Missions.MissionResult Result, string Reason) : SimEvent(Time);
