using Godot;

/// <summary>静室与休息的共用纸面控件；插画不包含任何交互文字。</summary>
public static class QuietRoomStyle
{
    public const string Assets = "res://textures/ui/cultivation/quiet_room_20261010/";
    public static readonly Color Ink = new("303b38"), Teal = new("396965");
    public static StyleBoxTexture Paper(float alpha = .94f, int padding = 20)
    {
        var paper = new StyleBoxTexture
        {
            Texture = new AtlasTexture
            {
                Atlas = GD.Load<Texture2D>("res://textures/ui/inventory_20261012/approved_shell_v1.png"),
                Region = new Rect2(340, 150, 1120, 580), FilterClip = true
            },
            TextureMarginLeft = 16, TextureMarginRight = 16, TextureMarginTop = 16, TextureMarginBottom = 16
        };
        paper.ModulateColor = new Color(1, 1, 1, alpha);
        paper.ContentMarginLeft = paper.ContentMarginRight = padding;
        paper.ContentMarginTop = paper.ContentMarginBottom = padding;
        return paper;
    }
    public static StyleBoxFlat Edge(bool selected = false, int padding = 12) => new()
    {
        BgColor = Colors.Transparent, BorderColor = selected ? Teal : new Color("a39372"),
        BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = selected ? 2 : 1,
        ContentMarginLeft = padding, ContentMarginRight = padding,
        ContentMarginTop = padding == 0 ? 0 : 8, ContentMarginBottom = padding == 0 ? 0 : 8
    };
    // 固定设计坐标随窗口等比缩放，防止中文、日期和横向栏位在小窗口中互相挤压。
    public static Control Canvas(Control parent)
    {
        var canvas = new Control { Name = "MaterialCanvas", MouseFilter = Control.MouseFilterEnum.Pass };
        parent.AddChild(canvas);
        void Fit()
        {
            var area = parent.Size;
            var scale = Mathf.Min(area.X / 1672f, area.Y / 941f);
            canvas.Size = new Vector2(1672, 941);
            canvas.Scale = Vector2.One * scale;
            canvas.Position = (area - canvas.Size * scale) / 2;
        }
        parent.Resized += Fit; Fit();
        return canvas;
    }
    public static void Frame(Control parent, bool dark = false)
    {
        var frame = new InventoryPaperFrame { Dark = dark, FillPaper = false };
        parent.AddChild(frame); parent.MoveChild(frame, 0);
        frame.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
    }
    public static Theme MakeTheme()
    {
        var theme = GD.Load<Theme>("res://resources/font/default_theme.tres").Duplicate() as Theme ?? new Theme();
        theme.DefaultFontSize = 20;
        theme.DefaultFont = GD.Load<FontFile>("res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf");
        theme.SetColor("font_color", "Label", Ink);
        foreach (var type in new[] { "Button", "OptionButton" })
        {
            theme.SetFontSize("font_size", type, 20);
            theme.SetColor("font_color", type, Ink);
            theme.SetColor("font_hover_color", type, Ink);
            theme.SetColor("font_pressed_color", type, Teal);
            theme.SetColor("font_focus_color", type, Ink);
            theme.SetColor("font_disabled_color", type, new Color("777c70"));
            theme.SetStylebox("normal", type, Edge());
            theme.SetStylebox("hover", type, Edge(true));
            theme.SetStylebox("pressed", type, Edge(true));
            theme.SetStylebox("hover_pressed", type, Edge(true));
            theme.SetStylebox("disabled", type, Edge());
            var focus = new StyleBoxFlat { BgColor = Colors.Transparent, BorderColor = Teal, BorderWidthBottom = 2 }; theme.SetStylebox("focus", type, focus);
        }
        theme.SetColor("font_color", "LineEdit", Ink);
        theme.SetStylebox("normal", "LineEdit", Paper(.94f, 10));
        theme.SetFontSize("font_size", "LineEdit", 22);
        theme.SetColor("font_color", "PopupMenu", Ink);
        theme.SetColor("font_hover_color", "PopupMenu", Ink);
        theme.SetStylebox("panel", "PopupMenu", Paper(1, 12));
        theme.SetStylebox("hover", "PopupMenu", Edge(true));
        theme.SetStylebox("background", "ProgressBar", new StyleBoxFlat { BgColor = new Color(.42f, .47f, .42f, .2f) });
        theme.SetStylebox("fill", "ProgressBar", new StyleBoxFlat { BgColor = Teal });
        theme.SetStylebox("panel", "TooltipPanel", new StyleBoxFlat { BgColor = new Color("20251e"), BorderColor = new Color("a39372"), BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1, ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 10, ContentMarginBottom = 10 });
        theme.SetColor("font_color", "TooltipLabel", new Color("efe6cd"));
        theme.SetStylebox("separator", "HSeparator", new StyleBoxFlat { BgColor = new Color("a3937277"), ContentMarginTop = 1, ContentMarginBottom = 1 });
        return theme;
    }
    public static Label Text(string text, int size = 20) { var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore }; label.AddThemeFontSizeOverride("font_size", size); label.AddThemeColorOverride("font_color", Ink); return label; }
    public static TextureRect Background(Control parent, string asset)
    {
        var bg = new TextureRect { Name = "SharedBackground", Texture = GD.Load<Texture2D>(Assets + asset), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = Control.MouseFilterEnum.Ignore };
        parent.AddChild(bg); bg.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); return bg;
    }
    public static string Date(GameTimeMgr time) => $"{time.year}年{time.month}月{time.day}日 · {time.hour:00}时";
    // 与常驻 GameTimeUI 已有昼夜读数一致。
    public static bool IsDay(GameTimeMgr time) => time.hour is >= 6 and < 18;
    public static void Primary(Button button)
    {
        button.CustomMinimumSize = new Vector2(0, 50);
        var normal = Paper(1, 16);
        var hover = Paper(1, 16); hover.ModulateColor = new Color(1.07f, 1.05f, 1.01f);
        normal.ContentMarginTop = normal.ContentMarginBottom = 8;
        hover.ContentMarginTop = hover.ContentMarginBottom = 8;
        button.AddThemeStyleboxOverride("normal", normal); button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", normal);
        button.AddThemeColorOverride("font_color", Ink); button.AddThemeColorOverride("font_hover_color", Teal);
        button.AddThemeFontSizeOverride("font_size", 22);
        button.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        if (!button.HasNode("MaterialActionFrame"))
        {
            var frame = new InventoryPaperFrame { Name = "MaterialActionFrame", FillPaper = false };
            button.AddChild(frame); frame.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        }
    }
}
