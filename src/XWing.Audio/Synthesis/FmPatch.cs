namespace XWing.Audio.Synthesis;

/// <summary>
/// Two-operator FM voice, in the spirit of the AdLib/Sound Blaster OPL2 that most 1993 players heard.
/// </summary>
public sealed record FmPatch(
    float ModRatio,
    float ModIndex,
    /// <summary>Seconds for the modulation index to decay toward <see cref="IndexSustain"/> (0 = constant).</summary>
    float IndexDecay,
    float IndexSustain,
    float Attack,
    float Decay,
    float Sustain,
    float Release,
    float Gain = 1f,
    /// <summary>Vibrato depth as a fraction of frequency.</summary>
    float Vibrato = 0f,
    /// <summary>Adds a second carrier detuned by this fraction (chorus for strings and pads).</summary>
    float Detune = 0f,
    /// <summary>Brightness follows loudness, the classic FM brass trick.</summary>
    bool IndexFollowsAmp = false)
{
    public static FmPatch ForProgram(int program) => program switch
    {
        >= 0 and <= 7 => Piano,
        45 or 46 => Pluck,
        47 => Timpani,
        >= 32 and <= 39 => Bass,
        42 or 43 => LowStrings,
        >= 40 and <= 55 => Strings,
        >= 56 and <= 63 => Brass,
        >= 64 and <= 79 => Reed,
        >= 80 and <= 87 => Lead,
        >= 88 and <= 95 => Pad,
        _ => Organ,
    };

    public static readonly FmPatch Piano = new(1f, 1.8f, 0.4f, 0.2f, 0.004f, 1.2f, 0.15f, 0.3f, 0.8f);
    public static readonly FmPatch Pluck = new(2f, 1.2f, 0.15f, 0.1f, 0.002f, 0.9f, 0f, 0.5f, 0.7f);
    public static readonly FmPatch Timpani = new(1.48f, 1.4f, 0.25f, 0.2f, 0.003f, 1.4f, 0f, 1.0f, 1.2f);
    public static readonly FmPatch Bass = new(1f, 2.2f, 0.18f, 0.6f, 0.005f, 0.5f, 0.6f, 0.08f, 0.9f);
    public static readonly FmPatch LowStrings = new(1f, 1.1f, 0f, 1f, 0.08f, 0.3f, 0.9f, 0.35f, 0.75f, 0.003f, 0.004f);
    public static readonly FmPatch Strings = new(1f, 0.9f, 0f, 1f, 0.12f, 0.3f, 0.85f, 0.45f, 0.55f, 0.004f, 0.004f);
    public static readonly FmPatch Brass = new(1f, 3.2f, 0f, 1f, 0.035f, 0.25f, 0.8f, 0.15f, 0.6f, 0.002f, 0f, IndexFollowsAmp: true);
    public static readonly FmPatch Reed = new(2f, 1.5f, 0f, 1f, 0.03f, 0.2f, 0.8f, 0.1f, 0.5f, 0.004f);
    public static readonly FmPatch Lead = new(1f, 1.3f, 0f, 1f, 0.01f, 0.2f, 0.9f, 0.1f, 0.45f, 0.005f);
    public static readonly FmPatch Pad = new(1f, 0.7f, 0f, 1f, 0.6f, 0.5f, 0.85f, 1.2f, 0.45f, 0.003f, 0.006f);
    public static readonly FmPatch Organ = new(2f, 1f, 0f, 1f, 0.01f, 0.1f, 0.9f, 0.08f, 0.45f);
}
