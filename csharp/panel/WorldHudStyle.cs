using Godot;

/// <summary>常驻时间与人物状态的共用纸墨样式，无阴影、描边文字或泛光。</summary>
public static class WorldHudStyle
{
    public const string PaperPath = "res://textures/ui/world_hud_20261011/paper_panel_v1.png";
    private const string FontPath = "res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf";
    public static readonly Color Ink = new("303d38"), SecondaryInk = new("535e54");
    public static readonly Color HealthInk = new("985344"), StaminaInk = new("537264");

    public static StyleBoxFlat QuietPaper(bool hovered = false, bool border = true) => new()
    {
        BgColor = new Color(hovered ? "e2e7d9" : "f0eddf"),
        BorderColor = new Color("a3a594"),
        BorderWidthLeft = border ? 1 : 0, BorderWidthRight = border ? 1 : 0,
        BorderWidthTop = border ? 1 : 0, BorderWidthBottom = border ? 1 : 0,
        CornerRadiusTopLeft = 3, CornerRadiusBottomRight = 3,
        ContentMarginLeft = 8, ContentMarginRight = 8,
        ContentMarginTop = 3, ContentMarginBottom = 3
    };

    public static void StylePaperButton(Button button, int fontSize = 16)
    {
        StyleSmallButton(button, fontSize);
        foreach (var state in new[] { "normal", "hover", "pressed" })
            button.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        if (button.GetNodeOrNull<WorldHudFrame>("InkButtonPaper") is null)
        {
            var paper = new WorldHudFrame { Name = "InkButtonPaper", ShowBehindParent = true };
            button.AddChild(paper);
            paper.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            button.MouseEntered += () => paper.Modulate = new Color("e1e9db");
            button.MouseExited += () => paper.Modulate = Colors.White;
        }
    }

    private static AtlasTexture PaperTexture() => new()
    {
        Atlas = GD.Load<Texture2D>(PaperPath),
        Region = new Rect2(20, 78, 2132, 568),
        FilterClip = true
    };

    public static StyleBoxTexture Paper(bool hovered = false) => new()
    {
        Texture = PaperTexture(),
        TextureMarginLeft = 20, TextureMarginRight = 20,
        TextureMarginTop = 10, TextureMarginBottom = 10,
        ModulateColor = hovered ? new Color(.96f, .98f, .95f) : Colors.White,
        ContentMarginLeft = 14, ContentMarginRight = 14,
        ContentMarginTop = 10, ContentMarginBottom = 10
    };

    public static Label Text(string text, int size = 18, bool secondary = false)
    {
        var label = new Label
        {
            Text = text, VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        StyleText(label, size, secondary);
        return label;
    }

    public static void StyleText(Label label, int size = 18, bool secondary = false)
    {
        label.AddThemeFontOverride("font", GD.Load<FontFile>(FontPath));
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", secondary ? SecondaryInk : Ink);
        label.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
        label.AddThemeColorOverride("font_outline_color", Colors.Transparent);
        label.AddThemeConstantOverride("outline_size", 0);
        label.AddThemeConstantOverride("shadow_offset_x", 0);
        label.AddThemeConstantOverride("shadow_offset_y", 0);
    }

    public static RichTextLabel RichText(string bbcode, int size = 18, bool secondary = false)
    {
        var label = new RichTextLabel
        {
            Text = bbcode, BbcodeEnabled = true, FitContent = true, ScrollActive = false,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        var font = GD.Load<FontFile>(FontPath);
        label.AddThemeFontOverride("normal_font", font);
        label.AddThemeFontOverride("bold_font", font);
        label.AddThemeFontSizeOverride("normal_font_size", size);
        label.AddThemeColorOverride("default_color", secondary ? SecondaryInk : Ink);
        label.AddThemeConstantOverride("line_separation", 0);
        return label;
    }

    public static void StyleSmallButton(Button button, int fontSize = 18)
    {
        button.AddThemeFontOverride("font", GD.Load<FontFile>(FontPath));
        button.AddThemeFontSizeOverride("font_size", fontSize);
        foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" })
            button.AddThemeColorOverride(state, Ink);
        button.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
        button.AddThemeColorOverride("font_outline_color", Colors.Transparent);
        button.AddThemeConstantOverride("outline_size", 0);
        foreach (var state in new[] { "normal", "hover", "pressed" })
            button.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        button.AddThemeStyleboxOverride("focus", new StyleBoxFlat
        {
            BgColor = Colors.Transparent, BorderColor = StaminaInk, BorderWidthBottom = 1
        });
        button.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
    }

    public static ProgressBar Bar(Color ink)
    {
        var bar = new ProgressBar
        {
            ShowPercentage = false, MinValue = 0, MaxValue = 100,
            CustomMinimumSize = new Vector2(90, 12),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        bar.AddThemeStyleboxOverride("background", new StyleBoxTexture
        {
            Texture = WorldHudArt.Texture("white_stroke"), ModulateColor = new Color("9ea595", .35f),
            TextureMarginLeft = 14, TextureMarginRight = 14
        });
        bar.AddThemeStyleboxOverride("fill", new StyleBoxTexture
        {
            Texture = WorldHudArt.Texture("white_stroke"), ModulateColor = ink,
            TextureMarginLeft = 14, TextureMarginRight = 14
        });
        return bar;
    }

    public static PanelContainer Seal(string character)
    {
        var seal = new PanelContainer
        {
            CustomMinimumSize = new Vector2(22, 22),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        var cinnabar = new Color("99624f");
        seal.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = Colors.Transparent, BorderColor = new Color(.60f, .38f, .31f, .7f),
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
            ContentMarginLeft = 1, ContentMarginRight = 1
        });
        var text = Text(character, 15);
        text.HorizontalAlignment = HorizontalAlignment.Center;
        text.AddThemeColorOverride("font_color", cinnabar);
        seal.AddChild(text);
        return seal;
    }

    public static ColorRect Rule() => new()
    {
        CustomMinimumSize = new Vector2(0, 1),
        Color = new Color(.27f, .36f, .31f, .23f),
        MouseFilter = Control.MouseFilterEnum.Ignore
    };
}
