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
    internal static readonly Vector2 CardSize = new(144, 196);
    private static readonly Color Ink = new("2e3027"), MutedInk = new("585746");
    private static readonly Color Paper = new("efe7d6"), Edge = new("49493c");
    private static Font? _font;

    private TrialTechniqueCardData _data = new("", "", 0, 0, 0, "", "", "");
    private Panel? _background;
    private TextureRect? _paper;
    private TextureRect? _illustration;
    private Panel? _focusRing;

    internal BoardCardKey? HandKey { get; init; }
    internal event Action<BoardCardKey>? DragStarted;

    public TrialTechniqueCard()
    {
        CustomMinimumSize = CardSize;
        Size = CardSize;
        PivotOffset = new Vector2(CardSize.X / 2f, CardSize.Y * .9f);
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

        _background = new Panel { Name = "CardPaper", MouseFilter = MouseFilterEnum.Ignore };
        _background.AddThemeStyleboxOverride("panel", TrialBattleArt.Frame(Paper, Edge, 4, 4));
        AddChild(_background);
        Fill(_background, 0f, 0f, 1f, 1f);
        _paper = new TextureRect
        {
            Texture = TrialBattleArt.Paper,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(_paper);
        Fill(_paper, .022f, .017f, .978f, .983f);
        AddPlate(.035f, .026f, .965f, .974f, Colors.Transparent, new Color("ab9e80", .7f));

        var dualCost = _data.StaminaCost != 0 && _data.ManaCost != 0;
        var cost = dualCost
            ? $"体 {_data.StaminaCost}  法 {_data.ManaCost}"
            : _data.StaminaCost != 0 ? $"体 {_data.StaminaCost}"
            : _data.ManaCost != 0 ? $"法 {_data.ManaCost}" : "无消耗";
        var seal = _data.Kind switch
        {
            "近攻" => new Color("754c40"),
            "远攻" => new Color("425b54"),
            "运转" => new Color("4b5763"),
            _ => MutedInk
        };
        var costEdge = dualCost ? .63f : .44f;
        AddPlate(.07f, .051f, costEdge, .151f, new Color(Ink, .075f), new Color(Edge, .32f));
        AddText("Cost", cost, 14, Ink, .103f, .041f, costEdge - .018f, .16f, HorizontalAlignment.Left);
        AddPlate(.665f, .051f, .93f, .151f, new Color(seal, .10f), new Color(seal, .48f));
        AddText("Kind", _data.Kind, 12, seal, .665f, .051f, .93f, .151f);
        AddDivider(.176f);
        AddText("Name", _data.SkillName, 21, Ink, .07f, .178f, .93f, .320f);

        var artWindow = AddPlate(.070f, .322f, .930f, .664f, new Color("726e54", .07f), new Color("91876d", .34f));
        artWindow.Draw += () =>
        {
            var corner = new Color(Edge, .68f);
            var span = artWindow.Size;
            artWindow.DrawPolyline([new(0.5f, 8), new(0.5f, 0.5f), new(12, 0.5f)], corner, 1f, true);
            artWindow.DrawPolyline([span - new Vector2(.5f, 8), span - new Vector2(.5f, .5f), span - new Vector2(12, .5f)], corner, 1f, true);
        };

        _illustration = new TextureRect
        {
            Name = "TechniqueArt",
            Texture = string.IsNullOrWhiteSpace(_data.IconPath) ? null : GD.Load<Texture2D>(_data.IconPath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(_illustration);
        Fill(_illustration, .085f, .328f, .915f, .658f);

        var details = string.IsNullOrWhiteSpace(_data.LingjiRange)
            ? _data.Effect
            : $"灵机 {_data.LingjiRange}\n{_data.Effect}";
        AddText("Details", details, 13, Ink, .07f, .668f, .93f, .853f);
        AddDivider(.865f);
        AddText("Capacity", $"占 {_data.QueuePoints}", 12, MutedInk, .10f, .868f, .90f, .978f);

        _focusRing = AddPlate(.008f, .008f, .992f, .992f, Colors.Transparent, new Color("a59365"));
        _focusRing.Visible = HasFocus();
        FocusEntered += () => _focusRing.Visible = true;
        FocusExited += () => _focusRing.Visible = false;

        Toggled += _ => RefreshSelection();
        MouseEntered += RefreshSelection;
        MouseExited += RefreshSelection;
        RefreshSelection();
    }

    internal void RefreshSelection()
    {
        if (_background is null || _illustration is null) return;
        var shade = ButtonPressed ? CraftingPaperStyle.SelectedDarkening : Disabled ? .91f : IsHovered() ? 1.035f : 1f;
        _background.Modulate = new Color(shade, shade, shade, 1);
        if (_paper is not null) _paper.Modulate = _background.Modulate;
        _illustration.Modulate = _background.Modulate;
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (Disabled || HandKey is not { } key) return default;
        var preview = new Control { MouseFilter = MouseFilterEnum.Ignore };
        preview.AddChild(new TrialTechniqueCard(_data)
        {
            Disabled = true, MouseFilter = MouseFilterEnum.Ignore,
            Position = new Vector2(-CardSize.X * .38f, -CardSize.Y * .26f)
        });
        SetDragPreview(preview);
        DragStarted?.Invoke(key);
        return this;
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
        var font = _font ??= GD.Load<Font>(CraftingPaperStyle.FontPath);
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

    private Panel AddPlate(float left, float top, float right, float bottom, Color fill, Color stroke)
    {
        var plate = new Panel { MouseFilter = MouseFilterEnum.Ignore };
        plate.AddThemeStyleboxOverride("panel", TrialBattleArt.Frame(fill, stroke, 2));
        AddChild(plate);
        Fill(plate, left, top, right, bottom);
        return plate;
    }

    private static void Fill(Control child, float left, float top, float right, float bottom)
    {
        child.AnchorLeft = left; child.AnchorTop = top; child.AnchorRight = right; child.AnchorBottom = bottom;
        child.OffsetLeft = child.OffsetTop = child.OffsetRight = child.OffsetBottom = 0;
    }
}
