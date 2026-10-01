using System.Numerics;

namespace XWing.Sim.AI;

public enum OrderType
{
    /// <summary>Hold course and speed.</summary>
    Hold,
    /// <summary>Engage ships of one flight group, then any hostile.</summary>
    AttackFlightGroup,
    /// <summary>Engage the nearest hostile.</summary>
    AttackAny,
    /// <summary>Fly the waypoint list; optionally loop or jump to hyperspace at the end.</summary>
    FlyWaypoints,
}

public sealed class PilotOrders
{
    public OrderType Type { get; init; } = OrderType.AttackAny;
    public string? Target { get; init; }
    public List<Vector3> Waypoints { get; init; } = new();
    public bool Loop { get; init; }
    public bool HyperspaceAtEnd { get; init; }
    /// <summary>Throttle used while cruising waypoints.</summary>
    public float CruiseThrottle { get; init; } = 1f;
}
