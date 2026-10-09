using Godot;
using System.Collections.Generic;

/// <summary>Layered deforming ribbons and independent, damped ink debris.</summary>
public partial class BattleInkRibbon : Node2D
{
    private static Shader? _shader;
    private static ArrayMesh? _mesh;
    private static ImageTexture? _noise;
    public static Texture2D SharedNoise => _noise ??= MakeNoise();
    private readonly List<ShaderMaterial> _materials = [];
    private readonly List<(Polygon2D Visual, Vector2 Origin, Vector2 Velocity, float Delay, float Size)> _drops = [];

    public void Configure(float span, float seed, Color color, bool contact, bool critical, Vector2 direction)
    {
        _shader ??= GD.Load<Shader>("res://shaders/battle_ink_ribbon.gdshader");
        if (_shader is null) return;
        _mesh ??= MakeMesh();
        _noise ??= MakeNoise();
        var bend = contact ? 16f : Mathf.Clamp(span * 0.19f, 52f, 86f);
        Scale = new Vector2(direction.X < 0f ? -1f : 1f, 1f);
        if (contact) Rotation = direction.Angle() - (direction.X < 0f ? Mathf.Pi : 0f) - 0.27f;
        for (var layer = 0; layer < 4; layer++)
        {
            var material = new ShaderMaterial { Shader = _shader };
            material.SetShaderParameter("ink_noise", _noise);
            material.SetShaderParameter("span", span * (layer > 1 ? 0.96f : 1f));
            material.SetShaderParameter("bend", bend + (layer - 1f) * 4f);
            material.SetShaderParameter("seed_value", seed * 0.007f + layer * 0.13f);
            material.SetShaderParameter("layer", (float)layer);
            material.SetShaderParameter("contact", contact ? 1f : 0f);
            material.SetShaderParameter("ink_color", critical ? color.Lerp(new Color(0.38f, 0.12f, 0.085f), 0.30f) : color);
            _materials.Add(material);
            AddChild(new MeshInstance2D { Name = $"InkRibbonLayer{layer}", Mesh = _mesh, Material = material, ZIndex = layer });
        }
        var rng = new RandomNumberGenerator { Seed = (ulong)(seed * 65537f) + 1UL };
        for (var i = 0; i < 5; i++)
        {
            var u = rng.RandfRange(0.28f, 0.90f);
            var angle = Mathf.Lerp(-2.94f, -0.20f, u);
            var origin = contact ? new Vector2((u - 0.5f) * span * 0.58f, 0f)
                : new Vector2(Mathf.Cos(angle) * span * 0.48f, Mathf.Sin(angle) * bend);
            var outward = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var velocity = outward * rng.RandfRange(36f, 128f) + Vector2.Right * rng.RandfRange(20f, 100f);
            var size = rng.RandfRange(1.6f, i < 2 ? 4.8f : 2.4f);
            Vector2[] shape = new Vector2[9];
            for (var j = 0; j < shape.Length; j++)
                shape[j] = Vector2.FromAngle(j * Mathf.Tau / shape.Length) * rng.RandfRange(0.70f, 1.0f);
            var visual = new Polygon2D { Name = "InkDebris", Polygon = shape, Color = new Color(color.R, color.G, color.B, 0f), ZIndex = 5 };
            AddChild(visual);
            _drops.Add((visual, origin, velocity, contact ? rng.RandfRange(0.015f, 0.09f) : u * 0.115f, size));
        }
    }

    public void SetProgress(float progress, float seconds)
    {
        foreach (var material in _materials) material.SetShaderParameter("progress", progress);
        foreach (var drop in _drops)
        {
            var age = seconds - drop.Delay;
            if (age < 0f) { drop.Visual.Visible = false; continue; }
            drop.Visual.Visible = true;
            var travel = (1f - Mathf.Exp(-age * 3.8f)) / 3.8f;
            drop.Visual.Position = drop.Origin + drop.Velocity * travel + Vector2.Down * (age * age * 82f);
            drop.Visual.Rotation = (drop.Velocity + Vector2.Down * age * 164f).Angle();
            var fade = 1f - Mathf.SmoothStep(0.19f, 0.51f, age);
            var stretch = 1f + Mathf.Exp(-age * 12f) * 1.8f;
            drop.Visual.Scale = new Vector2(drop.Size * stretch, drop.Size * 0.76f) * (0.65f + fade * 0.35f);
            drop.Visual.Modulate = new Color(1f, 1f, 1f, fade * 0.84f);
            // Color is opaque once emitted; Modulate carries the drying opacity.
            var color = drop.Visual.Color;
            drop.Visual.Color = new Color(color.R, color.G, color.B, 1f);
        }
    }

    private static ImageTexture MakeNoise()
    {
        // Shared continuous simplex noise avoids a visible lattice in the pigment.
        var noise = new FastNoiseLite { Seed = 731, NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
            Frequency = 0.038f, FractalType = FastNoiseLite.FractalTypeEnum.Fbm, FractalOctaves = 4 };
        var image = Image.CreateEmpty(256, 256, false, Image.Format.Rgba8);
        for (var y = 0; y < 256; y++)
            for (var x = 0; x < 256; x++)
            {
                var n = Mathf.Clamp(0.5f + noise.GetNoise2D(x, y) * 0.72f, 0f, 1f);
                image.SetPixel(x, y, new Color(n, n, n, 1f));
            }
        image.GenerateMipmaps();
        var texture = ImageTexture.CreateFromImage(image);
        image.Dispose();
        noise.Dispose();
        return texture;
    }

    private static ArrayMesh MakeMesh()
    {
        const int columns = 64;
        const int rows = 8;
        var vertices = new Vector3[(columns + 1) * (rows + 1)];
        var uv = new Vector2[vertices.Length];
        var indices = new int[columns * rows * 6];
        for (var x = 0; x <= columns; x++)
            for (var y = 0; y <= rows; y++)
            {
                var i = x * (rows + 1) + y;
                uv[i] = new Vector2(x / (float)columns, y / (float)rows);
                vertices[i] = new Vector3((uv[i].X - 0.5f) * 600f, (uv[i].Y - 0.5f) * 300f, 0f);
            }
        var cursor = 0;
        for (var x = 0; x < columns; x++)
            for (var y = 0; y < rows; y++)
            {
                var a = x * (rows + 1) + y;
                var b = a + rows + 1;
                indices[cursor++] = a; indices[cursor++] = b; indices[cursor++] = a + 1;
                indices[cursor++] = a + 1; indices[cursor++] = b; indices[cursor++] = b + 1;
            }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.TexUV] = uv;
        arrays[(int)Mesh.ArrayType.Index] = indices;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }
}
