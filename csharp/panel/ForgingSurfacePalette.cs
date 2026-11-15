using Godot;

/// <summary>炼器页面的低亮度纸面分层；复用原材质、九宫格和边距，不染文字或物品。</summary>
public static class ForgingSurfacePalette
{
    private static readonly Color PanelTone = new("d0d1c5");
    private static readonly Color MaterialTone = new("b8cbbd");
    private static readonly Color ControlTone = new("c3c4b7");
    // 原数量框是暖黄纸，偏冷的乘色将其压为灰棕，避免继续叠黄。
    private static readonly Color InputTone = new("c2c9dd");

    private static StyleBoxTexture Shade(StyleBoxTexture style, Color tone)
    {
        style.ModulateColor = new Color(tone.R, tone.G, tone.B, style.ModulateColor.A);
        return style;
    }

    public static StyleBoxTexture PanelPaper(bool tall = false, float alpha = 1, int padding = 8, bool plain = false)
        => Shade(ForgingInkStyle.Paper(tall, alpha, padding: padding, plain: plain), PanelTone);

    public static StyleBoxTexture MaterialCard()
        => Shade(ForgingInkStyle.Paper(padding: 8, plain: true), MaterialTone);

    public static void Button(Button button, bool primary = false)
    {
        ForgingInkStyle.Button(button, primary);
        if (primary) return;
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled" })
        {
            if (button.GetThemeStylebox(state) is not StyleBoxTexture original) continue;
            var copy = (StyleBoxTexture)original.Duplicate();
            var tone = state == "hover" ? ControlTone.Lerp(MaterialTone, .55f)
                : state == "pressed" ? ControlTone.Darkened(.08f) : ControlTone;
            button.AddThemeStyleboxOverride(state, Shade(copy, tone));
        }
    }

    public static void ConfigureTheme(Theme theme)
    {
        foreach (var name in new[] { "normal", "focus", "read_only" })
        {
            var original = FindStyle(theme, name, "LineEdit");
            if (original is null) continue;
            var style = original.Duplicate() as StyleBox;
            if (style is StyleBoxTexture texture) Shade(texture, InputTone);
            else if (style is StyleBoxFlat flat && flat.BgColor.A > 0)
                flat.BgColor = new Color(.70f, .68f, .60f, flat.BgColor.A);
            if (style is not null) theme.SetStylebox(name, "LineEdit", style);
        }
        if (FindStyle(theme, "panel", "PopupMenu") is StyleBoxTexture popup)
            theme.SetStylebox("panel", "PopupMenu", Shade((StyleBoxTexture)popup.Duplicate(), PanelTone));

        foreach (var type in new[] { "VScrollBar", "HScrollBar" })
        foreach (var name in new[] { "grabber", "grabber_highlight", "grabber_pressed" })
        {
            var original = FindStyle(theme, name, type) ?? FindStyle(theme, name, "ScrollBar");
            if (original is null) continue;
            var copy = original.Duplicate() as StyleBox;
            var tone = name == "grabber" ? new Color("687a6d") : new Color("4f6958");
            if (copy is StyleBoxFlat flat) flat.BgColor = new Color(tone.R, tone.G, tone.B, flat.BgColor.A);
            else if (copy is StyleBoxTexture texture) Shade(texture, new Color(.62f, .70f, .64f));
            if (copy is not null) theme.SetStylebox(name, type, copy);
        }
    }

    private static StyleBox? FindStyle(Theme theme, string name, string type)
    {
        if (theme.HasStylebox(name, type)) return theme.GetStylebox(name, type);
        var fallback = ThemeDB.GetDefaultTheme();
        return fallback.HasStylebox(name, type) ? fallback.GetStylebox(name, type) : null;
    }
}
