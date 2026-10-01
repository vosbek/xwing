using XWing.Audio.Midi;

namespace XWing.Audio.Synthesis;

/// <summary>Anything that turns MIDI channel events into stereo audio.</summary>
public interface ISynth
{
    int SampleRate { get; }
    void Process(in MidiEvent e);
    /// <summary>Release every sounding note (they fade out with their release envelope).</summary>
    void AllNotesOff();
    /// <summary>Adds (mixes) audio into the buffers.</summary>
    void Render(Span<float> left, Span<float> right);
}
