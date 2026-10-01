using Godot;

namespace XWing.Client;

public partial class Explosion : MeshInstance3D
{
    private float _age;
    private float _size = 10f;
    private StandardMaterial3D _mat = null!;
    private const float Life = 1.2f;

    public static Explosion Create(Vector3 position, float size)
    {
        var e = new Explosion { Position = position, _size = size };
        e._mat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = new Color(1f, 0.7f, 0.3f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.55f, 0.2f),
            EmissionEnergyMultiplier = 6f,
        };
        e.Mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 16, Rings = 8 };
        e.MaterialOverride = e._mat;
        e.Scale = Vector3.One * 0.1f;
        return e;
    }

    public override void _Process(double delta)
    {
        _age += (float)delta;
        float t = _age / Life;
        if (t >= 1f) { QueueFree(); return; }
        Scale = Vector3.One * (_size * Mathf.Sqrt(t) + 0.1f);
        var c = _mat.AlbedoColor;
        _mat.AlbedoColor = new Color(c.R, c.G * (1f - t * 0.5f), c.B * (1f - t), 1f - t);
    }
}
