using Godot;
using XWing.Sim.Combat;
using XWing.Sim.Core;
using XWing.Sim.Data;
using XWing.Sim.Missions;
using N = System.Numerics;

namespace XWing.Client;

/// <summary>
/// Owns the simulation and drives it at its own fixed tick rate, independent of the render
/// framerate. Rendering interpolates between the last two ticks, so the game runs at any
/// refresh rate while gameplay stays identical to the headless runner.
/// </summary>
public partial class SimHost : Node3D
{
    private readonly Dictionary<int, Node3D> _views = new();
    private readonly Dictionary<int, (N.Vector3 Pos, N.Quaternion Rot)> _previous = new();
    private readonly Dictionary<Projectile, MeshInstance3D> _bolts = new();
    private readonly Stack<MeshInstance3D> _boltPool = new();

    private ShipCatalog _catalog = null!;
    private MissionDefinition _mission = null!;
    private Camera3D _camera = null!;
    private Starfield _stars = null!;
    private Hud _hud = null!;
    private AudioHost _audio = null!;
    private Node3D _worldRoot = null!;
    private double _accumulator;
    private Material _rebelBolt = null!, _imperialBolt = null!, _heavyBolt = null!;
    private readonly Queue<(float At, string Path)> _screenshots = new();

    public World World { get; private set; } = null!;
    public Camera3D Camera => _camera;
    public float Throttle { get; private set; } = 2f / 3f;
    public bool Paused { get; private set; }
    public bool ExternalView { get; private set; }
    public Ship? Player => World.Player;

    public override void _Ready()
    {
        Controls.Register();
        _catalog = ShipCatalog.LoadBuiltIn();
        string[] userArgs = OS.GetCmdlineUserArgs();
        int missionArg = Array.IndexOf(userArgs, "--mission");
        _mission = missionArg >= 0 && missionArg + 1 < userArgs.Length
            ? MissionDefinition.FromJson(File.ReadAllText(userArgs[missionArg + 1]))
            : MissionDefinition.LoadBuiltIn("vertical_slice");

        AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = Colors.Black,
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.35f, 0.38f, 0.45f),
                AmbientLightEnergy = 0.35f,
                GlowEnabled = true,
                GlowIntensity = 0.9f,
                GlowBloom = 0.05f,
                TonemapMode = Godot.Environment.ToneMapper.Filmic,
            },
        });
        var sun = new DirectionalLight3D { LightEnergy = 1.4f, LightColor = new Color(1f, 0.96f, 0.9f) };
        AddChild(sun);
        sun.LookAt(new Vector3(-0.4f, -0.5f, -1f), Vector3.Up);

        _camera = new Camera3D { Fov = 70f, Near = 0.3f, Far = 30000f, Current = true };
        AddChild(_camera);
        _stars = new Starfield();
        AddChild(_stars);

        _rebelBolt = BoltMaterial(new Color(1f, 0.15f, 0.1f));
        _imperialBolt = BoltMaterial(new Color(0.2f, 1f, 0.2f));
        _heavyBolt = BoltMaterial(new Color(1f, 0.3f, 0.6f));

        var layer = new CanvasLayer();
        AddChild(layer);
        _hud = new Hud { Host = this };
        layer.AddChild(_hud);
        _audio = new AudioHost { Host = this };
        AddChild(_audio);

        // Dev aid: `-- --shot 12:/tmp/a.png --shot 30:/tmp/b.png` saves frames at those mission times
        // and quits after the last one. Used to check rendering in CI / headless sessions.
        string[] user = OS.GetCmdlineUserArgs();
        for (int i = 0; i + 1 < user.Length; i++)
            if (user[i] == "--shot" && user[i + 1].Split(':', 2) is [var at, var path])
                _screenshots.Enqueue((float.Parse(at, System.Globalization.CultureInfo.InvariantCulture), path));

        StartMission();
    }

    private static Material BoltMaterial(Color c) => new StandardMaterial3D
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        AlbedoColor = c,
        EmissionEnabled = true,
        Emission = c,
        EmissionEnergyMultiplier = 8f,
    };

    private void StartMission()
    {
        _worldRoot?.QueueFree();
        _worldRoot = new Node3D { Name = "WorldRoot" };
        AddChild(_worldRoot);
        _views.Clear();
        _previous.Clear();
        _bolts.Clear();
        _boltPool.Clear();
        _accumulator = 0;

        // `godot -- --autopilot` lets the AI fly the player: attract mode, and a smoke test of the
        // full client without input.
        bool autopilot = OS.GetCmdlineUserArgs().Contains("--autopilot");
        World = World.ForMission(_mission, _catalog, seed: (ulong)System.Environment.TickCount64, autopilotPlayer: autopilot);
        Throttle = Player?.Controls.Throttle ?? 2f / 3f;
        _hud.Reset();
        _audio.Reset();
        foreach (SimEvent e in World.EventLog) HandleEvent(e);
    }

    public override void _Process(double delta)
    {
        if (Input.IsActionJustPressed(Controls.Quit)) { GetTree().Quit(); return; }
        if (Input.IsActionJustPressed(Controls.Restart)) StartMission();
        if (Input.IsActionJustPressed(Controls.Pause)) Paused = !Paused;
        if (Input.IsActionJustPressed(Controls.ToggleView)) ExternalView = !ExternalView;
        if (Input.IsActionJustPressed(Controls.ToggleMusic))
        {
            _audio.MusicMuted = !_audio.MusicMuted;
            _hud.AddMessage(_audio.MusicMuted ? "Music off" : $"Music on ({_audio.MusicSource})");
        }

        float dt = World.Rules.Dt;
        if (!Paused)
        {
            ReadDiscreteInput();
            _accumulator = Math.Min(_accumulator + delta, 0.25); // avoid spiral of death after a hitch
            while (_accumulator >= dt)
            {
                ReadContinuousInput();
                CapturePrevious();
                World.Step();
                foreach (SimEvent e in World.TickEvents) HandleEvent(e);
                _accumulator -= dt;
            }
        }

        float alpha = (float)(_accumulator / dt);
        UpdateShipViews(alpha);
        UpdateBolts(alpha * dt);
        UpdateCamera(alpha);
        _stars.GlobalPosition = _camera.GlobalPosition;
        _hud.QueueRedraw();

        if (_screenshots.TryPeek(out var shot) && World.Time >= shot.At)
        {
            _screenshots.Dequeue();
            void Save()
            {
                RenderingServer.FramePostDraw -= Save;
                GetViewport().GetTexture().GetImage().SavePng(shot.Path);
            }
            RenderingServer.FramePostDraw += Save;
            if (_screenshots.Count == 0) GetTree().CreateTimer(0.5).Timeout += () => GetTree().Quit();
        }
    }

    private void ReadDiscreteInput()
    {
        if (Player is not { Alive: true } p) return;

        if (Input.IsActionJustPressed(Controls.Throttle0)) Throttle = 0f;
        if (Input.IsActionJustPressed(Controls.Throttle33)) Throttle = 1f / 3f;
        if (Input.IsActionJustPressed(Controls.Throttle67)) Throttle = 2f / 3f;
        if (Input.IsActionJustPressed(Controls.Throttle100)) Throttle = 1f;
        if (Input.IsActionJustPressed(Controls.ThrottleUp)) Throttle = Mathf.Min(1f, Throttle + 0.1f);
        if (Input.IsActionJustPressed(Controls.ThrottleDown)) Throttle = Mathf.Max(0f, Throttle - 0.1f);

        void Cmd(string action, ShipCommand c) { if (Input.IsActionJustPressed(action)) p.Command(c); }
        Cmd(Controls.FireMode, ShipCommand.CycleFireMode);
        Cmd(Controls.TargetNext, ShipCommand.TargetNext);
        Cmd(Controls.TargetPrev, ShipCommand.TargetPrevious);
        Cmd(Controls.TargetNearest, ShipCommand.TargetNearestEnemy);
        Cmd(Controls.LaserRecharge, ShipCommand.CycleLaserRecharge);
        Cmd(Controls.ShieldRecharge, ShipCommand.CycleShieldRecharge);
        Cmd(Controls.ShieldFocus, ShipCommand.CycleShieldFocus);
        Cmd(Controls.LasersToShields, ShipCommand.TransferLasersToShields);
        Cmd(Controls.ShieldsToLasers, ShipCommand.TransferShieldsToLasers);
        Cmd(Controls.Hyperspace, ShipCommand.Hyperspace);
    }

    private void ReadContinuousInput()
    {
        if (Player is not { Alive: true } p || p.Pilot is not null) return;
        p.Controls = new ShipControls
        {
            Pitch = Input.GetAxis(Controls.PitchDown, Controls.PitchUp),
            Yaw = Input.GetAxis(Controls.YawLeft, Controls.YawRight),
            Roll = Input.GetAxis(Controls.RollLeft, Controls.RollRight),
            Throttle = Throttle,
            Fire = Input.IsActionPressed(Controls.Fire),
        };
    }

    private void CapturePrevious()
    {
        foreach (Ship s in World.Ships) _previous[s.Id] = (s.Position, s.Orientation);
    }

    private (Vector3 Pos, Quaternion Rot) Interpolated(Ship s, float alpha)
    {
        if (!_previous.TryGetValue(s.Id, out var prev)) return (s.Position.ToGodot(), s.Orientation.ToGodot());
        N.Vector3 pos = N.Vector3.Lerp(prev.Pos, s.Position, alpha);
        N.Quaternion rot = N.Quaternion.Slerp(prev.Rot, s.Orientation, alpha);
        return (pos.ToGodot(), rot.ToGodot());
    }

    private void UpdateShipViews(float alpha)
    {
        foreach (Ship s in World.Ships)
        {
            if (!_views.TryGetValue(s.Id, out Node3D? view))
            {
                view = ShipModels.Build(s.Class);
                _worldRoot.AddChild(view);
                _views[s.Id] = view;
            }
            bool hideOwnShip = s.IsPlayer && !ExternalView && s.Alive;
            view.Visible = s.Alive && !hideOwnShip;
            if (!view.Visible) continue;
            var (pos, rot) = Interpolated(s, alpha);
            view.GlobalTransform = new Transform3D(new Basis(rot), pos);
        }
    }

    private void UpdateBolts(float extrapolate)
    {
        var live = new HashSet<Projectile>(World.Projectiles);
        foreach (var (p, mesh) in _bolts.Where(kv => !live.Contains(kv.Key)).ToList())
        {
            mesh.Visible = false;
            _boltPool.Push(mesh);
            _bolts.Remove(p);
        }

        foreach (Projectile p in World.Projectiles)
        {
            if (!_bolts.TryGetValue(p, out MeshInstance3D? mesh))
            {
                mesh = _boltPool.Count > 0 ? _boltPool.Pop() : NewBoltMesh();
                mesh.MaterialOverride = p.Heavy ? _heavyBolt : p.OwnerIff == Iff.Imperial ? _imperialBolt : _rebelBolt;
                mesh.Scale = p.Heavy ? new Vector3(2f, 2f, 1.5f) : Vector3.One;
                mesh.Visible = true;
                _bolts[p] = mesh;
            }
            Vector3 vel = p.Velocity.ToGodot();
            Vector3 dir = vel.Normalized();
            Vector3 up = Mathf.Abs(dir.Y) > 0.99f ? Vector3.Right : Vector3.Up;
            mesh.GlobalTransform = new Transform3D(Basis.LookingAt(dir, up).Scaled(mesh.Scale), (p.Position + p.Velocity * extrapolate).ToGodot());
        }
    }

    private MeshInstance3D NewBoltMesh()
    {
        var m = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.22f, 0.22f, 9f) }, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        _worldRoot.AddChild(m);
        return m;
    }

    private void UpdateCamera(float alpha)
    {
        if (Player is not { } p) return;
        var (pos, rot) = Interpolated(p, alpha);
        var basis = new Basis(rot);
        if (ExternalView || !p.Alive)
        {
            Vector3 eye = pos + basis.Z * 40f + basis.Y * 9f; // local +Z is backward: chase from behind and above
            _camera.GlobalTransform = new Transform3D(basis, eye);
        }
        else
        {
            _camera.GlobalTransform = new Transform3D(basis, pos + basis.Y * 0.9f);
        }
    }

    private void HandleEvent(SimEvent e)
    {
        _audio.OnEvent(World, e);
        switch (e)
        {
            case ShipDestroyed d when World.FindShip(d.ShipId) is { } s:
                _worldRoot.AddChild(Explosion.Create(s.Position.ToGodot(), s.Class.CollisionRadius * 3f));
                _hud.AddMessage($"{d.Callsign} destroyed");
                break;
            case HyperspaceEntered h:
                _hud.AddMessage($"{h.Callsign} jumped to hyperspace");
                if (World.FindShip(h.ShipId) is { IsPlayer: true }) _hud.Flash();
                break;
            case RadioMessage r:
                _hud.AddMessage(r.Text);
                break;
            case MissionEnded m:
                GD.Print($"[{m.Time:0.0}s] Mission {m.Result}: {m.Reason}");
                break;
            case GoalStateChanged g when g.State != GoalState.Pending:
                _hud.AddMessage($"Goal {g.State.ToString().ToLowerInvariant()}: {g.Description}");
                break;
            case LaserDry d when d.ShipId == Player?.Id:
                _hud.LaserDry();
                break;
        }
    }
}
