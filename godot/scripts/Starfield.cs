using Godot;

namespace XWing.Client;

/// <summary>Points on a large sphere that follows the camera, so stars never get closer.</summary>
public partial class Starfield : MultiMeshInstance3D
{
    public const float Radius = 9000f;

    public override void _Ready()
    {
        var rng = new RandomNumberGenerator { Seed = 1993 };
        const int count = 4000;
        var mm = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = new PointMesh(),
            InstanceCount = count,
        };
        for (int i = 0; i < count; i++)
        {
            // Uniform direction on a sphere.
            float z = rng.RandfRange(-1f, 1f);
            float a = rng.RandfRange(0f, Mathf.Tau);
            float r = Mathf.Sqrt(1f - z * z);
            mm.SetInstanceTransform(i, new Transform3D(Basis.Identity, new Vector3(r * Mathf.Cos(a), r * Mathf.Sin(a), z) * Radius));
            float b = Mathf.Pow(rng.Randf(), 3f) * 0.85f + 0.15f;
            mm.SetInstanceColor(i, new Color(b, b, b * rng.RandfRange(0.9f, 1.1f)));
        }
        Multimesh = mm;
        MaterialOverride = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            VertexColorUseAsAlbedo = true,
            UsePointSize = true,
            PointSize = 2f,
        };
        CastShadow = ShadowCastingSetting.Off;
    }
}
