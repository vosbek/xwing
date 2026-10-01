namespace XWing.Sim.Core;

public enum Iff
{
    Rebel,
    Imperial,
    Neutral,
}

public static class IffExtensions
{
    public static bool IsHostileTo(this Iff a, Iff b) =>
        a != b && a != Iff.Neutral && b != Iff.Neutral;
}
