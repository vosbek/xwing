using MeltySynth;
using XWing.Audio.Midi;

namespace XWing.Audio.Synthesis;

/// <summary>
/// Optional higher-fidelity music via a General MIDI SoundFont (.sf2) the player supplies.
/// Uses MeltySynth (MIT). No SoundFont ships with the project.
/// </summary>
public sealed class SoundFontSynth : ISynth
{
    private readonly Synthesizer _synth;

    public SoundFontSynth(string soundFontPath, int sampleRate)
    {
        _synth = new Synthesizer(soundFontPath, sampleRate);
        SampleRate = sampleRate;
    }

    public int SampleRate { get; }

    public void Process(in MidiEvent e)
    {
        switch (e.Kind)
        {
            case MidiEventKind.NoteOn: _synth.NoteOn(e.Channel, e.A, e.B); break;
            case MidiEventKind.NoteOff: _synth.NoteOff(e.Channel, e.A); break;
            case MidiEventKind.ControlChange: _synth.ProcessMidiMessage(e.Channel, 0xB0, e.A, e.B); break;
            case MidiEventKind.ProgramChange: _synth.ProcessMidiMessage(e.Channel, 0xC0, e.A, 0); break;
            case MidiEventKind.PitchBend:
            {
                int v = e.A + 8192;
                _synth.ProcessMidiMessage(e.Channel, 0xE0, v & 0x7F, (v >> 7) & 0x7F);
                break;
            }
        }
    }

    public void AllNotesOff() => _synth.NoteOffAll(false);

    public void Render(Span<float> left, Span<float> right)
    {
        // MeltySynth overwrites; we mix.
        Span<float> l = left.Length <= 4096 ? stackalloc float[left.Length] : new float[left.Length];
        Span<float> r = right.Length <= 4096 ? stackalloc float[right.Length] : new float[right.Length];
        _synth.Render(l, r);
        for (int i = 0; i < left.Length; i++) { left[i] += l[i]; right[i] += r[i]; }
    }
}
