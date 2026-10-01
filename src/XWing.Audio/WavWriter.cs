namespace XWing.Audio;

/// <summary>Writes 16-bit PCM WAV files, for previews and debugging.</summary>
public static class WavWriter
{
    public static void Write(string path, int sampleRate, float[] left, float[]? right = null)
    {
        int channels = right is null ? 1 : 2;
        int frames = left.Length;
        using var w = new BinaryWriter(File.Create(path));
        w.Write("RIFF"u8); w.Write(36 + frames * channels * 2); w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)channels);
        w.Write(sampleRate); w.Write(sampleRate * channels * 2); w.Write((short)(channels * 2)); w.Write((short)16);
        w.Write("data"u8); w.Write(frames * channels * 2);
        for (int i = 0; i < frames; i++)
        {
            w.Write((short)(Math.Clamp(left[i], -1f, 1f) * short.MaxValue));
            if (right is not null) w.Write((short)(Math.Clamp(right[i], -1f, 1f) * short.MaxValue));
        }
    }
}
