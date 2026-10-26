using Godot;

/// <summary>2026-10-12 已认可角色页：设计坐标只负责排版，所有内容来自当前角色。</summary>
public partial class InfoPanel
{
    private static readonly Vector2 CharacterDesignSize = new(1672, 941);
    private Control? _characterCanvas;
    private Label? _characterTitle, _characterName, _characterRealm, _characterRoot;
    private readonly List<Button> _characterTabs = [];
    private readonly Dictionary<int, Control> _characterPages = [];
    private PanelContainer? _readerPanel;
    private RichTextLabel? _readerText;
    private Label? _readerTitle;
    private TextureRect? _readerIcon;
    private ScrollContainer? _readerScroll;
    private Control? _readerAnchor;
    private int _readerTicket;
    private string _characterIdentity = "";
    private const string CharacterFont = "res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf";

    private static Label CharacterText(string text, int size = 22, bool secondary = false)
    {
        var label = WorldHudStyle.Text(text, size, secondary);
        PlayerInfoText.Bind(label, text);
        // 单行字段保留文字的最小宽度，避免被同排的伸展控件挤成竖排。
        label.AutowrapMode = TextServer.AutowrapMode.Off;
        return label;
    }

    private static Label CharacterParagraph(string text, int size = 22, bool secondary = false)
    {
        var label = CharacterText(text, size, secondary);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return label;
    }

    private static Label CharacterClippedText(string text, int size = 22, bool secondary = false)
    {
        var label = CharacterText(text, size, secondary);
        label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        label.ClipText = true;
        return label;
    }

    private static void CharacterRect(Control node, float x, float y, float width, float height)
    {
        node.Position = new Vector2(x, y); node.Size = new Vector2(width, height);
    }

    private static StyleBoxFlat CharacterStyle(bool selected = false, int padding = 16) => new()
    {
        BgColor = new Color("efe4c7", .55f),
        BorderColor = selected ? new Color("5c8175") : new Color("ad9875", .8f),
        BorderWidthLeft = selected ? 2 : 1, BorderWidthRight = selected ? 2 : 1,
        BorderWidthTop = selected ? 2 : 1, BorderWidthBottom = selected ? 2 : 1,
        ContentMarginLeft = padding, ContentMarginRight = padding,
        ContentMarginTop = padding, ContentMarginBottom = padding
    };

    // 按钮只勾亮边缘，底色透出原纸面／贴图，不用纯色块盖住素材。
    private static StyleBoxFlat CharacterButtonStyle(bool selected = false, int padding = 8, bool highlight = false)
    {
        var style = CharacterStyle(selected, padding);
        style.BgColor = Colors.Transparent;
        style.DrawCenter = false;
        if (highlight)
        {
            style.BorderColor = new Color("c5ad77");
            style.BorderWidthLeft = style.BorderWidthRight = style.BorderWidthTop = style.BorderWidthBottom = 2;
        }
        return style;
    }

    private static Button CharacterButton(string text, int size = 21, bool selected = false, int padding = 8)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(0, 42) };
        WorldHudStyle.StyleSmallButton(button, size);
        button.AddThemeColorOverride("font_hover_pressed_color", WorldHudStyle.Ink);
        button.AddThemeStyleboxOverride("normal", CharacterButtonStyle(selected, padding));
        foreach (var state in new[] { "hover", "pressed", "hover_pressed" })
            button.AddThemeStyleboxOverride(state, CharacterButtonStyle(selected, padding, true));
        button.AddThemeStyleboxOverride("disabled", CharacterButtonStyle(false, padding));
        button.AddThemeStyleboxOverride("focus", CharacterButtonStyle(selected, padding, true));
        button.AddThemeColorOverride("font_disabled_color", new Color("89806b"));
        button.MouseEntered += () => { if (!button.Disabled) button.Modulate = new Color(1.07f, 1.05f, 1.01f); };
        button.MouseExited += () => button.Modulate = Colors.White;
        return button;
    }

    private static VBoxContainer CharacterBox(Control parent, string title = "")
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.Fill };
        box.AddThemeConstantOverride("separation", 10); parent.AddChild(box);
        if (title.Length > 0) { box.AddChild(CharacterText(title, 28)); box.AddChild(new HSeparator()); }
        return box;
    }

    private static PanelContainer CharacterPanel(Control parent, float x, float y, float width, float height, int padding = 16)
    {
        var panel = new PanelContainer(); panel.AddThemeStyleboxOverride("panel", CharacterStyle(false, padding));
        parent.AddChild(panel); CharacterRect(panel, x, y, width, height);
        var frame = new InventoryPaperFrame { FillPaper = false, ShowBehindParent = true };
        panel.AddChild(frame); frame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        return panel;
    }

    private static ScrollContainer CharacterScroll(Control parent)
    {
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto, SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipContents = true };
        parent.AddChild(scroll); return scroll;
    }

    private static LineEdit CharacterSearch(string placeholder)
    {
        var edit = new LineEdit { PlaceholderText = placeholder, CustomMinimumSize = new Vector2(200, 44),
            SizeFlagsHorizontal = SizeFlags.ExpandFill, ClearButtonEnabled = true };
        edit.AddThemeFontOverride("font", GD.Load<FontFile>(CharacterFont)); edit.AddThemeFontSizeOverride("font_size", 21);
        edit.AddThemeColorOverride("font_color", WorldHudStyle.Ink);
        edit.AddThemeColorOverride("font_placeholder_color", new Color("89816c"));
        edit.AddThemeColorOverride("caret_color", WorldHudStyle.Ink);
        edit.AddThemeStyleboxOverride("normal", CharacterStyle(false, 7));
        var focus = CharacterStyle(false, 7); focus.BorderColor = new Color("c5ad77");
        focus.BorderWidthLeft = focus.BorderWidthRight = focus.BorderWidthTop = focus.BorderWidthBottom = 2;
        edit.AddThemeStyleboxOverride("focus", focus); return edit;
    }

    private static void CharacterClear(Node node)
    {
        foreach (var child in node.GetChildren()) { node.RemoveChild(child); child.QueueFree(); }
    }

    private static TextureRect CharacterIcon(Texture2D? texture, int size = 64) => new()
    {
        Texture = texture, CustomMinimumSize = new Vector2(size, size), MouseFilter = MouseFilterEnum.Ignore,
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
    };

    private void setup_character_redesign()
    {
        foreach (var name in new[] { "Backdrop", "Header", "LeftRail", "RightRail", "Content" }) GetNodeOrNull<CanvasItem>(name)?.Hide();
        _characterCanvas = new Control { Name = "CharacterDesignCanvas", MouseFilter = MouseFilterEnum.Stop };
        AddChild(_characterCanvas); _characterCanvas.Size = CharacterDesignSize;
        _characterCanvas.Theme = new Theme { DefaultFont = GD.Load<FontFile>(CharacterFont), DefaultFontSize = 21 };
        var backdrop = new TextureRect { Texture = GD.Load<Texture2D>("res://textures/ui/inventory_20261012/approved_shell_v1.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore };
        _characterCanvas.AddChild(backdrop); CharacterRect(backdrop, 0, 0, 1672, 941);
        var back = CharacterButton("←", 30); _characterCanvas.AddChild(back); CharacterRect(back, 28, 18, 52, 48); back.Pressed += hide_panel;
        var backMaterial = new StyleBoxTexture { Texture = GD.Load<Texture2D>("res://textures/ui/gongfa_back_button_v1.png") };
        foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed" }) back.AddThemeStyleboxOverride(state, backMaterial);
        _characterTitle = CharacterText("概览", 32); _characterCanvas.AddChild(_characterTitle); CharacterRect(_characterTitle, 92, 15, 300, 54);
        var inventory = CharacterButton("行囊 ↗", 24); _characterCanvas.AddChild(inventory); CharacterRect(inventory, 1505, 20, 140, 46);
        inventory.Pressed += () => { if (current_card is Character human && GetTree().GetFirstNodeInGroup("storage_panel") is StoragePanel storage) { storage.bind_player(human); hide_panel(); storage.show_panel(); storage.MoveToFront(); } };
        _characterName = CharacterText("无名", 31); _characterRealm = CharacterText("未详", 23); _characterRoot = CharacterText("未详", 21);
        var index = 0;
        foreach (var label in new[] { _characterName, _characterRealm, _characterRoot })
        { label.HorizontalAlignment = HorizontalAlignment.Center; label.ClipText = true; label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis; _characterCanvas.AddChild(label); CharacterRect(label, 30, 98 + index++ * 42, 214, 40); label.AddThemeColorOverride("font_color", new Color("eee8d7")); }
        var icons = GD.Load<Texture2D>("res://textures/ui/infopanel_nav_icons_ink_v1.png");
        var names = new[] { "概览", "属性", "战斗", "功法", "装备", "关系" };
        var regions = new[] { -1, 0, 1, 2, 3, 5 };
        for (var i = 0; i < names.Length; i++)
        {
            var page = i; var button = CharacterButton("     " + names[i], 30); _characterCanvas.AddChild(button);
            CharacterRect(button, 34, 229 + i * 77, 208, 62);
            foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" })
                button.AddThemeColorOverride(state, new Color("eee8d7"));
            if (regions[i] >= 0 && icons is not null)
            {
                var icon = CharacterIcon(new AtlasTexture { Atlas = icons, Region = new Rect2(regions[i] % 3 * 512, regions[i] / 3 * 512, 512, 512) }, 38);
                button.AddChild(icon); CharacterRect(icon, 17, 12, 38, 38);
            }
            else
            {
                var sheet = GD.Load<Texture2D>("res://textures/ui/inventory_20261012/category_icons_v1.png");
                var icon = CharacterIcon(new AtlasTexture { Atlas = sheet, Region = new Rect2(sheet.GetWidth() / 2f, sheet.GetHeight() * 2f / 3f, sheet.GetWidth() / 4f, sheet.GetHeight() / 3f) }, 38);
                button.AddChild(icon); CharacterRect(icon, 17, 12, 38, 38);
            }
            button.Pressed += () => { if (_npc_read_only) render_npc_page(); else render_new_page(page); };
            _characterTabs.Add(button);
        }
        if (_staticOverviewPage is not null)
        {
            var overviewScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, ClipContents = true };
            _characterCanvas.AddChild(overviewScroll); CharacterRect(overviewScroll, 282, 105, 1350, 795);
            _staticOverviewPage.Reparent(overviewScroll, false); _characterPages[0] = overviewScroll;
            if (_overviewWorldStatusSection is not null && _overviewWorldStatusSection.GetParent() == _staticOverviewPage)
                _staticOverviewPage.MoveChild(_overviewWorldStatusSection, _staticOverviewPage.GetChildCount() - 1);
            _appearancePortraitFrame!.CustomMinimumSize = new Vector2(370, 550);
            _appearancePortraitFrame.SizeFlagsVertical = SizeFlags.ExpandFill;
            _appearanceEntry!.AddThemeStyleboxOverride("panel", CharacterStyle(false, 14));
            if (_appearanceEntry.GetNodeOrNull<HBoxContainer>("OverviewRow") is { } row)
            {
                row.AddThemeConstantOverride("separation", 20);
                if (row.GetNodeOrNull<VBoxContainer>("OverviewDetails") is { } details) details.AddThemeConstantOverride("separation", 12);
                if (row.GetNodeOrNull<GridContainer>("OverviewDetails/OverviewFacts") is { } facts) facts.AddThemeConstantOverride("v_separation", 10);
            }
            _overviewName?.AddThemeFontSizeOverride("font_size", 30);
            if (_overviewMoodSection is not null) _overviewMoodSection.AddThemeConstantOverride("separation", 8);
            if (_overviewMoodScale is not null) _overviewMoodScale.CustomMinimumSize = new Vector2(360, 340);
            if (_overviewWorldStatusSection is not null)
            {
                _overviewWorldStatusSection.CustomMinimumSize = new Vector2(0, 110);
                _overviewWorldStatusSection.SizeFlagsVertical = SizeFlags.ExpandFill;
            }
            if (_overviewWorldStatusScroll is not null) _overviewWorldStatusScroll.CustomMinimumSize = new Vector2(0, 40);
        }
        for (var i = 1; i < 6; i++)
        {
            var page = new Control { Name = "CharacterPage" + i, ClipContents = true };
            _characterCanvas.AddChild(page); CharacterRect(page, 282, 105, 1350, 795); _characterPages[i] = page;
        }
        Resized += resize_character_design; resize_character_design();
    }

    private void resize_character_design()
    {
        if (_characterCanvas is null) return;
        var scale = Mathf.Min(Size.X / CharacterDesignSize.X, Size.Y / CharacterDesignSize.Y);
        _characterCanvas.Scale = Vector2.One * scale;
        _characterCanvas.Position = (Size - CharacterDesignSize * scale) / 2;
        hide_tooltip();
    }

    private void render_character_redesign(int page, Creature unit)
    {
        hide_tooltip(); _moodDetailsOverlay?.Hide(); _gongfaModal?.Hide();
        _activeNewPage = page;
        var identity = unit.GetInstanceId().ToString();
        if (_characterIdentity != identity)
        {
            clear_skill_rewrite_drag_state();
            _characterIdentity = identity; _viewedGongfa = null; _manualQuery = ""; _manualFilter = "全部"; _manualKeyword = ""; _skillQuery = ""; _skillSource = ""; _skillKeyword = ""; _battleFilter = "all"; _showSkillNodeNetwork = false;
            _selectedSkillRewriteId = _pendingSkillRewriteId = _activeSkillRewriteId = _skillRewriteFeedback = "";
            _characterDropTarget = -1;
        }
        _characterTitle!.Text = new[] { "概览", "属性", "战斗", "功法", "装备", "关系" }[page];
        _characterName!.Text = unit.entity_data?.entity_name ?? "无名";
        _characterRealm!.Text = unit.cultivation_realm;
        _characterRoot!.Text = (unit as Character)?.spirit_root_name ?? "未详";
        foreach (var entry in _characterPages) entry.Value.Visible = entry.Key == page;
        for (var i = 0; i < _characterTabs.Count; i++)
        {
            _characterTabs[i].Show();
            var material = i == page ? (StyleBox)new StyleBoxTexture { Texture = _selectedTabPlaque } : new StyleBoxEmpty();
            foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed" }) _characterTabs[i].AddThemeStyleboxOverride(state, material);
            foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" })
                _characterTabs[i].AddThemeColorOverride(state, i == page ? WorldHudStyle.Ink : new Color("eee8d7"));
        }
        switch (page)
        {
            case 0: clear_runtime_body(); _staticOverviewPage?.Show(); populate_overview_page(unit, unit as Character); if (_overviewName is not null) _overviewName.Text = "身份与经历"; break;
            case 1: build_character_attributes(unit); break;
            case 2: build_character_battle(unit); break;
            case 3: if (unit is Character human) build_character_gongfa(human); else CharacterEmpty(_characterPages[3], "当前对象没有秘籍资料"); break;
            case 4: build_character_equipment(unit as Character); break;
            case 5: build_character_relations(); break;
        }
    }

    private static void CharacterEmpty(Control parent, string text)
    {
        CharacterClear(parent); var empty = CharacterText(text, 26, true); parent.AddChild(empty);
        empty.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); empty.HorizontalAlignment = HorizontalAlignment.Center;
    }

    private void build_character_reader()
    {
        _readerPanel = new PanelContainer { Name = "CharacterHover", ZIndex = 750, MouseFilter = MouseFilterEnum.Stop };
        _readerPanel.AddThemeStyleboxOverride("panel", new StyleBoxEmpty()); AddChild(_readerPanel);
        _readerPanel.AddChild(new InventoryPaperFrame { Dark = true });
        var margin = new MarginContainer();
        foreach (var side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 18);
        _readerPanel.AddChild(margin);
        var box = CharacterBox(margin); var heading = new HBoxContainer(); heading.AddThemeConstantOverride("separation", 16); box.AddChild(heading);
        _readerIcon = CharacterIcon(null, 72); heading.AddChild(_readerIcon);
        _readerTitle = CharacterParagraph("", 27); _readerTitle.SizeFlagsHorizontal = SizeFlags.ExpandFill; heading.AddChild(_readerTitle);
        _readerTitle.AddThemeColorOverride("font_color", new Color("f0ead7"));
        var rule = new HSeparator(); rule.AddThemeStyleboxOverride("separator", new StyleBoxFlat { BgColor = new Color("9c967c"), ContentMarginTop = 1 });
        box.AddChild(rule); _readerScroll = CharacterScroll(box);
        var scrollbar = _readerScroll.GetVScrollBar(); scrollbar.CustomMinimumSize = new Vector2(8, 0);
        scrollbar.AddThemeStyleboxOverride("scroll", new StyleBoxFlat { BgColor = new Color("262b23"), ContentMarginLeft = 3, ContentMarginRight = 3 });
        foreach (var state in new[] { "grabber", "grabber_highlight", "grabber_pressed" })
            scrollbar.AddThemeStyleboxOverride(state, new StyleBoxFlat { BgColor = new Color(state == "grabber" ? "8f9179" : "c5ad77"), CornerRadiusTopLeft = 2, CornerRadiusTopRight = 2, CornerRadiusBottomLeft = 2, CornerRadiusBottomRight = 2 });
        _readerText = new RichTextLabel { BbcodeEnabled = true, FitContent = true, ScrollActive = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        _readerText.AddThemeFontOverride("normal_font", GD.Load<FontFile>(CharacterFont));
        _readerText.AddThemeFontOverride("bold_font", GD.Load<FontFile>(CharacterFont));
        _readerText.AddThemeFontSizeOverride("normal_font_size", 19); _readerText.AddThemeColorOverride("default_color", new Color("ded8c6"));
        _readerText.AddThemeColorOverride("font_selected_color", new Color("fff4d8"));
        _readerText.AddThemeConstantOverride("line_separation", 5); _readerScroll.AddChild(_readerText);
        _readerPanel.MouseExited += async () => { var ticket = ++_readerTicket; await ToSignal(GetTree().CreateTimer(.18), SceneTreeTimer.SignalName.Timeout); if (ticket == _readerTicket && GodotObject.IsInstanceValid(this) && (_readerAnchor is null || !_readerAnchor.GetGlobalRect().HasPoint(GetGlobalMousePosition())) && !_readerPanel.GetGlobalRect().HasPoint(GetGlobalMousePosition())) _readerPanel.Hide(); };
        _readerPanel.MouseEntered += () => ++_readerTicket; _readerPanel.Hide();
    }

    private void CharacterHover(Control anchor, string title, string body, Texture2D? icon = null)
    {
        HoverInfoManager.SetInfo(anchor, new HoverInfoManager.HoverInfoContent(title, body, icon));
    }

    private void show_character_reading(Control? anchor, string title, string body, Texture2D? icon = null)
    {
        if (_readerPanel is null || _readerText is null || !IsVisibleInTree() || string.IsNullOrWhiteSpace(body)) return;
        _readerAnchor = anchor; _readerTitle!.Text = title; _readerIcon!.Texture = icon; _readerIcon.Visible = icon is not null;
        PlayerInfoText.Bind(_readerText, body.Replace("[color=#2f6f3a]", "[color=#acc8a0]").Replace("[color=#9b3027]", "[color=#d4a08d]")); _readerScroll!.ScrollVertical = 0;
        HoverInfoManager.SetInfo(_readerText, new HoverInfoManager.HoverInfoContent(title, body, icon));
        var scale = Mathf.Clamp(Size.Y / 941f, .7f, 1.3f); _readerPanel.Scale = Vector2.One * scale;
        var width = Mathf.Min(460, (Size.X - 24) / scale);
        _readerText.CustomMinimumSize = new Vector2(width - 36, 0);
        _readerScroll.CustomMinimumSize = new Vector2(0, 40);
        _readerPanel.Size = new Vector2(width, 130); _readerPanel.Show(); _readerPanel.MoveToFront();
        CallDeferred(MethodName.place_character_reader);
    }

    private void place_character_reader()
    {
        if (_readerPanel is null || !_readerPanel.Visible) return;
        var scale = _readerPanel.Scale.X;
        var chromeHeight = Mathf.Max(0, _readerPanel.GetCombinedMinimumSize().Y - _readerScroll!.GetCombinedMinimumSize().Y);
        var maxHeight = Mathf.Max(40, (Size.Y - 24) / scale - chromeHeight);
        _readerScroll!.CustomMinimumSize = new Vector2(0, Mathf.Clamp(_readerText!.GetContentHeight() + 8, 40, maxHeight));
        _readerPanel.Size = new Vector2(_readerPanel.Size.X, 0);
        var bounds = _readerAnchor is not null && GodotObject.IsInstanceValid(_readerAnchor) ? _readerAnchor.GetGlobalRect() : new Rect2(GetGlobalMousePosition(), Vector2.Zero);
        var actual = _readerPanel.GetCombinedMinimumSize() * scale;
        var point = new Vector2(bounds.End.X + 12, bounds.Position.Y);
        if (point.X + actual.X > Size.X - 12) point.X = bounds.Position.X - actual.X - 12;
        if (point.X < 12) point = new Vector2(bounds.Position.X, bounds.End.Y + 12);
        _readerPanel.Position = new Vector2(Mathf.Clamp(point.X, 12, Mathf.Max(12, Size.X - actual.X - 12)), Mathf.Clamp(point.Y, 12, Mathf.Max(12, Size.Y - actual.Y - 12)));
    }
}
