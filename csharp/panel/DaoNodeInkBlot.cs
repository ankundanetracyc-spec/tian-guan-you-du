using Godot;

/// <summary>承载道途节点文字的水墨团，不使用规整矩形框。</summary>
public partial class DaoNodeInkBlot : Control
{
    private Color _ink = new(.16f, .13f, .09f, .75f);
    public Color Ink { get => _ink; set { _ink = value; QueueRedraw(); } }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Draw()
    {
        if (Size.X <= 1 || Size.Y <= 1) return;
        var center = Size * .5f;
        var offsets = new[] { new Vector2(-.28f, -.08f), new Vector2(-.12f, -.21f), new Vector2(.10f, -.17f), new Vector2(.30f, -.03f), new Vector2(-.30f, .13f), new Vector2(-.08f, .19f), new Vector2(.14f, .17f), new Vector2(.31f, .11f) };
        for (var i = 0; i < offsets.Length; i++)
        {
            var radius = Mathf.Max(Size.Y * (.43f + (i % 3) * .035f), 20f);
            var position = center + new Vector2(offsets[i].X * Size.X, offsets[i].Y * Size.Y);
            DrawCircle(position, radius, new Color(_ink, _ink.A * (.36f + (i % 3) * .07f)), true);
        }
        DrawArc(center, Mathf.Max(Size.X, Size.Y) * .44f, .20f, 2.80f, 26, new Color(_ink, _ink.A * .55f), 1.5f, true);
        DrawArc(center, Mathf.Max(Size.X, Size.Y) * .41f, 3.35f, 5.70f, 22, new Color(_ink, _ink.A * .32f), 1.2f, true);
    }
}
