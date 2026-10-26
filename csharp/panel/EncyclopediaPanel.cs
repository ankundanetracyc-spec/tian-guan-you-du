using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

/// <summary>修行百科：展示战斗状态、物品与生灵资料，可在未载入存档时查阅。</summary>
public partial class EncyclopediaPanel : Control
{
    private enum Section { Statuses, Items, Beasts, Terms }
    private sealed record Entry(string Title, string Meta, string Body, string SearchText, Texture2D? Icon = null, bool IsNegative = false, string? PreviewSpinePath = null, BeastTemplate? Beast = null, ItemData? Item = null, StatusDetail? Status = null, CreatureRecord? Creature = null, string? PortraitPath = null);
    private sealed record StatRange(string Name, string Minimum, string Mean, string Maximum);
    private sealed record StatusDetail(string Category, string? StackLimit, string? Source, string Effect);
    private sealed class CreatureRecord
    {
        public CreatureRecord() { }
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public string Realm { get; set; } = "";
        public string Stage { get; set; } = "";
        public string HumanParity { get; set; } = "";
        public string Habitat { get; set; } = "";
        public string Habit { get; set; } = "";
        public string BasicAttack { get; set; } = "";
        public string Talent { get; set; } = "";
        public string Technique { get; set; } = "";
        public string Portrait { get; set; } = "";
        public CreatureStats? Stats { get; set; }
        public float? PhysicalDefense { get; set; }
    }
    private sealed class CreatureStats
    {
        public CreatureStats() { }
        public CreatureStat? Physique { get; set; }
        public CreatureStat? Intelligence { get; set; }
        public CreatureStat? Soul { get; set; }
    }
    private sealed class CreatureStat
    {
        public CreatureStat() { }
        public float Min { get; set; }
        public float Mean { get; set; }
        public float Max { get; set; }
    }

    private readonly Dictionary<Section, List<Entry>> _entries = [];
    private readonly Dictionary<Section, Button> _tabs = [];
    private readonly Dictionary<Entry, Button> _entryButtons = [];
    private VBoxContainer? _entryList;
    private Label? _detailTitle;
    private Label? _detailMeta;
    private Label? _detailBody;
    private TextureRect? _detailIcon;
    private PanelContainer? _detailIconPlate;
    private Label? _entryCount;
    private LineEdit? _search;
    private TextureRect? _background;
    private Control? _pageContent;
    private ScrollContainer? _detailScroll;
    private VBoxContainer? _itemDetailContent;
    private VBoxContainer? _statusDetailContent;
    private HBoxContainer? _beastDetail;
    private VBoxContainer? _beastDossier;
    private PanelContainer? _beastPreviewPanel;
    private SubViewport? _beastPreviewViewport;
    private Node2D? _beastPreviewSpine;
    private Sprite2D? _beastPreviewFallback;
    private Label? _beastPreviewStatus;
    private Button? _beastRuntimeButton;
    private HBoxContainer? _beastAnimationControls;
    private readonly Dictionary<string, Button> _beastAnimationButtons = [];
    private string _beastPreviewResourcePath = "";
    private string _beastPreviewAnimation = "idle";
    private Section _current = Section.Statuses;
    private bool _closing;
    private bool _suppressSearchChanged;
    private Entry? _selectedEntry;
    private readonly Dictionary<Section, Entry> _selectedEntries = [];
    private Tween? _presentationTween;

    private static readonly Color Ink = new("2a251f");
    private static readonly Color MutedInk = new("665d4c");

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        ZIndex = 250;
        Build();
        VisibilityChanged += () => { if (!IsVisibleInTree()) CancelCatalogRequest(); };
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible || @event is not InputEventKey { Pressed: true, Echo: false } key || key.Keycode != Key.Escape) return;
        hide_panel();
        GetViewport().SetInputAsHandled();
    }

    public void show_panel()
    {
        ShowSection(Section.Statuses);
    }

    public void show_beasts()
    {
        ShowSection(Section.Beasts);
    }

    private async void ShowSection(Section section, bool resetSearch = true, bool animate = true)
    {
        if (!resetSearch && _current == section && IsCatalogLoading && Visible && !_closing) return;
        var request = ++_catalogRequest;
        _presentationTween?.Kill();
        _current = section;
        _closing = false;
        if (resetSearch && _search is not null && !string.IsNullOrEmpty(_search.Text))
        {
            _suppressSearchChanged = true;
            _search.Clear();
            _suppressSearchChanged = false;
        }
        Visible = true;
        MoveToFront();
        if (animate && _background is not null && _pageContent is not null)
        {
            _background.Modulate = new Color(1, 1, 1, 0);
            _pageContent.Modulate = new Color(1, 1, 1, 0);
            CallDeferred(nameof(play_open_animation));
        }
        else
        {
            if (_background is not null) _background.Modulate = Colors.White;
            if (_pageContent is not null) _pageContent.Modulate = Colors.White;
        }
        try
        {
            await EnsureCatalogLoadedAsync(section, request);
            if (!IsCatalogRequestCurrent(request)) return;
            Populate();
            if (_selectedEntry is not null) SetBeastPreview(_selectedEntry, _detailIcon?.Texture);
        }
        catch (Exception exception)
        {
            if (!IsCatalogRequestCurrent(request)) return;
            GD.PushError($"百科目录载入失败：{exception}");
            if (_detailTitle is not null) _detailTitle.Text = "目录暂时未能载入";
            if (_detailMeta is not null) _detailMeta.Text = "可切换分类后重试。";
        }
        finally { if (request == _catalogRequest) IsCatalogLoading = false; }
    }

    public void hide_panel()
    {
        if (!Visible || _closing || _background is null || _pageContent is null) return;
        CancelCatalogRequest();
        _closing = true;
        _presentationTween?.Kill();
        var tween = _presentationTween = CreateTween().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        tween.SetParallel();
        tween.TweenProperty(_background, "modulate:a", 0f, .16f);
        tween.TweenProperty(_pageContent, "modulate:a", 0f, .12f);
        tween.Chain().TweenCallback(Callable.From(finish_close));
    }

    private void Build()
    {
        _background = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://textures/ui/encyclopedia/encyclopedia_paper_fullscreen_v1.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Stop
        };
        _background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_background);

        var page = new Control();
        _pageContent = page;
        page.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(page);

        var root = new MarginContainer();
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        root.AnchorLeft = .012f; root.AnchorTop = .065f; root.AnchorRight = .988f; root.AnchorBottom = .985f;
        root.OffsetTop = 28;
        root.AddThemeConstantOverride("margin_left", 16); root.AddThemeConstantOverride("margin_right", 16);
        root.AddThemeConstantOverride("margin_top", 0); root.AddThemeConstantOverride("margin_bottom", 22);
        page.AddChild(root);

        var content = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 20);
        root.AddChild(content);

        var categoryArea = new CenterContainer { Name = "Categories" };
        categoryArea.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        categoryArea.AnchorLeft = .25f; categoryArea.AnchorTop = .03f; categoryArea.AnchorRight = .75f; categoryArea.AnchorBottom = .10f;
        page.AddChild(categoryArea);
        var categoryRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        categoryRow.AddThemeConstantOverride("separation", 8);
        categoryArea.AddChild(categoryRow);
        AddTab(categoryRow, "状态", Section.Statuses);
        AddTab(categoryRow, "物品", Section.Items);
        AddTab(categoryRow, "生灵", Section.Beasts);
        AddTab(categoryRow, "术语介绍", Section.Terms);

        var topRight = new HBoxContainer();
        topRight.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        topRight.AnchorLeft = .82f; topRight.AnchorTop = .03f; topRight.AnchorRight = .98f; topRight.AnchorBottom = .10f;
        topRight.AddThemeConstantOverride("separation", 8);
        page.AddChild(topRight);
        topRight.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _entryCount = MakeLabel("收录 0 条", 18, MutedInk);
        _entryCount.CustomMinimumSize = new Vector2(150, 0);
        _entryCount.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        _entryCount.AutowrapMode = TextServer.AutowrapMode.Off;
        _entryCount.HorizontalAlignment = HorizontalAlignment.Right;
        _entryCount.VerticalAlignment = VerticalAlignment.Center;
        topRight.AddChild(_entryCount);
        var closeIcon = GD.Load<Texture2D>("res://textures/ui/encyclopedia/encyclopedia_close_cross_v1.png");
        var close = new TextureButton
        {
            Name = "CloseEncyclopedia",
            TextureNormal = closeIcon,
            TextureHover = closeIcon,
            TexturePressed = closeIcon,
            IgnoreTextureSize = true,
            StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(56, 56),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            FocusMode = FocusModeEnum.None,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            TooltipText = ""
        };
        close.MouseEntered += () => close.Modulate = new Color(1.12f, .96f, .78f, 1f);
        close.MouseExited += () => close.Modulate = Colors.White;
        close.Pressed += hide_panel;
        topRight.AddChild(close);

        var listPanel = new PanelContainer
        {
            Name = "Catalog", CustomMinimumSize = new Vector2(320, 0),
            SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = .20f
        };
        StylePanel(listPanel, new Color(.97f, .94f, .86f, .78f));
        content.AddChild(listPanel);
        var listMargin = new MarginContainer();
        listMargin.AddThemeConstantOverride("margin_left", 16); listMargin.AddThemeConstantOverride("margin_right", 16);
        listMargin.AddThemeConstantOverride("margin_top", 14); listMargin.AddThemeConstantOverride("margin_bottom", 14);
        listPanel.AddChild(listMargin);
        var listBox = new VBoxContainer();
        listBox.AddThemeConstantOverride("separation", 11);
        listMargin.AddChild(listBox);

        _search = new LineEdit { Name = "CatalogSearch", PlaceholderText = "搜索名称、来源或说明…", ClearButtonEnabled = true, CustomMinimumSize = new Vector2(0, 48) };
        _search.AddThemeFontSizeOverride("font_size", 18);
        _search.AddThemeColorOverride("font_color", Ink);
        _search.AddThemeColorOverride("font_placeholder_color", MutedInk);
        _search.AddThemeColorOverride("caret_color", Ink);
        _search.AddThemeStyleboxOverride("normal", new StyleBoxFlat
        {
            BgColor = new Color(1f, .98f, .92f, .76f), BorderColor = new Color("a8997b"),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
            ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 8, ContentMarginBottom = 8
        });
        _search.TextChanged += OnSearchChanged;
        listBox.AddChild(_search);

        var scrollList = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        listBox.AddChild(scrollList);
        _entryList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _entryList.AddThemeConstantOverride("separation", 8);
        scrollList.AddChild(_entryList);

        var detailPanel = new PanelContainer { Name = "Detail", SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = .80f };
        StylePanel(detailPanel, new Color(.98f, .95f, .88f, .86f));
        content.AddChild(detailPanel);
        var detailMargin = new MarginContainer();
        detailMargin.AddThemeConstantOverride("margin_left", 32); detailMargin.AddThemeConstantOverride("margin_right", 32);
        detailMargin.AddThemeConstantOverride("margin_top", 28); detailMargin.AddThemeConstantOverride("margin_bottom", 28);
        detailPanel.AddChild(detailMargin);
        var detailBox = new VBoxContainer();
        detailBox.AddThemeConstantOverride("separation", 16);
        detailMargin.AddChild(detailBox);
        var detailHeader = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        detailHeader.AddThemeConstantOverride("separation", 20);
        detailBox.AddChild(detailHeader);
        _detailIconPlate = new PanelContainer { Name = "EntryIconPlate", CustomMinimumSize = new Vector2(128, 128), SizeFlagsVertical = SizeFlags.ShrinkCenter };
        StyleIconPlate(_detailIconPlate, false);
        detailHeader.AddChild(_detailIconPlate);
        var iconMargin = new MarginContainer();
        iconMargin.AddThemeConstantOverride("margin_left", 9); iconMargin.AddThemeConstantOverride("margin_right", 9);
        iconMargin.AddThemeConstantOverride("margin_top", 9); iconMargin.AddThemeConstantOverride("margin_bottom", 9);
        _detailIconPlate.AddChild(iconMargin);
        _detailIcon = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore };
        iconMargin.AddChild(_detailIcon);
        var detailHeading = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        detailHeading.AddThemeConstantOverride("separation", 7);
        detailHeader.AddChild(detailHeading);
        _detailTitle = MakeLabel("", 34, Ink);
        _detailTitle.Name = "EntryTitle";
        detailHeading.AddChild(_detailTitle);
        _detailMeta = MakeLabel("", 19, MutedInk);
        detailHeading.AddChild(_detailMeta);
        detailBox.AddChild(new HSeparator());
        _detailScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        detailBox.AddChild(_detailScroll);
        var detailContent = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        detailContent.AddThemeConstantOverride("separation", 18);
        _detailScroll.AddChild(detailContent);
        _detailBody = MakeLabel("", 22, Ink);
        _detailBody.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        detailContent.AddChild(_detailBody);
        _itemDetailContent = new VBoxContainer { Name = "ItemDetailContent", Visible = false, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _itemDetailContent.AddThemeConstantOverride("separation", 18);
        detailContent.AddChild(_itemDetailContent);
        _statusDetailContent = new VBoxContainer { Name = "StatusDetailContent", Visible = false, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _statusDetailContent.AddThemeConstantOverride("separation", 18);
        detailContent.AddChild(_statusDetailContent);

        _beastDetail = new HBoxContainer { Name = "BeastDetail", Visible = false, SizeFlagsVertical = SizeFlags.ExpandFill };
        _beastDetail.AddThemeConstantOverride("separation", 32);
        detailBox.AddChild(_beastDetail);
        BuildBeastPreview(_beastDetail);
        var dossierScroll = new ScrollContainer
        {
            Name = "BeastDossierScroll", CustomMinimumSize = new Vector2(430, 0),
            SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = .46f,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        _beastDetail.AddChild(dossierScroll);
        _beastDossier = new VBoxContainer { Name = "BeastDossier", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _beastDossier.AddThemeConstantOverride("separation", 26);
        dossierScroll.AddChild(_beastDossier);

    }

    private void AddTab(Container parent, string text, Section section)
    {
        var button = new Button { Name = $"Tab{section}", Text = text, CustomMinimumSize = new Vector2(180, 56), FocusMode = FocusModeEnum.None };
        StyleCategoryTab(button, false);
        button.Pressed += () => ShowSection(section, resetSearch: false, animate: false);
        _tabs[section] = button;
        parent.AddChild(button);
    }

    private void BuildBeastPreview(Container detailBox)
    {
        _beastPreviewPanel = new PanelContainer
        {
            Name = "BeastPreview", Visible = false,
            CustomMinimumSize = new Vector2(470, 0),
            SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = .54f,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Stop
        };
        StylePanel(_beastPreviewPanel, new Color(.95f, .91f, .82f, .58f));
        detailBox.AddChild(_beastPreviewPanel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 22);
        margin.AddThemeConstantOverride("margin_right", 22);
        margin.AddThemeConstantOverride("margin_top", 20);
        margin.AddThemeConstantOverride("margin_bottom", 22);
        _beastPreviewPanel.AddChild(margin);

        var previewLayout = new VBoxContainer();
        previewLayout.AddThemeConstantOverride("separation", 16);
        margin.AddChild(previewLayout);
        var heading = MakeLabel("生灵静态立绘", 20, MutedInk);
        previewLayout.AddChild(heading);

        var stage = new Control
        {
            Name = "AnimationStage", CustomMinimumSize = new Vector2(0, 350),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        previewLayout.AddChild(stage);
        var viewportContainer = new SubViewportContainer
        {
            Name = "AnimationViewportContainer",
            Stretch = true,
            MouseFilter = MouseFilterEnum.Ignore
        };
        viewportContainer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        stage.AddChild(viewportContainer);
        _beastPreviewViewport = new SubViewport
        {
            Name = "AnimationViewport", Size = new Vector2I(640, 550),
            TransparentBg = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled
        };
        viewportContainer.AddChild(_beastPreviewViewport);
        _beastPreviewViewport.SizeChanged += FitBeastPreview;

        _beastPreviewStatus = MakeLabel("点击下方按钮切换动作", 17, MutedInk);
        _beastPreviewStatus.Name = "AnimationStatus";
        _beastPreviewStatus.HorizontalAlignment = HorizontalAlignment.Center;
        previewLayout.AddChild(_beastPreviewStatus);

        var buttons = new HBoxContainer();
        _beastAnimationControls = buttons;
        buttons.AddThemeConstantOverride("separation", 8);
        previewLayout.AddChild(buttons);
        foreach (var (label, animation) in new[] { ("待机", "idle"), ("攻击", "attack"), ("受击", "hurt"), ("死亡", "death") })
        {
            var button = new Button
            {
                Name = $"Animation{animation}", Text = label,
                CustomMinimumSize = new Vector2(0, 48),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                FocusMode = FocusModeEnum.None
            };
            StyleTab(button, animation == "idle");
            button.Pressed += () => PlayBeastPreviewAnimation(animation);
            _beastAnimationButtons[animation] = button;
            buttons.AddChild(button);
        }
        _beastRuntimeButton = new Button { Name = "EnableAnimationPreview", Text = "打开完整动作展示", Visible = false, CustomMinimumSize = new Vector2(0, 48), FocusMode = FocusModeEnum.None };
        StyleCategoryTab(_beastRuntimeButton, true);
        _beastRuntimeButton.Pressed += OpenSpineEncyclopedia;
        previewLayout.AddChild(_beastRuntimeButton);
    }

    private void Populate()
    {
        if (_entryList is null || _detailTitle is null || _detailMeta is null || _detailBody is null) return;
        foreach (var pair in _tabs) StyleCategoryTab(pair.Value, pair.Key == _current);
        var title = SectionTitle(_current);
        var allEntries = _entries.GetValueOrDefault(_current, []);
        var query = _search?.Text.Trim() ?? "";
        var visibleEntries = string.IsNullOrEmpty(query)
            ? allEntries
            : allEntries.Where(entry => entry.SearchText.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        var matchingEntries = visibleEntries.ToHashSet();
        foreach (var (entry, button) in _entryButtons) button.Visible = matchingEntries.Contains(entry);
        if (_entryCount is not null) _entryCount.Text = $"收录 {visibleEntries.Count} 条";
        if (visibleEntries.Count == 0)
        {
            if (_selectedEntry is { } previous && _entryButtons.TryGetValue(previous, out var previousButton)) StyleEntry(previousButton);
            _selectedEntry = null;
            SetBeastPreview(null);
            if (_beastDetail is not null) _beastDetail.Visible = false;
            if (_detailScroll is not null) _detailScroll.Visible = true;
            if (_detailBody is not null) _detailBody.Visible = true;
            if (_itemDetailContent is not null) _itemDetailContent.Visible = false;
            if (_statusDetailContent is not null) _statusDetailContent.Visible = false;
            if (_detailIconPlate is not null) _detailIconPlate.Visible = true;
            _detailTitle.Text = $"暂无{title}";
            _detailMeta.Text = string.IsNullOrEmpty(query) ? "此类内容尚未收录。" : $"没有找到与“{query}”相符的词条。";
            if (_detailBody is not null) _detailBody.Text = "可切换分类，或尝试缩短搜索词。";
            if (_detailIcon is not null) _detailIcon.Texture = LoadTexture("res://textures/ui/encyclopedia/item_placeholder_v1.svg");
            if (_detailIconPlate is not null) StyleIconPlate(_detailIconPlate, false);
            return;
        }
        foreach (var entry in visibleEntries)
        {
            if (_entryButtons.ContainsKey(entry)) continue;
            CreateEntryButton(entry);
        }
        ShowEntry(_selectedEntries.TryGetValue(_current, out var selection) && matchingEntries.Contains(selection) ? selection : visibleEntries[0]);
    }

    private void CreateEntryButton(Entry entry)
    {
        if (_entryList is null) return;
        var button = new Button
        {
            Text = "",
            CustomMinimumSize = new Vector2(0, 96),
            TooltipText = ""
        };
        StyleEntry(button);
        var padding = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        padding.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        padding.AddThemeConstantOverride("margin_left", 11); padding.AddThemeConstantOverride("margin_right", 9);
        padding.AddThemeConstantOverride("margin_top", 8); padding.AddThemeConstantOverride("margin_bottom", 8);
        button.AddChild(padding);
        padding.MinimumSizeChanged += () => button.CustomMinimumSize = new Vector2(0, Mathf.Max(96, padding.GetCombinedMinimumSize().Y));
        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 14);
        padding.AddChild(row);
        var icon = new TextureRect
        {
            Texture = entry.Icon ?? LoadTexture("res://textures/ui/encyclopedia/item_placeholder_v1.svg"),
            CustomMinimumSize = new Vector2(56, 56),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        icon.Visible=entry.Meta!="战斗机制 · 术语";
        row.AddChild(icon);
        var labels = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
        labels.AddThemeConstantOverride("separation", 3);
        row.AddChild(labels);
        var titleLabel = MakeLabel(entry.Title, 19, Ink);
        titleLabel.MouseFilter = MouseFilterEnum.Ignore;
        labels.AddChild(titleLabel);
        var metaLabel = MakeLabel(entry.Meta, 15, MutedInk);
        metaLabel.MouseFilter = MouseFilterEnum.Ignore;
        labels.AddChild(metaLabel);
        button.Pressed += () => ShowEntry(entry);
        _entryButtons[entry] = button;
        _entryList.AddChild(button);
    }

    private void OnSearchChanged(string _)
    {
        if (!_suppressSearchChanged && !IsCatalogLoading) Populate();
    }

    private void ShowEntry(Entry entry)
    {
        if (_detailTitle is null || _detailMeta is null || _detailBody is null || _detailIcon is null || _detailIconPlate is null) return;
        if (ReferenceEquals(_selectedEntry, entry)) return;
        if (_selectedEntry is { } previous && _entryButtons.TryGetValue(previous, out var previousButton)) StyleEntry(previousButton);
        _selectedEntry = entry;
        _selectedEntries[_current] = entry;
        _detailTitle.Text = entry.Title;
        _detailMeta.Text = entry.Meta;
        var portrait = entry.PortraitPath is { } portraitPath ? LoadTexture(portraitPath) ?? entry.Icon : entry.Icon;
        _detailIcon.Texture = portrait ?? LoadTexture("res://textures/ui/encyclopedia/item_placeholder_v1.svg");
        var isBeast = entry.Beast is not null || entry.Creature is not null;
        var isItem = entry.Item is not null;
        var isStatus = entry.Status is not null;
        _detailBody.Visible = !isItem && !isStatus;
        PlayerInfoText.Bind(_detailBody, isItem || isStatus ? "" : entry.Body);
        if (_itemDetailContent is not null)
        {
            _itemDetailContent.Visible = isItem;
            if (entry.Item is { } item) BuildItemDetails(item);
        }
        if (_statusDetailContent is not null)
        {
            _statusDetailContent.Visible = isStatus;
            if (entry.Status is { } status) BuildStatusDetails(status);
        }
        _detailIconPlate.Visible = !isBeast && _current != Section.Terms;
        if (_detailScroll is not null) { _detailScroll.Visible = !isBeast; _detailScroll.ScrollVertical = 0; }
        if (_beastDetail is not null) _beastDetail.Visible = isBeast;
        if (entry.Beast is { } template) BuildBeastDossier(template);
        else if (entry.Creature is { } creature) BuildCreatureDossier(creature);
        if (_entryButtons.TryGetValue(entry, out var selectedButton)) StyleEntry(selectedButton, true);
        StyleIconPlate(_detailIconPlate, entry.IsNegative);
        SetBeastPreview(entry, portrait);
    }

    private void BuildItemDetails(ItemData item)
    {
        if (_itemDetailContent is null) return;
        foreach (var child in _itemDetailContent.GetChildren())
        {
            _itemDetailContent.RemoveChild(child);
            child.QueueFree();
        }

        var material = item.craft_data as MaterialCraftData;
        var isManual = item.item_category == "秘籍" || item.gongfa is GongfaResource;
        if (!string.IsNullOrWhiteSpace(item.description))
        {
            var description = AddItemSection(_itemDetailContent, "物品说明");
            description.AddChild(MakeLabel(item.description, 20, MutedInk));
        }
        if (isManual)
        {
            var section = AddItemSection(_itemDetailContent, "修习境界");
            section.AddChild(MakeLabel(ManualRealmDescription(item), 23, Ink));
        }
        else if (material is not null)
        {
            var section = AddItemSection(_itemDetailContent, "材料评定");
            var metrics = new HBoxContainer();
            metrics.AddThemeConstantOverride("separation", 14);
            section.AddChild(metrics);
            AddItemMetric(metrics, "材料品阶", material.material_grade_name(), true);
            AddItemMetric(metrics, "材评", material.material_score().ToString("0.##"), false);
        }
        else
        {
            var section = AddItemSection(_itemDetailContent, "物品品阶");
            var metrics = new HBoxContainer();
            metrics.AddThemeConstantOverride("separation", 14);
            section.AddChild(metrics);
            AddItemMetric(metrics, "品阶", item.get_grade_string(), true);
        }

        var dimensions = material is null
            ? new[] { "五行", "质地", "相性", "功用" }
            : new[] { "五行", "质地", "相性" };
        var affixes = AddItemSection(_itemDetailContent, material is null ? "物品词缀" : "材性词缀");
        var affixGrid = new GridContainer { Columns = dimensions.Length, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        affixGrid.AddThemeConstantOverride("h_separation", 12);
        affixGrid.AddThemeConstantOverride("v_separation", 12);
        affixes.AddChild(affixGrid);
        foreach (var dimension in dimensions)
        {
            var values = StoragePanel.get_display_tag_values(item.tags, dimension, material);
            AddItemPropertyCard(affixGrid, dimension, values.Count == 0 ? "未标注" : string.Join("、", values));
        }

        if (material is not null)
            BuildUtilityGradeSection(item, material);

        var usage = AddItemSection(_itemDetailContent, "持有与使用");
        var usageGrid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        usageGrid.AddThemeConstantOverride("h_separation", 12);
        usageGrid.AddThemeConstantOverride("v_separation", 12);
        usage.AddChild(usageGrid);
        AddItemPropertyCard(usageGrid, "堆叠", item.can_stack ? "可堆叠 · 不限单格数量" : "不可堆叠 · 每件占一格");
        AddItemPropertyCard(usageGrid, "使用范围", item.usage_type switch
        {
            1 => "仅限战斗",
            2 => "大世界与战斗均可使用",
            _ => "大世界物品"
        });

        if (!string.IsNullOrWhiteSpace(item.battle_skill_path) && ResourceLoader.Load<ActiveSkillData>(item.battle_skill_path) is { } battleSkill)
        {
            var effect = AddItemSection(_itemDetailContent, "战斗效果");
            effect.AddChild(MakeLabel(string.IsNullOrWhiteSpace(battleSkill.description) ? "暂无详细说明。" : CardPresentation.Underline(CardPresentation.Face(battleSkill, null)), 19, MutedInk));
        }
    }

    private void BuildStatusDetails(StatusDetail status)
    {
        if (_statusDetailContent is null) return;
        foreach (var child in _statusDetailContent.GetChildren())
        {
            _statusDetailContent.RemoveChild(child);
            child.QueueFree();
        }

        var facts = new List<(string Name, string Value)> { ("状态类别", status.Category) };
        if (!string.IsNullOrWhiteSpace(status.StackLimit)) facts.Add(("叠加上限", $"{status.StackLimit} 层"));
        if (!string.IsNullOrWhiteSpace(status.Source)) facts.Add(("来源", status.Source));

        var info = AddItemSection(_statusDetailContent, "状态资料");
        var grid = new GridContainer { Columns = Math.Min(3, facts.Count), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 12);
        grid.AddThemeConstantOverride("v_separation", 12);
        info.AddChild(grid);
        foreach (var (name, value) in facts) AddItemPropertyCard(grid, name, value);

        var effect = AddItemSection(_statusDetailContent, "效果说明");
        var description = MakeLabel(CardPresentation.Underline(status.Effect), 19, MutedInk);
        description.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        effect.AddChild(description);
    }

    private void BuildUtilityGradeSection(ItemData item, MaterialCraftData material)
    {
        var names = ItemAffixCatalog.get_tag_values(item.tags, "功用").ToList();
        var grades = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var utility in material.utilities)
        {
            if (utility is null || string.IsNullOrWhiteSpace(utility.utility_id)) continue;
            if (!grades.ContainsKey(utility.utility_id)) grades.Add(utility.utility_id, utility.grade_name());
            if (!names.Contains(utility.utility_id, StringComparer.Ordinal)) names.Add(utility.utility_id);
        }

        var section = AddItemSection(_itemDetailContent!, "功用品阶");
        if (names.Count == 0)
        {
            section.AddChild(MakeLabel("未配置功用词缀", 18, MutedInk));
            return;
        }

        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 12);
        section.AddChild(row);
        foreach (var name in names.Distinct(StringComparer.Ordinal))
            AddUtilityGradeCard(row, name, grades.GetValueOrDefault(name, "未配置品级"));
    }

    private static VBoxContainer AddItemSection(VBoxContainer parent, string title)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(.96f, .92f, .83f, .58f),
            BorderColor = new Color(.53f, .43f, .27f, .48f),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
            ContentMarginLeft = 18, ContentMarginRight = 18, ContentMarginTop = 14, ContentMarginBottom = 14
        });
        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 12);
        panel.AddChild(content);
        content.AddChild(MakeLabel(title, 22, Ink));
        parent.AddChild(panel);
        return content;
    }

    private static void AddItemMetric(Container parent, string title, string value, bool emphasized)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 88) };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = emphasized ? new Color(.80f, .67f, .42f, .52f) : new Color(.89f, .84f, .72f, .62f),
            BorderColor = emphasized ? new Color(.55f, .40f, .18f, .72f) : new Color(.59f, .50f, .34f, .46f),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            ContentMarginLeft = 16, ContentMarginRight = 16, ContentMarginTop = 10, ContentMarginBottom = 10
        });
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 4);
        panel.AddChild(content);
        content.AddChild(MakeLabel(title, 16, MutedInk));
        var valueLabel = MakeLabel(value, emphasized ? 28 : 26, Ink);
        valueLabel.AutowrapMode = TextServer.AutowrapMode.Off;
        content.AddChild(valueLabel);
        parent.AddChild(panel);
    }

    private static void AddItemPropertyCard(Container parent, string title, string value)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 76) };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(1f, .98f, .91f, .52f),
            BorderColor = new Color(.60f, .51f, .35f, .42f),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 9, ContentMarginBottom = 9
        });
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 3);
        panel.AddChild(content);
        content.AddChild(MakeLabel(title, 15, MutedInk));
        content.AddChild(MakeLabel(value, 19, Ink));
        parent.AddChild(panel);
    }

    private static void AddUtilityGradeCard(Container parent, string utility, string grade)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 68) };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(.91f, .85f, .72f, .62f),
            BorderColor = new Color(.62f, .50f, .31f, .56f),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 9, ContentMarginBottom = 9
        });
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 10);
        panel.AddChild(row);
        row.AddChild(MakeLabel(utility, 21, Ink));
        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var badge = new PanelContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        badge.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(.73f, .59f, .34f, .72f),
            BorderColor = new Color(.51f, .38f, .19f, .62f),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
            ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 4, ContentMarginBottom = 4
        });
        var gradeLabel = MakeLabel(grade, 16, Ink);
        gradeLabel.AutowrapMode = TextServer.AutowrapMode.Off;
        badge.AddChild(gradeLabel);
        row.AddChild(badge);
        parent.AddChild(panel);
    }

    private static IEnumerable<int> LoadBeastEntriesSteps(List<Entry> result)
    {
        foreach (var path in FindResources("res://resources/beast_templates"))
        {
            yield return 0;
            var template = ResourceLoader.Load<BeastTemplate>(path);
            if (template is null || template.species_id is not ("forest_wolf" or "forest_bei") || string.IsNullOrWhiteSpace(template.display_name)) continue;
            var portraitPath = template.species_id == "forest_bei" ? BattlePaperDoll.ForestBeiPortraitPath : BattlePaperDoll.ForestWolfPortraitPath;
            var portrait = LoadPortraitThumbnail(portraitPath);
            if (portrait is null) continue;

            var body = new StringBuilder();
            AppendSection(body, "境界", $"{template.cultivation_realm}{template.cultivation_stage} · 对标云息境界");
            AppendSection(body, "常见三项战斗属性", $"体魄 {FormatAttributeRange(template.physique_min, template.physique_mean, template.physique_max)}\n灵慧 {FormatAttributeRange(template.intelligence_min, template.intelligence_mean, template.intelligence_max)}\n神魂 {FormatAttributeRange(template.soul_min, template.soul_mean, template.soul_max)}");
            AppendSection(body, "派生战斗属性", BuildDerivedBeastStats(template));
            AppendSection(body, "种族天赋", template.racial_talent is null
                ? "尚未配置。"
                : $"{template.racial_talent.talent_name}\n{template.racial_talent.description}");
            if (template.deck is not null)
                AppendSection(body, "牌组倾向", template.deck.deck_description);
            AppendSection(body, "危险度", "尚未配置。");
            AppendSection(body, "遁速", template.dun_speed > 0f
                ? template.dun_speed.ToString("0.#")
                : "尚未配置；不以行动速度代替。");
            AppendSection(body, "掉落", FormatBeastLoot(template));

            var search = $"{template.display_name} 妖 通智 云息 林地 狼 群猎 属性 体魄 灵慧 神魂 身心协调值 {body}";
            result.Add(new Entry(
                template.display_name,
                $"妖 · {template.cultivation_realm}{template.cultivation_stage} · 林地",
                EmptyBody(body),
                search,
                portrait,
                Beast: template,
                PortraitPath: portraitPath));
        }

        foreach (var creature in ReadCreatureRecords())
        {
            yield return 0;
            if (string.IsNullOrWhiteSpace(creature.Name) || string.IsNullOrWhiteSpace(creature.Portrait)) continue;
            var portrait = LoadPortraitThumbnail(creature.Portrait);
            if (portrait is null) continue;
            var body = CreatureBody(creature);
            var realm = string.IsNullOrWhiteSpace(creature.Stage) ? creature.Realm : $"{creature.Realm}{creature.Stage}";
            var meta = creature.Realm == "凡兽"
                ? "凡兽 · 普通动物"
                : string.IsNullOrWhiteSpace(creature.HumanParity)
                    ? $"{creature.Category} · {realm}"
                    : $"{creature.Category} · {realm} · 对标{creature.HumanParity}";
            result.Add(new Entry(creature.Name, meta, body,
                $"{creature.Name} {creature.Category} {creature.Realm} {creature.Stage} {creature.HumanParity} {creature.Habitat} {creature.Habit} {body}",
                portrait, Creature: creature, PortraitPath: creature.Portrait));
        }
    }

    private static List<CreatureRecord> ReadCreatureRecords()
    {
        const string catalogPath = "res://resources/bestiary/creatures.json";
        if (!Godot.FileAccess.FileExists(catalogPath)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<CreatureRecord>>(Godot.FileAccess.GetFileAsString(catalogPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        }
        catch (Exception exception)
        {
            GD.PushError($"百科生灵目录读取失败：{exception.Message}");
            return [];
        }
    }
    private static string CreatureBody(CreatureRecord creature)
    {
        var body = new StringBuilder();
        var classification = creature.Realm == "凡兽" ? "凡兽 · 普通动物" : $"{creature.Category} · {creature.Realm}{(string.IsNullOrWhiteSpace(creature.Stage) ? "" : creature.Stage)}" +
            (string.IsNullOrWhiteSpace(creature.HumanParity) ? "" : $" · 对标{creature.HumanParity}");
        AppendSection(body, "归类", classification);
        AppendSection(body, "栖息与习性", $"{creature.Habitat}\n{creature.Habit}");
        if (creature.Stats is { } stats)
        {
            var rows = new List<string>();
            AddRange(rows, "体魄", stats.Physique); AddRange(rows, "灵慧", stats.Intelligence);
            AddRange(rows, "神魂", stats.Soul);
            if (rows.Count > 0) AppendSection(body, "基础三项战斗属性范围（首轮估值）", string.Join("\n", rows));
        }
        else AppendSection(body, "基础四维", "境界高于云息；本轮只定境界和术法方向，数值范围待单独校准。");
        if (creature.PhysicalDefense is { } defense) AppendSection(body, "物理防御参考", defense.ToString("0.#"));
        AppendSection(body, "普通攻击", creature.BasicAttack);
        if (!string.IsNullOrWhiteSpace(creature.Talent)) AppendSection(body, "种族天赋（初稿）", creature.Talent);
        if (!string.IsNullOrWhiteSpace(creature.Technique)) AppendSection(body, "术法意向（初稿）", creature.Technique);
        AppendSection(body, "设定说明", "首轮百科设定，尚未接入战斗模板；术法按现行自身／单体目标规则表达，不新增状态、射程或位移。");
        return EmptyBody(body);
    }

    private static void AddRange(List<string> rows, string label, CreatureStat? range)
    {
        if (range is not null) rows.Add($"{label} {FormatAttributeRange(range.Min, range.Mean, range.Max)}");
    }

    private void BuildCreatureDossier(CreatureRecord creature)
    {
        if (_beastDossier is null) return;
        foreach (var child in _beastDossier.GetChildren()) { _beastDossier.RemoveChild(child); child.QueueFree(); }
        if (_beastDossier.GetParent() is ScrollContainer scroll) scroll.ScrollVertical = 0;
        if (creature.Stats is { } stats)
        {
            var rows = new List<StatRange>();
            AddStat(rows, "体魄", stats.Physique); AddStat(rows, "灵慧", stats.Intelligence);
            AddStat(rows, "神魂", stats.Soul);
            if (rows.Count > 0) AddStatTable("基础三项战斗属性范围", rows);
        }
        else AddDossierSection("属性范围").AddChild(MakeLabel("高于云息境界，数值待后续校准。", 18, MutedInk));
        AddDossierSection("栖息与习性").AddChild(MakeLabel($"{creature.Habitat}\n{creature.Habit}", 18, MutedInk));
        AddDossierSection("普通攻击").AddChild(MakeLabel(creature.BasicAttack, 18, MutedInk));
        if (!string.IsNullOrWhiteSpace(creature.Talent)) AddDossierSection("种族天赋（初稿）").AddChild(MakeLabel(creature.Talent, 18, MutedInk));
        if (!string.IsNullOrWhiteSpace(creature.Technique)) AddDossierSection("术法意向（初稿）").AddChild(MakeLabel(creature.Technique, 18, MutedInk));
        _beastDossier.AddChild(MakeLabel("首轮百科设定，尚未接入战斗模板。", 16, MutedInk));
    }

    private static void AddStat(List<StatRange> rows, string name, CreatureStat? stat)
    {
        if (stat is not null) rows.Add(AttributeStat(name, stat.Min, stat.Mean, stat.Max));
    }

    private static string FormatAttributeRange(float min, float mean, float max) =>
        $"{min:0.#}–{max:0.#}（均值 {mean:0.#}）";

    private void BuildBeastDossier(BeastTemplate template)
    {
        if (_beastDossier is null) return;
        foreach (var child in _beastDossier.GetChildren()) { _beastDossier.RemoveChild(child); child.QueueFree(); }
        if (_beastDossier.GetParent() is ScrollContainer scroll) scroll.ScrollVertical = 0;

        AddStatTable("基础三项战斗属性", [
            AttributeStat("体魄", template.physique_min, template.physique_mean, template.physique_max),
            AttributeStat("灵慧", template.intelligence_min, template.intelligence_mean, template.intelligence_max),
            AttributeStat("神魂", template.soul_min, template.soul_mean, template.soul_max)
        ]);
        AddStatTable("战斗属性", DerivedBeastStats(template));

        var talent = AddDossierSection("种族天赋");
        if (template.racial_talent is { } racialTalent)
        {
            talent.AddChild(MakeLabel(racialTalent.talent_name.Replace($"{template.display_name}·", ""), 20, Ink));
            talent.AddChild(MakeLabel(racialTalent.description, 18, MutedInk));
        }
        else talent.AddChild(MakeLabel("尚未配置", 18, MutedInk));
        if (template.deck is { } deck)
            AddDossierSection("战斗习性").AddChild(MakeLabel(deck.deck_description, 18, MutedInk));

        var other = AddDossierSection("其他资料");
        var missing = new List<string> { "危险度" };
        if (template.dun_speed > 0f) other.AddChild(MakeLabel($"遁速　{template.dun_speed:0.#}", 18, Ink));
        else missing.Add("遁速");
        if (template.battle_loot.Count > 0) other.AddChild(MakeLabel($"掉落　{FormatBeastLoot(template)}", 18, Ink));
        else missing.Add("掉落");
        other.AddChild(MakeLabel($"{string.Join("、", missing)}：尚未配置", 17, MutedInk));
        _beastDossier.AddChild(MakeLabel("数值为兽种模板参考；个体属性在范围内随机生成。", 16, MutedInk));
    }

    private VBoxContainer AddDossierSection(string title)
    {
        var section = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        section.AddThemeConstantOverride("separation", 10);
        var heading = MakeLabel(title, 22, Ink);
        section.AddChild(heading);
        _beastDossier!.AddChild(section);
        return section;
    }

    private void AddStatTable(string title, IReadOnlyList<StatRange> stats)
    {
        var section = AddDossierSection(title);
        var table = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        table.AddThemeConstantOverride("h_separation", 16);
        table.AddThemeConstantOverride("v_separation", 8);
        section.AddChild(table);
        foreach (var header in new[] { "属性", "最小", "均值", "最大" })
            AddStatCell(table, header, true, header == "属性");
        foreach (var stat in stats)
        {
            AddStatCell(table, stat.Name, false, true);
            AddStatCell(table, stat.Minimum, false, false);
            AddStatCell(table, stat.Mean, false, false, true);
            AddStatCell(table, stat.Maximum, false, false);
        }
    }

    private static void AddStatCell(GridContainer table, string text, bool header, bool name, bool mean = false)
    {
        var label = MakeLabel(text, header ? 16 : 19, header || (!name && !mean) ? MutedInk : Ink);
        label.AutowrapMode = TextServer.AutowrapMode.Off;
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        label.HorizontalAlignment = name ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        table.AddChild(label);
    }

    private static StatRange AttributeStat(string name, float min, float mean, float max) =>
        new(name, min.ToString("0.#"), mean.ToString("0.#"), max.ToString("0.#"));

    private static List<StatRange> DerivedBeastStats(BeastTemplate template) => [
        IntegerStat("生命", template, (p, i, s) => 60 + p * 11 + s * 3 + i * 1.5f),
        new StatRange("物理防御", "0", "0", "0"),
        IntegerStat("精准", template, (p, i, s) => i * 7 + s + p * .5f),
        IntegerStat("身法", template, (p, i, s) => Mathf.Min(p, s) * 4 + s * 2 + i + p * .5f),
        IntegerStat("会心", template, (p, i, s) => p * .5f + s * 2f + i * .3f),
        IntegerStat("护心", template, (p, i, s) => p * 3 + i * 2 + Mathf.Min(p, s) * 2 + s * 1.5f),
        AttributeStat("行动速度",
            Creature.calculate_base_initiative_speed(template.physique_min, template.intelligence_min, template.soul_min),
            Creature.calculate_base_initiative_speed(template.physique_mean, template.intelligence_mean, template.soul_mean),
            Creature.calculate_base_initiative_speed(template.physique_max, template.intelligence_max, template.soul_max))
    ];

    private static StatRange IntegerStat(string name, BeastTemplate template, Func<float, float, float, float> formula) =>
        new(name,
            Mathf.RoundToInt(formula(template.physique_min, template.intelligence_min, template.soul_min)).ToString(),
            Mathf.RoundToInt(formula(template.physique_mean, template.intelligence_mean, template.soul_mean)).ToString(),
            Mathf.RoundToInt(formula(template.physique_max, template.intelligence_max, template.soul_max)).ToString());

    private static string BuildDerivedBeastStats(BeastTemplate template)
    {
        return string.Join("\n", DerivedBeastStats(template).Select(stat => $"{stat.Name} {stat.Minimum}–{stat.Maximum}（均值 {stat.Mean}）"));
    }

    private static string FormatBeastLoot(BeastTemplate template)
    {
        if (template.battle_loot.Count == 0) return "尚未配置。";
        var rows = new List<string>();
        foreach (var loot in template.battle_loot)
        {
            var itemPath = BasicDeckUpgradeConfig.get_string(loot, "item_path");
            var item = ResourceLoader.Load<ItemData>(itemPath);
            var minCount = BasicDeckUpgradeConfig.get_int(loot, "min_count", 1);
            var maxCount = BasicDeckUpgradeConfig.get_int(loot, "max_count", minCount);
            var chance = loot.GetValueOrDefault("chance", 1f).AsSingle();
            var name = item?.entity_name ?? "未能加载的掉落物";
            rows.Add($"{name} ×{minCount}–{maxCount}（{Mathf.RoundToInt(chance * 100f)}%）");
        }
        return string.Join("\n", rows);
    }

    private void SetBeastPreview(Entry? entry, Texture2D? portrait = null)
    {
        if (_beastPreviewPanel is null || _beastPreviewViewport is null) return;
        var hasStaticCreature = entry?.Beast is not null || entry?.Creature is not null;
        if (_beastAnimationControls is not null) _beastAnimationControls.Visible = false;
        if (_beastRuntimeButton is not null) _beastRuntimeButton.Visible = false;
        if (hasStaticCreature)
        {
            _beastPreviewPanel.Visible = true;
            _beastPreviewViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
            ClearBeastPreviewCharacter();
            _beastPreviewResourcePath = "";
            ShowBeastPreviewFallback(portrait ?? entry?.Icon, "静态展示 · 同一立绘可用于战斗场景。");
            return;
        }
        var resourcePath = entry?.PreviewSpinePath;
        _beastPreviewPanel.Visible = !string.IsNullOrWhiteSpace(resourcePath);
        if (string.IsNullOrWhiteSpace(resourcePath))
        {
            _beastPreviewViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
            if (_beastPreviewSpine is not null && GodotObject.IsInstanceValid(_beastPreviewSpine))
                _beastPreviewSpine.Call("set_active", false);
            if (_beastPreviewFallback is not null)
            {
                _beastPreviewFallback.Texture = null;
                _beastPreviewFallback.Hide();
            }
            return;
        }
        _beastPreviewViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;

        if (_beastPreviewSpine is not null && GodotObject.IsInstanceValid(_beastPreviewSpine) && _beastPreviewResourcePath == resourcePath)
        {
            _beastPreviewSpine.Call("set_active", true);
            SetBeastAnimationButtonsEnabled(true);
            PlayBeastPreviewAnimation("idle");
            return;
        }

        ClearBeastPreviewCharacter();
        _beastPreviewResourcePath = resourcePath;
        if (_beastRuntimeButton is not null) { _beastRuntimeButton.Visible = false; _beastRuntimeButton.Disabled = false; }
        if (!ClassDB.ClassExists("Spine") || !ClassDB.CanInstantiate("Spine"))
        {
        ShowBeastPreviewFallback(portrait ?? entry?.Icon, "静态展示 · 同一立绘可用于战斗场景。");
            return;
        }
        if (ResourceLoader.Load<Resource>(resourcePath) is not { } resource ||
            ClassDB.Instantiate("Spine").AsGodotObject() is not Node2D spine)
        {
            ShowBeastPreviewFallback(portrait ?? entry?.Icon, "动作资源未能加载，暂时显示立绘。");
            return;
        }

        spine.Name = "EncyclopediaForestWolfSpine";
        _beastPreviewViewport.AddChild(spine);
        spine.Call("set_resource", resource);
        spine.Call("set_flip_x", false);
        spine.Call("set_active", true);
        foreach (var animation in new[] { "idle", "attack", "hurt", "death" })
        {
            if (spine.Call("get_animation_length", animation).AsSingle() > 0f) continue;
            spine.QueueFree();
            var actionName = animation switch { "idle" => "待机", "attack" => "攻击", "hurt" => "受击", _ => "死亡" };
            ShowBeastPreviewFallback(portrait ?? entry?.Icon, $"暂缺「{actionName}」动作，显示静态立绘。");
            return;
        }

        _beastPreviewSpine = spine;
        FitBeastPreview();
        _beastPreviewFallback?.Hide();
        SetBeastAnimationButtonsEnabled(true);
        PlayBeastPreviewAnimation("idle");
    }

    private void ShowBeastPreviewFallback(Texture2D? icon, string message, bool canOpenRuntime = false)
    {
        if (_beastPreviewViewport is null) return;
        _beastPreviewFallback ??= new Sprite2D
        {
            Name = "BestiaryStaticPreview",
            Centered = true
        };
        if (_beastPreviewFallback.GetParent() is null) _beastPreviewViewport.AddChild(_beastPreviewFallback);
        _beastPreviewFallback.Texture = icon;
        _beastPreviewFallback.Show();
        FitBeastPreview();
        if (_beastPreviewStatus is not null) _beastPreviewStatus.Text = message;
        if (_beastRuntimeButton is not null) _beastRuntimeButton.Visible = canOpenRuntime;
        SetBeastAnimationButtonsEnabled(false);
    }

    private void FitBeastPreview()
    {
        if (_beastPreviewViewport is null) return;
        var size = (Vector2)_beastPreviewViewport.Size;
        var scale = Mathf.Max(.05f, Mathf.Min((size.X - 64f) / 1680f, (size.Y - 64f) / 1120f));
        if (_beastPreviewSpine is not null && GodotObject.IsInstanceValid(_beastPreviewSpine))
        {
            _beastPreviewSpine.Scale = Vector2.One * scale;
            _beastPreviewSpine.Position = size * .5f + new Vector2(-8f, 484f) * scale;
        }
        if (_beastPreviewFallback is not null)
        {
            _beastPreviewFallback.Scale = Vector2.One * scale;
            _beastPreviewFallback.Position = size * .5f;
        }
    }

    private void OpenSpineEncyclopedia()
    {
        var projectDirectory = ProjectSettings.GlobalizePath("res://");
        var runtimeDirectory = System.IO.Path.Combine(projectDirectory, "runtime", "godot", "Godot_v4.7.2-stable_spine3_8_mono_win64");
        var executable = System.IO.Path.Combine(runtimeDirectory, "Godot_spine3_8_editor.exe");
        if (!System.IO.File.Exists(executable))
        {
            if (_beastPreviewStatus is not null) _beastPreviewStatus.Text = "请使用项目的「打开项目」入口查看完整动作。";
            return;
        }
        try
        {
            var start = new System.Diagnostics.ProcessStartInfo(executable)
            {
                WorkingDirectory = projectDirectory, UseShellExecute = false, CreateNoWindow = true
            };
            foreach (var argument in new[] { "--path", projectDirectory, "--rendering-method", "gl_compatibility", "--rendering-driver", "opengl3", "--", "--encyclopedia-beasts" })
                start.ArgumentList.Add(argument);
            var dotnetDirectory = System.IO.Path.Combine(runtimeDirectory, "dotnet");
            start.Environment["DOTNET_ROOT"] = dotnetDirectory;
            start.Environment["DOTNET_MULTILEVEL_LOOKUP"] = "0";
            start.Environment["DOTNET_ROLL_FORWARD"] = "LatestMinor";
            start.Environment["PATH"] = dotnetDirectory + System.IO.Path.PathSeparator + System.Environment.GetEnvironmentVariable("PATH");
            if (System.Diagnostics.Process.Start(start) is not null)
            {
                if (_beastRuntimeButton is not null) _beastRuntimeButton.Disabled = true;
                if (_beastPreviewStatus is not null) _beastPreviewStatus.Text = "已打开完整动作展示窗口。";
            }
        }
        catch (Exception exception)
        {
            GD.PushWarning($"无法启动百科骨骼展示：{exception.Message}");
            if (_beastPreviewStatus is not null) _beastPreviewStatus.Text = "未能打开，请使用项目的「打开项目」入口。";
        }
    }

    private void ClearBeastPreviewCharacter()
    {
        if (_beastPreviewSpine is not null && GodotObject.IsInstanceValid(_beastPreviewSpine))
        {
            _beastPreviewSpine.Call("set_active", false);
            _beastPreviewSpine.QueueFree();
        }
        _beastPreviewSpine = null;
        _beastPreviewFallback?.Hide();
    }

    private void SetBeastAnimationButtonsEnabled(bool enabled)
    {
        foreach (var button in _beastAnimationButtons.Values) button.Disabled = !enabled;
    }

    private void PlayBeastPreviewAnimation(string animation)
    {
        if (_beastPreviewSpine is null || !GodotObject.IsInstanceValid(_beastPreviewSpine)) return;
        _beastPreviewAnimation = animation;
        _beastPreviewSpine.Call("set_active", true);
        _beastPreviewSpine.Call("play", animation, 1.0f, animation == "idle", 0, 0.0f);
        foreach (var (name, button) in _beastAnimationButtons) StyleTab(button, name == animation);
        if (_beastPreviewStatus is not null)
            _beastPreviewStatus.Text = animation switch
            {
                "attack" => "攻击 · 播放完成后回到待机",
                "hurt" => "受击 · 播放完成后回到待机",
                "death" => "死亡 · 停留在动作末帧",
                _ => "待机 · 循环播放"
            };
    }

    public override void _Process(double delta)
    {
        if (!Visible || _beastPreviewPanel?.Visible != true || _beastPreviewSpine is null || !GodotObject.IsInstanceValid(_beastPreviewSpine)) return;
        if (_beastPreviewAnimation is "attack" or "hurt" && !_beastPreviewSpine.Call("is_playing", 0).AsBool())
            PlayBeastPreviewAnimation("idle");
    }

    private static IEnumerable<int> LoadStatusEntriesSteps(List<Entry> result)
    {
        foreach (var path in FindResources("res://resources/battle/statuses"))
        {
            yield return 0;
            var status = ResourceLoader.Load<StatusEffect>(path);
            if (status is null || string.IsNullOrWhiteSpace(status.name)) continue;
            var kind = status.is_debuff ? "负面状态" : "增益状态";
            var effect = string.IsNullOrWhiteSpace(status.description) ? "暂无详细说明。" : status.description;
            var icon = BattleUnitOverlay.GetStatusIcon(status.effect_id, null) ?? LoadTexture("res://textures/ui/encyclopedia/buff_category_v1.svg");
            result.Add(new Entry(status.name, kind, "", $"{status.name} {kind} {status.max_stacks} {effect}", icon, status.is_debuff,
                Status: new StatusDetail(kind, status.max_stacks.ToString(), null, effect)));
        }

        foreach (var path in FindResources("res://resources/activeskills"))
        {
            yield return 0;
            var skill = ResourceLoader.Load<ActiveSkillData>(path);
            if (skill is null || !GongfaAvailability.IsSkillEnabled(skill) || string.IsNullOrWhiteSpace(skill.skill_name)) continue;
            var isPassive = skill.skill_type == ActiveSkillData.SkillType.POWER || skill.slot_category == "passive" || skill.keywords.Contains("固有");
            if (!isPassive) continue;
            var source = string.IsNullOrWhiteSpace(skill.source_gongfa_name) ? "角色固有" : skill.source_gongfa_name;
            var effect = string.IsNullOrWhiteSpace(skill.description) ? "暂无详细说明。" : CardPresentation.Face(skill, null);
            var icon = BattleUnitOverlay.GetStatusIcon($"心法:{skill.skill_name}", new StatusInstance { source_name = source })
                ?? LoadTexture("res://textures/ui/encyclopedia/buff_category_v1.svg");
            result.Add(new Entry(skill.skill_name, $"常驻心法 · {source}", "", $"{skill.skill_name} {source} {effect}", icon,
                Status: new StatusDetail("常驻被动", null, source, effect)));
        }

        AddStatusResourceEntry(result, "气血", "战斗资源", "基础上限8层，可由修命淬体与明确的战斗效果调整。每下降1层，按当前回落回复率恢复生命；基础回复率为最大生命的5%。战斗结束时清空。", "回落 生命 回复");
        AddStatusResourceEntry(result, "血炉", "战斗资源", "每2点提高1%物理伤害；可被相关招式消耗，战斗结束时清空。", "物理伤害 赤阳");
        AddStatusResourceEntry(result, "炁", "战斗资源", "非费用资源，跨回合保留。炁达到当前法力上限的两倍时触发周天：清空炁并恢复法力，再结算周天效果；超额不保留，战斗结束清零。", "周天 法力");
        AddStatusResourceEntry(result, "护盾", "战斗资源", "由法力凝成并环绕自身的护罩，优先吸收受到的伤害，并随实际吸收量减少。", "防御 法力 吸收");
    }

    private static void AddStatusResourceEntry(List<Entry> result, string title, string kind, string description, string search)
    {
        var icon = BattleUnitOverlay.GetStatusIcon(title, null) ?? LoadTexture("res://textures/ui/encyclopedia/buff_category_v1.svg");
        result.Add(new Entry(title, kind, "", $"{title} {kind} {description} {search}", icon,
            Status: new StatusDetail(kind, null, null, description)));
    }

    private static IEnumerable<int> LoadItemEntriesSteps(List<Entry> result)
    {
        foreach (var path in FindResources("res://resources/entities/items"))
        {
            yield return 0;
            var item = ResourceLoader.Load<ItemData>(path);
            if (item is null || !GongfaAvailability.IsItemEnabled(item) || string.IsNullOrWhiteSpace(item.entity_name)) continue;
            var icon = LoadTexture(item.icon_path);
            if (icon is null) continue;
            var category = string.IsNullOrWhiteSpace(item.item_subcategory) ? item.item_category : $"{item.item_category} · {item.item_subcategory}";
            var body = new StringBuilder();
            var isManual = item.item_category == "秘籍" || item.gongfa is GongfaResource;
            var material = item.craft_data as MaterialCraftData;
            if (isManual)
                AppendSection(body, "修习境界", ManualRealmDescription(item));
            else if (material is not null)
                AppendSection(body, "品阶", $"{material.material_grade_name()}（材评 {material.material_score():0.##}）");
            else
                AppendSection(body, "品阶", item.get_grade_string());
            var tags = StoragePanel.build_tags(item.tags, material);
            if (tags != "无词缀") AppendSection(body, "物品词缀", tags);
            AppendSection(body, "堆叠", item.can_stack ? "可堆叠，不限单格数量" : "不可堆叠，每件占一格");
            AppendSection(body, "使用范围", item.usage_type switch { 1 => "仅限战斗", 2 => "大世界与战斗均可使用", _ => "大世界物品" });
            if (!string.IsNullOrWhiteSpace(item.battle_skill_path) && ResourceLoader.Load<ActiveSkillData>(item.battle_skill_path) is { } battleSkill)
                AppendSection(body, "战斗效果", CardPresentation.Underline(CardPresentation.Face(battleSkill, null)));

            var qualitySearch = isManual
                ? ManualRealmDescription(item)
                : material is not null
                    ? $"{material.material_grade_name()} 材评 {material.material_score():0.##}"
                    : item.get_grade_string();
            var searchable = $"{item.entity_name} {category} {qualitySearch} {tags} {body}";
            result.Add(new Entry(item.entity_name, category, EmptyBody(body), searchable, icon, Item: item));
        }
    }

    private static string ManualRealmDescription(ItemData item)
    {
        if (item.gongfa is not GongfaResource gongfa) return "境界未标注";
        if (!string.IsNullOrWhiteSpace(gongfa.main_realm_start) && !string.IsNullOrWhiteSpace(gongfa.main_realm_end))
            return $"{gongfa.main_realm_start}—{gongfa.main_realm_end}";
        if (!string.IsNullOrWhiteSpace(gongfa.required_realm)) return gongfa.required_realm;
        var chapterRealms = gongfa.get_sorted_chapters()
            .Select(chapter => chapter.required_realm)
            .Where(realm => !string.IsNullOrWhiteSpace(realm))
            .Distinct()
            .ToList();
        return chapterRealms.Count > 0 ? $"卷章门槛：{string.Join("、", chapterRealms)}" : "境界未标注";
    }

    private static Texture2D? LoadTexture(string path) =>
        string.IsNullOrWhiteSpace(path) || !ResourceLoader.Exists(path) ? null : ResourceLoader.Load<Texture2D>(path);

    private static Texture2D? LoadPortraitThumbnail(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !ResourceLoader.Exists(path)) return null;
        var thumbnail = LoadBakedPortraitThumbnail(path);
        if (thumbnail is not null) return thumbnail;
        // Keep only small directory thumbnails resident; the full portrait is loaded for the selected entry.
        var source = LoadTexture(path);
        if (source is null) return null;
        using (var image = source.GetImage())
        {
            if (image.IsEmpty()) return null;
            var size = image.GetSize();
            var scale = Mathf.Min(144f / size.X, 96f / size.Y);
            image.Resize(Mathf.Max(1, Mathf.RoundToInt(size.X * scale)), Mathf.Max(1, Mathf.RoundToInt(size.Y * scale)), Image.Interpolation.Lanczos);
            return ImageTexture.CreateFromImage(image);
        }
    }

    private static IEnumerable<string> FindResources(string root)
    {
        var files = new List<string>();
        CollectResources(root, files);
        files.Sort(StringComparer.Ordinal);
        return files;
    }

    private static void CollectResources(string directory, List<string> files)
    {
        var access = DirAccess.Open(directory);
        if (access is null) return;
        foreach (var file in access.GetFiles())
            if (file.EndsWith(".tres", StringComparison.OrdinalIgnoreCase)) files.Add($"{directory}/{file}");
        foreach (var child in access.GetDirectories()) CollectResources($"{directory}/{child}", files);
    }

    private static void AppendSection(StringBuilder builder, string heading, string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (builder.Length > 0) builder.AppendLine().AppendLine();
        var displayText = System.Text.RegularExpressions.Regex.Replace(text.Trim(), @"(?<![A-Za-z])Buff(?![A-Za-z])", "状态", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        builder.Append(heading).AppendLine().Append(displayText);
    }

    private static string EmptyBody(StringBuilder builder) => builder.Length == 0 ? "暂无详细说明。" : builder.ToString().Trim();

    private static string SectionTitle(Section section) => section switch
    {
        Section.Statuses => "状态", Section.Items => "物品", Section.Beasts => "生灵", Section.Terms => "术语介绍", _ => "资料"
    };

    private void play_open_animation()
    {
        if (_background is null || _pageContent is null || !Visible || _closing) return;
        var tween = _presentationTween = CreateTween().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.SetParallel();
        tween.TweenProperty(_background, "modulate:a", 1f, .18f);
        tween.TweenProperty(_pageContent, "modulate:a", 1f, .16f);
    }

    private void finish_close()
    {
        if (_beastPreviewSpine is not null && GodotObject.IsInstanceValid(_beastPreviewSpine))
            _beastPreviewSpine.Call("set_active", false);
        if (_beastPreviewViewport is not null) _beastPreviewViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
        Hide();
        _closing = false;
    }

    private static Label MakeLabel(string text, int size, Color color)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = MouseFilterEnum.Ignore };
        PlayerInfoText.Bind(label, text);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static void StylePanel(PanelContainer panel, Color color)
    {
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = color, BorderColor = new Color("82755c"),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12, CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12
        });
    }

    private static void StyleIconPlate(PanelContainer panel, bool negative)
    {
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = negative ? new Color(.45f, .20f, .16f, .20f) : new Color(.73f, .62f, .39f, .18f),
            BorderColor = negative ? new Color("a45b4c") : new Color("af965c"),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12, CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12
        });
    }

    private static void StyleTab(Button button, bool selected)
    {
        button.AddThemeFontSizeOverride("font_size", 18);
        button.AddThemeColorOverride("font_color", Ink);
        button.AddThemeColorOverride("font_hover_color", Ink);
        button.AddThemeColorOverride("font_pressed_color", Ink);
        button.AddThemeColorOverride("font_focus_color", Ink);
        button.AddThemeColorOverride("font_disabled_color", new Color(MutedInk, .55f));
        button.Modulate = selected ? new Color(1.08f, .91f, .62f, 1f) : Colors.White;
        button.AddThemeStyleboxOverride("normal", new StyleBoxFlat
        {
            BgColor = selected ? new Color(.84f, .74f, .52f, .65f) : new Color(1f, .98f, .91f, .42f),
            BorderColor = new Color(.55f, .49f, .36f, .5f), BorderWidthLeft = 1, BorderWidthTop = 1,
            BorderWidthRight = 1, BorderWidthBottom = 1, CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6
        });
        button.AddThemeStyleboxOverride("hover", new StyleBoxFlat { BgColor = new Color(.98f, .92f, .77f, .9f), CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6 });
    }

    private static void StyleCategoryTab(Button button, bool selected)
    {
        button.AddThemeFontSizeOverride("font_size", 20);
        var labelColor = selected ? new Color("2a251f") : new Color("f1e8d3");
        var highlightedLabelColor = selected ? new Color("2a251f") : new Color("fff5df");
        button.AddThemeColorOverride("font_color", labelColor);
        button.AddThemeColorOverride("font_hover_color", highlightedLabelColor);
        button.AddThemeColorOverride("font_pressed_color", highlightedLabelColor);
        button.AddThemeColorOverride("font_focus_color", labelColor);
        button.Modulate = Colors.White;
        button.AddThemeStyleboxOverride("normal", CategoryTabStyle(selected, false, false));
        button.AddThemeStyleboxOverride("hover", CategoryTabStyle(selected, true, false));
        button.AddThemeStyleboxOverride("pressed", CategoryTabStyle(selected, false, true));
        button.AddThemeStyleboxOverride("disabled", CategoryTabStyle(selected, false, false));
    }

    private static StyleBoxFlat CategoryTabStyle(bool selected, bool hovered, bool pressed)
    {
        Color background;
        if (selected)
            background = pressed ? new Color(.68f, .52f, .30f) : hovered ? new Color(.84f, .72f, .47f) : new Color(.77f, .63f, .38f, .98f);
        else
            background = pressed ? new Color(.24f, .28f, .25f, .98f) : hovered ? new Color(.18f, .22f, .20f, .98f) : new Color(.10f, .13f, .12f, .96f);
        var border = selected ? new Color(.43f, .31f, .14f, 1f) : new Color(.67f, .56f, .35f, .92f);
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = selected ? 2 : 1,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 5,
            ContentMarginBottom = 5
        };
    }

    private static void StyleEntry(Button button, bool selected = false)
    {
        button.AddThemeFontSizeOverride("font_size", 17);
        button.AddThemeColorOverride("font_color", Ink);
        var normal = new StyleBoxFlat
        {
            BgColor = selected ? new Color(.88f, .79f, .58f, .65f) : new Color(1f, .98f, .91f, .56f),
            BorderColor = selected ? new Color("967442") : new Color(.55f, .49f, .36f, .5f),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            ContentMarginLeft = 12, ContentMarginRight = 10, ContentMarginTop = 7, ContentMarginBottom = 7
        };
        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", new StyleBoxFlat
        {
            BgColor = new Color(.98f, .92f, .77f, .9f), BorderColor = new Color(.65f, .49f, .23f, .85f),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            ContentMarginLeft = 12, ContentMarginRight = 10, ContentMarginTop = 7, ContentMarginBottom = 7
        });
    }
}
