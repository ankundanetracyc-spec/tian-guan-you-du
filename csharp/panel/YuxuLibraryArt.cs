using Godot;

/// <summary>沿用已确认的无字框架和项目字体；新 PNG 尚未导入时可直接读取源图。</summary>
public static class YuxuLibraryArt
{
    public static readonly Color Ink = new("30382e"), Green = new("526f4f"), Red = new("925348"), Muted = new("68705e");
    public static Texture2D? Texture(string path)
    {
        if (ResourceLoader.Exists(path)) return GD.Load<Texture2D>(path);
        if (!Godot.FileAccess.FileExists(path)) return null;
        var image = Image.LoadFromFile(ProjectSettings.GlobalizePath(path));
        return image is null || image.IsEmpty() ? null : ImageTexture.CreateFromImage(image);
    }
    public static Font Font() => GD.Load<FontFile>("res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf");
    public static StyleBoxFlat Paper(bool selected = false) => new()
    {
        BgColor = new(selected ? "c7cfb5" : "eee5d4"), BorderColor = new("9a9179"),
        BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
        ContentMarginLeft = 6, ContentMarginRight = 6
    };
    public static void ButtonStyle(Button button, int size = 23, bool paper = true)
    {
        button.AddThemeFontOverride("font", Font()); button.AddThemeFontSizeOverride("font_size", size);
        foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" }) button.AddThemeColorOverride(state, Ink);
        button.AddThemeColorOverride("font_disabled_color", Muted);
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" })
            button.AddThemeStyleboxOverride(state, paper ? Paper(state == "pressed" || state == "hover") : new StyleBoxEmpty());
    }
}
