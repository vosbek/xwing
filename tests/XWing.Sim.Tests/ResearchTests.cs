using XWing.Sim.Data;
using XWing.Sim.Research;

namespace XWing.Sim.Tests;

public class ResearchTests
{
    [Fact]
    public void Battery_reports_class_max_speed_at_full_throttle()
    {
        var rows = FlightTests.RunAll(TestBench.Catalog).ToList();
        Measurement m = rows.Single(r => r.TestId == "flight.max_speed" && r.Ship == "XWING" && r.Condition == "throttle=1");
        Assert.Equal(TestBench.Catalog.Get("XWING").MaxSpeed, m.Value, 2);
        Assert.DoesNotContain(rows, r => double.IsInfinity(r.Value) && r.Ship != "CRV");
    }

    [Fact]
    public void Comparer_applies_tolerance_and_reports_unmeasured()
    {
        var sim = new[]
        {
            new Measurement("a", "XWING", "c", 10.0, "s"),
            new Measurement("b", "XWING", "c", 10.0, "s"),
            new Measurement("z", "XWING", "c", 1.0, "s"),
        };
        var reference = MeasurementComparer.ParseReference("""
            test_id,ship,condition,value,unit,tolerance,source,notes
            # comment line
            a,xwing,c,10.4,s,0.5,dosbox,within
            b,XWING,c,12,s,0.5,dosbox,"outside, by 2"
            """);

        var result = MeasurementComparer.Compare(sim, reference);
        Assert.Equal(CompareStatus.Pass, result[0].Status);
        Assert.Equal(CompareStatus.Fail, result[1].Status);
        Assert.Equal(CompareStatus.Unmeasured, result[2].Status);
    }

    [Fact]
    public void Sim_csv_round_trips()
    {
        var rows = new[] { new Measurement("flight.max_speed", "TIE", "throttle=1", 100, "m/s") };
        Assert.Equal(rows, MeasurementComparer.ParseSim(MeasurementComparer.ToCsv(rows)));
    }

    [Fact]
    public void Every_built_in_ship_has_provenance()
    {
        Assert.All(ShipCatalog.LoadBuiltIn().All, c => Assert.False(string.IsNullOrWhiteSpace(c.Provenance)));
    }
}
