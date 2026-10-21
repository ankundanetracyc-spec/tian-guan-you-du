using System;
using System.Collections.Generic;
using System.Linq;
using System;
using Godot;
using GDict = Godot.Collections.Dictionary;

/// <summary>
/// 世界人物交易面板。交易采用全屏行囊式布局：两侧只展示可交易物品，
/// 中间只显示对方态度与交易条件判断，具体估值保留在规则层，不直接暴露给玩家。
/// </summary>
public partial class TradePanel : Control
{
    public event Action? Closed;
    public event Action? TradeCompleted;

    private sealed class TradeEntry
    {
        public string path = "";
        public int count;
        public int selected_count;
        public bool selected;
        public StorageItemSlot? slot;
        public Label? checkmark;
        public Panel? selection_frame;
        public HSlider? quantity_slider;
        public Label? quantity_label;
    }

    private const int VisibleSlotCount = 30;
    private const int MinimumDisplaySlotCount = 15;
    private const float TradeSlotWidth = 110f;
    private const float TradeSlotIconSize = 92f;
    private const float TradeSlotBaseHeight = 148f;
    private const string SlotScenePath = "res://tscn/panel/StorageItemSlot.tscn";
    private static readonly string[] DefaultNpcTradeItems =
    [
        "res://resources/entities/items/灵石.tres",
        "res://resources/entities/items/兽皮.tres",
        "res://resources/entities/items/兽骨.tres",
        "res://resources/entities/items/黄庭养形篇秘籍.tres",
    ];

    private Character? _player;
    private WorldNpcData? _npc;
    private PackedScene? _slot_scene;
    private FontFile? _font;
    private GridContainer? _player_grid;
    private GridContainer? _npc_grid;
    private Label? _npc_name_label;
    private Label? _attitude_label;
    private Label? _trade_terms_label;
    private PanelContainer? _floating_hint_panel;
    private Label? _floating_hint;
    private Button? _trade_button;
    private readonly List<TradeEntry> _player_entries = [];
    private readonly List<TradeEntry> _npc_entries = [];
    private readonly Dictionary<string, ItemData?> _item_cache = [];

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        ZIndex = 500;
        Theme = QuietRoomStyle.MakeTheme();
        _slot_scene = ResourceLoader.Load<PackedScene>(SlotScenePath);
        _font = ResourceLoader.Load<FontFile>("res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf");
        build_ui();
        Hide();
    }

    public void show_for(Character player, WorldNpcData npc)
    {
        if (!GodotObject.IsInstanceValid(player) || !GodotObject.IsInstanceValid(npc)) return;
        _player = player;
        _npc = npc;
        ensure_npc_offer();
        refresh_grids();
        Show();
        MoveToFront();
    }

    public void close()
    {
        HoverInfoManager.HideAll();
        Hide();
        Closed?.Invoke();
    }

    private void build_ui()
    {
        var backdrop = new TextureRect
        {
            Texture = new AtlasTexture { Atlas = GD.Load<Texture2D>("res://textures/ui/inventory_20261012/approved_shell_v1.png"), Region = new Rect2(270, 0, 1402, 941), FilterClip = true },
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(backdrop);

        var canvas = QuietRoomStyle.Canvas(this);

        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 34);
        margin.AddThemeConstantOverride("margin_top", 26);
        margin.AddThemeConstantOverride("margin_right", 34);
        margin.AddThemeConstantOverride("margin_bottom", 24);
        canvas.AddChild(margin);

        var root = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 14);
        margin.AddChild(root);

        var header = new HBoxContainer(); root.AddChild(header);
        var title = make_label("交易", 32, QuietRoomStyle.Ink);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill; header.AddChild(title);
        var close_button = make_button("返回", 22, new Vector2(120, 46));
        close_button.Pressed += close; header.AddChild(close_button);
        var instruction = make_label("点击选取物品 · 拖动数量条选择数量 · 悬停查看详情", 18, WorldHudStyle.SecondaryInk);
        root.AddChild(instruction);

        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 14);
        root.AddChild(body);

        body.AddChild(build_inventory_panel(true));
        body.AddChild(build_settlement_panel());
        body.AddChild(build_inventory_panel(false));

    }

    private Control build_inventory_panel(bool player_side)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0, 0),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        panel.AddThemeStyleboxOverride("panel", QuietRoomStyle.Paper(1, 0));

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 18);
        margin.AddThemeConstantOverride("margin_top", 14);
        margin.AddThemeConstantOverride("margin_right", 18);
        margin.AddThemeConstantOverride("margin_bottom", 14);
        panel.AddChild(margin);

        var column = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 10);
        margin.AddChild(column);

        var name = make_label(player_side ? "我的行囊" : "", 24, QuietRoomStyle.Ink);
        name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        if (!player_side) _npc_name_label = name;
        column.AddChild(name);
        column.AddChild(new HSeparator());

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
        };
        column.AddChild(scroll);
        var grid = new GridContainer
        {
            Columns = 5,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 10);
        scroll.AddChild(grid);
        if (player_side) _player_grid = grid;
        else _npc_grid = grid;
        QuietRoomStyle.Frame(panel);
        return panel;
    }

    private Control build_settlement_panel()
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(260, 0),
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        panel.AddThemeStyleboxOverride("panel", QuietRoomStyle.Paper(1, 0));
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 22);
        margin.AddThemeConstantOverride("margin_top", 18);
        margin.AddThemeConstantOverride("margin_right", 22);
        margin.AddThemeConstantOverride("margin_bottom", 18);
        panel.AddChild(margin);
        var column = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 14);
        margin.AddChild(column);

        var heading = make_label("对方态度", 24, QuietRoomStyle.Ink);
        heading.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(heading);
        column.AddChild(new HSeparator());

        _attitude_label = make_label("请选择双方物品", 24, QuietRoomStyle.Ink);
        _attitude_label.HorizontalAlignment = HorizontalAlignment.Center;
        _attitude_label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        column.AddChild(_attitude_label);

        _trade_terms_label = make_label("选择双方物品后判断", 19, WorldHudStyle.SecondaryInk);
        _trade_terms_label.HorizontalAlignment = HorizontalAlignment.Center;
        _trade_terms_label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        column.AddChild(_trade_terms_label);

        var spacer = new Control { SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        column.AddChild(spacer);

        _trade_button = make_button("交易", 27, new Vector2(0, 58));
        _trade_button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _trade_button.Pressed += complete_trade;
        column.AddChild(_trade_button);

        _floating_hint_panel = new PanelContainer
        {
            Visible = false,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _floating_hint_panel.AddThemeStyleboxOverride("panel", panel_style(new Color(.035f, .04f, .035f, .96f), new Color("9a7a3d"), 5));
        _floating_hint = make_label("", 16, new Color("f1dfad"));
        _floating_hint.HorizontalAlignment = HorizontalAlignment.Center;
        _floating_hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _floating_hint_panel.AddChild(_floating_hint);
        column.AddChild(_floating_hint_panel);
        QuietRoomStyle.Frame(panel);
        return panel;
    }

    private void refresh_grids()
    {
        if (_player is null || _npc is null || _player_grid is null || _npc_grid is null) return;
        HoverInfoManager.HideAll();
        _player_entries.Clear();
        _npc_entries.Clear();
        clear_grid(_player_grid);
        clear_grid(_npc_grid);

        foreach (var entry in _player.get_inventory_snapshot())
        {
            var path = entry.GetValueOrDefault("item_path", "").AsString();
            var count = entry.GetValueOrDefault("count", 0).AsInt32();
            var equipped = entry.GetValueOrDefault("equipped", false).AsBool();
            if (string.IsNullOrWhiteSpace(path) || count <= 0 || equipped || load_item(path) is null) continue;
            _player_entries.Add(new TradeEntry { path = path, count = count });
        }
        foreach (var key in _npc.trade_inventory.Keys)
        {
            var path = key.AsString();
            var count = _npc.trade_inventory[key].AsInt32();
            if (string.IsNullOrWhiteSpace(path) || count <= 0 || load_item(path) is null) continue;
            _npc_entries.Add(new TradeEntry { path = path, count = count });
        }

        populate_grid(_player_grid, _player_entries);
        populate_grid(_npc_grid, _npc_entries);
        var npc_name = string.IsNullOrWhiteSpace(_npc.npc_name) ? "对方" : _npc.npc_name;
        if (_npc_name_label is not null) _npc_name_label.Text = $"{npc_name} · {_npc.formal_identity} · {_npc.cultivation_realm}";
        update_attitude();
    }

    private void populate_grid(GridContainer grid, List<TradeEntry> entries)
    {
        if (_slot_scene is null) return;
        var slot_index = 0;
        foreach (var entry in entries.Take(VisibleSlotCount))
        {
            if (_slot_scene.Instantiate() is not StorageItemSlot slot) continue;
            grid.AddChild(slot);
            configure_trade_slot(slot);
            if (load_item(entry.path) is { } item)
            {
                slot.set_item(slot_index, entry.path, item, entry.count);
                var item_name = slot.GetNodeOrNull<Label>("ItemName");
                item_name?.AddThemeColorOverride("font_color", QuietRoomStyle.Ink);
                if (item_name is not null) item_name.Text = $"{item.entity_name} * {entry.count}";
                var category = StoragePanel.category_of(item, entry.path);
                if (!string.IsNullOrWhiteSpace(item.item_subcategory)) category = string.IsNullOrWhiteSpace(category) ? item.item_subcategory : $"{category} / {item.item_subcategory}";
                var grade = item.craft_data is MaterialCraftData material ? $"品阶：{material.material_grade_name()}（{material.material_score():0.##}）" : item.get_grade_string();
                var metadata = string.Join(" · ", new[] { category, grade, $"数量 x{entry.count}" }.Where(value => !string.IsNullOrWhiteSpace(value)));
                var tags = StoragePanel.build_tags(item.tags, item.craft_data as MaterialCraftData);
                var effect = StoragePanel.build_item_usage_effect(item);
                var icon = string.IsNullOrWhiteSpace(item.icon_path) || !ResourceLoader.Exists(item.icon_path) ? null : ResourceLoader.Load<Texture2D>(item.icon_path);
                HoverInfoManager.SetInfo(slot, new HoverInfoManager.HoverInfoContent(item.entity_name,
                    $"词缀与功用\n{tags}\n\n使用效果\n{effect}", icon, metadata));
            }
            var frame = make_selection_frame();
            slot.AddChild(frame);
            var mark = make_checkmark();
            slot.AddChild(mark);
            entry.slot = slot;
            entry.checkmark = mark;
            entry.selection_frame = frame;
            slot.clicked += _ => toggle_entry(entry);
            slot_index++;
        }
        while (slot_index < MinimumDisplaySlotCount)
        {
            if (_slot_scene.Instantiate() is not StorageItemSlot empty) break;
            grid.AddChild(empty);
            configure_trade_slot(empty);
            empty.set_empty(slot_index++);
        }
    }

    private void toggle_entry(TradeEntry entry)
    {
        entry.selected = !entry.selected;
        entry.selected_count = entry.selected ? Math.Max(1, Math.Min(entry.selected_count == 0 ? 1 : entry.selected_count, entry.count)) : 0;
        if (entry.checkmark is not null) entry.checkmark.Visible = entry.selected;
        if (entry.selection_frame is not null) entry.selection_frame.Visible = entry.selected;
        update_quantity_picker(entry);
        update_attitude();
    }

    private void configure_trade_slot(StorageItemSlot slot)
    {
        if (!slot.HasMeta("trade_material_layout"))
        {
            slot.configure_inventory_layout(TradeSlotWidth, TradeSlotBaseHeight);
            slot.SetMeta("trade_material_layout", true);
        }
        slot.CustomMinimumSize = new Vector2(TradeSlotWidth, TradeSlotBaseHeight);
        if (slot.GetNodeOrNull<TextureRect>("Frame/ItemIcon") is { } icon)
        {
            icon.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
            icon.OffsetLeft = 12; icon.OffsetRight = -12; icon.OffsetTop = 10; icon.OffsetBottom = TradeSlotIconSize;
        }
        if (slot.GetNodeOrNull<Label>("ItemName") is { } name)
        {
            name.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
            name.OffsetLeft = 5; name.OffsetRight = -5;
            name.OffsetTop = TradeSlotIconSize + 4;
            name.OffsetBottom = TradeSlotBaseHeight;
            name.AddThemeFontSizeOverride("font_size", 18);
        }
    }

    private void update_quantity_picker(TradeEntry entry)
    {
        if (entry.slot is null) return;
        var show_picker = entry.selected && entry.count > 1;
        if (!show_picker)
        {
            entry.quantity_slider?.QueueFree();
            entry.quantity_label?.QueueFree();
            entry.quantity_slider = null;
            entry.quantity_label = null;
            configure_trade_slot(entry.slot);
            return;
        }

        entry.slot.CustomMinimumSize = new Vector2(TradeSlotWidth, 196);
        if (entry.quantity_slider is not null)
        {
            entry.quantity_slider.MaxValue = entry.count;
            entry.quantity_slider.Value = entry.selected_count;
            if (entry.quantity_label is not null) entry.quantity_label.Text = $"数量 {entry.selected_count}/{entry.count}";
            return;
        }

        var amount = make_label($"数量 {entry.selected_count}/{entry.count}", 14, new Color("6b5429"));
        amount.HorizontalAlignment = HorizontalAlignment.Center;
        amount.MouseFilter = MouseFilterEnum.Ignore;
        amount.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        amount.OffsetTop = 148; amount.OffsetBottom = 168;
        entry.slot.AddChild(amount);
        entry.quantity_label = amount;

        var slider = new HSlider
        {
            MinValue = 1,
            MaxValue = entry.count,
            Step = 1,
            Value = entry.selected_count,
        };
        slider.AddThemeStyleboxOverride("grabber_area", panel_style(QuietRoomStyle.Teal, QuietRoomStyle.Teal, 0));
        slider.AddThemeStyleboxOverride("slider", panel_style(new Color("c2bea9"), new Color("a39372"), 0));
        slider.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        slider.OffsetLeft = 9; slider.OffsetTop = 172; slider.OffsetRight = -9; slider.OffsetBottom = 191;
        slider.ValueChanged += value =>
        {
            entry.selected_count = (int)Math.Round(value);
            if (entry.quantity_label is not null) entry.quantity_label.Text = $"数量 {entry.selected_count}/{entry.count}";
            update_attitude();
        };
        entry.slot.AddChild(slider);
        entry.quantity_slider = slider;
    }

    private void update_attitude()
    {
        var selected_player = _player_entries.Where(item => item.selected).ToList();
        var selected_npc = _npc_entries.Where(item => item.selected).ToList();
        if (selected_player.Count == 0 && selected_npc.Count == 0)
        {
            set_attitude("请选择双方物品", "选择双方物品后判断", false, false);
            return;
        }

        var player_value = selected_player.Sum(item_value);
        var npc_value = selected_npc.Sum(item_value);
        var basis = Math.Max(30, npc_value);
        var ratio = (player_value - npc_value) / (float)basis;

        string attitude;
        var can_trade = ratio >= -.10f;
        if (ratio >= .45f) attitude = "满意交易";
        else if (ratio >= .10f) attitude = "比较满意";
        else if (can_trade) attitude = "能接受";
        else if (ratio >= -.45f) attitude = "差价偏大";
        else attitude = "不满意";

        var detail = can_trade
            ? "双方按当前物品价值判断，对方愿意成交。"
            : "物品价值差距较大，对方不愿按此条件成交。";
        set_attitude(attitude, detail, can_trade, true);
    }

    private void set_attitude(string attitude, string terms_text, bool can_trade, bool has_selection)
    {
        if (_attitude_label is not null)
        {
            _attitude_label.Text = attitude;
            _attitude_label.AddThemeColorOverride("font_color", can_trade ? QuietRoomStyle.Teal : new Color("985344"));
        }
        if (_trade_terms_label is not null) _trade_terms_label.Text = terms_text;
        if (_trade_button is not null)
        {
            _trade_button.Disabled = !can_trade;
        }
        if (_floating_hint_panel is not null)
        {
            _floating_hint_panel.Visible = has_selection && !can_trade;
            if (_floating_hint is not null) _floating_hint.Text = has_selection ? "对方不愿接受这笔交易" : "";
        }
    }

    private void complete_trade()
    {
        if (_player is null || _npc is null || _trade_button?.Disabled != false) return;
        var outgoing = _player_entries.Where(item => item.selected).ToList();
        var incoming = _npc_entries.Where(item => item.selected).ToList();
        if (outgoing.Count == 0 && incoming.Count == 0) return;
        if (!_player.can_spend_world_stamina(WorldStaminaRules.InteractionCost))
        {
            if (_floating_hint_panel is not null) _floating_hint_panel.Show();
            if (_floating_hint is not null) _floating_hint.Text = $"精力不足，完成交易需要 {WorldStaminaRules.InteractionCost:0.#} 点；当前 {_player.current_world_stamina:0.#}。";
            return;
        }

        var removed = new List<TradeEntry>();
        foreach (var entry in outgoing)
        {
            if (_player.remove_item_from_inventory(entry.path, entry.selected_count)) removed.Add(entry);
            else
            {
                foreach (var rollback in removed) _player.add_item_to_inventory_silently(rollback.path, rollback.selected_count);
                return;
            }
        }

        var added = new List<TradeEntry>();
        foreach (var entry in incoming)
        {
            if (_player.add_item_to_inventory_silently(entry.path, entry.selected_count)) added.Add(entry);
            else
            {
                foreach (var rollback in added) _player.remove_item_from_inventory(rollback.path, rollback.selected_count);
                foreach (var rollback in removed) _player.add_item_to_inventory_silently(rollback.path, rollback.selected_count);
                return;
            }
        }

        if (!_player.try_spend_world_stamina(WorldStaminaRules.InteractionCost))
        {
            foreach (var rollback in added) _player.remove_item_from_inventory(rollback.path, rollback.selected_count);
            foreach (var rollback in removed) _player.add_item_to_inventory_silently(rollback.path, rollback.selected_count);
            return;
        }
        var outgoingValue = outgoing.Sum(item_value);
        var incomingValue = incoming.Sum(item_value);
        foreach (var entry in outgoing) _npc.trade_inventory[entry.path] = _npc.trade_inventory.TryGetValue(entry.path, out var old) ? old.AsInt32() + entry.selected_count : entry.selected_count;
        foreach (var entry in incoming)
        {
            var remaining = _npc.trade_inventory.TryGetValue(entry.path, out var old) ? old.AsInt32() - entry.selected_count : 0;
            if (remaining <= 0) _npc.trade_inventory.Remove(entry.path);
            else _npc.trade_inventory[entry.path] = remaining;
        }
        foreach (var entry in incoming) _player.publish_item_acquired_notice(entry.path, entry.selected_count);
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        var month = time is null ? 0 : (time.year - 1) * 12 + time.month - 1;
        var eventId = "trade:" + Guid.NewGuid().ToString("N");
        var isFairTrade = CharacterRelationshipRules.IsFairTrade(outgoingValue, incomingValue);
        var relationshipFeedback = "信任：本次交换未达到公平估值范围，关系不变。";
        if (isFairTrade && _player.entity_data is HumanData playerData)
        {
            CharacterRelationshipRules.RecordFairTrade(playerData, _npc, eventId, month);
            relationshipFeedback = CharacterRelationshipRules.DescribePlayerEvidence(playerData, _npc.npc_id, eventId,
                "信任：本次交易已记录，关系证据已处理。");
        }
        var date = time is null ? $"{month / 12 + 1}年{month % 12 + 1}月" : $"{time.year}年{time.month}月{time.day}日 {time.hour:D2}时";
        var outgoingItems = string.Join("、", outgoing.Select(entry => $"{load_item(entry.path)?.entity_name ?? "物品"} ×{entry.selected_count}"));
        var incomingItems = string.Join("、", incoming.Select(entry => $"{load_item(entry.path)?.entity_name ?? "物品"} ×{entry.selected_count}"));
        _npc.record(new NpcActionRecord
        {
            month_index = month, date_label = date, action_id = "player_trade_completed", action_name = "交易",
            node_id = _npc.current_node_id, subscene_id = _npc.current_subscene_id, target_npc_id = "player",
            reason = "双方完成物品交换。",
            outcome = $"你交出{(outgoingItems.Length == 0 ? "无物品" : outgoingItems)}，换得{(incomingItems.Length == 0 ? "无物品" : incomingItems)}。\n{relationshipFeedback}"
        });
        refresh_grids();
        if (_floating_hint_panel is not null) _floating_hint_panel.Show();
        if (_floating_hint is not null) _floating_hint.Text = relationshipFeedback;
        TradeCompleted?.Invoke();
    }

    private void ensure_npc_offer()
    {
        if (_npc is null || _npc.trade_inventory.Count > 0) return;
        var seed = stable_seed(_npc.npc_id);
        for (var i = 0; i < 5; i++)
        {
            var path = DefaultNpcTradeItems[(int)((seed + (ulong)i * 7) % (ulong)DefaultNpcTradeItems.Length)];
            if (load_item(path) is null) continue;
            _npc.trade_inventory[path] = 1 + (int)((seed + (ulong)i * 11) % 4);
        }
    }

    private ItemData? load_item(string path)
    {
        if (_item_cache.TryGetValue(path, out var item)) return item;
        item = string.IsNullOrWhiteSpace(path) || !ResourceLoader.Exists(path) ? null : ResourceLoader.Load<ItemData>(path);
        if (!GongfaAvailability.IsItemEnabled(item)) item = null;
        _item_cache[path] = item;
        return item;
    }

    private int item_value(TradeEntry entry)
    {
        var item = load_item(entry.path);
        return TradeValueRules.Estimate(item, entry.selected_count);
    }

    private void clear_grid(GridContainer grid)
    {
        foreach (var child in grid.GetChildren()) { grid.RemoveChild(child); child.QueueFree(); }
    }

    private Label make_checkmark()
    {
        var label = make_label("✓", 18, QuietRoomStyle.Teal);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.MouseFilter = MouseFilterEnum.Ignore;
        label.AddThemeColorOverride("font_outline_color", new Color(.16f, .12f, .06f, .88f));
        label.AddThemeConstantOverride("outline_size", 0);
        label.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight);
        label.OffsetLeft = -25; label.OffsetRight = -4; label.OffsetTop = 4; label.OffsetBottom = 25;
        label.Visible = false;
        return label;
    }

    private Panel make_selection_frame()
    {
        var frame = new Panel { MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        frame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        frame.AddThemeStyleboxOverride("panel", panel_style(Colors.Transparent, QuietRoomStyle.Teal, 0));
        return frame;
    }

    private Button make_button(string text, int size, Vector2 minimum)
    {
        var button = new Button { Text = text, CustomMinimumSize = minimum };
        QuietRoomStyle.Primary(button);
        button.CustomMinimumSize = minimum;
        return button;
    }

    private Label make_label(string text, int size, Color color)
    {
        var label = new Label { Text = text };
        apply_font(label, size, color);
        return label;
    }

    private void apply_font(Control control, int size, Color color)
    {
        if (_font is not null) control.AddThemeFontOverride("font", _font);
        control.AddThemeFontSizeOverride("font_size", size);
        control.AddThemeColorOverride("font_color", color);
        if (control is RichTextLabel rich)
        {
            if (_font is not null) rich.AddThemeFontOverride("normal_font", _font);
            rich.AddThemeFontSizeOverride("normal_font_size", size);
            rich.AddThemeColorOverride("default_color", color);
        }
    }

    private static StyleBoxFlat panel_style(Color background, Color border, int radius)
    {
        var style = new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
        };
        style.SetCornerRadiusAll(radius);
        return style;
    }

    private static ulong stable_seed(string value)
    {
        unchecked
        {
            ulong hash = 1469598103934665603;
            foreach (var c in value ?? "") { hash ^= c; hash *= 1099511628211; }
            return hash;
        }
    }
}
