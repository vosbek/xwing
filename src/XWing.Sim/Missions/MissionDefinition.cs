using System.Numerics;
using XWing.Sim.AI;
using XWing.Sim.Core;
using XWing.Sim.Data;

namespace XWing.Sim.Missions;

/// <summary>
/// Engine-native mission description. Deliberately shaped like X-Wing's own mission files
/// (flight groups with arrival triggers, orders, goals, and triggered radio messages) so an XWI
/// importer can target it later without changing the runtime.
/// </summary>
public sealed class MissionDefinition
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public List<FlightGroupDef> FlightGroups { get; init; } = new();
    public List<GoalDef> Goals { get; init; } = new();
    public List<MessageDef> Messages { get; init; } = new();

    public static MissionDefinition FromJson(string json) => SimJson.Deserialize<MissionDefinition>(json);
    public static MissionDefinition LoadBuiltIn(string name) => FromJson(SimJson.ReadEmbedded($"{name}.json"));
}

public sealed class FlightGroupDef
{
    public string Name { get; init; } = "";
    public string ShipClass { get; init; } = "";
    public Iff Iff { get; init; }
    public int Count { get; init; } = 1;
    public SkillLevel Skill { get; init; } = SkillLevel.Officer;
    public bool IsPlayer { get; init; }
    public Vector3 Position { get; init; }
    /// <summary>Degrees, 0 = facing -Z, positive = turned right.</summary>
    public float Heading { get; init; }
    public float Pitch { get; init; }
    /// <summary>Initial throttle and speed fraction.</summary>
    public float Throttle { get; init; } = 1f;
    public float Spacing { get; init; } = 40f;
    public PilotOrders Orders { get; init; } = new();
    /// <summary>When the group appears. Null means at mission start.</summary>
    public TriggerDef? Arrival { get; init; }
    public float ArrivalDelay { get; init; }
}

public enum TriggerType
{
    MissionStart,
    Time,
    FlightGroupArrived,
    FlightGroupDestroyed,
    FlightGroupAttacked,
    ObjectivesComplete,
}

public sealed class TriggerDef
{
    public TriggerType Type { get; init; }
    public string? FlightGroup { get; init; }
    public float Seconds { get; init; }
}

public enum GoalType
{
    /// <summary>Every craft in the group must be destroyed.</summary>
    DestroyAll,
    /// <summary>No craft in the group may be destroyed.</summary>
    MustSurvive,
}

public sealed class GoalDef
{
    public GoalType Type { get; init; }
    public string FlightGroup { get; init; } = "";
    public bool Primary { get; init; } = true;

    public string Describe() => Type switch
    {
        GoalType.DestroyAll => $"Destroy all of {FlightGroup}",
        GoalType.MustSurvive => $"{FlightGroup} must survive",
        _ => Type.ToString(),
    };
}

public sealed class MessageDef
{
    public TriggerDef Trigger { get; init; } = new();
    public float Delay { get; init; }
    public string Text { get; init; } = "";
}
