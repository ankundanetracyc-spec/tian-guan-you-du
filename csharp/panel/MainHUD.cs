using Godot;

/// <summary>大地图常驻界面：只显示主角生存状态与个人入口，不承载辖地经营信息。</summary>
public partial class MainHUD : Control
{
    public event Action? CharacterRequested, InventoryRequested, RumorsRequested, LettersRequested, CraftRequested, RealmRequested, CultivationRequested, WorldMapRequested;
    public event Action? PlayerStatusRequested;
    public event Action<string>? TaskRequested;

    private Character? _player;
    private GameTimeMgr? _timeManager;
    private Label? _realmLabel;
    private Label? _playerNameLabel;
    private Label? _healthLabel;
    private ProgressBar? _healthBar;
    private Label? _worldStaminaLabel;
    private ProgressBar? _worldStaminaBar;
    private Label? _cultivationStatusLabel;
    private Label? _worldStatusLabel;
    private HBoxContainer? _actionBar;
    private Button? _actionBarToggle;
    private bool _actionBarExpanded = true;
    private Control? _taskTracker;
    private VBoxContainer? _taskList;
    private GameNotificationFeed? _notificationFeed;

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
        _timeManager = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        if (_timeManager is not null) _timeManager.time_changed += RefreshPlayerStatus;
        ConfigureNavigation();
        BuildPlayerStatus();
        BuildTaskTracker();
        _notificationFeed = new GameNotificationFeed { Name = "EventNotificationFeed" };
        AddChild(_notificationFeed);
    }

    public override void _ExitTree()
    {
        if (_timeManager is not null) _timeManager.time_changed -= RefreshPlayerStatus;
        bind_player(null);
    }

    public void bind_player(Character? player)
    {
        if (ReferenceEquals(_player, player))
        {
            RefreshPlayerStatus();
            return;
        }

        if (GodotObject.IsInstanceValid(_player))
        {
            _player!.WorldStaminaChanged -= on_player_world_stamina_changed;
            _player.BreakthroughCompleted -= on_player_breakthrough_completed;
            _player.CultivationInspirationChanged -= on_player_cultivation_inspiration_changed;
        }

        _player = player;
        if (GodotObject.IsInstanceValid(_player))
        {
            _player!.WorldStaminaChanged += on_player_world_stamina_changed;
            _player.BreakthroughCompleted += on_player_breakthrough_completed;
            _player.CultivationInspirationChanged += on_player_cultivation_inspiration_changed;
        }
        RefreshPlayerStatus();
    }

    public void refresh_player_status() => RefreshPlayerStatus();

    public void show_event_notification(string title, string detail, string category = "见闻")
        => _notificationFeed?.push(title, detail, category);

    private void on_player_world_stamina_changed(Character _) => RefreshPlayerStatus();
    private void on_player_breakthrough_completed(Character character, string __)
    {
        RefreshPlayerStatus();
        refresh_player_world_status_details(character, GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0);
    }

    private void on_player_cultivation_inspiration_changed(Character character)
    {
        RefreshPlayerStatus();
        refresh_player_world_status_details(character, GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0);
    }

    public void set_task_entries(IEnumerable<JournalPanel.Entry> entries, bool visible)
    {
        if (_taskTracker is null || _taskList is null) return;
        var displayed = entries.Take(2).ToArray();
        var presentation = displayed.Select(entry => (entry.Id, entry.Title, entry.Body)).ToArray();
        if (_taskTrackerPresentation.SequenceEqual(presentation))
        {
            _taskTracker.Visible = visible && displayed.Length > 0;
            if (_taskTrackerToggle is not null) _taskTrackerToggle.Visible = _taskTracker.Visible;
            return;
        }
        _taskTrackerPresentation = presentation;
        foreach (var child in _taskList.GetChildren()) { _taskList.RemoveChild(child); child.QueueFree(); }
        var index = 0;
        foreach (var entry in displayed)
        {
            if (index > 0) _taskList.AddChild(WorldHudStyle.Rule());
            var button = new Button { Name = "TaskEntry" + index, Text = "", MouseFilter = MouseFilterEnum.Stop, ClipContents = true,
                TooltipText = "" };
            WorldHudStyle.StyleSmallButton(button);
            var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
            button.AddChild(margin); margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; box.AddThemeConstantOverride("separation", 5); margin.AddChild(box);
            var title = WorldHudStyle.RichText(entry.Title, 20);
            box.AddChild(title);
            var objective = WorldHudStyle.RichText(entry.Body, 16, true);
            box.AddChild(objective);
            margin.MinimumSizeChanged += () => button.CustomMinimumSize = new Vector2(0, margin.GetCombinedMinimumSize().Y);
            button.GuiInput += e => {
                if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true, DoubleClick: true })
                { TaskRequested?.Invoke(string.IsNullOrEmpty(entry.Id) ? entry.Title : entry.Id); button.AcceptEvent(); }
            };
            _taskList.AddChild(button); index++;
        }
        _taskTracker.Visible = visible && index > 0;
        CallDeferred(nameof(apply_task_tracker_horizontal_layout));
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
        var panel = new Button
        {
            Name = "PlayerStatus", Text = "", MouseFilter = MouseFilterEnum.Stop,
            ZIndex = 100, FocusMode = FocusModeEnum.All, ClipContents = true
        };
        panel.SetAnchorsPreset(LayoutPreset.BottomLeft);
        panel.OffsetLeft = 22; panel.OffsetTop = -172; panel.OffsetRight = 402; panel.OffsetBottom = -22;
        foreach (var state in new[] { "normal", "hover", "pressed" })
            panel.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        panel.AddThemeStyleboxOverride("focus", new StyleBoxFlat
        {
            BgColor = Colors.Transparent, BorderColor = WorldHudStyle.StaminaInk,
            BorderWidthBottom = 1
        });
        panel.Pressed += () => PlayerStatusRequested?.Invoke();
        AddChild(panel);
        var frame = new WorldHudFrame();
        panel.AddChild(frame);
        frame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        panel.MouseEntered += () => frame.Modulate = new Color(.96f, .98f, .95f);
        panel.MouseExited += () => frame.Modulate = Colors.White;
        var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        panel.AddChild(margin);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 16); margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_top", 10); margin.AddThemeConstantOverride("margin_bottom", 10);
        var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 5);
        margin.AddChild(box);
        var heading = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        heading.AddThemeConstantOverride("separation", 10);
        box.AddChild(heading);
        _playerNameLabel = WorldHudStyle.Text("修行者", 24);
        _playerNameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _playerNameLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        heading.AddChild(_playerNameLabel);
        _realmLabel = WorldHudStyle.Text("未定道途", 18, true);
        heading.AddChild(_realmLabel);
        heading.AddChild(WorldHudStyle.Text("›", 18, true));
        box.AddChild(WorldHudStyle.Rule());
        var health = BuildStatusRow(box, "生命", WorldHudStyle.HealthInk);
        _healthLabel = health.value; _healthBar = health.bar;
        var stamina = BuildStatusRow(box, "精力", WorldHudStyle.StaminaInk);
        _worldStaminaLabel = stamina.value; _worldStaminaBar = stamina.bar;
        _cultivationStatusLabel = WorldHudStyle.Text("", 15, true);
        _cultivationStatusLabel.CustomMinimumSize = new Vector2(0, 20);
        _cultivationStatusLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _cultivationStatusLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _cultivationStatusLabel.ClipText = true;
        box.AddChild(_cultivationStatusLabel);
        _worldStatusLabel = WorldHudStyle.Text("", 15, true);
        _worldStatusLabel.CustomMinimumSize = new Vector2(0, 20);
        _worldStatusLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _worldStatusLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _worldStatusLabel.ClipText = true;
        box.AddChild(_worldStatusLabel);
    }

    private static (Label value, ProgressBar bar) BuildStatusRow(VBoxContainer parent, string title, Color ink)
    {
        var group = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        group.AddThemeConstantOverride("separation", 2);
        parent.AddChild(group);
        var caption = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        group.AddChild(caption);
        var name = WorldHudStyle.Text(title, 18);
        name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        caption.AddChild(name);
        var value = WorldHudStyle.Text("— / —", 20);
        value.HorizontalAlignment = HorizontalAlignment.Right;
        caption.AddChild(value);
        var bar = WorldHudStyle.Bar(ink);
        group.AddChild(bar);
        return (value, bar);
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
        var frame = new WorldHudFrame { Name = "TaskPaper", Rectangular = true };
        panel.AddChild(frame);

        var margin = new MarginContainer { Name = "Margin" };
        panel.AddChild(margin);
        _taskList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _taskList.AddThemeConstantOverride("separation", 8); margin.AddChild(_taskList);
        panel.Hide();
    }

    private void RefreshPlayerStatus()
    {
        if (!GodotObject.IsInstanceValid(_player)) return;
        if (_realmLabel is not null)
        {
            var realm = _player!.cultivation_realm;
            _realmLabel.Text = realm;
            if (_playerNameLabel is not null) _playerNameLabel.Text = _player.entity_name;
            if (GetNodeOrNull<Button>("PlayerStatus") is { } status)
            {
                var tooltipGameHour = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
                var worldStatuses = WorldStatusRules.format_summary(PillToxicityRules.WithStatus(_player.entity_data as HumanData, tooltipGameHour), tooltipGameHour);
                var cultivationStatus = CharacterStatusPresentationRules.format_bottleneck_hud_line(_player.get_cultivation_bottleneck_summary());
                var cultivationTooltip = string.IsNullOrWhiteSpace(cultivationStatus) ? "" : $"\n{cultivationStatus.Trim()}";
                HoverInfoManager.SetTooltip(status, $"{_player.entity_name} · {realm}\n生命 {_player.current_health} / {_player.max_health}\n精力 {_player.current_world_stamina:0.#} / {_player.max_world_stamina:0.#}\n大世界状态：{worldStatuses}{cultivationTooltip}");
            }
        }
        if (_healthLabel is not null) _healthLabel.Text = $"{_player!.current_health} / {_player.max_health}";
        if (_healthBar is not null) { _healthBar.MaxValue = Math.Max(1, _player!.max_health); _healthBar.Value = _player.current_health; }
        if (_worldStaminaLabel is not null) _worldStaminaLabel.Text = $"{_player!.current_world_stamina:0.#} / {_player.max_world_stamina:0.#}";
        if (_worldStaminaBar is not null) { _worldStaminaBar.MaxValue = Math.Max(1, _player!.max_world_stamina); _worldStaminaBar.Value = _player.current_world_stamina; }
        var currentGameHour = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
        var statusLineCount = 0;
        if (_cultivationStatusLabel is not null)
        {
            _cultivationStatusLabel.Text = CharacterStatusPresentationRules.format_bottleneck_hud_line(_player.get_cultivation_bottleneck_summary());
            _cultivationStatusLabel.Visible = !string.IsNullOrWhiteSpace(_cultivationStatusLabel.Text);
            if (_cultivationStatusLabel.Visible) statusLineCount++;
        }
        if (_worldStatusLabel is not null)
        {
            _worldStatusLabel.Text = WorldStatusRules.format_hud_line(PillToxicityRules.WithStatus(_player!.entity_data as HumanData, currentGameHour), currentGameHour);
            _worldStatusLabel.Visible = !string.IsNullOrWhiteSpace(_worldStatusLabel.Text);
            if (_worldStatusLabel.Visible) statusLineCount++;
        }
        if (GetNodeOrNull<Control>("PlayerStatus") is { } statusPanel)
            statusPanel.OffsetTop = -172 - 32 * statusLineCount;
        if (FindButton("BtnRealm") is { } realmButton)
        {
            var realms = GetNodeOrNull<RealmManager>("/root/RealmManager");
            var unlocked = realms?.is_realm_after_or_equal(_player!.cultivation_realm, "云息一层") == true;
            if (GetNodeOrNull<Control>("Hbox/V_Realm") is { } realmEntry) realmEntry.Visible = unlocked;
            realmButton.Disabled = !unlocked;
        }
        if (GetNodeOrNull<Control>("Hbox/V_Cultivation") is { } cultivationEntry)
        {
            cultivationEntry.Visible = true;
        }
    }

    private void ConnectButton(string path, Action action) { if (FindButton(path) is { } button) button.Pressed += action; }
    private Button? FindButton(string name) => GetNodeOrNull<Button>($"Hbox/{name}") ?? GetNodeOrNull<Button>(name) ?? FindChild(name, true, false) as Button;
    private void toggle_action_bar()
    {
        _actionBarExpanded = !_actionBarExpanded;
        if (_actionBar is not null) _actionBar.Visible = _actionBarExpanded;
        RefreshNavigationToggle();
    }
}
