using Godot;
using Godot.Collections;

/// <summary>玩家行囊：普通物品与唯一法宝都从当前角色数据读取，不连接共享仓库。</summary>
public partial class StoragePanel : Control
{
    private const string ArtifactFilter = "__artifacts__";
    private const int GridSlotCount = Character.InventorySlotCapacity;
    private const int GridMaximumColumns = 8;
    private const float GridSlotWidth = 128f;
    private const float GridSlotHeight = 168f;
    private const float GridHorizontalGap = 12f;
    [Signal] public delegate void secret_book_requestedEventHandler(string item_path);

    private Character? _player;
    private StorageItemSlot? _active_slot;
    private ArtifactInstanceData? _active_artifact;
    private string _category_filter = "";
    private readonly System.Collections.Generic.Dictionary<string, ItemData> _cache = [];
    private readonly System.Collections.Generic.Dictionary<string, string> _category_button_labels = [];
    private Label? _title, _empty, _summary, _info_name, _info_category, _info_grade, _info_count, _info_effect_title;
    private TextureRect? _info_icon;
    private readonly System.Collections.Generic.Dictionary<string, Button> _category_buttons = [];
    private Button? _action_button;
    private GridContainer? _grid, _empty_grid;
    private PanelContainer? _detail_panel;
    private RichTextLabel? _info_tags, _info_desc;
    private StyleBoxFlat? _empty_slot_style;

    public override void _Ready()
    {
        AddToGroup("map_overlay_blocker"); ZIndex = 20; MouseFilter = MouseFilterEnum.Stop;
        _empty=GetNodeOrNull<Label>("Window/Margin/VBox/Body/BodyMargin/Workspace/Stack/EmptyLabel"); _summary=GetNodeOrNull<Label>("Window/Margin/VBox/Header/SummaryLabel"); _grid=GetNodeOrNull<GridContainer>("Window/Margin/VBox/Body/BodyMargin/Workspace/Stack/Scroll/SlotGrid"); _empty_grid=GetNodeOrNull<GridContainer>("Window/Margin/VBox/Body/BodyMargin/Workspace/Stack/EmptySlotGrid");
        _detail_panel=GetNodeOrNull<PanelContainer>("Window/Margin/VBox/Body/BodyMargin/Workspace/DetailPanel"); _info_icon=GetNodeOrNull<TextureRect>("Window/Margin/VBox/Body/BodyMargin/Workspace/DetailPanel/PopupMargin/PopupVBox/InfoIcon"); _info_name=GetNodeOrNull<Label>("Window/Margin/VBox/Body/BodyMargin/Workspace/DetailPanel/PopupMargin/PopupVBox/InfoName"); _info_category=GetNodeOrNull<Label>("Window/Margin/VBox/Body/BodyMargin/Workspace/DetailPanel/PopupMargin/PopupVBox/InfoCategory"); _info_grade=GetNodeOrNull<Label>("Window/Margin/VBox/Body/BodyMargin/Workspace/DetailPanel/PopupMargin/PopupVBox/InfoGrade"); _info_count=GetNodeOrNull<Label>("Window/Margin/VBox/Body/BodyMargin/Workspace/DetailPanel/PopupMargin/PopupVBox/InfoCount"); _info_tags=GetNodeOrNull<RichTextLabel>("Window/Margin/VBox/Body/BodyMargin/Workspace/DetailPanel/PopupMargin/PopupVBox/InfoTags"); _info_effect_title=GetNodeOrNull<Label>("Window/Margin/VBox/Body/BodyMargin/Workspace/DetailPanel/PopupMargin/PopupVBox/InfoEffectTitle"); _info_desc=GetNodeOrNull<RichTextLabel>("Window/Margin/VBox/Body/BodyMargin/Workspace/DetailPanel/PopupMargin/PopupVBox/InfoDesc");
        _action_button=GetNodeOrNull<Button>("Window/Margin/VBox/Body/BodyMargin/Workspace/DetailPanel/PopupMargin/PopupVBox/ActionButton");
        if (_summary is not null) _summary.Text = "";
        var close_button = GetNodeOrNull<Button>("Window/Margin/VBox/Header/CloseButton");
        if (close_button is not null) close_button.Pressed += hide_panel;
        register_category_button("", "Rail/RailMargin/VBox/AllButton"); register_category_button("材料", "Rail/RailMargin/VBox/MaterialButton"); register_category_button("装备", "Rail/RailMargin/VBox/EquipmentButton"); register_category_button("秘籍", "Rail/RailMargin/VBox/SecretButton"); register_category_button("阵盘", "Rail/RailMargin/VBox/FormationButton"); register_category_button("器具", "Rail/RailMargin/VBox/ToolButton"); register_category_button("符箓", "Rail/RailMargin/VBox/TalismanButton"); register_category_button("丹药", "Rail/RailMargin/VBox/PillButton"); register_category_button("灵膳", "Rail/RailMargin/VBox/FoodButton"); register_category_button(ArtifactFilter, "Rail/RailMargin/VBox/ArtifactButton");
        if (_action_button is not null) { _action_button.Pressed += on_action_pressed; _action_button.Hide(); }
        populate_empty_grid();
        filter_to_category("");
        Hide();
    }

    public override void _Process(double delta)
    {
        if (_grid is null || _empty_grid is null || _grid.GetParent() is not Control scroll) return;
        var availableWidth = scroll.Size.X;
        if (availableWidth <= 1f) return;
        var columns = Mathf.Clamp(Mathf.FloorToInt((availableWidth + GridHorizontalGap) / (GridSlotWidth + GridHorizontalGap)), 1, GridMaximumColumns);
        if (_grid.Columns == columns && _empty_grid.Columns == columns) return;
        _grid.Columns = columns;
        _empty_grid.Columns = columns;
    }

    public Rect2 get_ui_rect() => GetNodeOrNull<Control>("Window")?.GetGlobalRect() ?? GetGlobalRect();
    public void bind_player(Character? player)
    {
        if (_player is not null) _player.InventoryChanged -= on_player_inventory_changed;
        _player = player;
        if (_player is not null) _player.InventoryChanged += on_player_inventory_changed;
        refresh_slots();
    }
    public void show_panel() { Show(); refresh_slots(); }
    public void hide_panel() { Hide(); clear_active_info(); }
    public void toggle_panel() { if (Visible) hide_panel(); else show_panel(); }
    public void filter_to_category(string category)
    {
        _category_filter=category == ArtifactFilter ? ArtifactFilter : normalize_category(category);
        foreach (var pair in _category_buttons)
        {
            var selected = pair.Key == _category_filter;
            pair.Value.ButtonPressed = selected;
            pair.Value.Text = (selected ? "● " : "　") + _category_button_labels[pair.Key];
            pair.Value.AddThemeColorOverride("font_color", selected ? new Color(1f, 0.9f, 0.58f, 1f) : new Color(0.9f, 0.85f, 0.7f, 1f));
        }
        refresh_slots();
    }

    public void refresh_slots()
    {
        if (_grid is null) return;
        clear_active_info();
        foreach(var child in _grid.GetChildren()) child.QueueFree();
        if (_player is null)
        {
            if (_empty is not null) _empty.Visible=false;
            return;
        }
        var snapshot=_player.get_inventory_snapshot();
        var entries=new List<(Dictionary entry,ItemData item,int position)>();
        foreach(var entry in snapshot)
        {
            var path=entry.GetValueOrDefault("item_path","").AsString(); var item=load_item_data(path);
            if(item is not null && _category_filter != ArtifactFilter && (_category_filter=="" || category_of(item,path)==_category_filter)) entries.Add((entry,item,entry.GetValueOrDefault("slot_index",-1).AsInt32()));
        }
        var artifacts = _category_filter == ArtifactFilter || _category_filter == "" ? _player.get_artifact_items().ToList() : new List<ArtifactInstanceData>();
        artifacts.RemoveAll(item => item is null);
        if(_empty is not null)_empty.Visible=false;
        var scene=ResourceLoader.Load<PackedScene>("res://tscn/panel/StorageItemSlot.tscn"); if(scene is null)return;
        var normalAt = entries.ToDictionary(value => value.position);
        var artifactsAt = artifacts.ToDictionary(item => _player.get_artifact_inventory_slot_index(item.instance_id));
        for (var position = 0; position < GridSlotCount; position++)
        {
            var slot=scene.Instantiate<StorageItemSlot>();
            slot.inventory_reorder_enabled=true;
            slot.CustomMinimumSize = new Vector2(GridSlotWidth, GridSlotHeight);
            slot.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _grid.AddChild(slot);
            if (normalAt.TryGetValue(position,out var normal))
            {
                var entry=normal.entry;
                slot.set_item(position,entry.GetValueOrDefault("item_path","").AsString(),normal.item,entry.GetValueOrDefault("count",0).AsInt32());
                slot.info_requested+=on_slot_info_requested;
            }
            else if (artifactsAt.TryGetValue(position,out var artifact))
            {
                slot.set_artifact(artifact);
                slot.slot_index=position;
                slot.info_requested+=on_slot_info_requested;
            }
            else slot.set_empty(position);
            slot.slot_moved+=on_slot_moved;
        }
    }

    private void on_slot_moved(int fromPosition,int toPosition)
    {
        if (_player is not null && _player.move_inventory_slot(fromPosition,toPosition)) refresh_slots();
    }

    private void on_player_inventory_changed(Character _) { if (Visible) refresh_slots(); }
    private void register_category_button(string category, NodePath node_path)
    {
        var button = GetNodeOrNull<Button>(node_path);
        if (button is null) return;
        button.ToggleMode = true;
        _category_buttons[category] = button;
        _category_button_labels[category] = button.Text.Trim();
        button.Pressed += () => filter_to_category(category);
    }
    private void populate_empty_grid()
    {
        if (_empty_grid is null) return;
        foreach (var child in _empty_grid.GetChildren()) child.QueueFree();
        _empty_slot_style ??= new StyleBoxFlat
        {
            BgColor = new Color(0.73f, 0.67f, 0.55f, 0.78f),
            BorderColor = new Color(0.30f, 0.25f, 0.16f, 0.82f),
            BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomRight = 4, CornerRadiusBottomLeft = 4
        };
        for (var i = 0; i < GridSlotCount; i++)
        {
            var cell = new Control
            {
                CustomMinimumSize = new Vector2(GridSlotWidth, GridSlotHeight),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                MouseFilter = MouseFilterEnum.Ignore
            };
            var frame = new Panel { CustomMinimumSize = new Vector2(GridSlotWidth, GridSlotWidth), MouseFilter = MouseFilterEnum.Ignore };
            frame.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop);
            frame.OffsetLeft = -GridSlotWidth * .5f;
            frame.OffsetTop = 0;
            frame.OffsetRight = GridSlotWidth * .5f;
            frame.OffsetBottom = GridSlotWidth;
            frame.AddThemeStyleboxOverride("panel", _empty_slot_style);
            cell.AddChild(frame);
            _empty_grid.AddChild(cell);
        }
    }
    private void on_slot_info_requested(StorageItemSlot slot,bool immediate)
    {
        if (slot.is_artifact)
        {
            if (slot.artifact_data is null) return;
            _active_slot=slot; _active_artifact=slot.artifact_data; fill_artifact_info(slot.artifact_data); return;
        }
        var item=load_item_data(slot.item_path); if(item is null)return;
        _active_slot=slot; _active_artifact=null; fill_info(item,slot.item_count);
    }
    private void clear_active_info()
    {
        _active_slot=null; _active_artifact=null; _detail_panel?.Hide();
        if (_info_icon is not null) { _info_icon.Texture=null; _info_icon.Hide(); }
        if (_info_name is not null) { _info_name.Text=""; _info_name.Hide(); }
        if (_info_category is not null) { _info_category.Text=""; _info_category.Hide(); }
        if (_info_grade is not null) { _info_grade.Text=""; _info_grade.Hide(); }
        if (_info_count is not null) { _info_count.Text=""; _info_count.Hide(); }
        if (_info_tags is not null) { _info_tags.Text=""; _info_tags.Hide(); }
        if (_info_effect_title is not null) _info_effect_title.Hide();
        if (_info_desc is not null) { _info_desc.Text=""; _info_desc.Hide(); }
        _action_button?.Hide();
    }
    private void show_info_content()
    {
        _detail_panel?.Show(); _info_icon?.Show(); _info_name?.Show(); _info_category?.Show();
        _info_grade?.Show(); _info_count?.Show(); _info_tags?.Show(); _info_effect_title?.Show(); _info_desc?.Show();
    }
    private void fill_info(ItemData item,int count)
    {
        show_info_content();
        if(_info_name is not null)_info_name.Text=item.entity_name;
        if (_info_icon is not null) _info_icon.Texture=string.IsNullOrWhiteSpace(item.icon_path) || !ResourceLoader.Exists(item.icon_path) ? null : ResourceLoader.Load<Texture2D>(item.icon_path);
        var category=category_of(item,_active_slot?.item_path??"");
        if(!string.IsNullOrWhiteSpace(item.item_subcategory))category=string.IsNullOrWhiteSpace(category)?item.item_subcategory:$"{category} / {item.item_subcategory}";
        if(_info_category is not null)_info_category.Text=category;
        if(_info_grade is not null)_info_grade.Text=item.craft_data is MaterialCraftData material ? $"品阶：{material.material_grade_name()}（{material.material_score():0.##}）" : item.get_grade_string(); if(_info_count is not null)_info_count.Text=$"数量 x{count}";
        if(_info_tags is not null)_info_tags.Text=build_tags(item.tags,item.craft_data as MaterialCraftData);
        if(_info_effect_title is not null)_info_effect_title.Text="使用效果";
        if(_info_desc is not null)_info_desc.Text=build_item_usage_effect(item);
        if (_action_button is not null && _player is not null && item is EquipmentData)
        {
            _action_button.Text=_player.is_item_equipped(_active_slot?.item_path??"")?"卸下":"穿戴"; _action_button.Disabled=false; _action_button.TooltipText=""; _action_button.Show();
        }
        else if (_action_button is not null && is_secret_book(item))
        {
            _action_button.Text="安排研读"; _action_button.Disabled=false; _action_button.TooltipText=""; _action_button.Show();
        }
        else _action_button?.Hide();
    }
    private void fill_artifact_info(ArtifactInstanceData artifact)
    {
        show_info_content();
        var name=artifact_name(artifact); var equipped=_player?.is_artifact_equipped(artifact.instance_id)==true;
        if(_info_name is not null)_info_name.Text=name;
        if (_info_icon is not null) { var path=artifact.form?.icon_path??""; _info_icon.Texture=string.IsNullOrWhiteSpace(path) || !ResourceLoader.Exists(path) ? null : ResourceLoader.Load<Texture2D>(path); }
        if(_info_category is not null)_info_category.Text=$"法宝 · {(string.IsNullOrWhiteSpace(artifact.ritual_mode)?"未定祭法":artifact.ritual_mode)}";
        if(_info_grade is not null)_info_grade.Text=$"{artifact.artifact_tier} · {(string.IsNullOrWhiteSpace(artifact.element)?"五行未定":artifact.element)}";
        if(_info_count is not null)_info_count.Text=artifact.max_durability>0?$"耐久 {Math.Max(0,artifact.current_durability)}/{artifact.max_durability}　灵识 {Math.Max(0,artifact.spirit_cost)}":"耐久未定　灵识 "+Math.Max(0,artifact.spirit_cost);
        if(_info_tags is not null)_info_tags.Text=string.IsNullOrWhiteSpace(artifact.utility_id)?"主功用：未定":$"主功用：{artifact.utility_id}　品级：{Math.Max(0,artifact.utility_grade)}";
        if(_info_effect_title is not null)_info_effect_title.Text="法宝信息";
        if(_info_desc is not null)_info_desc.Text=$"实例 ID：{artifact.instance_id}\n\n法宝属于玩家行囊中的唯一实例，不与普通物品堆叠。\n{(string.IsNullOrWhiteSpace(artifact.bound_basic_card_path)?"":"绑定基础牌："+artifact.bound_basic_card_path)}";
        if (_action_button is not null && _player is not null)
        {
            _action_button.Text=equipped?"卸下":"装备"; _action_button.Disabled=!equipped && _player.entity_data is HumanData data && (data.artifact_slot_count<=data.equipped_artifact_ids.Count || (artifact.max_durability>0 && artifact.current_durability<=0)); _action_button.TooltipText=_action_button.Disabled?"没有可用的法宝槽，或法宝耐久为零":""; _action_button.Show();
        }
    }
    private void on_action_pressed()
    {
        if (_player is null) return;
        if (_active_artifact is null && _active_slot is not null && load_item_data(_active_slot.item_path) is ItemData active_item && is_secret_book(active_item))
        {
            var study_path = _active_slot.item_path; clear_active_info(); EmitSignal(SignalName.secret_book_requested, study_path); return;
        }
        var changed = _active_artifact is not null
            ? (_player.is_artifact_equipped(_active_artifact.instance_id) ? _player.unequip_artifact(_active_artifact.instance_id) : _player.equip_artifact(_active_artifact.instance_id))
            : _active_slot is not null && (_player.is_item_equipped(_active_slot.item_path) ? _player.unequip_inventory_item(_active_slot.item_path) : _player.equip_inventory_item(_active_slot.item_path));
        if (changed) refresh_slots();
    }
    private static string artifact_name(ArtifactInstanceData artifact) => string.IsNullOrWhiteSpace(artifact.artifact_name) ? artifact.form?.form_name ?? "未命名法宝" : artifact.artifact_name;
    internal static string build_tags(Array<string> tags,MaterialCraftData? material)
    {
        var lines=new List<string>();
        foreach(var dimension in new[]{"五行","质地","相性","功用"})
        {
            var values=ItemAffixCatalog.get_tag_values(tags,dimension);
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
    internal static string build_item_usage_effect(ItemData item)
    {
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
            if(!string.IsNullOrWhiteSpace(skill.description))return skill.description.Trim();
            return skill.get_brief_description();
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
    private ItemData? load_item_data(string path){if(string.IsNullOrWhiteSpace(path))return null;if(_cache.TryGetValue(path,out var item))return item;if(!ResourceLoader.Exists(path))return null;item=ResourceLoader.Load<ItemData>(path);if(item is not null)_cache[path]=item;return item;}
    private static bool is_secret_book(ItemData item)=>item.gongfa is not null||category_of(item,"")=="秘籍";
    internal static string category_of(ItemData item,string path){var c=normalize_category(item.item_category);if(c!="")return c;return item.item_subcategory is "石材" or "矿石" or "草药" or "粮食" or "纤维" or "兽材" or "灵石"||path.Contains("石材")?"材料":"";}
    private static string normalize_category(string value)=>value.Trim() switch {"" or "全部"=>"","材料" or "粮食" or "灵石"=>"材料","装备"=>"装备","秘籍"=>"秘籍","阵盘"=>"阵盘","器具"=>"器具","符箓"=>"符箓","丹药"=>"丹药","灵膳"=>"灵膳",var other=>other};
    private static string wuxing_name(int value)=>value switch {0=>"无要求（无灵根可学）",1=>"金",2=>"木",3=>"水",4=>"火",5=>"土",31=>"金木水火土",32=>"通用（有灵根即可）",_=>"复合灵根"};
}
