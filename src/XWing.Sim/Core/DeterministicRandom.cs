namespace XWing.Sim.Core;

/// <summary>
/// xorshift64* seeded via SplitMix64. Owned by the simulation so results never depend on
/// System.Random implementation details. Same seed + same inputs => same mission, bit for bit.
/// </summary>
public sealed class DeterministicRandom
{
    private ulong _state;

    public DeterministicRandom(ulong seed)
    {
        ulong z = seed + 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        _state = (z ^ (z >> 31)) | 1UL;
    }

    public ulong NextULong()
    {
        _state ^= _state >> 12;
        _state ^= _state << 25;
        _state ^= _state >> 27;
        return _state * 0x2545F4914F6CDD1DUL;
    }

    /// <summary>Uniform in [0, 1).</summary>
    public float NextFloat() => (NextULong() >> 40) * (1f / (1UL << 24));

    public float Range(float min, float max) => min + (max - min) * NextFloat();

    public bool Chance(float probability) => NextFloat() < probability;
}
