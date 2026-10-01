using Godot;
using XWing.Audio.Midi;
using XWing.Audio.Music;
using XWing.Audio.Sfx;
using XWing.Audio.Synthesis;
using XWing.Sim.Core;

namespace XWing.Client;

/// <summary>
/// Turns simulation events into sound and drives the adaptive score. All audio content is
/// generated in code (XWing.Audio), so the game has sound with zero asset files.
///
/// Player overrides (QoL / modding), looked up in Godot's user data folder:
///   user://music/{cruise,combat,victory,failure}.mid  replace a cue with any General MIDI file
///   user://soundfonts/*.sf2                           play music through a SoundFont instead of FM
/// </summary>
public partial class AudioHost : Node
{
    private const int MusicRate = 32000;

    public SimHost Host { get; set; } = null!;

    private readonly Dictionary<SfxId, AudioStreamWav> _bank = new();
    private readonly List<AudioStreamPlayer> _flat = new();
    private readonly List<AudioStreamPlayer3D> _spatial = new();
    private AudioStreamPlayer _engine = null!;
    private AudioStreamPlayer _music = null!;
    private AudioStreamGeneratorPlayback? _musicPlayback;
    private MusicDirector? _director;
    private MoodTracker _mood = new();
    private float[] _musicL = new float[4096], _musicR = new float[4096];
    private Vector2[] _musicFrames = new Vector2[4096];

    private int? _lastTarget;
    private HyperspaceState _lastHyperspace;

    public bool MusicMuted { get => _director?.Muted ?? false; set { if (_director is not null) _director.Muted = value; } }
    public string MusicSource { get; private set; } = "";

    public override void _Ready()
    {
        foreach (SfxId id in Enum.GetValues<SfxId>())
        {
            float[] samples = SfxSynth.Generate(id);
            var wav = new AudioStreamWav
            {
                Format = AudioStreamWav.FormatEnum.Format16Bits,
                MixRate = SfxSynth.SampleRate,
                Stereo = false,
                Data = SfxSynth.ToPcm16(samples),
            };
            if (id == SfxId.EngineLoop)
            {
                wav.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
                wav.LoopBegin = 0;
                wav.LoopEnd = samples.Length;
            }
            _bank[id] = wav;
        }

        for (int i = 0; i < 10; i++) { var p = new AudioStreamPlayer(); AddChild(p); _flat.Add(p); }
        for (int i = 0; i < 24; i++)
        {
            var p = new AudioStreamPlayer3D { UnitSize = 120f, MaxDistance = 7000f, AttenuationFilterCutoffHz = 9000f };
            AddChild(p);
            _spatial.Add(p);
        }

        _engine = new AudioStreamPlayer { Stream = _bank[SfxId.EngineLoop], VolumeDb = -14f };
        AddChild(_engine);

        _music = new AudioStreamPlayer { Stream = new AudioStreamGenerator { MixRate = MusicRate, BufferLength = 0.2f }, VolumeDb = -4f };
        AddChild(_music);
        _music.Play();
        _musicPlayback = _music.GetStreamPlayback() as AudioStreamGeneratorPlayback;
    }

    public override void _ExitTree()
    {
        // C# wrappers hold references until GC, which runs after Godot's leak check. Release
        // generated streams and playbacks explicitly so shutdown is clean.
        foreach (AudioStreamPlayer p in _flat.Append(_engine).Append(_music)) { p.Stop(); p.Stream = null; }
        foreach (AudioStreamPlayer3D p in _spatial) { p.Stop(); p.Stream = null; }
        _musicPlayback?.Dispose();
        _musicPlayback = null;
        foreach (AudioStreamWav wav in _bank.Values) wav.Dispose();
        _bank.Clear();
    }

    /// <summary>Called when a mission (re)starts.</summary>
    public void Reset()
    {
        bool muted = MusicMuted;
        _director = new MusicDirector(CreateSynth(), LoadScore()) { Muted = muted };
        _mood = new MoodTracker();
        _lastTarget = null;
        _lastHyperspace = HyperspaceState.None;
    }

    private ISynth CreateSynth()
    {
        string dir = ProjectSettings.GlobalizePath("user://soundfonts");
        string? sf2 = DirAccess.DirExistsAbsolute(dir)
            ? Directory.EnumerateFiles(dir, "*.sf2").OrderBy(f => f).FirstOrDefault()
            : null;
        if (sf2 is not null)
        {
            try
            {
                MusicSource = $"SoundFont: {Path.GetFileName(sf2)}";
                return new SoundFontSynth(sf2, MusicRate);
            }
            catch (Exception e)
            {
                GD.PushWarning($"Could not load SoundFont {sf2}: {e.Message}. Falling back to FM.");
            }
        }
        MusicSource = "FM synth";
        return new FmSynth(MusicRate);
    }

    private static IReadOnlyDictionary<MusicCue, CueTrack> LoadScore()
    {
        var score = Score.Default().ToDictionary(kv => kv.Key, kv => kv.Value);
        string dir = ProjectSettings.GlobalizePath("user://music");
        foreach (MusicCue cue in score.Keys.ToList())
        {
            string path = Path.Combine(dir, $"{cue.ToString().ToLowerInvariant()}.mid");
            if (!File.Exists(path)) continue;
            try { score[cue] = score[cue] with { File = MidiFile.Read(File.ReadAllBytes(path)) }; }
            catch (Exception e) { GD.PushWarning($"Ignoring {path}: {e.Message}"); }
        }
        return score;
    }

    public override void _Process(double delta)
    {
        if (Host.World is not { } world || _director is null) return;

        UpdateCockpitSounds(world);
        _director.Request(_mood.Evaluate(world));
        FeedMusic();
    }

    private void FeedMusic()
    {
        if (_musicPlayback is null || _director is null) return;
        int frames = _musicPlayback.GetFramesAvailable();
        while (frames > 0)
        {
            int n = Math.Min(frames, _musicL.Length);
            _director.Render(_musicL.AsSpan(0, n), _musicR.AsSpan(0, n));
            if (_musicFrames.Length != n) _musicFrames = new Vector2[n];
            for (int i = 0; i < n; i++) _musicFrames[i] = new Vector2(_musicL[i], _musicR[i]);
            _musicPlayback.PushBuffer(_musicFrames);
            frames -= n;
        }
    }

    private void UpdateCockpitSounds(World world)
    {
        if (world.Player is not { } p) return;

        bool flying = p.Alive && !Host.Paused;
        if (flying && !_engine.Playing) _engine.Play();
        if (!flying && _engine.Playing) _engine.Stop();
        if (flying)
        {
            float f = p.Class.MaxSpeed > 0 ? p.Speed / p.Class.MaxSpeed : 0f;
            _engine.PitchScale = 0.6f + 0.7f * f;
            _engine.VolumeDb = Mathf.LinearToDb(0.25f + 0.35f * f) - 6f;
        }

        if (p.TargetId != _lastTarget && p.TargetId is not null) Play(SfxId.TargetBeep, -10f);
        _lastTarget = p.TargetId;

        if (p.Hyperspace == HyperspaceState.Charging && _lastHyperspace == HyperspaceState.None) Play(SfxId.Hyperspace, -2f);
        _lastHyperspace = p.Hyperspace;
    }

    /// <summary>Called by SimHost for each event of each tick.</summary>
    public void OnEvent(World world, SimEvent e)
    {
        int? playerId = world.Player?.Id;
        switch (e)
        {
            case LaserFired f when world.FindShip(f.ShipId) is { } s:
                SfxId laser = s.Iff == Iff.Imperial ? SfxId.ImperialLaser : SfxId.RebelLaser;
                if (s.Id == playerId) Play(laser, -8f);
                else PlayAt(laser, s.Position.ToGodot(), -4f);
                break;
            case TurretFired t:
                PlayAt(SfxId.Turbolaser, t.Muzzle.ToGodot(), 0f);
                break;
            case ShipHit h when h.ShipId == playerId:
                if (h.ShieldDamage > 0) Play(SfxId.ShieldHit, -6f);
                if (h.HullDamage > 0) Play(SfxId.HullHit, -3f);
                break;
            case ShipHit h when world.FindShip(h.ShipId) is { } s:
                PlayAt(h.HullDamage > 0 ? SfxId.HullHit : SfxId.ShieldHit, s.Position.ToGodot(), -8f);
                break;
            case ShipDestroyed d when world.FindShip(d.ShipId) is { } s:
                SfxId boom = s.Class.Category == XWing.Sim.Data.ShipCategory.Capital ? SfxId.BigExplosion : SfxId.Explosion;
                if (s.Id == playerId) Play(boom, 0f);
                else PlayAt(boom, s.Position.ToGodot(), 4f);
                break;
            case LaserDry d when d.ShipId == playerId:
                Play(SfxId.LaserDry, -6f);
                break;
            case RadioMessage:
                Play(SfxId.RadioChirp, -12f);
                break;
        }
    }

    private void Play(SfxId id, float volumeDb)
    {
        AudioStreamPlayer p = _flat.FirstOrDefault(x => !x.Playing) ?? _flat[0];
        p.Stream = _bank[id];
        p.VolumeDb = volumeDb;
        p.Play();
    }

    private void PlayAt(SfxId id, Vector3 position, float volumeDb)
    {
        // Prefer a free voice; otherwise steal the one farthest from the listener.
        AudioStreamPlayer3D p = _spatial.FirstOrDefault(x => !x.Playing)
            ?? _spatial.MaxBy(x => x.GlobalPosition.DistanceSquaredTo(Host.Camera.GlobalPosition))!;
        p.Stream = _bank[id];
        p.VolumeDb = volumeDb;
        p.GlobalPosition = position;
        p.Play();
    }
}
