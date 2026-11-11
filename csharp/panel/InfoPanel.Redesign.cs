using Godot;

/// <summary>2026-11-08 纸墨角色七页：设计坐标只负责排版，所有内容来自当前角色。</summary>
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
        var label = CraftingPaperStyle.Text(text, size, secondary);
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
        var rect = new Rect2(x, y, width, height);
        node.SetMeta("character_layout_rect", rect);
        CharacterApplyRect(node, rect);
    }

    private static void CharacterApplyRect(Control node, Rect2 rect)
    {
        var ratio = Vector2.One;
        if (node.GetParent() is Control parent && parent.HasMeta("character_layout_basis"))
            ratio = parent.Size / parent.GetMeta("character_layout_basis").AsVector2();
        node.Position = rect.Position * ratio; node.Size = rect.Size * ratio;
    }

    // Resize page frames without stretching letters, icons or button textures.
    private static void CharacterResponsive(Control node, Vector2 basis)
    {
        node.SetMeta("character_layout_basis", basis);
        node.Resized += () =>
        {
            foreach (var child in node.GetChildren().OfType<Control>())
                if (child.HasMeta("character_layout_rect")) CharacterApplyRect(child, child.GetMeta("character_layout_rect").AsRect2());
        };
    }

    private static StyleBoxTexture CharacterStyle(bool selected = false, int padding = 16)
    {
        if (selected)
        {
            var face = PaperButtonFeedback.Face(CraftingPaperStyle.ButtonKind.Regular, selected: true);
            face.ContentMarginLeft = face.ContentMarginRight = face.ContentMarginTop = face.ContentMarginBottom = padding;
            return face;
        }
        return CharacterPaper(padding: padding);
    }

    private static StyleBoxTexture CharacterButtonStyle(bool selected = false, int padding = 8, bool highlight = false)
    {
        var face = PaperButtonFeedback.Face(CraftingPaperStyle.ButtonKind.Regular,
            highlight ? CraftingPaperStyle.ButtonState.Hover : CraftingPaperStyle.ButtonState.Normal, selected);
        face.ContentMarginLeft = face.ContentMarginRight = padding;
        face.ContentMarginTop = face.ContentMarginBottom = Math.Min(7, padding);
        return face;
    }

    private static Button CharacterButton(string text, int size = 21, bool selected = false, int padding = 8)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(0, 43) };
        PaperButtonFeedback.Apply(button, CraftingPaperStyle.ButtonKind.Regular, size, selected);
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
        var panel = new PanelContainer(); panel.AddThemeStyleboxOverride("panel", height < 110 ? CraftingPaperStyle.Paper(padding, brightness: 1) : CharacterStyle(false, padding));
        parent.AddChild(panel); CharacterRect(panel, x, y, width, height);
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
        CraftingPaperStyle.ApplyInput(edit, 21, 13, 7);
        return edit;
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

    private void setup_character_redesign() => setup_character_paper();

    private void resize_character_design()
    {
        if (_characterCanvas is null) return;
        var scale = Mathf.Max(.01f, Size.Y / CharacterDesignSize.Y);
        _characterCanvas.Scale = Vector2.One * scale;
        _characterCanvas.Position = Vector2.Zero;
        _characterCanvas.Size = Size / scale;
        resize_character_paper();
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
        update_character_paper_header(page, unit);
        foreach (var entry in _characterPages) entry.Value.Visible = entry.Key == page;
        switch (page)
        {
            case 0: clear_runtime_body(); _staticOverviewPage?.Show(); populate_overview_page(unit, unit as Character); break;
            case 1: build_character_attributes(unit); break;
            case 2: build_character_battle(unit); break;
            case 3: if (unit is Character human) build_character_gongfa(human); else CharacterEmpty(_characterPages[3], "当前对象没有秘籍资料"); break;
            case 4: build_character_proficiency(unit as Character); break;
            case 5: build_character_equipment(unit as Character); break;
            case 6: build_character_relations(); break;
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
        _readerPanel.Theme = CraftingPaperStyle.MakeTheme();
        _readerPanel.AddThemeStyleboxOverride("panel", CharacterPaper(padding: 0, dark: true)); AddChild(_readerPanel);
        var margin = new MarginContainer();
        foreach (var side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 27);
        _readerPanel.AddChild(margin);
        var box = CharacterBox(margin); var heading = new HBoxContainer(); heading.AddThemeConstantOverride("separation", 16); box.AddChild(heading);
        _readerIcon = CharacterIcon(null, 72); heading.AddChild(_readerIcon);
        _readerTitle = CharacterParagraph("", 27); _readerTitle.SizeFlagsHorizontal = SizeFlags.ExpandFill; heading.AddChild(_readerTitle);
        _readerTitle.AddThemeColorOverride("font_color", new Color("f0ead7"));
        var rule = new HSeparator(); rule.AddThemeStyleboxOverride("separator", new StyleBoxFlat { BgColor = new Color("9c967c"), ContentMarginTop = 1 });
        box.AddChild(rule); _readerScroll = CharacterScroll(box);
        _readerScroll.GetVScrollBar().CustomMinimumSize = new Vector2(13, 0);
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
        if (anchor.MouseFilter == Control.MouseFilterEnum.Ignore)
            anchor.MouseFilter = Control.MouseFilterEnum.Pass;
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
        _readerText.CustomMinimumSize = new Vector2(width - 54, 0);
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
