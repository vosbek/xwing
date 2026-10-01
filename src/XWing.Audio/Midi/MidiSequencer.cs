using XWing.Audio.Synthesis;

namespace XWing.Audio.Midi;

/// <summary>
/// Plays a <see cref="MidiFile"/> into an <see cref="ISynth"/> with sample-accurate event timing.
/// Tracks bar boundaries so the music director can switch cues on the beat.
/// </summary>
public sealed class MidiSequencer(ISynth synth)
{
    private MidiEvent[] _events = Array.Empty<MidiEvent>();
    private int _next;
    private double _tick;
    private double _samplesPerTick;
    private long _lengthTicks;
    private int _ticksPerBar;
    private int _ppq;

    public ISynth Synth => synth;
    public bool IsPlaying { get; private set; }
    public bool Loop { get; private set; }
    public double CurrentTick => _tick;
    /// <summary>Monotonic count of bar lines crossed since <see cref="Play"/> (loop wraps count too).</summary>
    public long BarsElapsed { get; private set; }

    public void Play(MidiFile file, bool loop)
    {
        _events = file.Tracks.SelectMany(t => t.Events)
            .Select((e, i) => (e, i))
            .OrderBy(x => x.e.Tick).ThenBy(x => x.e.Kind == MidiEventKind.NoteOn ? 1 : 0).ThenBy(x => x.i)
            .Select(x => x.e).ToArray();
        _ppq = file.TicksPerQuarter;
        SetTempo(500_000);
        var sig = _events.FirstOrDefault(e => e.Kind == MidiEventKind.TimeSignature);
        int beats = sig.Kind == MidiEventKind.TimeSignature ? sig.A : 4;
        int unit = sig.Kind == MidiEventKind.TimeSignature ? 1 << sig.B : 4;
        _ticksPerBar = Math.Max(1, beats * _ppq * 4 / unit);
        // Loop length: round up to a whole bar.
        long last = file.LengthTicks;
        _lengthTicks = Math.Max(_ticksPerBar, (last + _ticksPerBar - 1) / _ticksPerBar * _ticksPerBar);
        _next = 0;
        _tick = 0;
        BarsElapsed = 0;
        Loop = loop;
        IsPlaying = true;
    }

    public void Stop()
    {
        IsPlaying = false;
        synth.AllNotesOff();
    }

    private void SetTempo(int microsPerQuarter) =>
        _samplesPerTick = microsPerQuarter / 1_000_000.0 * synth.SampleRate / _ppq;

    /// <summary>Frames until the next bar line at the current tempo (≥ 1 while playing).</summary>
    public int FramesUntilNextBar()
    {
        if (!IsPlaying) return int.MaxValue;
        double nextBar = (Math.Floor(_tick / _ticksPerBar + 1e-9) + 1) * _ticksPerBar;
        return Math.Max(1, (int)Math.Ceiling((nextBar - _tick) * _samplesPerTick - 1e-6));
    }

    /// <summary>Advances playback and mixes the synth output into the buffers.</summary>
    public void Render(Span<float> left, Span<float> right)
    {
        int offset = 0;
        while (offset < left.Length)
        {
            if (IsPlaying) Dispatch();

            int frames = left.Length - offset;
            if (IsPlaying)
            {
                double target = _next < _events.Length ? Math.Min(_events[_next].Tick, _lengthTicks) : _lengthTicks;
                int untilEvent = Math.Max(1, (int)Math.Ceiling((target - _tick) * _samplesPerTick - 1e-6));
                frames = Math.Min(frames, untilEvent);
            }

            synth.Render(left.Slice(offset, frames), right.Slice(offset, frames));
            offset += frames;

            if (!IsPlaying) continue;
            long barBefore = (long)Math.Floor(_tick / _ticksPerBar + 1e-9);
            _tick += frames / _samplesPerTick;
            long barAfter = (long)Math.Floor(_tick / _ticksPerBar + 1e-9);
            BarsElapsed += Math.Max(0, barAfter - barBefore);

            if (_tick >= _lengthTicks - 1e-6 && _next >= _events.Length)
            {
                if (Loop)
                {
                    _tick -= _lengthTicks;
                    if (_tick < 0) _tick = 0;
                    _next = 0;
                }
                else
                {
                    IsPlaying = false;
                    synth.AllNotesOff();
                }
            }
        }
    }

    private void Dispatch()
    {
        while (_next < _events.Length && _events[_next].Tick <= _tick + 1e-6)
        {
            MidiEvent e = _events[_next++];
            if (e.Kind == MidiEventKind.Tempo) SetTempo(e.A);
            else if (e.Kind != MidiEventKind.TimeSignature) synth.Process(e);
        }
    }
}
