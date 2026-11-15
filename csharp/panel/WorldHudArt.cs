using Godot;

/// <summary>Shared, text-free ink assets. Game text and hit areas remain native controls.</summary>
public static class WorldHudArt
{
    private const string Root = "res://textures/ui/world_ink_20261104/";
    public static Texture2D Texture(string name) => GD.Load<Texture2D>(Root + name + ".tres");

    public static StyleBoxTexture Surface(bool small = false, bool border = true) => new()
    {
        Texture = Texture(border ? (small ? "paper_tab" : "paper_panel") : "paper_inner"),
        TextureMarginLeft = border ? 30 : 12, TextureMarginRight = border ? 30 : 12,
        TextureMarginTop = border ? 18 : 8, TextureMarginBottom = border ? (small ? 26 : 64) : 12,
        ContentMarginLeft = 8, ContentMarginRight = 8,
        ContentMarginTop = 3, ContentMarginBottom = 3
    };

    public static TextureRect Mark(string texture, Vector2 position, Vector2 size, Color? tint = null) => new()
    {
        Texture = Texture(texture), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = Control.MouseFilterEnum.Ignore,
        Modulate = tint ?? Colors.White, Position = position, Size = size
    };

    public static void AddLabelPaper(Label label, bool brush = false)
    {
        label.AddThemeStyleboxOverride("normal", new StyleBoxEmpty
        {
            ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 3, ContentMarginBottom = 3
        });
        var frame = new WorldHudFrame { Name = "InkLabelPaper", InkBrush = brush, ShowBehindParent = true };
        label.AddChild(frame);
        frame.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
    }
}
