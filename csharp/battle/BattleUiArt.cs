using Godot;

/// <summary>战斗界面统一使用已确认的灰墨青／米赭独立素材。</summary>
internal static class BattleUiArt
{
    internal const string Directory = "res://textures/ui/combat/queue_ui_20261009_muted_v2/";
    internal static readonly Vector2 Canvas = new(1672, 941);
    internal static readonly Color Text = new("f4f0df"), Accent = new("c4b58c"), Warning = new("b56854");
    private static readonly Dictionary<string, Texture2D> Textures = [];

    internal static void ReleaseGeneratedStyles()
    {
        foreach (var key in Textures.Keys.Where(key => key.Contains(':') && !key.StartsWith("res://", StringComparison.Ordinal)).ToArray())
        { Textures[key].Dispose(); Textures.Remove(key); }
    }

    internal static StyleBoxTexture PaperBarStyle()
    {
        var atlas = new AtlasTexture
        {
            Atlas = Texture("res://textures/ui/combat/combat_skill_bar_paper_20261012.png"),
            Region = new Rect2(38,176,2096,390)
        };
        var style = new StyleBoxTexture { Texture = atlas };
        style.SetTextureMargin(Side.Left,64); style.SetTextureMargin(Side.Right,64);
        style.SetTextureMargin(Side.Top,40); style.SetTextureMargin(Side.Bottom,40);
        return style;
    }

    internal static Texture2D Texture(string name)
    {
        var path = name.StartsWith("res://") ? name : $"{Directory}{name}_v2.png";
        if (Textures.TryGetValue(path, out var cached)) return cached;
        Texture2D? texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
        // 新素材尚未被编辑器导入时，测试入口仍可从 PNG 直接加载。
        if (texture is null)
        {
            using var image = Image.LoadFromFile(ProjectSettings.GlobalizePath(path));
            texture = ImageTexture.CreateFromImage(image);
        }
        Textures[path] = texture;
        return texture;
    }

    internal static StyleBoxTexture Style(string name, Vector2 size, int edge = 14, int vertical = 12)
    {
        var key = $"{name}:{size}";
        if (!Textures.TryGetValue(key, out var texture))
        {
            using var image = Texture(name).GetImage();
            image.Resize((int)size.X, (int)size.Y, Image.Interpolation.Lanczos);
            texture = ImageTexture.CreateFromImage(image);
            Textures[key] = texture;
        }
        var style = new StyleBoxTexture { Texture = texture };
        foreach (var side in new[] { Side.Left, Side.Right }) style.SetTextureMargin(side, edge);
        foreach (var side in new[] { Side.Top, Side.Bottom }) style.SetTextureMargin(side, vertical);
        foreach (var side in new[] { Side.Left, Side.Top, Side.Right, Side.Bottom }) style.SetContentMargin(side, 0);
        return style;
    }

    internal static TextureRect Icon(string name, Vector2 size) => new()
    {
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        MouseFilter = Control.MouseFilterEnum.Ignore,
        Texture = Texture(name), CustomMinimumSize = size, Size = size
    };

    internal static void Skin(Button button)
    {
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" })
        {
            var style = Style("control_button", new(140, 40));
            style.SetContentMargin(Side.Left, 6); style.SetContentMargin(Side.Right, 6);
            style.SetContentMargin(Side.Top, 4); style.SetContentMargin(Side.Bottom, 4);
            button.AddThemeStyleboxOverride(state, style);
        }
        button.AddThemeColorOverride("font_color", Text);
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        button.AddThemeColorOverride("font_pressed_color", Accent);
        button.AddThemeColorOverride("font_disabled_color", new Color(.53f, .57f, .52f));
        button.AddThemeFontSizeOverride("font_size", 16);
        button.FocusMode = Control.FocusModeEnum.None;
    }

    internal static float Scale(Viewport viewport) => Mathf.Min(viewport.GetVisibleRect().Size.X / Canvas.X, viewport.GetVisibleRect().Size.Y / Canvas.Y);
    internal static Vector2 Origin(Viewport viewport) => new((viewport.GetVisibleRect().Size.X - Canvas.X * Scale(viewport)) * .5f, 0);
    internal static void Fit(Control root)
    {
        root.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        root.Size = Canvas;
        root.Scale = Vector2.One * Scale(root.GetViewport());
        root.Position = Origin(root.GetViewport());
    }

    internal static void ClearChildren(Node node)
    {
        foreach (var child in node.GetChildren()) { node.RemoveChild(child); child.QueueFree(); }
    }
}
