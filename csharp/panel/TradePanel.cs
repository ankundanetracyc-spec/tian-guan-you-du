using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GDict = Godot.Collections.Dictionary;

/// <summary>
/// 世界人物交易面板：两侧纯文字目录，中间分别核对双方交出物品。
/// 真实数量、估值与成交仍走原业务规则，不把角色完整随身物品暴露给对方。
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
        public Button? row;
        public SpinBox? quantity_picker;
    }

    private const float TradeRowHeight = 68f;
    private static readonly string[] DefaultNpcTradeItems =
    [
        "res://resources/entities/items/灵石.tres",
        "res://resources/entities/items/兽皮.tres",
        "res://resources/entities/items/兽骨.tres",
        "res://resources/entities/items/黄庭养形篇秘籍.tres",
    ];

    private Character? _player;
    private WorldNpcData? _npc;
    private FontFile? _font;
    private GridContainer? _player_grid;
    private GridContainer? _npc_grid;
    private Label? _npc_name_label;
    private VBoxContainer? _outgoing_list, _incoming_list;
    private Label? _selection_summary;
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
        _font = ResourceLoader.Load<FontFile>("res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf");
        build_ui();
        Hide();
    }

    public override void _Input(InputEvent @event)
    {
        if (Visible && @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            close();
            GetViewport().SetInputAsHandled();
        }
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
        _player = null;
        _npc = null;
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
        foreach (var edge in new[] { "left", "right" }) margin.AddThemeConstantOverride("margin_" + edge, 34);
        foreach (var edge in new[] { "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + edge, 24);
        canvas.AddChild(margin);
        var root = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 12); margin.AddChild(root);
        var header = new HBoxContainer(); root.AddChild(header);
        var back = make_button("‹", 32, new Vector2(52, 48));
        back.TooltipText = "返回"; back.Pressed += close; header.AddChild(back);
        var title = make_label("交易", 34, QuietRoomStyle.Ink); header.AddChild(title);
        _npc_name_label = make_label("", 20, WorldHudStyle.SecondaryInk);
        _npc_name_label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _npc_name_label.HorizontalAlignment = HorizontalAlignment.Right;
        header.AddChild(_npc_name_label);
        root.AddChild(new HSeparator());
        var body = new HBoxContainer { Name = "TradeColumns", SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 14); root.AddChild(body);
        body.AddChild(build_inventory_panel(true));
        body.AddChild(build_offer_panel(true));
        body.AddChild(build_offer_panel(false));
        body.AddChild(build_inventory_panel(false));

        var footer = new HBoxContainer { CustomMinimumSize = new Vector2(0, 82) };
        footer.AddThemeConstantOverride("separation", 24); root.AddChild(footer);
        var summary = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        footer.AddChild(summary);
        _selection_summary = make_label("尚未选择物品", 21, QuietRoomStyle.Ink); summary.AddChild(_selection_summary);
        _trade_terms_label = make_label("选择物品后判断", 17, WorldHudStyle.SecondaryInk);
        _trade_terms_label.AutowrapMode = TextServer.AutowrapMode.WordSmart; summary.AddChild(_trade_terms_label);
        _attitude_label = make_label("请选择双方物品", 23, QuietRoomStyle.Ink);
        _attitude_label.VerticalAlignment = VerticalAlignment.Center; footer.AddChild(_attitude_label);
        _trade_button = make_button("交易", 25, new Vector2(150, 56));
        _trade_button.Name = "CommitTrade"; _trade_button.Pressed += complete_trade; footer.AddChild(_trade_button);
        _floating_hint_panel = new PanelContainer { Visible = false, MouseFilter = MouseFilterEnum.Ignore };
        _floating_hint_panel.AddThemeStyleboxOverride("panel", panel_style(new Color(.035f, .04f, .035f, .96f), new Color("9a7a3d"), 2));
        _floating_hint = make_label("", 17, new Color("f1dfad"));
        _floating_hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _floating_hint_panel.AddChild(_floating_hint); root.AddChild(_floating_hint_panel);
    }

    private VBoxContainer build_trade_column(string title, float ratio, bool offer)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsStretchRatio = ratio };
        var paper = InventoryUiPaper.Style("index_panel_paper.png");
        paper.ContentMarginTop = 18; paper.ContentMarginBottom = 16;
        panel.AddThemeStyleboxOverride("panel", paper);
        var column = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 10); panel.AddChild(column);
        var heading = make_label(title, 23, QuietRoomStyle.Ink); column.AddChild(heading);
        var labels = new HBoxContainer(); column.AddChild(labels);
        var name = make_label("名称", 16, WorldHudStyle.SecondaryInk); name.SizeFlagsHorizontal = SizeFlags.ExpandFill; labels.AddChild(name);
        if (!offer) labels.AddChild(make_label("可供", 16, WorldHudStyle.SecondaryInk));
        var quantity = make_label(offer ? "数量" : "本次", 16, WorldHudStyle.SecondaryInk);
        quantity.CustomMinimumSize = new Vector2(offer ? 44 : 88, 0); quantity.HorizontalAlignment = HorizontalAlignment.Right; labels.AddChild(quantity);
        column.SetMeta("panel", panel);
        return column;
    }

    private Control build_inventory_panel(bool player_side)
    {
        var column = build_trade_column(player_side ? "我的可交易物品" : "对方愿意交易的物品", 1.2f, false);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        column.AddChild(scroll);
        var grid = new GridContainer { Name = player_side ? "PlayerTradeList" : "NpcTradeList", Columns = 1, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("v_separation", 2); scroll.AddChild(grid);
        column.AddChild(make_label("点击添加一件 · 右侧可改数量", 15, WorldHudStyle.SecondaryInk));
        if (player_side) _player_grid = grid; else _npc_grid = grid;
        return column.GetParent<Control>();
    }

    private Control build_offer_panel(bool outgoing)
    {
        var column = build_trade_column(outgoing ? "我将交出" : "我将获得", 1f, true);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        column.AddChild(scroll);
        var list = new VBoxContainer { Name = outgoing ? "OutgoingTradeList" : "IncomingTradeList", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 2); scroll.AddChild(list);
        column.AddChild(make_label("点击减少一件", 15, WorldHudStyle.SecondaryInk));
        if (outgoing) _outgoing_list = list; else _incoming_list = list;
        return column.GetParent<Control>();
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
            if (string.IsNullOrWhiteSpace(path) || count <= 0 || equipped || entry.GetValueOrDefault("storage_locked", false).AsBool() || load_item(path) is null) continue;
            var existing = _player_entries.FirstOrDefault(candidate => candidate.path == path);
            if (existing is null) _player_entries.Add(new TradeEntry { path = path, count = count });
            else existing.count += count;
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
        foreach (var entry in entries)
        {
            if (load_item(entry.path) is not { } item) continue;
            var button = new Button { Name = "TradeSourceRow", CustomMinimumSize = new Vector2(0, TradeRowHeight), SizeFlagsHorizontal = SizeFlags.ExpandFill };
            button.AddThemeStyleboxOverride("normal", trade_row_style(false));
            button.AddThemeStyleboxOverride("hover", trade_row_style(true));
            button.AddThemeStyleboxOverride("pressed", trade_row_style(true));
            button.AddThemeStyleboxOverride("focus", QuietRoomStyle.Edge(true, 0));
            var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            row.OffsetLeft = 10; row.OffsetRight = -10; row.OffsetTop = 6; row.OffsetBottom = -6;
            row.AddThemeConstantOverride("separation", 8); button.AddChild(row);
            var name = make_label(item.entity_name, 21, QuietRoomStyle.Ink);
            name.Name = "ItemName"; name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            name.AutowrapMode = TextServer.AutowrapMode.WordSmart; name.VerticalAlignment = VerticalAlignment.Center; name.MouseFilter = MouseFilterEnum.Ignore; row.AddChild(name);
            var available = make_label(entry.count.ToString(), 20, WorldHudStyle.SecondaryInk);
            available.CustomMinimumSize = new Vector2(35, 0); available.HorizontalAlignment = HorizontalAlignment.Right;
            available.VerticalAlignment = VerticalAlignment.Center; available.MouseFilter = MouseFilterEnum.Ignore; row.AddChild(available);
            var picker = new SpinBox { Name = "TradeQuantity", MinValue = 0, MaxValue = entry.count, Step = 1, Value = 0, CustomMinimumSize = new Vector2(88, 0), SizeFlagsVertical = SizeFlags.ShrinkCenter };
            picker.GetLineEdit().Alignment = HorizontalAlignment.Right;
            apply_font(picker, 20, QuietRoomStyle.Ink); row.AddChild(picker);
            entry.row = button; entry.quantity_picker = picker;
            button.Pressed += () => set_entry_quantity(entry, Math.Min(entry.count, entry.selected_count + 1));
            picker.ValueChanged += value => set_entry_quantity(entry, (int)Math.Round(value));
            HoverInfoManager.SetInfo(button, StoragePanel.ItemHover(item, entry.count, entry.path));
            grid.AddChild(button);
        }
        if (entries.Count == 0) grid.AddChild(make_label("暂无可交易物品", 20, WorldHudStyle.SecondaryInk));
    }

    private static StyleBoxFlat trade_row_style(bool selected) => new()
    {
        BgColor = selected ? new Color("b9c9aa66") : Colors.Transparent,
        BorderColor = new Color("bcb39388"), BorderWidthBottom = 1,
        ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 6, ContentMarginBottom = 6,
    };

    private void set_entry_quantity(TradeEntry entry, int quantity)
    {
        entry.selected_count = Math.Clamp(quantity, 0, entry.count);
        entry.selected = entry.selected_count > 0;
        entry.quantity_picker?.SetValueNoSignal(entry.selected_count);
        entry.row?.AddThemeStyleboxOverride("normal", trade_row_style(entry.selected));
        update_attitude();
    }

    private void refresh_offers()
    {
        fill_offer(_outgoing_list, _player_entries);
        fill_offer(_incoming_list, _npc_entries);
        if (_selection_summary is not null)
            _selection_summary.Text = $"交出 {_player_entries.Sum(e => e.selected_count)} 件 · 获得 {_npc_entries.Sum(e => e.selected_count)} 件";
    }

    private void fill_offer(VBoxContainer? list, List<TradeEntry> entries)
    {
        if (list is null) return;
        foreach (var child in list.GetChildren()) { list.RemoveChild(child); child.QueueFree(); }
        foreach (var entry in entries.Where(e => e.selected && e.selected_count > 0))
        {
            var item = load_item(entry.path);
            if (item is null) continue;
            var button = new Button { Name = "TradeOfferRow", CustomMinimumSize = new Vector2(0, TradeRowHeight) };
            button.AddThemeStyleboxOverride("normal", trade_row_style(true));
            button.AddThemeStyleboxOverride("hover", trade_row_style(true));
            button.AddThemeStyleboxOverride("pressed", trade_row_style(true));
            var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); row.OffsetLeft = 10; row.OffsetRight = -10; row.OffsetTop = 6; row.OffsetBottom = -6;
            button.AddChild(row);
            var name = make_label(item.entity_name, 21, QuietRoomStyle.Ink); name.Name = "ItemName";
            name.SizeFlagsHorizontal = SizeFlags.ExpandFill; name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            name.VerticalAlignment = VerticalAlignment.Center; name.MouseFilter = MouseFilterEnum.Ignore; row.AddChild(name);
            var count = make_label(entry.selected_count.ToString(), 22, QuietRoomStyle.Teal);
            count.Name = "SelectedCount"; count.CustomMinimumSize = new Vector2(40, 0); count.HorizontalAlignment = HorizontalAlignment.Right;
            count.VerticalAlignment = VerticalAlignment.Center; count.MouseFilter = MouseFilterEnum.Ignore; row.AddChild(count);
            button.Pressed += () => set_entry_quantity(entry, entry.selected_count - 1);
            HoverInfoManager.SetInfo(button, StoragePanel.ItemHover(item, entry.selected_count, entry.path));
            list.AddChild(button);
        }
        if (list.GetChildCount() == 0) list.AddChild(make_label("尚未选择物品", 19, WorldHudStyle.SecondaryInk));
    }

    private static string trade_item_identity(ItemData item)
    {
        var category = StoragePanel.category_of(item, item.ResourcePath);
        var grade = item.craft_data is MaterialCraftData material ? material.material_grade_name()
            : item is PortraitClothingData ? "" : item.get_grade_string();
        return string.Join(" · ", new[] { category, grade }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private void update_attitude()
    {
        refresh_offers();
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
        var player = _player; var npc = _npc;
        if (player is null || npc is null || _trade_button?.Disabled != false) return;
        var outgoing = _player_entries.Where(item => item.selected).ToList();
        var incoming = _npc_entries.Where(item => item.selected).ToList();
        if (outgoing.Count == 0 && incoming.Count == 0) return;
        if (!player.can_spend_world_stamina(WorldStaminaRules.InteractionCost))
        {
            if (_floating_hint_panel is not null) _floating_hint_panel.Show();
            if (_floating_hint is not null) _floating_hint.Text = $"精力不足，完成交易需要 {WorldStaminaRules.InteractionCost:0.#} 点；当前 {player.current_world_stamina:0.#}。";
            return;
        }

        // Recheck the live sources before mutating either side: the catalog is a view, not ownership.
        if (outgoing.Concat(incoming).Any(e => e.selected_count <= 0 || e.selected_count > e.count)
            || incoming.Any(e => !npc.trade_inventory.TryGetValue(e.path, out var count) || count.AsInt32() < e.selected_count))
        {
            trade_failure("可交易物品数量已变化，请重新选择。");
            return;
        }
        var outgoingValue = outgoing.Sum(item_value);
        var incomingValue = incoming.Sum(item_value);
        if ((outgoingValue - incomingValue) / (float)Math.Max(30, incomingValue) < -.10f)
        {
            trade_failure("对方不愿接受这笔交易。");
            return;
        }
        var giving = outgoing.ToDictionary(e => e.path, e => e.selected_count, StringComparer.Ordinal);
        var receiving = incoming.ToDictionary(e => e.path, e => e.selected_count, StringComparer.Ordinal);
        var npcResult = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var path in giving.Keys.Union(receiving.Keys, StringComparer.Ordinal))
        {
            var previous = npc.trade_inventory.TryGetValue(path, out var count) ? count.AsInt32() : 0;
            var next = (long)previous + giving.GetValueOrDefault(path) - receiving.GetValueOrDefault(path);
            if (next < 0 || next > int.MaxValue)
            {
                trade_failure("对方当前无法完成这笔交换。");
                return;
            }
            npcResult[path] = (int)next;
        }
        // The inventory model releases the offered instances in a copy, plans all incoming footprints,
        // and commits once. Stamina is consumed only after validation and before inventory notifications.
        if (!player.try_exchange_inventory(giving, receiving, announceAcquisition: false,
                beforeCommit: () => player.try_spend_world_stamina(WorldStaminaRules.InteractionCost)))
        {
            trade_failure(player.can_spend_world_stamina(WorldStaminaRules.InteractionCost)
                ? "行囊空间不足，或所选物品已不可交易。请调整后重试。"
                : $"精力不足，完成交易需要 {WorldStaminaRules.InteractionCost:0.#} 点。");
            return;
        }
        foreach (var pair in npcResult)
        {
            if (pair.Value == 0) npc.trade_inventory.Remove(pair.Key);
            else npc.trade_inventory[pair.Key] = pair.Value;
        }
        foreach (var entry in incoming) player.publish_item_acquired_notice(entry.path, entry.selected_count);
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        var month = time is null ? 0 : (time.year - 1) * 12 + time.month - 1;
        var eventId = "trade:" + Guid.NewGuid().ToString("N");
        var isFairTrade = CharacterRelationshipRules.IsFairTrade(outgoingValue, incomingValue);
        var relationshipFeedback = "信任：本次交换未达到公平估值范围，关系不变。";
        if (isFairTrade && player.entity_data is HumanData playerData)
        {
            CharacterRelationshipRules.RecordFairTrade(playerData, npc, eventId, month);
            relationshipFeedback = CharacterRelationshipRules.DescribePlayerEvidence(playerData, npc.npc_id, eventId,
                "信任：本次交易已记录，关系证据已处理。");
        }
        var date = time is null ? $"{month / 12 + 1}年{month % 12 + 1}月" : $"{time.year}年{time.month}月{time.day}日";
        var outgoingItems = string.Join("、", outgoing.Select(entry => $"{load_item(entry.path)?.entity_name ?? "物品"} ×{entry.selected_count}"));
        var incomingItems = string.Join("、", incoming.Select(entry => $"{load_item(entry.path)?.entity_name ?? "物品"} ×{entry.selected_count}"));
        npc.record(new NpcActionRecord
        {
            month_index = month, date_label = date, action_id = "player_trade_completed", action_name = "交易",
            node_id = npc.current_node_id, subscene_id = npc.current_subscene_id, target_npc_id = "player",
            reason = "双方完成物品交换。",
            outcome = $"你交出{(outgoingItems.Length == 0 ? "无物品" : outgoingItems)}，换得{(incomingItems.Length == 0 ? "无物品" : incomingItems)}。\n{relationshipFeedback}"
        });
        refresh_grids();
        if (_floating_hint_panel is not null) _floating_hint_panel.Show();
        if (_floating_hint is not null) _floating_hint.Text = relationshipFeedback;
        TradeCompleted?.Invoke();
    }

    private void trade_failure(string message)
    {
        if (_floating_hint_panel is not null) _floating_hint_panel.Show();
        if (_floating_hint is not null) _floating_hint.Text = message;
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
        if ((!GongfaAvailability.IsItemEnabled(item) || !AlchemyProductCatalog.IsItemEnabled(item))) item = null;
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
