using Godot;

/// <summary>Match the source nine-slice corners to their intended screen size before stretching the center.</summary>
internal static class InventoryUiPaper
{
    private static readonly Dictionary<string, Texture2D> Textures = [];
    private static bool _cleanupRegistered;
    public static StyleBoxTexture Style(string file, int sourceMargin = 44, int displayMargin = 18)
    {
        if (!_cleanupRegistered && Engine.GetMainLoop() is SceneTree tree)
        {
            _cleanupRegistered = true;
            tree.Root.TreeExiting += () =>
            {
                foreach (var cached in Textures.Values) cached.Dispose();
                Textures.Clear(); _cleanupRegistered = false;
            };
        }
        var key = $"{file}:{sourceMargin}:{displayMargin}";
        if (!Textures.TryGetValue(key, out var texture))
        {
            var original = GD.Load<Texture2D>("res://textures/ui/inventory_index_20261029/" + file);
            using var image = original.GetImage();
            var factor = (float)displayMargin / sourceMargin;
            image.Resize(Mathf.Max(1, Mathf.RoundToInt(image.GetWidth() * factor)), Mathf.Max(1, Mathf.RoundToInt(image.GetHeight() * factor)), Image.Interpolation.Lanczos);
            texture = ImageTexture.CreateFromImage(image); Textures[key] = texture;
        }
        return new StyleBoxTexture
        {
            Texture = texture, TextureMarginLeft = displayMargin, TextureMarginRight = displayMargin,
            TextureMarginTop = displayMargin, TextureMarginBottom = displayMargin,
            ContentMarginLeft = 16, ContentMarginRight = 16, ContentMarginTop = 8, ContentMarginBottom = 8
        };
    }
}
