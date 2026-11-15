using Godot;

/// <summary>共用纸面按钮；文字、字号与明暗可配置，交互状态保持一致。</summary>
public partial class PaperButton : Button
{
    public static PaperButton Create(string text, int fontSize = 27, bool light = true)
    {
        var button = new PaperButton { Text = text };
        ApplyStyle(button, fontSize, light);
        return button;
    }

    public static void ApplyStyle(Button button, int fontSize, bool light)
    {
        var ink = new Color("252920"); var paper = new Color("f0ead7");
        button.MouseDefaultCursorShape = CursorShape.PointingHand;
        button.AddThemeFontOverride("font", GD.Load<FontFile>("res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf"));
        button.AddThemeFontSizeOverride("font_size", fontSize);
        foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" })
            button.AddThemeColorOverride(state, light ? ink : paper);
        button.AddThemeColorOverride("font_disabled_color", new Color("827a64"));
        button.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
        button.AddThemeColorOverride("font_outline_color", Colors.Transparent);
        button.AddThemeConstantOverride("outline_size", 0);
        foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled" })
        {
            if (light) button.AddThemeStyleboxOverride(state, WorldHudStyle.Paper(state is "hover" or "hover_pressed"));
            else button.AddThemeStyleboxOverride(state, new StyleBoxFlat
            {
                BgColor = new Color(.08f, .09f, .07f, .84f),
                BorderColor = new Color(state is "hover" or "hover_pressed" ? "d4bc87" : "b6aa8a"),
                BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
                ContentMarginLeft = 12, ContentMarginRight = 12,
                CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4
            });
        }
        button.AddThemeStyleboxOverride("focus", new StyleBoxFlat
        {
            BgColor = Colors.Transparent, BorderColor = new Color("6e8a76"),
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1
        });
        button.MouseEntered += () => { if (!button.Disabled) button.Modulate = new Color(1.07f, 1.05f, 1.01f); };
        button.MouseExited += () => button.Modulate = Colors.White;
    }
}
