using XWing.Audio.Midi;
using static XWing.Audio.Midi.Notes;

namespace XWing.Audio.Music;

/// <summary>
/// Placeholder score, original material written for this project (not derived from the film or
/// game soundtracks). Orchestral GM instrumentation, so it also sounds right through a SoundFont.
/// Replace any cue by dropping a .mid file with the cue's name into the music override folder.
/// </summary>
public static class Score
{
    // General MIDI programs (0-based)
    private const int Strings = 48, SlowStrings = 49, Contrabass = 43, Timpani = 47, Harp = 46;
    private const int Trumpet = 56, FrenchHorn = 60, BrassSection = 61, FingerBass = 33, WarmPad = 89;

    public static IReadOnlyDictionary<MusicCue, CueTrack> Default() => new Dictionary<MusicCue, CueTrack>
    {
        [MusicCue.Cruise] = new(Cruise(), Loop: true),
        [MusicCue.Combat] = new(Combat(), Loop: true),
        [MusicCue.Victory] = new(Victory(), Loop: false, Then: MusicCue.Cruise),
        [MusicCue.Failure] = new(Failure(), Loop: false, Then: MusicCue.None),
    };

    /// <summary>Driving D-minor ostinato, 8 bars.</summary>
    public static MidiFile Combat()
    {
        var c = new Composer(bpm: 138);
        var ostinato = c.Part("Strings ostinato", 0, Strings, volume: 92, pan: 44);
        var pads = c.Part("High strings", 4, SlowStrings, volume: 62, pan: 84);
        var brass = c.Part("Brass", 1, BrassSection, volume: 104, pan: 70);
        var bass = c.Part("Bass", 2, FingerBass, volume: 100);
        var timp = c.Part("Timpani", 3, Timpani, volume: 96, pan: 56);
        var drums = c.Part("Percussion", 9, 0, volume: 88);

        (string Root, bool Minor)[] chords = { ("D", true), ("D", true), ("Bb", false), ("C", false), ("D", true), ("D", true), ("G", true), ("A", false) };
        int[] pattern = { 0, 0, 7, 0, 12, 0, 7, 3 };

        for (int bar = 0; bar < 8; bar++)
        {
            var (root, minor) = chords[bar];
            int r3 = N(root + "3");
            if (r3 > N("F#3")) r3 -= 12;
            for (int i = 0; i < 8; i++)
            {
                int step = pattern[i] == 3 && !minor ? 4 : pattern[i];
                ostinato.Note(bar, i * 0.5, 0.45, r3 + step, i % 2 == 0 ? 100 : 78);
            }
            pads.Chord(bar, 0, 4, Triad(root + "4", minor).Select(k => k > N("A4") ? k - 12 : k), 64);
            int b2 = r3 - 12;
            bass.Note(bar, 0, 0.75, b2, 110).Note(bar, 1.5, 0.5, b2, 90).Note(bar, 2, 0.75, b2, 104).Note(bar, 3, 0.5, b2 + 7, 90).Note(bar, 3.5, 0.5, b2 + 12, 86);
            timp.Note(bar, 0, 1, r3 - 12 + (r3 - 12 < N("F2") ? 12 : 0), 100);

            for (int i = 0; i < 8; i++) drums.Note(bar, i * 0.5, 0.1, 42, i % 2 == 0 ? 70 : 50);
            drums.Note(bar, 0, 0.2, 36, 110).Note(bar, 2, 0.2, 36, 100).Note(bar, 2.5, 0.2, 36, 80);
            drums.Note(bar, 1, 0.2, 38, 96).Note(bar, 3, 0.2, 38, 100);
        }
        drums.Note(0, 0, 2, 49, 100);
        drums.Note(4, 0, 2, 49, 90);
        for (int i = 0; i < 4; i++) timp.Note(7, 2 + i * 0.5, 0.5, N("A2"), 70 + i * 10);

        (int bar, double beat, double len, string note)[] melody =
        {
            (0, 0, 1.5, "A4"), (0, 1.5, 0.5, "D5"), (0, 2, 1, "E5"), (0, 3, 1, "F5"),
            (1, 0, 1.5, "E5"), (1, 1.5, 0.5, "D5"), (1, 2, 2, "A4"),
            (2, 0, 1, "Bb4"), (2, 1, 1, "D5"), (2, 2, 1.5, "F5"), (2, 3.5, 0.5, "E5"),
            (3, 0, 1, "E5"), (3, 1, 1, "C5"), (3, 2, 2, "G5"),
            (4, 0, 1.5, "A5"), (4, 1.5, 0.5, "G5"), (4, 2, 1, "F5"), (4, 3, 1, "E5"),
            (5, 0, 3, "D5"), (5, 3, 1, "A4"),
            (6, 0, 1, "Bb4"), (6, 1, 1, "D5"), (6, 2, 1.5, "G5"), (6, 3.5, 0.5, "F5"),
            (7, 0, 2, "E5"), (7, 2, 1, "C#5"), (7, 3, 1, "A4"),
        };
        foreach (var m in melody) brass.Note(m.bar, m.beat, m.len, N(m.note), 100);
        return c.Build(bars: 8);
    }

    /// <summary>Calm, spacious D-dorian travelling music, 8 bars.</summary>
    public static MidiFile Cruise()
    {
        var c = new Composer(bpm: 76);
        var pad = c.Part("Pad", 0, WarmPad, volume: 78, pan: 50);
        var horn = c.Part("Horn", 1, FrenchHorn, volume: 92, pan: 74);
        var bass = c.Part("Bass", 2, Contrabass, volume: 86);
        var harp = c.Part("Harp", 3, Harp, volume: 66, pan: 88);

        int[][] chords =
        {
            new[] { N("D3"), N("A3"), N("C4"), N("F4"), N("E4") },
            new[] { N("Bb2"), N("F3"), N("A3"), N("D4") },
            new[] { N("F2"), N("C3"), N("A3"), N("E4") },
            new[] { N("C3"), N("G3"), N("D4"), N("E4") },
        };
        for (int bar = 0; bar < 8; bar++)
        {
            int[] ch = chords[bar % 4];
            pad.Chord(bar, 0, 4, ch.Distinct(), 70);
            bass.Note(bar, 0, 2, ch[0] - 12, 90).Note(bar, 2, 2, ch[0] - 12 + 7, 76);
            int[] arp = { ch[0] + 12, ch[1] + 12, ch[2] + 12, ch[^1] + 12 };
            for (int i = 0; i < 8; i++) harp.Note(bar, i * 0.5, 0.9, arp[i % 4] + (i >= 4 ? 12 : 0), i == 0 ? 76 : 58);
        }

        (int bar, double beat, double len, string note)[] melody =
        {
            (0, 0, 3, "A4"),
            (1, 0, 1, "G4"), (1, 1, 1, "F4"), (1, 2, 2, "D4"),
            (2, 0, 2, "F4"), (2, 2, 2, "A4"),
            (3, 0, 4, "G4"),
            (4, 0, 3, "D5"), (4, 3, 1, "C5"),
            (5, 0, 2, "Bb4"), (5, 2, 2, "A4"),
            (6, 0, 1.5, "G4"), (6, 1.5, 0.5, "A4"), (6, 2, 2, "C5"),
            (7, 0, 4, "A4"),
        };
        foreach (var m in melody) horn.Note(m.bar, m.beat, m.len, N(m.note), 84);
        return c.Build(bars: 8);
    }

    /// <summary>Short Bb-major fanfare for objectives complete.</summary>
    public static MidiFile Victory()
    {
        var c = new Composer(bpm: 112);
        var trumpet = c.Part("Trumpet", 1, Trumpet, volume: 110, pan: 70);
        var brass = c.Part("Brass", 2, BrassSection, volume: 96, pan: 50);
        var strings = c.Part("Strings", 0, Strings, volume: 84, pan: 40);
        var timp = c.Part("Timpani", 3, Timpani, volume: 100);
        var drums = c.Part("Percussion", 9, 0, volume: 90);

        (int bar, double beat, double len, string note)[] tune =
        {
            (0, 0, 1, "Bb4"), (0, 1, 0.5, "D5"), (0, 1.5, 0.5, "F5"), (0, 2, 2, "Bb5"),
            (1, 0, 1, "A5"), (1, 1, 1, "G5"), (1, 2, 1, "F5"), (1, 3, 1, "Eb5"),
            (2, 0, 1, "D5"), (2, 1, 1, "Eb5"), (2, 2, 1.5, "F5"), (2, 3.5, 0.5, "C5"),
            (3, 0, 4, "D5"),
        };
        foreach (var m in tune) trumpet.Note(m.bar, m.beat, m.len, N(m.note), 110);

        string[] roots = { "Bb", "Eb", "F", "Bb" };
        for (int bar = 0; bar < 4; bar++)
        {
            brass.Chord(bar, 0, bar == 3 ? 4 : 1.8, Triad(roots[bar] + "3", false), 92);
            if (bar < 3) brass.Chord(bar, 2, 1.8, Triad(roots[bar] + "3", false), 84);
            strings.Chord(bar, 0, 4, Triad(roots[bar] + "4", false), 76);
            timp.Note(bar, 0, 1, N(roots[bar] == "Eb" ? "Eb3" : roots[bar] + "2"), 104);
        }
        for (int i = 0; i < 8; i++) timp.Note(2, 2 + i * 0.25, 0.25, N("F2"), 60 + i * 6);
        drums.Note(0, 0, 2, 49, 100).Note(3, 0, 3, 49, 120).Note(3, 0, 0.3, 36, 120);
        return c.Build(bars: 4);
    }

    /// <summary>Slow, falling D-minor lament for a failed mission.</summary>
    public static MidiFile Failure()
    {
        var c = new Composer(bpm: 58);
        var low = c.Part("Low strings", 0, Strings, volume: 96, pan: 52);
        var horn = c.Part("Horn", 1, FrenchHorn, volume: 90, pan: 72);
        var timp = c.Part("Timpani", 3, Timpani, volume: 80);

        int[][] chords = { new[] { N("D2"), N("A2"), N("F3") }, new[] { N("Bb1"), N("G2"), N("D3") }, new[] { N("A1"), N("E2"), N("C#3") }, new[] { N("D2"), N("A2"), N("D3") } };
        for (int bar = 0; bar < 4; bar++) low.Chord(bar, 0, 4, chords[bar], 84);
        timp.Note(0, 0, 2, N("D2"), 90).Note(3, 0, 2, N("D2"), 70);

        (int bar, double beat, double len, string note)[] line =
        {
            (0, 0, 2, "A4"), (0, 2, 1, "G4"), (0, 3, 1, "F4"),
            (1, 0, 2, "E4"), (1, 2, 2, "D4"),
            (2, 0, 2, "C#4"), (2, 2, 2, "E4"),
            (3, 0, 4, "D4"),
        };
        foreach (var m in line) horn.Note(m.bar, m.beat, m.len, N(m.note), 80);
        return c.Build(bars: 4);
    }
}
