using XWing.Audio.Midi;
using XWing.Audio.Music;
using XWing.Audio.Sfx;
using XWing.Audio.Synthesis;
using XWing.Sim.Core;
using XWing.Sim.Missions;
using static XWing.Audio.Midi.Notes;

namespace XWing.Sim.Tests;

public class AudioTests
{
    /// <summary>Records which sample each event arrived at.</summary>
    private sealed class RecordingSynth(int rate) : ISynth
    {
        public int SampleRate => rate;
        public long Sample;
        public readonly List<(long Sample, MidiEvent Event)> Events = new();
        public int NotesOffCalls;
        public void Process(in MidiEvent e) => Events.Add((Sample, e));
        public void AllNotesOff() => NotesOffCalls++;
        public void Render(Span<float> left, Span<float> right) => Sample += left.Length;
    }

    private static void Render(MidiSequencer seq, double seconds, int block = 256)
    {
        int n = (int)(seconds * seq.Synth.SampleRate);
        var l = new float[block];
        var r = new float[block];
        for (int i = 0; i < n; i += block) seq.Render(l.AsSpan(0, Math.Min(block, n - i)), r.AsSpan(0, Math.Min(block, n - i)));
    }

    private static MidiFile OneBar(double bpm, int key = 60)
    {
        var c = new Composer(bpm);
        c.Part("p", 0, 0).Note(0, 1, 1, key);
        return c.Build(bars: 1);
    }

    [Fact]
    public void Note_names()
    {
        Assert.Equal(60, N("C4"));
        Assert.Equal(69, N("A4"));
        Assert.Equal(58, N("Bb3"));
        Assert.Equal(73, N("C#5"));
    }

    [Fact]
    public void Midi_round_trips_through_bytes()
    {
        MidiFile original = Score.Combat();
        MidiFile copy = MidiFile.Read(original.Write());
        Assert.Equal(original.TicksPerQuarter, copy.TicksPerQuarter);
        Assert.Equal(original.Tracks.Count, copy.Tracks.Count);
        for (int t = 0; t < original.Tracks.Count; t++)
            Assert.Equal(original.Tracks[t].Events.OrderBy(e => e.Tick), copy.Tracks[t].Events);
    }

    [Fact]
    public void Reader_handles_running_status_and_zero_velocity_note_off()
    {
        byte[] data =
        {
            (byte)'M', (byte)'T', (byte)'h', (byte)'d', 0, 0, 0, 6, 0, 0, 0, 1, 0x01, 0xE0,
            (byte)'M', (byte)'T', (byte)'r', (byte)'k', 0, 0, 0, 12,
            0x00, 0x90, 60, 100,  // note on
            0x60, 62, 90,         // running status: note on 62
            0x10, 60, 0,          // running status, velocity 0 = note off
            0x00, 0xFF, 0x2F, 0x00,
        };
        MidiFile f = MidiFile.Read(data);
        var e = f.Tracks[0].Events;
        Assert.Equal(new MidiEvent(0, MidiEventKind.NoteOn, 0, 60, 100), e[0]);
        Assert.Equal(new MidiEvent(0x60, MidiEventKind.NoteOn, 0, 62, 90), e[1]);
        Assert.Equal(MidiEventKind.NoteOff, e[2].Kind);
        Assert.Equal(0x70, e[2].Tick);
    }

    [Fact]
    public void Sequencer_places_events_at_the_right_sample()
    {
        var synth = new RecordingSynth(10_000);
        var seq = new MidiSequencer(synth);
        seq.Play(OneBar(bpm: 120), loop: false);
        Render(seq, 3);
        // Beat 1 at 120 bpm = 0.5 s = sample 5000.
        long at = synth.Events.First(e => e.Event.Kind == MidiEventKind.NoteOn).Sample;
        Assert.InRange(at, 4999, 5001);
        Assert.False(seq.IsPlaying);
    }

    [Fact]
    public void Looping_cue_repeats_with_no_gap()
    {
        var synth = new RecordingSynth(10_000);
        var seq = new MidiSequencer(synth);
        seq.Play(OneBar(bpm: 120), loop: true); // bar = 2 s
        Render(seq, 6.9);
        long[] onsets = synth.Events.Where(e => e.Event.Kind == MidiEventKind.NoteOn).Select(e => e.Sample).ToArray();
        Assert.Equal(4, onsets.Length);
        for (int i = 1; i < onsets.Length; i++) Assert.InRange(onsets[i] - onsets[i - 1], 19_998, 20_002);
        Assert.Equal(3, seq.BarsElapsed);
    }

    [Fact]
    public void Composer_clamps_ringing_notes_to_the_loop_point()
    {
        var c = new Composer(120);
        c.Part("p", 0, 0).Note(0, 3.5, 2, 60);
        Assert.Equal(4 * 480, c.Build(bars: 1).LengthTicks);
    }

    [Fact]
    public void Director_switches_on_the_next_bar_line()
    {
        var cues = new Dictionary<MusicCue, CueTrack>
        {
            [MusicCue.Cruise] = new(OneBar(120, 60), Loop: true),
            [MusicCue.Combat] = new(OneBar(120, 72), Loop: true),
        };
        var director = new MusicDirector(new RecordingSynth(10_000), cues);
        var l = new float[1000];
        var r = new float[1000];
        void Seconds(double s) { for (int i = 0; i < s * 10; i++) director.Render(l, r); }

        director.Request(MusicCue.Cruise);
        Assert.Equal(MusicCue.Cruise, director.Current);
        Seconds(0.5);
        director.Request(MusicCue.Combat);
        Seconds(1.0); // t = 1.5 s, bar line at 2 s
        Assert.Equal(MusicCue.Cruise, director.Current);
        Seconds(0.6);
        Assert.Equal(MusicCue.Combat, director.Current);
    }

    [Fact]
    public void Stinger_is_not_interrupted_and_hands_over_to_its_follow_up()
    {
        var cues = new Dictionary<MusicCue, CueTrack>
        {
            [MusicCue.Cruise] = new(OneBar(120, 60), Loop: true),
            [MusicCue.Combat] = new(OneBar(120, 64), Loop: true),
            [MusicCue.Victory] = new(OneBar(120, 72), Loop: false, Then: MusicCue.Cruise),
        };
        var director = new MusicDirector(new RecordingSynth(10_000), cues);
        var l = new float[1000];
        var r = new float[1000];

        director.Request(MusicCue.Victory);
        director.Request(MusicCue.Combat); // ignored while the fanfare plays
        for (int i = 0; i < 15; i++) director.Render(l, r);
        Assert.Equal(MusicCue.Victory, director.Current);
        for (int i = 0; i < 10; i++) director.Render(l, r);
        Assert.Equal(MusicCue.Cruise, director.Current);
    }

    [Fact]
    public void Mood_follows_the_mission()
    {
        World w = World.ForMission(MissionDefinition.LoadBuiltIn("vertical_slice"), seed: 1, autopilotPlayer: true);
        var mood = new MoodTracker();
        Assert.Equal(MusicCue.Cruise, mood.Evaluate(w)); // Alpha starts > 5 km out

        var seen = new List<MusicCue>();
        while (w.Mission!.Result is null && w.Time < 600f)
        {
            w.Step();
            MusicCue cue = mood.Evaluate(w);
            if (seen.Count == 0 || seen[^1] != cue) seen.Add(cue);
        }
        Assert.Contains(MusicCue.Combat, seen);
        Assert.Single(seen, c => c == MusicCue.Victory);
        Assert.True(seen.IndexOf(MusicCue.Combat) < seen.IndexOf(MusicCue.Victory));
    }

    [Fact]
    public void Fm_synth_sounds_and_releases()
    {
        var synth = new FmSynth(22050);
        synth.Process(new MidiEvent(0, MidiEventKind.ProgramChange, 0, 61));
        synth.Process(new MidiEvent(0, MidiEventKind.NoteOn, 0, 62, 110));
        synth.Process(new MidiEvent(0, MidiEventKind.NoteOn, 9, 38, 110)); // snare
        var l = new float[4410];
        var r = new float[4410];
        synth.Render(l, r);
        Assert.True(l.Max(MathF.Abs) > 0.05f);
        Assert.All(l, s => Assert.InRange(s, -1.5f, 1.5f));

        synth.Process(new MidiEvent(0, MidiEventKind.NoteOff, 0, 62));
        for (int i = 0; i < 20; i++) synth.Render(l, r);
        Assert.Equal(0, synth.ActiveVoices);
    }

    [Fact]
    public void Default_score_is_complete_and_loops_on_bar_lines()
    {
        var score = Score.Default();
        Assert.All(new[] { MusicCue.Cruise, MusicCue.Combat, MusicCue.Victory, MusicCue.Failure }, c => Assert.True(score.ContainsKey(c)));
        foreach (var (_, track) in score)
            Assert.Equal(0, track.File.LengthTicks % (4 * track.File.TicksPerQuarter));
    }

    [Theory]
    [MemberData(nameof(AllSfx))]
    public void Sound_effects_are_bounded_and_non_silent(SfxId id)
    {
        float[] s = SfxSynth.Generate(id);
        Assert.NotEmpty(s);
        Assert.All(s, v => Assert.InRange(v, -1f, 1f));
        Assert.True(s.Max(MathF.Abs) > 0.1f);
    }

    public static IEnumerable<object[]> AllSfx() => Enum.GetValues<SfxId>().Select(id => new object[] { id });

    [Fact]
    public void Engine_loop_is_seamless()
    {
        float[] s = SfxSynth.Generate(SfxId.EngineLoop);
        float maxStep = Enumerable.Range(1, s.Length - 1).Max(i => MathF.Abs(s[i] - s[i - 1]));
        Assert.True(MathF.Abs(s[0] - s[^1]) <= maxStep * 1.5f, "wrap-around jump larger than any step inside the loop");
    }
}
