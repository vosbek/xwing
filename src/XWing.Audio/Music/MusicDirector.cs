using XWing.Audio.Midi;
using XWing.Audio.Synthesis;

namespace XWing.Audio.Music;

public enum MusicCue
{
    None,
    Cruise,
    Combat,
    Victory,
    Failure,
}

/// <param name="Loop">Loop until another cue is requested.</param>
/// <param name="Then">For one-shot cues (stingers): what plays afterwards.</param>
public sealed record CueTrack(MidiFile File, bool Loop, MusicCue Then = MusicCue.None);

/// <summary>
/// Adaptive music in the spirit of LucasArts' iMUSE: the game asks for a mood, and the director
/// switches on the next bar line so transitions land on the beat instead of cutting mid-phrase.
/// Stingers (victory, failure) can't be interrupted by ordinary mood changes.
/// </summary>
public sealed class MusicDirector
{
    private readonly MidiSequencer _sequencer;
    private readonly Dictionary<MusicCue, CueTrack> _cues;
    private MusicCue? _pending;
    private long _requestedAtBar;

    public MusicDirector(ISynth synth, IReadOnlyDictionary<MusicCue, CueTrack> cues)
    {
        _sequencer = new MidiSequencer(synth);
        _cues = cues.ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    public MusicCue Current { get; private set; } = MusicCue.None;
    public MusicCue? Pending => _pending;
    public float Volume { get; set; } = 1f;
    public bool Muted { get; set; }
    public int SampleRate => _sequencer.Synth.SampleRate;

    private static bool IsStinger(MusicCue c) => c is MusicCue.Victory or MusicCue.Failure;

    /// <summary>Ask for a mood. Cheap to call every frame; repeated requests are ignored.</summary>
    public void Request(MusicCue cue)
    {
        if (cue == Current && _pending is null) return;
        if (cue == _pending) return;
        // A playing or queued stinger finishes before ordinary moods take over.
        if (!IsStinger(cue) && (IsStinger(Current) && _sequencer.IsPlaying || _pending is { } p && IsStinger(p))) return;

        if (Current == MusicCue.None || !_sequencer.IsPlaying)
        {
            Switch(cue);
            return;
        }
        _pending = cue;
        _requestedAtBar = _sequencer.BarsElapsed;
    }

    public void Render(Span<float> left, Span<float> right)
    {
        left.Clear();
        right.Clear();
        int offset = 0;
        while (offset < left.Length)
        {
            int frames = left.Length - offset;
            if (_pending is not null && _sequencer.IsPlaying) frames = Math.Min(frames, _sequencer.FramesUntilNextBar());

            _sequencer.Render(left.Slice(offset, frames), right.Slice(offset, frames));
            offset += frames;

            if (_pending is { } next && (!_sequencer.IsPlaying || _sequencer.BarsElapsed > _requestedAtBar))
                Switch(next);
            else if (!_sequencer.IsPlaying && Current != MusicCue.None && _cues.TryGetValue(Current, out var done) && !done.Loop)
                Switch(done.Then);
        }

        float gain = Muted ? 0f : Volume;
        for (int i = 0; i < left.Length; i++)
        {
            // Gentle soft clip so stacked voices never wrap.
            left[i] = MathF.Tanh(left[i] * gain);
            right[i] = MathF.Tanh(right[i] * gain);
        }
    }

    private void Switch(MusicCue cue)
    {
        _pending = null;
        _sequencer.Stop();
        Current = cue;
        if (cue != MusicCue.None && _cues.TryGetValue(cue, out CueTrack? track))
            _sequencer.Play(track.File, track.Loop);
    }
}
