using Godot;

/// <summary>试玩手牌展示数据；费用、点数和效果均由战斗场景计算并提供。</summary>
public sealed record TrialTechniqueCardData(
    string SkillName,
    string Kind,
    int QueuePoints,
    int StaminaCost,
    int ManaCost,
    string LingjiRange,
    string Effect,
    string IconPath);

/// <summary>
/// 纸墨风的独立试玩手牌，仅负责显示与点击，不读取或改变技能规则。
/// 作为 Button 使用：订阅 Pressed，设置 Disabled、ToggleMode、ButtonPressed。
/// </summary>
public partial class TrialTechniqueCard : Button
{
    private static readonly Color Ink = new("332e28");
    private static readonly Color MutedInk = new("655849");
    private static readonly Color Paper = new("e8dcc4");
    private static readonly Color Edge = new("716759");
    private const string CardFont = "res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf";

    private TrialTechniqueCardData _data = new("", "", 0, 0, 0, "", "", "");
    private Panel? _background;
    private TextureRect? _illustration;

    public TrialTechniqueCard()
    {
        CustomMinimumSize = new Vector2(150, 210);
        MouseDefaultCursorShape = CursorShape.PointingHand;
        ToggleMode = true;
        Text = "";
    }

    public TrialTechniqueCard(TrialTechniqueCardData data) : this()
        => _data = data ?? throw new ArgumentNullException(nameof(data));

    public override void _Ready()
    {
        // 显示元素独立于 Button 的禁用主题色，结算时仍可阅读留存手牌。
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" })
            AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        ClipContents = true;

        // 朴素暖纸与细墨边：牌面不含装饰性金框。
        _background = new Panel
        {
            Name = "CardPaper",
            MouseFilter = MouseFilterEnum.Ignore
        };
        _background.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = Paper, BorderColor = Edge,
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3
        });
        AddChild(_background); Fill(_background, 0f, 0f, 1f, 1f);

        // 左上是真实资源消耗，右上是类别；队列容量单独位于牌底。
        var cost = _data.StaminaCost != 0 && _data.ManaCost != 0
            ? $"体 {_data.StaminaCost}  法 {_data.ManaCost}"
            : _data.StaminaCost != 0 ? $"体 {_data.StaminaCost}"
            : _data.ManaCost != 0 ? $"法 {_data.ManaCost}" : "无消耗";
        AddText("Cost", cost, 13, Ink, .07f, .045f, .63f, .145f, HorizontalAlignment.Left);
        AddText("Kind", _data.Kind, 13, MutedInk, .64f, .045f, .93f, .145f, HorizontalAlignment.Right);
        AddDivider(.15f);
        AddText("Name", _data.SkillName, 19, Ink, .09f, .17f, .91f, .275f);

        _illustration = new TextureRect
        {
            Name = "TechniqueArt",
            Texture = string.IsNullOrWhiteSpace(_data.IconPath) ? null : GD.Load<Texture2D>(_data.IconPath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(_illustration); Fill(_illustration, .10f, .30f, .90f, .58f);

        var details = string.IsNullOrWhiteSpace(_data.LingjiRange)
            ? _data.Effect
            : $"灵机 {_data.LingjiRange}\n{_data.Effect}";
        AddText("Details", details, 14, Ink, .07f, .60f, .93f, .78f);
        AddDivider(.825f);
        AddText("Capacity", $"占 {_data.QueuePoints}", 13, MutedInk, .08f, .84f, .92f, .96f);

        Toggled += _ => RefreshSelection();
        MouseEntered += RefreshSelection;
        MouseExited += RefreshSelection;
        RefreshSelection();
    }

    private void RefreshSelection()
    {
        if (_background is null || _illustration is null) return;
        // 选中压暗整张纸面，保留纹理；不染绿色、不增加不同材质边框。
        var shade = ButtonPressed ? .84f : Disabled ? .91f : IsHovered() ? 1.04f : 1f;
        _background.Modulate = new Color(shade, shade, shade, 1);
        _illustration.Modulate = new Color(shade, shade, shade, 1);
    }

    private void AddText(string name, string text, int size, Color color,
        float left, float top, float right, float bottom, HorizontalAlignment alignment = HorizontalAlignment.Center)
    {
        var label = new Label
        {
            Name = name,
            Text = text ?? "",
            HorizontalAlignment = alignment,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            ClipText = true,
            MouseFilter = MouseFilterEnum.Ignore
        };
        var font = GD.Load<FontFile>(CardFont);
        if (font is not null) label.AddThemeFontOverride("font", font);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        AddChild(label); Fill(label, left, top, right, bottom);
    }

    private void AddDivider(float top)
    {
        var line = new ColorRect
        {
            Color = new Color(.38f, .34f, .29f, .25f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(line); Fill(line, .07f, top, .93f, top);
        line.OffsetBottom = 1;
    }

    private static void Fill(Control child, float left, float top, float right, float bottom)
    {
        child.AnchorLeft = left; child.AnchorTop = top; child.AnchorRight = right; child.AnchorBottom = bottom;
        child.OffsetLeft = child.OffsetTop = child.OffsetRight = child.OffsetBottom = 0;
    }
}
