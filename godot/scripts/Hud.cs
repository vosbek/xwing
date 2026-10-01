using Godot;
using XWing.Sim.Combat;
using XWing.Sim.Core;
using XWing.Sim.Missions;
using XWing.Sim.Sensors;

namespace XWing.Client;

/// <summary>
/// Vector-drawn cockpit instruments laid out like the original's dashboard: front sensor on the
/// left, rear sensor on the right, targeting computer (CMD) in the middle, energy gauges between.
/// Everything scales with the viewport height, so it works at any resolution or aspect ratio.
/// </summary>
public partial class Hud : Control
{
    public SimHost Host { get; set; } = null!;

    private readonly List<(string Text, double At)> _messages = new();
    private double _flashAt = double.NegativeInfinity;
    private double _dryAt = double.NegativeInfinity;

    private static readonly Color Green = new(0.35f, 1f, 0.45f);
    private static readonly Color Dim = new(0.2f, 0.55f, 0.25f);
    private static readonly Color Red = new(1f, 0.3f, 0.25f);
    private static readonly Color Blue = new(0.4f, 0.7f, 1f);
    private static readonly Color Yellow = new(1f, 0.9f, 0.3f);
    private static readonly Color Panel = new(0.02f, 0.04f, 0.03f, 0.78f);

    private Font _font = null!;
    private float _s = 1f;

    /// <summary>Wall clock, for purely cosmetic blinking.</summary>
    private static double Now => Time.GetTicksMsec() / 1000.0;
    /// <summary>Mission clock: messages and effects freeze while paused and match the sim.</summary>
    private double SimNow => Host?.World?.Time ?? 0;

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        _font = ThemeDB.FallbackFont;
    }

    public void Reset() { _messages.Clear(); _flashAt = double.NegativeInfinity; }
    public void AddMessage(string text) { _messages.Add((text, SimNow)); if (_messages.Count > 20) _messages.RemoveAt(0); }
    public void Flash() => _flashAt = SimNow;
    public void LaserDry() => _dryAt = SimNow;

    private void Text(Vector2 pos, string text, Color color, int size = 15, HorizontalAlignment align = HorizontalAlignment.Left, float width = -1)
        => DrawString(_font, pos, text, align, width, Math.Max(1, (int)(size * _s)), color);

    private static Color IffColor(Iff iff) => iff switch { Iff.Rebel => Green, Iff.Imperial => Red, _ => Blue };

    public override void _Draw()
    {
        if (Host?.World is not { } world) return;
        Vector2 size = GetViewportRect().Size;
        if (size.Y < 100f) return; // minimized window / headless: nothing sensible to draw
        _s = size.Y / 900f;
        Ship? player = world.Player;

        if (player is { Alive: true } && !Host.ExternalView) DrawReticle(world, player, size);
        if (player is not null) DrawDashboard(world, player, size);
        DrawMessages(size);
        DrawGoals(world, size);
        DrawOverlays(world, player, size);
    }

    private void DrawReticle(World world, Ship player, Vector2 size)
    {
        Vector2 c = size / 2f;
        float r = 14f * _s;
        DrawLine(c + new Vector2(-r * 2, 0), c + new Vector2(-r * 0.6f, 0), Green, 2f);
        DrawLine(c + new Vector2(r * 0.6f, 0), c + new Vector2(r * 2, 0), Green, 2f);
        DrawLine(c + new Vector2(0, -r * 2), c + new Vector2(0, -r * 0.6f), Green, 2f);
        DrawLine(c + new Vector2(0, r * 0.6f), c + new Vector2(0, r * 2), Green, 2f);

        if (world.FindShip(player.TargetId) is not { Alive: true } t) return;
        Vector3 tp = new(t.Position.X, t.Position.Y, t.Position.Z);
        if (Host.Camera.IsPositionBehind(tp)) return;
        Vector2 p = Host.Camera.UnprojectPosition(tp);
        float dist = (tp - Host.Camera.GlobalPosition).Length();
        float b = Mathf.Clamp(t.Class.CollisionRadius * 900f / Mathf.Max(dist, 1f), 12f, 120f) * _s;
        Color col = IffColor(t.Iff);
        float k = b * 0.4f;
        foreach (var (sx, sy) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
        {
            var corner = p + new Vector2(sx * b, sy * b);
            DrawLine(corner, corner - new Vector2(sx * k, 0), col, 2f);
            DrawLine(corner, corner - new Vector2(0, sy * k), col, 2f);
        }
    }

    private void DrawDashboard(World world, Ship player, Vector2 size)
    {
        float h = 190f * _s;
        float top = size.Y - h;
        DrawRect(new Rect2(0, top, size.X, h), Panel);
        DrawLine(new Vector2(0, top), new Vector2(size.X, top), Dim, 2f);

        float scopeR = 78f * _s;
        var front = new Vector2(scopeR + 24f * _s, top + h / 2f);
        var rear = new Vector2(size.X - scopeR - 24f * _s, top + h / 2f);
        List<RadarBlip> blips = player.Alive ? Radar.Scan(world, player) : new();
        DrawScope(front, scopeR, "FORE", blips.Where(b => b.Scope == RadarScope.Front));
        DrawScope(rear, scopeR, "AFT", blips.Where(b => b.Scope == RadarScope.Rear));

        DrawCmd(world, player, new Rect2(size.X / 2f - 190f * _s, top + 14f * _s, 380f * _s, h - 28f * _s));
        DrawEnergy(world, player, new Vector2(front.X + scopeR + 30f * _s, top + 18f * _s), h - 36f * _s);
        DrawShields(player, new Vector2(rear.X - scopeR - 110f * _s, top + h / 2f));
    }

    private void DrawScope(Vector2 c, float r, string label, IEnumerable<RadarBlip> blips)
    {
        DrawCircle(c, r, new Color(0f, 0.08f, 0.03f, 0.9f));
        DrawArc(c, r, 0, Mathf.Tau, 48, Dim, 2f);
        DrawArc(c, r * 0.5f, 0, Mathf.Tau, 32, new Color(Dim, 0.5f), 1f);
        DrawLine(c - new Vector2(r, 0), c + new Vector2(r, 0), new Color(Dim, 0.4f), 1f);
        DrawLine(c - new Vector2(0, r), c + new Vector2(0, r), new Color(Dim, 0.4f), 1f);
        Text(c + new Vector2(-r, -r - 4f * _s), label, Dim, 12);

        bool blink = (int)(Now * 4) % 2 == 0;
        foreach (RadarBlip b in blips)
        {
            Vector2 p = c + new Vector2(b.X, -b.Y) * r;
            Color col = b.IsTarget ? (blink ? Yellow : IffColor(b.Iff)) : IffColor(b.Iff);
            float sz = (b.IsCapital ? 5f : 3f) * _s;
            if (b.IsCapital) DrawRect(new Rect2(p - new Vector2(sz, sz), new Vector2(sz * 2, sz * 2)), col);
            else DrawCircle(p, sz, col);
        }
    }

    private void DrawCmd(World world, Ship player, Rect2 r)
    {
        DrawRect(r, new Color(0f, 0.06f, 0.02f, 0.9f));
        DrawRect(r, Dim, false, 2f);
        float x = r.Position.X + 14f * _s, y = r.Position.Y + 26f * _s, line = 22f * _s;

        if (Targeting.Describe(world, player) is not { } t)
        {
            Text(new Vector2(x, y), "CMD   NO TARGET", Dim, 17);
            Text(new Vector2(x, y + line * 1.5f), "T/Y cycle   R nearest enemy", Dim, 13);
            return;
        }
        Color col = IffColor(t.Iff);
        Text(new Vector2(x, y), t.Callsign.ToUpperInvariant(), col, 19);
        Text(new Vector2(x + 170f * _s, y), t.ClassName.ToUpperInvariant(), col, 15);
        Text(new Vector2(x, y + line), $"DIST {t.Distance / 1000f:0.00} km   SPD {t.Speed:0}", Green, 15);
        Bar(new Vector2(x, y + line * 1.6f), 300f * _s, "SHD", t.ShieldFraction, Blue);
        Bar(new Vector2(x, y + line * 2.5f), 300f * _s, "HULL", t.HullFraction, t.HullFraction > 0.3f ? Green : Red);

        // Small attitude indicator: where the target is relative to the nose.
        var c = new Vector2(r.End.X - 40f * _s, r.Position.Y + 40f * _s);
        DrawArc(c, 26f * _s, 0, Mathf.Tau, 24, Dim, 1.5f);
        Vector2 d = new Vector2(t.LocalDirection.X, -t.LocalDirection.Y);
        if (d.Length() > 1e-3f) d = d.Normalized() * Mathf.Clamp(Mathf.Acos(Mathf.Clamp(-t.LocalDirection.Z, -1, 1)) / Mathf.Pi, 0, 1);
        DrawCircle(c + d * 26f * _s, 3.5f * _s, t.LocalDirection.Z > 0 ? Red : Yellow);
    }

    private void Bar(Vector2 pos, float width, string label, float fraction, Color color)
    {
        Text(pos + new Vector2(0, 12f * _s), label, Dim, 12);
        var bar = new Rect2(pos + new Vector2(44f * _s, 0), new Vector2(width - 44f * _s, 12f * _s));
        DrawRect(bar, new Color(Dim, 0.3f));
        DrawRect(new Rect2(bar.Position, new Vector2(bar.Size.X * Mathf.Clamp(fraction, 0, 1), bar.Size.Y)), color);
    }

    private void DrawEnergy(World world, Ship p, Vector2 origin, float height)
    {
        // Speed bar with throttle marker.
        float bw = 16f * _s;
        var speedRect = new Rect2(origin, new Vector2(bw, height - 20f * _s));
        DrawRect(speedRect, new Color(Dim, 0.3f));
        float max = p.Class.MaxSpeed * world.Rules.EngineFactorMax;
        float sf = p.Speed / max;
        DrawRect(new Rect2(speedRect.Position + new Vector2(0, speedRect.Size.Y * (1 - sf)), new Vector2(bw, speedRect.Size.Y * sf)), Green);
        float ty = speedRect.Position.Y + speedRect.Size.Y * (1 - p.Controls.Throttle * p.Class.MaxSpeed * EnergySystem.EngineFactor(p, world.Rules) / max);
        DrawLine(new Vector2(origin.X - 5f * _s, ty), new Vector2(origin.X + bw + 5f * _s, ty), Yellow, 2f);
        Text(new Vector2(origin.X - 4f * _s, origin.Y + height), $"{p.Speed:0}", Green, 13);

        // Laser charge, one bar per cannon.
        float lx = origin.X + bw + 22f * _s;
        bool dry = SimNow - _dryAt < 0.4;
        for (int i = 0; i < p.LaserCharge.Length; i++)
        {
            var rr = new Rect2(new Vector2(lx + i * 13f * _s, origin.Y), new Vector2(9f * _s, height - 20f * _s));
            DrawRect(rr, new Color(Dim, 0.3f));
            float f = p.LaserCharge[i];
            Color col = f < p.Class.LaserShotCost || dry ? Red : f > 0.99f ? Green : Yellow;
            DrawRect(new Rect2(rr.Position + new Vector2(0, rr.Size.Y * (1 - f)), new Vector2(rr.Size.X, rr.Size.Y * f)), col);
        }

        float tx = lx + Math.Max(p.LaserCharge.Length, 1) * 13f * _s + 12f * _s;
        float ly = origin.Y + 14f * _s, step = 22f * _s;
        Text(new Vector2(tx, ly), $"LSR  {Pips(p.LaserRecharge)}", Green, 14);
        Text(new Vector2(tx, ly + step), $"SHD  {(p.Class.HasShields ? Pips(p.ShieldRecharge) : "----")}", Green, 14);
        Text(new Vector2(tx, ly + step * 2), $"ENG  {EnergySystem.EngineFactor(p, world.Rules) * 100f:0}%", Green, 14);
        Text(new Vector2(tx, ly + step * 3), $"FIRE {p.FireMode.ToString().ToUpperInvariant()}", Green, 14);
        Text(new Vector2(tx, ly + step * 4), $"THR  {p.Controls.Throttle * 100f:0}%", Yellow, 14);
        if (p.Hyperspace == HyperspaceState.Charging) Text(new Vector2(tx, ly + step * 5.2f), "HYPERDRIVE", Yellow, 14);
    }

    private static string Pips(RechargeLevel level) => level switch
    {
        RechargeLevel.Off => "OFF",
        _ => new string('|', (int)level).PadRight(3, '.'),
    };

    private void DrawShields(Ship p, Vector2 c)
    {
        float r = 46f * _s;
        if (p.Class.HasShields)
        {
            float cap = p.Class.ShieldCapacity;
            DrawArc(c, r, Mathf.Pi * 1.1f, Mathf.Pi * 1.9f, 24, ShieldColor(p.ShieldFront / cap), 6f * _s);
            DrawArc(c, r, Mathf.Pi * 0.1f, Mathf.Pi * 0.9f, 24, ShieldColor(p.ShieldRear / cap), 6f * _s);
            string focus = p.ShieldFocus switch { ShieldFocus.Front => "FWD", ShieldFocus.Rear => "AFT", _ => "EVEN" };
            Text(c + new Vector2(-r, r + 22f * _s), $"SHIELDS {focus}", Dim, 12, HorizontalAlignment.Center, r * 2);
        }
        // Simple top-down silhouette.
        DrawLine(c + new Vector2(0, -20f * _s), c + new Vector2(0, 20f * _s), Green, 3f);
        DrawLine(c + new Vector2(-18f * _s, 8f * _s), c + new Vector2(18f * _s, 8f * _s), Green, 3f);
        Text(c + new Vector2(-r, -r - 8f * _s), $"HULL {p.HullFraction * 100f:0}%", p.HullFraction > 0.3f ? Green : Red, 13, HorizontalAlignment.Center, r * 2);
    }

    private static Color ShieldColor(float f) =>
        f <= 0.01f ? new Color(Dim, 0.25f) : f < 0.35f ? Red : f < 0.7f ? Yellow : Green;

    private void DrawMessages(Vector2 size)
    {
        double now = SimNow;
        float y = 30f * _s;
        foreach (var (text, at) in _messages.Where(m => now - m.At < 8).TakeLast(5))
        {
            float a = (float)Math.Clamp(8 - (now - at), 0, 1);
            Text(new Vector2(20f * _s, y), text, new Color(Green, a), 16);
            y += 24f * _s;
        }
    }

    private void DrawGoals(World world, Vector2 size)
    {
        if (world.Mission is not { } m) return;
        float x = size.X - 330f * _s, y = 30f * _s;
        int secs = (int)world.Time;
        Text(new Vector2(x, y), $"{m.Definition.Name}   {secs / 60:00}:{secs % 60:00}", Dim, 14);
        for (int i = 0; i < m.Goals.Length; i++)
        {
            Color col = m.Goals[i] switch { GoalState.Complete => Green, GoalState.Failed => Red, _ => Dim };
            string mark = m.Goals[i] switch { GoalState.Complete => "[x]", GoalState.Failed => "[!]", _ => "[ ]" };
            Text(new Vector2(x, y + (i + 1) * 20f * _s), $"{mark} {m.Definition.Goals[i].Describe()}", col, 14);
        }
    }

    private void DrawOverlays(World world, Ship? player, Vector2 size)
    {
        double sinceFlash = SimNow - _flashAt;
        if (sinceFlash < 1.5)
            DrawRect(new Rect2(Vector2.Zero, size), new Color(1, 1, 1, (float)(1 - sinceFlash / 1.5)));

        if (world.Time < 12f)
            Text(new Vector2(0, size.Y * 0.32f),
                "Arrows: pitch/yaw   Q/E: roll   Space: fire   [ ] \\ Backspace: throttle   X: link   T/R: target   F9/F10: power   H: hyperspace   V: view",
                new Color(Green, Mathf.Clamp(12f - world.Time, 0, 1)), 14, HorizontalAlignment.Center, size.X);

        if (Host.Paused)
            Text(new Vector2(0, size.Y * 0.4f), "PAUSED", Yellow, 32, HorizontalAlignment.Center, size.X);

        if (world.Mission?.Result is { } result)
        {
            Color col = result == MissionResult.Success ? Green : Red;
            string title = result == MissionResult.Success ? "MISSION COMPLETE" : "MISSION FAILED";
            Text(new Vector2(0, size.Y * 0.4f), title, col, 36, HorizontalAlignment.Center, size.X);
            Text(new Vector2(0, size.Y * 0.4f + 36f * _s), $"{world.Mission.ResultReason}   (F5 to fly again)", col, 16, HorizontalAlignment.Center, size.X);
        }
    }
}
