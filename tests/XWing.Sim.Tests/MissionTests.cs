using XWing.Sim.Core;
using XWing.Sim.Missions;

namespace XWing.Sim.Tests;

public class MissionTests
{
    private static MissionDefinition Slice => MissionDefinition.LoadBuiltIn("vertical_slice");

    [Fact]
    public void Mission_start_spawns_only_groups_without_arrival_trigger()
    {
        World w = World.ForMission(Slice);
        Assert.NotNull(w.Player);
        Assert.True(w.Player!.IsPlayer);
        Assert.DoesNotContain(w.Ships, s => s.FlightGroup == "Beta");
        Assert.Equal(2, w.Ships.Count(s => s.FlightGroup == "Alpha"));
    }

    [Fact]
    public void Second_wave_arrives_after_first_wave_destroyed_plus_delay()
    {
        World w = World.ForMission(Slice);
        foreach (Ship s in w.Ships.Where(s => s.FlightGroup == "Alpha").ToList())
            Sim.Combat.DamageModel.Apply(w, s, 9999f, s.Position, null);

        w.Run(7.5f);
        Assert.DoesNotContain(w.Ships, s => s.FlightGroup == "Beta");
        w.Run(1f);
        Assert.Equal(2, w.Ships.Count(s => s.FlightGroup == "Beta"));
        Assert.Equal(GoalState.Complete, w.Mission!.Goals[0]);
    }

    [Fact]
    public void Leaving_early_fails_the_mission()
    {
        World w = World.ForMission(Slice);
        w.Player!.Command(ShipCommand.Hyperspace);
        w.Run(5f);
        Assert.Equal(MissionResult.Failure, w.Mission!.Result);
    }

    [Fact]
    public void Player_death_fails_the_mission()
    {
        World w = World.ForMission(Slice);
        Sim.Combat.DamageModel.Apply(w, w.Player!, 9999f, w.Player!.Position, null);
        w.Step();
        Assert.Equal(MissionResult.Failure, w.Mission!.Result);
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    public void Autopilot_completes_the_vertical_slice(ulong seed)
    {
        World w = World.ForMission(Slice, seed: seed, autopilotPlayer: true);
        while (w.Mission!.Result is null && w.Time < 600f) w.Step();
        Assert.Equal(MissionResult.Success, w.Mission.Result);
        Assert.All(w.Mission.Goals, g => Assert.Equal(GoalState.Complete, g));
    }

    [Fact]
    public void Same_seed_is_bit_identical_and_different_seed_diverges()
    {
        ulong Hash(ulong seed)
        {
            World w = World.ForMission(Slice, seed: seed, autopilotPlayer: true);
            w.Run(60f);
            return w.StateHash();
        }

        Assert.Equal(Hash(7), Hash(7));
        Assert.NotEqual(Hash(7), Hash(8));
    }

    [Fact]
    public void Unknown_flight_group_reference_is_rejected()
    {
        var bad = new MissionDefinition
        {
            FlightGroups = { new FlightGroupDef { Name = "Red", ShipClass = "XWING", IsPlayer = true } },
            Goals = { new GoalDef { Type = GoalType.DestroyAll, FlightGroup = "Nobody" } },
        };
        Assert.Throws<InvalidDataException>(() => World.ForMission(bad));
    }
}
