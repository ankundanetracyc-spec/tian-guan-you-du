using Godot;

/// <summary>独立战备画布，复用现有战斗按钮及统一悬停说明。</summary>
public partial class BattleSupplyHud : CanvasLayer
{
    private BattleManager _manager = null!;
    private Control _root = null!;
    private Button _entry = null!;
    private Label _arrow = null!;
    private readonly Button[] _slots = new Button[3];
    private readonly TextureRect[] _icons = new TextureRect[3];
    private readonly Label[] _names = new Label[3], _qualities = new Label[3], _counts = new Label[3];
    private string _signature = "";
    public bool Expanded { get; private set; }

    public void Setup(BattleManager manager)
    {
        _manager = manager;
        Name = "BattleSuppliesLayer"; Layer = 20; ProcessMode = ProcessModeEnum.Always;
        _root = new Control { Name = "BattleSupplies", MouseFilter = Control.MouseFilterEnum.Ignore };
        AddChild(_root);
        _entry = MakeButton("BattleSuppliesToggle", new(0, 800), new(136, 64));
        Caption(_entry, "战备", Vector2.Zero, new(136, 64), 22);
        _arrow = Caption(_entry, "▾", new(112, 0), new(16, 64), 13);
        _arrow.AddThemeColorOverride("font_color", BattleUiArt.Accent);
        _entry.Pressed += () => SetExpanded(!Expanded);
        for (var i = 0; i < 3; i++)
        {
            var index = i;
            var tile = MakeButton($"BattleSupplySlot{i + 1}", new(0, 494 + i * 102), new(136, 94));
            _slots[i] = tile;
            _icons[i] = new TextureRect { Position = new(46, 4), Size = new(44, 40),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = Control.MouseFilterEnum.Ignore };
            tile.AddChild(_icons[i]);
            _names[i] = Caption(tile, "未编入", new(0, 44), new(136, 22), 18);
            _qualities[i] = Caption(tile, "", new(0, 67), new(136, 20), 14);
            _qualities[i].AddThemeColorOverride("font_color", BattleUiArt.Accent);
            _counts[i] = Caption(tile, "", new(94, 8), new(30, 22), 17);
            _counts[i].HorizontalAlignment = HorizontalAlignment.Right;
            tile.Pressed += () => { if (_manager.queue_pill_action(index)) { _signature = ""; Refresh(); } };
        }
        _root.Hide();
    }

    private Button MakeButton(string name, Vector2 position, Vector2 size)
    {
        var button = new Button { Name = name, Position = position, Size = size, CustomMinimumSize = size };
        BattleUiArt.Skin(button); _root.AddChild(button); return button;
    }

    private static Label Caption(Node parent, string text, Vector2 position, Vector2 size, int font)
    {
        var label = new Label { Text = text, Position = position, Size = size,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", font);
        label.AddThemeColorOverride("font_color", BattleUiArt.Text);
        parent.AddChild(label); return label;
    }

    public override void _Process(double delta)
    {
        if (_root is null || !GodotObject.IsInstanceValid(_manager)) return;
        BattleUiArt.Fit(_root);
        var detailsOpen = GetTree().GetNodesInGroup("human_info_panel").OfType<CanvasItem>().Any(item => item.IsVisibleInTree());
        var visible = !_manager.is_board_battle() && _manager.SupplyHudVisible && _manager.get_main_player_unit() is Character && !detailsOpen;
        _root.Visible = visible;
        if (!visible) SetExpanded(false);
        Refresh();
    }

    private void Refresh()
    {
        if (_manager.get_main_player_unit() is not Character owner) return;
        var signature = $"{Expanded}:{owner.current_health}:{owner.max_health}:{owner.current_mana}:{_manager.get_effective_mana_capacity(owner)}:{owner.PillToxicity:0.######}:" +
            _manager.get_filled_action_slot_count(owner) + ":" + string.Join("|", owner.battle_supplies.Select((slot, i) =>
                $"{slot.item_path}:{slot.count}:{_manager.get_planned_pill_count(owner, i)}:{_manager.get_available_supply_count(owner, i)}"));
        if (_signature == signature) return;
        _signature = signature;
        for (var i = 0; i < 3; i++)
        {
            _slots[i].Visible = Expanded;
            var slot = i < owner.battle_supplies.Count ? owner.battle_supplies[i] : null;
            var pill = BattleSupplyRules.Load(slot?.item_path ?? "");
            _slots[i].Disabled = pill is null || _manager.get_available_supply_count(owner, i) <= 0 ||
                _manager.get_filled_action_slot_count(owner) >= _manager.get_action_slot_capacity(owner);
            _icons[i].Texture = pill is null ? null : GD.Load<Texture2D>(pill.icon_path);
            _icons[i].Material = null;
            if (pill is not null) AlchemyPillVisual.Apply(_icons[i], pill.efficacy_tier, pill.pill_id);
            _names[i].Text = pill is null ? "未编入" : BattleSupplyRules.Name(pill);
            _counts[i].Text = pill is null ? "" : $"×{slot!.count}";
            var planned = _manager.get_planned_pill_count(owner, i);
            _qualities[i].Text = pill is null ? "" : BattleSupplyRules.QualityNames[pill.efficacy_tier] + (planned > 0 ? $" · 已排{planned}" : "");
            HoverInfoManager.SetInfo(_slots[i], _manager.SupplyHover(owner, i));
        }
    }

    private void SetExpanded(bool value)
    {
        if (Expanded == value) return;
        Expanded = value; _arrow.Text = value ? "▴" : "▾"; _signature = "";
        foreach (var slot in _slots) { slot.Visible = value; if (!value) HoverInfoManager.HideFor(slot); }
    }

    public bool Dismiss()
    {
        if (!Expanded) return false;
        if (!HoverInfoManager.DismissVisible()) SetExpanded(false);
        return true;
    }

    public override void _Input(InputEvent @event)
    {
        if (!Expanded || @event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mouse) return;
        var point = _root.GetGlobalTransformWithCanvas().AffineInverse() * mouse.Position;
        if (!_entry.GetRect().HasPoint(point) && !_slots.Any(slot => slot.GetRect().HasPoint(point))) SetExpanded(false);
    }

    public override void _ExitTree()
    {
        foreach (var slot in _slots) if (GodotObject.IsInstanceValid(slot)) HoverInfoManager.HideFor(slot);
    }
}
