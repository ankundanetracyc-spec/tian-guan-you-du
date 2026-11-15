using Godot;

/// <summary>Native frame drawing with reusable hand-painted paper and ink assets.</summary>
public partial class WorldHudFrame : Control
{
    private StyleBoxTexture? _panel, _tab, _plain;
    private Texture2D? _ring, _brush;
    public bool ShowBorder { get; set; } = true;
    public bool Circular { get; set; }
    public bool Rectangular { get; set; }
    public bool InkBrush { get; set; }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _panel = WorldHudArt.Surface();
        _tab = WorldHudArt.Surface(true);
        _plain = WorldHudArt.Surface(true, false);
        _ring = WorldHudArt.Texture("ink_ring");
        _brush = WorldHudArt.Texture("brush_band");
        Resized += QueueRedraw;
    }

    public override void _Draw()
    {
        if (_panel is null || Size.X <= 0 || Size.Y <= 0) return;
        if (InkBrush)
        {
            DrawTextureRect(_brush!, new Rect2(Vector2.Zero, Size), false);
            return;
        }
        if (Circular)
        {
            DrawCircle(Size * .5f, Mathf.Min(Size.X, Size.Y) * .44f, new Color("f0eddf"));
            DrawTextureRect(_ring!, new Rect2(Vector2.Zero, Size), false);
            return;
        }
        // Paint at half the asset scale, keeping fine ink edges at any panel size.
        DrawSetTransform(Vector2.Zero, 0, Vector2.One * .5f);
        DrawStyleBox(!ShowBorder ? _plain! : Size.Y < 90 ? _tab! : _panel, new Rect2(Vector2.Zero, Size * 2));
    }
}
