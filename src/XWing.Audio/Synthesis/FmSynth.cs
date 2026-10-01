using XWing.Audio.Midi;

namespace XWing.Audio.Synthesis;

/// <summary>
/// Built-in General-MIDI-ish FM synthesizer with noise drums on channel 10. Needs no sample data,
/// so music works out of the box and has the period-correct "AdLib" character.
/// </summary>
public sealed class FmSynth : ISynth
{
    private const int MaxVoices = 48;
    private const float TwoPi = MathF.PI * 2f;

    private readonly Voice[] _voices = new Voice[MaxVoices];
    private readonly Channel[] _channels = new Channel[16];
    private ulong _noise = 0x9E3779B97F4A7C15UL;
    private long _age;

    public FmSynth(int sampleRate)
    {
        SampleRate = sampleRate;
        for (int i = 0; i < _voices.Length; i++) _voices[i] = new Voice();
        for (int i = 0; i < 16; i++) _channels[i] = new Channel();
    }

    public int SampleRate { get; }
    public float MasterGain { get; set; } = 0.35f;
    public int ActiveVoices => _voices.Count(v => v.Active);

    private sealed class Channel
    {
        public int Program;
        public float Volume = 100 / 127f;
        public float Expression = 1f;
        public float Pan = 0.5f;
        public float Bend = 1f; // frequency multiplier
    }

    private enum Stage { Off, Attack, Decay, Sustain, Release }

    private sealed class Voice
    {
        public bool Active => Stage != Stage.Off;
        public Stage Stage;
        public int Channel, Key;
        public float Velocity;
        public float Freq;
        public double PhaseC, PhaseC2, PhaseM, PhaseV;
        public float Env, ReleaseFrom;
        public float Time, ReleaseTime;
        public long Age;
        public FmPatch Patch = FmPatch.Organ;
        public DrumKind Drum;
    }

    private enum DrumKind { None, Kick, Snare, ClosedHat, OpenHat, Crash, Tom }

    public void Process(in MidiEvent e)
    {
        Channel ch = _channels[e.Channel & 15];
        switch (e.Kind)
        {
            case MidiEventKind.NoteOn: NoteOn(e.Channel, e.A, e.B); break;
            case MidiEventKind.NoteOff: NoteOff(e.Channel, e.A); break;
            case MidiEventKind.ProgramChange: ch.Program = e.A; break;
            case MidiEventKind.PitchBend: ch.Bend = MathF.Pow(2f, e.A / 8192f * 2f / 12f); break;
            case MidiEventKind.ControlChange:
                switch (e.A)
                {
                    case 7: ch.Volume = e.B / 127f; break;
                    case 10: ch.Pan = e.B / 127f; break;
                    case 11: ch.Expression = e.B / 127f; break;
                    case 120 or 123: AllNotesOff(); break;
                }
                break;
        }
    }

    private void NoteOn(int channel, int key, int velocity)
    {
        Voice v = Allocate();
        v.Stage = Stage.Attack;
        v.Channel = channel;
        v.Key = key;
        v.Velocity = velocity / 127f;
        v.Freq = 440f * MathF.Pow(2f, (key - 69) / 12f);
        v.PhaseC = v.PhaseC2 = v.PhaseM = v.PhaseV = 0;
        v.Env = 0f;
        v.Time = 0f;
        v.Age = ++_age;
        v.Drum = channel == 9 ? DrumFor(key) : DrumKind.None;
        v.Patch = channel == 9 ? FmPatch.Organ : FmPatch.ForProgram(_channels[channel].Program);
    }

    private void NoteOff(int channel, int key)
    {
        foreach (Voice v in _voices)
            if (v.Active && v.Stage != Stage.Release && v.Channel == channel && v.Key == key && v.Drum == DrumKind.None)
                Release(v);
    }

    private static void Release(Voice v)
    {
        v.Stage = Stage.Release;
        v.ReleaseFrom = v.Env;
        v.ReleaseTime = 0f;
    }

    public void AllNotesOff()
    {
        foreach (Voice v in _voices)
            if (v.Active && v.Stage != Stage.Release) Release(v);
    }

    private Voice Allocate()
    {
        Voice? free = _voices.FirstOrDefault(v => !v.Active);
        if (free is not null) return free;
        // Steal the oldest releasing voice, else the oldest voice.
        return _voices.Where(v => v.Stage == Stage.Release).MinBy(v => v.Age) ?? _voices.MinBy(v => v.Age)!;
    }

    private static DrumKind DrumFor(int key) => key switch
    {
        35 or 36 => DrumKind.Kick,
        37 or 38 or 39 or 40 => DrumKind.Snare,
        42 or 44 => DrumKind.ClosedHat,
        46 => DrumKind.OpenHat,
        49 or 51 or 52 or 55 or 57 or 59 => DrumKind.Crash,
        >= 41 and <= 50 => DrumKind.Tom,
        _ => DrumKind.ClosedHat,
    };

    private float Noise()
    {
        _noise ^= _noise << 13; _noise ^= _noise >> 7; _noise ^= _noise << 17;
        return (_noise >> 40) * (2f / (1 << 24)) - 1f;
    }

    public void Render(Span<float> left, Span<float> right)
    {
        float dt = 1f / SampleRate;
        foreach (Voice v in _voices)
        {
            if (!v.Active) continue;
            Channel ch = _channels[v.Channel];
            float amp = v.Velocity * ch.Volume * ch.Expression * MasterGain;
            float panL = MathF.Cos(ch.Pan * MathF.PI / 2f), panR = MathF.Sin(ch.Pan * MathF.PI / 2f);
            for (int i = 0; i < left.Length && v.Active; i++)
            {
                float s = v.Drum != DrumKind.None ? DrumSample(v, dt) : ToneSample(v, ch, dt);
                s *= amp;
                left[i] += s * panL;
                right[i] += s * panR;
            }
        }
    }

    private float Envelope(Voice v, FmPatch p, float dt)
    {
        v.Time += dt;
        switch (v.Stage)
        {
            case Stage.Attack:
                v.Env += dt / MathF.Max(p.Attack, 1e-4f);
                if (v.Env >= 1f) { v.Env = 1f; v.Stage = Stage.Decay; }
                break;
            case Stage.Decay:
                v.Env -= dt / MathF.Max(p.Decay, 1e-4f) * (1f - p.Sustain);
                if (v.Env <= p.Sustain) { v.Env = p.Sustain; v.Stage = Stage.Sustain; }
                break;
            case Stage.Sustain:
                if (p.Sustain <= 0f) v.Stage = Stage.Off;
                break;
            case Stage.Release:
                v.ReleaseTime += dt;
                v.Env = v.ReleaseFrom * MathF.Max(0f, 1f - v.ReleaseTime / MathF.Max(p.Release, 1e-3f));
                if (v.Env <= 0f) v.Stage = Stage.Off;
                break;
        }
        return v.Env;
    }

    private float ToneSample(Voice v, Channel ch, float dt)
    {
        FmPatch p = v.Patch;
        float env = Envelope(v, p, dt);
        float vib = p.Vibrato > 0f ? 1f + p.Vibrato * MathF.Sin(TwoPi * (float)v.PhaseV) * MathF.Min(1f, v.Time * 2f) : 1f;
        v.PhaseV = (v.PhaseV + 5.5 * dt) % 1.0;
        float f = v.Freq * ch.Bend * vib;

        float index = p.IndexDecay > 0f
            ? p.ModIndex * (p.IndexSustain + (1f - p.IndexSustain) * MathF.Exp(-v.Time / p.IndexDecay))
            : p.ModIndex;
        if (p.IndexFollowsAmp) index *= 0.35f + 0.65f * env;

        float mod = index * MathF.Sin(TwoPi * (float)v.PhaseM);
        float s = MathF.Sin(TwoPi * (float)v.PhaseC + mod);
        if (p.Detune > 0f)
        {
            s = 0.6f * (s + MathF.Sin(TwoPi * (float)v.PhaseC2 + mod));
            v.PhaseC2 = (v.PhaseC2 + f * (1f + p.Detune) * dt) % 1.0;
        }
        v.PhaseC = (v.PhaseC + f * dt) % 1.0;
        v.PhaseM = (v.PhaseM + f * p.ModRatio * dt) % 1.0;
        return s * env * p.Gain;
    }

    private float DrumSample(Voice v, float dt)
    {
        v.Time += dt;
        float t = v.Time;
        float s;
        float length;
        switch (v.Drum)
        {
            case DrumKind.Kick:
            {
                float f = 45f + 110f * MathF.Exp(-t * 30f);
                v.PhaseC = (v.PhaseC + f * dt) % 1.0;
                s = 1.4f * MathF.Sin(TwoPi * (float)v.PhaseC) * MathF.Exp(-t * 9f);
                length = 0.45f;
                break;
            }
            case DrumKind.Tom:
            {
                float f = v.Freq * 0.5f * (1f + 0.5f * MathF.Exp(-t * 20f));
                v.PhaseC = (v.PhaseC + f * dt) % 1.0;
                s = MathF.Sin(TwoPi * (float)v.PhaseC) * MathF.Exp(-t * 8f);
                length = 0.5f;
                break;
            }
            case DrumKind.Snare:
            {
                v.PhaseC = (v.PhaseC + 185f * dt) % 1.0;
                s = (0.75f * Noise() + 0.4f * MathF.Sin(TwoPi * (float)v.PhaseC)) * MathF.Exp(-t * 18f);
                length = 0.3f;
                break;
            }
            case DrumKind.ClosedHat:
            {
                float n = Noise();
                s = 0.35f * (n - v.Env) * MathF.Exp(-t * 60f); // crude high-pass: difference of successive noise
                v.Env = n;
                length = 0.08f;
                break;
            }
            case DrumKind.OpenHat:
            {
                float n = Noise();
                s = 0.3f * (n - v.Env) * MathF.Exp(-t * 9f);
                v.Env = n;
                length = 0.45f;
                break;
            }
            default: // Crash
            {
                float n = Noise();
                s = 0.35f * (n - 0.5f * v.Env) * MathF.Exp(-t * 2.2f);
                v.Env = n;
                length = 2f;
                break;
            }
        }
        if (t >= length) v.Stage = Stage.Off;
        return s;
    }
}
