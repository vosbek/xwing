using System.Numerics;
using XWing.Sim.Core;
using XWing.Sim.Data;

namespace XWing.Sim.Tests;

internal static class TestBench
{
    public static readonly ShipCatalog Catalog = ShipCatalog.LoadBuiltIn();

    public static World NewWorld(SimRules? rules = null) => new(Catalog, rules ?? new SimRules());

    public static Ship Spawn(World world, string cls = "XWING", Iff iff = Iff.Rebel, Vector3? pos = null, float heading = 0f) =>
        world.Spawn(cls, iff, cls, 0, pos ?? Vector3.Zero, SimMath.FromHeadingPitch(heading, 0f));
}
