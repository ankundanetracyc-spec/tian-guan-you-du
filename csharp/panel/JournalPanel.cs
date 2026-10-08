using Godot;

/// <summary>
/// 单人模式的见闻簿。它把零散消息、亲历、人物与正在发生的事件放在同一个可回看的入口中。
/// </summary>
public partial class JournalPanel : Control
{
    public enum Section { Rumors, Journal, People, Events, Tasks }
    public sealed record Entry(string Title, string Body, string Source, string Place, string State, string? Time = null);

    private readonly Dictionary<Section, List<Entry>> _entries = new();
    private readonly Dictionary<Section, Button> _tabs = new();
    private VBoxContainer? _entryList;
    private Label? _detailTitle;
    private Label? _detailMeta;
    private Label? _detailBody;
    private Label? _sectionTitle;
    private TextureRect? _scroll;
    private Control? _pageContent;
    private Control? _sideTabs;
    private Control? _closeControl;
    private Section _current = Section.Rumors;
    private bool _closing;

    private static readonly Color Ink = new("2a251f");
    private static readonly Color MutedInk = new("665d4c");
    private static readonly Color Paper = new(0.96f, 0.93f, 0.84f, 0.94f);
    private static readonly Color WarmGold = new("a9823f");

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        ZIndex = 250;
        Build();
    }

    public void set_entries(Section section, IEnumerable<Entry> entries)
    {
        _entries[section] = entries.ToList();
        if (Visible && section == _current) Populate();
    }

    public void show_panel(Section section = Section.Rumors, string? selectedTitle = null)
    {
        _current = section;
        _closing = false;
        Visible = true;
        MoveToFront();
        Populate(selectedTitle);
        if (_scroll is null || _pageContent is null || _sideTabs is null || _closeControl is null) return;
        _scroll.Scale = new Vector2(.025f, 1f);
        _scroll.PivotOffset = _scroll.Size * .5f;
        _pageContent.Modulate = new Color(1, 1, 1, 0);
        _sideTabs.Modulate = new Color(1, 1, 1, 0);
        _closeControl.Modulate = new Color(1, 1, 1, 0);
        CallDeferred(nameof(play_open_animation));
    }

    public void hide_panel()
    {
        if (!Visible || _closing || _scroll is null || _pageContent is null || _sideTabs is null || _closeControl is null) return;
        _closing = true;
        _scroll.PivotOffset = _scroll.Size * .5f;
        var tween = CreateTween().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        tween.SetParallel();
        tween.TweenProperty(_scroll, "scale:x", .025f, .28f);
        tween.TweenProperty(_pageContent, "modulate:a", 0f, .12f);
        tween.TweenProperty(_sideTabs, "modulate:a", 0f, .12f);
        tween.TweenProperty(_closeControl, "modulate:a", 0f, .12f);
        tween.Chain().TweenCallback(Callable.From(finish_close));
    }

    private void Build()
    {
        var shade = new ColorRect { Color = new Color(0.02f, 0.025f, 0.02f, 0.62f), MouseFilter = MouseFilterEnum.Stop };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(shade);

        // 卷轴本身是独立透明素材；界面不再叠加第二层水墨背景。
        _scroll = new TextureRect { Texture = GD.Load<Texture2D>("res://textures/ui/qingxi/journal_scroll_slim_v2.png"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale };
        _scroll.SetAnchorsPreset(LayoutPreset.Center); _scroll.AnchorLeft = .075f; _scroll.AnchorTop = -.20f; _scroll.AnchorRight = .97f; _scroll.AnchorBottom = 1.20f;
        _scroll.MouseFilter = MouseFilterEnum.Ignore; AddChild(_scroll);

        var root = new MarginContainer(); _pageContent = root; root.SetAnchorsPreset(LayoutPreset.Center); root.AnchorLeft = .16f; root.AnchorTop = .13f; root.AnchorRight = .91f; root.AnchorBottom = .88f;
        root.AddThemeConstantOverride("margin_left", 22); root.AddThemeConstantOverride("margin_right", 22); root.AddThemeConstantOverride("margin_top", 16); root.AddThemeConstantOverride("margin_bottom", 16); AddChild(root);
        var vertical = new VBoxContainer(); vertical.AddThemeConstantOverride("separation", 0); root.AddChild(vertical);

        // 标签固定在左侧轴杆外，卷面只用于阅读内容。
        var navMargin = new MarginContainer(); _sideTabs = navMargin; navMargin.SetAnchorsPreset(LayoutPreset.Center); navMargin.AnchorLeft = .006f; navMargin.AnchorTop = .19f; navMargin.AnchorRight = .103f; navMargin.AnchorBottom = .84f; AddChild(navMargin);
        var nav = new VBoxContainer(); nav.AddThemeConstantOverride("separation", 11); navMargin.AddChild(nav);
        AddTab(nav, "传闻", Section.Rumors); AddTab(nav, "行记", Section.Journal); AddTab(nav, "人物", Section.People); AddTab(nav, "异动", Section.Events); AddTab(nav, "任务", Section.Tasks);
        var divider = new HSeparator(); divider.AddThemeConstantOverride("separation", 10); nav.AddChild(divider);
        foreach (var name in new[] { "地志", "势力", "妖录", "灵物", "旧闻" }) AddLockedTab(nav, name);

        // 右轴上系一条丝带作为收卷控制；避免用突兀的独立按钮破坏卷轴整体感。
        var close = new TextureButton { TextureNormal = GD.Load<Texture2D>("res://textures/ui/qingxi/journal_close_ribbon_v1.png"), IgnoreTextureSize = true, StretchMode = TextureButton.StretchModeEnum.Scale, TooltipText = "收卷" }; _closeControl = close;
        close.SetAnchorsPreset(LayoutPreset.Center); close.AnchorLeft = .887f; close.AnchorTop = .105f; close.AnchorRight = .94f; close.AnchorBottom = .31f;
        close.Pressed += hide_panel; AddChild(close);
        var closeLabel = MakeLabel("收卷", 17, Ink); closeLabel.HorizontalAlignment = HorizontalAlignment.Center; closeLabel.VerticalAlignment = VerticalAlignment.Center; closeLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); closeLabel.MouseFilter = MouseFilterEnum.Ignore; close.AddChild(closeLabel);

        var content = new HBoxContainer(); content.SizeFlagsVertical = SizeFlags.ExpandFill; content.AddThemeConstantOverride("separation", 16); vertical.AddChild(content);

        var listPanel = new PanelContainer { CustomMinimumSize = new Vector2(420, 0) }; StylePanel(listPanel, new Color(0.97f, .94f, .86f, .78f)); content.AddChild(listPanel);
        var listMargin = new MarginContainer(); listMargin.AddThemeConstantOverride("margin_left", 15); listMargin.AddThemeConstantOverride("margin_right", 15); listMargin.AddThemeConstantOverride("margin_top", 13); listMargin.AddThemeConstantOverride("margin_bottom", 13); listPanel.AddChild(listMargin);
        var listBox = new VBoxContainer(); listBox.AddThemeConstantOverride("separation", 8); listMargin.AddChild(listBox);
        _sectionTitle = MakeLabel("传闻", 24, Ink); listBox.AddChild(_sectionTitle);
        var scrollList = new ScrollContainer(); scrollList.SizeFlagsVertical = SizeFlags.ExpandFill; listBox.AddChild(scrollList);
        _entryList = new VBoxContainer(); _entryList.SizeFlagsHorizontal = SizeFlags.ExpandFill; _entryList.AddThemeConstantOverride("separation", 9); scrollList.AddChild(_entryList);

        var detailPanel = new PanelContainer(); detailPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill; StylePanel(detailPanel, new Color(0.98f, .95f, .88f, .86f)); content.AddChild(detailPanel);
        var detailMargin = new MarginContainer(); detailMargin.AddThemeConstantOverride("margin_left", 28); detailMargin.AddThemeConstantOverride("margin_right", 28); detailMargin.AddThemeConstantOverride("margin_top", 24); detailMargin.AddThemeConstantOverride("margin_bottom", 24); detailPanel.AddChild(detailMargin);
        var detailBox = new VBoxContainer(); detailBox.AddThemeConstantOverride("separation", 14); detailMargin.AddChild(detailBox);
        _detailTitle = MakeLabel("", 31, Ink); detailBox.AddChild(_detailTitle);
        _detailMeta = MakeLabel("", 17, MutedInk); _detailMeta.AutowrapMode = TextServer.AutowrapMode.WordSmart; detailBox.AddChild(_detailMeta);
        var line = new HSeparator(); detailBox.AddChild(line);
        _detailBody = MakeLabel("", 20, Ink); _detailBody.SizeFlagsVertical = SizeFlags.ExpandFill; _detailBody.AutowrapMode = TextServer.AutowrapMode.WordSmart; detailBox.AddChild(_detailBody);
    }

    private void AddTab(Container parent, string text, Section section)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(0, 68) }; StyleButton(button, false); button.Pressed += () => { _current = section; Populate(); };
        _tabs[section] = button; parent.AddChild(button);
    }

    private void AddLockedTab(Container parent, string text)
    {
        var button = new Button { Text = $"{text}  ·  待续", Disabled = false, CustomMinimumSize = new Vector2(0, 45) }; StyleButton(button, false); button.Modulate = new Color(.55f, .53f, .48f, .85f);
        button.Pressed += () => ShowReserved(text); parent.AddChild(button);
    }

    private void Populate(string? selectedTitle = null)
    {
        if (_entryList is null || _detailTitle is null || _detailMeta is null || _detailBody is null || _sectionTitle is null) return;
        foreach (var child in _entryList.GetChildren()) child.QueueFree();
        foreach (var pair in _tabs) StyleButton(pair.Value, pair.Key == _current);
        var title = _current switch { Section.Rumors => "传闻", Section.Journal => "行记", Section.People => "人物", Section.Events => "异动", _ => "任务" };
        _sectionTitle.Text = title;
        var entries = _entries.GetValueOrDefault(_current, []);
        if (entries.Count == 0) { ShowEmpty(title); return; }
        foreach (var entry in entries)
        {
            var button = new Button { Text = $"{entry.Title}\n{entry.Source}    {entry.State}", Alignment = HorizontalAlignment.Left, CustomMinimumSize = new Vector2(0, 74), TooltipText = entry.Body };
            button.AutowrapMode = TextServer.AutowrapMode.WordSmart; StyleEntry(button); button.Pressed += () => ShowEntry(entry); _entryList.AddChild(button);
        }
        ShowEntry(entries.FirstOrDefault(entry => entry.Title == selectedTitle) ?? entries[0]);
    }

    private void ShowEntry(Entry entry)
    {
        if (_detailTitle is null || _detailMeta is null || _detailBody is null) return;
        _detailTitle.Text = entry.Title;
        var time = string.IsNullOrWhiteSpace(entry.Time) ? "" : $"\n记于：{entry.Time}";
        _detailMeta.Text = $"来源：{entry.Source}\n地点：{entry.Place}\n当前进展：{entry.State}{time}";
        _detailBody.Text = entry.Body;
    }

    private void ShowEmpty(string title)
    {
        if (_detailTitle is null || _detailMeta is null || _detailBody is null) return;
        _detailTitle.Text = $"暂无{title}";
        _detailMeta.Text = "见闻会随你的经历与外界变化逐渐补全。";
        _detailBody.Text = "此处暂未收录新的内容。";
    }

    private void ShowReserved(string title)
    {
        _sectionTitle?.SetText(title);
        if (_entryList is not null) foreach (var child in _entryList.GetChildren()) child.QueueFree();
        if (_detailTitle is not null) _detailTitle.Text = $"{title} · 待续";
        if (_detailMeta is not null) _detailMeta.Text = "此栏入口已留出。";
        if (_detailBody is not null) _detailBody.Text = "随着游历展开，这里会逐步收录相关见闻。";
    }

    private void play_open_animation()
    {
        if (_scroll is null || _pageContent is null || _sideTabs is null || _closeControl is null || !Visible || _closing) return;
        _scroll.PivotOffset = _scroll.Size * .5f;
        var tween = CreateTween().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_scroll, "scale:x", 1f, .32f);
        tween.TweenInterval(.08f);
        tween.SetParallel();
        tween.TweenProperty(_pageContent, "modulate:a", 1f, .16f);
        tween.TweenProperty(_sideTabs, "modulate:a", 1f, .16f);
        tween.TweenProperty(_closeControl, "modulate:a", 1f, .16f);
    }

    private void finish_close()
    {
        Hide();
        _closing = false;
    }

    private static Label MakeLabel(string text, int size, Color color)
    {
        var label = new Label { Text = text, ThemeTypeVariation = "Label", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static void StylePanel(PanelContainer panel, Color color)
    {
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = color, BorderColor = new Color("82755c"), BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1, CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12, CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12 });
    }

    private static void StyleButton(Button button, bool selected)
    {
        button.AddThemeFontSizeOverride("font_size", 24); button.AddThemeColorOverride("font_color", Ink);
        button.Modulate = selected ? new Color(1.08f, .91f, .62f, 1f) : Colors.White;
        button.AddThemeStyleboxOverride("normal", PaperStyle());
        button.AddThemeStyleboxOverride("hover", PaperStyle());
    }

    private static StyleBoxTexture PaperStyle() => new() { Texture = GD.Load<Texture2D>("res://textures/ui/qingxi/journal_tab_paper_v2.png"), TextureMarginLeft = 34, TextureMarginRight = 34, TextureMarginTop = 20, TextureMarginBottom = 20 };

    private static void StyleEntry(Button button)
    {
        button.AddThemeFontSizeOverride("font_size", 18); button.AddThemeColorOverride("font_color", Ink);
        button.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = new Color(1f, .98f, .91f, .56f), BorderColor = new Color(.55f, .49f, .36f, .5f), BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1, CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8, ContentMarginLeft = 12, ContentMarginTop = 8 });
        button.AddThemeStyleboxOverride("hover", new StyleBoxFlat { BgColor = new Color("f1e3b5"), CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8, ContentMarginLeft = 12, ContentMarginTop = 8 });
    }
}
