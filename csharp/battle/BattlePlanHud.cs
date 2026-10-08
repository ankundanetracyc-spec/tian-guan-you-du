using Godot;
using Godot.Collections;

/// <summary>分段规划战斗的顶部时序、规划确认与玩家专属队列显示。</summary>
public partial class BattlePlanHud : Control
{
    private BattleManager? _manager;
    private PanelContainer? _timelinePanel;
    private HBoxContainer? _timeline;
    private PanelContainer? _planningPanel;
    private HBoxContainer? _planningSlots;
    private PanelContainer? _actionQueuePanel;
    private HBoxContainer? _actionQueueSlots;
    private Button? _queueToggle;
    private Label? _planningStatus;
    private string _timelineText = "";
    private string _queueSignature = "";
    private bool _queueVisible = true;

    public void Setup(BattleManager manager)
    {
        _manager = manager;
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ZIndex = 50;
        BuildTimeline();
        BuildPlanningPanel();
        BuildActionQueuePanel();
        RefreshAll();
    }

    public override void _Process(double delta)
    {
        if (!GodotObject.IsInstanceValid(_manager)) return;
        var names = _manager!.get_timeline_names();
        var nextText = string.Join("   ›   ", names);
        if (nextText != _timelineText)
        {
            _timelineText = nextText;
            RebuildTimeline(names);
        }
        if (_manager.is_planning_phase() && _planningStatus is not null)
        {
            var player = _manager.get_main_player_unit();
            var queueCount = GodotObject.IsInstanceValid(player) ? _manager.get_battle_unit(player!).skill_queue.Count : 0;
            _planningStatus.Text = $"规划本段行动  {queueCount}/{_manager.get_action_slot_capacity()}  ·  {Mathf.CeilToInt(_manager.get_planning_seconds_remaining())} 秒后开始";
        }
        RefreshPlayerQueueIfChanged();
    }

    public void RefreshAll()
    {
        RefreshPlanningPanel();
        RefreshPlayerQueue(true);
        UpdateStageVisibility();
        if (_manager is not null) RebuildTimeline(_manager.get_timeline_names());
    }

    public void RefreshPlayerQueue() => RefreshPlayerQueue(true);

    private void BuildTimeline()
    {
        _timelinePanel = new PanelContainer
        {
            Name = "ActionTimeline",
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 1
        };
        _timelinePanel.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        _timelinePanel.AnchorLeft = .20f;
        _timelinePanel.AnchorRight = .80f;
        _timelinePanel.OffsetTop = 98;
        _timelinePanel.OffsetBottom = 154;
        _timelinePanel.AddThemeStyleboxOverride("panel", PanelStyle(new Color(.025f, .045f, .043f, .86f), new Color(.60f, .52f, .32f, .62f)));
        _timeline = new HBoxContainer { Name = "Names", Alignment = BoxContainer.AlignmentMode.Center, SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        _timeline.AddThemeConstantOverride("separation", 9);
        _timelinePanel.AddChild(_timeline);
        AddChild(_timelinePanel);
    }

    private void BuildPlanningPanel()
    {
        _planningPanel = new PanelContainer
        {
            Name = "PlanningQueue",
            MouseFilter = MouseFilterEnum.Pass,
            ZIndex = 2
        };
        _planningPanel.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        _planningPanel.AnchorLeft = .06f;
        _planningPanel.AnchorRight = .94f;
        _planningPanel.AnchorTop = .36f;
        _planningPanel.AnchorBottom = .64f;
        _planningPanel.OffsetLeft = 0;
        _planningPanel.OffsetRight = 0;
        _planningPanel.OffsetTop = -8;
        _planningPanel.OffsetBottom = 8;
        _planningPanel.AddThemeStyleboxOverride("panel", PanelStyle(new Color(.025f, .045f, .043f, .91f), new Color(.69f, .59f, .35f, .86f)));

        var column = new VBoxContainer { Name = "Contents", SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Pass };
        column.AddThemeConstantOverride("separation", 8);
        _planningStatus = new Label { Text = "规划本段行动", HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        _planningStatus.AddThemeFontSizeOverride("font_size", 20);
        _planningStatus.AddThemeColorOverride("font_color", new Color(.93f, .85f, .65f));
        column.AddChild(_planningStatus);

        _planningSlots = new HBoxContainer { Name = "Slots", Alignment = BoxContainer.AlignmentMode.Center, SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        _planningSlots.AddThemeConstantOverride("separation", 5);
        column.AddChild(_planningSlots);

        var controls = new HBoxContainer { Name = "Controls", Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Stop };
        controls.AddThemeConstantOverride("separation", 10);
        var rest = MakeButton("休整  +2 精力", 130);
        rest.Pressed += () => _manager?.queue_system_action(0);
        controls.AddChild(rest);
        var breathe = MakeButton("吐纳  +1 法力", 130);
        breathe.Pressed += () => _manager?.queue_system_action(1);
        controls.AddChild(breathe);
        var confirm = MakeButton("确认队列", 130);
        confirm.Pressed += () => _manager?.confirm_player_queue();
        controls.AddChild(confirm);
        column.AddChild(controls);
        _planningPanel.AddChild(column);
        AddChild(_planningPanel);
    }

    private void BuildActionQueuePanel()
    {
        _actionQueuePanel = new PanelContainer
        {
            Name = "PlayerActionQueue",
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 2
        };
        _actionQueuePanel.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
        _actionQueuePanel.AnchorLeft = .15f;
        _actionQueuePanel.AnchorRight = .85f;
        _actionQueuePanel.OffsetTop = -238;
        _actionQueuePanel.OffsetBottom = -12;
        _actionQueuePanel.AddThemeStyleboxOverride("panel", PanelStyle(new Color(.025f, .045f, .043f, .92f), new Color(.60f, .52f, .32f, .70f)));

        var column = new VBoxContainer { Name = "Contents", SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 4);
        var heading = new Label { Text = "主角行动队列", HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        heading.AddThemeFontSizeOverride("font_size", 16);
        heading.AddThemeColorOverride("font_color", new Color(.86f, .86f, .78f));
        column.AddChild(heading);
        _actionQueueSlots = new HBoxContainer { Name = "Slots", Alignment = BoxContainer.AlignmentMode.Center, SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        _actionQueueSlots.AddThemeConstantOverride("separation", 4);
        column.AddChild(_actionQueueSlots);
        _actionQueuePanel.AddChild(column);
        AddChild(_actionQueuePanel);

        _queueToggle = MakeButton("隐藏队列", 108);
        _queueToggle.Name = "QueueVisibilityToggle";
        _queueToggle.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight);
        _queueToggle.AnchorLeft = 1;
        _queueToggle.AnchorRight = 1;
        _queueToggle.AnchorTop = 1;
        _queueToggle.AnchorBottom = 1;
        _queueToggle.OffsetLeft = -135;
        _queueToggle.OffsetRight = -20;
        _queueToggle.OffsetTop = -284;
        _queueToggle.OffsetBottom = -250;
        _queueToggle.Pressed += ToggleQueueVisibility;
        AddChild(_queueToggle);
    }

    private void RefreshPlanningPanel()
    {
        if (_manager is null || _planningSlots is null) return;
        foreach (var child in _planningSlots.GetChildren()) child.QueueFree();
        var player = _manager.get_main_player_unit();
        var queue = GodotObject.IsInstanceValid(player) ? _manager.get_battle_unit(player!).skill_queue : [];
        var capacity = _manager.get_action_slot_capacity();
        for (var i = 0; i < capacity; i++)
        {
            var index = i;
            var action = i < queue.Count ? queue[i] : null;
            _planningSlots.AddChild(CreatePlanSlot(action, index, queue.Count));
        }
        if (_planningStatus is not null)
        {
            var locked = !_manager.can_player_plan();
            _planningStatus.Text = locked ? "队列已确认，等待行动开始" : $"规划本段行动  {queue.Count}/{capacity}";
        }
        UpdateStageVisibility();
    }

    private Control CreatePlanSlot(BattleQueuedSkill? action, int index, int queueCount)
    {
        var slot = new PanelContainer
        {
            CustomMinimumSize = new Vector2(138, 94),
            MouseFilter = MouseFilterEnum.Pass
        };
        slot.AddThemeStyleboxOverride("panel", PanelStyle(
            action is null ? new Color(.03f, .05f, .05f, .52f) : new Color(.08f, .11f, .09f, .92f),
            action is null ? new Color(.43f, .47f, .40f, .42f) : new Color(.69f, .59f, .35f, .82f)));
        var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Pass, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var title = new Label
        {
            Text = action?.get_display_name() ?? "空位",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            ClipText = true,
            CustomMinimumSize = new Vector2(124, 40),
            MouseFilter = MouseFilterEnum.Ignore
        };
        title.AddThemeFontSizeOverride("font_size", 15);
        title.AddThemeColorOverride("font_color", action is null ? new Color(.53f, .56f, .50f) : new Color(.94f, .91f, .81f));
        column.AddChild(title);
        if (action is not null)
        {
            var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Stop };
            var up = MakeButton("↑", 31); up.Disabled = index == 0;
            up.Pressed += () => _manager?.reorder_player_action(index, index - 1);
            buttons.AddChild(up);
            var down = MakeButton("↓", 31); down.Disabled = index >= queueCount - 1;
            down.Pressed += () => _manager?.reorder_player_action(index, index + 1);
            buttons.AddChild(down);
            var remove = MakeButton("×", 31);
            remove.Pressed += () => _manager?.cancel_player_action(index);
            buttons.AddChild(remove);
            column.AddChild(buttons);
        }
        slot.AddChild(column);
        return slot;
    }

    private void RefreshPlayerQueueIfChanged()
    {
        if (_manager is null) return;
        var player = _manager.get_main_player_unit();
        var queue = GodotObject.IsInstanceValid(player) ? _manager.get_battle_unit(player!).skill_queue : [];
        var signature = $"{_manager.get_segment_phase()}:{string.Join("|", queue.Select(action => action.get_display_name()))}";
        if (signature == _queueSignature) return;
        _queueSignature = signature;
        RefreshPlayerQueue(true);
    }

    private void RefreshPlayerQueue(bool resetSignature)
    {
        if (_manager is null || _actionQueueSlots is null) return;
        if (resetSignature) _queueSignature = "";
        foreach (var child in _actionQueueSlots.GetChildren()) child.QueueFree();
        var player = _manager.get_main_player_unit();
        var queue = GodotObject.IsInstanceValid(player) ? _manager.get_battle_unit(player!).skill_queue : [];
        foreach (var action in queue)
        {
            var slot = new PanelContainer { CustomMinimumSize = new Vector2(112, 144), MouseFilter = MouseFilterEnum.Ignore };
            slot.AddThemeStyleboxOverride("panel", PanelStyle(new Color(.07f, .09f, .08f, .84f), new Color(.57f, .55f, .43f, .64f)));
            var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
            if (action.kind == BattleQueuedSkill.ActionKind.Skill && !string.IsNullOrWhiteSpace(action.skill.icon_path))
            {
                var texture = ResourceLoader.Load<Texture2D>(action.skill.icon_path);
                if (texture is not null)
                {
                    var icon = new TextureRect { Texture = texture, CustomMinimumSize = new Vector2(76, 92), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore };
                    column.AddChild(icon);
                }
            }
            var name = new Label { Text = action.get_display_name(), HorizontalAlignment = HorizontalAlignment.Center, ClipText = true, CustomMinimumSize = new Vector2(100, 25), MouseFilter = MouseFilterEnum.Ignore };
            name.AddThemeFontSizeOverride("font_size", 14);
            name.AddThemeColorOverride("font_color", new Color(.92f, .90f, .82f));
            column.AddChild(name);
            slot.AddChild(column);
            _actionQueueSlots.AddChild(slot);
        }
        UpdateStageVisibility();
    }

    private void RebuildTimeline(Array<string> names)
    {
        if (_timeline is null) return;
        foreach (var child in _timeline.GetChildren()) child.QueueFree();
        if (names.Count == 0)
        {
            var empty = new Label { Text = _manager?.is_planning_phase() == true ? "规划中" : "行动段结束", HorizontalAlignment = HorizontalAlignment.Center, SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
            empty.AddThemeFontSizeOverride("font_size", 17);
            empty.AddThemeColorOverride("font_color", new Color(.75f, .78f, .70f));
            _timeline.AddChild(empty);
            return;
        }
        for (var i = 0; i < names.Count; i++)
        {
            var label = new Label { Text = names[i], HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
            label.AddThemeFontSizeOverride("font_size", i == 0 ? 22 : 16);
            label.AddThemeColorOverride("font_color", i == 0 ? new Color(.98f, .86f, .55f) : new Color(.78f, .81f, .74f));
            _timeline.AddChild(label);
            if (i < names.Count - 1)
            {
                var divider = new Label { Text = "›", VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
                divider.AddThemeFontSizeOverride("font_size", 16);
                divider.AddThemeColorOverride("font_color", new Color(.52f, .58f, .51f));
                _timeline.AddChild(divider);
            }
        }
    }

    private void UpdateStageVisibility()
    {
        if (_manager is null) return;
        var planning = _manager.get_segment_phase() == BattleManager.SegmentPhase.Planning;
        if (_planningPanel is not null) _planningPanel.Visible = planning;
        if (_actionQueuePanel is not null) _actionQueuePanel.Visible = !planning && _queueVisible;
        if (_queueToggle is not null)
        {
            _queueToggle.Visible = !planning;
            _queueToggle.Text = _queueVisible ? "隐藏队列" : "显示队列";
        }
    }

    private void ToggleQueueVisibility()
    {
        _queueVisible = !_queueVisible;
        UpdateStageVisibility();
    }

    private static Button MakeButton(string text, float width)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(width, 31), MouseFilter = MouseFilterEnum.Stop, FocusMode = FocusModeEnum.None };
        button.AddThemeFontSizeOverride("font_size", 14);
        button.AddThemeColorOverride("font_color", new Color(.93f, .91f, .82f));
        button.AddThemeStyleboxOverride("normal", PanelStyle(new Color(.10f, .13f, .11f, .96f), new Color(.56f, .54f, .42f, .86f)));
        button.AddThemeStyleboxOverride("hover", PanelStyle(new Color(.17f, .19f, .15f, .98f), new Color(.79f, .67f, .40f, .96f)));
        button.AddThemeStyleboxOverride("pressed", PanelStyle(new Color(.08f, .11f, .09f, .98f), new Color(.81f, .71f, .47f, .98f)));
        return button;
    }

    private static StyleBoxFlat PanelStyle(Color background, Color border) => new()
    {
        BgColor = background,
        BorderColor = border,
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        CornerRadiusTopLeft = 7,
        CornerRadiusTopRight = 7,
        CornerRadiusBottomRight = 7,
        CornerRadiusBottomLeft = 7,
        ContentMarginLeft = 8,
        ContentMarginTop = 6,
        ContentMarginRight = 8,
        ContentMarginBottom = 6
    };
}
