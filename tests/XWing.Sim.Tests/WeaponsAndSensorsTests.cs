using System.Numerics;
using XWing.Sim.Combat;
using XWing.Sim.Core;
using XWing.Sim.Sensors;
using static XWing.Sim.Tests.TestBench;

namespace XWing.Sim.Tests;

public class WeaponsAndSensorsTests
{
    [Fact]
    public void Lasers_fire_in_rotation_and_cost_charge()
    {
        World w = NewWorld();
        Ship x = Spawn(w);
        x.Controls.Fire = true;
        w.Step();
        Assert.Single(w.Projectiles);
        Assert.Equal(1f - x.Class.LaserShotCost, x.LaserCharge[0], 4);
        Assert.Equal(1, x.NextCannon);
    }

    [Fact]
    public void Quad_link_fires_four_bolts()
    {
        World w = NewWorld();
        Ship x = Spawn(w);
        x.Command(ShipCommand.CycleFireMode);
        x.Command(ShipCommand.CycleFireMode);
        x.Controls.Fire = true;
        w.Step();
        Assert.Equal(FireMode.Quad, x.FireMode);
        Assert.Equal(4, w.Projectiles.Count);
    }

    [Fact]
    public void Fast_bolt_does_not_tunnel_through_a_small_ship()
    {
        World w = NewWorld();
        Ship tie = Spawn(w, "TIE", Iff.Imperial, new Vector3(0, 0, -100));
        w.SpawnProjectile(new Projectile
        {
            OwnerId = -1, Position = new Vector3(0, 0, -80), Velocity = new Vector3(0, 0, -5000),
            Damage = 10, TimeLeft = 1,
        });
        w.Step(); // one tick covers ~83 m, well past the 5 m radius
        Assert.Equal(tie.Class.Hull - 10f, tie.Hull, 3);
        Assert.Empty(w.Projectiles);
    }

    [Fact]
    public void Bolts_expire_at_max_range()
    {
        World w = NewWorld();
        Ship x = Spawn(w);
        x.Controls.Fire = true;
        w.Step();
        x.Controls.Fire = false;
        w.Run(x.Class.LaserRange / x.Class.LaserBoltSpeed + 0.1f);
        Assert.Empty(w.Projectiles);
    }

    [Fact]
    public void Target_cycle_and_nearest_enemy()
    {
        World w = NewWorld();
        Ship me = Spawn(w);
        Ship farTie = w.Spawn("TIE", Iff.Imperial, "Far", 0, new Vector3(0, 0, -3000), Quaternion.Identity);
        Ship nearTie = w.Spawn("TIE", Iff.Imperial, "Near", 0, new Vector3(0, 0, -500), Quaternion.Identity);
        Ship friend = w.Spawn("XWING", Iff.Rebel, "Gold", 0, new Vector3(0, 0, -100), Quaternion.Identity);

        Assert.Equal(nearTie.Id, Targeting.NearestEnemy(w, me));
        Assert.Equal(farTie.Id, Targeting.Cycle(w, me, +1));
        me.TargetId = farTie.Id;
        Assert.Equal(nearTie.Id, Targeting.Cycle(w, me, +1));
        me.TargetId = farTie.Id;
        Assert.Equal(friend.Id, Targeting.Cycle(w, me, -1));
    }

    [Fact]
    public void Radar_places_ahead_at_front_center_and_behind_on_rear_scope()
    {
        World w = NewWorld();
        Ship me = Spawn(w);
        w.Spawn("TIE", Iff.Imperial, "Ahead", 0, new Vector3(0, 0, -1000), Quaternion.Identity);
        w.Spawn("TIE", Iff.Imperial, "Behind", 0, new Vector3(0, 0, 1000), Quaternion.Identity);
        w.Spawn("TIE", Iff.Imperial, "Right", 0, new Vector3(1000, 0, -0.001f), Quaternion.Identity);

        var blips = Radar.Scan(w, me);
        RadarBlip ahead = blips[0], behind = blips[1], right = blips[2];
        Assert.Equal(RadarScope.Front, ahead.Scope);
        Assert.Equal(0f, ahead.X, 3);
        Assert.Equal(RadarScope.Rear, behind.Scope);
        Assert.Equal(0f, behind.X, 3);
        Assert.Equal(1f, right.X, 2); // 90° off the nose = scope edge
    }

    [Fact]
    public void Lead_point_hits_constant_velocity_target()
    {
        Vector3 shooter = Vector3.Zero;
        Vector3 target = new(0, 0, -1000);
        Vector3 targetVel = new(100, 0, 0);
        Vector3 aim = SimMath.LeadPoint(shooter, Vector3.Zero, target, targetVel, 750f);
        float t = aim.Length() / 750f;
        Assert.True(Vector3.Distance(target + targetVel * t, aim) < 2f);
    }
}
