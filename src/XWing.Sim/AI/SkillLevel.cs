namespace XWing.Sim.AI;

/// <summary>X-Wing's AI ranks.</summary>
public enum SkillLevel
{
    Novice,
    Officer,
    Veteran,
    Ace,
    TopAce,
}

/// <summary>Per-rank behaviour knobs. All placeholders. [SPEC A-02]</summary>
public sealed record SkillProfile(
    float ReactionTime,
    float AimErrorDeg,
    float FireConeDeg,
    float EvadeChance,
    float SteeringGain)
{
    public static SkillProfile For(SkillLevel level) => level switch
    {
        SkillLevel.Novice => new(0.60f, 6.0f, 9f, 0.15f, 2.0f),
        SkillLevel.Officer => new(0.45f, 4.0f, 7f, 0.30f, 2.5f),
        SkillLevel.Veteran => new(0.30f, 2.5f, 6f, 0.45f, 3.0f),
        SkillLevel.Ace => new(0.20f, 1.5f, 5f, 0.60f, 3.5f),
        SkillLevel.TopAce => new(0.12f, 0.8f, 4f, 0.75f, 4.0f),
        _ => throw new ArgumentOutOfRangeException(nameof(level)),
    };
}
