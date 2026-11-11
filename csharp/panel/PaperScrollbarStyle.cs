using Godot;

/// <summary>Fixed-size scrollbar caps and a plain stretchable shaft, assembled from the approved atlas in memory.</summary>
public static class PaperScrollbarStyle
{
    private static readonly Dictionary<bool, Texture2D> Parts = [];
    public static StyleBoxTexture Create(bool grabber = false, bool highlighted = false, bool pressed = false) => new()
    {
        Texture = Part(grabber),
        TextureMarginLeft = 3, TextureMarginRight = 3, TextureMarginTop = 6, TextureMarginBottom = 6,
        ContentMarginLeft = 6, ContentMarginRight = 7, ContentMarginTop = grabber ? 12 : 0, ContentMarginBottom = grabber ? 12 : 0,
        ModulateColor = pressed ? new Color(.90f, .94f, .90f) : highlighted ? new Color(1.08f, 1.10f, 1.05f) : Colors.White
    };
    private static Texture2D Part(bool grabber)
    {
        if (Parts.TryGetValue(grabber, out var existing)) return existing;
        using var source = CraftingPaperStyle.Sprite(grabber ? "scrollThumb" : "scrollTrack").GetImage();
        if (source.IsCompressed()) source.Decompress();
        source.Convert(Image.Format.Rgba8);
        int cap = grabber ? 22 : 28;
        // Do not stretch the thumb's grip drawing; preserve plain wood from above it. Original PNGs stay intact.
        int shaft = grabber ? 28 : source.GetHeight() / 2;
        using var result = Image.CreateEmpty(14, 13, false, Image.Format.Rgba8);
        foreach (var (sourceY, sourceHeight, targetY, targetHeight) in new[]
        { (0, cap, 0, 6), (shaft, 1, 6, 1), (source.GetHeight() - cap, cap, 7, 6) })
        {
            using var patch = source.GetRegion(new Rect2I(0, sourceY, source.GetWidth(), sourceHeight));
            patch.Resize(14, targetHeight, Image.Interpolation.Lanczos);
            result.BlitRect(patch, new Rect2I(0, 0, 14, targetHeight), new Vector2I(0, targetY));
        }
        var pixels = result.GetData();
        for (int i = 0; i < pixels.Length; i += 4)
            pixels[i + 3] = (byte)Math.Clamp((int)Math.Round((pixels[i + 3] - 8) * 255d / 247), 0, 255);
        using var clean = Image.CreateFromData(14, 13, false, Image.Format.Rgba8, pixels);
        var texture = ImageTexture.CreateFromImage(clean);
        texture.ResourceName = grabber ? "PaperScrollbar/fixed-cap-thumb" : "PaperScrollbar/fixed-cap-track";
        return Parts[grabber] = texture;
    }
}
