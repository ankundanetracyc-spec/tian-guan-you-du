using Godot;

/// <summary>大地图常驻界面：只显示主角生存状态与个人入口，不承载辖地经营信息。</summary>
public partial class MainHUD : Control
{
    public event Action? CharacterRequested, InventoryRequested, RumorsRequested, LettersRequested, CraftRequested, RealmRequested, CultivationRequested, WorldMapRequested;
    public event Action? PlayerStatusRequested;
    public event Action<string>? TaskRequested;

    private Character? _player;
    private Label? _realmLabel;
    private Label? _healthLabel;
    private ProgressBar? _healthBar;
    private Label? _worldStaminaLabel;
    private ProgressBar? _worldStaminaBar;
    private HBoxContainer? _actionBar;
    private Button? _actionBarToggle;
    private bool _actionBarExpanded = true;
    private Control? _taskTracker;
    private VBoxContainer? _taskList;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        ConnectButton("BtnCharacter", () => CharacterRequested?.Invoke());
        ConnectButton("BtnInventory", () => InventoryRequested?.Invoke());
        ConnectButton("BtnRumors", () => RumorsRequested?.Invoke());
        ConnectButton("BtnLetters", () => LettersRequested?.Invoke());
        ConnectButton("BtnCraft", () => CraftRequested?.Invoke());
        ConnectButton("BtnWorld", () => WorldMapRequested?.Invoke());
        ConnectButton("BtnRealm", () => RealmRequested?.Invoke());
        ConnectButton("BtnCultivation", () => CultivationRequested?.Invoke());
        _actionBar = GetNodeOrNull<HBoxContainer>("Hbox");
        _actionBarToggle = GetNodeOrNull<Button>("ActionBarToggle");
        if (_actionBarToggle is not null) _actionBarToggle.Pressed += toggle_action_bar;
        BuildPlayerStatus();
        BuildTaskTracker();
    }

    public override void _Process(double delta) => RefreshPlayerStatus();

    public void bind_player(Character? player) { _player = player; RefreshPlayerStatus(); }

    public void set_task_entries(IEnumerable<JournalPanel.Entry> entries, bool visible)
    {
        if (_taskTracker is null || _taskList is null) return;
        foreach (var child in _taskList.GetChildren()) child.QueueFree();
        foreach (var entry in entries.Take(2))
        {
            var summary = entry.Body.Length > 96 ? entry.Body[..93] + "…" : entry.Body;
            var button = new Button
            {
                Text = $"{entry.Title}　·　{entry.State}\n{summary}",
                Alignment = HorizontalAlignment.Left,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(0, 78),
                TooltipText = "点击查看任务详情",
                MouseFilter = MouseFilterEnum.Stop
            };
            button.AddThemeFontSizeOverride("font_size", 16);
            button.AddThemeColorOverride("font_color", new Color("30291f"));
            button.AddThemeStyleboxOverride("normal", new StyleBoxFlat
            {
                BgColor = new Color(.91f, .86f, .72f, .91f), BorderColor = new Color(.45f, .34f, .18f, .72f),
                BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
                CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
                ContentMarginLeft = 11, ContentMarginRight = 11, ContentMarginTop = 6, ContentMarginBottom = 6
            });
            button.AddThemeStyleboxOverride("hover", new StyleBoxFlat
            {
                BgColor = new Color(.98f, .92f, .77f, .98f), BorderColor = new Color(.65f, .49f, .23f, .95f),
                BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
                CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
                ContentMarginLeft = 11, ContentMarginRight = 11, ContentMarginTop = 6, ContentMarginBottom = 6
            });
            button.Pressed += () => TaskRequested?.Invoke(entry.Title);
            _taskList.AddChild(button);
        }
        _taskTracker.Visible = visible;
        CallDeferred(nameof(refresh_task_tracker_layout));
    }

    public void set_journal_unread_count(int count)
    {
        if (GetNodeOrNull<Label>("Hbox/V_Rumors/Label") is not { } label) return;
        label.Text = count > 0 ? $"见闻 · 新{count}" : "见闻";
    }

    // 迁移期间保留旧调用点；资源、民力等不再显示在常驻 HUD。
    public void bind_estate_resources(EstateResourceSystem? _) { }
    public void bind_territory_state(QingxiTerritoryState? _) { }
    public void set_estate_summary_visible(bool visible)
    {
        if (GetNodeOrNull<Control>("PlayerStatus") is { } panel) panel.Visible = visible;
    }

    public void set_world_map_available(bool available)
    {
        if (GetNodeOrNull<Control>("Hbox/V_World") is { } worldEntry) worldEntry.Visible = available;
    }

    public Rect2 get_ui_rect() => GetGlobalRect();

    public void set_guided_button(string? buttonName)
    {
        foreach (var name in new[] { "BtnCharacter", "BtnInventory", "BtnRumors", "BtnLetters", "BtnCraft", "BtnCultivation", "BtnRealm" })
            if (FindButton(name) is { } button) button.Modulate = name == buttonName ? new Color("fff1a8") : Colors.White;
    }

    private void BuildPlayerStatus()
    {
        var panel = new Button { Name = "PlayerStatus", Text = "", MouseFilter = MouseFilterEnum.Stop, ZIndex = 100, FocusMode = FocusModeEnum.None };
        panel.SetAnchorsPreset(LayoutPreset.BottomLeft);
        panel.OffsetLeft = 22; panel.OffsetTop = -198; panel.OffsetRight = 352; panel.OffsetBottom = -22;
        panel.AddThemeStyleboxOverride("normal", new StyleBoxFlat
        {
            BgColor = new Color(.035f, .045f, .04f, .93f), BorderColor = new Color("a78d5a"),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 9, CornerRadiusTopRight = 9, CornerRadiusBottomLeft = 9, CornerRadiusBottomRight = 9,
            ContentMarginLeft = 2, ContentMarginRight = 2, ContentMarginTop = 2, ContentMarginBottom = 2
        });
        panel.AddThemeStyleboxOverride("hover", new StyleBoxFlat
        {
            BgColor = new Color(.07f, .075f, .06f, .97f), BorderColor = new Color("d1b475"),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 9, CornerRadiusTopRight = 9, CornerRadiusBottomLeft = 9, CornerRadiusBottomRight = 9,
            ContentMarginLeft = 2, ContentMarginRight = 2, ContentMarginTop = 2, ContentMarginBottom = 2
        });
        panel.AddThemeStyleboxOverride("pressed", panel.GetThemeStylebox("hover"));
        panel.Pressed += () => PlayerStatusRequested?.Invoke();
        AddChild(panel);
        var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        margin.AddThemeConstantOverride("margin_left", 16); margin.AddThemeConstantOverride("margin_right", 16); margin.AddThemeConstantOverride("margin_top", 13); margin.AddThemeConstantOverride("margin_bottom", 13); panel.AddChild(margin);
        var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 5); margin.AddChild(box);
        _realmLabel = new Label { Text = "未定道途", MouseFilter = MouseFilterEnum.Ignore };
        _realmLabel.AddThemeFontSizeOverride("font_size", 20); _realmLabel.AddThemeColorOverride("font_color", new Color("e6cf96")); box.AddChild(_realmLabel);
        _healthLabel = new Label { Text = "生命 0 / 0", MouseFilter = MouseFilterEnum.Ignore };
        _healthLabel.AddThemeFontSizeOverride("font_size", 15); _healthLabel.AddThemeColorOverride("font_color", new Color("f2ead9")); box.AddChild(_healthLabel);
        _healthBar = new ProgressBar { ShowPercentage = false, CustomMinimumSize = new Vector2(0, 15), MaxValue = 1, Value = 0, MouseFilter = MouseFilterEnum.Ignore };
        _healthBar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color(.12f, .08f, .07f), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
        _healthBar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = new Color("a64e40"), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
        box.AddChild(_healthBar);
        _worldStaminaLabel = new Label { Text = "体力 100 / 100", MouseFilter = MouseFilterEnum.Ignore };
        _worldStaminaLabel.AddThemeFontSizeOverride("font_size", 15); _worldStaminaLabel.AddThemeColorOverride("font_color", new Color("e2e4d2")); box.AddChild(_worldStaminaLabel);
        _worldStaminaBar = new ProgressBar { ShowPercentage = false, CustomMinimumSize = new Vector2(0, 13), MaxValue = 100, Value = 100, MouseFilter = MouseFilterEnum.Ignore };
        _worldStaminaBar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color(.09f, .10f, .08f), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
        _worldStaminaBar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = new Color("718b68"), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
        box.AddChild(_worldStaminaBar);
    }

    private void BuildTaskTracker()
    {
        var panel = new PanelContainer { Name = "TaskTracker", MouseFilter = MouseFilterEnum.Pass, ZIndex = 10 };
        _taskTracker = panel;
        panel.SetAnchorsPreset(LayoutPreset.TopRight);
        panel.AnchorTop = .25f; panel.AnchorBottom = .25f;
        panel.OffsetLeft = -476; panel.OffsetTop = 0; panel.OffsetRight = -40; panel.OffsetBottom = 232;
        panel.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 21); margin.AddThemeConstantOverride("margin_right", 19);
        margin.AddThemeConstantOverride("margin_top", 22); margin.AddThemeConstantOverride("margin_bottom", 20);
        panel.AddChild(margin);
        var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 9); margin.AddChild(box);
        var heading = new Label { Text = "阶段目标 · 当前任务", HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        heading.AddThemeFontSizeOverride("font_size", 19); heading.AddThemeColorOverride("font_color", new Color("352d22"));
        box.AddChild(heading);
        _taskList = new VBoxContainer(); _taskList.AddThemeConstantOverride("separation", 8); box.AddChild(_taskList);
        panel.Hide();
    }

    private void refresh_task_tracker_layout()
    {
        if (_taskTracker is null || _taskList is null) return;
        var height = Mathf.Clamp(_taskList.GetCombinedMinimumSize().Y + 80, 190, 246);
        _taskTracker.OffsetBottom = height;
    }

    private void RefreshPlayerStatus()
    {
        if (!GodotObject.IsInstanceValid(_player)) return;
        if (_realmLabel is not null)
        {
            var realm = _player!.cultivation_realm;
            _realmLabel.Text = $"{_player.entity_name} · {realm}";
        }
        if (_healthLabel is not null) _healthLabel.Text = $"生命 {_player!.current_health} / {_player.max_health}";
        if (_healthBar is not null) { _healthBar.MaxValue = Math.Max(1, _player!.max_health); _healthBar.Value = _player.current_health; }
        if (_worldStaminaLabel is not null) _worldStaminaLabel.Text = $"体力 {_player!.current_world_stamina:0.#} / {_player.max_world_stamina:0.#}";
        if (_worldStaminaBar is not null) { _worldStaminaBar.MaxValue = Math.Max(1, _player!.max_world_stamina); _worldStaminaBar.Value = _player.current_world_stamina; }
        if (FindButton("BtnRealm") is { } realmButton)
        {
            var realms = GetNodeOrNull<RealmManager>("/root/RealmManager");
            var unlocked = realms?.is_realm_after_or_equal(_player!.cultivation_realm, "云息一层") == true;
            if (GetNodeOrNull<Control>("Hbox/V_Realm") is { } realmEntry) realmEntry.Visible = unlocked;
            realmButton.Disabled = !unlocked;
            realmButton.TooltipText = unlocked ? "查看境界" : "";
        }
        if (GetNodeOrNull<Control>("Hbox/V_Cultivation") is { } cultivationEntry)
        {
            cultivationEntry.Visible = true;
            if (FindButton("BtnCultivation") is { } cultivationButton)
                cultivationButton.TooltipText = cultivationEntry.Visible ? "查看修命与修性进度" : "";
        }
    }

    private void ConnectButton(string path, Action action) { if (FindButton(path) is { } button) button.Pressed += action; }
    private Button? FindButton(string name) => GetNodeOrNull<Button>($"Hbox/{name}") ?? GetNodeOrNull<Button>(name) ?? FindChild(name, true, false) as Button;
    private void toggle_action_bar()
    {
        _actionBarExpanded = !_actionBarExpanded;
        if (_actionBar is not null) _actionBar.Visible = _actionBarExpanded;
        if (_actionBarToggle is not null) _actionBarToggle.Text = _actionBarExpanded ? "收起 ▲" : "展开 ▼";
    }
}
