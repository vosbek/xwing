using System.Numerics;
using XWing.Sim.Core;
using XWing.Sim.Data;
using static XWing.Sim.Tests.TestBench;

namespace XWing.Sim.Tests;

public class FlightModelTests
{
    [Fact]
    public void Throttle_sets_a_target_speed_reached_at_class_acceleration()
    {
        World w = NewWorld();
        Ship x = Spawn(w);
        x.Controls.Throttle = 1f;

        w.Run(1f);
        Assert.Equal(x.Class.Acceleration, x.Speed, 0.5f);

        w.Run(10f);
        Assert.Equal(x.Class.MaxSpeed, x.Speed, 0.01f);
    }

    [Fact]
    public void Velocity_always_points_along_the_nose_no_drift()
    {
        World w = NewWorld();
        Ship x = Spawn(w);
        x.Controls = new ShipControls { Throttle = 1f, Yaw = 1f, Pitch = 0.5f, Roll = 0.3f };
        for (int i = 0; i < 300; i++)
        {
            Vector3 before = x.Position;
            w.Step();
            Vector3 moved = x.Position - before;
            if (moved.Length() > 1e-4f)
                Assert.True(Vector3.Dot(Vector3.Normalize(moved), x.Forward) > 0.9999f);
        }
    }

    [Theory]
    [InlineData("yaw")]
    [InlineData("pitch")]
    [InlineData("roll")]
    public void Positive_stick_input_turns_the_expected_way(string axis)
    {
        World w = NewWorld();
        Ship x = Spawn(w);
        x.Controls = new ShipControls
        {
            Throttle = 0.33f,
            Yaw = axis == "yaw" ? 1 : 0,
            Pitch = axis == "pitch" ? 1 : 0,
            Roll = axis == "roll" ? 1 : 0,
        };
        w.Run(0.5f);

        switch (axis)
        {
            case "yaw": Assert.True(x.Forward.X > 0.2f, "yaw right swings nose to +X"); break;
            case "pitch": Assert.True(x.Forward.Y > 0.2f, "pull back raises nose"); break;
            case "roll": Assert.True(x.Up.X > 0.2f, "roll right tips the canopy to +X"); break;
        }
    }

    [Fact]
    public void Maneuver_curve_makes_mid_throttle_turn_faster_than_full_throttle()
    {
        float Turned(float throttle)
        {
            World w = NewWorld(new SimRules { AngularResponseTime = 0f });
            Ship x = Spawn(w);
            x.Speed = throttle * x.Class.MaxSpeed;
            x.Controls = new ShipControls { Throttle = throttle, Yaw = 1f };
            w.Run(1f);
            return MathF.Acos(Vector3.Dot(x.Forward, SimMath.LocalForward)) * SimMath.Rad2Deg;
        }

        float mid = Turned(0.33f);
        float full = Turned(1f);
        Assert.Equal(TestBench.Catalog.Get("XWING").YawRate, mid, 1.5f);
        Assert.True(mid > full);
    }

    [Fact]
    public void Curve_interpolates_and_clamps()
    {
        var c = new Curve(new CurvePoint(0, 0), new CurvePoint(1, 10));
        Assert.Equal(5f, c.Evaluate(0.5f), 4);
        Assert.Equal(0f, c.Evaluate(-3f));
        Assert.Equal(10f, c.Evaluate(7f));
    }

    [Fact]
    public void Heading_convention_matches_docs()
    {
        Quaternion east = SimMath.FromHeadingPitch(90f, 0f);
        Vector3 f = Vector3.Transform(SimMath.LocalForward, east);
        Assert.Equal(1f, f.X, 4);
    }
}
