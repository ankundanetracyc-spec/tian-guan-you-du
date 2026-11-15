using Godot;

internal static class TrialBattleArt
{
    private const string AssetRoot = "res://textures/ui/combat/clash_trial_20261115/";
    private static Texture2D? _paper;
    private static Texture2D? _inkCloth;

    internal static Texture2D? Paper => _paper ??= Texture("xuan_paper.png");
    internal static Texture2D? InkCloth => _inkCloth ??= Texture("ink_cloth.png");

    private static Texture2D? Texture(string file)
    {
        var path = AssetRoot + file;
        if (ResourceLoader.Exists(path)) return GD.Load<Texture2D>(path);
        if (!Godot.FileAccess.FileExists(path)) return null;
        using var image = Image.LoadFromFile(path);
        return image is null || image.IsEmpty() ? null : ImageTexture.CreateFromImage(image);
    }

    internal static StyleBoxFlat Frame(Color fill, Color stroke, int radius = 4, int shadow = 0) => new()
    {
        BgColor = fill, BorderColor = stroke,
        BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
        CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
        ShadowSize = shadow, ShadowColor = new Color(0, 0, 0, .32f), ShadowOffset = new Vector2(0, 3)
    };
}
