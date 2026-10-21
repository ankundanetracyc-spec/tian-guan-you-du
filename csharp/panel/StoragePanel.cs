using Godot;
using Godot.Collections;

/// <summary>玩家行囊：原生纸墨控件、物品悬停信息与单击操作，数据来自当前角色。</summary>
public partial class StoragePanel : Control
{
    private const string ArtifactFilter = "__artifacts__";
    private const int GridSlotCount = Character.InventorySlotCapacity;
    private const int GridMaximumColumns = 8;
    private const float GridHorizontalGap = 12f;
    // 30 格按八列四行展示，完整网格落在正文与页脚之间。
    private const float GridSlotHeight = 164f;
    private const float PopupWidth = 394f;

    private Character? _player;
    private StorageItemSlot? _active_slot;
    private StorageItemSlot? _layoutAnchorSlot;
    private ArtifactInstanceData? _active_artifact;
    private bool _pinned_selection, _pointer_over_detail, _inventory_mutation;
    private IInventoryItemUseHandler? _item_use_handler;
    private string _category_filter = "", _search_text = "";
    private int _sort_mode;
    private readonly System.Collections.Generic.Dictionary<string, ItemData> _cache = [];
    private readonly System.Collections.Generic.Dictionary<string, Button> _category_buttons = [];
    private readonly System.Collections.Generic.Dictionary<string, InventoryPaperFrame> _category_frames = [];
    private Godot.Timer? _detail_hide_timer;
    private sealed record InventoryEntry(int Position, string Path, ItemData? Item, int Count, ArtifactInstanceData? Artifact)
    {
        public string Name => Artifact is not null ? artifact_name(Artifact) : Item?.entity_name ?? "";
        public string Category => Artifact is not null ? "法宝" : Item is null ? "" : category_of(Item, Path);
    }

    public override void _Ready()
    {
        AddToGroup("storage_panel");
        AddToGroup("map_overlay_blocker");
        ZIndex = 20;
        MouseFilter = MouseFilterEnum.Stop;
        build_inventory_view();
        _detail_hide_timer = new Godot.Timer { OneShot = true, WaitTime = 0.2 };
        _detail_hide_timer.Timeout += on_detail_hide_timeout;
        AddChild(_detail_hide_timer);
        Resized += fit_inventory_canvas;
        GetViewport().SizeChanged += on_detail_viewport_resized;
        VisibilityChanged += on_visibility_changed;
        fit_inventory_canvas();
        filter_to_category("");
        Hide();
    }

    public override void _ExitTree()
    {
        if (_player is not null) _player.InventoryChanged -= on_player_inventory_changed;
        if (GetViewport() is { } viewport) viewport.SizeChanged -= on_detail_viewport_resized;
        bind_detail_layout_anchor(null);
    }

    public override void _Process(double delta)
    {
        if (Visible && GetViewport().GuiIsDragging()) clear_active_info();
    }

    public override void _Input(InputEvent @event)
    {
        if (!Visible) return;
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            if (_pinned_selection) clear_active_info();
            else hide_panel();
            GetViewport().SetInputAsHandled();
        }
        else if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mouse
            && _detail_panel?.Visible == true
            && !_detail_panel.GetGlobalRect().HasPoint(mouse.Position)
            && (_grid is null || !_grid.GetChildren().OfType<StorageItemSlot>().Any(slot => !slot.is_empty && slot.GetGlobalRect().HasPoint(mouse.Position))))
        {
            clear_active_info();
        }
    }

    public Rect2 get_ui_rect() => _canvas?.GetGlobalRect() ?? GetGlobalRect();
    public void bind_player(Character? player)
    {
        if (_player is not null) _player.InventoryChanged -= on_player_inventory_changed;
        _player = player;
        if (_player is not null) _player.InventoryChanged += on_player_inventory_changed;
        refresh_slots();
    }
    public void show_panel() { Show(); fit_inventory_canvas(); refresh_slots(); }
    public void hide_panel() { HoverInfoManager.HideAll(); clear_active_info(); Hide(); }
    public void toggle_panel() { if (Visible) hide_panel(); else show_panel(); }
    private void on_visibility_changed() { if (!Visible) clear_active_info(); }

    public void filter_to_category(string category)
    {
        _category_filter = category == ArtifactFilter ? ArtifactFilter : normalize_category(category);
        foreach (var pair in _category_buttons)
        {
            var selected = pair.Key == _category_filter;
            pair.Value.ButtonPressed = selected;
            style_category_text(pair.Value, selected);
            if (_category_frames.TryGetValue(pair.Key, out var frame)) frame.Visible = selected;
        }
        if (_grid_scroll is not null) _grid_scroll.ScrollVertical = 0;
        refresh_slots();
    }

    private List<InventoryEntry> inventory_entries()
    {
        var entries = new List<InventoryEntry>();
        if (_player is null) return entries;
        foreach (var entry in _player.get_inventory_snapshot())
        {
            var path = entry.GetValueOrDefault("item_path", "").AsString();
            if (load_item_data(path) is { } item)
                entries.Add(new InventoryEntry(entry.GetValueOrDefault("slot_index", -1).AsInt32(), path, item,
                    entry.GetValueOrDefault("count", 0).AsInt32(), null));
        }
        foreach (var artifact in _player.get_artifact_items())
            if (artifact is not null)
                entries.Add(new InventoryEntry(_player.get_artifact_inventory_slot_index(artifact.instance_id), "", null, 1, artifact));
        return entries;
    }

    private List<InventoryEntry> ordered_entries(IEnumerable<InventoryEntry> entries) => _sort_mode switch
    {
        1 => entries.OrderByDescending(entry => entry.Count).ThenBy(entry => entry.Name, StringComparer.CurrentCulture).ThenBy(entry => entry.Position).ToList(),
        2 => entries.OrderBy(entry => entry.Category, StringComparer.CurrentCulture).ThenBy(entry => entry.Name, StringComparer.CurrentCulture).ThenBy(entry => entry.Position).ToList(),
        3 => entries.OrderBy(entry => entry.Position).ToList(),
        _ => entries.OrderBy(entry => entry.Name, StringComparer.CurrentCulture).ThenBy(entry => entry.Position).ToList()
    };

    public void refresh_slots()
    {
        if (_grid is null) return;
        HoverInfoManager.HideAll();
        clear_active_info();
        foreach (var child in _grid.GetChildren()) { _grid.RemoveChild(child); child.QueueFree(); }
        var all = inventory_entries();
        var entries = ordered_entries(all.Where(entry =>
            (_category_filter == "" || (_category_filter == ArtifactFilter ? entry.Artifact is not null : entry.Artifact is null && entry.Category == _category_filter))
            && (_search_text.Length == 0 || entry.Name.Contains(_search_text, StringComparison.OrdinalIgnoreCase))));
        var physicalOrder = _sort_mode == 3 && _category_filter == "" && _search_text.Length == 0;
        var byPosition = physicalOrder ? entries.ToDictionary(entry => entry.Position) : null;
        var scene = ResourceLoader.Load<PackedScene>("res://tscn/panel/StorageItemSlot.tscn");
        if (scene is null) return;
        var slotWidth = (_grid_scroll!.Size.X - GridHorizontalGap * (GridMaximumColumns - 1) - 20f) / GridMaximumColumns;
        for (var index = 0; index < GridSlotCount; index++)
        {
            InventoryEntry? entry = null;
            if (byPosition is not null) byPosition.TryGetValue(index, out entry);
            else if (index < entries.Count) entry = entries[index];
            var slot = scene.Instantiate<StorageItemSlot>();
            slot.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            slot.inventory_reorder_enabled = physicalOrder;
            _grid.AddChild(slot);
            slot.configure_inventory_layout(Mathf.Max(128f, slotWidth), GridSlotHeight);
            if (entry?.Artifact is { } artifact) { slot.set_artifact(artifact); slot.slot_index = entry.Position; }
            else if (entry?.Item is { } item) slot.set_item(entry.Position, entry.Path, item, entry.Count);
            else slot.set_empty(physicalOrder ? index : -1);
            slot.info_requested += on_slot_info_requested;
            slot.info_cleared += on_slot_info_cleared;
            slot.slot_moved += on_slot_moved;
            slot.MouseEntered += on_inventory_slot_mouse_entered;
            if (!slot.is_empty)
                HoverInfoManager.SetInfo(slot, build_hover_content(slot));
        }
        if (_summary is not null) _summary.Text = entries.Count == all.Count
            ? $"已用 {all.Count} / {GridSlotCount} 格" : $"找到 {entries.Count} 项 · 已用 {all.Count} / {GridSlotCount} 格";
        if (_empty is not null)
        {
            _empty.Visible = entries.Count == 0;
            _empty.Text = _search_text.Length > 0 ? "未找到匹配物品" : "行囊中暂无此类物品";
        }
    }

    private void organize_inventory()
    {
        if (_player is null) return;
        var ordered = ordered_entries(inventory_entries());
        var positions = ordered.Select(entry => entry.Position).ToArray();
        _inventory_mutation = true;
        try
        {
            for (var index = 0; index < positions.Length && index < GridSlotCount; index++)
            {
                var source = positions[index];
                if (source == index) continue;
                if (!_player.move_inventory_slot(source, index)) continue;
                for (var other = index + 1; other < positions.Length; other++)
                    if (positions[other] == index) { positions[other] = source; break; }
                positions[index] = index;
            }
        }
        finally { _inventory_mutation = false; refresh_slots(); }
    }

    private void on_slot_moved(int fromPosition, int toPosition)
    {
        if (_player is not null && _player.move_inventory_slot(fromPosition, toPosition)) refresh_slots();
    }
    private void on_inventory_slot_mouse_entered()
    {
        if (!_pinned_selection && _detail_panel?.Visible == true) clear_active_info(false);
    }
    private void on_player_inventory_changed(Character _) { if (Visible && !_inventory_mutation) refresh_slots(); }

    private bool can_act_on(StorageItemSlot slot)
    {
        if (_player is null) return false;
        if (slot.artifact_data is { } artifact)
            return _player.is_artifact_equipped(artifact.instance_id)
                || (_player.entity_data is HumanData data && data.artifact_slot_count > data.equipped_artifact_ids.Count
                    && (artifact.max_durability <= 0 || artifact.current_durability > 0));
        return load_item_data(slot.item_path) is { } item
            && (item is EquipmentData or PortraitClothingData || _item_use_handler?.CanUse(_player, item, slot.item_count) == true);
    }

    private void on_slot_info_requested(StorageItemSlot slot, bool immediate)
    {
        if (slot.is_empty) { if (immediate) clear_active_info(); return; }
        if (!immediate)
        {
            if (_pinned_selection) return;
            clear_active_info(false);
            return;
        }

        HoverInfoManager.HideAll();
        if (immediate)
        {
            if (_pinned_selection && _active_slot == slot) { clear_active_info(); return; }
            _pinned_selection = can_act_on(slot);
        }
        _detail_hide_timer?.Stop();
        _active_slot = slot;
        _active_artifact = slot.artifact_data;
        bind_detail_layout_anchor(slot);
        update_slot_selection_visuals();
        if (slot.artifact_data is { } artifact) fill_artifact_info(artifact);
        else if (load_item_data(slot.item_path) is { } item) fill_info(item, slot.item_count);
        else { clear_active_info(); return; }
        show_info_content(slot);
    }

    private void on_slot_info_cleared(StorageItemSlot slot)
    {
        if (!_pinned_selection && _active_slot == slot) _detail_hide_timer?.Start();
    }
    private void on_detail_mouse_entered() { _pointer_over_detail = true; _detail_hide_timer?.Stop(); }
    private void on_detail_mouse_exited() { _pointer_over_detail = false; if (!_pinned_selection && _detail_panel?.Visible == true) _detail_hide_timer?.Start(); }
    private void on_detail_hide_timeout()
    {
        if (_pinned_selection || _pointer_over_detail) return;
        if (_detail_panel?.Visible == true && _detail_panel.GetGlobalRect().HasPoint(GetGlobalMousePosition())) return;
        if (_active_slot is not null && _active_slot.GetGlobalRect().HasPoint(GetGlobalMousePosition())) return;
        clear_active_info();
    }
    private void update_slot_selection_visuals()
    {
        if (_grid is null) return;
        foreach (var slot in _grid.GetChildren().OfType<StorageItemSlot>()) slot.set_inventory_selected(slot == _active_slot);
    }
    private void clear_active_info() => clear_active_info(true);
    private void clear_active_info(bool hideHover)
    {
        _detail_hide_timer?.Stop();
        if (hideHover) HoverInfoManager.HideAll();
        _pointer_over_detail = false;
        bind_detail_layout_anchor(null);
        _active_slot = null; _active_artifact = null; _pinned_selection = false;
        _detail_panel?.Hide();
        update_slot_selection_visuals();
        _action_button?.Hide(); _cancel_button?.Hide(); _action_row?.Hide();
    }
    private void show_info_content(StorageItemSlot slot)
    {
        if (_detail_panel is null) return;
        _detail_panel.Show();
        _info_icon!.Visible = _info_icon.Texture is not null;
        foreach (var label in new[] { _info_name, _info_category, _info_grade, _info_count, _info_effect_title })
            if (label is not null) label.Visible = !string.IsNullOrWhiteSpace(label.Text);
        if (_info_tags is not null) _info_tags.Visible = !string.IsNullOrWhiteSpace(_info_tags.Text);
        if (_info_desc is not null) _info_desc.Visible = !string.IsNullOrWhiteSpace(_info_desc.Text);
        if (_action_row is not null) _action_row.Visible = _action_button?.Visible == true;
        layout_detail_popup(slot);
    }

    private HoverInfoManager.HoverInfoContent build_hover_content(StorageItemSlot slot)
    {
        if (slot.artifact_data is { } artifact)
        {
            var name = artifact_name(artifact);
            var path = artifact.form?.icon_path ?? "";
            var icon = string.IsNullOrWhiteSpace(path) || !ResourceLoader.Exists(path) ? null : ResourceLoader.Load<Texture2D>(path);
            var subtitle = $"法宝 · {(string.IsNullOrWhiteSpace(artifact.ritual_mode) ? "未定祭法" : artifact.ritual_mode)} · {artifact.artifact_tier} · {(string.IsNullOrWhiteSpace(artifact.element) ? "五行未定" : artifact.element)}";
            var durability = artifact.max_durability > 0
                ? $"耐久 {Math.Max(0, artifact.current_durability)}/{artifact.max_durability}　灵识（外祭在场）{(artifact.ritual_mode == "内炼" ? 0 : Math.Max(0, artifact.spirit_cost))}"
                : "耐久未定　灵识 " + (artifact.ritual_mode == "内炼" ? 0 : Math.Max(0, artifact.spirit_cost));
            var utility = string.IsNullOrWhiteSpace(artifact.utility_id) ? "主功用：未定" : $"主功用：{artifact.utility_id}　品级：{Math.Max(0, artifact.utility_grade)}";
            var binding = string.IsNullOrWhiteSpace(artifact.bound_basic_card_path) ? "" : $"\n绑定基础牌：{artifact.bound_basic_card_path}";
            return new HoverInfoManager.HoverInfoContent(name,
                $"{durability}\n{utility}\n实例 ID：{artifact.instance_id}{binding}", icon, subtitle);
        }

        var item = load_item_data(slot.item_path);
        if (item is null) return new HoverInfoManager.HoverInfoContent("", "");
        var category = category_of(item, slot.item_path);
        if (!string.IsNullOrWhiteSpace(item.item_subcategory)) category = string.IsNullOrWhiteSpace(category) ? item.item_subcategory : $"{category} · {item.item_subcategory}";
        var grade = item.craft_data is MaterialCraftData material
            ? $"品阶：{material.material_grade_name()}（{material.material_score():0.##}）" : item.get_grade_string();
        var subtitleText = string.Join(" · ", new[] { category, grade, $"数量　{slot.item_count}" }.Where(value => !string.IsNullOrWhiteSpace(value)));
        var tags = build_tags(item.tags, item.craft_data as MaterialCraftData);
        var effect = build_item_usage_effect(item);
        var itemIcon = string.IsNullOrWhiteSpace(item.icon_path) || !ResourceLoader.Exists(item.icon_path) ? null : ResourceLoader.Load<Texture2D>(item.icon_path);
        return new HoverInfoManager.HoverInfoContent(item.entity_name,
            $"词缀与功用\n{tags}\n\n使用效果\n{effect}", itemIcon, subtitleText);
    }

    private void bind_detail_layout_anchor(StorageItemSlot? slot)
    {
        if (_layoutAnchorSlot == slot) return;
        if (_layoutAnchorSlot is not null && GodotObject.IsInstanceValid(_layoutAnchorSlot))
            _layoutAnchorSlot.ItemRectChanged -= on_detail_anchor_rect_changed;
        _layoutAnchorSlot = slot;
        if (_layoutAnchorSlot is not null)
            _layoutAnchorSlot.ItemRectChanged += on_detail_anchor_rect_changed;
    }

    private void on_detail_anchor_rect_changed()
    {
        if (_detail_panel?.Visible == true && _active_slot is not null) layout_detail_popup(_active_slot);
    }

    private void on_detail_viewport_resized()
    {
        fit_inventory_canvas();
        if (_active_slot is not null && _detail_panel?.Visible == true)
            CallDeferred(MethodName.layout_active_detail_deferred);
    }

    private void layout_active_detail_deferred()
    {
        if (_active_slot is not null && _detail_panel?.Visible == true) layout_detail_popup(_active_slot);
    }

    private void fill_info(ItemData item, int count)
    {
        _info_name!.Text = item.entity_name;
        _info_icon!.Texture = string.IsNullOrWhiteSpace(item.icon_path) || !ResourceLoader.Exists(item.icon_path) ? null : ResourceLoader.Load<Texture2D>(item.icon_path);
        var category = category_of(item, _active_slot?.item_path ?? "");
        if (!string.IsNullOrWhiteSpace(item.item_subcategory)) category = string.IsNullOrWhiteSpace(category) ? item.item_subcategory : $"{category} · {item.item_subcategory}";
        _info_category!.Text = category;
        _info_grade!.Text = item.craft_data is MaterialCraftData material
            ? $"品阶：{material.material_grade_name()}（{material.material_score():0.##}）" : item.get_grade_string();
        _info_count!.Text = $"数量　{count}";
        _info_tags!.Text = build_tags(item.tags, item.craft_data as MaterialCraftData);
        _info_effect_title!.Text = "使用效果";
        _info_desc!.Text = build_item_usage_effect(item);
        configure_item_actions(item);
    }
    private void fill_artifact_info(ArtifactInstanceData artifact)
    {
        _info_name!.Text = artifact_name(artifact);
        var path = artifact.form?.icon_path ?? "";
        _info_icon!.Texture = string.IsNullOrWhiteSpace(path) || !ResourceLoader.Exists(path) ? null : ResourceLoader.Load<Texture2D>(path);
        _info_category!.Text = $"法宝 · {(string.IsNullOrWhiteSpace(artifact.ritual_mode) ? "未定祭法" : artifact.ritual_mode)}";
        _info_grade!.Text = $"{artifact.artifact_tier} · {(string.IsNullOrWhiteSpace(artifact.element) ? "五行未定" : artifact.element)}";
        _info_count!.Text = artifact.max_durability > 0
            ? $"耐久 {Math.Max(0, artifact.current_durability)}/{artifact.max_durability}　灵识（外祭在场）{(artifact.ritual_mode == "内炼" ? 0 : Math.Max(0, artifact.spirit_cost))}"
            : "耐久未定　灵识 " + (artifact.ritual_mode == "内炼" ? 0 : Math.Max(0, artifact.spirit_cost));
        _info_tags!.Text = string.IsNullOrWhiteSpace(artifact.utility_id) ? "主功用：未定" : $"主功用：{artifact.utility_id}　品级：{Math.Max(0, artifact.utility_grade)}";
        _info_effect_title!.Text = "法宝信息";
        _info_desc!.Text = $"实例 ID：{artifact.instance_id}\n\n法宝属于玩家行囊中的唯一实例，不与普通物品堆叠。\n{(string.IsNullOrWhiteSpace(artifact.bound_basic_card_path) ? "" : "绑定基础牌：" + artifact.bound_basic_card_path)}";
        _action_button!.Visible = _cancel_button!.Visible = _pinned_selection;
        var equipped = _player?.is_artifact_equipped(artifact.instance_id) == true;
        _action_button.Text = equipped ? "卸下" : "使用";
        _action_button.Disabled = false;
        HoverInfoManager.SetTooltip(_action_button, "");
    }
    private void configure_item_actions(ItemData item)
    {
        _action_button?.Hide(); _cancel_button?.Hide();
        if (!_pinned_selection || _player is null || _active_slot is null || _action_button is null || _cancel_button is null) return;
        if (!can_act_on(_active_slot)) { _pinned_selection = false; return; }
        HoverInfoManager.SetTooltip(_action_button, "");
        bool clothing = item is PortraitClothingData;
        bool equipped = _player.is_item_equipped(_active_slot.item_path);
        _action_button.Text = clothing ? equipped ? "已穿着" : "穿上" : item is EquipmentData && equipped ? "卸下" : "使用";
        _action_button.Disabled = clothing && equipped;
        _action_button.Show(); _cancel_button.Show();
    }
    private void on_action_pressed()
    {
        if (_player is null) return;
        string selectedPath = _active_slot?.item_path ?? "";
        bool clothingAction = !string.IsNullOrWhiteSpace(selectedPath) && load_item_data(selectedPath) is PortraitClothingData;
        bool changed;
        if (_active_artifact is not null)
            changed = _player.is_artifact_equipped(_active_artifact.instance_id)
                ? _player.unequip_artifact(_active_artifact.instance_id) : _player.equip_artifact(_active_artifact.instance_id);
        else if (_active_slot is not null && load_item_data(_active_slot.item_path) is PortraitClothingData)
            changed = _player.equip_inventory_item(_active_slot.item_path, "portrait_clothing");
        else if (_active_slot is not null && load_item_data(_active_slot.item_path) is EquipmentData)
            changed = _player.is_item_equipped(_active_slot.item_path)
                ? _player.unequip_inventory_item(_active_slot.item_path) : _player.equip_inventory_item(_active_slot.item_path);
        else if (_active_slot is { } slot && load_item_data(slot.item_path) is { } item && _item_use_handler is { } handler && handler.CanUse(_player, item, slot.item_count))
            changed = handler.TryUse(_player, item, slot.item_count);
        else changed = false;
        if (changed)
        {
            if (clothingAction)
                GetTree().CallGroup("human_info_panel", "refresh_after_clothing_item");
            refresh_slots();
        }
    }
    public void set_item_use_handler(IInventoryItemUseHandler? handler)
    {
        _item_use_handler = handler;
        if (_pinned_selection && _active_slot is not null && _active_artifact is null && load_item_data(_active_slot.item_path) is { } item)
        {
            configure_item_actions(item);
            show_info_content(_active_slot);
        }
    }
    private static string artifact_name(ArtifactInstanceData artifact) => string.IsNullOrWhiteSpace(artifact.artifact_name) ? artifact.form?.form_name ?? "未命名法宝" : artifact.artifact_name;
    internal static string build_tags(Array<string> tags,MaterialCraftData? material)
    {
        var lines=new List<string>();
        foreach(var dimension in new[]{"五行","质地","相性","功用"})
        {
            var values=get_display_tag_values(tags,dimension,material);
            if(dimension=="功用"&&material is not null)
            {
                var utilityGrades=material.utilities.Where(utility=>utility is not null&&!string.IsNullOrWhiteSpace(utility.utility_id)).Select(utility=>$"{utility.utility_id}（{utility.grade_name()}）").ToList();
                if(utilityGrades.Count>0)lines.Add($"功用：{string.Join("、",utilityGrades)}");
                else if(values.Count>0)lines.Add($"功用：{string.Join("、",values)}");
                continue;
            }
            if(values.Count>0)lines.Add($"{dimension}：{string.Join("、",values)}");
        }
        return lines.Count==0?"无词缀":string.Join("\n",lines);
    }
    internal static List<string> get_display_tag_values(Array<string> tags,string dimension,MaterialCraftData? material)
    {
        if(dimension=="质地"&&material is not null&&ItemAffixCatalog.is_material_texture(material.texture))
            return [material.texture];
        return ItemAffixCatalog.get_tag_values(tags,dimension).ToList();
    }
    internal static string build_item_usage_effect(ItemData item)
    {
        if (item is PortraitClothingData clothing)
            return $"立绘衣着 · {clothing.portrait_gender switch { "male" => "男修", "female" => "女修", _ => "男女通用" }}\n穿上后只更换人物立绘衣着，不增加属性，也不替换衣甲装备。";
        if(item is EquipmentData equipment)
        {
            var equipType=new[]{"武器","防具","饰品"}[Mathf.Clamp(equipment.equip_type,0,2)];
            var bonuses=$"{bonus_text(equipment.fixed_bonuses,false)}{bonus_text(equipment.mult_bonuses,true)}".Trim();
            return string.IsNullOrEmpty(bonuses)?$"{equipType}；未配置装备属性加成。":$"{equipType} · 装备后生效\n{bonuses}";
        }
        if(item.gongfa is GongfaResource gongfa)
        {
            var description=string.IsNullOrWhiteSpace(gongfa.description)?"暂无功法说明。":gongfa.description.Trim();
            var studyProgress=item.required_study_progress>0?item.required_study_progress:gongfa.required_study_progress;
            return $"{description}\n\n五行：{wuxing_name((int)gongfa.wuxing)}\n境界要求：{(string.IsNullOrWhiteSpace(gongfa.required_realm)?"无":gongfa.required_realm)}\n研读需求：{studyProgress:0.0} 研读值";
        }
        if(!string.IsNullOrWhiteSpace(item.battle_skill_path))
        {
            if(!ResourceLoader.Exists(item.battle_skill_path))return "战斗效果资源未找到。";
            var skill=ResourceLoader.Load<ActiveSkillData>(item.battle_skill_path);
            if(skill is null)return "战斗效果资源无法读取。";
            return CardPresentation.Underline(CardPresentation.Face(skill,null));
        }
        if(item.item_category=="材料"||item.craft_data is MaterialCraftData)
            return "用于材料匹配与制作；词缀描述材料性质，不直接触发成品效果。";
        return item.usage_type switch
        {
            0=>"世界内使用；具体效果尚未配置说明。",
            1=>"战斗中使用；具体效果尚未配置说明。",
            2=>"世界与战斗中均可使用；具体效果尚未配置说明。",
            _=>"暂无使用效果说明。"
        };
    }
    private static string bonus_text(Godot.Collections.Dictionary bonuses,bool percent) { var lines=new List<string>();foreach(var key in bonuses.Keys){var v=bonuses[key].AsSingle();lines.Add($"{key.AsString()} {(v>=0?"+":"")}{(percent?(v*100).ToString("0")+"%":v.ToString("0"))}");}return lines.Count==0?"":string.Join("\n",lines)+"\n"; }
    private ItemData? load_item_data(string path){if(string.IsNullOrWhiteSpace(path))return null;if(_cache.TryGetValue(path,out var item))return item;if(!ResourceLoader.Exists(path))return null;item=ResourceLoader.Load<ItemData>(path);if(!GongfaAvailability.IsItemEnabled(item))return null;if(item is not null)_cache[path]=item;return item;}
    internal static string category_of(ItemData item,string path){var c=normalize_category(item.item_category);if(c!="")return c;return item.item_subcategory is "矿石" or "草药" or "粮食" or "纤维" or "兽材" or "灵石"?"材料":"";}
    private static string normalize_category(string value)=>value.Trim() switch {"" or "全部"=>"","材料" or "粮食" or "灵石"=>"材料","装备"=>"装备","秘籍"=>"秘籍","阵盘"=>"阵盘","器具"=>"器具","符箓"=>"符箓","丹药"=>"丹药","灵膳"=>"灵膳",var other=>other};
    private static string wuxing_name(int value)=>value switch {0=>"无要求（无灵根可学）",1=>"金",2=>"木",3=>"水",4=>"火",5=>"土",31=>"金木水火土",32=>"通用（有灵根即可）",_=>"复合灵根"};
}
