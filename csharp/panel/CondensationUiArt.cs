using Godot;

/// <summary>透明纸墨界面素材；各区域独立，不从整张截图带入纸面背景。</summary>
public static class CondensationUiArt
{
    private static Texture2D? _sheet, _icons;
    private static Texture2D? _card, _draw, _discard, _rest, _turn, _divider, _opening;
    private static readonly Texture2D?[] IconCache = new Texture2D?[6];
    private static Texture2D Read(string path)
    {
        if (ResourceLoader.Exists(path)) return GD.Load<Texture2D>(path);
        // 编辑器尚未刷新导入时也可立即打开测试入口。
        return ImageTexture.CreateFromImage(Image.LoadFromFile(path));
    }
    public static Texture2D Region(Rect2 rect) => new AtlasTexture
    {
        Atlas = _sheet ??= Read("res://textures/ui/cultivation/condensation_ui_kit_v3.png"), Region = rect, FilterClip = true
    };
    public static Texture2D Card => _card ??= Region(new Rect2(96, 66, 352, 498));
    public static Texture2D DrawPile => _draw ??= Region(new Rect2(588, 104, 358, 418));
    public static Texture2D DiscardPile => _discard ??= Region(new Rect2(1127, 130, 307, 391));
    public static Texture2D Rest => _rest ??= Region(new Rect2(35, 715, 470, 150));
    public static Texture2D Turn => _turn ??= Region(new Rect2(608, 626, 319, 321));
    public static Texture2D Divider => _divider ??= Region(new Rect2(1020, 747, 485, 64));
    public static Texture2D Opening => _opening ??= Read("res://textures/ui/cultivation/condensation_opening_panel_20261106.png");
    public static Texture2D Icon(int index) => IconCache[index] ??= new AtlasTexture
    {
        Atlas = _icons ??= Read("res://textures/ui/cultivation/condensation_ink_icons_v3.png"),
        Region = new Rect2(index % 3 * 512, index / 3 * 512, 512, 512), FilterClip = true
    };
}
