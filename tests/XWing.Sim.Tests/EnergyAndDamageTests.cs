using System.Numerics;
using XWing.Sim.Combat;
using XWing.Sim.Core;
using XWing.Sim.Data;
using static XWing.Sim.Tests.TestBench;

namespace XWing.Sim.Tests;

public class EnergyAndDamageTests
{
    [Fact]
    public void Diverting_power_to_lasers_costs_engine_speed_and_turning_them_off_adds_it()
    {
        World w = NewWorld();
        Ship x = Spawn(w);
        SimRules r = w.Rules;

        x.LaserRecharge = RechargeLevel.Normal;
        Assert.Equal(1f, EnergySystem.EngineFactor(x, r), 4);
        x.LaserRecharge = RechargeLevel.Maximum;
        Assert.Equal(1f - 2 * r.EngineCostPerLevel, EnergySystem.EngineFactor(x, r), 4);
        x.LaserRecharge = RechargeLevel.Off;
        Assert.Equal(1f + r.EngineCostPerLevel, EnergySystem.EngineFactor(x, r), 4);
    }

    [Fact]
    public void Unshielded_ship_shield_setting_does_not_affect_engines()
    {
        World w = NewWorld();
        Ship tie = Spawn(w, "TIE", Iff.Imperial);
        tie.ShieldRecharge = RechargeLevel.Maximum;
        Assert.Equal(1f, EnergySystem.EngineFactor(tie, w.Rules), 4);
    }

    [Fact]
    public void Lasers_recharge_at_rate_times_level()
    {
        World w = NewWorld();
        Ship x = Spawn(w);
        Array.Fill(x.LaserCharge, 0f);
        x.LaserRecharge = RechargeLevel.Increased;
        w.Run(1f);
        Assert.Equal(x.Class.LaserRechargeRate * 2f, x.LaserCharge[0], 3);
    }

    [Fact]
    public void Shield_focus_moves_energy_to_the_focused_side()
    {
        World w = NewWorld();
        Ship x = Spawn(w);
        x.ShieldRecharge = RechargeLevel.Off;
        x.ShieldFocus = ShieldFocus.Front;
        x.ShieldFront = 20f;
        x.ShieldRear = 80f;
        w.Run(2f);
        Assert.True(x.ShieldFront > 20f);
        Assert.Equal(100f, x.ShieldFront + x.ShieldRear, 2);
    }

    [Fact]
    public void Energy_transfer_round_trip_conserves_up_to_caps()
    {
        World w = NewWorld();
        Ship x = Spawn(w);
        x.ShieldFront = x.ShieldRear = 50f;
        float shieldsBefore = x.ShieldFront + x.ShieldRear;
        EnergySystem.TransferLasersToShields(x, w.Rules);
        Assert.True(x.ShieldFront + x.ShieldRear > shieldsBefore);
        Assert.True(x.LaserCharge.All(c => c < 1f));
        EnergySystem.TransferShieldsToLasers(x, w.Rules);
        Assert.All(x.LaserCharge, c => Assert.Equal(1f, c, 3));
        Assert.Equal(shieldsBefore, x.ShieldFront + x.ShieldRear, 2);
    }

    [Fact]
    public void Front_hit_drains_front_shield_then_overflows_to_hull()
    {
        World w = NewWorld();
        Ship x = Spawn(w);
        Vector3 nose = x.Position + x.Forward * 5f;

        DamageModel.Apply(w, x, 60f, nose, null);
        Assert.Equal(40f, x.ShieldFront, 3);
        Assert.Equal(100f, x.ShieldRear, 3);
        Assert.Equal(100f, x.Hull, 3);

        DamageModel.Apply(w, x, 60f, nose, null);
        Assert.Equal(0f, x.ShieldFront, 3);
        Assert.Equal(80f, x.Hull, 3);
    }

    [Fact]
    public void Rear_hit_uses_rear_shield()
    {
        World w = NewWorld();
        Ship x = Spawn(w);
        DamageModel.Apply(w, x, 30f, x.Position - x.Forward * 5f, null);
        Assert.Equal(70f, x.ShieldRear, 3);
        Assert.Equal(100f, x.ShieldFront, 3);
    }

    [Fact]
    public void Destruction_emits_event_once_and_ship_is_no_longer_alive()
    {
        World w = NewWorld();
        Ship tie = Spawn(w, "TIE", Iff.Imperial);
        DamageModel.Apply(w, tie, 1000f, tie.Position, null);
        DamageModel.Apply(w, tie, 1000f, tie.Position, null);
        Assert.False(tie.Alive);
        Assert.Single(w.EventLog.OfType<ShipDestroyed>());
    }
}
