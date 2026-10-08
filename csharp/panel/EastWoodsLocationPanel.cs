using Godot;
using System;

/// <summary>东林地全屏地点内景；进入副本必须由玩家主动选择。</summary>
public partial class EastWoodsLocationPanel : Control
{
    public event Action? ExplorationRequested;
    public event Action? Closed;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        ZIndex = 205;
        build();
        Hide();
    }

    public void show_location()
    {
        Show();
        MoveToFront();
    }

    public void close()
    {
        Hide();
        Closed?.Invoke();
    }

    private void build()
    {
        var background = new TextureRect
        {
            Texture = ResourceLoader.Load<Texture2D>("res://textures/world_locations/east_woods_overview_v1.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var shade = new ColorRect { Color = new Color(0.025f, 0.035f, 0.032f, 0.16f), MouseFilter = MouseFilterEnum.Ignore };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);

        var title = make_label("东林地", 36, new Color("f1dfad"));
        title.Position = new Vector2(64, 46);
        AddChild(title);
        var subtitle = make_label("林地与山野　·　东林镇妖会辖域", 17, new Color("e3d6b6"));
        subtitle.Position = new Vector2(68, 98);
        AddChild(subtitle);

        var explore = make_scene_tag("深入林地");
        explore.AnchorLeft = 0.5f;
        explore.AnchorRight = 0.5f;
        explore.AnchorTop = 0.70f;
        explore.AnchorBottom = 0.70f;
        explore.OffsetLeft = -112;
        explore.OffsetTop = -32;
        explore.OffsetRight = 112;
        explore.OffsetBottom = 32;
        explore.Pressed += () => ExplorationRequested?.Invoke();
        AddChild(explore);

        var back = make_button("返回世界地图", 184, 44);
        back.SetAnchorsPreset(LayoutPreset.TopRight);
        back.Position = new Vector2(-220, 28);
        back.Pressed += close;
        AddChild(back);
    }

    private static Label make_label(string text, int size, Color color)
    {
        var result = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        result.AddThemeFontSizeOverride("font_size", size);
        result.AddThemeColorOverride("font_color", color);
        return result;
    }

    private static Button make_button(string text, int width, int height)
    {
        var result = new Button { Text = text, CustomMinimumSize = new Vector2(width, height) };
        result.AddThemeFontSizeOverride("font_size", 17);
        result.AddThemeColorOverride("font_color", new Color("eadfca"));
        result.AddThemeStyleboxOverride("normal", panel_style(new Color(0.13f, 0.11f, 0.08f, 0.96f), new Color("75603b"), 6));
        result.AddThemeStyleboxOverride("hover", panel_style(new Color(0.24f, 0.18f, 0.1f, 0.98f), new Color("c09a52"), 6));
        return result;
    }

    private static Button make_scene_tag(string text)
    {
        var result = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(224, 64),
            Theme = ResourceLoader.Load<Theme>("res://resources/font/default_theme.tres"),
            FocusMode = Control.FocusModeEnum.None,
        };
        result.AddThemeFontSizeOverride("font_size", 21);
        result.AddThemeColorOverride("font_color", new Color("30291e"));
        result.AddThemeColorOverride("font_hover_color", new Color("211c15"));
        result.AddThemeStyleboxOverride("normal", tag_style(new Color("e4dbc4"), new Color("74694f")));
        result.AddThemeStyleboxOverride("hover", tag_style(new Color("f0e6ce"), new Color("a78c52")));
        result.AddThemeStyleboxOverride("pressed", tag_style(new Color("d4c8a8"), new Color("8c7444")));
        result.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        return result;
    }

    private static StyleBoxFlat tag_style(Color background, Color border)
    {
        var style = new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            ContentMarginLeft = 18,
            ContentMarginRight = 18,
            ContentMarginTop = 8,
            ContentMarginBottom = 8,
        };
        style.SetCornerRadiusAll(5);
        return style;
    }

    private static StyleBoxFlat panel_style(Color background, Color border, int radius)
    {
        var style = new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
        };
        style.SetCornerRadiusAll(radius);
        return style;
    }
}
