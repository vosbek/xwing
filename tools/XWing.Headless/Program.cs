using System.Globalization;
using XWing.Sim.Core;
using XWing.Sim.Data;
using XWing.Sim.Missions;
using XWing.Sim.Research;

// Headless front end for the simulation: run missions without rendering, emit the test-bench
// measurements, and diff them against numbers measured in the original game.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

var argList = args.ToList();
string command = argList.Count > 0 ? argList[0] : "help";
string? Opt(string name) { int i = argList.IndexOf(name); return i >= 0 && i + 1 < argList.Count ? argList[i + 1] : null; }
bool Flag(string name) => argList.Contains(name);

ShipCatalog catalog = Opt("--ships") is { } shipsPath ? ShipCatalog.FromJson(File.ReadAllText(shipsPath)) : ShipCatalog.LoadBuiltIn();

switch (command)
{
    case "run": return Run();
    case "measure": return Measure();
    case "compare": return Compare();
    case "ships": return Ships();
    default: return Help();
}

int Help()
{
    Console.WriteLine("""
        xwing-headless <command> [options]

          run       Fly a mission with no renderer. The player is flown by the AI.
                      --mission <file.json>   (default: built-in vertical_slice)
                      --seconds <n>           max mission time (default 900)
                      --seed <n>              RNG seed (default 1)
                      --trace <out.csv>       per-tick player state for analysis
                      --quiet                 only print the summary

          measure   Run the test-bench battery (same tests as research/measurements/PROTOCOL.md).
                      --out <sim.csv>         write CSV instead of stdout

          compare   Diff test-bench output against measurements from the original game.
                      --reference <file.csv>  (default research/measurements/original.csv)
                      --sim <sim.csv>         (default: run the battery now)
                    Exit code 1 if any measured value is outside tolerance.

          ships     List ship classes and their provenance.

          Global:   --ships <ships.json>      use a different ship catalog
        """);
    return 0;
}

int Run()
{
    MissionDefinition mission = Opt("--mission") is { } path
        ? MissionDefinition.FromJson(File.ReadAllText(path))
        : MissionDefinition.LoadBuiltIn("vertical_slice");
    float seconds = float.Parse(Opt("--seconds") ?? "900");
    ulong seed = ulong.Parse(Opt("--seed") ?? "1");
    bool quiet = Flag("--quiet");

    World world = World.ForMission(mission, catalog, seed: seed, autopilotPlayer: true);
    using StreamWriter? trace = Opt("--trace") is { } tracePath ? new StreamWriter(tracePath) : null;
    trace?.WriteLine("tick,time,x,y,z,qx,qy,qz,qw,speed,throttle,hull,shield_front,shield_rear,laser_charge,target");

    Console.WriteLine($"Mission: {mission.Name} (seed {seed}, {world.Rules.TickRate} Hz)");
    PrintEvents(world.EventLog, world, quiet);

    long maxTicks = (long)(seconds * world.Rules.TickRate);
    while (world.Tick < maxTicks && world.Mission!.Result is null)
    {
        world.Step();
        PrintEvents(world.TickEvents, world, quiet);
        if (trace is not null && world.Player is { } p)
        {
            trace.WriteLine(string.Join(",", world.Tick, world.Time.ToString("0.###"),
                p.Position.X, p.Position.Y, p.Position.Z,
                p.Orientation.X, p.Orientation.Y, p.Orientation.Z, p.Orientation.W,
                p.Speed, p.Controls.Throttle, p.Hull, p.ShieldFront, p.ShieldRear, p.AverageLaserCharge, p.TargetId ?? -1));
        }
    }

    MissionRuntime m = world.Mission!;
    Console.WriteLine();
    Console.WriteLine($"== Result after {world.Time:0.0}s: {(m.Result?.ToString() ?? "Unfinished (time limit)")} {m.ResultReason}");
    for (int i = 0; i < m.Goals.Length; i++)
        Console.WriteLine($"   [{m.Goals[i],-8}] {mission.Goals[i].Describe()}");
    Console.WriteLine($"   Kills: {string.Join(", ", KillTally(world))}");
    foreach (Ship s in world.Ships.Where(s => !s.Class.Category.Equals(ShipCategory.Fighter) || s.IsPlayer))
    {
        var hits = world.EventLog.OfType<ShipHit>().Where(h => h.ShipId == s.Id).ToList();
        Console.WriteLine($"   {s.Callsign}: hit {hits.Count}x, {hits.Sum(h => h.ShieldDamage):0} shield / {hits.Sum(h => h.HullDamage):0} hull damage taken");
    }
    Console.WriteLine($"   State hash: {world.StateHash():x16}");
    return m.Result == MissionResult.Success ? 0 : 2;
}

static IEnumerable<string> KillTally(World world) =>
    world.EventLog.OfType<ShipDestroyed>()
        .GroupBy(e => world.FindShip(e.KillerId)?.Callsign ?? "collision/unknown")
        .Select(g => $"{g.Key}: {g.Count()}");

static void PrintEvents(IEnumerable<SimEvent> events, World world, bool quiet)
{
    if (quiet) return;
    foreach (SimEvent e in events)
    {
        string? line = e switch
        {
            ShipArrived a => $"{a.Callsign} arrives",
            ShipDestroyed d => $"{d.Callsign} destroyed by {world.FindShip(d.KillerId)?.Callsign ?? "?"}",
            HyperspaceEntered h => $"{h.Callsign} jumps to hyperspace",
            RadioMessage r => $"\"{r.Text}\"",
            GoalStateChanged g => $"Goal {g.State}: {g.Description}",
            MissionEnded m => $"MISSION {m.Result}: {m.Reason}",
            ShipCollision c => $"Collision #{c.ShipA} / #{c.ShipB}",
            _ => null,
        };
        if (line is not null) Console.WriteLine($"[{e.Time,7:0.00}] {line}");
    }
}

int Measure()
{
    string csv = MeasurementComparer.ToCsv(FlightTests.RunAll(catalog));
    if (Opt("--out") is { } outPath) { File.WriteAllText(outPath, csv); Console.WriteLine($"Wrote {outPath}"); }
    else Console.Write(csv);
    return 0;
}

int Compare()
{
    string refPath = Opt("--reference") ?? Path.Combine("research", "measurements", "original.csv");
    List<ReferenceMeasurement> reference = MeasurementComparer.ParseReference(File.ReadAllText(refPath));
    List<Measurement> sim = Opt("--sim") is { } simPath
        ? MeasurementComparer.ParseSim(File.ReadAllText(simPath))
        : FlightTests.RunAll(catalog).ToList();

    List<ComparisonRow> rows = MeasurementComparer.Compare(sim, reference);
    foreach (ComparisonRow r in rows.Where(r => r.Status != CompareStatus.Unmeasured))
    {
        Console.WriteLine($"{r.Status,-5} {r.Sim.TestId} {r.Sim.Ship} {r.Sim.Condition}: " +
                          $"engine {r.Sim.Value} vs original {r.Reference!.Value} {r.Sim.Unit} " +
                          $"(delta {r.Delta:+0.###;-0.###;0}, tol ±{r.Reference.Tolerance}) [{r.Reference.Source}]");
    }
    int pass = rows.Count(r => r.Status == CompareStatus.Pass);
    int fail = rows.Count(r => r.Status == CompareStatus.Fail);
    int unmeasured = rows.Count(r => r.Status == CompareStatus.Unmeasured);
    Console.WriteLine($"\n{pass} pass, {fail} fail, {unmeasured} not yet measured in the original ({rows.Count} engine values).");
    return fail > 0 ? 1 : 0;
}

int Ships()
{
    foreach (ShipClass c in catalog.All.OrderBy(c => c.Id))
        Console.WriteLine($"{c.Id,-6} {c.Name,-20} {c.Category,-8} speed {c.MaxSpeed,5} hull {c.Hull,5} shields {c.ShieldCapacity,4}  [{c.Provenance}]");
    return 0;
}
