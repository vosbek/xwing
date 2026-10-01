namespace XWing.Audio.Midi;

/// <summary>Small helper for writing music in code, in beats rather than ticks.</summary>
public sealed class Composer
{
    private readonly MidiFile _file;
    private readonly MidiTrack _conductor;

    public Composer(double bpm, int beatsPerBar = 4, int ticksPerQuarter = 480)
    {
        _file = new MidiFile { TicksPerQuarter = ticksPerQuarter };
        _conductor = new MidiTrack { Name = "Conductor" };
        _conductor.Events.Add(new MidiEvent(0, MidiEventKind.Tempo, A: (int)Math.Round(60_000_000 / bpm)));
        _conductor.Events.Add(new MidiEvent(0, MidiEventKind.TimeSignature, A: beatsPerBar, B: 2));
        _file.Tracks.Add(_conductor);
        BeatsPerBar = beatsPerBar;
    }

    public int BeatsPerBar { get; }

    public Part Part(string name, int channel, int program, int volume = 100, int pan = 64)
    {
        var track = new MidiTrack { Name = name };
        _file.Tracks.Add(track);
        if (channel != 9) track.Events.Add(new MidiEvent(0, MidiEventKind.ProgramChange, channel, program));
        track.Events.Add(new MidiEvent(0, MidiEventKind.ControlChange, channel, 7, volume));
        track.Events.Add(new MidiEvent(0, MidiEventKind.ControlChange, channel, 10, pan));
        return new Part(track, channel, _file.TicksPerQuarter, BeatsPerBar);
    }

    /// <summary>Pads the conductor track so the file is exactly <paramref name="bars"/> long (for clean loops).</summary>
    public MidiFile Build(int bars)
    {
        long end = (long)bars * BeatsPerBar * _file.TicksPerQuarter;
        // Notes ringing past the loop point would make the file a bar longer and leave a silent
        // gap before the loop restarts. Cut them at the end instead.
        foreach (MidiTrack t in _file.Tracks)
            for (int i = 0; i < t.Events.Count; i++)
                if (t.Events[i] is { Kind: MidiEventKind.NoteOff } e && e.Tick >= end)
                    t.Events[i] = e with { Tick = end - 1 };
        _conductor.Events.Add(new MidiEvent(end, MidiEventKind.ControlChange, 0, 111, 0)); // marker CC at loop point
        return _file;
    }
}

public sealed class Part(MidiTrack track, int channel, int ppq, int beatsPerBar)
{
    private long T(double beat) => (long)Math.Round(beat * ppq);

    /// <summary>Note at <paramref name="bar"/> (0-based) + <paramref name="beat"/>, lasting <paramref name="length"/> beats.</summary>
    public Part Note(int bar, double beat, double length, int key, int velocity = 96)
    {
        double start = bar * beatsPerBar + beat;
        // End a hair early so repeated notes re-articulate cleanly.
        track.Events.Add(new MidiEvent(T(start + length) - 1, MidiEventKind.NoteOff, channel, key));
        track.Events.Add(new MidiEvent(T(start), MidiEventKind.NoteOn, channel, key, velocity));
        return this;
    }

    public Part Chord(int bar, double beat, double length, IEnumerable<int> keys, int velocity = 80)
    {
        foreach (int k in keys) Note(bar, beat, length, k, velocity);
        return this;
    }
}

/// <summary>Note names: <c>N("D", 4)</c> = 62.</summary>
public static class Notes
{
    private static readonly Dictionary<string, int> Pitch = new()
    {
        ["C"] = 0, ["C#"] = 1, ["Db"] = 1, ["D"] = 2, ["D#"] = 3, ["Eb"] = 3, ["E"] = 4, ["F"] = 5,
        ["F#"] = 6, ["Gb"] = 6, ["G"] = 7, ["G#"] = 8, ["Ab"] = 8, ["A"] = 9, ["A#"] = 10, ["Bb"] = 10, ["B"] = 11,
    };

    /// <summary>"D4", "Bb3", "C#5" → MIDI key (C4 = 60).</summary>
    public static int N(string name)
    {
        int split = name.Length - (char.IsDigit(name[^1]) && name.Length > 2 && name[^2] == '-' ? 2 : 1);
        return Pitch[name[..split]] + (int.Parse(name[split..]) + 1) * 12;
    }

    public static int[] Triad(string root, bool minor) { int r = N(root); return new[] { r, r + (minor ? 3 : 4), r + 7 }; }
}
