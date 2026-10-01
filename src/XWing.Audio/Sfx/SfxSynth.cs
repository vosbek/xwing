namespace XWing.Audio.Sfx;

public enum SfxId
{
    RebelLaser,
    ImperialLaser,
    Turbolaser,
    ShieldHit,
    HullHit,
    Explosion,
    BigExplosion,
    LaserDry,
    TargetBeep,
    RadioChirp,
    Hyperspace,
    EngineLoop,
}

/// <summary>
/// Procedurally synthesized sound effects: no sample files, deterministic, and easy to tweak.
/// They aim for the character of the original's effects (a punchy laser zap, crunchy explosions,
/// a droning engine), not their exact waveforms. Output is mono float samples in [-1, 1].
/// </summary>
public static class SfxSynth
{
    public const int SampleRate = 22050;

    public static float[] Generate(SfxId id) => id switch
    {
        SfxId.RebelLaser => Laser(startHz: 1500f, endHz: 260f, length: 0.22f, buzz: 0.35f, seed: 1),
        SfxId.ImperialLaser => Laser(startHz: 1900f, endHz: 420f, length: 0.18f, buzz: 0.7f, seed: 2),
        SfxId.Turbolaser => Laser(startHz: 700f, endHz: 110f, length: 0.4f, buzz: 0.5f, seed: 3),
        SfxId.ShieldHit => ShieldHit(),
        SfxId.HullHit => Crunch(0.25f, 4),
        SfxId.Explosion => Explosion(1.4f, 5),
        SfxId.BigExplosion => Explosion(3.0f, 6),
        SfxId.LaserDry => Click(),
        SfxId.TargetBeep => Beep(new[] { 1320f, 1760f }, 0.05f),
        SfxId.RadioChirp => Beep(new[] { 2200f, 1650f }, 0.04f),
        SfxId.Hyperspace => Hyperspace(),
        SfxId.EngineLoop => EngineLoop(),
        _ => Array.Empty<float>(),
    };

    private static float[] Buffer(float seconds) => new float[(int)(seconds * SampleRate)];

    private sealed class Noise(ulong seed)
    {
        private ulong _s = seed * 0x9E3779B97F4A7C15UL | 1;
        public float Next() { _s ^= _s << 13; _s ^= _s >> 7; _s ^= _s << 17; return (_s >> 40) * (2f / (1 << 24)) - 1f; }
    }

    private static float[] Laser(float startHz, float endHz, float length, float buzz, int seed)
    {
        var b = Buffer(length);
        var n = new Noise((ulong)seed);
        double phase = 0;
        for (int i = 0; i < b.Length; i++)
        {
            float t = i / (float)SampleRate, u = t / length;
            float f = endHz + (startHz - endHz) * MathF.Exp(-u * 4f);
            phase += f / SampleRate;
            float saw = (float)(2 * (phase % 1.0) - 1);
            float sine = MathF.Sin((float)(phase * 2 * Math.PI));
            float env = MathF.Min(1f, t * 400f) * MathF.Pow(1f - u, 1.6f);
            b[i] = (sine * (1f - buzz) + saw * buzz + n.Next() * 0.08f) * env * 0.8f;
        }
        return b;
    }

    private static float[] ShieldHit()
    {
        var b = Buffer(0.3f);
        var n = new Noise(7);
        for (int i = 0; i < b.Length; i++)
        {
            float t = i / (float)SampleRate;
            float ring = MathF.Sin(2 * MathF.PI * 880f * t + 3f * MathF.Sin(2 * MathF.PI * 1230f * t));
            b[i] = (0.5f * ring + 0.5f * n.Next()) * MathF.Exp(-t * 14f) * 0.8f;
        }
        return b;
    }

    private static float[] Crunch(float length, int seed)
    {
        var b = Buffer(length);
        var n = new Noise((ulong)seed);
        float lp = 0f;
        for (int i = 0; i < b.Length; i++)
        {
            float t = i / (float)SampleRate;
            lp += (n.Next() - lp) * 0.3f;
            b[i] = MathF.Tanh(lp * 4f) * MathF.Exp(-t * 12f) * 0.9f;
        }
        return b;
    }

    private static float[] Explosion(float length, int seed)
    {
        var b = Buffer(length);
        var n = new Noise((ulong)seed);
        float lp1 = 0f, lp2 = 0f;
        for (int i = 0; i < b.Length; i++)
        {
            float t = i / (float)SampleRate, u = t / length;
            // Filter opens on impact then closes: bright crack into a long rumble.
            float cutoff = 0.02f + 0.5f * MathF.Exp(-t * 10f);
            lp1 += (n.Next() - lp1) * cutoff;
            lp2 += (lp1 - lp2) * cutoff;
            float thump = MathF.Sin(2 * MathF.PI * (40f + 60f * MathF.Exp(-t * 15f)) * t) * MathF.Exp(-t * 6f);
            float env = MathF.Min(1f, t * 300f) * MathF.Pow(1f - u, 2f);
            b[i] = MathF.Tanh((lp2 * 6f + thump) * env * 1.5f) * 0.9f;
        }
        return b;
    }

    private static float[] Click()
    {
        var b = Buffer(0.04f);
        for (int i = 0; i < b.Length; i++)
        {
            float t = i / (float)SampleRate;
            b[i] = MathF.Sign(MathF.Sin(2 * MathF.PI * 320f * t)) * MathF.Exp(-t * 120f) * 0.4f;
        }
        return b;
    }

    private static float[] Beep(float[] tones, float each)
    {
        var b = Buffer(each * tones.Length);
        int per = b.Length / tones.Length;
        for (int i = 0; i < b.Length; i++)
        {
            int k = Math.Min(i / per, tones.Length - 1);
            float t = (i - k * per) / (float)SampleRate;
            float env = MathF.Min(1f, t * 800f) * MathF.Min(1f, (each - t) * 800f);
            b[i] = MathF.Sign(MathF.Sin(2 * MathF.PI * tones[k] * t)) * env * 0.25f;
        }
        return b;
    }

    private static float[] Hyperspace()
    {
        const float length = 2.5f;
        var b = Buffer(length);
        var n = new Noise(9);
        float lp = 0f;
        double phase = 0;
        for (int i = 0; i < b.Length; i++)
        {
            float t = i / (float)SampleRate, u = t / length;
            float f = 60f + 900f * u * u;
            phase += f / SampleRate;
            lp += (n.Next() - lp) * (0.02f + 0.4f * u);
            float env = MathF.Sin(MathF.PI * MathF.Min(1f, u * 1.15f));
            b[i] = (0.5f * MathF.Sin((float)(phase * 2 * Math.PI)) + 1.5f * lp) * env * 0.7f;
        }
        return b;
    }

    /// <summary>One second that loops seamlessly: every component completes whole cycles.</summary>
    private static float[] EngineLoop()
    {
        var b = Buffer(1f);
        for (int i = 0; i < b.Length; i++)
        {
            float t = i / (float)SampleRate;
            float s = MathF.Sin(2 * MathF.PI * 55f * t) * 0.5f
                      + MathF.Sin(2 * MathF.PI * 110f * t) * 0.25f
                      + MathF.Sin(2 * MathF.PI * 167f * t) * 0.12f
                      + MathF.Sin(2 * MathF.PI * 331f * t) * 0.05f;
            float wobble = 1f + 0.15f * MathF.Sin(2 * MathF.PI * 3f * t);
            b[i] = s * wobble * 0.5f;
        }
        return b;
    }

    /// <summary>16-bit little-endian PCM, the format game engines and WAV files want.</summary>
    public static byte[] ToPcm16(float[] samples)
    {
        var bytes = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            short v = (short)(Math.Clamp(samples[i], -1f, 1f) * short.MaxValue);
            bytes[2 * i] = (byte)v;
            bytes[2 * i + 1] = (byte)(v >> 8);
        }
        return bytes;
    }
}
