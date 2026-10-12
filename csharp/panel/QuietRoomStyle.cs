using Godot;

/// <summary>静室与休息的共用纸面控件；插画不包含任何交互文字。</summary>
public static class QuietRoomStyle
{
    public const string Assets = "res://textures/ui/cultivation/quiet_room_20261010/";
    public static readonly Color Ink = new("303b38"), Teal = new("396965");
    public static StyleBoxFlat Paper(float alpha = .94f, int padding = 20) => new()
    {
        BgColor = new Color(.96f, .945f, .89f, alpha), BorderColor = new Color(.40f, .44f, .39f, .32f),
        BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
        ContentMarginLeft = padding, ContentMarginRight = padding, ContentMarginTop = padding, ContentMarginBottom = padding
    };
    public static Theme MakeTheme()
    {
        var theme = GD.Load<Theme>("res://resources/font/default_theme.tres").Duplicate() as Theme ?? new Theme();
        theme.DefaultFontSize = 20;
        theme.SetColor("font_color", "Label", Ink);
        foreach (var type in new[] { "Button", "OptionButton" })
        {
            theme.SetFontSize("font_size", type, 20);
            theme.SetColor("font_color", type, Ink);
            theme.SetColor("font_hover_color", type, Ink);
            theme.SetColor("font_pressed_color", type, new Color("f5f1e5"));
            theme.SetColor("font_disabled_color", type, new Color("777c70"));
            theme.SetStylebox("normal", type, Paper(.82f, 14));
            var hover = Paper(.98f, 14); hover.BorderColor = Teal; theme.SetStylebox("hover", type, hover);
            var selected = Paper(1, 14); selected.BgColor = Teal; theme.SetStylebox("pressed", type, selected);
            theme.SetStylebox("disabled", type, Paper(.5f, 14));
            var focus = new StyleBoxFlat { BgColor = Colors.Transparent, BorderColor = Teal, BorderWidthBottom = 2 }; theme.SetStylebox("focus", type, focus);
        }
        theme.SetColor("font_color", "LineEdit", Ink);
        theme.SetStylebox("normal", "LineEdit", Paper(.94f, 10));
        theme.SetFontSize("font_size", "LineEdit", 22);
        theme.SetColor("font_color", "PopupMenu", Ink);
        theme.SetColor("font_hover_color", "PopupMenu", Ink);
        theme.SetStylebox("panel", "PopupMenu", Paper(1, 12));
        theme.SetStylebox("hover", "PopupMenu", Paper(1, 12));
        theme.SetStylebox("background", "ProgressBar", new StyleBoxFlat { BgColor = new Color(.42f, .47f, .42f, .2f), CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6 });
        theme.SetStylebox("fill", "ProgressBar", new StyleBoxFlat { BgColor = Teal, CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6 });
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
        button.CustomMinimumSize = new Vector2(0, 58);
        var normal = Paper(1, 16); normal.BgColor = Teal;
        var hover = Paper(1, 16); hover.BgColor = new Color("477b75");
        button.AddThemeStyleboxOverride("normal", normal); button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeColorOverride("font_color", new Color("f5f1e5")); button.AddThemeColorOverride("font_hover_color", new Color("ffffff"));
        button.AddThemeFontSizeOverride("font_size", 24);
    }
}
