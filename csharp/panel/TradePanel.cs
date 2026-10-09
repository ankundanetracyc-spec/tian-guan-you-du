using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GDict = Godot.Collections.Dictionary;

/// <summary>
/// 世界人物交易面板。交易采用全屏行囊式布局：两侧只展示可交易物品，
/// 中间只显示对方态度与人情判断，具体估值保留在规则层，不直接暴露给玩家。
/// </summary>
public partial class TradePanel : Control
{
    public event Action? Closed;

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
    private const float TradeSlotWidth = 128f;
    private const float TradeSlotIconSize = 128f;
    private const float TradeSlotBaseHeight = 160f;
    private const string SlotScenePath = "res://tscn/panel/StorageItemSlot.tscn";
    private static readonly string[] DefaultNpcTradeItems =
    [
        "res://resources/entities/items/止血草.tres",
        "res://resources/entities/items/灵石.tres",
        "res://resources/entities/items/铁矿石.tres",
        "res://resources/entities/items/山麻.tres",
        "res://resources/entities/items/兽皮.tres",
        "res://resources/entities/items/兽骨.tres",
        "res://resources/entities/items/回春丹.tres",
        "res://resources/entities/items/雷击符.tres",
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
    private Label? _favor_label;
    private PanelContainer? _floating_hint_panel;
    private Label? _floating_hint;
    private PanelContainer? _item_hover_panel;
    private TextureRect? _item_hover_icon;
    private Label? _item_hover_name;
    private Label? _item_hover_meta;
    private RichTextLabel? _item_hover_detail;
    private StorageItemSlot? _hovered_trade_slot;
    private Button? _trade_button;
    private readonly List<TradeEntry> _player_entries = [];
    private readonly List<TradeEntry> _npc_entries = [];
    private readonly Dictionary<string, ItemData?> _item_cache = [];

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        ZIndex = 500;
        _slot_scene = ResourceLoader.Load<PackedScene>(SlotScenePath);
        _font = ResourceLoader.Load<FontFile>("res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf");
        build_ui();
        Hide();
    }

    public override void _Process(double delta)
    {
        if (_item_hover_panel?.Visible != true) return;
        position_item_hover_panel();
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
        hide_item_hover();
        Hide();
        Closed?.Invoke();
    }

    private void build_ui()
    {
        var backdrop = new TextureRect
        {
            Texture = ResourceLoader.Load<Texture2D>("res://textures/ui/infopanel_fullscreen_bg_v1.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(backdrop);

        var veil = new ColorRect { Color = new Color(.035f, .04f, .035f, .54f), MouseFilter = MouseFilterEnum.Ignore };
        veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(veil);

        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 34);
        margin.AddThemeConstantOverride("margin_top", 26);
        margin.AddThemeConstantOverride("margin_right", 34);
        margin.AddThemeConstantOverride("margin_bottom", 24);
        AddChild(margin);

        var root = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 14);
        margin.AddChild(root);

        var title = make_label("交易", 34, new Color("f1dfad"));
        title.HorizontalAlignment = HorizontalAlignment.Center;
        root.AddChild(title);

        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 14);
        root.AddChild(body);

        body.AddChild(build_inventory_panel(true));
        body.AddChild(build_settlement_panel());
        body.AddChild(build_inventory_panel(false));

        var close_button = make_button("关闭", 22, new Vector2(150, 46));
        close_button.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        close_button.Pressed += close;
        root.AddChild(close_button);

        _item_hover_panel = build_item_hover_panel();
        AddChild(_item_hover_panel);
    }

    private Control build_inventory_panel(bool player_side)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(730, 0),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        panel.AddThemeStyleboxOverride("panel", panel_style(new Color(.055f, .065f, .06f, .92f), new Color("9a7a3d"), 8));

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 18);
        margin.AddThemeConstantOverride("margin_top", 14);
        margin.AddThemeConstantOverride("margin_right", 18);
        margin.AddThemeConstantOverride("margin_bottom", 14);
        panel.AddChild(margin);

        var column = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 10);
        margin.AddChild(column);

        var name = make_label(player_side ? "我" : "", 27, new Color("f1dfad"));
        name.HorizontalAlignment = HorizontalAlignment.Center;
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
        return panel;
    }

    private Control build_settlement_panel()
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(290, 0),
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        panel.AddThemeStyleboxOverride("panel", panel_style(new Color(.09f, .10f, .095f, .96f), new Color("b79a5c"), 8));
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 22);
        margin.AddThemeConstantOverride("margin_top", 18);
        margin.AddThemeConstantOverride("margin_right", 22);
        margin.AddThemeConstantOverride("margin_bottom", 18);
        panel.AddChild(margin);
        var column = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 14);
        margin.AddChild(column);

        var heading = make_label("对方态度", 27, new Color("f1dfad"));
        heading.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(heading);
        column.AddChild(new HSeparator());

        _attitude_label = make_label("请选择双方物品", 29, new Color("e6c77e"));
        _attitude_label.HorizontalAlignment = HorizontalAlignment.Center;
        _attitude_label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        column.AddChild(_attitude_label);

        _favor_label = make_label("选择双方物品后判断", 17, new Color("b9d2c0"));
        _favor_label.HorizontalAlignment = HorizontalAlignment.Center;
        _favor_label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        column.AddChild(_favor_label);

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
        return panel;
    }

    private PanelContainer build_item_hover_panel()
    {
        var panel = new PanelContainer
        {
            Name = "TradeItemHoverInfo",
            Visible = false,
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 1000,
            Size = new Vector2(410, 190),
        };
        panel.AddThemeStyleboxOverride("panel", panel_style(new Color(.025f, .03f, .028f, .96f), new Color("b79a5c"), 6));

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        panel.AddChild(margin);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 7);
        margin.AddChild(column);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 10);
        column.AddChild(header);
        _item_hover_icon = new TextureRect
        {
            CustomMinimumSize = new Vector2(58, 58),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        header.AddChild(_item_hover_icon);
        var heading = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        header.AddChild(heading);
        _item_hover_name = make_label("", 21, new Color("f1dfad"));
        _item_hover_name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        heading.AddChild(_item_hover_name);
        _item_hover_meta = make_label("", 14, new Color("c8bd9d"));
        _item_hover_meta.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        heading.AddChild(_item_hover_meta);

        column.AddChild(new HSeparator());
        column.AddChild(make_label("物品详情", 15, new Color("e6c77e")));
        _item_hover_detail = new RichTextLabel
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            ScrollActive = false,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        apply_font(_item_hover_detail, 15, new Color("f1ead7"));
        column.AddChild(_item_hover_detail);
        return panel;
    }

    private void refresh_grids()
    {
        if (_player is null || _npc is null || _player_grid is null || _npc_grid is null) return;
        hide_item_hover();
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
                item_name?.AddThemeColorOverride("font_color", Colors.White);
            }
            var frame = make_selection_frame();
            slot.AddChild(frame);
            var mark = make_checkmark();
            slot.AddChild(mark);
            entry.slot = slot;
            entry.checkmark = mark;
            entry.selection_frame = frame;
            slot.clicked += _ => toggle_entry(entry);
            slot.info_requested += (hovered, _) => show_item_hover(entry, hovered);
            slot.info_cleared += on_trade_slot_info_cleared;
            slot_index++;
        }
        while (slot_index < VisibleSlotCount)
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

    private void show_item_hover(TradeEntry entry, StorageItemSlot slot)
    {
        if (slot.is_empty || slot.is_artifact || _item_hover_panel is null) return;
        var item = load_item(entry.path);
        if (item is null) return;
        _hovered_trade_slot = slot;
        if (_item_hover_icon is not null)
            _item_hover_icon.Texture = string.IsNullOrWhiteSpace(item.icon_path) || !ResourceLoader.Exists(item.icon_path) ? null : ResourceLoader.Load<Texture2D>(item.icon_path);
        if (_item_hover_name is not null) _item_hover_name.Text = item.entity_name;
        var category = StoragePanel.category_of(item, entry.path);
        if (!string.IsNullOrWhiteSpace(item.item_subcategory)) category = string.IsNullOrWhiteSpace(category) ? item.item_subcategory : $"{category} / {item.item_subcategory}";
        var grade = item.craft_data is MaterialCraftData material ? $"品阶：{material.material_grade_name()}（{material.material_score():0.##}）" : item.get_grade_string();
        var metadata = new[] { category, grade, $"数量 x{entry.count}" }.Where(value => !string.IsNullOrWhiteSpace(value));
        var metadataText = string.Join(" · ", metadata);
        if (_item_hover_meta is not null) _item_hover_meta.Text = metadataText;
        if (_item_hover_detail is not null)
        {
            var tags = StoragePanel.build_tags(item.tags, item.craft_data as MaterialCraftData);
            var effect = StoragePanel.build_item_usage_effect(item);
            _item_hover_detail.Text = $"词缀与功用\n{tags}\n\n使用效果\n{effect}";
        }
        size_item_hover_panel(metadataText);
        position_item_hover_panel();
        _item_hover_panel.Show();
        _item_hover_panel.MoveToFront();
    }

    private void configure_trade_slot(StorageItemSlot slot)
    {
        slot.CustomMinimumSize = new Vector2(TradeSlotWidth, TradeSlotBaseHeight);
        if (slot.GetNodeOrNull<Control>("Frame") is { } frame) frame.OffsetBottom = TradeSlotIconSize;
        if (slot.GetNodeOrNull<Label>("ItemName") is { } name)
        {
            name.OffsetTop = TradeSlotIconSize + 4;
            name.OffsetBottom = TradeSlotBaseHeight;
        }
    }

    private void size_item_hover_panel(string metadataText)
    {
        if (_item_hover_panel is null || _item_hover_detail is null) return;
        var viewport = GetViewportRect().Size;
        var panelWidth = Mathf.Max(1f, Mathf.Min(520f, viewport.X - 24f));
        var maxHeight = Mathf.Max(1f, viewport.Y - 24f);
        var contentWidth = Mathf.Max(1f, panelWidth - 34f);
        var headingWidth = Mathf.Max(1f, contentWidth - 68f);
        var detailText = _item_hover_detail.Text;
        var detailFont = viewport.X < 620f ? 13 : 15;
        var metaFont = viewport.X < 620f ? 12 : 14;
        var detailHeight = 0f;
        var totalHeight = 0f;

        for (; detailFont >= 9; detailFont--)
        {
            var titleLines = estimate_wrapped_lines(_item_hover_name?.Text ?? "", headingWidth, 21);
            var metaLines = estimate_wrapped_lines(metadataText, headingWidth, metaFont);
            var headerHeight = Mathf.Max(58f, titleLines * 29f + metaLines * (metaFont + 5f) + 5f);
            var detailLines = estimate_wrapped_lines(detailText, contentWidth, detailFont);
            var lineHeight = detailFont * 1.55f;
            // Extra room covers small differences in font metrics and automatic wrapping.
            detailHeight = (detailLines + 1) * lineHeight;
            totalHeight = headerHeight + detailHeight + 66f;
            if (totalHeight <= maxHeight) break;
        }

        apply_font(_item_hover_detail, detailFont, new Color("f1ead7"));
        if (_item_hover_meta is not null) apply_font(_item_hover_meta, metaFont, new Color("c8bd9d"));
        _item_hover_detail.CustomMinimumSize = new Vector2(0, detailHeight);
        _item_hover_panel.CustomMinimumSize = new Vector2(panelWidth, Mathf.Min(totalHeight, maxHeight));
        _item_hover_panel.Size = new Vector2(panelWidth, Mathf.Min(totalHeight, maxHeight));
    }

    private static int estimate_wrapped_lines(string text, float width, int fontSize)
    {
        var lines = 0;
        foreach (var line in text.Split('\n'))
        {
            if (line.Length == 0)
            {
                lines++;
                continue;
            }

            var lineCount = 1;
            var usedWidth = 0f;
            foreach (var character in line)
            {
                var glyphWidth = character >= 0x2E80 ? fontSize : fontSize * .58f;
                if (usedWidth > 0 && usedWidth + glyphWidth > width)
                {
                    lineCount++;
                    usedWidth = 0;
                }
                usedWidth += glyphWidth;
            }
            lines += lineCount;
        }
        return Math.Max(1, lines);
    }

    private void position_item_hover_panel()
    {
        if (_item_hover_panel is null) return;
        var viewport = GetViewportRect().Size;
        var mouse = GetViewport().GetMousePosition();
        var size = _item_hover_panel.Size;
        var pointer = mouse + new Vector2(16, 18);
        if (pointer.X + size.X > viewport.X - 12) pointer.X = mouse.X - size.X - 16;
        if (pointer.Y + size.Y > viewport.Y - 12) pointer.Y = mouse.Y - size.Y - 18;
        _item_hover_panel.Position = new Vector2(
            Mathf.Clamp(pointer.X, 12, Mathf.Max(12, viewport.X - size.X - 12)),
            Mathf.Clamp(pointer.Y, 12, Mathf.Max(12, viewport.Y - size.Y - 12)));
    }

    private void on_trade_slot_info_cleared(StorageItemSlot slot)
    {
        if (_hovered_trade_slot != slot) return;
        hide_item_hover();
    }

    private void hide_item_hover()
    {
        _hovered_trade_slot = null;
        _item_hover_panel?.Hide();
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

        entry.slot.CustomMinimumSize = new Vector2(TradeSlotWidth, 214);
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
        amount.OffsetTop = 164; amount.OffsetBottom = 183;
        entry.slot.AddChild(amount);
        entry.quantity_label = amount;

        var slider = new HSlider
        {
            MinValue = 1,
            MaxValue = entry.count,
            Step = 1,
            Value = entry.selected_count,
            TooltipText = "拖动选择本次交易的数量",
        };
        slider.AddThemeStyleboxOverride("grabber_area", panel_style(new Color("362817"), new Color("795e31"), 5));
        slider.AddThemeStyleboxOverride("slider", panel_style(new Color("e2c35f"), new Color("6c4d19"), 5));
        slider.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        slider.OffsetLeft = 9; slider.OffsetTop = 187; slider.OffsetRight = -9; slider.OffsetBottom = 208;
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
        var favor = Math.Max(0, _npc?.player_relationship ?? 0) * 18;
        var difference = player_value - npc_value;
        var favor_covers = difference < 0 && favor >= -difference;

        string attitude;
        var can_trade = false;
        if (ratio >= .45f) { attitude = "满意交易"; can_trade = true; }
        else if (ratio >= .10f) { attitude = "比较满意"; can_trade = true; }
        else if (ratio >= -.10f) { attitude = "能接受"; can_trade = true; }
        else if (ratio >= -.45f) { attitude = $"不太满意 · {(favor_covers ? "人情可抵" : "人情不足")}"; can_trade = favor_covers; }
        else { attitude = $"不满意 · {(favor_covers ? "人情可抵" : "人情不足")}"; can_trade = favor_covers; }

        var favor_text = difference < 0
            ? (favor_covers ? "人情足以抵消差价，对方愿意成交。" : "人情不足，无法抵消差价。")
            : "当前差价无需动用人情。";
        set_attitude(attitude, favor_text, can_trade, true);
    }

    private void set_attitude(string attitude, string favor_text, bool can_trade, bool has_selection)
    {
        if (_attitude_label is not null)
        {
            _attitude_label.Text = attitude;
            _attitude_label.AddThemeColorOverride("font_color", can_trade ? new Color("b9d2c0") : new Color("d9a28b"));
        }
        if (_favor_label is not null) _favor_label.Text = favor_text;
        if (_trade_button is not null)
        {
            _trade_button.Disabled = !can_trade;
            _trade_button.TooltipText = can_trade ? "" : (has_selection ? "对方不愿接受这笔交易" : "请选择双方物品");
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
            if (_floating_hint is not null) _floating_hint.Text = $"体力不足，完成交易需要 {WorldStaminaRules.InteractionCost:0.#} 点；当前 {_player.current_world_stamina:0.#}。";
            return;
        }

        var removed = new List<TradeEntry>();
        foreach (var entry in outgoing)
        {
            if (_player.remove_item_from_inventory(entry.path, entry.selected_count)) removed.Add(entry);
            else
            {
                foreach (var rollback in removed) _player.add_item_to_inventory(rollback.path, rollback.selected_count);
                return;
            }
        }

        var added = new List<TradeEntry>();
        foreach (var entry in incoming)
        {
            if (_player.add_item_to_inventory(entry.path, entry.selected_count)) added.Add(entry);
            else
            {
                foreach (var rollback in added) _player.remove_item_from_inventory(rollback.path, rollback.selected_count);
                foreach (var rollback in removed) _player.add_item_to_inventory(rollback.path, rollback.selected_count);
                return;
            }
        }

        if (!_player.try_spend_world_stamina(WorldStaminaRules.InteractionCost))
        {
            foreach (var rollback in added) _player.remove_item_from_inventory(rollback.path, rollback.selected_count);
            foreach (var rollback in removed) _player.add_item_to_inventory(rollback.path, rollback.selected_count);
            return;
        }
        foreach (var entry in outgoing) _npc.trade_inventory[entry.path] = _npc.trade_inventory.TryGetValue(entry.path, out var old) ? old.AsInt32() + entry.selected_count : entry.selected_count;
        foreach (var entry in incoming)
        {
            var remaining = _npc.trade_inventory.TryGetValue(entry.path, out var old) ? old.AsInt32() - entry.selected_count : 0;
            if (remaining <= 0) _npc.trade_inventory.Remove(entry.path);
            else _npc.trade_inventory[entry.path] = remaining;
        }
        _npc.player_relationship = Math.Min(100, _npc.player_relationship + 1);
        refresh_grids();
    }

    private void ensure_npc_offer()
    {
        if (_npc is null || _npc.trade_inventory.Count > 0) return;
        var seed = stable_seed(_npc.npc_id);
        for (var i = 0; i < 5; i++)
        {
            var path = DefaultNpcTradeItems[(int)((seed + (ulong)i * 7) % (ulong)DefaultNpcTradeItems.Length)];
            if (!ResourceLoader.Exists(path)) continue;
            _npc.trade_inventory[path] = 1 + (int)((seed + (ulong)i * 11) % 4);
        }
    }

    private ItemData? load_item(string path)
    {
        if (_item_cache.TryGetValue(path, out var item)) return item;
        item = string.IsNullOrWhiteSpace(path) || !ResourceLoader.Exists(path) ? null : ResourceLoader.Load<ItemData>(path);
        _item_cache[path] = item;
        return item;
    }

    private int item_value(TradeEntry entry)
    {
        var item = load_item(entry.path);
        if (item is null) return 0;
        var category_bonus = item.item_category switch
        {
            "秘籍" => 32,
            "装备" => 26,
            "丹药" => 20,
            "符箓" => 16,
            "器具" or "阵盘" => 18,
            _ => 8,
        };
        return Math.Max(8, 18 + item.grade_value * 8 + category_bonus) * Math.Max(1, entry.selected_count);
    }

    private void clear_grid(GridContainer grid)
    {
        foreach (var child in grid.GetChildren()) child.QueueFree();
    }

    private Label make_checkmark()
    {
        var label = make_label("✓", 62, new Color(0.94f, 0.86f, 0.52f, 0.92f));
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.MouseFilter = MouseFilterEnum.Ignore;
        label.AddThemeColorOverride("font_outline_color", new Color(.16f, .12f, .06f, .88f));
        label.AddThemeConstantOverride("outline_size", 6);
        label.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        label.OffsetBottom = TradeSlotIconSize;
        label.Visible = false;
        return label;
    }

    private Panel make_selection_frame()
    {
        var frame = new Panel { MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        frame.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        frame.OffsetBottom = TradeSlotIconSize;
        frame.AddThemeStyleboxOverride("panel", panel_style(new Color(1f, .9f, .5f, .10f), new Color("e8c967"), 4));
        return frame;
    }

    private Button make_button(string text, int size, Vector2 minimum)
    {
        var button = new Button { Text = text, CustomMinimumSize = minimum };
        apply_font(button, size, new Color("f1dfad"));
        button.AddThemeColorOverride("font_hover_color", new Color("fff4cc"));
        button.AddThemeColorOverride("font_disabled_color", new Color(.54f, .54f, .52f, .72f));
        button.AddThemeStyleboxOverride("normal", panel_style(new Color(.12f, .14f, .13f, .96f), new Color("9a7a3d"), 5));
        button.AddThemeStyleboxOverride("hover", panel_style(new Color(.21f, .25f, .22f, .98f), new Color("d3b86c"), 5));
        button.AddThemeStyleboxOverride("disabled", panel_style(new Color(.16f, .17f, .16f, .78f), new Color(.36f, .36f, .34f, .8f), 5));
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
