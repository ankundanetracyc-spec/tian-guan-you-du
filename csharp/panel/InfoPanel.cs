using Godot;

/// <summary>
/// 人物档案与角色管理。正式入口沿用 InfoPanel.tscn，认可版布局由各部分文件生成原生控件。
/// 场景中的旧版字段保留兼容与属性说明来源，角色值、秘籍和装备均读取运行时数据。
/// </summary>
public partial class InfoPanel : Control
{
    public event Action<WorldNpcData>? NpcTradeRequested;
    public event Action<WorldNpcData>? NpcTalkRequested;
    public event Action<WorldNpcData>? NpcSparRequested;
    public event Action<WorldNpcData>? NpcPurchaseRequested;
    public Func<WorldNpcData, bool>? NpcSparAvailable { get; set; }
    public Func<WorldNpcData, bool>? NpcPurchaseAvailable { get; set; }
    public event Action? AppearanceChanged;
    public WorldEntity? current_card { get; private set; }

    // 人物页中间栏采用两排各五槽，卡面沿用局内原始比例，避免固定槽挤压两侧信息栏。
    private const float BattleLoadoutScale = 1f;
    private const float BattleLoadoutSlotWidth = CombatSkillBar.SlotWidth * BattleLoadoutScale;
    private const float BattleLoadoutSlotHeight = CombatSkillBar.SlotHeight * BattleLoadoutScale;
    private const float BattleLoadoutFrameWidth = CombatSkillBar.FrameWidth * BattleLoadoutScale;
    private const float BattleLoadoutFrameHeight = CombatSkillBar.FrameHeight * BattleLoadoutScale;
    private const float BattleLoadoutFrameTop = CombatSkillBar.FrameTop * BattleLoadoutScale;
    private const float BattleLoadoutActionHeight = 28f;

    private Control? _scrollBody;
    private ScrollContainer? _profilePage;
    private ScrollContainer? _gongfaPage, _equipPage, _relationsPage;
    private Button? _scheduleButton;
    private Control? _profileSection, _attributeSection, _appearancePreview;
    private WardrobePicker? _wardrobe;
    private PanelContainer? _appearanceEntry;
    private PanelContainer? _appearancePortraitFrame;
    private PortraitRenderer? _profilePortraitRenderer;
    private Label? _overviewName, _overviewRenownValue, _overviewRenownBreakdown, _overviewKarmaValue, _overviewMoodSummary;
    private VBoxContainer? _overviewMoodSection;
    private MoodDiscControl? _overviewMoodScale;
    private Label? _overviewMoodReason;
    private readonly Dictionary<string, Label> _overviewValueLabels = [];
    private PanelContainer? _tooltipPanel;
    private Label? _tooltipLabel;
    private Label? _fullscreenName, _fullscreenSummary, _fullscreenSchedule;
    private readonly Button[] _tabButtons = new Button[6];
    private readonly TextureRect[] _tabInks = new TextureRect[6];
    private readonly List<GongfaResource> _gongfaBooks = [];
    private bool _newLayout; private VBoxContainer? _newBody;
    private Control? _staticOverviewPage;
    private Control? _staticAttributePage;
    private Control? _staticGongfaPage;
    private Control? _staticBattlePage;
    private string _battleFilter = "all";
    private string _battleSelectedPath = "";
    private GridContainer? _battleSlotGrid;
    private VBoxContainer? _battleSkillList;
    private Label? _battleSkillCounter;
    private Label? _battleDetailTitle;
    private Label? _battleDetailMeta;
    private RichTextLabel? _battleDetailText;
    private Control? _gongfaModal;
    private Label? _gongfaModalTitle;
    private Label? _gongfaModalBody;
    private VBoxContainer? _gongfaModalChoices;
    private Button? _gongfaBranchActivate;
    private int _gongfaSelectedChapter = -1;
    private string _gongfaSelectedBranch = "";
    private GongfaResource? _viewedGongfa;
    private bool _viewingSubGongfa;
    private string _gongfaPickerSlot = "";
    private readonly List<(Button button, Label caption)> _newTabs = [];
    private Texture2D? _selectedTabPlaque;
    private int _activeNewPage;
    private float _livePageRefresh;
    private string _gongfaModalMode = "";
    private WorldNpcData? _npc_card;
    private VBoxContainer? _npc_page;
    private bool _npc_read_only;

    public override void _Process(double delta)
    {
        if (!_newLayout || !Visible || _activeNewPage != 3 || current_card is not Character human) return;
        _livePageRefresh += (float)delta; if (_livePageRefresh < .25f) return; _livePageRefresh = 0;
        // 研读进度只更新固定文字。周期性重建整页会销毁悬停中的按钮。
        refresh_gongfa_live_study_state(human);
    }

    public override void _Ready()
    {
        AddToGroup("human_info_panel");
        var characterPage = GetNodeOrNull<ScrollContainer>("Content/Page");
        if (characterPage is not null) characterPage.SizeFlagsVertical = SizeFlags.ExpandFill;
        _newBody = GetNodeOrNull<VBoxContainer>("Content/Page/Body");
        if (_newBody is not null) _newBody.SizeFlagsVertical = SizeFlags.ExpandFill;
        if (_newBody is not null) { _newLayout = true; _staticOverviewPage = GetNodeOrNull<Control>("Content/Page/Body/OverviewLayout"); _staticAttributePage = GetNodeOrNull<Control>("Content/Page/Body/AttributeLayout"); _staticBattlePage = GetNodeOrNull<Control>("Content/Page/Body/BattleLayout"); _staticGongfaPage = GetNodeOrNull<Control>("Content/Page/Body/GongfaLayout"); setup_new_layout(); setup_overview_layout(); build_gongfa_modal(); build_tooltip(); setup_character_redesign(); Hide(); return; }
        // 根节点覆盖全屏只用于定位卷轴，不能截走卷轴外的地图点击。
        MouseFilter = MouseFilterEnum.Ignore;
        _scrollBody = GetNodeOrNull<Control>("ScrollBody");
        _profilePage = GetNodeOrNull<ScrollContainer>("ScrollBody/ContentPanel/ScrollContainer");
        _gongfaPage = GetNodeOrNull<ScrollContainer>("ScrollBody/ContentPanel/GongfaContainer");
        _equipPage = GetNodeOrNull<ScrollContainer>("ScrollBody/ContentPanel/EquipContainer");
        _relationsPage = GetNodeOrNull<ScrollContainer>("ScrollBody/ContentPanel/RelationsContainer");
        _profileSection = GetNodeOrNull<Control>("ScrollBody/ContentPanel/ScrollContainer/VBoxContainer2/AttrProfileRow");
        _attributeSection = GetNodeOrNull<Control>("ScrollBody/ContentPanel/ScrollContainer/VBoxContainer2/AttributesContent");
        _appearancePreview = GetNodeOrNull<Control>("ScrollBody/ContentPanel/ScrollContainer/VBoxContainer2/AttrProfileRow/AppearancePreview");
        // 当前版本不用人物立绘；同时清除它的布局占位，避免档案内容被挤成两栏。
        if (_appearancePreview is not null) { _appearancePreview.Hide(); _appearancePreview.CustomMinimumSize = Vector2.Zero; _appearancePreview.SizeFlagsHorizontal = SizeFlags.ShrinkBegin; }
        if (_profilePage is not null)
        {
            // 卷轴正文严格裁在内容区内；小分辨率时用滚动条承载，不让控件越过画芯。
            _profilePage.ClipContents = true;
            _profilePage.HorizontalScrollMode = ScrollContainer.ScrollMode.Auto;
            _profilePage.VerticalScrollMode = ScrollContainer.ScrollMode.Auto;
        }

        bind_tab("TabProfile", 0); bind_tab("TabAttr", 1); bind_tab("TabGongfa", 2);
        bind_tab("TabEquip", 3); bind_tab("TabRelations", 4);
        configure_fullscreen_layout();
        add_runtime_actions();
        bind_gongfa_controls();
        build_tooltip();
        Hide();
    }

    private void bind_tab(string name, int page)
    {
        var button = GetNodeOrNull<Button>($"ScrollBody/ContentPanel/TabBar/{name}Wrapper/{name}");
        var ink = GetNodeOrNull<TextureRect>($"ScrollBody/ContentPanel/TabBar/{name}Wrapper/InkBg_{name}");
        if (button is null) return;
        _tabButtons[page] = button; _tabInks[page] = ink!;
        button.ToggleMode = false;
        button.Pressed += () => show_page(page);
    }

    private void add_runtime_actions()
    {
        var content = GetNodeOrNull<Control>("ScrollBody/ContentPanel");
        if (content is null) return;
        var close = new Button { Name = "RuntimeClose", Text = "×", MouseFilter = MouseFilterEnum.Stop, Flat = true };
        close.SetAnchorsPreset(LayoutPreset.TopLeft);
        close.OffsetLeft = 26; close.OffsetTop = 20; close.OffsetRight = 76; close.OffsetBottom = 66;
        close.AddThemeFontSizeOverride("font_size", 28); close.AddThemeColorOverride("font_color", new Color(.25f, .25f, .25f));
        close.Pressed += hide_panel; content.AddChild(close);

        var profileBox = GetNodeOrNull<VBoxContainer>("ScrollBody/ContentPanel/ScrollContainer/VBoxContainer2/AttrProfileRow/AttrProfileInfoBox");
        if (profileBox is null) return;
    }

    public Rect2 get_ui_rect() => _scrollBody?.GetGlobalRect() ?? GetGlobalRect();

    public void show_entity(WorldEntity entity)
    {
        if (!GodotObject.IsInstanceValid(entity)) return;
        hide_tooltip(); _moodDetailsOverlay?.Hide(); _overviewIsPreview=false;
        leave_npc_mode();
        if (_newLayout) { current_card = entity; populate(entity); ZIndex = 20; Show(); render_new_page(0); MoveToFront(); return; }
        current_card = entity;
        populate(entity);
        ZIndex = 20; Show(); _scrollBody?.Show(); show_page(0); MoveToFront();
    }

    /// <summary>世界人物沿用同一张人物档案，但只显示已知资料，不开放主角专属页面。</summary>
    public void show_npc(WorldNpcData npc)
    {
        if (!GodotObject.IsInstanceValid(npc) || !_newLayout || _newBody is null) return;
        npc.ensure_appearance_identity();
        leave_npc_mode();
        current_card = null;
        _npc_card = npc;
        _npc_read_only = true;
        configure_npc_layout();
        ZIndex = 300;
        Show();
        render_npc_page();
        MoveToFront();
    }

    public void hide_panel()
    {
        clear_skill_rewrite_drag_state();
        _readerPanel?.Hide(); _gongfaModal?.Hide();
        _tooltipPanel?.Hide();
        _moodDetailsOverlay?.Hide();
        leave_npc_mode();
        Hide();
        current_card = null;
    }


    private void configure_npc_layout()
    {
        var title = GetNodeOrNull<Label>("Header/Title");
        if (title is not null) title.Text = "概览";
        set_label("NameValue", string.IsNullOrWhiteSpace(_npc_card?.npc_name) ? "无名" : _npc_card!.npc_name);
        set_label("RealmValue", string.IsNullOrWhiteSpace(_npc_card?.cultivation_realm) ? "境界未详" : _npc_card!.cultivation_realm);
        set_label("SpiritrootValue", string.IsNullOrWhiteSpace(_npc_card?.spirit_root_name) ? "灵根未详" : _npc_card!.spirit_root_name);
        set_tab_mode(true);
        _staticOverviewPage?.Hide();
        _staticAttributePage?.Hide();
        _staticBattlePage?.Hide();
        _staticGongfaPage?.Hide();
        clear_runtime_body();
    }

    private void set_tab_mode(bool npc_mode)
    {
        var tabs = new[] { "TabOverview", "TabAttr", "TabBattle", "TabGongfa", "TabEquip", "TabRelations" };
        var captions = new[] { "概览", "属性", "战斗", "功法", "装备", "关系" };
        for (var i = 0; i < tabs.Length; i++)
        {
            var tab = GetNodeOrNull<Button>($"LeftRail/Box/Nav/{tabs[i]}");
            if (tab is null) continue;
            tab.Visible = !npc_mode || i == 0;
            var caption = tab.GetNodeOrNull<Label>("Caption");
            if (caption is not null) caption.Text = captions[i];
        }
    }

    private void leave_npc_mode()
    {
        if (!_npc_read_only && _npc_card is null) return;
        _npc_card = null;
        _npc_read_only = false;
        _npc_page = null;
        if (!_newLayout) return;
        var title = GetNodeOrNull<Label>("Header/Title");
        if (title is not null) title.Text = "概览";
        set_tab_mode(false);
        clear_runtime_body();
    }

    private void clear_runtime_body()
    {
        if (_staticOverviewPage is not null)
            foreach (var child in _staticOverviewPage.GetChildren().Where(child => child.Name == "NpcSupplementMargin").ToList()) { _staticOverviewPage.RemoveChild(child); child.QueueFree(); }
        if (_newBody is null) return;
        foreach (var child in _newBody.GetChildren())
            if (child != _staticOverviewPage && child != _staticAttributePage && child != _staticBattlePage && child != _staticGongfaPage)
                child.QueueFree();
        _npc_page = null;
    }

    private void render_npc_page() => render_shared_npc_overview();

    private static string npc_faction_name(string faction_id) => faction_id switch
    {
        "yuxu_xianzong" or "yuxu_sect" or "yuxu" => "玉虚仙宗",
        "fulan_temple" => "伏岚观",
        "lanpu_lu" => "澜浦陆氏",
        "eastwood_hunters" => "东林行猎会",
        "tianji_branch" => "天机阁分支",
        "white_stone_town" => "白石城",
        "herb_valley_gatherers" => "灵草谷采药会",
        "wuxiang_shengzong" => "无相圣宗",
        _ => string.IsNullOrWhiteSpace(faction_id) ? "无明确归属" : faction_id,
    };

    /// <summary>供统一返回键优先收起人物页内的临时查看层。</summary>
    public bool dismiss_top_overlay()
    {
        if (_moodDetailsOverlay?.Visible == true) { _moodDetailsOverlay.Hide(); return true; }
        if (_gongfaModal?.Visible == true) { _gongfaModal.Hide(); return true; }
        if (HoverInfoManager.DismissVisible()) { hide_tooltip(); return true; }
        if (_readerPanel?.Visible == true) { hide_tooltip(); return true; }
        if (_tooltipPanel?.Visible == true) { _tooltipPanel.Hide(); return true; }
        return false;
    }

    private void show_page(int page)
    {
        if (_newLayout) { render_new_page(page); return; }
        if (current_card is null) return;
        if (_profilePage is not null) _profilePage.Visible = page is 0 or 1;
        if (_profileSection is not null) _profileSection.Visible = page == 0;
        if (_attributeSection is not null) _attributeSection.Visible = page == 1;
        if (_gongfaPage is not null) _gongfaPage.Visible = page == 2;
        if (_equipPage is not null) _equipPage.Visible = page == 3;
        if (_relationsPage is not null) _relationsPage.Visible = page == 4;
        for (var i = 0; i < _tabButtons.Length; i++)
        {
            var selected = i == page;
            if (_tabButtons[i] is not null) _tabButtons[i].AddThemeColorOverride("font_color", selected ? new Color(.12f, .095f, .065f) : new Color(.349f, .349f, .349f));
            if (_tabInks[i] is not null) { _tabInks[i].Visible = selected; _tabInks[i].Scale = Vector2.One; }
        }
        if (page == 2) populate_gongfa(current_card as Character);
        if (page == 3) populate_equip(current_card as Character);
        if (page == 4) populate_relations();
    }

    private void setup_new_layout()
    {
        var nav = GetNodeOrNull<VBoxContainer>("LeftRail/Box/Nav");
        if (nav is not null && nav.GetNodeOrNull<Button>("TabOverview") is null)
        {
            var overview = new Button { Name = "TabOverview", Text = "概览", CustomMinimumSize = new Vector2(232, 64), Flat = true };
            nav.AddChild(overview);
            nav.MoveChild(overview, 0);
        }
        var paths = new[] { "TabOverview", "TabAttr", "TabBattle", "TabGongfa", "TabEquip", "TabRelations" };
        _selectedTabPlaque = GD.Load<Texture2D>("res://textures/ui/infopanel_nav_plaque_clean_v2.png");
        for (var i = 0; i < paths.Length; i++)
        {
            var page = i; var button = GetNodeOrNull<Button>($"LeftRail/Box/Nav/{paths[i]}")!;
            var authoredCaption = button.GetNodeOrNull<Label>("Caption");
            var caption = authoredCaption?.Text ?? button.Text; button.Text = ""; button.Icon = null;
            button.RemoveThemeStyleboxOverride("normal"); button.CustomMinimumSize = new Vector2(232, 64); button.SizeFlagsHorizontal = SizeFlags.ShrinkCenter; button.Flat = true;
            var plaque = button.GetNodeOrNull<TextureRect>("SelectedPlaque");
            if (plaque is null)
            {
                plaque = new TextureRect { Name = "SelectedPlaque", Texture = _selectedTabPlaque, MouseFilter = MouseFilterEnum.Ignore, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, ShowBehindParent = true, Visible = false };
                plaque.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); button.AddChild(plaque);
            }
            var text = authoredCaption;
            if (text is null)
            {
                text = new Label { Name = "Caption", Text = caption, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
                text.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); text.AddThemeFontSizeOverride("font_size", 30); button.AddChild(text);
            }
            // 场景原有 Caption 默认会截获鼠标，导致标签看起来能点、实际按钮收不到点击。
            text.MouseFilter = MouseFilterEnum.Ignore;
            _newTabs.Add((button, text));
            button.Pressed += () => render_new_page(page);
        }
        GetNodeOrNull<Button>("Header/Close")!.Pressed += hide_panel;
        GetNodeOrNull<Button>("RightRail/Box/Schedule")?.Hide();
    }

    private void setup_overview_layout()
    {
        if (_staticOverviewPage is null) return;
        _staticOverviewPage.GetNodeOrNull<Label>("Title")?.Hide();
        _appearanceEntry = new PanelContainer { Name = "AppearanceEntry", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _appearanceEntry.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = Colors.Transparent, BorderColor = new Color(.35f, .28f, .17f, .42f),
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
            ContentMarginLeft = 26, ContentMarginRight = 26, ContentMarginTop = 30, ContentMarginBottom = 24,
        });
        _appearanceEntry.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        var row = new HBoxContainer { Name="OverviewRow", SizeFlagsVertical = SizeFlags.ExpandFill }; row.AddThemeConstantOverride("separation", 34); _appearanceEntry.AddChild(row);
        var leftColumn = new VBoxContainer { Name = "OverviewPortraitColumn", CustomMinimumSize = new Vector2(370, 0), SizeFlagsVertical = SizeFlags.ExpandFill };
        row.AddChild(leftColumn);
        _appearancePortraitFrame = new PanelContainer { Name = "PortraitFrame", CustomMinimumSize = new Vector2(370, 690), SizeFlagsVertical = SizeFlags.ShrinkBegin };
        _appearancePortraitFrame.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(.88f, .84f, .74f, .72f), BorderColor = new Color(.35f, .28f, .17f, .42f), BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1, ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 10, ContentMarginBottom = 10 });
        leftColumn.AddChild(_appearancePortraitFrame);
        _profilePortraitRenderer = new PortraitRenderer { Name = "ProfilePortrait", CustomMinimumSize = new Vector2(330, 500), SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        _appearancePortraitFrame.AddChild(_profilePortraitRenderer);

        var details = new VBoxContainer { Name="OverviewDetails", CustomMinimumSize=new Vector2(350,0), SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsStretchRatio=.95f };
        details.AddThemeConstantOverride("separation", 18); row.AddChild(details);
        _overviewName = new Label { Name = "OverviewName", Text = "无名", AutowrapMode=TextServer.AutowrapMode.WordSmart, MouseFilter = MouseFilterEnum.Ignore };
        _overviewName.AddThemeFontSizeOverride("font_size", 38); _overviewName.AddThemeColorOverride("font_color", new Color(.13f, .11f, .08f)); details.AddChild(_overviewName);
        var facts = new GridContainer { Name = "OverviewFacts", Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        facts.AddThemeConstantOverride("h_separation", 22); facts.AddThemeConstantOverride("v_separation", 18); details.AddChild(facts);
        foreach (var field in new[] { (key: "gender", title: "性别"), (key: "age", title: "年龄"), (key: "species", title: "种族"), (key: "realm", title: "境界"), (key: "spirit_root", title: "灵根"), (key: "identity", title: "身份"), (key: "faction", title: "所属势力"), (key: "location", title: "当前位置") })
        {
            var name = new Label { Text = field.title, CustomMinimumSize = new Vector2(78, 0), MouseFilter = MouseFilterEnum.Ignore };
            name.AddThemeFontSizeOverride("font_size", 21); name.AddThemeColorOverride("font_color", new Color(.34f, .29f, .20f)); facts.AddChild(name);
            var value = new Label { Name = field.key, Text = "未详", AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
            value.AddThemeFontSizeOverride("font_size", 22); value.AddThemeColorOverride("font_color", new Color(.13f, .11f, .08f)); facts.AddChild(value);
            _overviewValueLabels[field.key] = value;
        }
        details.AddChild(new HSeparator());
        _overviewRenownValue = new Label { Name = "OverviewRenown", Text = "声望：百川 未详", AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = MouseFilterEnum.Ignore };
        _overviewRenownValue.AddThemeFontSizeOverride("font_size", 19); _overviewRenownValue.AddThemeColorOverride("font_color", new Color(.16f, .14f, .10f)); details.AddChild(_overviewRenownValue);
        _overviewRenownBreakdown = new Label { Name = "OverviewRenownBreakdown", Text = "", AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = MouseFilterEnum.Ignore };
        _overviewRenownBreakdown.AddThemeFontSizeOverride("font_size", 14); _overviewRenownBreakdown.AddThemeColorOverride("font_color", new Color(.36f, .32f, .24f)); details.AddChild(_overviewRenownBreakdown);
        _overviewKarmaValue = new Label { Name = "OverviewKarma", Text = "业力：未详", MouseFilter = MouseFilterEnum.Ignore };
        _overviewKarmaValue.AddThemeFontSizeOverride("font_size", 20); _overviewKarmaValue.AddThemeColorOverride("font_color", new Color(.16f, .14f, .10f)); details.AddChild(_overviewKarmaValue);
        _overviewMoodSection = new VBoxContainer { Name = "OverviewMoodSection", CustomMinimumSize=new Vector2(440,0), SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio=1.15f };
        _overviewMoodSection.AddThemeConstantOverride("separation", 18);
        var moodTitle = new Label { Text="心境", MouseFilter=MouseFilterEnum.Ignore };
        moodTitle.AddThemeFontSizeOverride("font_size",30); _overviewMoodSection.AddChild(moodTitle);
        _overviewMoodSection.AddChild(new HSeparator());
        _overviewMoodScale = new MoodDiscControl { Name="MoodDisc", SizeFlagsHorizontal=SizeFlags.ExpandFill };
        bind_mood_disc(_overviewMoodScale);
        _overviewMoodSection.AddChild(_overviewMoodScale);
        var moodHint = new Label { Text="越近圆心，心绪越轻", HorizontalAlignment=HorizontalAlignment.Center, MouseFilter=MouseFilterEnum.Ignore };
        moodHint.AddThemeFontSizeOverride("font_size",18); moodHint.AddThemeColorOverride("font_color",new Color("#81745c")); _overviewMoodSection.AddChild(moodHint);
        _overviewMoodSummary = new Label { Name = "OverviewMoodSummary", Text = "心境：未详", MouseFilter = MouseFilterEnum.Ignore };
        _overviewMoodSummary.AddThemeFontSizeOverride("font_size", 20); _overviewMoodSummary.AddThemeColorOverride("font_color", new Color(.16f, .14f, .10f)); _overviewMoodSection.AddChild(_overviewMoodSummary);
        _overviewMoodReason = new Label { Text="暂无已确认的长期心绪记录。", AutowrapMode=TextServer.AutowrapMode.WordSmart, MouseFilter=MouseFilterEnum.Ignore };
        _overviewMoodReason.AddThemeFontSizeOverride("font_size",18); _overviewMoodReason.AddThemeColorOverride("font_color",new Color("#756c59")); _overviewMoodSection.AddChild(_overviewMoodReason);
        row.AddChild(_overviewMoodSection);
        var moodDetails=new Button { Name="MoodDetails",Text="查看心境详情",Flat=true,CustomMinimumSize=new Vector2(0,42),SizeFlagsHorizontal=SizeFlags.ShrinkCenter };
        moodDetails.AddThemeFontSizeOverride("font_size",20); moodDetails.AddThemeColorOverride("font_color",new Color("#62543b"));
        _overviewMoodSection.AddChild(moodDetails); moodDetails.Pressed+=open_mood_details;
        _staticOverviewPage.AddChild(_appearanceEntry);
        _staticOverviewPage.MoveChild(_appearanceEntry, Math.Min(1, _staticOverviewPage.GetChildCount() - 1));
        build_overview_world_status_section();

        _wardrobe = new WardrobePicker { Name = "WardrobePicker" };
        _wardrobe.ClothingApplied += () => { refresh_profile_portrait(); AppearanceChanged?.Invoke(); render_new_page(_activeNewPage); };
        AddChild(_wardrobe); _wardrobe.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    private void update_appearance_entry(Character? character)
    {
        if (_appearanceEntry is null) return;
        bool hasParts = character is not null && has_appearance_assets(character);
        _appearanceEntry.Visible = character is not null;
        if (_profilePortraitRenderer is not null)
        {
            _profilePortraitRenderer.Visible = hasParts;
            if (hasParts && character?.entity_data is HumanData sourceData)
            {
                var data = sourceData.Duplicate(true) as HumanData ?? sourceData;
                var gender = data.gender == 1 ? "female" : "male";
                var appearanceCatalog = character.GetNodeOrNull<AppearanceModCatalog>("/root/AppearanceModCatalog");
                if (appearanceCatalog?.allows_color_tint(gender, "skin") != true) data.appearance_skin_tone = -1;
                if (appearanceCatalog?.allows_color_tint(gender, "hair") != true) data.appearance_hair_color = -1;
                var portraitCatalog = character.GetNode<PortraitCatalog>("/root/PortraitCatalog");
                var context = portraitCatalog.get_context(data, gender, character!.Get("current_age").AsInt32(), data.faction_name, character!.Get("cultivation_realm").AsString());
                var requested = portraitCatalog.get_requested_appearance(data);
                if (data.appearance_hair_back_style < 0) requested["hair_back"] = "";
                _profilePortraitRenderer.configure(data, context, requested);
            }
            else _profilePortraitRenderer.clear_portrait();
        }
        if (_appearancePortraitFrame is not null) _appearancePortraitFrame.Visible = hasParts;
    }

    private void populate_overview_page(Creature unit, Character? human)
    {
        if (_overviewName is not null) _overviewName.Text = unit.entity_name;
        update_appearance_entry(human);
        foreach (var key in _overviewValueLabels.Keys) set_overview_value(key, "未详");
        if (_overviewRenownValue is not null) _overviewRenownValue.Text = "声望：百川 未详";
        if (_overviewRenownBreakdown is not null) _overviewRenownBreakdown.Text = "";
        if (_overviewKarmaValue is not null) _overviewKarmaValue.Text = "业力：未详";
        set_overview_mood(null);
        var data = human?.entity_data as HumanData;
        var currentGameHour = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
        refresh_overview_world_status(data, human?.get_cultivation_bottleneck_summary(), currentGameHour);
        if (human is null) { set_overview_value("identity", "无"); set_overview_value("faction", "无"); return; }
        var gender = !string.IsNullOrWhiteSpace(human.family_gender) ? human.family_gender : data is null ? "未详" : data.gender == 1 ? "女" : "男";
        var identity = !string.IsNullOrWhiteSpace(data?.sect_current_rank) ? data!.sect_current_rank
            : !string.IsNullOrWhiteSpace(data?.family_guest_affiliation_id) ? "家族客卿" : "无";
        var factionId = !string.IsNullOrWhiteSpace(data?.sect_affiliation_id) ? data!.sect_affiliation_id : data?.family_guest_affiliation_id ?? "";
        var faction = string.IsNullOrWhiteSpace(factionId) ? "无" : npc_faction_name(factionId);
        var scene = GetTree().CurrentScene;
        var location = scene is not null && scene.HasMethod("get_profile_location_for")
            ? scene.Call("get_profile_location_for", human).AsString()
            : "位置未详";
        set_overview_value("gender", gender);
        set_overview_value("age", $"{unit.current_age} 岁");
        set_overview_value("species", "人族");
        set_overview_value("realm", string.IsNullOrWhiteSpace(unit.cultivation_realm) ? "未详" : unit.cultivation_realm);
        set_overview_value("spirit_root", string.IsNullOrWhiteSpace(data?.spirit_root_name) ? "未详" : data!.spirit_root_name);
        if (_overviewValueLabels.TryGetValue("spirit_root", out var rootLabel))
            bind_tooltip(rootLabel, format_root_weight_tooltip(data?.root_weights));
        if (find_label(this, "SpiritrootValue") is { } sidebarRoot)
            bind_tooltip(sidebarRoot, format_root_weight_tooltip(data?.root_weights));
        set_overview_value("identity", identity);
        set_overview_value("faction", faction);
        set_overview_value("location", string.IsNullOrWhiteSpace(location) ? "位置未详" : location);
        if (data is null) return;
        var renown = RegionalRenownSystem.get_bai_chuan_total(data);
        if (_overviewRenownValue is not null) _overviewRenownValue.Text = $"声望：百川 {renown}";
        if (_overviewRenownBreakdown is not null) _overviewRenownBreakdown.Text = RegionalRenownSystem.get_bai_chuan_overview_breakdown_text(data);
        if (_overviewKarmaValue is not null) _overviewKarmaValue.Text = $"业力：{format_signed_value(data.karma_value)}";
        // 旧横轴不能推导八种个人心绪；尚未记录圆盘状态时如实显示未详。
        set_overview_mood(null);
    }

    private void set_overview_value(string key, string value)
    {
        if (_overviewValueLabels.TryGetValue(key, out var label)) label.Text = string.IsNullOrWhiteSpace(value) ? "未详" : value;
    }

    private static bool has_appearance_assets(Character character)
    {
        if (character.entity_data is not HumanData data) return false;
        var catalog = character.GetNodeOrNull<AppearanceModCatalog>("/root/AppearanceModCatalog");
        if (catalog is null) return false;
        string gender = data.gender == 1 ? "female" : "male";
        return catalog.get_parts(gender, "face").Count > 0 && catalog.get_parts(gender, "clothes").Count > 0;
    }

    private void refresh_profile_portrait()
    {
        if (current_card is Character character) update_appearance_entry(character);
    }

    public void refresh_after_clothing_item()
    {
        if (current_card is not Character) return;
        refresh_profile_portrait();
        AppearanceChanged?.Invoke();
        render_new_page(_activeNewPage);
    }

    private void set_overview_mood(Vector2? position, string reason = "")
    {
        if (_overviewMoodSummary is null || _overviewMoodScale is null) return;
        _overviewMoodScale.ShowState(position);
        _overviewMoodSummary.Text = $"当前心境　{_overviewMoodScale.CurrentName}";
        if (_overviewMoodReason is not null)
            _overviewMoodReason.Text = !string.IsNullOrWhiteSpace(reason) ? reason
                : position is null ? "暂无已确认的长期心绪记录。" : "心境稳定，暂无持续主导的心绪。";
        _overviewMoodScale.StateReason=_overviewMoodReason?.Text ?? "暂无记录。";
    }

    /// <summary>只供界面演示使用，不写入角色与正式存档。</summary>
    public void preview_mood(Vector2 position)
    {
        _overviewIsPreview=true;
        set_overview_mood(position, position.Length() < .001f
            ? "外观预览：心境稳定，暂无持续主导的心绪。"
            : "外观预览：落点距离圆心越远，心绪越强。");
    }

    private static string format_signed_value(int value) => value > 0 ? $"+{value}" : value.ToString();

    private static string format_root_weight_tooltip(Godot.Collections.Array? weights)
    {
        if (weights is null || weights.Count < 5) return "灵根权重：未详";
        string[] names = ["金", "木", "水", "火", "土"];
        return "灵根权重\n" + string.Join("　", Enumerable.Range(0, 5).Select(i => $"{names[i]} {weights[i].AsInt32()}%"));
    }
    /// <summary>把主辅修装配卡提升到页面顶部，让功法树和详情成为主要工作区。</summary>
    private void configure_static_gongfa_layout()
    {
        if (_staticGongfaPage is null) return;
        var workspace = _staticGongfaPage.GetNodeOrNull<HBoxContainer>("Workspace");
        var equipColumn = workspace?.GetNodeOrNull<VBoxContainer>("EquipColumn");
        var equipPanel = equipColumn?.GetNodeOrNull<PanelContainer>("EquipPanel");
        if (workspace is null || equipColumn is null || equipPanel is null) return;

        equipColumn.RemoveChild(equipPanel);
        equipColumn.Hide();
        var selectorRow = new HBoxContainer { Name = "SlotSelectorRow", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        selectorRow.AddThemeConstantOverride("separation", 12);
        _staticGongfaPage.AddChild(selectorRow);
        _staticGongfaPage.MoveChild(selectorRow, 1);
        selectorRow.AddChild(equipPanel);
        equipPanel.CustomMinimumSize = Vector2.Zero;
        equipPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        equipPanel.SizeFlagsVertical = SizeFlags.ShrinkBegin;

        var box = equipPanel.GetNodeOrNull<VBoxContainer>("Box");
        var oldSlots = box?.GetNodeOrNull<VBoxContainer>("EquipSlots");
        if (box is not null && oldSlots is not null)
        {
            var mainSlot = oldSlots.GetNode<Button>("MainSlot");
            var subSlot = oldSlots.GetNode<Button>("SubSlot");
            oldSlots.RemoveChild(mainSlot);
            oldSlots.RemoveChild(subSlot);
            box.RemoveChild(oldSlots);
            oldSlots.QueueFree();

            var slotButtons = new HBoxContainer { Name = "SlotButtons", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            slotButtons.AddThemeConstantOverride("separation", 12);
            box.AddChild(slotButtons);
            box.MoveChild(slotButtons, 1);
            foreach (var button in new[] { mainSlot, subSlot })
            {
                button.CustomMinimumSize = new Vector2(0, 104);
                button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                button.SizeFlagsVertical = SizeFlags.ShrinkBegin;
                slotButtons.AddChild(button);
            }
        }

        // ScrollContainer 已提供完整可用高度；取消固定高度後讓卷脈與詳情自然填滿。
        workspace.CustomMinimumSize = new Vector2(950, 0);
        workspace.SizeFlagsVertical = SizeFlags.ExpandFill;
        var tree = workspace.GetNodeOrNull<Panel>("TreePanel");
        if (tree is not null) tree.SizeFlagsStretchRatio = 2.3f;
        var detail = workspace.GetNodeOrNull<PanelContainer>("DetailPanel");
        if (detail is not null)
        {
            detail.CustomMinimumSize = new Vector2(360, 0);
            detail.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            detail.SizeFlagsStretchRatio = .85f;
            detail.GetNodeOrNull<RichTextLabel>("Box/DetailText")?.Set("scroll_active", true);
        }
    }
    private void render_new_page(int page)
    {
        if (!_newLayout || _newBody is null || current_card is not Creature unit) return;
        if (_characterCanvas is not null) { render_character_redesign(page, unit); return; }
        hide_tooltip(); _moodDetailsOverlay?.Hide();
        var pageTitles = new[] { "概览", "属性", "战斗", "功法", "装备", "关系" };
        var headerTitle = GetNodeOrNull<Label>("Header/Title");
        if (headerTitle is not null) headerTitle.Text = pageTitles[page];
        _activeNewPage = page; _livePageRefresh = 0;
        if (page != 3) _gongfaModal?.Hide();
        var pageScroll = GetNodeOrNull<ScrollContainer>("Content/Page");
        if (pageScroll is not null)
        {
            pageScroll.HorizontalScrollMode = page == 3 ? ScrollContainer.ScrollMode.Auto : ScrollContainer.ScrollMode.Disabled;
            pageScroll.VerticalScrollMode = ScrollContainer.ScrollMode.Auto;
        }
        update_new_tab_selection(page);
        // 切页时先移除非场景正文；否则“战斗”页临时文字会残留到功法、属性页下方。
        void ClearTransientBody()
        {
            foreach (var child in _newBody.GetChildren())
                if (child != _staticOverviewPage && child != _staticAttributePage && child != _staticBattlePage && child != _staticGongfaPage) { if (child is CanvasItem canvas) canvas.Visible = false; child.QueueFree(); }
        }
        if (page == 0 && _staticOverviewPage is not null)
        {
            ClearTransientBody();
            _staticAttributePage?.Hide(); _staticBattlePage?.Hide(); _staticGongfaPage?.Hide();
            _staticOverviewPage.Show();
            populate_overview_page(unit, current_card as Character);
            return;
        }
        if (page == 1 && _staticAttributePage is not null)
        {
            ClearTransientBody();
            _staticOverviewPage?.Hide(); _staticBattlePage?.Hide(); _staticGongfaPage?.Hide();
            _staticAttributePage.Show();
            populate_static_attribute_page(unit, current_card as Character);
            return;
        }
        if (page == 2 && _staticBattlePage is not null)
        {
            ClearTransientBody();
            _staticOverviewPage?.Hide(); _staticAttributePage?.Hide(); _staticGongfaPage?.Hide(); _staticBattlePage.Show();
            populate_static_battle_page(unit);
            return;
        }
        if (page == 3 && _staticGongfaPage is not null && current_card is Character gongfaHuman)
        {
            ClearTransientBody();
            _staticOverviewPage?.Hide(); _staticAttributePage?.Hide();
            _staticBattlePage?.Hide();
            _staticGongfaPage.Show();
            populate_static_gongfa_page(gongfaHuman);
            return;
        }
        if (page == 4)
        {
            _staticOverviewPage?.Hide(); _staticAttributePage?.Hide(); _staticBattlePage?.Hide(); _staticGongfaPage?.Hide();
            ClearTransientBody();
            render_equipment_page(current_card as Character);
            return;
        }
        _staticOverviewPage?.Hide();
        _staticAttributePage?.Hide(); _staticBattlePage?.Hide();
        _staticGongfaPage?.Hide();
        ClearTransientBody();
        Label Text(string value, int size = 17) { var label = new Label { Text = value, AutowrapMode = TextServer.AutowrapMode.WordSmart }; label.AddThemeFontSizeOverride("font_size", size); label.AddThemeColorOverride("font_color", new Color(.12f, .11f, .09f)); _newBody.AddChild(label); return label; }
        Texture2D? AttributeIcon(int index)
        {
            var sheet = GD.Load<Texture2D>("res://textures/ui/infopanel_attribute_icons_ink_v2.png");
            if (sheet is null || index < 0 || index > 5) return null;
            return new AtlasTexture { Atlas = sheet, Region = new Rect2((index % 3) * 512, (index / 3) * 512, 512, 512) };
        }
        HSeparator Divider()
        {
            var divider = new HSeparator { CustomMinimumSize = new Vector2(0, 3) };
            divider.AddThemeStyleboxOverride("separator", new StyleBoxFlat { BgColor = new Color(.29f, .24f, .16f, .52f), ContentMarginTop = 1, ContentMarginBottom = 1 });
            return divider;
        }
        void AttributeGrid(string title, (string name, string value, int icon)[] stats, int itemsPerRow = 2)
        {
            var section = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            section.AddThemeConstantOverride("separation", 9); _newBody.AddChild(section);
            var heading = new Label { Text = title }; heading.AddThemeFontSizeOverride("font_size", 23); heading.AddThemeColorOverride("font_color", new Color(.12f, .11f, .09f)); section.AddChild(heading);
            section.AddChild(Divider());
            for (var i = 0; i < stats.Length; i += itemsPerRow)
            {
                var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
                row.AddThemeConstantOverride("separation", 36); section.AddChild(row);
                for (var column = 0; column < itemsPerRow && i + column < stats.Length; column++)
                {
                    var (name, value, icon) = stats[i + column];
                    var item = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
                    item.AddThemeConstantOverride("separation", 9); row.AddChild(item);
                    item.AddChild(new TextureRect { Texture = AttributeIcon(icon), CustomMinimumSize = new Vector2(27, 27), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
                    var nameLabel = new Label { Text = name }; nameLabel.AddThemeFontSizeOverride("font_size", 18); item.AddChild(nameLabel);
                    var valueLabel = new Label { Text = value, HorizontalAlignment = HorizontalAlignment.Left, SizeFlagsHorizontal = SizeFlags.ShrinkBegin }; valueLabel.AddThemeFontSizeOverride("font_size", 18); item.AddChild(valueLabel);
                }
            }
        }
        var human = current_card as Character;
        var rootData = human?.entity_data as HumanData;
        int RootWeight(int index) => rootData is null || rootData.root_weights.Count <= index ? 0 : rootData.root_weights[index].AsInt32();
        Text(pageTitles[page], 28);
        if (page == 1)
        {
            AttributeGrid("基础属性",
                [("体魄", unit.physique.ToString("0.0"), 0), ("灵慧", unit.intelligence.ToString("0.0"), 1),
                 ("神魂", unit.soul.ToString("0.0"), 2),
                 ("福缘", unit.fortune.ToString("0.0"), 4), ("悟性", unit.comprehension.ToString("0.0"), 5), ("魅力", data_number(unit,"charisma").ToString("0.0"), -1), ("交际", data_number(unit,"social").ToString("0.0"), -1), ("治事", data_number(unit,"management").ToString("0.0"), -1)]);
            AttributeGrid("战斗属性",
                [("生命", $"{unit.current_health}/{unit.max_health}", -1), ("体力", $"{unit.current_stamina}/{unit.max_stamina}", -1), ("法力", $"{unit.current_mana}/{unit.max_mana}", -1),
                 ("速度", unit.initiative_speed.ToString("0.0"), -1), ("身心协调值", unit.coordination.ToString("0.0"), -1), ("灵识容量", $"{unit.used_spirit_perception}/{unit.max_spirit_perception}", -1),
                 ("物理防御", unit.physical_defense.ToString(), -1), ("精准", unit.precision.ToString(), -1),
                 ("身法", unit.dodge.ToString(), -1),
                 ("会心", unit.critical.ToString(), -1), ("护心", unit.crit_resist.ToString(), -1),
                 ("会心伤害", unit.crit_damage_mult.ToString("0.00"), -1),
                 ("物理减伤", $"{unit.physical_damage_reduction*100:0.#}%", -1)]);
            AttributeGrid("行旅属性", [("遁速", human?.dun_speed.ToString("0.#") ?? "未配置", -1)]);
            AttributeGrid("灵根权重",
                [("金灵根", RootWeight(0).ToString(), -1), ("木灵根", RootWeight(1).ToString(), -1),
                 ("水灵根", RootWeight(2).ToString(), -1), ("火灵根", RootWeight(3).ToString(), -1),
                 ("土灵根", RootWeight(4).ToString(), -1)], 5);
            AttributeGrid("修行属性",
                [("灵根", human?.spirit_root_name ?? "未详", -1), ("境界", unit.cultivation_realm, -1),
                 ("灵力", $"{unit.spirit_strands} 缕", -1), ("灵核", human?.get_spirit_core_summary() ?? "—", -1),
                 ("学习效率", human is null ? "—" : human.get_learning_rate().ToString("0.00") + "/小时", -1), ("修炼效率", human is null ? "—" : human.get_cultivation_rate().ToString("0.000") + "/小时", -1)], 3);
            AttributeGrid("造诣",
                [("剑道", data_number(unit, "sword_proficiency").ToString("0.0"), -1), ("刀道", data_number(unit, "blade_proficiency").ToString("0.0"), -1),
                 ("枪道", data_number(unit, "spear_proficiency").ToString("0.0"), -1), ("体道", data_number(unit, "body_proficiency").ToString("0.0"), -1),
                 ("术法", data_number(unit, "magic_proficiency").ToString("0.0"), -1), ("器道", data_number(unit, "forging_proficiency").ToString("0.0"), -1),
                 ("符道", data_number(unit, "talisman_proficiency").ToString("0.0"), -1), ("阵道", data_number(unit, "formation_proficiency").ToString("0.0"), -1),
                 ("丹道", data_number(unit, "alchemy_proficiency").ToString("0.0"), -1)], 3);
        }
        else if (page == 2) Text("战斗界面暂未开放。");
        else if (page == 3 && human is not null) Text($"主修：{human.main_gongfa?.gongfa_name ?? "尚未设置"}\n辅修：{(human.sub_gongfa_list.Count == 0 ? "暂无" : string.Join("、", human.sub_gongfa_list.Select(g => g.gongfa_name)))}");
        else if (page == 4) Text(human is null || human.pack_items.Count == 0 ? "随身物资：暂无" : $"随身物资\n{string.Join("\n", human.pack_items)}");
        else if (page == 5) Text("亲缘、声望与往来会在家族发展中逐步记录。 ");
    }

    /// <summary>装备页将常规穿戴和法宝构筑分开：四件穿戴直接给属性，法宝独占槽位与灵识容量。</summary>
    private void render_equipment_page(Character? human)
    {
        if (_newBody is null) return;
        var ink = new Color(.12f, .095f, .065f);
        var muted = new Color(.30f, .26f, .19f);
        Label Text(string text, int size = 17)
        {
            var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            label.AddThemeFontSizeOverride("font_size", size); label.AddThemeColorOverride("font_color", ink); return label;
        }
        PanelContainer Panel()
        {
            var panel = new PanelContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
            panel.AddThemeStyleboxOverride("panel", equipment_panel_style()); return panel;
        }
        var title = Text("装备", 28); _newBody.AddChild(title);
        var subtitle = Text("常规穿戴提供直接属性；法宝决定御使构筑与灵识占用。", 16); subtitle.Modulate = muted; _newBody.AddChild(subtitle);
        var layout = new HBoxContainer { CustomMinimumSize = new Vector2(0, 610), SizeFlagsVertical = SizeFlags.ExpandFill };
        layout.AddThemeConstantOverride("separation", 16); _newBody.AddChild(layout);

        var data = human?.entity_data as HumanData;
        var attire = Panel(); attire.CustomMinimumSize = new Vector2(390, 0); layout.AddChild(attire);
        var attireCanvas = new Control { CustomMinimumSize = new Vector2(390, 570) }; attire.AddChild(attireCanvas);
        attireCanvas.AddChild(new EquipmentFigure { Position = new Vector2(142, 84), Size = new Vector2(112, 398), MouseFilter = MouseFilterEnum.Ignore });
        add_equipment_slot(attireCanvas, "头冠", data?.headwear_path ?? "", new Vector2(18, 44), "头部");
        add_equipment_slot(attireCanvas, "衣甲", data?.clothes_path ?? "", new Vector2(14, 196), "躯干");
        add_equipment_slot(attireCanvas, "饰品", string.IsNullOrEmpty(data?.ornament_1_path) ? data?.ornament_2_path ?? "" : data.ornament_1_path, new Vector2(236, 205), "胸前");
        add_equipment_slot(attireCanvas, "足履", data?.footwear_path ?? "", new Vector2(226, 390), "足部");

        if (human is not null && data is not null)
        {
            var catalog = GetNode<PortraitCatalog>("/root/PortraitCatalog");
            string sex = data.gender == 1 ? "female" : "male";
            var context = catalog.get_context(data, sex, human.Get("current_age").AsInt32(), data.faction_name, human.cultivation_realm);
            var clothes = catalog.choose_part(context, "clothes", data.appearance_clothes_style.ToString());
            string garment = clothes.Count > 0 ? clothes["display_name"].AsString() : "暂无常服";
            var changeClothing = new Button { Name = "ChangeClothing", Text = "衣着 · " + garment, Position = new Vector2(20, 490), Size = new Vector2(350, 52) };
            changeClothing.AddThemeFontSizeOverride("font_size", 19);
            changeClothing.Pressed += () => _wardrobe?.open_for(human); attireCanvas.AddChild(changeClothing);
        }

        var artifacts = Panel(); artifacts.CustomMinimumSize = new Vector2(420, 0); artifacts.SizeFlagsHorizontal = SizeFlags.ExpandFill; layout.AddChild(artifacts);
        var artifactBox = new VBoxContainer(); artifactBox.AddThemeConstantOverride("separation", 11); artifacts.AddChild(artifactBox);
        var artifactHeading = new HBoxContainer(); artifactBox.AddChild(artifactHeading);
        var artifactTitle = Text("法宝御使", 24); artifactTitle.SizeFlagsHorizontal = SizeFlags.ExpandFill; artifactHeading.AddChild(artifactTitle);
        var capacity = Text($"法宝槽　0/{data?.artifact_slot_count ?? 0}", 18); artifactHeading.AddChild(capacity);
        var spirit = Text($"灵识　{human?.used_spirit_perception ?? 0}/{human?.max_spirit_perception ?? 0}", 18); artifactBox.AddChild(spirit);
        var maximumSpirit = Mathf.Max(1, human?.max_spirit_perception ?? 0);
        var meter = new ProgressBar { MinValue = 0, MaxValue = maximumSpirit, Value = Mathf.Clamp(human?.used_spirit_perception ?? 0, 0, maximumSpirit), ShowPercentage = false, CustomMinimumSize = new Vector2(0, 13) };
        meter.AddThemeStyleboxOverride("background", equipment_meter_style(false)); meter.AddThemeStyleboxOverride("fill", equipment_meter_style(true)); artifactBox.AddChild(meter);
        artifactBox.AddChild(Text("内炼提供化法且不占灵识；外祭技能须占栏排队，召出后占用灵识。", 15));
        for (var i = 0; i < 3; i++)
        {
            var unlocked = i < (data?.artifact_slot_count ?? 0);
            var slot = new PanelContainer { CustomMinimumSize = new Vector2(0, 112) }; slot.AddThemeStyleboxOverride("panel", equipment_slot_style(unlocked)); artifactBox.AddChild(slot);
            var slotBox = new VBoxContainer(); slotBox.AddThemeConstantOverride("separation", 3); slot.AddChild(slotBox);
            slotBox.AddChild(Text(unlocked ? "空闲法宝槽" : "未解锁法宝槽", 20));
            var state = Text(unlocked ? "可从炼器与法宝管理中装配" : "随境界、神魂与功法逐步开放", 15); state.Modulate = muted; slotBox.AddChild(state);
        }

        var details = Panel(); details.CustomMinimumSize = new Vector2(300, 0); layout.AddChild(details);
        var detailBox = new VBoxContainer(); detailBox.AddThemeConstantOverride("separation", 12); details.AddChild(detailBox);
        detailBox.AddChild(Text("法宝详情", 24));
        detailBox.AddChild(new HSeparator());
        var detail = Text("尚未选中法宝\n\n炼器完成后，此处会显示：\n本行与阴阳\n主功用及品级\n灵识占用、耐久\n内炼或外祭构型\n绑定基础牌与炼制记录", 17); detail.SizeFlagsVertical = SizeFlags.ExpandFill; detailBox.AddChild(detail);
        var forge = new Button { Text = "查看炼器", CustomMinimumSize = new Vector2(0, 42) }; forge.Disabled = true; forge.AddThemeColorOverride("font_color", ink); detailBox.AddChild(forge);

        var supply = new PanelContainer(); supply.AddThemeStyleboxOverride("panel", equipment_panel_style());
        supply.CustomMinimumSize = new Vector2(0, 126); supply.SizeFlagsVertical = SizeFlags.ShrinkEnd; _newBody.AddChild(supply);
        var supplyBox = new VBoxContainer(); supplyBox.AddThemeConstantOverride("separation", 8); supply.AddChild(supplyBox);
        var supplyTitle = Text("战备", 22); supplyTitle.HorizontalAlignment = HorizontalAlignment.Center; supplyBox.AddChild(supplyTitle);
        var groups = new HBoxContainer(); groups.AddThemeConstantOverride("separation", 24); supplyBox.AddChild(groups);
        add_supply_group(groups, "丹药栏"); add_supply_group(groups, "符箓栏");
    }

    private void add_equipment_slot(Control canvas, string slotName, string path, Vector2 position, string bodyPart)
    {
        var resource = string.IsNullOrWhiteSpace(path) ? null : ResourceLoader.Load<EquipmentData>(path);
        var panel = new PanelContainer { Position = position, Size = new Vector2(142, 128), TooltipText = string.IsNullOrWhiteSpace(path) ? $"{slotName}尚未穿戴" : $"{bodyPart}穿戴：{resource?.entity_name ?? "未知装备"}" };
        panel.AddThemeStyleboxOverride("panel", equipment_slot_style(resource is not null)); canvas.AddChild(panel);
        var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 4); panel.AddChild(box);
        var title = new Label { Text = slotName, HorizontalAlignment = HorizontalAlignment.Center }; title.AddThemeFontSizeOverride("font_size", 19); title.AddThemeColorOverride("font_color", new Color(.12f, .095f, .065f)); box.AddChild(title);
        var item = new Label { Text = resource?.entity_name ?? "尚未穿戴", HorizontalAlignment = HorizontalAlignment.Center }; item.AddThemeFontSizeOverride("font_size", 17); item.AddThemeColorOverride("font_color", resource is null ? new Color(.38f, .34f, .26f) : new Color(.12f, .095f, .065f)); box.AddChild(item);
        var bonus = new Label { Text = resource is null ? "—" : equipment_bonus_text(resource), AutowrapMode = TextServer.AutowrapMode.WordSmart, HorizontalAlignment = HorizontalAlignment.Center, SizeFlagsVertical = SizeFlags.ExpandFill }; bonus.AddThemeFontSizeOverride("font_size", 14); bonus.AddThemeColorOverride("font_color", new Color(.18f, .14f, .08f)); box.AddChild(bonus);
    }

    private void add_supply_group(HBoxContainer parent, string title)
    {
        var group = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; parent.AddChild(group);
        var heading = new Label { Text = title, HorizontalAlignment = HorizontalAlignment.Center }; heading.AddThemeFontSizeOverride("font_size", 18); heading.AddThemeColorOverride("font_color", new Color(.12f, .095f, .065f)); group.AddChild(heading);
        var cells = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; cells.AddThemeConstantOverride("separation", 8); group.AddChild(cells);
        for (var i = 0; i < 4; i++)
        {
            var cell = new PanelContainer { CustomMinimumSize = new Vector2(78, 44) }; cell.AddThemeStyleboxOverride("panel", equipment_slot_style(false)); cells.AddChild(cell);
            var label = new Label { Text = "预留", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }; label.AddThemeFontSizeOverride("font_size", 15); label.AddThemeColorOverride("font_color", new Color(.30f, .26f, .19f)); cell.AddChild(label);
        }
    }

    private static string equipment_bonus_text(EquipmentData equipment)
    {
        var labels = new Dictionary<string, string> { ["physique"] = "体魄", ["intelligence"] = "灵慧", ["soul"] = "神魂", ["max_health"] = "生命上限", ["physical_defense"] = "物理防御", ["critical"] = "会心", ["max_spirit_perception"] = "灵识上限", ["mana_regen_rate"] = "法力恢复" };
        var values = new List<string>();
        foreach (Variant key in equipment.fixed_bonuses.Keys) { var name = key.AsString(); var label = labels.GetValueOrDefault(name, name); var value = equipment.fixed_bonuses[key].AsSingle(); values.Add($"{label} {(value >= 0 ? "+" : "")}{value:0.#}"); }
        return values.Count == 0 ? "无直接属性加成" : string.Join("\n", values.Take(2));
    }

    private static StyleBoxFlat equipment_panel_style() => new() { BgColor = new Color(.90f, .86f, .75f, .42f), BorderColor = new Color(.28f, .22f, .14f, .72f), BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1, ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 14, ContentMarginBottom = 14 };
    private static StyleBoxFlat equipment_slot_style(bool active) => new() { BgColor = active ? new Color(.87f, .82f, .68f, .70f) : new Color(.86f, .82f, .72f, .32f), BorderColor = active ? new Color(.43f, .31f, .13f, .85f) : new Color(.30f, .26f, .18f, .48f), BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1, ContentMarginLeft = 9, ContentMarginRight = 9, ContentMarginTop = 8, ContentMarginBottom = 8 };
    private static StyleBoxFlat equipment_meter_style(bool filled) => new() { BgColor = filled ? new Color(.17f, .36f, .30f, .92f) : new Color(.22f, .18f, .12f, .68f), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 };

    private sealed partial class EquipmentFigure : Control
    {
        public override void _Draw()
        {
            var ink = new Color(.18f, .17f, .14f, .53f); var center = Size.X * .5f;
            DrawCircle(new Vector2(center, 30), 22, new Color(.28f, .27f, .23f, .17f)); DrawArc(new Vector2(center, 30), 22, 0, Mathf.Tau, 36, ink, 2);
            DrawLine(new Vector2(center, 52), new Vector2(center, 250), ink, 3); DrawLine(new Vector2(center, 78), new Vector2(20, 158), ink, 3); DrawLine(new Vector2(center, 78), new Vector2(Size.X - 20, 158), ink, 3);
            DrawLine(new Vector2(center, 248), new Vector2(33, 370), ink, 3); DrawLine(new Vector2(center, 248), new Vector2(Size.X - 33, 370), ink, 3);
            foreach (var point in new[] { new Vector2(center, 80), new Vector2(center, 145), new Vector2(center, 210), new Vector2(center, 278) }) DrawCircle(point, 4, new Color(.30f, .25f, .16f, .60f));
        }
    }

    private void update_new_tab_selection(int selected)
    {
        for (var i = 0; i < _newTabs.Count; i++)
        {
            var active = i == selected; var (button, caption) = _newTabs[i];
            button.GetNodeOrNull<TextureRect>("SelectedPlaque")?.SetDeferred("visible", active);
            caption.AddThemeColorOverride("font_color", active ? new Color(.11f, .09f, .06f) : Colors.White);
        }
    }

    /// <summary>功法页的框架由场景承载；脚本只填充角色当前的装配与研读状态。</summary>
    private void bind_static_gongfa_controls()
    {
        build_gongfa_modal();
        var detailBox = GetNodeOrNull<VBoxContainer>("Content/Page/Body/GongfaLayout/Workspace/DetailPanel/Box");
        if (detailBox is not null)
        {
            _gongfaBranchActivate = gongfa_popup_button("激活此分支");
            _gongfaBranchActivate.Visible = false;
            _gongfaBranchActivate.Pressed += activate_selected_gongfa_branch;
            detailBox.AddChild(_gongfaBranchActivate);
        }
        GetNodeOrNull<Button>("Content/Page/Body/GongfaLayout/SlotSelectorRow/EquipPanel/Box/SlotButtons/MainSlot")!.Pressed += () => { if (current_card is Character human) show_gongfa_equip_popup(human, false); };
        GetNodeOrNull<Button>("Content/Page/Body/GongfaLayout/SlotSelectorRow/EquipPanel/Box/SlotButtons/SubSlot")!.Pressed += () => { if (current_card is Character human) show_gongfa_equip_popup(human, true); };
        for (var i = 0; i < 5; i++)
        {
            var chapterIndex = i;
            GetNodeOrNull<Button>($"Content/Page/Body/GongfaLayout/Workspace/TreePanel/TreeRows/Row{i + 1}/Left")!.Pressed += () => select_static_gongfa_route(chapterIndex, "left");
            GetNodeOrNull<Button>($"Content/Page/Body/GongfaLayout/Workspace/TreePanel/TreeRows/Row{i + 1}/Right")!.Pressed += () => select_static_gongfa_route(chapterIndex, "right");
            var chapter = GetNodeOrNull<Label>($"Content/Page/Body/GongfaLayout/Workspace/TreePanel/TreeRows/Row{i + 1}/Chapter");
            if (chapter is not null) chapter.GuiInput += input => { if (input is InputEventMouseButton mouse && mouse.ButtonIndex == MouseButton.Left && mouse.Pressed) select_static_gongfa_node(chapterIndex); };
        }
    }

    private void select_static_gongfa_route(int chapterIndex, string routeName)
    {
        if (current_card is not Character human || _viewedGongfa is null) return;
        var chapters = _viewedGongfa.get_sorted_chapters();
        if (chapterIndex < 0 || chapterIndex >= chapters.Count) return;
        _gongfaSelectedChapter = chapterIndex; _gongfaSelectedBranch = routeName;
        populate_static_gongfa_page(human);
    }

    private void select_static_gongfa_node(int chapterIndex)
    {
        if (current_card is not Character human || _viewedGongfa is null || chapterIndex < 0 || chapterIndex >= _viewedGongfa.get_sorted_chapters().Count) return;
        _gongfaSelectedChapter = chapterIndex; _gongfaSelectedBranch = ""; populate_static_gongfa_page(human);
    }

    private void activate_selected_gongfa_branch()
    {
        if (current_card is not Character human || _viewedGongfa is null || !_viewingSubGongfa || _gongfaSelectedChapter < 0 || string.IsNullOrEmpty(_gongfaSelectedBranch)) return;
        var chapters = _viewedGongfa.get_sorted_chapters(); if (_gongfaSelectedChapter >= chapters.Count) return;
        var chapter = chapters[_gongfaSelectedChapter]; if (!human.is_gongfa_chapter_learned(_viewedGongfa, chapter.get_id())) return;
        human.set_sub_gongfa_chapter_route(_viewedGongfa, chapter.get_id(), _gongfaSelectedBranch);
        populate_static_gongfa_page(human);
    }

    private void build_gongfa_modal()
    {
        if (_gongfaModal is not null) return;
        var overlay = new Control { Name = "GongfaChoicePopup", MouseFilter = MouseFilterEnum.Stop, ZIndex = 710 };
        overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var veil = new ColorRect { Color = new Color(.055f, .045f, .025f, .52f), MouseFilter = MouseFilterEnum.Stop };
        veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); veil.GuiInput += input => { if (input is InputEventMouseButton mouse && mouse.ButtonIndex == MouseButton.Left && mouse.Pressed) overlay.Hide(); }; overlay.AddChild(veil);
        // 研读总览需要同时容纳三个位与每卷条件，正文区独立滚动，不能把信息硬挤进小窗。
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(640, 650), MouseFilter = MouseFilterEnum.Stop };
        panel.SetAnchorsPreset(LayoutPreset.Center); panel.OffsetLeft = -320; panel.OffsetTop = -325; panel.OffsetRight = 320; panel.OffsetBottom = 325;
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(.91f, .86f, .74f, .98f), BorderColor = new Color(.25f, .18f, .09f, .92f), BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2, CornerRadiusTopLeft = 9, CornerRadiusTopRight = 9, CornerRadiusBottomLeft = 9, CornerRadiusBottomRight = 9, ContentMarginLeft = 24, ContentMarginRight = 24, ContentMarginTop = 20, ContentMarginBottom = 20 }); overlay.AddChild(panel);
        var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 12); panel.AddChild(box);
        var title = new Label { HorizontalAlignment = HorizontalAlignment.Center }; title.AddThemeFontSizeOverride("font_size", 25); title.AddThemeColorOverride("font_color", new Color(.12f, .095f, .065f)); box.AddChild(title);
        var body = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart }; body.AddThemeFontSizeOverride("font_size", 16); body.AddThemeColorOverride("font_color", new Color(.20f, .16f, .10f)); box.AddChild(body);
        var line = new HSeparator(); line.AddThemeStyleboxOverride("separator", new StyleBoxFlat { BgColor = new Color(.30f, .23f, .13f, .55f), ContentMarginTop = 1, ContentMarginBottom = 1 }); box.AddChild(line);
        var choiceScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, VerticalScrollMode = ScrollContainer.ScrollMode.Auto };
        box.AddChild(choiceScroll);
        var choices = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; choices.AddThemeConstantOverride("separation", 8); choiceScroll.AddChild(choices);
        overlay.Hide(); AddChild(overlay); _gongfaModal = overlay; _gongfaModalTitle = title; _gongfaModalBody = body; _gongfaModalChoices = choices;
    }

    private void show_gongfa_equip_popup(Character human, bool sub)
    {
        _gongfaModalMode = "equip";
        if (_gongfaModal is null || _gongfaModalTitle is null || _gongfaModalBody is null || _gongfaModalChoices is null) return;
        foreach (var child in _gongfaModalChoices.GetChildren()) child.QueueFree();
        var equipped = sub ? human.sub_gongfa_list.FirstOrDefault() : human.main_gongfa;
        _gongfaModalTitle.Text = sub ? "更换辅修秘籍" : "更换主修秘籍";
        _gongfaModalBody.Text = equipped is null ? "选择一部已研读秘籍装配。" : $"当前装配：《{equipped.gongfa_name}》\n选择另一部已研读秘籍即可替换，已研读卷章不会丢失。";
        if (equipped is not null)
        {
            var remove = gongfa_popup_button("卸下当前秘籍"); remove.Pressed += () => { if (sub) human.remove_sub_gongfa(equipped); else human.unequip_main_gongfa(); _gongfaModal.Hide(); populate_static_gongfa_page(human); }; _gongfaModalChoices.AddChild(remove);
        }
        foreach (var book in human.get_known_gongfas().Where(human.has_learned_gongfa).Where(book => sub ? book.can_be_sub : book.can_be_main && book.is_wuxing_compatible(human.spirit_root_type)))
        {
            var choose = gongfa_popup_button($"装配《{book.gongfa_name}》"); choose.TooltipText = book.description;
            choose.Pressed += () => { _gongfaPickerSlot = sub ? "sub" : "main"; equip_selected_gongfa(human, book); _gongfaModal.Hide(); }; _gongfaModalChoices.AddChild(choose);
        }
        if (_gongfaModalChoices.GetChildCount() == 0) _gongfaModalChoices.AddChild(new Label { Text = "尚无已研读秘籍。完成任一卷研读后，会在这里显示为可装配。", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        _gongfaModal.Show(); _gongfaModal.MoveToFront();
    }

    private void show_gongfa_study_popup(Character human, int index)
    {
        _gongfaModalMode = "study-slot";
        if (_gongfaModal is null || _gongfaModalTitle is null || _gongfaModalBody is null || _gongfaModalChoices is null) return;
        foreach (var child in _gongfaModalChoices.GetChildren()) child.QueueFree();
        var slots = human.get_study_slot_snapshot(); var secret = index < slots.Count ? slots[index].GetValueOrDefault("book", default).AsGodotObject() as SecretBook : null;
        _gongfaModalTitle.Text = $"研读位 {index + 1}";
        if (secret?.gongfa is null)
        {
            _gongfaModalBody.Text = "此研读位空闲。把待研读秘籍放入此处，完成本卷后才可设为主修。";
            if (human.get_pending_intro_gongfa() is { } intro)
            {
                var start = gongfa_popup_button($"开始研读《{intro.gongfa_name}》");
                start.Pressed += () => { if (human.start_pending_intro_gongfa_study()) { _gongfaModal.Hide(); populate_static_gongfa_page(human); } };
                _gongfaModalChoices.AddChild(start);
            }
            else _gongfaModalChoices.AddChild(new Label { Text = "当前没有待研读的秘籍。", HorizontalAlignment = HorizontalAlignment.Center });
        }
        else
        {
            var nextId = secret.is_studying_now ? secret.studying_chapter_id : secret.get_next_unread_chapter(human);
            var chapter = secret.gongfa.get_chapter_by_id(nextId); var need = Mathf.Max(chapter?.required_study_progress ?? secret.required_study_progress, .1f); var progress = Mathf.Clamp(secret.current_study_progress / need, 0, 1);
            _gongfaModalBody.Text = secret.is_studying_now ? $"《{secret.gongfa.gongfa_name}》\n正在研读：{chapter?.chapter_name ?? "当前卷"}\n进度：{secret.current_study_progress:0.0} / {need:0.0}　{progress * 100:0}%" : $"《{secret.gongfa.gongfa_name}》\n下一卷：{chapter?.chapter_name ?? "已全部研读"}\n秘籍保留在研读位，满足条件后自动开始研读。";
            if (!secret.is_studying_now && chapter is not null) foreach (var requirement in gongfa_requirement_lines(human, secret.gongfa, chapter)) _gongfaModalChoices.AddChild(requirement);
            var stop = gongfa_popup_button("暂停研读（保留本卷进度）"); stop.Pressed += () => { human.remove_from_study_slot(index); _gongfaModal.Hide(); populate_static_gongfa_page(human); }; _gongfaModalChoices.AddChild(stop);
        }
        _gongfaModal.Show(); _gongfaModal.MoveToFront();
    }

    /// <summary>研读总览只在弹窗中展开，避免与主辅修装配争夺功法页的底部空间。</summary>
    private void show_gongfa_study_overview_popup(Character human)
    {
        _gongfaModalMode = "study";
        if (_gongfaModal is null || _gongfaModalTitle is null || _gongfaModalBody is null || _gongfaModalChoices is null) return;
        foreach (var child in _gongfaModalChoices.GetChildren()) child.QueueFree();
        _gongfaModalTitle.Text = "研读";
        var slots = human.get_study_slot_snapshot();
        var occupied = slots.Count(slot => slot.GetValueOrDefault("occupied", false).AsBool());
        _gongfaModalBody.Text = occupied == 0 ? "当前没有正在研读的秘籍。取得秘籍后，可从此处安排研读。" : $"研读位 {occupied}/3 · 完成本卷后秘籍会保留在研读位；下一卷条件满足时自动续读。";
        for (var i = 0; i < slots.Count; i++)
        {
            var secret = slots[i].GetValueOrDefault("book", default).AsGodotObject() as SecretBook;
            if (secret?.gongfa is null) continue;
            var chapterId = secret.is_studying_now ? secret.studying_chapter_id : secret.get_next_unread_chapter(human);
            var chapter = secret.gongfa.get_chapter_by_id(chapterId);
            var required = Mathf.Max(chapter?.required_study_progress ?? secret.get_required_study_progress(), .1f);
            var ratio = Mathf.Clamp(secret.current_study_progress / required, 0, 1);
            var remaining = Mathf.Max(0, required - secret.current_study_progress);
            var eta = $"余 {remaining:0.0} 进度，需由玩家主动研读";
            var card = new PanelContainer { CustomMinimumSize = new Vector2(0, 132) };
            card.AddThemeStyleboxOverride("panel", gongfa_card_style()); _gongfaModalChoices.AddChild(card);
            var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 5); card.AddChild(box);
            var title = new Label { Text = $"《{secret.gongfa.gongfa_name}》 · 第{chinese_volume_number(chapter?.order ?? 1)}卷 · {chapter?.chapter_name ?? "已全部研读"}" };
            title.AddThemeFontSizeOverride("font_size", 17); title.AddThemeColorOverride("font_color", new Color(.12f, .095f, .065f)); box.AddChild(title);
            var progress = new ProgressBar { MinValue = 0, MaxValue = 100, Value = ratio * 100, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 12) };
            progress.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color(.20f, .17f, .11f, .74f), CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6 });
            progress.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = new Color(.72f, .49f, .16f, .96f), BorderColor = new Color(.96f, .78f, .34f, .85f), BorderWidthTop = 1, BorderWidthBottom = 1, CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6 }); box.AddChild(progress);
            box.AddChild(new Label { Text = secret.is_studying_now ? $"研读进度　{ratio * 100:0}%　{secret.current_study_progress:0.0} / {required:0.0}　{eta}" : chapter is null ? "本秘籍已全部研读" : "等待下一卷条件满足后可继续研读", Modulate = new Color(.18f, .14f, .08f) });
            if (chapter is not null) foreach (var line in gongfa_requirement_lines(human, secret.gongfa, chapter)) box.AddChild(line);
            var slotIndex = i; var stop = gongfa_popup_button("暂停研读"); stop.CustomMinimumSize = new Vector2(0, 32); stop.Pressed += () => { human.remove_from_study_slot(slotIndex); _gongfaModal.Hide(); populate_static_gongfa_page(human); }; box.AddChild(stop);
        }
        // 未放入研读位的秘籍也必须保留可见入口：卸下只是不再安排，不会让秘籍“消失”。
        var scheduledPaths = slots.Select(slot => slot.GetValueOrDefault("book", default).AsGodotObject() as SecretBook).Where(book => book?.gongfa is not null).Select(book => book!.gongfa!.ResourcePath).ToHashSet();
        var availableBooks = human.get_known_gongfas().Where(book => !scheduledPaths.Contains(book.ResourcePath)).ToList();
        if (human.get_pending_intro_gongfa() is { } intro && !availableBooks.Any(book => book.ResourcePath == intro.ResourcePath)) availableBooks.Add(intro);
        foreach (var book in availableBooks)
        {
            var chapters = book.get_sorted_chapters();
            var learned = chapters.Where(chapter => human.is_gongfa_chapter_learned(book, chapter.get_id())).ToList();
            var next = chapters.FirstOrDefault(chapter => !human.is_gongfa_chapter_learned(book, chapter.get_id()));
            var card = new PanelContainer { CustomMinimumSize = new Vector2(0, 142) };
            card.AddThemeStyleboxOverride("panel", gongfa_card_style()); _gongfaModalChoices.AddChild(card);
            var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 5); card.AddChild(box);
            var progressText = learned.Count == 0 ? "未研读" : $"已研读第{chinese_volume_number(learned.Last().order)}卷 100%";
            var title = new Label { Text = $"《{book.gongfa_name}》 · {progressText}" };
            title.AddThemeFontSizeOverride("font_size", 17); title.AddThemeColorOverride("font_color", new Color(.12f, .095f, .065f)); box.AddChild(title);
            if (next is null) box.AddChild(new Label { Text = "全卷已研读，可前往主辅修装配。", Modulate = new Color(.16f, .43f, .22f) });
            else
            {
                box.AddChild(new Label { Text = $"待研读：第{chinese_volume_number(next.order)}卷 · {next.chapter_name}", Modulate = new Color(.18f, .14f, .08f) });
                foreach (var line in gongfa_requirement_lines(human, book, next)) box.AddChild(line);
                var isIntro = human.get_pending_intro_gongfa()?.ResourcePath == book.ResourcePath;
                var arrange = gongfa_popup_button(human.has_free_study_slot() ? "放入研读位" : "研读位已满"); arrange.CustomMinimumSize = new Vector2(0, 32); arrange.Disabled = !human.has_free_study_slot();
                arrange.Pressed += () => { var ok = isIntro ? human.start_pending_intro_gongfa_study() : human.arrange_gongfa_study(book); if (ok) { _gongfaModal.Hide(); populate_static_gongfa_page(human); } };
                box.AddChild(arrange);
            }
        }
        if (_gongfaModalChoices.GetChildCount() == 0) _gongfaModalChoices.AddChild(new Label { Text = "暂无可研读秘籍。", HorizontalAlignment = HorizontalAlignment.Center });
        _gongfaModal.Show(); _gongfaModal.MoveToFront();
    }

    private static IEnumerable<Label> gongfa_requirement_lines(Character human, GongfaResource book, GongfaChapterConfig chapter)
    {
        if (!string.IsNullOrWhiteSpace(chapter.required_realm))
        {
            var realms = human.GetNodeOrNull<RealmManager>("/root/RealmManager"); var met = realms?.is_realm_after_or_equal(human.cultivation_realm, chapter.required_realm) ?? human.cultivation_realm == chapter.required_realm;
            yield return gongfa_requirement_label($"境界：{human.cultivation_realm} / {chapter.required_realm}", met);
        }
        foreach (Variant key in chapter.required_attributes.Keys)
        {
            var name = key.AsString(); var need = chapter.required_attributes[key].AsSingle(); var actual = CardRules.Number(human, name);
            yield return gongfa_requirement_label($"{battle_attribute_name(name)}：{actual:0.0} / {need:0.0}", actual >= need);
        }
        foreach (var id in chapter.required_chapter_ids) yield return gongfa_requirement_label($"{gongfa_chapter_name(book, id)}：{(human.is_gongfa_chapter_learned(book, id) ? "已研读" : "未研读")}", human.is_gongfa_chapter_learned(book, id));
        if (chapter.required_attributes.Count == 0 && chapter.required_chapter_ids.Count == 0 && string.IsNullOrWhiteSpace(chapter.required_realm)) yield return gongfa_requirement_label("当前卷无额外研读条件", true);
    }

    private static Label gongfa_requirement_label(string text, bool met)
    {
        var label = new Label { Text = $"{(met ? "✓" : "✕")} {text}" }; label.AddThemeFontSizeOverride("font_size", 14); label.AddThemeColorOverride("font_color", met ? new Color(.16f, .43f, .22f) : new Color(.64f, .16f, .12f)); return label;
    }

    private static Button gongfa_popup_button(string text)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(0, 44) }; button.AddThemeFontSizeOverride("font_size", 17); button.AddThemeColorOverride("font_color", new Color(.12f, .095f, .065f));
        button.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = new Color(.77f, .67f, .40f, .45f), BorderColor = new Color(.38f, .26f, .08f, .7f), BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1, CornerRadiusTopLeft = 5, CornerRadiusTopRight = 5, CornerRadiusBottomLeft = 5, CornerRadiusBottomRight = 5 }); return button;
    }

    private static string gongfa_tempering_benefit(string key, float value) => key switch
    {
        "max_health_flat" => $"最大生命 +{value:0.#}",
        "max_health_percent" => $"最大生命 +{value:0.#}%",
        "max_mana_yunxi" => $"云息境界起法力上限 +{value:0.#}",
        "qi_blood_cap" => $"气血上限 +{value:0.#}",
        "physical_damage_percent" => $"物理伤害 +{value:0.#}%",
        "dodge_percent" => $"身法 +{value:0.#}%",
        "blood_furnace_cap" => $"血炉上限 +{value:0.#}",
        "fall_heal_rate_pp" => $"气血回落回复率 +{value:0.#}个百分点",
        "wound_physical_reduction_pp_per_stack" => $"每层伤势使所受物理伤害减免额外 +{value:0.#}个百分点",
        "wound_consume_heal_percent_per_stack" => $"主动消耗每层伤势回复最大生命 +{value:0.#}%",
        "defuse_gain_layers" => $"化解获得层数 +{value:0.#}",
        "borrowed_force_damage_pp_per_stack" => $"借力每层物理伤害加成 +{value:0.#}个百分点",
        _ => $"{battle_attribute_name(key)} +{value:0.#}"
    };

    private static string gongfa_tempering_preview(string key, float value) => key switch
    {
        "fall_heal_rate_pp" => $"回落回复 +{value:0.#}个百分点",
        "wound_physical_reduction_pp_per_stack" => $"伤势每层：物理减伤 +{value:0.#}个百分点",
        "wound_consume_heal_percent_per_stack" => $"伤势每层消耗回复 +{value:0.#}%",
        "borrowed_force_damage_pp_per_stack" => $"借力每层物理增伤 +{value:0.#}个百分点",
        _ => trim_gongfa_preview(gongfa_tempering_benefit(key, value))
    };

    private static List<string> gongfa_tempering_lines(GongfaChapterConfig chapter)
    {
        var lines = new List<string>();
        foreach (Variant key in chapter.combat_tempering_bonuses.Keys)
            lines.Add(gongfa_tempering_benefit(key.AsString(), chapter.combat_tempering_bonuses[key].AsSingle()));
        return lines;
    }

    private static string trim_gongfa_preview(string value, int maxLength = 18)
    {
        value = value.Replace("\r", " ").Replace("\n", " ").Trim();
        if (value.Length <= maxLength) return value;
        return value[..Math.Max(1, maxLength - 1)] + "…";
    }

    private static string gongfa_skill_target(ActiveSkillData skill) => BattleInkCombatVfx.IsForestBeiPackCall(skill)
        ? "全体存活的友方林地凶狼（含同物种变种）" : CardRules.IsResponse(skill)
        ? "自身（敌方攻击自身时应对）"
        : skill.target_type switch { 2 => "指定敌方", 3 => "指定任意目标", _ => "自身" };

    private static string gongfa_rewrite_detail(GongfaDeckRewriteConfig rewrite, bool isMain, GodotObject owner)
    {
        var resultSkill = rewrite.get_result_skill(isMain) as ActiveSkillData;
        if (resultSkill is null) return "化法结果技能未配置。";
        var sourceName = !string.IsNullOrWhiteSpace(rewrite.source_skill_name)
            ? rewrite.source_skill_name
            : rewrite.source_skill?.Get("skill_name").AsString() ?? "基础技能";
        var operation = rewrite.mode == GongfaDeckRewriteConfig.RewriteMode.ADD
            ? $"新增《{resultSkill.skill_name}》"
            : sourceName == resultSkill.skill_name
                ? $"《{resultSkill.skill_name}》追加化法效果（保留基础效果）"
                : $"将《{sourceName}》改写为《{resultSkill.skill_name}》";
        var lines = new List<string> { $"化法：{operation}", $"类型：{resultSkill.get_card_category()}" };
        if (resultSkill.tags.Count > 0) lines.Add($"关键词：{string.Join("、", resultSkill.tags)}");
        if (resultSkill.skill_type != ActiveSkillData.SkillType.POWER)
            lines.Add($"耗费：体力 {resultSkill.energy_cost}；法力 {resultSkill.mana_cost}");
        lines.Add($"目标：{gongfa_skill_target(resultSkill)}");
        if (resultSkill.damage_type != "physical" ||
            resultSkill.base_damage > 0 ||
            resultSkill.damage_bonuses.Count > 0 ||
            !string.IsNullOrWhiteSpace(resultSkill.damage_formula))
            lines.Add($"伤害类型：{resultSkill.damage_type switch { "physical" => "物理", "soul" => "神魂", "spell" => "法术", _ => resultSkill.damage_type }}");
        var face = CardPresentation.Underline(System.Text.RegularExpressions.Regex.Replace(CardPresentation.Face(resultSkill, owner), @"(?m)^使用次数：.*(?:\r?\n|$)", "").Trim());
        var isXuanShuRewrite=resultSkill.gongfa_rewrite_effect_ids.Any(id=>id.StartsWith("xuan_shu_",StringComparison.Ordinal));
        if(isXuanShuRewrite)
        {
            lines.Add(face);
            var calculations=CardPresentation.CombatCalculationDetails(resultSkill,owner);
            if(!string.IsNullOrWhiteSpace(calculations))lines.Add($"数值推演：\n{calculations}");
        }
        else lines.Add($"效果：{face}");
        var perBattle = resultSkill.uses_per_battle > 0 ? resultSkill.uses_per_battle : resultSkill.max_charges;
        var limits = new List<string>();
        if (resultSkill.uses_per_round > 0) limits.Add($"每轮{resultSkill.uses_per_round}次");
        if (perBattle > 0) limits.Add($"本场{perBattle}次");
        lines.Add($"使用次数：{(limits.Count > 0 ? string.Join("；", limits) : "不限")}");
        if (resultSkill.cooldown_rounds > 0) lines.Add(CardPresentation.CombatCooldownText(resultSkill, owner, null));
        return string.Join("\n", lines);
    }

    private static string gongfa_branch_status(bool learned, bool isMain, string route, string branch)
    {
        if (!learned) return "未研读";
        if (isMain) return "主脉通达";
        return route == branch ? "已激活" : "可选择";
    }

    private static List<string> gongfa_branch_preview(GongfaChapterConfig chapter, bool left, bool isMain, GodotObject owner)
    {
        if (left)
        {
            var tempering = new List<string>();
            foreach (Variant key in chapter.combat_tempering_bonuses.Keys)
                tempering.Add(gongfa_tempering_preview(key.AsString(), chapter.combat_tempering_bonuses[key].AsSingle()));
            if (tempering.Count == 0) return ["无修持加成"];
            return tempering.Take(2).ToList();
        }

        var previews = new List<string>();
        foreach (var rewrite in chapter.get_deck_rewrites(isMain).Where(item => item.enabled).Take(2))
        {
            if (rewrite.get_result_skill(isMain) is not ActiveSkillData skill) continue;
            var sourceName = !string.IsNullOrWhiteSpace(rewrite.source_skill_name)
                ? rewrite.source_skill_name
                : rewrite.source_skill?.Get("skill_name").AsString() ?? "基础技能";
            var operation = rewrite.mode == GongfaDeckRewriteConfig.RewriteMode.ADD
                ? $"新增《{skill.skill_name}》"
                : sourceName == skill.skill_name
                    ? $"《{skill.skill_name}》追加化法"
                    : $"改写《{sourceName}》→《{skill.skill_name}》";
            previews.Add(operation);
            var effect = GongfaSkillRewriteRuntime.GetEffectPreview(skill);
            if (string.IsNullOrWhiteSpace(effect))
                effect = CardPresentation.Face(skill, owner).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? skill.description;
            if (!string.IsNullOrWhiteSpace(effect)) previews.Add(CardPresentation.Underline(trim_gongfa_preview(effect)));
        }
        return previews.Count > 0 ? previews : ["无"];
    }

    private static string gongfa_branch_tooltip(GongfaChapterConfig chapter, bool left, bool isMain, GodotObject owner)
    {
        var lines = left
            ? new List<string> { "修持（仅战斗内）", "战斗属性：" }.Concat(gongfa_tempering_lines(chapter)).ToList()
            : chapter.get_deck_rewrites(isMain).Where(item => item.enabled).Select(item => gongfa_rewrite_detail(item, isMain, owner)).ToList();
        return lines.Count > 0 ? string.Join("\n", lines) : left ? "修持（仅战斗内）\n无" : "化法\n无";
    }

    private void update_gongfa_heart_method_display(Character human, GongfaResource? main, GongfaResource? sub)
    {
        var heartText = _staticGongfaPage?.GetNodeOrNull<RichTextLabel>("HeartMethodPanel/Box/HeartMethodText");
        if (heartText is null) return;
        var entries = new List<string>();
        void Add(GongfaResource? book, bool isMain)
        {
            var skill = book?.get_heart_method_skill(isMain);
            if (book is null || skill is null) return;
            var learnedOrder = book.get_sorted_chapters()
                .Where(chapter => human.is_gongfa_chapter_learned(book, chapter.get_id()))
                .Select(chapter => chapter.order).DefaultIfEmpty(0).Max();
            var description = CardPresentation.Underline(book.get_heart_method_description(learnedOrder, isMain));
            var currentTags = skill.get_presentation_tags().ToList();
            if(book.gongfa_name=="百骸衍枝化生宝册"&&learnedOrder>=3)
                foreach(var tag in new[]{"应对","化解"})if(!currentTags.Contains(tag))currentTags.Add(tag);
            var keywords = currentTags.Count > 0 ? $"关键词：{string.Join("、", currentTags)}\n" : "";
            entries.Add($"[b]{(isMain ? "主修" : "辅修")}《{book.gongfa_name}》 · {skill.skill_name}[/b]\n{keywords}{description}");
        }
        Add(main, true);
        foreach (var book in human.sub_gongfa_list) Add(book, false);
        heartText.BbcodeEnabled = true;
        heartText.Text = entries.Count > 0 ? string.Join("\n\n", entries) : "装配主修或辅修功法后，心法被动会自动生效并显示在此处。";
    }

    private void update_static_gongfa_tree(Character human)
    {
        if (_staticGongfaPage is null) return;
        var rows = _staticGongfaPage.GetNode<VBoxContainer>("Workspace/TreePanel/TreeRows");
        var chapters = _viewedGongfa?.get_sorted_chapters();
        var displayedCount = Mathf.Clamp(chapters?.Count ?? 0, 0, 5);
        if (chapters is null) { foreach (var row in rows.GetChildren()) if (row is Control control) control.Hide(); return; }
        for (var i = 0; i < 5; i++)
        {
            var row = rows.GetNode<HBoxContainer>($"Row{i + 1}");
            var layer = row.GetNode<Label>("Chapter");
            var left = row.GetNode<Button>("Left");
            var right = row.GetNode<Button>("Right");
            // 移除分支图标，文字居中显示；固定牌框比例并由卷脉行均匀占满高度。
            left.Icon = right.Icon = null;
            left.Alignment = right.Alignment = HorizontalAlignment.Center;
            left.ClipText = right.ClipText = true;
            left.SizeFlagsVertical = right.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            left.CustomMinimumSize = right.CustomMinimumSize = new Vector2(240, 104);
            left.AddThemeFontSizeOverride("font_size", 13);
            right.AddThemeFontSizeOverride("font_size", 13);
            var present = i < displayedCount;
            row.Visible = present;
            if (!present) continue;
            var chapter = chapters[i];
            var learned = human.is_gongfa_chapter_learned(_viewedGongfa!, chapter.get_id());
            layer.SetMeta("gongfa_learned", learned);
            var leftName = string.IsNullOrWhiteSpace(chapter.left_branch_name) ? "修持" : chapter.left_branch_name;
            var rightName = string.IsNullOrWhiteSpace(chapter.right_branch_name) ? "化法" : chapter.right_branch_name;
            layer.Text = $"第{chinese_volume_number(chapter.order)}卷\n{chapter.chapter_name}";
            var worldSpellNames = chapter.learned_world_spells.OfType<WorldSpellData>()
                .Select(spell => spell.spell_name).Where(name => !string.IsNullOrWhiteSpace(name)).ToList();
            layer.TooltipText = $"《{_viewedGongfa!.gongfa_name}》第{chapter.order}卷 · {chapter.chapter_name}" +
                (worldSpellNames.Count > 0 ? $"\n大世界术法：{string.Join("、", worldSpellNames)}" : "");
            var route = human.get_sub_gongfa_chapter_route(_viewedGongfa!, chapter.get_id());
            var leftPreview = gongfa_branch_preview(chapter, true, !_viewingSubGongfa, human);
            var rightPreview = gongfa_branch_preview(chapter, false, !_viewingSubGongfa, human);
            left.Text = $"{leftName} · {gongfa_branch_status(learned, !_viewingSubGongfa, route, "left")}\n{string.Join("\n", leftPreview)}";
            right.Text = $"{rightName} · {gongfa_branch_status(learned, !_viewingSubGongfa, route, "right")}\n{string.Join("\n", rightPreview)}";
            left.TooltipText = gongfa_branch_tooltip(chapter, true, !_viewingSubGongfa, human);
            right.TooltipText = gongfa_branch_tooltip(chapter, false, !_viewingSubGongfa, human);
            left.Disabled = right.Disabled = !learned;
            var leftActive = learned && !_viewingSubGongfa;
            var rightActive = learned && !_viewingSubGongfa;
            if (_viewingSubGongfa && learned)
            {
                leftActive = route == "left";
                rightActive = route == "right";
            }
            left.SetMeta("gongfa_active", leftActive); right.SetMeta("gongfa_active", rightActive);
            // 选中与已激活始终用深墨字，避免落在浅色金笺上失去可读性。
            left.Modulate = learned ? Colors.White : new Color(1f, 1f, 1f, .62f);
            right.Modulate = learned ? Colors.White : new Color(1f, 1f, 1f, .62f);
            var selectedLeft = i == _gongfaSelectedChapter && _gongfaSelectedBranch == "left";
            var selectedRight = i == _gongfaSelectedChapter && _gongfaSelectedBranch == "right";
            left.AddThemeColorOverride("font_color", selectedLeft || leftActive ? new Color(.14f, .085f, .025f) : new Color(.17f, .13f, .08f));
            right.AddThemeColorOverride("font_color", selectedRight || rightActive ? new Color(.14f, .085f, .025f) : new Color(.17f, .13f, .08f));
            // 已激活分支用金墨染色；未激活保持淡墨，避免只靠文字状态辨认。
            left.GetNodeOrNull<TextureRect>("Backdrop")?.Set("modulate", leftActive ? new Color(1.38f, .78f, .24f, 1f) : learned ? new Color(.90f, .86f, .72f, 1f) : new Color(.56f, .53f, .45f, .78f));
            right.GetNodeOrNull<TextureRect>("Backdrop")?.Set("modulate", rightActive ? new Color(1.38f, .78f, .24f, 1f) : learned ? new Color(.90f, .86f, .72f, 1f) : new Color(.56f, .53f, .45f, .78f));
        }
    }

    private static string chinese_volume_number(int value)
    {
        const string numerals = "一二三四五六七八九十";
        return value is >= 1 and <= 10 ? numerals[value - 1].ToString() : value.ToString();
    }

    /// <summary>战斗页管理即时战斗的技能槽，不再读写旧卡组。</summary>
    private void populate_static_battle_page(Creature unit)
    {
        if (_characterCanvas is not null) { refresh_character_battle(unit); return; }
        if (_staticBattlePage is null) return;
        var page = GetNodeOrNull<ScrollContainer>("Content/Page");
        if (page is not null) _staticBattlePage.CustomMinimumSize = new Vector2(0, Mathf.Max(720, page.Size.Y - 8));

        var human = unit as Character;
        var available = human?.get_available_battle_skills()
            .Where(Character.is_combat_skill_slot_eligible)
            .Where(skill => !string.IsNullOrWhiteSpace(skill.ResourcePath))
            .GroupBy(skill => skill.ResourcePath)
            .Select(group => group.First())
            .ToList() ?? [];
        var slotPaths = human?.get_combat_skill_slot_paths().ToList() ?? [];
        var catalog = available.ToList();
        if (human is not null)
        {
            foreach (var path in human.get_default_combat_skill_paths())
            {
                var skill = ResourceLoader.Load<ActiveSkillData>(path);
                if (skill is not null && Character.is_combat_skill_slot_eligible(skill) && !catalog.Any(value => value.ResourcePath == path)) catalog.Add(skill);
            }
        }
        var slotSkills = new List<ActiveSkillData?>();
        foreach (var path in slotPaths)
        {
            var skill = catalog.FirstOrDefault(value => value.ResourcePath == path);
            skill ??= ResourceLoader.Load<ActiveSkillData>(path);
            if (skill is not null && Character.is_combat_skill_slot_eligible(skill))
            {
                slotSkills.Add(skill);
                if (!catalog.Any(value => value.ResourcePath == skill.ResourcePath)) catalog.Add(skill);
            }
        }

        var selected = catalog.FirstOrDefault(skill => skill.ResourcePath == _battleSelectedPath)
            ?? slotSkills.FirstOrDefault(skill => skill is not null)
            ?? catalog.FirstOrDefault();
        _battleSelectedPath = selected?.ResourcePath ?? "";

        var heading = _staticBattlePage.GetNodeOrNull<Label>("Workspace/Loadout/Box/HeadingRow/Heading");
        if (heading is not null) heading.Text = "技能槽";
        _battleSkillCounter = _staticBattlePage.GetNodeOrNull<Label>("Workspace/Loadout/Box/HeadingRow/Counter");
        if (_battleSkillCounter is not null) _battleSkillCounter.Text = $"{slotSkills.Count} / 10";

        void SetKind(string node, string caption, string filter, int count)
        {
            var button = _staticBattlePage.GetNodeOrNull<Button>($"Workspace/Library/Box/Kinds/{node}");
            if (button is not null) button.Text = $"{(_battleFilter == filter ? "●" : "○")} {caption}　{count}";
        }
        SetKind("All", "全部", "all", catalog.Count);
        SetKind("Attack", "攻伐", "attack", catalog.Count(skill => battle_skill_category(skill) == "攻伐"));
        SetKind("Technique", "术诀", "technique", catalog.Count(skill => battle_skill_category(skill) == "术诀"));
        SetKind("Defense", "守御", "defense", catalog.Count(skill => battle_skill_category(skill) == "守御"));

        populate_battle_skill_library(human, catalog, slotPaths);
        populate_battle_slot_grid(human, slotPaths, slotSkills);
        show_battle_skill_detail(selected);
    }

    private void show_battle_skill_detail(ActiveSkillData? skill)
    {
        if (_staticBattlePage is null) return;
        _battleDetailTitle ??= _staticBattlePage.GetNodeOrNull<Label>("Workspace/DetailPanel/Box/DetailTitle");
        _battleDetailMeta ??= _staticBattlePage.GetNodeOrNull<Label>("Workspace/DetailPanel/Box/DetailMeta");
        _battleDetailText ??= _staticBattlePage.GetNodeOrNull<RichTextLabel>("Workspace/DetailPanel/Box/DetailText");
        if (_battleDetailTitle is null || _battleDetailMeta is null || _battleDetailText is null) return;
        if (skill is null)
        {
            _battleDetailTitle.Text = "尚无可装配技能";
            _battleDetailMeta.Text = "等待角色学会可进入即时战斗的技能";
            _battleDetailText.Text = "技能槽装配主动技能或应对技能；排入行动队列后占用行动槽。主修或辅修秘籍的心法被动自动生效，开战时显示于Buff栏，不设独立心法槽。";
            return;
        }

        var category = battle_skill_category(skill);
        if (skill.skill_type == ActiveSkillData.SkillType.POWER)
        {
            _battleDetailMeta.Text = "常驻被动　开战显示于Buff栏";
            var passiveSource = string.IsNullOrWhiteSpace(skill.source_gongfa_name) ? "秘籍" : skill.source_gongfa_name;
            _battleDetailText.Text = $"[b]效果[/b]\n{CardPresentation.Underline(CardPresentation.Face(skill,current_card))}\n\n[b]来源[/b]\n{passiveSource}";
            return;
        }
        _battleDetailTitle.Text = skill.skill_name;
        _battleDetailMeta.Text = $"{category}　体力 {skill.energy_cost}　法力 {skill.mana_cost}";
        var source = string.IsNullOrWhiteSpace(skill.source_gongfa_name) ? "通用" : skill.source_gongfa_name;
        var skillDescription=CardPresentation.Underline(CardPresentation.Face(skill,current_card));
        _battleDetailText.Text = $"[b]技能效果[/b]\n{skillDescription}\n\n[b]来源[/b]\n{source}\n\n[b]装配操作[/b]\n左侧点击加入或移出技能槽；中间可调整顺序或移除。";
    }

    private void bind_static_battle_controls()
    {
        if (_staticBattlePage is null) return;
        // 旧卡组展示是过渡布局，人物战斗页只保留技能库、10 个固定槽与单项详情。
        _staticBattlePage.GetNodeOrNull<Control>("Columns")?.Hide();
        _staticBattlePage.GetNodeOrNull<Control>("DeckOverview")?.Hide();
        var workspace = _staticBattlePage.GetNodeOrNull<HBoxContainer>("Workspace");
        if (workspace is not null) workspace.AddThemeConstantOverride("separation", 12);
        var library = workspace?.GetNodeOrNull<PanelContainer>("Library");
        if (library is not null) library.CustomMinimumSize = new Vector2(270, 0);
        var loadout = workspace?.GetNodeOrNull<PanelContainer>("Loadout");
        if (loadout is not null) loadout.CustomMinimumSize = new Vector2(610, 0);
        var detail = workspace?.GetNodeOrNull<PanelContainer>("DetailPanel");
        if (detail is not null) detail.CustomMinimumSize = new Vector2(270, 0);
        if (_staticBattlePage.GetNodeOrNull<GridContainer>("Workspace/Loadout/Box/SlotScroll/SlotGrid") is { } slotGrid)
        {
            slotGrid.Columns = 5;
            slotGrid.CustomMinimumSize = Vector2.Zero;
        }
        foreach (var (node, filter) in new[] { ("All", "all"), ("Attack", "attack"), ("Technique", "technique"), ("Defense", "defense") })
        {
            var button = _staticBattlePage.GetNodeOrNull<Button>($"Workspace/Library/Box/Kinds/{node}");
            if (button is not null)
            {
                button.MouseFilter = MouseFilterEnum.Stop;
                button.Pressed += () => { _battleFilter = filter; if (current_card is Creature unit) populate_static_battle_page(unit); };
            }
        }
    }

    private void populate_battle_skill_library(Character? human, List<ActiveSkillData> catalog, List<string> slotPaths)
    {
        _battleSkillList ??= _staticBattlePage?.GetNodeOrNull<VBoxContainer>("Workspace/Library/Box/SkillScroll/SkillList");
        var hint = _staticBattlePage?.GetNodeOrNull<Label>("Workspace/Library/Box/Hint");
        if (_battleSkillList is null) return;
        foreach (var child in _battleSkillList.GetChildren()) { _battleSkillList.RemoveChild(child); child.QueueFree(); }
        if (human is null)
        {
            if (hint is not null) hint.Text = "仅角色可调整即时战斗技能槽";
            _battleSkillList.AddChild(new Label { Text = "当前对象没有可调整的技能槽。", AutowrapMode = TextServer.AutowrapMode.WordSmart });
            return;
        }

        if (hint is not null) hint.Text = $"已学技能　{catalog.Count} 项\n点击加入或移出技能槽；排入行动队列后占用行动槽。心法随功法生效。";
        var displayed = catalog.Where(skill => _battleFilter == "all" || battle_skill_category(skill) == (_battleFilter switch { "attack" => "攻伐", "technique" => "术诀", "defense" => "守御", _ => "" })).ToList();
        foreach (var skill in displayed)
        {
            var path = skill.ResourcePath;
            var equippedIndex = slotPaths.IndexOf(path);
            var equipped = equippedIndex >= 0;
            var button = create_battle_skill_library_entry(skill, equipped, equippedIndex);
            button.Pressed += () =>
            {
                _battleSelectedPath = path;
                toggle_battle_skill_slot(human, skill);
            };
            _battleSkillList.AddChild(button);
        }
        if (displayed.Count == 0) _battleSkillList.AddChild(new Label { Text = "该类别暂无可装配技能。", AutowrapMode = TextServer.AutowrapMode.WordSmart });
    }

    private Button create_battle_skill_library_entry(ActiveSkillData skill, bool equipped, int slotIndex)
    {
        var button = new Button { Name = "SkillEntry", Text = "", TooltipText = CardPresentation.Underline(skill.get_skill_description(current_card)), CustomMinimumSize = new Vector2(0, 66), ClipContents = true, MouseFilter = MouseFilterEnum.Stop };
        button.AddThemeStyleboxOverride("normal", BattleLibraryEntryStyle(equipped, false));
        button.AddThemeStyleboxOverride("hover", BattleLibraryEntryStyle(equipped, true));
        button.AddThemeStyleboxOverride("pressed", BattleLibraryEntryStyle(equipped, true));
        // 角色面板也复用局内的显式完整图标绘制，不会与技能栏产生缩放差异。
        var icon = CombatSkillBar.CreateSkillIconVisual(skill, new Vector2(42, 42), new Vector2(8, 10));
        button.AddChild(icon);
        var name = new Label { Text = skill.skill_name, Position = new Vector2(58, 8), Size = new Vector2(158, 24), ClipText = true, MouseFilter = MouseFilterEnum.Ignore };
        name.AddThemeFontSizeOverride("font_size", 16); name.AddThemeColorOverride("font_color", new Color(.12f, .095f, .065f)); button.AddChild(name);
        var category = battle_skill_category(skill);
        var meta = new Label { Text = $"{category}　精 {skill.energy_cost} · 法 {skill.mana_cost}", Position = new Vector2(58, 32), Size = new Vector2(158, 18), ClipText = true, MouseFilter = MouseFilterEnum.Ignore };
        meta.AddThemeFontSizeOverride("font_size", 12); meta.AddThemeColorOverride("font_color", new Color(.30f, .24f, .15f)); button.AddChild(meta);
        var state = new Label { Text = equipped ? $"第 {slotIndex + 1} 槽" : "点击加入", Position = new Vector2(58, 49), Size = new Vector2(158, 14), ClipText = true, MouseFilter = MouseFilterEnum.Ignore };
        state.AddThemeFontSizeOverride("font_size", 11); state.AddThemeColorOverride("font_color", equipped ? new Color(.48f, .29f, .035f) : new Color(.38f, .35f, .28f)); button.AddChild(state);
        return button;
    }

    private void toggle_battle_skill_slot(Character human, ActiveSkillData skill)
    {
        var paths = human.get_combat_skill_slot_paths().ToList();
        var key = Character.combat_skill_key(skill);
        var index = paths.IndexOf(key);
        if (index >= 0) paths[index] = "";
        else { index = paths.FindIndex(path => path.Length == 0); if (index < 0) return; paths[index] = key; }
        human.set_combat_skill_slot_paths(new Godot.Collections.Array<string>(paths));
        populate_static_battle_page(human);
    }

    private static StyleBoxFlat BattleLibraryEntryStyle(bool equipped, bool hovered) => new()
    {
        BgColor = equipped ? new Color(.78f, .67f, .40f, .46f) : hovered ? new Color(.79f, .74f, .62f, .38f) : new Color(.75f, .71f, .61f, .26f),
        BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
        BorderColor = equipped ? new Color(.40f, .28f, .09f, .68f) : new Color(.40f, .34f, .24f, .46f),
        CornerRadiusTopLeft = 5, CornerRadiusTopRight = 5, CornerRadiusBottomLeft = 5, CornerRadiusBottomRight = 5
    };

    private static string battle_skill_category(ActiveSkillData skill)
    {
        if (skill.slot_category == "response" || CardRules.IsResponse(skill)) return "守御";
        return skill.get_skill_category() switch { "攻伐" => "攻伐", "守御" => "守御", _ => "术诀" };
    }

    private void populate_battle_slot_grid(Character? human, List<string> slotPaths, List<ActiveSkillData?> slotSkills)
    {
        _battleSlotGrid ??= _staticBattlePage?.GetNodeOrNull<GridContainer>("Workspace/Loadout/Box/SlotScroll/SlotGrid");
        if (_battleSlotGrid is null) return;
        foreach (var child in _battleSlotGrid.GetChildren()) { _battleSlotGrid.RemoveChild(child); child.QueueFree(); }
        for (var index = 0; index < 10; index++)
        {
            var slotIndex = index;
            var automaticSlot=index==9&&human?.has_xuanshu_heart_method_equipped()==true;
            var holder = new VBoxContainer { CustomMinimumSize = new Vector2(BattleLoadoutSlotWidth, BattleLoadoutSlotHeight + BattleLoadoutActionHeight), SizeFlagsHorizontal = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
            var skill = index < slotSkills.Count ? slotSkills[index] : null;
            if (skill is null)
            {
                var empty = create_battle_empty_slot(index,automaticSlot);
                empty.AddThemeStyleboxOverride("disabled", BattleSlotStyle(false, false));
                holder.AddChild(empty); holder.AddChild(new Control { CustomMinimumSize = new Vector2(0, BattleLoadoutActionHeight), MouseFilter = MouseFilterEnum.Ignore });
            }
            else
            {
                var slot = create_battle_loadout_slot(skill, slotIndex, _battleSelectedPath == skill.ResourcePath);
                slot.Pressed += () => { _battleSelectedPath = skill.ResourcePath; populate_static_battle_page(human!); };
                holder.AddChild(slot);
                var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, CustomMinimumSize = new Vector2(BattleLoadoutSlotWidth, BattleLoadoutActionHeight), MouseFilter = MouseFilterEnum.Stop };
                var up = create_battle_slot_action("↑", () => move_battle_skill(human!, slotIndex, -1));
                var down = create_battle_slot_action("↓", () => move_battle_skill(human!, slotIndex, 1));
                var remove = create_battle_slot_action("×", () => remove_battle_skill(human!, slotIndex));
                up.Disabled = slotIndex == 0; down.Disabled = slotIndex >= slotPaths.Count - 1;
                actions.AddChild(up); actions.AddChild(down); actions.AddChild(remove); holder.AddChild(actions);
            }
            _battleSlotGrid.AddChild(holder);
        }
    }

    private Button create_battle_loadout_slot(ActiveSkillData skill, int index, bool selected)
    {
        var automaticSlot=index==9&&current_card is Character character&&character.has_xuanshu_heart_method_equipped();
        var button = new Button { Name = $"Slot{index + 1}", Text = "", TooltipText = CardPresentation.Underline(CardPresentation.Face(skill,current_card)), CustomMinimumSize = new Vector2(BattleLoadoutSlotWidth, BattleLoadoutSlotHeight), ClipContents = true, MouseFilter = MouseFilterEnum.Stop };
        button.AddThemeStyleboxOverride("normal", BattleSlotStyle(true, selected));
        button.AddThemeStyleboxOverride("hover", BattleSlotStyle(true, true));
        button.AddThemeStyleboxOverride("pressed", BattleSlotStyle(true, true));
        var number = new Label { Text = index == 9 ? "0" : (index + 1).ToString(), Position = new Vector2(18f * BattleLoadoutScale, 38f * BattleLoadoutScale), Size = new Vector2(18f * BattleLoadoutScale, 16f * BattleLoadoutScale), MouseFilter = MouseFilterEnum.Ignore, ZIndex = 4 };
        number.AddThemeFontSizeOverride("font_size", 15); number.AddThemeColorOverride("font_color", new Color(.62f, .51f, .28f)); button.AddChild(number);
        var cost = new Label { Text = $"体力{skill.energy_cost} · 法力{skill.mana_cost}", Position = new Vector2(6f * BattleLoadoutScale, 8f * BattleLoadoutScale), Size = new Vector2(BattleLoadoutSlotWidth - 12f * BattleLoadoutScale, 20f * BattleLoadoutScale), HorizontalAlignment = HorizontalAlignment.Center, ClipText = true, MouseFilter = MouseFilterEnum.Ignore, ZIndex = 4 };
        cost.AddThemeFontSizeOverride("font_size", 15); cost.AddThemeColorOverride("font_color", new Color(.48f, .34f, .15f)); button.AddChild(cost);
        button.AddChild(create_battle_loadout_icon(skill));
        // 已装备槽与空槽使用同一份素材框；此前只有空槽绘制框线，导致技能图标像脱离槽位。
        button.AddChild(create_battle_loadout_frame(skill, selected, true));
        if(automaticSlot)add_skill_slot_lock_overlay(button,
            new Vector2((BattleLoadoutSlotWidth-BattleLoadoutFrameWidth)*.5f,BattleLoadoutFrameTop),
            new Vector2(BattleLoadoutFrameWidth,BattleLoadoutFrameHeight),3);
        var name = new Label { Text = skill.skill_name, Position = new Vector2(6f * BattleLoadoutScale, 160f * BattleLoadoutScale), Size = new Vector2(BattleLoadoutSlotWidth - 12f * BattleLoadoutScale, 18f * BattleLoadoutScale), HorizontalAlignment = HorizontalAlignment.Center, ClipText = true, MouseFilter = MouseFilterEnum.Ignore, ZIndex = 4 };
        name.AddThemeFontSizeOverride("font_size", 16); name.AddThemeColorOverride("font_color", new Color(.12f, .095f, .065f)); button.AddChild(name);
        return button;
    }

    private static SkillIconVisual create_battle_loadout_icon(ActiveSkillData skill)
    {
        return CombatSkillBar.CreateSkillIcon(skill, BattleLoadoutScale);
    }

    private static Button create_battle_empty_slot(int index,bool automaticSlot)
    {
        var button = new Button { Name = $"Slot{index + 1}", Text = "", CustomMinimumSize = new Vector2(BattleLoadoutSlotWidth, BattleLoadoutSlotHeight), Disabled = true, ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
        button.AddThemeStyleboxOverride("disabled", BattleSlotStyle(false, false));
        var number = new Label { Text = index == 9 ? "0" : (index + 1).ToString(), Position = new Vector2(18f * BattleLoadoutScale, 38f * BattleLoadoutScale), Size = new Vector2(18f * BattleLoadoutScale, 16f * BattleLoadoutScale), MouseFilter = MouseFilterEnum.Ignore, ZIndex = 4 };
        number.AddThemeFontSizeOverride("font_size", 15); number.AddThemeColorOverride("font_color", new Color(.48f, .44f, .34f)); button.AddChild(number);
        button.AddChild(create_battle_loadout_frame(null, false, false));
        if(automaticSlot)add_skill_slot_lock_overlay(button,
            new Vector2((BattleLoadoutSlotWidth-BattleLoadoutFrameWidth)*.5f,BattleLoadoutFrameTop),
            new Vector2(BattleLoadoutFrameWidth,BattleLoadoutFrameHeight),3);
        var empty = new Label { Text = "空槽", Position = new Vector2(6f * BattleLoadoutScale, 160f * BattleLoadoutScale), Size = new Vector2(BattleLoadoutSlotWidth - 12f * BattleLoadoutScale, 18f * BattleLoadoutScale), HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore, ZIndex = 4 };
        empty.AddThemeFontSizeOverride("font_size", 16); empty.AddThemeColorOverride("font_color", new Color(.48f, .44f, .34f)); button.AddChild(empty);
        return button;
    }

    /// <summary>
    /// 人物面板不再叠加装饰性角框。边线严格依照实际绘制出的完整图标尺寸，
    /// 方形图标也不会被一只竖向大框压住。
    /// </summary>
    private static Panel create_battle_loadout_frame(ActiveSkillData? skill, bool selected, bool filled)
    {
        var windowSize = new Vector2(BattleLoadoutFrameWidth, BattleLoadoutFrameHeight);
        var windowPosition = new Vector2((BattleLoadoutSlotWidth - BattleLoadoutFrameWidth) * .5f, BattleLoadoutFrameTop);
        var frameRect = new Rect2(windowPosition, windowSize);
        if (filled && skill is not null && !string.IsNullOrWhiteSpace(skill.icon_path))
        {
            var texture = ResourceLoader.Load<Texture2D>(skill.icon_path);
            if (texture is not null)
            {
                var source = texture.GetSize();
                if (source.X > 0 && source.Y > 0)
                {
                    var scale = Mathf.Min(windowSize.X / source.X, windowSize.Y / source.Y);
                    var drawSize = source * scale;
                    frameRect = new Rect2(windowPosition + (windowSize - drawSize) * .5f, drawSize);
                }
            }
        }

        var frame = new Panel { Name = "IconFrame", Position = frameRect.Position, Size = frameRect.Size, MouseFilter = MouseFilterEnum.Ignore, ZIndex = 3 };
        frame.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = Colors.Transparent,
            BorderColor = filled ? (selected ? new Color(.78f, .90f, .58f, .98f) : new Color(.72f, .82f, .72f, .60f)) : new Color(.65f, .70f, .68f, .22f),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3
        });
        return frame;
    }

    private static Button create_battle_slot_action(string text, Action action)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(34, BattleLoadoutActionHeight), Flat = true, MouseFilter = MouseFilterEnum.Stop };
        button.AddThemeFontSizeOverride("font_size", 18); button.AddThemeColorOverride("font_color", new Color(.30f, .24f, .15f)); button.AddThemeColorOverride("font_hover_color", new Color(.58f, .36f, .06f));
        var empty = new StyleBoxEmpty(); button.AddThemeStyleboxOverride("normal", empty); button.AddThemeStyleboxOverride("hover", empty); button.AddThemeStyleboxOverride("pressed", empty); button.AddThemeStyleboxOverride("disabled", empty);
        button.Pressed += action; return button;
    }

    private void move_battle_skill(Character human, int index, int delta)
    {
        var paths = human.get_combat_skill_slot_paths().ToList();
        var target = index + delta;
        if (index < 0 || index >= paths.Count || target < 0 || target >= paths.Count) return;
        (paths[index], paths[target]) = (paths[target], paths[index]);
        var normalized = new Godot.Collections.Array<string>(); foreach (var path in paths) normalized.Add(path);
        human.set_combat_skill_slot_paths(normalized); populate_static_battle_page(human);
    }

    private void remove_battle_skill(Character human, int index)
    {
        var paths = human.get_combat_skill_slot_paths().ToList();
        if (index < 0 || index >= paths.Count) return;
        paths[index] = ""; var normalized = new Godot.Collections.Array<string>(); foreach (var path in paths) normalized.Add(path);
        human.set_combat_skill_slot_paths(normalized); populate_static_battle_page(human);
    }

    private static StyleBoxFlat BattleSlotStyle(bool filled, bool selected) => new()
    {
        BgColor = selected ? new Color(.75f, .62f, .28f, .14f) : filled ? new Color(.18f, .16f, .12f, .04f) : Colors.Transparent,
        BorderColor = new Color(.43f, .38f, .27f, .20f),
        BorderWidthLeft = 0, BorderWidthTop = 0, BorderWidthRight = 0, BorderWidthBottom = 0,
        CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
        ContentMarginLeft = 3, ContentMarginRight = 3, ContentMarginTop = 3, ContentMarginBottom = 3
    };

    private static string battle_attribute_name(string name) => name switch { "physique" => "体魄", "coordination" => "身心协调值", "intelligence" => "灵慧", "soul" => "神魂", "comprehension" => "悟性", "body_proficiency" => "体道造诣", "magic_proficiency" => "术法造诣", "sword_proficiency" => "剑道造诣", "talisman_proficiency" => "符道造诣", "cultivation_rate" => "修炼效率", "mind_regen_rate" => "神识恢复", "mana_regen_rate" => "法力恢复", "physical_damage" => "物理伤害", "max_health" => "生命上限", "max_qi_blood" => "气血上限", "blood_furnace_value_cap" => "血炉上限", "physical_defense" => "物理防御", "precision" => "精准", "dodge" => "身法", "critical" => "会心", "crit_resist" => "护心", "crit_damage_mult" => "会心伤害", "initiative_speed" => "速度", _ => name };

    private static string gongfa_chapter_name(GongfaResource? book, string chapterId)
    {
        if (book is null || string.IsNullOrWhiteSpace(chapterId)) return "下一卷";
        return book.get_sorted_chapters().FirstOrDefault(chapter => chapter.get_id() == chapterId)?.chapter_name ?? "下一卷";
    }

    private void populate_static_gongfa_page(Character human)
    {
        if (_characterCanvas is not null) { refresh_character_gongfa(human); return; }
        if (_staticGongfaPage is null) return;
        var main = human.main_gongfa;
        // 兼容旧存档：同一本秘籍不能同时占用主修、辅修槽。
        if (main is not null && human.sub_gongfa_list.Contains(main)) human.remove_sub_gongfa(main);
        var sub = human.sub_gongfa_list.FirstOrDefault();
        update_gongfa_heart_method_display(human, main, sub);
        if (_viewedGongfa is null || (_viewedGongfa != main && _viewedGongfa != sub && _gongfaPickerSlot.Length == 0))
        {
            _viewedGongfa = main ?? sub;
            _viewingSubGongfa = _viewedGongfa == sub && sub is not null;
        }

        var mainButton = _staticGongfaPage.GetNode<Button>("SlotSelectorRow/EquipPanel/Box/SlotButtons/MainSlot");
        var subButton = _staticGongfaPage.GetNode<Button>("SlotSelectorRow/EquipPanel/Box/SlotButtons/SubSlot");
        mainButton.Text = main is null ? "主修\n尚未装配\n点击选择" : $"主修\n《{main.gongfa_name}》\n已装配";
        subButton.Text = sub is null ? "辅修\n尚未装配\n点击选择" : $"辅修\n《{sub.gongfa_name}》\n已装配";

        update_static_gongfa_tree(human);

        var detailTitle = _staticGongfaPage.GetNode<Label>("Workspace/DetailPanel/Box/DetailTitle");
        var detailState = _staticGongfaPage.GetNode<Label>("Workspace/DetailPanel/Box/DetailState");
        var detailText = _staticGongfaPage.GetNode<RichTextLabel>("Workspace/DetailPanel/Box/DetailText");
        if (_viewedGongfa is null)
        {
            detailTitle.Text = "尚未装配功法";
            detailState.Text = "从上方主修或辅修栏选择已研读秘籍";
            detailText.Text = "秘籍按卷顺序研读。境界不足时，下一卷不会进入研读。";
        }
        else
        {
            var chapters = _viewedGongfa.get_sorted_chapters();
            var learned = chapters.Count(chapter => human.is_gongfa_chapter_learned(_viewedGongfa, chapter.get_id()));
            detailTitle.Text = $"《{_viewedGongfa.gongfa_name}》";
            detailState.Text = $"{(_viewingSubGongfa ? "辅修" : "主修")}　已研读 {learned}/{chapters.Count} 卷";
            var worldSpellSummary = chapters.SelectMany(chapter => chapter.learned_world_spells.OfType<WorldSpellData>()
                .Select(spell => $"{spell.get_icon_bbcode(42)} 第{chinese_volume_number(chapter.order)}卷 · {spell.spell_name} · {(human.is_gongfa_chapter_learned(_viewedGongfa, chapter.get_id()) ? "已解锁" : "学会本卷后解锁")}"))
                .ToList();
            var overview = _viewedGongfa.description.Length > 0 ? _viewedGongfa.description : "每卷对应本功法的一层传承。主修可同时启用两支；辅修可随时切换每层已研读的一支。";
            detailText.BbcodeEnabled = true;
            detailText.Text = overview + (worldSpellSummary.Count > 0
                ? $"\n\n[b]本功法的大世界术法[/b]\n{string.Join("\n", worldSpellSummary)}\n点击对应卷章可查看完整效果。"
                : "");
        }
        update_gongfa_tree_selection_detail(human, detailTitle, detailState, detailText);

        // 选择秘籍改为居中弹窗，页面底部不再临时插入一行内容而迫使玩家滚动。
        _staticGongfaPage.GetNodeOrNull<Control>("Bottom/EquipPanel/Box/BookPicker")?.Hide();

    }

    /// <summary>功法页当前不含研读位；研读操作统一在静室等地点完成。</summary>
    private void refresh_gongfa_live_study_state(Character human) { }

    private static string gongfa_chapter_world_spell_details(GongfaChapterConfig chapter, bool chapter_learned)
    {
        var spells = chapter.learned_world_spells.OfType<WorldSpellData>().ToList();
        if (spells.Count == 0) return "";
        return $"[b]本卷大世界术法[/b]\n{string.Join("\n\n", spells.Select(spell => spell.get_manual_detail_text(chapter_learned)))}";
    }

    private void update_gongfa_tree_selection_detail(Character human, Label title, Label state, RichTextLabel text)
    {
        if (_gongfaBranchActivate is not null) _gongfaBranchActivate.Visible = false;
        if (_viewedGongfa is null || _gongfaSelectedChapter < 0) return;
        var chapters = _viewedGongfa.get_sorted_chapters();
        if (_gongfaSelectedChapter >= chapters.Count) { _gongfaSelectedChapter = -1; _gongfaSelectedBranch = ""; return; }
        var chapter = chapters[_gongfaSelectedChapter]; var learned = human.is_gongfa_chapter_learned(_viewedGongfa, chapter.get_id());
        var isMain = !_viewingSubGongfa;
        text.BbcodeEnabled = true;
        if (string.IsNullOrEmpty(_gongfaSelectedBranch))
        {
            title.Text = $"第{chinese_volume_number(chapter.order)}卷 · {chapter.chapter_name}";
            state.Text = learned ? "本卷已研读 · 可查看两条分支" : "本卷尚未研读 · 查看解锁条件";
            var requirements = new List<string>();
            if (_viewedGongfa.wuxing != GongfaResource.Wuxing.NONE)
            {
                var root = _viewedGongfa.wuxing.ToString().Replace("_", "、");
                var met = _viewedGongfa.is_wuxing_compatible(human.spirit_root_type);
                requirements.Add($"[color={(met ? "#2f6f3a" : "#9b3027")}]灵根：{root}{(met ? "　✓" : "　✕")}[/color]");
            }
            if (!string.IsNullOrEmpty(chapter.required_realm))
            {
                var realms = human.GetNodeOrNull<RealmManager>("/root/RealmManager"); var met = realms?.is_realm_after_or_equal(human.cultivation_realm, chapter.required_realm) ?? human.cultivation_realm == chapter.required_realm;
                requirements.Add($"[color={(met ? "#2f6f3a" : "#9b3027")}]境界：{human.cultivation_realm} / {chapter.required_realm}{(met ? "　✓" : "　✕")}[/color]");
            }
            foreach (Variant key in chapter.required_attributes.Keys)
            {
                var attribute = key.AsString(); var need = chapter.required_attributes[key].AsSingle(); var actual = CardRules.Number(human, attribute); var met = actual >= need;
                requirements.Add($"[color={(met ? "#2f6f3a" : "#9b3027")}]{battle_attribute_name(attribute)}：{actual:0.0} / {need:0.0}{(met ? "　✓" : "　✕")}[/color]");
            }
            foreach (var prerequisite in chapter.required_chapter_ids)
            {
                var met = human.is_gongfa_chapter_learned(_viewedGongfa, prerequisite);
                requirements.Add($"[color={(met ? "#2f6f3a" : "#9b3027")}]{gongfa_chapter_name(_viewedGongfa, prerequisite)}：{(met ? "已研读 ✓" : "未研读 ✕")}[/color]");
            }
            if (requirements.Count == 0) requirements.Add("[color=#2f6f3a]无额外条件[/color]");
            var learnedSkills = chapter.get_learned_skills(isMain).OfType<ActiveSkillData>().Select(skill => $"《{skill.skill_name}》 · {CardPresentation.Underline(CardPresentation.Face(skill, human))}").ToList();
            var permanent = chapter.learn_permanent_bonuses.Keys.Select(key => $"{battle_attribute_name(key.AsString())} +{chapter.learn_permanent_bonuses[key]}").ToList();
            var heartVersion = _viewedGongfa.get_heart_method_description(chapter.order, isMain);
            var heartDetails = string.IsNullOrWhiteSpace(heartVersion) ? "" : $"\n\n[b]本卷心法[/b]\n{CardPresentation.Underline(heartVersion)}";
            var worldSpellDetails = gongfa_chapter_world_spell_details(chapter, learned);
            text.Text = $"[b]解锁条件[/b]\n{string.Join("\n", requirements)}\n\n[b]学会永久属性[/b]\n{(permanent.Count > 0 ? string.Join("\n", permanent) : "无")}\n\n[b]本卷习得技能[/b]\n{(learnedSkills.Count > 0 ? string.Join("\n", learnedSkills) : "无新增技能")}" +
                heartDetails + (worldSpellDetails.Length > 0 ? $"\n\n{worldSpellDetails}" : "") + $"\n\n{chapter.description}";
            return;
        }
        var left = _gongfaSelectedBranch == "left"; var branchName = left ? chapter.left_branch_name : chapter.right_branch_name;
        title.Text = $"第{chinese_volume_number(chapter.order)}卷 · {branchName}";
        var route = isMain ? "主修两条分支均生效" : human.get_sub_gongfa_chapter_route(_viewedGongfa, chapter.get_id());
        state.Text = !learned ? "本卷尚未研读，无法激活" : isMain ? route : route == _gongfaSelectedBranch ? "此分支已激活" : "待确认激活";
        var benefits = new List<string>();
        if (left)
        {
            benefits.AddRange(gongfa_tempering_lines(chapter));
        }
        else foreach (var rewrite in chapter.get_deck_rewrites(isMain).Where(item => item.enabled))
            benefits.Add(gongfa_rewrite_detail(rewrite, isMain, human));
        if (benefits.Count == 0) benefits.Add("无");
        var branchWorldSpellDetails = gongfa_chapter_world_spell_details(chapter, learned);
        text.Text = $"[b]{(left ? "修持属性（仅战斗内）" : "化法效果")}[/b]\n{string.Join("\n", benefits)}" +
            (branchWorldSpellDetails.Length > 0 ? $"\n\n{branchWorldSpellDetails}" : "") + $"\n\n{chapter.description}";
        if (_gongfaBranchActivate is not null)
        {
            _gongfaBranchActivate.Visible = true;
            _gongfaBranchActivate.Disabled = !learned || isMain || route == _gongfaSelectedBranch;
            _gongfaBranchActivate.Text = isMain ? "主修两支均已生效" : route == _gongfaSelectedBranch ? "此分支已激活" : "激活此分支";
        }
    }

    private void equip_selected_gongfa(Character human, GongfaResource book)
    {
        var slot = _gongfaPickerSlot;
        var success = false;
        if (slot == "main") success = human.switch_main_gongfa(book);
        else if (_gongfaPickerSlot == "sub")
        {
            if(!human.has_learned_gongfa(book)||!book.can_be_sub)return;
            if(human.sub_gongfa_list.Contains(book))success=true;
            else
            {
            // 同一本秘籍转入辅修时，自动从主修卸下；反向切为主修同理由 switch_main_gongfa 处理。
            if (book == human.main_gongfa) human.unequip_main_gongfa();
            var oldSub = human.sub_gongfa_list.FirstOrDefault();
            if (oldSub is not null) human.remove_sub_gongfa(oldSub);
            success = human.add_sub_gongfa(book);
            }
        }
        if (!success) return;
        _viewedGongfa = book;
        _viewingSubGongfa = slot == "sub";
        _gongfaPickerSlot = "";
        populate_static_gongfa_page(human);
    }

    /// <summary>属性页由场景节点固定承载；脚本只写入数值，保证可在编辑器直接编排。</summary>
    private void populate_static_attribute_page(Creature unit, Character? human)
    {
        void Row(string nodeName, string title, string value)
        {
            if (_staticAttributePage?.FindChild(nodeName, true, false) is not HBoxContainer row) return;
            var nameLabel = row.GetNodeOrNull<Label>("Name");
            var valueLabel = row.GetNodeOrNull<Label>("Value");
            if (nameLabel is null && row.GetNodeOrNull<Label>("Display") is { } display)
            {
                // 场景原本只有一个合并标签。运行时拆为名称和值，分别承载说明和计算公式。
                display.Name = "Name"; nameLabel = display;
                valueLabel = display.Duplicate() as Label;
                if (valueLabel is not null) { valueLabel.Name = "Value"; valueLabel.HorizontalAlignment = HorizontalAlignment.Left; valueLabel.SizeFlagsHorizontal = SizeFlags.ShrinkBegin; row.AddChild(valueLabel); }
            }
            if (nameLabel is null || valueLabel is null) return;
            row.AddThemeConstantOverride("separation", 9);
            nameLabel.Text = title; nameLabel.SizeFlagsHorizontal = SizeFlags.ShrinkBegin; nameLabel.CustomMinimumSize = Vector2.Zero;
            valueLabel.Text = value; valueLabel.HorizontalAlignment = HorizontalAlignment.Left; valueLabel.SizeFlagsHorizontal = SizeFlags.ShrinkBegin; valueLabel.CustomMinimumSize = Vector2.Zero;
        }
        var rootData = human?.entity_data as HumanData;
        int Root(int index) => rootData is null || rootData.root_weights.Count <= index ? 0 : rootData.root_weights[index].AsInt32();
        Row("PhysiqueRow", "体魄", unit.physique.ToString("0.0")); Row("IntelligenceRow", "灵慧", unit.intelligence.ToString("0.0"));
        Row("CoordinationRow", "身心协调值（派生）", unit.coordination.ToString("0.0")); Row("SoulRow", "神魂", unit.soul.ToString("0.0"));
        Row("FortuneRow", "福缘", unit.fortune.ToString("0.0")); Row("ComprehensionRow", "悟性", unit.comprehension.ToString("0.0"));
        Row("CharismaRow", "魅力", data_number(unit,"charisma").ToString("0.0")); Row("SocialRow", "交际", data_number(unit,"social").ToString("0.0")); Row("ManagementRow", "治事", data_number(unit,"management").ToString("0.0"));
        Row("HealthRow", "生命", $"{unit.current_health}/{unit.max_health}"); Row("StaminaRow", "体力", $"{unit.current_stamina}/{unit.max_stamina}"); Row("ManaRow", "法力", $"{unit.current_mana}/{unit.max_mana}");
        Row("MindRow", "速度", unit.initiative_speed.ToString("0.0"));
        Row("SpiritPerceptionRow", "灵识", $"{unit.used_spirit_perception}/{unit.max_spirit_perception}");
        Row("PhysicalDefenseRow", "物理防御", unit.physical_defense.ToString()); Row("PrecisionRow", "精准", unit.precision.ToString()); Row("DodgeRow", "身法", unit.dodge.ToString());
        Row("CriticalRow", "会心", unit.critical.ToString()); Row("CritResistRow", "护心", unit.crit_resist.ToString()); Row("CritDamageRow", "会心伤害", unit.crit_damage_mult.ToString("0.00"));
        Row("StaminaRegenRow", "物理减伤", $"{unit.physical_damage_reduction*100:0.#}%"); Row("ManaRegenRow", "遁速", human?.dun_speed.ToString("0.#") ?? "未配置");
        Row("MetalRootRow", "金灵根", Root(0).ToString()); Row("WoodRootRow", "木灵根", Root(1).ToString()); Row("WaterRootRow", "水灵根", Root(2).ToString()); Row("FireRootRow", "火灵根", Root(3).ToString()); Row("EarthRootRow", "土灵根", Root(4).ToString());
        Row("SpiritRootRow", "灵根", human?.spirit_root_name ?? "未详"); Row("RealmRow", "境界", unit.cultivation_realm); Row("StrandsRow", "灵力", $"{unit.spirit_strands} 缕");
        Row("CoreRow", "灵核", human?.get_spirit_core_summary() ?? "—"); Row("LearningRow", "学习效率", human is null ? "—" : human.get_learning_rate().ToString("0.00") + "/小时"); Row("CultivationRow", "修炼效率", human is null ? "—" : human.get_cultivation_rate().ToString("0.000") + "/小时");
        Row("SwordRow", "剑道", data_number(unit, "sword_proficiency").ToString("0.0")); Row("BladeRow", "刀道", data_number(unit, "blade_proficiency").ToString("0.0")); Row("SpearRow", "枪道", data_number(unit, "spear_proficiency").ToString("0.0"));
        Row("BodyRow", "体道", data_number(unit, "body_proficiency").ToString("0.0")); Row("MagicRow", "术法", data_number(unit, "magic_proficiency").ToString("0.0")); Row("ForgingRow", "器道", data_number(unit, "forging_proficiency").ToString("0.0"));
        Row("TalismanRow", "符道", data_number(unit, "talisman_proficiency").ToString("0.0")); Row("FormationRow", "阵道", data_number(unit, "formation_proficiency").ToString("0.0")); Row("AlchemyRow", "丹道", data_number(unit, "alchemy_proficiency").ToString("0.0"));
        bind_static_attribute_tooltips(unit, human, rootData);
    }

    /// <summary>
    /// 新版属性页的每一项只有一个 Display 标签；旧版 refresh_tooltips 只寻找旧节点，
    /// 导致场景改版后「名称和值悬停」一并失效。这里以行名为稳定锚点重新绑定。
    /// </summary>
    private void bind_static_attribute_tooltips(Creature unit, Character? human, HumanData? rootData)
    {
        var rootTotal = rootData is null ? 0 : rootData.root_weights.Sum(weight => weight.AsInt32());
        void Bind(string rowName, string description, string formula)
        {
            if (_staticAttributePage?.FindChild(rowName, true, false) is not HBoxContainer row) return;
            if (row.GetNodeOrNull<Label>("Name") is { } nameLabel) bind_tooltip(nameLabel, description);
            if (row.GetNodeOrNull<Label>("Value") is { } valueLabel) bind_tooltip(valueLabel, formula);
        }
        void Base(string row, string name, string description, string formula) => Bind(row, $"{name}\n{description}", $"{name}\n{formula}");

        Base("PhysiqueRow", "体魄", "肉身根基，参与生命、行动、身法与物理伤害。", $"当前体魄 = 基础体魄 + 功法 + 装备 + 状态修正\n生命贡献 = 9 × 最终体魄 + 2 × 基础体魄\n行动速度贡献 = 0.5 × 体魄；精准贡献 = 0.5 × 体魄\n当前值：{unit.physique:0.0}");
        Base("IntelligenceRow", "灵慧", "思虑与感知根基，参与行动速度、精准、身法、会心与护心。", $"当前灵慧 = 基础灵慧 + 功法 + 装备 + 状态修正\n生命贡献 = 1.5 × 灵慧；行动速度贡献 = 0.75 × 灵慧\n精准贡献 = 7 × 灵慧；身法贡献 = 1 × 灵慧；会心贡献 = 0.3 × 灵慧；护心贡献 = 2 × 灵慧\n当前值：{unit.intelligence:0.0}");
        Base("CoordinationRow", "身心协调值（派生）", "体魄与神魂相互制约后的协调值，不独立成长或存档。", $"身心协调值 = min(体魄, 神魂) = {unit.coordination:0.0}\n参与行动速度、身法、护心与部分应对判定。");
        Base("SoulRow", "神魂", "感知与反应根基，参与行动速度、精准、身法、会心与护心。", $"当前神魂 = 基础神魂 + 功法 + 装备 + 状态修正\n生命贡献 = 3 × 神魂；行动速度贡献 = 0.75 × 神魂\n精准贡献 = 1 × 神魂；身法贡献 = 2 × 神魂；会心贡献 = 2 × 神魂；护心贡献 = 1.5 × 神魂\n当前值：{unit.soul:0.0}");
        Base("FortuneRow", "福缘", "参与事件、探索与收获的福缘判定；当前不参与固定战斗派生属性计算。", $"当前福缘 = 基础福缘 + 功法/事件修正\n当前值：{unit.fortune:0.0}");
        Base("ComprehensionRow", "悟性", "参与功法研读、修行与相关事件的悟性判定；当前不参与固定战斗派生属性计算。", $"当前悟性 = 基础悟性 + 功法/事件修正\n当前值：{unit.comprehension:0.0}");

        Base("CharismaRow", "魅力", "参与交涉、招揽与人物事件。", $"当前魅力：{data_number(unit,"charisma"):0.0}");
        Base("SocialRow", "交际", "参与往来、交易与外务事件。", $"当前交际：{data_number(unit,"social"):0.0}");
        Base("ManagementRow", "治事", "参与事务调度与经营事件。", $"当前治事：{data_number(unit,"management"):0.0}");
        Base("HealthRow", "生命", "生命归零时角色无法继续行动。", $"当前生命：{unit.current_health}/{unit.max_health}\n生命上限由角色属性、功法、装备与状态共同决定。");
        Base("StaminaRow", "体力", "用于施展招式；每轮补给3点，休整回复2点，均不超过上限。", $"战斗内上限：6\n当前/上限：{unit.current_stamina}/{unit.max_stamina}");
        Base("ManaRow", "法力", "用于术法等行动；跨轮保留，不自动回复。只有明确写出的技能或效果可回复法力。", $"法力容量由境界及已有修正规则决定\n当前/上限：{unit.current_mana}/{unit.max_mana}");
        Base("MindRow", "速度", "每秒获得的行动进度，达到1000时执行队首。不是遁速或身法。", $"基础值及功法、状态、伤势共同影响战斗实际速度。\n常驻速度：{unit.initiative_speed:0.0}");
        Base("SpiritPerceptionRow", "灵识", "御使飞剑、法宝、傀儡、阵旗等持续实体的容量；不是普通术法的即时费用。", $"显示方式 = 已占用 / 容量\n收回、损毁或驱散持续实体后释放占用\n当前：{unit.used_spirit_perception}/{unit.max_spirit_perception}");
        Base("PhysicalDefenseRow", "物理防御", "减轻武器、拳脚等物理伤害。", $"当前物理防御 = 基础 + 装备 + 功法 + 状态修正\n当前值：{unit.physical_defense}");
        Base("PrecisionRow", "精准", "决定攻击与技艺命中的基础能力。", $"精准 = 7 × 灵慧 + 神魂 + 0.5 × 体魄\n当前值：{unit.precision}");
        Base("DodgeRow", "身法", "决定规避的基础能力，不替代速度或遁速。", $"身法 = 4 × 身心协调值 + 2 × 神魂 + 灵慧 + 0.5 × 体魄\n当前值：{unit.dodge}");
        Base("CriticalRow", "会心", "影响造成关键效果的基础能力。", $"会心 = 0.5 × 体魄 + 2 × 神魂 + 0.3 × 灵慧\n当前值：{unit.critical}");
        Base("CritResistRow", "护心", "抵抗敌方会心与关键伤害的基础能力。", $"护心 = 3 × 体魄 + 2 × 灵慧 + 2 × 身心协调值 + 1.5 × 神魂\n当前值：{unit.crit_resist}");
        Base("CritDamageRow", "会心伤害", "会心触发时使用的伤害倍率。", $"会心伤害倍率 = 基础倍率 + 功法/装备/状态修正\n当前倍率：{unit.crit_damage_mult:0.00}");
        Base("StaminaRegenRow", "物理减伤", "按现行伤害规则减轻物理伤害。", $"当前：{unit.physical_damage_reduction*100:0.#}%");
        Base("ManaRegenRow", "遁速", "影响大世界移动与撤退成功概率，不影响战斗行动条。", $"当前：{human?.dun_speed.ToString("0.#") ?? "未配置"}");

        var roots = new[] { ("MetalRootRow", "金"), ("WoodRootRow", "木"), ("WaterRootRow", "水"), ("FireRootRow", "火"), ("EarthRootRow", "土") };
        for (var index = 0; index < roots.Length; index++)
        {
            var (row, element) = roots[index]; var weight = rootData is null || rootData.root_weights.Count <= index ? 0 : rootData.root_weights[index].AsInt32();
            Base(row, $"{element}灵根权重", "灵根权重是角色原有灵根构成的可视化拆分，不是本次新增属性。", $"{element}权重：{weight}\n五行权重合计：{rootTotal}\n后续将用于计算功法五行适配与修炼收益。");
        }
        Base("SpiritRootRow", "灵根", "角色是否具备引灵入道的资质，以及灵根构成的概述。", $"当前灵根：{human?.spirit_root_name ?? "未详"}\n无灵根角色不能进行常规修炼。");
        Base("RealmRow", "境界", "当前修行阶段，决定突破门槛与可用内容。", $"当前境界：{unit.cultivation_realm}\n突破需满足对应的灵力、属性与功法条件。");
        Base("StrandsRow", "灵力", "修行积累的根基，达到要求后可尝试突破。", $"当前灵力：{unit.spirit_strands} 缕\n增长由修炼效率、环境和功法适配共同决定。");
        Base("CoreRow", "灵核", "云息六层至七层凝成的统一修行根基。", $"当前灵核：{human?.get_spirit_core_summary() ?? "未凝"}\n体积、密度与杂质由凝核牌局结算；玄基仍在后续独立突破。");
        Base("LearningRow", "学习效率", "影响秘籍研读的推进速度；不受灵根和环境灵气影响。", $"学习效率 = max(0.01, 0.55 + 悟性×0.030 + 神魂×0.012 + 灵慧×0.015)\n当前：{(human?.get_learning_rate() ?? 0):0.00}/小时");
        Base("CultivationRow", "修炼效率", "影响灵力积累速度；灵根、环境与主修功法缺一不可。", $"修炼效率 = 0.0020 × 境界倍率 × 灵根倍率 × 环境系数\n灵根倍率 = 灵根资质 × 功法五行匹配\n灵石、阵法与机缘均提高环境灵气浓度\n当前：{(human?.get_cultivation_rate() ?? 0):0.000}/小时");

        foreach (var (row, name) in new[] { ("SwordRow", "剑道"), ("BladeRow", "刀道"), ("SpearRow", "枪道"), ("BodyRow", "体道"), ("MagicRow", "术法"), ("ForgingRow", "器道"), ("TalismanRow", "符道"), ("FormationRow", "阵道"), ("AlchemyRow", "丹道") })
            Base(row, name, $"{name}造诣影响相关技艺、配方与战斗手段的掌握。", $"当前{name}造诣 = 基础造诣 + 功法/装备/技艺修正\n当前值：{data_number(unit, row switch { "SwordRow" => "sword_proficiency", "BladeRow" => "blade_proficiency", "SpearRow" => "spear_proficiency", "BodyRow" => "body_proficiency", "MagicRow" => "magic_proficiency", "ForgingRow" => "forging_proficiency", "TalismanRow" => "talisman_proficiency", "FormationRow" => "formation_proficiency", _ => "alchemy_proficiency" }):0.0}");
    }

    private void populate(WorldEntity entity)
    {
        set_label("NameValue", entity.entity_name);
        if (entity is not Creature unit) return;
        set_label("AgeValue", $"{unit.current_age} / {unit.max_age} 岁");
        set_label("GenderValue", string.IsNullOrWhiteSpace(entity.family_gender) ? "未详" : entity.family_gender);
        set_label("RealmValue", unit.cultivation_realm); set_label("FactionValue", entity.get_faction_display_name());
        set_label("PhysiqueValue", unit.physique.ToString("0.0"));
        set_label("IntelligenceValue", unit.intelligence.ToString("0.0"));
        set_label("SoulValue", unit.soul.ToString("0.0")); set_label("FortuneValue", unit.fortune.ToString("0.0"));
        set_label("ComprehensionValue", unit.comprehension.ToString("0.0")); set_label("SpiritStrandsValue", $"{unit.spirit_strands} 缕");
        set_label("StatusSummaryValue", $"生命 {unit.current_health}/{unit.max_health}　法力 {unit.current_mana}/{unit.max_mana}");
        populate_attribute_values(unit);
        refresh_tooltips(unit);
        if (entity is Character human)
        {
            set_label("SpiritrootValue", human.spirit_root_name); set_label("CoreCountValue", human.get_spirit_core_summary());
            set_label("PersonalityValue", "待观"); set_label("DestinyValue", "待观");
        }
        refresh_fullscreen_summary(entity as Creature, entity as Character);
    }

    /// <summary>全屏人物档案：左侧人物与纵向分页，中部内容，右侧状态。保留原卷轴节点及其各页功能。</summary>
    private void configure_fullscreen_layout()
    {
        if (_scrollBody is null) return;
        _scrollBody.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        if (GetNodeOrNull<TextureRect>("ScrollBody/CanvasBg") is { } canvas) { canvas.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); canvas.OffsetLeft = 12; canvas.OffsetTop = 12; canvas.OffsetRight = -12; canvas.OffsetBottom = -12; }
        GetNodeOrNull<CanvasItem>("ScrollBody/LeftAxle")?.Hide(); GetNodeOrNull<CanvasItem>("ScrollBody/RightAxle")?.Hide();
        var content = GetNodeOrNull<Control>("ScrollBody/ContentPanel");
        if (content is not null)
        {
            content.SetAnchorsPreset(LayoutPreset.FullRect); content.OffsetLeft = 316; content.OffsetTop = 112; content.OffsetRight = -304; content.OffsetBottom = -34;
            var bar = content.GetNodeOrNull<Container>("TabBar");
            if (bar is not null)
            {
                var nav = new VBoxContainer { Name = "FullscreenNav", MouseFilter = MouseFilterEnum.Ignore };
                nav.SetAnchorsPreset(LayoutPreset.TopLeft); nav.Position = new Vector2(28, 148); nav.Size = new Vector2(242, 420); nav.AddThemeConstantOverride("separation", 9); _scrollBody.AddChild(nav);
                foreach (var child in bar.GetChildren().ToArray()) { bar.RemoveChild(child); nav.AddChild(child); if (child is Control control) { control.CustomMinimumSize = new Vector2(242, 48); control.SizeFlagsHorizontal = SizeFlags.ExpandFill; } }
                bar.Hide();
            }
            foreach (var page in new[] { _profilePage, _gongfaPage, _equipPage, _relationsPage }) if (page is not null) { page.OffsetTop = 0; page.OffsetBottom = 0; page.SetAnchorsPreset(LayoutPreset.FullRect); }
        }
        var tabNames = new[] { "总览", "属性", "功法", "装备", "关系" }; for (var i = 0; i < tabNames.Length; i++) if (_tabButtons[i] is not null) _tabButtons[i].Text = tabNames[i];
        build_fullscreen_sidebars();
    }

    private void build_fullscreen_sidebars()
    {
        var left = side_panel("CharacterSummary", new Vector2(28, 92), new Vector2(242, 286)); _scrollBody!.AddChild(left);
        var leftBox = new VBoxContainer(); leftBox.AddThemeConstantOverride("separation", 10); left.AddChild(padded(leftBox));
        _fullscreenName = new Label { Text = "人物档案", HorizontalAlignment = HorizontalAlignment.Center }; _fullscreenName.AddThemeFontSizeOverride("font_size", 28); leftBox.AddChild(_fullscreenName);
        _fullscreenSummary = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(0, 94) }; _fullscreenSummary.AddThemeFontSizeOverride("font_size", 16); leftBox.AddChild(_fullscreenSummary);
        var right = side_panel("CharacterStatus", new Vector2(-278, 92), new Vector2(242, 300), true); _scrollBody.AddChild(right);
        var rightBox = new VBoxContainer(); rightBox.AddThemeConstantOverride("separation", 12); right.AddChild(padded(rightBox));
        var title = new Label { Text = "当前修行", HorizontalAlignment = HorizontalAlignment.Center }; title.AddThemeFontSizeOverride("font_size", 21); rightBox.AddChild(title);
        _fullscreenSchedule = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(0, 130) }; rightBox.AddChild(_fullscreenSchedule);
    }

    private static PanelContainer side_panel(string name, Vector2 position, Vector2 size, bool right = false)
    {
        var panel = new PanelContainer { Name = name, MouseFilter = MouseFilterEnum.Stop }; panel.SetAnchorsPreset(right ? LayoutPreset.TopRight : LayoutPreset.TopLeft); panel.Position = position; panel.CustomMinimumSize = new Vector2(size.X, 0); panel.Size = size;
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(.88f, .84f, .73f, .9f), BorderColor = new Color(.31f, .27f, .2f, .7f), BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1, CornerRadiusTopLeft = 7, CornerRadiusTopRight = 7, CornerRadiusBottomLeft = 7, CornerRadiusBottomRight = 7 }); return panel;
    }
    private static MarginContainer padded(Control child) { var margin = new MarginContainer(); margin.AddThemeConstantOverride("margin_left", 16); margin.AddThemeConstantOverride("margin_right", 16); margin.AddThemeConstantOverride("margin_top", 16); margin.AddThemeConstantOverride("margin_bottom", 16); margin.AddChild(child); return margin; }
    private void refresh_fullscreen_summary(Creature? unit, Character? human)
    {
        if (unit is null) return;
        if (_fullscreenName is not null) _fullscreenName.Text = unit.entity_name;
        if (_fullscreenSummary is not null) _fullscreenSummary.Text = $"{unit.cultivation_realm}\n{(human?.spirit_root_name ?? "未详")}\n生命 {unit.current_health}/{unit.max_health}\n法力 {unit.current_mana}/{unit.max_mana}";
        if (_fullscreenSchedule is not null) _fullscreenSchedule.Text = human is null ? "此角色暂无修行信息。" : $"主修：{(human.main_gongfa?.gongfa_name ?? "尚未设置")}\n研读：{(human.is_studying() ? "进行中" : "暂无")}\n修炼：{(human.can_cultivate() ? "可执行" : "暂不可执行")}\n\n修炼与休息均由玩家在对应地点主动进行。";
    }

    private void populate_gongfa(Character? human)
    {
        set_label("GongfaHeader", human is null ? "功法" : $"{human.entity_name}的功法");
        set_label("GongfaHint", human is null ? "此人尚无可查看的功法。" : human.main_gongfa is null ? "尚未主修功法。" : $"主修《{human.main_gongfa.gongfa_name}》｜卷章决定可用法门，研读完成后才会解锁。 ");
        refresh_gongfa_workspace(human);
        var slots = find_node(this, "StudySlots") as VBoxContainer;
        if (slots is null) return;
        foreach (var child in slots.GetChildren()) child.QueueFree();
        if (human is null) return;
        var snapshot = human.get_study_slot_snapshot();
        var occupied = 0;
        foreach (var slot in snapshot)
        {
            var data = slot;
            if (!data.GetValueOrDefault("occupied", false).AsBool()) continue;
            occupied++;
            var book = data.GetValueOrDefault("book", default).AsGodotObject() as SecretBook;
            if (book?.gongfa is null) continue;
            var required = Mathf.Max(book.required_study_progress, .1f);
            var progress = Mathf.Clamp(book.current_study_progress / required * 100f, 0, 100);
            var card = new PanelContainer { TooltipText = book.gongfa.description, CustomMinimumSize = new Vector2(0, 68) };
            card.AddThemeStyleboxOverride("panel", gongfa_card_style()); slots.AddChild(card);
            var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 4); card.AddChild(box);
            var text = new Label { Text = $"《{book.gongfa.gongfa_name}》　{(book.is_studying_now ? "研读中" : "已停下")}　{progress:0}%" };
            text.AddThemeFontSizeOverride("font_size", 16); text.Modulate = new Color(.25f, .25f, .25f); box.AddChild(text);
            var bar = new ProgressBar { MinValue = 0, MaxValue = 100, Value = progress, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 10) }; box.AddChild(bar);
        }
        set_label("GongfaStudyTitle", occupied == 0 ? "研读中的秘籍（暂无）" : $"研读中的秘籍（{occupied}/3）");
    }

    private void bind_gongfa_controls()
    {
        if (find_node(this, "TreeBookPicker") is OptionButton picker) picker.ItemSelected += index => { if (current_card is Character human && index >= 0 && index < _gongfaBooks.Count) configure_tree(human, _gongfaBooks[(int)index]); };
        if (find_node(this, "TreeMainButton") is Button main) main.Pressed += () => { if (current_card is Character human && selected_gongfa() is { } book) { human.switch_main_gongfa(book); populate_gongfa(human); } };
        if (find_node(this, "TreeSubButton") is Button sub) sub.Pressed += () => { if (current_card is Character human && selected_gongfa() is { } book && book != human.main_gongfa) { human.add_sub_gongfa(book); populate_gongfa(human); } };
    }

    private GongfaResource? selected_gongfa()
    {
        var picker = find_node(this, "TreeBookPicker") as OptionButton;
        return picker is not null && picker.Selected >= 0 && picker.Selected < _gongfaBooks.Count ? _gongfaBooks[picker.Selected] : null;
    }

    private void refresh_gongfa_workspace(Character? human)
    {
        var picker = find_node(this, "TreeBookPicker") as OptionButton;
        _gongfaBooks.Clear(); if (human is not null) foreach (var book in human.get_known_gongfas()) _gongfaBooks.Add(book);
        if (picker is not null) { picker.Clear(); foreach (var book in _gongfaBooks) picker.AddItem($"《{book.gongfa_name}》"); picker.Disabled = _gongfaBooks.Count == 0; }
        if (human is null || _gongfaBooks.Count == 0) { fill_gongfa_lists(null); return; }
        var selected = human.main_gongfa is not null ? human.main_gongfa : _gongfaBooks[0];
        if (picker is not null) picker.Select(_gongfaBooks.IndexOf(selected));
        configure_tree(human, selected); fill_gongfa_lists(human);
    }

    private void configure_tree(Character human, GongfaResource book)
    {
        if (find_node(this, "GongfaTree") is GongfaTree tree) tree.configure(human, book, book == human.main_gongfa);
        if (find_node(this, "TreeMainButton") is Button main) { var compatible = book.is_wuxing_compatible(human.spirit_root_type); main.Disabled = book == human.main_gongfa || !book.can_be_main || !compatible; main.Text = book == human.main_gongfa ? "当前主修" : compatible ? "设为主修" : "灵根不适配"; }
        if (find_node(this, "TreeSubButton") is Button sub) { sub.Disabled = book == human.main_gongfa || !book.can_be_sub || human.sub_gongfa_list.Contains(book); sub.Text = human.sub_gongfa_list.Contains(book) ? "已列辅修" : "列为辅修"; }
    }

    private void fill_gongfa_lists(Character? human)
    {
        var main = find_node(this, "MainGongfaBoard") as Container; var sub = find_node(this, "SubGongfaList") as Container; var learned = find_node(this, "LearnedGongfaList") as Container;
        clear_children(main); clear_children(sub); clear_children(learned);
        if (human is null) return;
        if (main is not null) main.AddChild(gongfa_row(human.main_gongfa, "主修 · 作为修炼根本", human));
        if (sub is not null) { if (human.sub_gongfa_list.Count == 0) sub.AddChild(muted("暂无辅修功法。")); else foreach (var book in human.sub_gongfa_list) sub.AddChild(gongfa_row(book, "辅修 · 提供兼修效果", human)); }
        if (learned is not null) { var any = false; foreach (var book in human.get_known_gongfas()) { if (!human.has_learned_gongfa(book)) continue; learned.AddChild(gongfa_row(book, "已学", human)); any = true; } if (!any) learned.AddChild(muted("尚无完整学会的功法；首卷研读完成后会显示于此。")); }
    }

    private Control gongfa_row(GongfaResource? book, string state, Character human)
    {
        if (book is null) return muted("尚未设置主修功法。 ");
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(0, 58), TooltipText = book.description }; panel.AddThemeStyleboxOverride("panel", gongfa_card_style());
        var box = new VBoxContainer(); panel.AddChild(box); var title = new Label { Text = $"《{book.gongfa_name}》　{state}" }; title.AddThemeFontSizeOverride("font_size", 16); box.AddChild(title);
        var chapters = book.get_sorted_chapters(); var done = chapters.Count(chapter => human.is_gongfa_chapter_learned(book, chapter.get_id())); box.AddChild(muted($"卷章 {done}/{chapters.Count}　{book.description}")); return panel;
    }


    private static void clear_children(Container? container) { if (container is not null) foreach (var child in container.GetChildren()) child.QueueFree(); }
    private static Label muted(string text) { var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart }; label.Modulate = new Color(.38f, .36f, .3f); label.AddThemeFontSizeOverride("font_size", 14); return label; }
    private static StyleBoxFlat gongfa_card_style() => new() { BgColor = new Color(.87f, .83f, .71f, .38f), BorderColor = new Color(.42f, .34f, .22f, .6f), BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1, CornerRadiusTopLeft = 5, CornerRadiusTopRight = 5, CornerRadiusBottomLeft = 5, CornerRadiusBottomRight = 5, ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 6, ContentMarginBottom = 6 };

    private void populate_equip(Character? human) => set_label("PackTitle", human is null || human.pack_items.Count == 0 ? "随身物资：暂无" : $"随身物资：{human.pack_items.Count} 类");
    private void populate_relations() => set_label("EmptyRelations", "亲缘、声望与往来会在家族发展中逐步记录。");

    private void populate_attribute_values(Creature unit)
    {
        // 旧档案页原本预留的字段全部给出当前基础值；尚未有专门成长系统的字段以数据底值显示。
        set_label("CharismaValue", data_number(unit, "charisma").ToString("0.0"));
        set_label("SocialValue", data_number(unit, "social").ToString("0.0"));
        set_label("ManagementValue", data_number(unit, "management").ToString("0.0"));
        set_label("SwordValue", data_number(unit, "sword_proficiency").ToString("0.0"));
        set_label("BladeValue", data_number(unit, "blade_proficiency").ToString("0.0"));
        set_label("SpearValue", data_number(unit, "spear_proficiency").ToString("0.0"));
        set_label("BodyValue", data_number(unit, "body_proficiency").ToString("0.0"));
        set_label("MagicValue", data_number(unit, "magic_proficiency").ToString("0.0"));
        set_label("ForgingValue", data_number(unit, "forging_proficiency").ToString("0.0"));
        set_label("TalismanValue", data_number(unit, "talisman_proficiency").ToString("0.0"));
        set_label("FormationValue", data_number(unit, "formation_proficiency").ToString("0.0"));
        set_label("AlchemyValue", data_number(unit, "alchemy_proficiency").ToString("0.0"));
        set_label("HealthValue", $"{unit.current_health}/{unit.max_health}"); set_label("ManaValue", $"{unit.current_mana}/{unit.max_mana}");
        set_label("SpiritPerceptionValue", $"{unit.used_spirit_perception}/{unit.max_spirit_perception}");
        set_label("MindValue", unit.initiative_speed.ToString("0.0")); set_label("ElixirValue", data_number(unit, "pill_toxicity").ToString("0.0"));
        set_label("LearnRate", unit is Character human ? human.get_learning_rate().ToString("0.00") + "/小时" : "0/小时");
        set_label("CultivateRate", unit is Character cultivator ? cultivator.get_cultivation_rate().ToString("0.000") + "/小时" : "0/小时");
        set_label("InitiativeSpeedValue", unit.initiative_speed.ToString("0.0"));
        foreach(var retired in new[]{"CardsPerTurnValue","InitialHandSizeValue"})
            if(FindChild(retired,true,false)?.GetParent() is HBoxContainer retiredRow)retiredRow.Hide();
        set_label("PhyDefValue", unit.physical_defense.ToString());
        set_label("EvasionValue", unit.precision.ToString()); set_label("DodgeValue", unit.dodge.ToString()); set_label("CritRateValue", unit.critical.ToString());
        set_label("CritResValue", unit.crit_resist.ToString()); set_label("CritDmgValue", unit.crit_damage_mult.ToString("0.00"));
    }

    private static float data_number(Creature unit, string key) => unit.entity_data is null ? 0f : unit.entity_data.Get(key).AsSingle();

    private void refresh_tooltips(Creature unit)
    {
        attribute("PhysiqueValue", "体魄是角色的肉身根基，影响行动、生命与物理攻击。", $"当前体魄 = 基础体魄 + 功法修正 + 装备修正 + 状态修正\n当前值：{unit.physique:0.0}");
        attribute("IntelligenceValue", "灵慧是思虑与精准根基，影响行动、命中与防护。", $"当前灵慧 = 基础灵慧 + 功法修正 + 装备修正 + 状态修正\n当前值：{unit.intelligence:0.0}");
        attribute("SoulValue", "神魂是感知与反应根基；与体魄较低的一方决定身心协调。", $"当前神魂 = 基础神魂 + 功法修正 + 装备修正 + 状态修正\n当前值：{unit.soul:0.0}");
        attribute("FortuneValue", "福缘代表事件、探索与收获中的潜在机缘。\n贡献：进入事件与结算的福缘判定；当前尚无固定战斗派生值。", $"当前福缘 = 基础福缘 + 功法修正 + 事件修正\n当前值：{unit.fortune:0.0}");
        attribute("ComprehensionValue", "悟性代表理解法门与领会变化的能力。\n贡献：进入功法研读、修行与相关事件的悟性判定；当前尚无固定战斗派生值。", $"当前悟性 = 基础悟性 + 功法修正 + 事件修正\n当前值：{unit.comprehension:0.0}");
        attribute("CharismaValue", "魅力影响交涉、招揽与人物事件。\n贡献：进入交涉、招揽与部分关系事件的魅力判定。", "当前魅力 = 基础魅力 + 性格初始修正 + 功法/物品/状态修正");
        attribute("SocialValue", "交际影响往来、交易与对外事务。\n贡献：进入交易、结交与外务事件的交际判定。", "当前交际 = 基础交际 + 性格初始修正 + 功法/物品/状态修正");
        attribute("ManagementValue", "治事影响辖地调度、账务与长期经营。\n贡献：进入民力调度、经营结算与相关事件的治事判定。", "当前治事 = 基础治事 + 性格初始修正 + 功法/物品/状态修正");
        attribute("HealthValue", "生命归零时角色无法继续行动。", $"生命上限 = 60 + 9 × 体魄 + 2 × 基础体魄 + 3 × 神魂 + 1.5 × 灵慧\n当前值由伤害、恢复与上限共同决定：{unit.current_health}/{unit.max_health}");
        attribute("MindValue", "速度决定行动进度积蓄；达到1000执行队首。", $"当前速度：{unit.initiative_speed:0.0}");
        attribute("EvasionValue", "精准决定攻击与技艺命中的基础能力。", $"精准 = 7 × 灵慧 + 神魂 + 0.5 × 体魄\n当前精准：{unit.precision}");
        attribute("DodgeValue", "身法决定规避的基础能力；以体魄和神魂较低者计算协调贡献。", $"身法 = 4 × 身心协调值 + 2 × 神魂 + 灵慧 + 0.5 × 体魄\n身心协调值 = min(体魄, 神魂) = {unit.coordination:0.#}\n当前身法：{unit.dodge}");
        attribute("CritRateValue", "会心影响造成关键效果的基础能力。", $"会心 = 0.5 × 体魄 + 2 × 神魂 + 0.3 × 灵慧\n当前会心：{unit.critical}");
        attribute("CritResValue", "护心抵消敌方会心与关键伤害的基础能力。", $"护心 = 3 × 体魄 + 2 × 灵慧 + 2 × 身心协调值 + 1.5 × 神魂\n当前护心：{unit.crit_resist}");
        attribute("PhyDefValue", "物理防御减轻武器、拳脚等物理伤害。", "当前物理防御 = 基础防御 + 装备 + 功法 + 状态修正");
        attribute("SpiritStrandsValue", "灵力是修行积累的根基，达到要求后可尝试突破。", "当前灵力 = 已完成的修行积累；修炼日程与环境共同决定增长速度。");
        proficiency("SwordValue", "剑道"); proficiency("BladeValue", "刀道"); proficiency("SpearValue", "枪道"); proficiency("BodyValue", "体道"); proficiency("MagicValue", "术法");
        proficiency("ForgingValue", "器道"); proficiency("TalismanValue", "符道"); proficiency("FormationValue", "阵道"); proficiency("AlchemyValue", "丹道");
    }

    private void proficiency(string valueName, string name) => attribute(valueName, $"{name}造诣影响相关技艺、配方与战斗手段的掌握。", $"当前{name}造诣包含人物基础值、已研读卷章的永久奖励，以及当前功法/装备/技艺修正。\n已研读卷章的造诣奖励写入人物基础值，不依赖装配该秘籍。");

    private void attribute(string valueName, string description, string formula)
    {
        tooltip(valueName, formula);
        var value = find_label(this, valueName);
        if (value?.GetParent() is not Node row) return;
        foreach (var child in row.GetChildren()) if (child is Label label && label != value) { bind_tooltip(label, description); break; }
    }

    private void tooltip(string labelName, string text)
    {
        var label = find_label(this, labelName);
        if (label is null) return;
        bind_tooltip(label, text);
    }

    private void bind_tooltip(Label label, string text)
    {
        label.TooltipText = ""; label.SetMeta("info_tooltip", text); label.MouseFilter = MouseFilterEnum.Pass;
        if (label.HasMeta("info_tooltip_bound"))
        {
            HoverInfoManager.SetInfo(label, new HoverInfoManager.HoverInfoContent("", text));
            return;
        }
        label.SetMeta("info_tooltip_bound", true);
        label.MouseEntered += () => HoverInfoManager.SetInfo(label, new HoverInfoManager.HoverInfoContent("", label.GetMeta("info_tooltip").AsString()));
        label.MouseExited += () => HoverInfoManager.HideFor(label);
    }

    private void build_tooltip()
    {
        _tooltipPanel = new PanelContainer { Name = "AttributeTooltip", Size = new Vector2(420, 116), MouseFilter = MouseFilterEnum.Ignore, ZIndex = 100 };
        _tooltipPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(.075f, .065f, .045f, .97f), BorderColor = new Color(.72f, .58f, .32f, .95f), BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1, CornerRadiusTopLeft = 7, CornerRadiusTopRight = 7, CornerRadiusBottomLeft = 7, CornerRadiusBottomRight = 7 });
        var margin = new MarginContainer(); margin.AddThemeConstantOverride("margin_left", 12); margin.AddThemeConstantOverride("margin_right", 12); margin.AddThemeConstantOverride("margin_top", 9); margin.AddThemeConstantOverride("margin_bottom", 9); _tooltipPanel.AddChild(margin);
        _tooltipLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = MouseFilterEnum.Ignore, VerticalAlignment = VerticalAlignment.Center };
        _tooltipLabel.AddThemeFontSizeOverride("font_size", 15); _tooltipLabel.AddThemeColorOverride("font_color", new Color(.96f, .91f, .78f)); margin.AddChild(_tooltipLabel);
        AddChild(_tooltipPanel); _tooltipPanel.Hide();
    }

    private void show_tooltip(string text)
    {
        if (_overviewMoodScale is null || string.IsNullOrWhiteSpace(text) || !Visible) return;
        HoverInfoManager.SetInfo(_overviewMoodScale, new HoverInfoManager.HoverInfoContent("心境", text));
    }

    private void hide_tooltip() { ++_readerTicket; HoverInfoManager.HideAll(); _tooltipPanel?.Hide(); _readerPanel?.Hide(); }


    private void set_label(string name, string value)
    {
        var label = find_label(this, name);
        if (label is not null) label.Text = value;
    }

    private static Label? find_label(Node node, string name)
    {
        if (node is Label label && node.Name == name) return label;
        foreach (var child in node.GetChildren()) { var found = find_label(child, name); if (found is not null) return found; }
        return null;
    }

    private static Node? find_node(Node node, string name)
    {
        if (node.Name == name) return node;
        foreach (var child in node.GetChildren()) { var found = find_node(child, name); if (found is not null) return found; }
        return null;
    }
}
