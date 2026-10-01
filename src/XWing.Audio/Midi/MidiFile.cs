namespace XWing.Audio.Midi;

public enum MidiEventKind
{
    NoteOn,
    NoteOff,
    ControlChange,
    ProgramChange,
    PitchBend,
    /// <summary>A = microseconds per quarter note.</summary>
    Tempo,
    /// <summary>A = numerator, B = denominator (as a power of two, e.g. 2 = quarter).</summary>
    TimeSignature,
}

/// <summary>
/// One event at an absolute tick. Channel events use Channel/A/B as in the MIDI spec
/// (note + velocity, controller + value, program, signed pitch bend in A).
/// </summary>
public readonly record struct MidiEvent(long Tick, MidiEventKind Kind, int Channel = 0, int A = 0, int B = 0);

public sealed class MidiTrack
{
    public string Name { get; set; } = "";
    public List<MidiEvent> Events { get; } = new();
}

/// <summary>Standard MIDI File (format 0 and 1, PPQ timing) reader and writer.</summary>
public sealed class MidiFile
{
    public int TicksPerQuarter { get; init; } = 480;
    public List<MidiTrack> Tracks { get; } = new();

    public long LengthTicks => Tracks.Count == 0 ? 0 : Tracks.Max(t => t.Events.Count == 0 ? 0 : t.Events.Max(e => e.Tick));

    public static MidiFile Read(byte[] data)
    {
        var r = new Reader(data);
        if (r.Tag() != "MThd") throw new InvalidDataException("Not a MIDI file (missing MThd)");
        int headerLength = r.Int32();
        int format = r.Int16();
        int trackCount = r.Int16();
        int division = r.Int16();
        r.Skip(headerLength - 6);
        if (format > 1) throw new NotSupportedException($"MIDI format {format} is not supported");
        if ((division & 0x8000) != 0) throw new NotSupportedException("SMPTE time division is not supported");

        var file = new MidiFile { TicksPerQuarter = division };
        for (int t = 0; t < trackCount && !r.End; t++)
        {
            string tag = r.Tag();
            int length = r.Int32();
            if (tag != "MTrk") { r.Skip(length); t--; continue; }
            file.Tracks.Add(ReadTrack(r, r.Position + length));
        }
        return file;
    }

    private static MidiTrack ReadTrack(Reader r, int end)
    {
        var track = new MidiTrack();
        long tick = 0;
        int running = 0;
        while (r.Position < end)
        {
            tick += r.VarInt();
            int status = r.Byte();
            if (status < 0x80)
            {
                if (running == 0) throw new InvalidDataException("Running status without a previous status byte");
                r.Position--;
                status = running;
            }

            if (status == 0xFF)
            {
                int type = r.Byte();
                int len = (int)r.VarInt();
                int start = r.Position;
                switch (type)
                {
                    case 0x03: track.Name = System.Text.Encoding.ASCII.GetString(r.Bytes(len)); break;
                    case 0x51 when len == 3: track.Events.Add(new MidiEvent(tick, MidiEventKind.Tempo, A: (r.Byte() << 16) | (r.Byte() << 8) | r.Byte())); break;
                    case 0x58 when len >= 2: track.Events.Add(new MidiEvent(tick, MidiEventKind.TimeSignature, A: r.Byte(), B: r.Byte())); break;
                }
                r.Position = start + len;
                if (type == 0x2F) break;
                continue;
            }
            if (status is 0xF0 or 0xF7)
            {
                r.Skip((int)r.VarInt());
                continue;
            }

            running = status;
            int ch = status & 0x0F;
            switch (status & 0xF0)
            {
                case 0x80: track.Events.Add(new MidiEvent(tick, MidiEventKind.NoteOff, ch, r.Byte(), r.Byte())); break;
                case 0x90:
                {
                    int key = r.Byte(), vel = r.Byte();
                    track.Events.Add(new MidiEvent(tick, vel == 0 ? MidiEventKind.NoteOff : MidiEventKind.NoteOn, ch, key, vel));
                    break;
                }
                case 0xA0: r.Skip(2); break;
                case 0xB0: track.Events.Add(new MidiEvent(tick, MidiEventKind.ControlChange, ch, r.Byte(), r.Byte())); break;
                case 0xC0: track.Events.Add(new MidiEvent(tick, MidiEventKind.ProgramChange, ch, r.Byte())); break;
                case 0xD0: r.Skip(1); break;
                case 0xE0:
                {
                    int lo = r.Byte(), hi = r.Byte();
                    track.Events.Add(new MidiEvent(tick, MidiEventKind.PitchBend, ch, ((hi << 7) | lo) - 8192));
                    break;
                }
                default: throw new InvalidDataException($"Unexpected status byte 0x{status:X2}");
            }
        }
        r.Position = end;
        return track;
    }

    public byte[] Write()
    {
        using var ms = new MemoryStream();
        void Tag(string s) => ms.Write(System.Text.Encoding.ASCII.GetBytes(s));
        void I32(int v) { ms.WriteByte((byte)(v >> 24)); ms.WriteByte((byte)(v >> 16)); ms.WriteByte((byte)(v >> 8)); ms.WriteByte((byte)v); }
        void I16(int v) { ms.WriteByte((byte)(v >> 8)); ms.WriteByte((byte)v); }

        Tag("MThd"); I32(6); I16(1); I16(Tracks.Count); I16(TicksPerQuarter);
        foreach (MidiTrack track in Tracks)
        {
            byte[] body = WriteTrack(track);
            Tag("MTrk"); I32(body.Length); ms.Write(body);
        }
        return ms.ToArray();
    }

    private static byte[] WriteTrack(MidiTrack track)
    {
        var ms = new MemoryStream();
        void Var(long v)
        {
            Span<byte> buf = stackalloc byte[5];
            int n = 0;
            buf[n++] = (byte)(v & 0x7F);
            while ((v >>= 7) > 0) buf[n++] = (byte)((v & 0x7F) | 0x80);
            for (int i = n - 1; i >= 0; i--) ms.WriteByte(buf[i]);
        }
        void Bytes(params int[] b) { foreach (int x in b) ms.WriteByte((byte)x); }

        long last = 0;
        if (track.Name.Length > 0)
        {
            byte[] name = System.Text.Encoding.ASCII.GetBytes(track.Name);
            Var(0); Bytes(0xFF, 0x03); Var(name.Length); ms.Write(name);
        }
        // Stable sort keeps NoteOff-before-NoteOn order at equal ticks as authored.
        foreach (MidiEvent e in track.Events.OrderBy(e => e.Tick))
        {
            Var(e.Tick - last);
            last = e.Tick;
            switch (e.Kind)
            {
                case MidiEventKind.NoteOn: Bytes(0x90 | e.Channel, e.A, e.B); break;
                case MidiEventKind.NoteOff: Bytes(0x80 | e.Channel, e.A, e.B); break;
                case MidiEventKind.ControlChange: Bytes(0xB0 | e.Channel, e.A, e.B); break;
                case MidiEventKind.ProgramChange: Bytes(0xC0 | e.Channel, e.A); break;
                case MidiEventKind.PitchBend: { int v = e.A + 8192; Bytes(0xE0 | e.Channel, v & 0x7F, (v >> 7) & 0x7F); break; }
                case MidiEventKind.Tempo: Bytes(0xFF, 0x51, 0x03, (e.A >> 16) & 0xFF, (e.A >> 8) & 0xFF, e.A & 0xFF); break;
                case MidiEventKind.TimeSignature: Bytes(0xFF, 0x58, 0x04, e.A, e.B, 24, 8); break;
            }
        }
        Var(0); Bytes(0xFF, 0x2F, 0x00);
        return ms.ToArray();
    }

    private sealed class Reader(byte[] data)
    {
        public int Position;
        public bool End => Position >= data.Length;
        public int Byte() => Position < data.Length ? data[Position++] : throw new InvalidDataException("Unexpected end of MIDI data");
        public int Int16() => (Byte() << 8) | Byte();
        public int Int32() => (Byte() << 24) | (Byte() << 16) | (Byte() << 8) | Byte();
        public string Tag() => System.Text.Encoding.ASCII.GetString(Bytes(4));
        public void Skip(int n) => Position += n;
        public byte[] Bytes(int n)
        {
            if (Position + n > data.Length) throw new InvalidDataException("Unexpected end of MIDI data");
            byte[] b = data.AsSpan(Position, n).ToArray();
            Position += n;
            return b;
        }
        public long VarInt()
        {
            long v = 0;
            for (int i = 0; i < 4; i++)
            {
                int b = Byte();
                v = (v << 7) | (uint)(b & 0x7F);
                if ((b & 0x80) == 0) return v;
            }
            throw new InvalidDataException("Variable-length quantity too long");
        }
    }
}
