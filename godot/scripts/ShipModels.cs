using Godot;
using XWing.Sim.Data;

namespace XWing.Client;

/// <summary>
/// Placeholder procedural models so the slice runs with zero art assets. The renderer only asks
/// for a Node3D per class id; swapping in imported models later touches nothing else.
/// </summary>
public static class ShipModels
{
    public static Node3D Build(ShipClass cls) => cls.Id.ToUpperInvariant() switch
    {
        "XWING" => XWing(),
        "TIE" => Tie(),
        "CRV" => Corvette(),
        _ => Fallback(cls),
    };

    private static StandardMaterial3D Mat(Color c, float metallic = 0.3f, float rough = 0.6f) =>
        new() { AlbedoColor = c, Metallic = metallic, Roughness = rough };

    private static StandardMaterial3D Glow(Color c, float energy = 4f) =>
        new() { AlbedoColor = c, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, EmissionEnabled = true, Emission = c, EmissionEnergyMultiplier = energy };

    private static MeshInstance3D Box(Node3D parent, Vector3 size, Vector3 pos, Material mat, Vector3? rotDeg = null)
    {
        var m = new MeshInstance3D { Mesh = new BoxMesh { Size = size }, Position = pos, MaterialOverride = mat };
        if (rotDeg is { } r) m.RotationDegrees = r;
        parent.AddChild(m);
        return m;
    }

    private static MeshInstance3D Cyl(Node3D parent, float radius, float length, Vector3 pos, Material mat, Vector3 rotDeg, int segments = 12)
    {
        var m = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = length, RadialSegments = segments },
            Position = pos,
            RotationDegrees = rotDeg,
            MaterialOverride = mat,
        };
        parent.AddChild(m);
        return m;
    }

    private static Node3D XWing()
    {
        var root = new Node3D { Name = "XWing" };
        var hull = Mat(new Color(0.82f, 0.82f, 0.8f));
        var stripe = Mat(new Color(0.7f, 0.12f, 0.1f));
        Box(root, new Vector3(1.3f, 1.3f, 11f), new Vector3(0, 0, -0.5f), hull);
        Box(root, new Vector3(0.7f, 0.6f, 3f), new Vector3(0, 0, -7.2f), hull);
        Box(root, new Vector3(0.9f, 0.5f, 2.2f), new Vector3(0, 0.75f, 0.5f), Mat(new Color(0.15f, 0.18f, 0.22f), 0.8f, 0.2f));
        foreach (int sx in new[] { -1, 1 })
        foreach (int sy in new[] { -1, 1 })
        {
            // S-foils open in attack position (~13°).
            var wing = new Node3D { Position = new Vector3(0, 0, 2f), RotationDegrees = new Vector3(0, 0, sx * sy * 13f) };
            root.AddChild(wing);
            Box(wing, new Vector3(5.2f, 0.18f, 2.8f), new Vector3(sx * 3.2f, sy * 0.35f, 0), hull);
            Box(wing, new Vector3(1.4f, 0.2f, 2.85f), new Vector3(sx * 2.2f, sy * 0.36f, 0), stripe);
            Cyl(wing, 0.55f, 3f, new Vector3(sx * 1.1f, sy * 0.75f, 0.2f), hull, new Vector3(90, 0, 0));
            Box(wing, new Vector3(0.6f, 0.6f, 0.2f), new Vector3(sx * 1.1f, sy * 0.75f, 1.75f), Glow(new Color(1f, 0.45f, 0.3f), 3f));
            Cyl(wing, 0.1f, 4.5f, new Vector3(sx * 5.8f, sy * 0.35f, -3.2f), hull, new Vector3(90, 0, 0), 6);
        }
        return root;
    }

    private static Node3D Tie()
    {
        var root = new Node3D { Name = "TIE" };
        var gray = Mat(new Color(0.45f, 0.47f, 0.5f));
        var panel = Mat(new Color(0.12f, 0.13f, 0.15f), 0.5f, 0.4f);
        root.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 1.4f, Height = 2.8f }, MaterialOverride = gray });
        root.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.7f, BottomRadius = 0.7f, Height = 0.2f },
            Position = new Vector3(0, 0, -1.35f), RotationDegrees = new Vector3(90, 0, 0),
            MaterialOverride = Mat(new Color(0.1f, 0.12f, 0.12f), 0.9f, 0.1f),
        });
        Box(root, new Vector3(5.6f, 0.5f, 0.5f), Vector3.Zero, gray);
        foreach (int sx in new[] { -1, 1 })
        {
            Cyl(root, 4.2f, 0.25f, new Vector3(sx * 2.9f, 0, 0), panel, new Vector3(0, 0, 90), 6);
            Cyl(root, 0.6f, 0.4f, new Vector3(sx * 2.9f, 0, 0), gray, new Vector3(0, 0, 90), 6);
        }
        return root;
    }

    private static Node3D Corvette()
    {
        var root = new Node3D { Name = "Corvette" };
        var hull = Mat(new Color(0.88f, 0.86f, 0.8f));
        var trim = Mat(new Color(0.6f, 0.15f, 0.12f));
        Box(root, new Vector3(22f, 12f, 30f), new Vector3(0, 0, -62f), hull);
        Box(root, new Vector3(12f, 11f, 70f), new Vector3(0, 0, -12f), hull);
        Box(root, new Vector3(12.2f, 1.5f, 70f), new Vector3(0, 0, -12f), trim);
        Box(root, new Vector3(34f, 22f, 28f), new Vector3(0, 0, 38f), hull);
        Box(root, new Vector3(36f, 2f, 6f), new Vector3(0, 12f, 28f), trim);
        for (int i = 0; i < 11; i++)
        {
            float x = -14f + (i % 6) * 5.6f;
            float y = i < 6 ? 5f : -5f;
            Box(root, new Vector3(3.8f, 3.8f, 1f), new Vector3(x, y, 52.6f), Glow(new Color(1f, 0.35f, 0.2f), 5f));
        }
        Box(root, new Vector3(4f, 3f, 6f), new Vector3(0, 7.5f, -64f), Mat(new Color(0.2f, 0.22f, 0.25f), 0.8f, 0.2f));
        return root;
    }

    private static Node3D Fallback(ShipClass cls)
    {
        var root = new Node3D { Name = cls.Id };
        float r = cls.CollisionRadius;
        Box(root, new Vector3(r, r * 0.4f, r * 2f), Vector3.Zero, Mat(Colors.Magenta));
        return root;
    }
}
