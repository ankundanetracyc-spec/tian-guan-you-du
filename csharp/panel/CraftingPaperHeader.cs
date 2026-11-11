using Godot;

/// <summary>工艺页共用顶栏几何；返回直接置于画布，纸条在其左侧收口。</summary>
public static partial class CraftingPaperHeader
{
    public static readonly Vector2 BarPosition = new(24, 10);
    public static readonly Vector2 BarSize = new(1494, 62);
    public static readonly Vector2 ReturnPosition = new(1534, 18);
    public static readonly Vector2 ReturnSize = new(114, 46);
    public const float ReturnRightInset = 24;
    public const int FontSize = 22;
    public const int RowTop = 8;
    public const int RowHeight = 46;
    public const float PaperBrightness = 1f;
    public const int MetricGap = 32;
    public enum Cell { Help, Context, Book1, Book2, Book, Spirit, Energy, Attainment }

    public static Rect2 Bounds(Cell cell) => cell switch
    {
        Cell.Help => new(14, RowTop, 136, RowHeight),
        Cell.Context => new(172, RowTop, 492, RowHeight),
        Cell.Book1 => new(680, RowTop, 108, RowHeight),
        Cell.Book2 => new(800, RowTop, 108, RowHeight),
        Cell.Book => new(680, RowTop, 228, RowHeight),
        _ => throw new ArgumentOutOfRangeException(nameof(cell), "Resource groups use the shared metrics row.")
    };

    public static void Place(Control bar, Control control, Cell cell)
    {
        if (control.GetParent() is null) bar.AddChild(control);
        else if (control.GetParent() != bar) control.Reparent(bar, false);
        var bounds = Bounds(cell);
        control.Position = bounds.Position;
        control.Size = bounds.Size;
    }

    public static Label Metric(Control bar, Cell cell, string caption, string value)
    {
        var row = bar.GetNodeOrNull<HBoxContainer>("CraftingHeaderMetrics");
        if (row is null)
        {
            row = new HBoxContainer { Name = "CraftingHeaderMetrics", Alignment = BoxContainer.AlignmentMode.End,
                Position = new Vector2(926, RowTop), Size = new Vector2(556, RowHeight) };
            row.AddThemeConstantOverride("separation", MetricGap);
            bar.AddChild(row);
        }
        var group = new HBoxContainer { Name = "CraftingHeader" + cell };
        group.AddThemeConstantOverride("separation", 8);
        row.AddChild(group);
        var label = Text(caption, true); label.Name = "CraftingHeader" + cell + "Caption";
        var number = Text(value); number.Name = "CraftingHeader" + cell + "Value";
        group.AddChild(label); group.AddChild(number);
        int index = 0;
        foreach (string name in new[] { "CraftingHeaderSpirit", "CraftingHeaderEnergy", "CraftingHeaderAttainment" })
            if (row.GetNodeOrNull<Control>(name) is { } control) row.MoveChild(control, index++);
        return number;
    }

    public static Label Text(string text, bool muted = false)
    {
        var label = CraftingPaperStyle.Text(text, FontSize, muted);
        label.CustomMinimumSize = new Vector2(0, RowHeight);
        label.SizeFlagsVertical = Control.SizeFlags.Fill;
        label.VerticalAlignment = VerticalAlignment.Center;
        return label;
    }

    // Match the approved character panel: original atlas paper, without the
    // crafting page's older darkening or desaturation. Other consumers opt in.
    public static StyleBoxTexture Paper(int padding = 18, bool bordered = true, float brightness = PaperBrightness, float alpha = 1) =>
        CraftingPaperStyle.Paper(padding, bordered, brightness, alpha, saturation: 1);

    public static Panel Create(Control canvas, string name)
    {
        var bar = new Panel { Name = name };
        canvas.AddChild(bar);
        Apply(bar);
        return bar;
    }

    public static void Apply(Control bar)
    {
        bar.Position = BarPosition;
        bar.Size = BarSize;
        bar.AddThemeStyleboxOverride("panel", Paper(0));
    }

    public static void PlaceReturn(Control canvas, Button button)
    {
        if (button.GetParent() != canvas) button.Reparent(canvas, false);
        button.AnchorLeft = button.AnchorRight = 1;
        button.AnchorTop = button.AnchorBottom = 0;
        button.OffsetLeft = -(ReturnRightInset + ReturnSize.X);
        button.OffsetTop = ReturnPosition.Y;
        button.OffsetRight = -ReturnRightInset;
        button.OffsetBottom = ReturnPosition.Y + ReturnSize.Y;
        button.AddThemeFontSizeOverride("font_size", FontSize);
    }

    public static void AddReturnArrow(Button button, Color? color = null)
    {
        var marker = button.GetNodeOrNull<ReturnMarker>("ReturnMarker");
        if (marker is null)
        {
            marker = new ReturnMarker { Name = "ReturnMarker", MouseFilter = Control.MouseFilterEnum.Ignore, Size = new Vector2(8, 10) };
            button.AddChild(marker);
            void Fit() => marker.Position = new Vector2(button.Size.X - 22, (button.Size.Y - 10) / 2);
            button.Resized += Fit; Fit();
        }
        marker.Color = color ?? CraftingPaperStyle.Ink;
    }

    private partial class ReturnMarker : Control
    {
        public Color Color { get; set; } = CraftingPaperStyle.Ink;
        public override void _Draw() => DrawColoredPolygon([new(0, 0), new(0, 10), new(7, 5)], Color);
    }
}
