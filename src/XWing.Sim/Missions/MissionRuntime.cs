using System.Numerics;
using XWing.Sim.AI;
using XWing.Sim.Core;

namespace XWing.Sim.Missions;

public enum GoalState
{
    Pending,
    Complete,
    Failed,
}

public enum MissionResult
{
    Success,
    Failure,
}

public sealed class MissionRuntime
{
    private sealed class GroupState
    {
        public required FlightGroupDef Def;
        public bool Arrived;
        public float? Countdown;
        public readonly List<Ship> Ships = new();
    }

    private sealed class MessageState
    {
        public required MessageDef Def;
        public float? Countdown;
        public bool Sent;
    }

    private readonly Dictionary<string, GroupState> _groups;
    private readonly List<MessageState> _messages;
    private readonly World _world;
    private bool _objectivesAnnounced;

    public MissionRuntime(World world, MissionDefinition definition, bool autopilotPlayer = false)
    {
        _world = world;
        Definition = definition;
        AutopilotPlayer = autopilotPlayer;
        _groups = definition.FlightGroups.ToDictionary(g => g.Name, g => new GroupState { Def = g });
        _messages = definition.Messages.Select(m => new MessageState { Def = m }).ToList();
        Goals = new GoalState[definition.Goals.Count];

        foreach (GoalDef goal in definition.Goals)
            if (!_groups.ContainsKey(goal.FlightGroup))
                throw new InvalidDataException($"Goal references unknown flight group '{goal.FlightGroup}'");
        foreach (FlightGroupDef g in definition.FlightGroups)
            if (!world.Catalog.All.Any(c => string.Equals(c.Id, g.ShipClass, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException($"Flight group '{g.Name}' uses unknown ship class '{g.ShipClass}'");
        foreach (FlightGroupDef g in definition.FlightGroups)
            if (g.Arrival?.FlightGroup is { } fg && !_groups.ContainsKey(fg))
                throw new InvalidDataException($"Flight group '{g.Name}' arrival references unknown group '{fg}'");
    }

    public MissionDefinition Definition { get; }
    public bool AutopilotPlayer { get; }
    public GoalState[] Goals { get; }
    public bool ObjectivesComplete { get; private set; }
    public MissionResult? Result { get; private set; }
    public string? ResultReason { get; private set; }

    public void Update(float dt)
    {
        foreach (GroupState g in _groups.Values)
        {
            if (g.Arrived) continue;
            if (g.Countdown is null && IsSatisfied(g.Def.Arrival ?? new TriggerDef { Type = TriggerType.MissionStart }))
                g.Countdown = g.Def.ArrivalDelay;
            if (g.Countdown is { } c)
            {
                g.Countdown = c - dt;
                if (g.Countdown <= 0f) Arrive(g);
            }
        }

        foreach (MessageState m in _messages)
        {
            if (m.Sent) continue;
            if (m.Countdown is null && IsSatisfied(m.Def.Trigger)) m.Countdown = m.Def.Delay;
            if (m.Countdown is { } c)
            {
                m.Countdown = c - dt;
                if (m.Countdown <= 0f)
                {
                    m.Sent = true;
                    _world.Emit(new RadioMessage(_world.Time, m.Def.Text));
                }
            }
        }

        EvaluateGoals();
        EvaluateEnd();
    }

    private void Arrive(GroupState g)
    {
        g.Arrived = true;
        FlightGroupDef def = g.Def;
        Quaternion orientation = SimMath.FromHeadingPitch(def.Heading, def.Pitch);
        Vector3 right = Vector3.Transform(SimMath.LocalRight, orientation);
        Vector3 back = -Vector3.Transform(SimMath.LocalForward, orientation);

        for (int i = 0; i < def.Count; i++)
        {
            // Echelon formation: alternate sides, each pair further back.
            int rank = (i + 1) / 2;
            float side = i % 2 == 1 ? 1f : -1f;
            Vector3 offset = i == 0 ? Vector3.Zero : right * (side * rank * def.Spacing) + back * (rank * def.Spacing * 0.5f);

            Ship ship = _world.Spawn(def.ShipClass, def.Iff, def.Name, i, def.Position + offset, orientation);
            ship.Controls.Throttle = def.Throttle;
            ship.Speed = def.Throttle * ship.Class.MaxSpeed;

            if (def.IsPlayer && i == 0)
            {
                ship.IsPlayer = true;
                _world.Player = ship;
                if (AutopilotPlayer) ship.Pilot = new PilotAI(new PilotOrders { Type = OrderType.AttackAny }, SkillLevel.Ace);
            }
            else
            {
                ship.Pilot = new PilotAI(def.Orders, def.Skill);
            }
            g.Ships.Add(ship);
        }
    }

    private bool IsSatisfied(TriggerDef t)
    {
        GroupState? g = t.FlightGroup is { } name ? _groups[name] : null;
        return t.Type switch
        {
            TriggerType.MissionStart => true,
            TriggerType.Time => _world.Time >= t.Seconds,
            TriggerType.FlightGroupArrived => g is { Arrived: true },
            TriggerType.FlightGroupDestroyed => g is { Arrived: true } && g.Ships.All(s => s.Destroyed),
            TriggerType.FlightGroupAttacked => g is not null && g.Ships.Any(s => s.LastHitTime > float.NegativeInfinity),
            TriggerType.ObjectivesComplete => ObjectivesComplete,
            _ => false,
        };
    }

    private void EvaluateGoals()
    {
        for (int i = 0; i < Goals.Length; i++)
        {
            GoalDef def = Definition.Goals[i];
            GroupState g = _groups[def.FlightGroup];
            GoalState state = def.Type switch
            {
                GoalType.DestroyAll when g.Arrived && g.Ships.All(s => s.Destroyed) => GoalState.Complete,
                // A target that escaped to hyperspace can no longer be destroyed.
                GoalType.DestroyAll when g.Ships.Any(s => s.Hyperspace == HyperspaceState.Departed) => GoalState.Failed,
                GoalType.MustSurvive when g.Ships.Any(s => s.Destroyed) => GoalState.Failed,
                _ => GoalState.Pending,
            };
            if (state != Goals[i] && Goals[i] == GoalState.Pending)
            {
                Goals[i] = state;
                _world.Emit(new GoalStateChanged(_world.Time, i, def.Describe(), state));
            }
        }

        // Survive goals count as satisfied while nothing has failed.
        bool complete = Definition.Goals.Select((def, i) => (def, state: Goals[i]))
            .Where(x => x.def.Primary)
            .All(x => x.state == GoalState.Complete || (x.def.Type == GoalType.MustSurvive && x.state == GoalState.Pending));
        ObjectivesComplete = complete;

        if (complete && !_objectivesAnnounced)
        {
            _objectivesAnnounced = true;
            _world.Emit(new RadioMessage(_world.Time, "Primary mission objectives complete. Hyperspace when ready."));
            if (AutopilotPlayer && _world.Player?.Pilot is { } pilot) pilot.WantsHyperspace = true;
        }
    }

    private void EvaluateEnd()
    {
        if (Result is not null || _world.Player is not { } player) return;

        if (player.Destroyed)
            End(MissionResult.Failure, "Player craft destroyed");
        else if (player.Hyperspace == HyperspaceState.Departed)
        {
            if (Goals.Contains(GoalState.Failed)) End(MissionResult.Failure, "A mission goal failed");
            else if (!ObjectivesComplete) End(MissionResult.Failure, "Left the battle before objectives were complete");
            else End(MissionResult.Success, "Objectives complete, hyperspaced out");
        }
    }

    private void End(MissionResult result, string reason)
    {
        // Survive goals are only decided when the mission ends.
        for (int i = 0; i < Goals.Length; i++)
        {
            if (Goals[i] != GoalState.Pending || Definition.Goals[i].Type != GoalType.MustSurvive) continue;
            Goals[i] = GoalState.Complete;
            _world.Emit(new GoalStateChanged(_world.Time, i, Definition.Goals[i].Describe(), GoalState.Complete));
        }
        Result = result;
        ResultReason = reason;
        _world.Emit(new MissionEnded(_world.Time, result, reason));
    }
}
