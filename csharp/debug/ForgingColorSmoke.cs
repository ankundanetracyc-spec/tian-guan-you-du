using Godot;

/// <summary>成形结束后的材质应与无材质的原图一致，同时保留节点着色和透明度。</summary>
public partial class ForgingColorSmoke : Node
{
    private async Task Frames(int count = 2)
    {
        for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
    public override async void _Ready()
    {
        try
        {
            if (DisplayServer.GetName() == "headless") throw new InvalidOperationException("颜色检查需要图形渲染。");
            GetWindow().Mode = Window.ModeEnum.Windowed; GetWindow().Size = new(640, 480); GetWindow().Unfocusable = true;
            var shader = GD.Load<Shader>("res://shaders/forging_materialize.gdshader");
            int checks = 0;
            foreach (string file in new[] { "slender_sword_2x6.png", "broad_sword_3x6.png", "spined_sword_3x6.png" })
            foreach (var tint in new[] { Colors.White, new Color(.74f, .82f, .90f, .55f) })
            {
                var source = GD.Load<Texture2D>(InventoryIconDensityPack.Root + file);
                int width = source.GetWidth(), height = source.GetHeight();
                var viewport = new SubViewport { Size = new(width * 2, height), TransparentBg = true, Disable3D = true,
                    RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
                AddChild(viewport);
                var material = new ShaderMaterial { Shader = shader };
                material.SetShaderParameter("reveal", 1f); material.SetShaderParameter("heat", 0f);
                for (int side = 0; side < 2; side++)
                    viewport.AddChild(new TextureRect { Texture = source, Size = source.GetSize(), Position = new(width * side, 0),
                        Modulate = tint, Material = side == 0 ? null : material,
                        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale });
                await Frames(3);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var pixels = viewport.GetTexture().GetImage();
                int compared = 0; float maxError = 0;
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                {
                    var expected = pixels.GetPixel(x, y); var actual = pixels.GetPixel(x + width, y);
                    if (expected.A < .02f && actual.A < .02f) continue;
                    float difference = Math.Max(Math.Max(Math.Abs(expected.R - actual.R), Math.Abs(expected.G - actual.G)),
                        Math.Max(Math.Abs(expected.B - actual.B), Math.Abs(expected.A - actual.A)));
                    maxError = Math.Max(maxError, difference); compared++;
                }
                if (compared < 1000 || maxError > 2.1f / 255)
                    throw new InvalidOperationException($"{file} tint={tint} 原图与成形材质不一致：pixels={compared} error={maxError}");
                GD.Print($"FORGING_COLOR_OK file={file} tinted={tint != Colors.White} pixels={compared} max_error={maxError}");
                checks++; viewport.QueueFree(); await Frames();
            }
            GD.Print($"FORGING_COLOR_PASS checks={checks}"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
