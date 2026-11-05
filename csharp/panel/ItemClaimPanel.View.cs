using Godot;

public partial class ItemClaimPanel
{
    private void build_view()
    {
        var shade = new ColorRect { Color = new(.06f, .07f, .06f, .6f), MouseFilter = Control.MouseFilterEnum.Stop };
        shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); AddChild(shade);
        _canvas = new Control { Name = "ReceiptCanvas", Size = DesignSize, MouseFilter = Control.MouseFilterEnum.Stop }; AddChild(_canvas);
        put(new ColorRect { Color = new("ebe1c9"), MouseFilter = Control.MouseFilterEnum.Ignore }, _canvas, 0, 0, 1920, 1080);
        put(new TextureRect { Texture = GD.Load<Texture2D>("res://textures/ui/inventory_20261012/approved_shell_v1.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
            Modulate = new(1, 1, 1, .20f), MouseFilter = Control.MouseFilterEnum.Ignore }, _canvas, 0, 0, 1920, 1080);
        var back = button("‹", _canvas, 48, 32, 54, 54, request_finish);
        foreach (var state in new[] { "normal", "hover", "pressed" }) back.AddThemeStyleboxOverride(state,
            new StyleBoxTexture { Texture = GD.Load<Texture2D>("res://textures/ui/gongfa_back_button_v1.png") });
        back.AddThemeFontSizeOverride("font_size", 38);
        put(text("领取物品", 44, Ink), _canvas, 124, 26, 400, 70);
        var left = paper(_canvas, 42, 124, 442, 906); var right = paper(_canvas, 504, 124, 1370, 906);
        put(text("待领取", 30, Ink), left, 28, 20, 370, 45); put(rule(), left, 28, 76, 386, 1);
        _scroll = new ScrollContainer { Name = "ClaimItemsScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, VerticalScrollMode = ScrollContainer.ScrollMode.Auto };
        put(_scroll, left, 28, 98, 386, 680);
        _rows = new VBoxContainer { Name = "ClaimItemGrid", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _rows.AddThemeConstantOverride("separation", 8); _scroll.AddChild(_rows);
        var auto = button("自动放入", left, 72, 815, 298, 60, auto_place); auto.Name = "ClaimAutoPlace";
        put(text("行囊", 30, Ink), right, 28, 20, 400, 45); put(rule(), right, 28, 76, 1314, 1);
        _status = text("", 18, Muted); _status.Name = "ClaimFooterStatus"; put(_status, right, 28, 82, 940, 38);
        _surface = new NativeInventorySurface { Name = "BodyStorage", MouseFilter = Control.MouseFilterEnum.Pass, DrawContents = draw_inventory };
        put(_surface, right, 90, 120, 1220, 700);
        foreach (var region in Character.InventoryRegions)
        { var r = _regions[region.Id]; put(text(region.Name, 25, new("52664a")), _surface, r.Position.X, r.Position.Y - 40, r.Size.X, 36); }
        put(rule(), right, 28, 806, 1314, 1);
        var discard = new NativeInventorySurface { Name = "DiscardHint", MouseFilter = Control.MouseFilterEnum.Ignore };
        discard.DrawContents = s => dashed(s, new Rect2(Vector2.Zero, s.Size), new("b17c61"));
        put(discard, right, 28, 828, 410, 58);
        var hint = text("拖出行囊即丢弃", 23, new("a06f55")); hint.HorizontalAlignment = HorizontalAlignment.Center;
        put(hint, discard, 0, 0, 410, 58);
        put(text("未领取物品将在离开时放弃", 18, Muted), right, 462, 840, 370, 34);
        var confirm = button("确认领取", right, 862, 828, 220, 58, request_finish); confirm.Name = "ClaimFooterPrimary";
        confirm.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = new("748a68"), CornerRadiusTopLeft = 5, CornerRadiusTopRight = 5, CornerRadiusBottomLeft = 5, CornerRadiusBottomRight = 5 });
        confirm.AddThemeColorOverride("font_color", new("f6f0de"));
        var abandon = button("放弃剩余", right, 1102, 828, 240, 58, request_finish); abandon.Name = "ClaimFooterClose";
        _count = text("", 16, Muted); _count.HorizontalAlignment = HorizontalAlignment.Right; put(_count, right, 982, 82, 355, 38);
    }
    private void draw_inventory(NativeInventorySurface s)
    {
        foreach (var region in Character.InventoryRegions)
        {
            var r = _regions[region.Id]; s.DrawRect(r, new(.84f, .88f, .75f, .25f));
            var c = new Color(.55f, .62f, .47f, .5f);
            for (int x = 0; x <= region.Width; x++) s.DrawLine(r.Position + new Vector2(x * Cell, 0), r.Position + new Vector2(x * Cell, r.Size.Y), c);
            for (int y = 0; y <= region.Height; y++) s.DrawLine(r.Position + new Vector2(0, y * Cell), r.Position + new Vector2(r.Size.X, y * Cell), c);
        }
        if (_drag is not { Active: true } d || !_regions.TryGetValue(d.Region, out var regionRect)) return;
        var rect = new Rect2(regionRect.Position + new Vector2(d.Cell.X, d.Cell.Y) * Cell, new Vector2(d.Size.X, d.Size.Y) * Cell);
        var color = new Color(d.Valid ? "678851" : "ac7057"); s.DrawRect(rect, new Color(color, .22f));
        if (d.Valid) s.DrawRect(rect, color, false, 2); else dashed(s, rect, color);
    }
    private static void dashed(Control s, Rect2 r, Color c)
    {
        s.DrawDashedLine(r.Position, r.Position + new Vector2(r.Size.X, 0), c, 1.5f, 8);
        s.DrawDashedLine(r.Position + new Vector2(r.Size.X, 0), r.End, c, 1.5f, 8);
        s.DrawDashedLine(r.End, r.Position + new Vector2(0, r.Size.Y), c, 1.5f, 8);
        s.DrawDashedLine(r.Position + new Vector2(0, r.Size.Y), r.Position, c, 1.5f, 8);
    }
    private static Texture2D? load_icon(string path) => string.IsNullOrWhiteSpace(path) ? null : GD.Load<Texture2D>(path);
    private static Label text(string value, int size, Color color)
    {
        var l = new Label { Text = value, VerticalAlignment = VerticalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore,
            ClipText = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
        l.AddThemeFontOverride("font", GD.Load<FontFile>("res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf"));
        l.AddThemeFontSizeOverride("font_size", size); l.AddThemeColorOverride("font_color", color); return l;
    }
    private static void put(Control c, Control p, float x, float y, float w, float h) { c.Position = new(x, y); c.Size = new(w, h); p.AddChild(c); }
    private static Panel paper(Control p, float x, float y, float w, float h)
    { var c = new Panel { MouseFilter = Control.MouseFilterEnum.Pass }; c.AddThemeStyleboxOverride("panel", InventoryUiPaper.Style("index_panel_paper.png")); put(c, p, x, y, w, h); return c; }
    private static ColorRect rule() => new() { Color = new("b6a27e"), MouseFilter = Control.MouseFilterEnum.Ignore };
    private static void style_button(Button b)
    {
        b.FocusMode = Control.FocusModeEnum.None; b.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        b.AddThemeFontOverride("font", GD.Load<FontFile>("res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf")); b.AddThemeFontSizeOverride("font_size", 24);
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus", "hover_pressed" })
            b.AddThemeStyleboxOverride(state, state == "focus" ? new StyleBoxEmpty() : InventoryUiPaper.Style(state is "hover" or "pressed" ? "index_row_selected.png" : "index_row_normal.png", 42, 10));
        foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color" }) b.AddThemeColorOverride(state, Ink);
    }
    private static Button button(string caption, Control p, float x, float y, float w, float h, Action clicked)
    { var b = new Button { Text = caption }; style_button(b); put(b, p, x, y, w, h); b.Pressed += clicked; return b; }
}
