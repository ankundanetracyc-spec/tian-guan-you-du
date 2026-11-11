using Godot;
using Godot.Collections;

/// <summary>人类角色：统一承载成长、功法、装备与秘籍研读的运行时状态。</summary>
public partial class Character : Creature
{
    public const int InventoryCapacity = 200;
    // Kept for legacy icon-grid callers; storage admission uses InventoryCapacity.
    public const int InventorySlotCapacity = InventoryCapacity;
    private const long GameHoursPerAgeYear = 360L * 24L;
    private const string PublicBasicSkillPoolPath = "res://resources/basic_decks/公共基础技能池.tres";
    // 早期灵石的可用储灵量：三灵根在密度 180、每日修炼十二时的条件下约可持续十日。
    // 灵石只提供环境，实际消耗仍随角色在该环境中的纳气速率变化。
    private const float SpiritStoneStoredLingqi = .36f;
    [Signal] public delegate void gongfa_chapter_completedEventHandler(Character character,string gongfa_name,string chapter_id,string chapter_name);
    [Signal] public delegate void breakthrough_startedEventHandler(Character character,string target_realm);
    [Signal] public delegate void breakthrough_completedEventHandler(Character character,string new_realm);
    [Signal] public delegate void inventory_changedEventHandler();
    private HumanData? human => entity_data as HumanData;
    /// <summary>供家族剧情和界面订阅的运行时通知；不依赖面板轮询研读状态。</summary>
    public event Action<Character, string, string, string, Dictionary>? GongfaChapterCompleted;
    public event Action<Character, ItemData, int>? ItemAcquired;
    public event Action<Character, GongfaResource>? StudyStarted;
    public event Action<Character>? InventoryChanged;
    public event Action<Character>? WorldStaminaChanged;
    /// <summary>境界实际变更后的通知；用于阶段任务。</summary>
    public event Action<Character, string>? BreakthroughCompleted;
    public event Action<Character, GongfaResource>? MainGongfaChanged;
    // 本次研读的卷章若已完成，供截止时间类系统读取实际达到所需进度的游戏时辰。
    private long _study_action_completion_hour = -1;
    private long _last_breakthrough_completion_hour = -1;
    public long get_study_action_completion_hour() => _study_action_completion_hour;
    public long get_last_breakthrough_completion_hour() => _last_breakthrough_completion_hour;

    private void advance_study_for_world_action(SecretBook book, int hours, float multiplier)
    {
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        var rate = Math.Max(.01f, get_learning_rate()) * Math.Max(0f, multiplier);
        var needed = Math.Max(0f, book.get_required_study_progress() - book.current_study_progress);
        _study_action_completion_hour = time is null || rate <= 0f
            ? -1
            : (long)time.get_total_game_hours() + Math.Clamp((long)Math.Ceiling(needed / rate), 1L, (long)hours);
        try { book.advance_study_hours(hours * multiplier); }
        finally { _study_action_completion_hour = -1; }
    }
    public GongfaResource? main_gongfa; public Array<GongfaResource> sub_gongfa_list=[];
    /// <summary>旧调用点兼容属性；数据实际保存在 HumanData 的玩家行囊字段中。</summary>
    public Array<string> pack_items => human?.inventory_item_paths ?? new Array<string>();
    public Array<int> pack_counts => human?.inventory_item_counts ?? new Array<int>();
    public Array<ArtifactInstanceData> artifact_inventory => human?.artifact_inventory ?? new Array<ArtifactInstanceData>();
    public int max_pack_slots { get => InventoryCapacity; set { } }
    private readonly Array<SecretBook> _study_books=[]; private float _session_hours; private int _last_schedule_hour; private readonly Dictionary _last_schedule_report=[];
    private string _portraitClothingResourcePath = "";
    private PortraitClothingData? _portraitClothingResource;
    private bool is_visualization_cloud_breath() => cultivation_realm.StartsWith("云息", StringComparison.Ordinal);
    public int spirit_root_type {get=>human?.spirit_root_type??0;set{if(human is not null)human.spirit_root_type=value;}} public string spirit_root_name{get=>human?.spirit_root_name??"无灵根";set{if(human is not null)human.spirit_root_name=value;}} public int core_count{get=>human?.core_count??0;set{if(human is not null)human.core_count=value;}}
    public bool has_spirit_core=>human?.has_spirit_core??false;
    public float spirit_core_volume=>human?.spirit_core_volume??1f;
    public float spirit_core_density=>human?.spirit_core_density??1f;
    public float spirit_core_impurity=>human?.spirit_core_impurity??0f;
    public string spirit_core_quality=>human?.spirit_core_quality??"";
    /// <summary>大世界遁速评分；不复用战斗身法。</summary>
    public float dun_speed { get => Mathf.Max(0.01f, human?.dun_speed ?? 100.0f); set { if (human is not null) human.dun_speed = Mathf.Max(0.01f, value); } }
    public bool can_fly_world_map { get => human?.can_fly_world_map ?? false; set { if (human is not null) human.can_fly_world_map = value; } }
    public float max_world_stamina { get => Mathf.Max(0f, human?.max_world_stamina ?? WorldStaminaRules.InitialCapacity); set { if (human is null) return; human.max_world_stamina = Mathf.Max(0f, value); current_world_stamina = Mathf.Min(current_world_stamina, human.max_world_stamina); } }
    public float current_world_stamina { get => Mathf.Clamp(human?.current_world_stamina ?? WorldStaminaRules.InitialCapacity, 0f, max_world_stamina); set { if (human is null) return; var next = Mathf.Clamp(value, 0f, max_world_stamina); if (Mathf.IsEqualApprox(human.current_world_stamina, next)) return; human.current_world_stamina = next; WorldStaminaChanged?.Invoke(this); GetTree()?.CallGroup("main_scene", "notify_world_stamina_changed", this); } }
    public bool can_spend_world_stamina(float amount) => amount <= 0f || current_world_stamina + 0.0001f >= amount;
    public bool try_spend_world_stamina(float amount) { if (amount <= 0f) return true; if (!can_spend_world_stamina(amount)) return false; current_world_stamina = Mathf.Max(0f, current_world_stamina - amount); return true; }
    public float restore_world_stamina(float amount) { var before = current_world_stamina; current_world_stamina = Mathf.Min(max_world_stamina, before + Mathf.Max(0f, amount)); return current_world_stamina - before; }
    public int get_available_world_action_hours(float costPerHour, int requestedHours) { if (requestedHours <= 0 || costPerHour <= 0f) return Math.Max(0, requestedHours); return Math.Min(requestedHours, Mathf.FloorToInt((current_world_stamina + 0.0001f) / costPerHour)); }
    public int daily_cultivation_hours{get=>human?.daily_cultivation_hours??0;set{if(human is not null)human.daily_cultivation_hours=value;}} public int daily_study_hours{get=>human?.daily_study_hours??0;set{if(human is not null)human.daily_study_hours=value;}} public int daily_rest_hours{get=>human?.daily_rest_hours??0;set{if(human is not null)human.daily_rest_hours=value;}} public int daily_labor_hours{get=>human?.daily_labor_hours??0;set{if(human is not null)human.daily_labor_hours=value;}} public bool daily_schedule_enabled{get=>human?.daily_schedule_enabled??false;set{if(human is not null)human.daily_schedule_enabled=value;}} public bool schedule_auto_breakthrough{get=>human?.schedule_auto_breakthrough??false;set{if(human is not null)human.schedule_auto_breakthrough=value;}} public bool study_auto_next_chapter{get=>human?.study_auto_next_chapter??false;set{if(human is not null)human.study_auto_next_chapter=value;}}
    public int cultivation_priority{get=>human?.cultivation_priority??0;set{if(human is not null)human.cultivation_priority=value;}} public int study_priority{get=>human?.study_priority??0;set{if(human is not null)human.study_priority=value;}} public int rest_priority{get=>human?.rest_priority??0;set{if(human is not null)human.rest_priority=value;}} public int labor_priority{get=>human?.labor_priority??0;set{if(human is not null)human.labor_priority=value;}} public float labor_progress_hours{get=>human?.labor_progress_hours??0;set{if(human is not null)human.labor_progress_hours=value;}} public string assigned_labor_site_id{get=>human?.assigned_labor_site_id??"";set{if(human is not null)human.assigned_labor_site_id=value;}} public string residence_room_id{get=>human?.residence_room_id??"";set{if(human is not null)human.residence_room_id=value;}} public string spouse_name{get=>human?.spouse_name??"";set{if(human is not null)human.spouse_name=value;}}
    public float ambient_lingqi_pressure{get=>human?.ambient_lingqi_pressure??100;set{if(human is not null)human.ambient_lingqi_pressure=value;}} public string basic_deck_path{get=>human?.basic_deck_path??"";set{if(human is not null)human.basic_deck_path=value;}} public int max_sub_slots{get=>Math.Max(0,(human?.max_sub_slots??2)+Mathf.RoundToInt(get_realm_attribute_adjustment("sub_slot_increase").fixedBonus));set{if(human is not null)human.max_sub_slots=value;}} public float mana_regen_rate{get=>human?.mana_regen_rate??.04f;set{if(human is not null)human.mana_regen_rate=value;}}
    public int meditation_room_index{get=>human?.meditation_room_index??-1;}
    public bool meditation_spirit_stone_supply{get=>human?.meditation_spirit_stone_supply??false;}
    public override void _Ready()
    {
        entity_data ??=new HumanData();
        if(entity_data is not HumanData)entity_data=new HumanData{entity_name=entity_data.entity_name};
        if (human is not null) TalismanCraftingService.RestoreDefinitions(human);
        ensure_basic_deck();ensure_spirit_core_state();ensure_body_refinement_state();ensure_visualization_state();ensure_inventory_state();
        load_gongfa_data();apply_missing_gongfa_permanent_bonuses();base._Ready();refresh_gongfa_effects();
        var time=GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        if(time is not null)
        {
            _last_schedule_hour=time.get_total_game_hours();
            if(human is not null && human.age_last_updated_game_hour < 0) human.age_last_updated_game_hour=time.get_total_game_hours();
            time.TimeAdvanceStarting+=process_age_advance;
            time.time_changed+=process_breakthrough_retreat;
            time.time_changed+=process_body_refinement;
            time.time_changed+=process_yuanxi_external_time;
        }
    }
    private void process_age_advance(int fromHour,int toHour,string _reason)
    {
        if(human is null || toHour<=fromHour) return;
        var last=human.age_last_updated_game_hour;
        if(last<0 || last>fromHour) last=fromHour;
        var elapsed=(long)toHour-last;
        if(elapsed<GameHoursPerAgeYear) { human.age_last_updated_game_hour=last; return; }
        var years=elapsed/GameHoursPerAgeYear;
        current_age=checked(current_age+(int)years);
        human.age_last_updated_game_hour=last+years*GameHoursPerAgeYear;
    }
    public override void load_from_data(EntityData source){var data=source as HumanData??new HumanData{entity_name=source.entity_name};entity_data=data.Duplicate(true) as HumanData;if(human is not null)TalismanCraftingService.RestoreDefinitions(human);ensure_basic_deck();ensure_spirit_core_state();ensure_body_refinement_state();ensure_visualization_state();ensure_inventory_state();load_gongfa_data();apply_missing_gongfa_permanent_bonuses();base.load_from_data(entity_data!);refresh_gongfa_effects();}
    private void ensure_basic_deck(){if(human is null)return;human.basic_deck_path=PublicBasicSkillPoolPath;if(Mathf.IsEqualApprox(human.dun_speed,100.0f)){var legacy_speed=!Mathf.IsEqualApprox(human.dun_speed_cells_per_day,1.0f)?human.dun_speed_cells_per_day:human.travel_speed_cells_per_day;if(!Mathf.IsEqualApprox(legacy_speed,1.0f))human.dun_speed=legacy_speed*100.0f;}human.dun_speed=Mathf.Max(0.01f,human.dun_speed);human.dun_speed_cells_per_day=1.0f;human.travel_speed_cells_per_day=1.0f;}
    private void ensure_spirit_core_state()
    {
        if (human is null) return;
        human.spirit_core_volume=Mathf.Max(CondensationCoreState.MinimumVolume,human.spirit_core_volume);
        human.spirit_core_density=Mathf.Max(CondensationCoreState.MinimumDensity,human.spirit_core_density);
        human.spirit_core_impurity=Mathf.Clamp(human.spirit_core_impurity,0f,CondensationCoreState.FailureImpurity);
        if (!human.has_spirit_core) human.spirit_core_quality="";
    }
    private void ensure_body_refinement_state()
    {
        if (human is null) return;
        foreach (var part in BodyRefinementRules.Parts) if (!human.body_refinement_progress.ContainsKey(part)) human.body_refinement_progress[part] = 0f;
        foreach (var method in BodyRefinementCatalog.LoadDefaultMethods())
            if (method.default_unlocked && !human.learned_body_refinement_method_paths.Contains(method.ResourcePath)) human.learned_body_refinement_method_paths.Add(method.ResourcePath);
    }
    private void ensure_visualization_state()
    {
        if (human is null) return;
        human.learned_visualization_ids ??= [];
        human.visualization_progress ??= [];
        human.equipped_visualizations ??= [];
        foreach (var scene in VisualizationCatalog.Defaults)
        {
            if (!human.learned_visualization_ids.Contains(scene.Id)) human.learned_visualization_ids.Add(scene.Id);
            if (!human.visualization_progress.ContainsKey(scene.Id)) human.visualization_progress[scene.Id] = 1f;
            if (!human.equipped_visualizations.ContainsKey(scene.Slot)) human.equipped_visualizations[scene.Slot] = scene.Id;
        }
        if (string.IsNullOrEmpty(human.visualization_focus_id) || VisualizationCatalog.Get(human.visualization_focus_id) is null)
            human.visualization_focus_id = VisualizationCatalog.Defaults[0].Id;
    }
    private void ensure_inventory_state()
    {
        if (human is null) return;
        human.inventory_item_paths ??= [];
        human.inventory_item_counts ??= [];
        human.inventory_item_positions ??= [];
        human.artifact_inventory_positions ??= [];
        human.artifact_inventory ??= [];
        human.equipped_artifact_ids ??= [];
        while (human.inventory_item_counts.Count < human.inventory_item_paths.Count) human.inventory_item_counts.Add(1);
        while (human.inventory_item_positions.Count > human.inventory_item_paths.Count) human.inventory_item_positions.RemoveAt(human.inventory_item_positions.Count - 1);
        while (human.artifact_inventory_positions.Count > human.artifact_inventory.Count) human.artifact_inventory_positions.RemoveAt(human.artifact_inventory_positions.Count - 1);
        while (human.inventory_item_positions.Count < human.inventory_item_paths.Count) human.inventory_item_positions.Add(find_free_inventory_position());
        while (human.inventory_item_counts.Count > human.inventory_item_paths.Count) human.inventory_item_counts.RemoveAt(human.inventory_item_counts.Count - 1);
        while (human.artifact_inventory_positions.Count < human.artifact_inventory.Count) human.artifact_inventory_positions.Add(find_free_inventory_position());
        for (var i = human.inventory_item_paths.Count - 1; i >= 0; i--)
        {
            if (string.IsNullOrWhiteSpace(human.inventory_item_paths[i]) || human.inventory_item_counts[i] <= 0)
            {
                human.inventory_item_paths.RemoveAt(i); human.inventory_item_counts.RemoveAt(i); human.inventory_item_positions.RemoveAt(i);
            }
        }
        ensure_portrait_clothing_state();
        foreach (var path in new[] { human.weapon_path, human.headwear_path, human.clothes_path, human.ornament_1_path, human.footwear_path, human.ornament_2_path })
            if (!HasMeta("artifact_battle_clone") && !string.IsNullOrWhiteSpace(path) && ResourceLoader.Load<ItemData>(path) is not null && get_inventory_total_count(path) == 0)
            {
                human.inventory_item_paths.Add(path); human.inventory_item_counts.Add(1); human.inventory_item_positions.Add(find_free_inventory_position());
            }
        for (var i = human.artifact_inventory.Count - 1; i >= 0; i--)
        {
            var artifact = human.artifact_inventory[i];
            if (artifact is null) { human.artifact_inventory.RemoveAt(i); human.artifact_inventory_positions.RemoveAt(i); continue; }
            artifact.ensure_instance_id();
        }
        for (var i = human.equipped_artifact_ids.Count - 1; i >= 0; i--)
            if (find_artifact(human.equipped_artifact_ids[i]) is null) human.equipped_artifact_ids.RemoveAt(i);
        ensure_spatial_inventory_state();
        sync_artifact_spirit_perception();
    }
    private void ensure_portrait_clothing_state()
    {
        if (human is null || !IsInsideTree()) return;
        var mods = GetNodeOrNull<AppearanceModCatalog>("/root/AppearanceModCatalog");
        if (mods is null) return;
        string gender = human.gender == 1 ? "female" : "male";
        int style = human.appearance_clothes_style;
        if (style < 0)
        {
            var catalog = GetNodeOrNull<PortraitCatalog>("/root/PortraitCatalog");
            if (catalog is not null)
            {
                var context = catalog.get_context(human, gender, 18, human.faction_name, "");
                var chosen = catalog.choose_part(context, "clothes");
                if (chosen.Count > 0) style = chosen.GetValueOrDefault("variant_index", -1).AsInt32();
            }
            if (style < 0)
                foreach (Dictionary entry in mods.get_parts(gender, "clothes"))
                {
                    style = entry.GetValueOrDefault("variant_index", -1).AsInt32();
                    if (style >= 0) break;
                }
            if (style >= 0) human.appearance_clothes_style = style;
        }

        string itemPath = human.portrait_clothing_item_path;
        var clothing = load_portrait_clothing_resource(itemPath);
        if (clothing is null || !clothing.can_be_worn_by(human))
        {
            itemPath = "";
            foreach (Dictionary entry in mods.get_parts(gender, "clothes"))
                if (entry.GetValueOrDefault("variant_index", -1).AsInt32() == style)
                {
                    itemPath = entry.GetValueOrDefault("item_path", "").AsString();
                    break;
                }
            clothing = load_portrait_clothing_resource(itemPath);
        }
        if (clothing is null || !clothing.can_be_worn_by(human)) return;

        human.portrait_clothing_item_path = itemPath;
        human.appearance_clothes_style = clothing.appearance_style_index;
        if (!HasMeta("artifact_battle_clone") && !human.inventory_item_paths.Contains(itemPath))
        {
            human.inventory_item_paths.Add(itemPath);
            human.inventory_item_counts.Add(1);
            human.inventory_item_positions.Add(find_free_inventory_position());
        }
    }
    private PortraitClothingData? load_portrait_clothing_resource(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !ResourceLoader.Exists(path))
        {
            _portraitClothingResourcePath = "";
            _portraitClothingResource = null;
            return null;
        }
        if (_portraitClothingResourcePath == path && _portraitClothingResource is { } cached && GodotObject.IsInstanceValid(cached))
            return cached;
        _portraitClothingResource = ResourceLoader.Load<PortraitClothingData>(path);
        _portraitClothingResourcePath = _portraitClothingResource is null ? "" : path;
        return _portraitClothingResource;
    }
    private ItemData? load_inventory_item(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (_spatialItemCache.TryGetValue(path, out var cached) && cached is not null && GodotObject.IsInstanceValid(cached)) return cached;
        var item = ResourceLoader.Exists(path) ? ResourceLoader.Load<ItemData>(path) : null;
        if (item is not null) _spatialItemCache[path] = item;
        return item;
    }
    private int get_inventory_index(string path, int start = 0)
    {
        if (human is null) return -1;
        for (var i = Math.Max(0, start); i < human.inventory_item_paths.Count; i++) if (human.inventory_item_paths[i] == path) return i;
        return -1;
    }
    private int find_free_inventory_position()
    {
        if (human is null) return 0;
        var occupied = new HashSet<int>(human.inventory_item_positions);
        occupied.UnionWith(human.artifact_inventory_positions);
        for (var i = 0; i < InventorySlotCapacity; i++) if (!occupied.Contains(i)) return i;
        return InventorySlotCapacity + occupied.Count;
    }
    private void notify_inventory_changed()
    {
        ensure_spatial_inventory_state();
        sync_artifact_spirit_perception();
        EmitSignal(SignalName.inventory_changed);
        InventoryChanged?.Invoke(this);
    }
    public Array<Dictionary> get_inventory_snapshot()
    {
        ensure_inventory_state();
        var result = new Array<Dictionary>();
        if (human is null) return result;
        foreach (var i in Enumerable.Range(0, human.inventory_item_paths.Count).OrderBy(index => human.inventory_item_positions[index]))
        {
            var path = human.inventory_item_paths[i];
            var item = load_inventory_item(path);
            var placement = human.inventory_placements.FirstOrDefault(p => p.row_slot == human.inventory_item_positions[i]);
            result.Add(new Dictionary
            {
                ["slot_index"] = human.inventory_item_positions[i], ["item_path"] = path, ["count"] = human.inventory_item_counts[i],
                ["item_name"] = item?.entity_name ?? path, ["category"] = item?.item_category ?? "",
                ["equipped"] = is_item_equipped(path), ["storage_locked"] = has_nonempty_storage_bag(placement?.instance_id ?? ""), ["instance_id"] = placement?.instance_id ?? "",
                ["region_id"] = placement?.region_id ?? "", ["cell_x"] = placement?.cell_x ?? -1, ["cell_y"] = placement?.cell_y ?? -1, ["rotation"] = placement?.rotation ?? 0
            });
        }
        return result;
    }
    public int get_inventory_total_count(string path)
    {
        if (human is null || string.IsNullOrWhiteSpace(path)) return 0;
        var total = 0;
        for (var i = 0; i < human.inventory_item_paths.Count; i++) if (human.inventory_item_paths[i] == path) total += Math.Max(0, human.inventory_item_counts[i]);
        return total;
    }
    public int get_inventory_slot_count()
    {
        ensure_inventory_state();
        return human is null ? 0 : human.inventory_item_paths.Count + human.artifact_inventory.Count;
    }
    public int get_inventory_free_capacity() => Math.Max(0, InventoryCapacity - get_inventory_used_capacity());
    // Legacy API name retained for callers that still describe capacity as free slots.
    public int get_inventory_free_slot_count() => get_inventory_free_capacity();
    public bool has_inventory_item(string path, int count = 1) => count <= 0 || get_inventory_total_count(path) >= count;
    public int get_artifact_inventory_slot_index(string instanceId)
    {
        ensure_inventory_state();
        if (human is null) return -1;
        for (var i = 0; i < human.artifact_inventory.Count; i++)
            if (human.artifact_inventory[i]?.instance_id == instanceId) return human.artifact_inventory_positions[i];
        return -1;
    }
    public bool move_inventory_slot(int fromPosition, int toPosition)
    {
        if (human is null || fromPosition < 0 || fromPosition >= InventorySlotCapacity || toPosition < 0 || toPosition >= InventorySlotCapacity || fromPosition == toPosition) return false;
        ensure_inventory_state();
        var sourceItem = human.inventory_item_positions.IndexOf(fromPosition);
        var targetItem = human.inventory_item_positions.IndexOf(toPosition);
        var sourceArtifact = human.artifact_inventory_positions.IndexOf(fromPosition);
        var targetArtifact = human.artifact_inventory_positions.IndexOf(toPosition);
        if (sourceItem < 0 && sourceArtifact < 0) return false;
        if (sourceItem >= 0) human.inventory_item_positions[sourceItem] = toPosition;
        else human.artifact_inventory_positions[sourceArtifact] = toPosition;
        if (targetItem >= 0) human.inventory_item_positions[targetItem] = fromPosition;
        else if (targetArtifact >= 0) human.artifact_inventory_positions[targetArtifact] = fromPosition;
        notify_inventory_changed();
        return true;
    }
    public bool can_add_item_to_inventory(string path, int count = 1)
    {
        if (human is null || count <= 0) return false;
        return can_add_inventory_batch(new System.Collections.Generic.Dictionary<string, int> { [path] = count });
    }
    public bool add_item_to_inventory(string path, int count = 1)
        => add_item_to_inventory_core(path, count, announceAcquisition: true);

    /// <summary>用于交易暂存、失败回滚等不应被呈现为新收获的行囊变更。</summary>
    public bool add_item_to_inventory_silently(string path, int count = 1)
        => add_item_to_inventory_core(path, count, announceAcquisition: false);

    private bool add_item_to_inventory_core(string path, int count, bool announceAcquisition)
    {
        if (human is null || count <= 0) return false;
        return try_exchange_inventory(new System.Collections.Generic.Dictionary<string, int>(),
            new System.Collections.Generic.Dictionary<string, int> { [path] = count }, announceAcquisition);
    }

    public void publish_item_acquired_notice(string path, int count = 1)
    {
        if (count <= 0) return;
        var item = load_inventory_item(path);
        if (item is not null) ItemAcquired?.Invoke(this, item, count);
    }
    public bool remove_item_from_inventory(string path, int count = 1)
    {
        if (human is null || count <= 0) return false;
        return try_exchange_inventory(new System.Collections.Generic.Dictionary<string, int> { [path] = count },
            new System.Collections.Generic.Dictionary<string, int>());
    }
    public Array<string> get_equipped_item_paths()
    {
        var result = new Array<string>();
        if (human is null) return result;
        foreach (var path in new[] { human.weapon_path, human.headwear_path, human.clothes_path, human.ornament_1_path, human.footwear_path, human.ornament_2_path })
            if (!string.IsNullOrWhiteSpace(path) && !result.Contains(path)) result.Add(path);
        if (!string.IsNullOrWhiteSpace(human.portrait_clothing_item_path) && !result.Contains(human.portrait_clothing_item_path))
            result.Add(human.portrait_clothing_item_path);
        return result;
    }
    private int get_equipped_item_count(string path) => human is null ? 0 : new[] { human.weapon_path, human.headwear_path, human.clothes_path, human.ornament_1_path, human.footwear_path, human.ornament_2_path }.Count(item => item == path) + (human.portrait_clothing_item_path == path ? 1 : 0);
    public bool is_item_equipped(string path) => get_equipped_item_count(path) > 0;
    public bool equip_inventory_item(string path, string slot = "")
    {
        if (human is null || !has_inventory_item(path)) return false;
        if (load_inventory_item(path) is PortraitClothingData portraitClothing)
        {
            if (!portraitClothing.can_be_worn_by(human)) return false;
            human.portrait_clothing_item_path = path;
            human.appearance_clothes_style = portraitClothing.appearance_style_index;
            human.EmitChanged();
            notify_inventory_changed();
            return true;
        }
        if (load_inventory_item(path) is not EquipmentData equipment) return false;
        var target = slot.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(target)) target = equipment.equip_type switch { 0 => "weapon", 1 => "clothes", 2 => string.IsNullOrEmpty(human.ornament_1_path) ? "ornament_1" : string.IsNullOrEmpty(human.ornament_2_path) ? "ornament_2" : "ornament_1", _ => "" };
        if (target is not ("weapon" or "headwear" or "clothes" or "ornament_1" or "footwear" or "ornament_2")) return false;
        if (equipment.definition_id.ToString().StartsWith("sect_sword_", StringComparison.Ordinal) && target != "weapon") return false;
        if (target == "weapon") remove_forged_weapon_for_standard_equipment();
        switch (target) { case "weapon": human.weapon_path = path; break; case "headwear": human.headwear_path = path; break; case "clothes": human.clothes_path = path; break; case "ornament_1": human.ornament_1_path = path; break; case "footwear": human.footwear_path = path; break; case "ornament_2": human.ornament_2_path = path; break; }
        refresh_gongfa_effects();
        notify_inventory_changed();
        return true;
    }
    public bool unequip_inventory_item(string path)
    {
        if (human is null || !is_item_equipped(path)) return false;
        if (human.portrait_clothing_item_path == path) return false;
        if (human.weapon_path == path) human.weapon_path = "";
        if (human.headwear_path == path) human.headwear_path = "";
        if (human.clothes_path == path) human.clothes_path = "";
        if (human.ornament_1_path == path) human.ornament_1_path = "";
        if (human.footwear_path == path) human.footwear_path = "";
        if (human.ornament_2_path == path) human.ornament_2_path = "";
        refresh_gongfa_effects();
        notify_inventory_changed();
        return true;
    }
    public Array<ArtifactInstanceData> get_artifact_items() { ensure_inventory_state(); return human?.artifact_inventory ?? new Array<ArtifactInstanceData>(); }
    private ArtifactInstanceData? find_artifact(string instanceId)
    {
        if (human is null) return null;
        foreach (var artifact in human.artifact_inventory) if (artifact is not null && artifact.instance_id == instanceId) return artifact;
        return null;
    }
    public bool is_artifact_equipped(string instanceId) => human?.equipped_artifact_ids.Contains(instanceId) == true;
    public bool add_artifact_to_inventory(ArtifactInstanceData artifact)
        => try_add_artifact_to_inventory(artifact);
    public bool remove_artifact_from_inventory(string instanceId)
    {
        if (human is null || _spatialCommitting || is_artifact_equipped(instanceId) || find_artifact(instanceId)?.travel_in_use == true) return false;
        var index = -1;
        for (var i = 0; i < human.artifact_inventory.Count; i++) if (human.artifact_inventory[i]?.instance_id == instanceId) { index = i; break; }
        if (index < 0) return false;
        human.artifact_inventory.RemoveAt(index); human.artifact_inventory_positions.RemoveAt(index); notify_inventory_changed(); return true;
    }
    public int get_used_artifact_spirit() => HasMeta("artifact_battle_clone") ? used_spirit_perception : 0; // 局外没有在场外祭实体；内炼不占灵识。
    public bool equip_artifact(string instanceId)
    {
        if (HasMeta("artifact_battle_clone")) return false;
        if (human is null || find_artifact(instanceId) is not { } artifact || is_artifact_equipped(instanceId)) return false;
        // 新器物装备/单槽御用尚待接通，不走旧内炼/外祭与耐久分支。
        if (ForgedArtifactInventory.IsForged(artifact)) return equip_forged_sword(artifact);
        if (human.artifact_slot_count <= human.equipped_artifact_ids.Count) return false;
        if (artifact.ritual_mode == "内炼" && !string.IsNullOrEmpty(artifact.bound_basic_card_path)
            && equipped_combat_artifacts().Any(other => other.ritual_mode == "内炼" &&
                resolve_combat_skill_slot_path(other.bound_basic_card_path) == resolve_combat_skill_slot_path(artifact.bound_basic_card_path))) return false;
        if (artifact.max_durability > 0 && artifact.current_durability <= 0) return false;
        human.equipped_artifact_ids.Add(instanceId); notify_inventory_changed(); return true;
    }
    public bool unequip_artifact(string instanceId)
    {
        if (HasMeta("artifact_battle_clone")) return false;
        if (human is null || !human.equipped_artifact_ids.Remove(instanceId)) return false;
        revoke_artifact_skill(instanceId);
        refresh_gongfa_effects();
        notify_inventory_changed(); return true;
    }
    private void sync_artifact_spirit_perception()
    {
        // 局外持有/御用本身不占灵识；行囊刷新不能拿器物占用覆盖人物的共同占用池。
        // 器体在场及其他事务应由各自生命周期登记、释放，保留它们已经占用的额度。
        if (human is not null) human.used_spirit_perception = Math.Max(0, human.used_spirit_perception);
    }
    private void load_gongfa_data(){if(human is null)return;main_gongfa=load_gongfa(human.main_gongfa_path);sub_gongfa_list.Clear();foreach(var path in human.sub_gongfa_paths){var g=load_gongfa(path);if(g is not null)sub_gongfa_list.Add(g);}}
    private static GongfaResource? load_gongfa(string path)=>GongfaAvailability.EnabledPath(path) is { Length: > 0 } enabled?ResourceLoader.Load<GongfaResource>(enabled):null;
    public bool has_learned_gongfa(GongfaResource gongfa)=>GongfaAvailability.IsEnabled(gongfa)&&human?.learned_gongfa_paths.Contains(gongfa.ResourcePath)==true;
    public Array<GongfaResource> get_known_gongfas()
    {
        Array<GongfaResource> result=[];
        void Add(GongfaResource? value){if(value is not null&&GongfaAvailability.IsEnabled(value)&&!result.Any(item=>item.ResourcePath==value.ResourcePath))result.Add(value);}
        Add(main_gongfa); foreach(var value in sub_gongfa_list)Add(value);
        if(human is not null)foreach(var path in human.learned_gongfa_paths)Add(load_gongfa(path));
        if(human is not null)Add(load_gongfa(human.sect_lineage_gongfa_path));
        foreach(var path in pack_items)
            if(ResourceLoader.Exists(path)&&ResourceLoader.Load<ItemData>(path)?.gongfa is GongfaResource held)Add(held);
        return result;
    }
    /// <summary>汇总已研读卷章授予的可调整主动技能。心法由当前主修/辅修槽位单独提供，不作为习得技能。</summary>
    public Array<ActiveSkillData> get_available_battle_skills()
    {
        Array<ActiveSkillData> result = [];
        void Add(Resource? resource)
        {
            if (resource is not ActiveSkillData skill || !GongfaAvailability.IsSkillEnabled(skill) || skill.skill_type == ActiveSkillData.SkillType.POWER || string.Equals(skill.slot_category, "passive", StringComparison.OrdinalIgnoreCase) || result.Any(card => combat_skill_key(card) == combat_skill_key(skill))) return;
            result.Add(skill);
        }
        foreach (var skill in active_skills) Add(skill);
        if (human is not null)
            foreach (var path in human.legacy_learned_combat_skill_paths)
                if (ResourceLoader.Exists(path)) Add(ResourceLoader.Load<ActiveSkillData>(path));
        foreach (var path in get_unlocked_skill_node_paths()) Add(ResourceLoader.Load<ActiveSkillData>(path));
        foreach (var book in get_known_gongfas())
        {
            if (book is null) continue;
            var isMain = book == main_gongfa;
            foreach (var skill in GongfaSkillRewriteRuntime.ApplyBookSkillRewrites(this, book, isMain)) Add(skill);
        }
        return result;
    }
    public bool is_gongfa_chapter_learned(GongfaResource gongfa,string chapter_id)=>get_learned_gongfa_chapter_ids(gongfa).Contains(chapter_id);
    /// <summary>卷章研读门槛的唯一判定入口，供自动续读与界面条件展示共用。</summary>
    public bool can_learn_gongfa_chapter(GongfaResource gongfa, GongfaChapterConfig chapter)
    {
        if (human is null || gongfa is null || !GongfaAvailability.IsEnabled(gongfa) || chapter is null) return false;
        var ownsCopy = pack_items.Any(path => load_inventory_item(path)?.gongfa?.ResourcePath == gongfa.ResourcePath);
        if (!ownsCopy && human.baishi_first_volume_only && human.baishi_first_volume_path == gongfa.ResourcePath &&
            gongfa.get_sorted_chapters().FirstOrDefault()?.get_id() != chapter.get_id()) return false;
        // 玉虚师承首期只开放所选秘籍的第一卷；首卷之后的借阅权限尚未定案。
        if (!ownsCopy && human.sect_affiliation_id == "yuxu_xianzong" &&
            human.sect_lineage_gongfa_path == gongfa.ResourcePath &&
            human.sect_lineage_first_volume_only_access &&
            (human.sect_current_rank is "真传" or "真传弟子") &&
            gongfa.get_sorted_chapters().FirstOrDefault() is { } firstChapter &&
            firstChapter.get_id() != chapter.get_id() &&
            is_gongfa_chapter_learned(gongfa, firstChapter.get_id()))
            return false;
        // 研读同样必须先具备该书要求的灵根；灵根权重只在实际修炼速率中结算。
        if (!gongfa.is_wuxing_compatible(spirit_root_type)) return false;
        if (!string.IsNullOrEmpty(chapter.required_realm))
        {
            var realms = GetNodeOrNull<RealmManager>("/root/RealmManager");
            if (realms is not null && !realms.is_realm_after_or_equal(cultivation_realm, chapter.required_realm)) return false;
            if (realms is null && cultivation_realm != chapter.required_realm) return false;
        }
        foreach (Variant key in chapter.required_attributes.Keys)
            if (CardRules.Number(this, key.AsString()) < chapter.required_attributes[key].AsSingle()) return false;
        return chapter.required_chapter_ids.All(id => is_gongfa_chapter_learned(gongfa, id));
    }
    public bool toggle_loaded_skill(Resource skill)
    {
        if(human is null||string.IsNullOrWhiteSpace(skill.ResourcePath))return false;
        var loadedPath = human.loaded_skill_paths.FirstOrDefault(path => resolve_combat_skill_slot_path(path) == skill.ResourcePath);
        if (!string.IsNullOrWhiteSpace(loadedPath)) { human.loaded_skill_paths.Remove(loadedPath); return false; }
        human.loaded_skill_paths.Add(skill.ResourcePath);return true;
    }
    public bool is_skill_loaded(Resource skill)=>human is not null && human.loaded_skill_paths.Any(path => resolve_combat_skill_slot_path(path) == skill.ResourcePath);
    public bool is_battle_deck_configured()=>human?.battle_deck_configured == true;
    /// <summary>即时战斗技能槽只承载可主动使用或可应对排队的指令；主修/辅修槽位中的秘籍自动提供心法被动。</summary>
    public static bool is_combat_skill_slot_eligible(ActiveSkillData? skill)
    {
        if (skill is null) return false;
        if (!GongfaAvailability.IsSkillEnabled(skill)) return false;
        if (skill.skill_type is ActiveSkillData.SkillType.POWER or ActiveSkillData.SkillType.DRAW or ActiveSkillData.SkillType.CURSE) return false;
        if (string.Equals(skill.slot_category, "passive", StringComparison.OrdinalIgnoreCase)) return false;
        if (skill.keywords.Any(keyword => keyword.Contains("固有", StringComparison.Ordinal))) return false;
        if (skill.special_effects.Any(effect => effect.GetValueOrDefault("effect", "").AsString() == "draw")) return false;
        // 即时战斗的体力恢复由系统行动“休整”提供，旧基础库中的“养息”不再占用技能槽。
        if (skill.skill_name == "养息") return false;
        return true;
    }
    public bool has_xuanshu_heart_method_equipped() => main_gongfa?.gongfa_name == "玄枢含章经" || sub_gongfa_list.Any(book => book?.gongfa_name == "玄枢含章经");
    /// <summary>返回当前基础技能库可转为固定技能槽的去重路径；基础牌的数量仍由凝核牌局/旧技能库逻辑另行读取。</summary>
    public Array<string> get_default_combat_skill_paths()
    {
        if (human is null || human.basic_deck_path != PublicBasicSkillPoolPath) return [];
        var paths = get_basic_deck_skill_paths_before_gongfa();
        paths = GongfaSkillRewriteRuntime.ApplyBasicSkillRewrites(this, paths);
        var result = new Array<string>();
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path) || result.Contains(path)) continue;
            var skill = ResourceLoader.Load<ActiveSkillData>(path);
            if (is_combat_skill_slot_eligible(skill)) result.Add(path);
        }
        return result;
    }
    private static BasicDeckResource? _publicBasicDeckResource;
    // 技能池保存的是路径，不会替各招式定义保持托管引用；结算快照会反复解析这些资源。
    private static readonly System.Collections.Generic.Dictionary<string, ActiveSkillData> _publicBasicSkillDefinitions = new(StringComparer.Ordinal);
    public static BasicDeckResource PublicBasicDeckResource => _publicBasicDeckResource ??=
        ResourceLoader.Load<BasicDeckResource>(PublicBasicSkillPoolPath);
    private Array<string> get_basic_deck_skill_paths_before_gongfa()
    {
        if (human is null || human.basic_deck_path != PublicBasicSkillPoolPath) return [];
        // ResourceLoader 的原生缓存不是 C# 包装器的强引用；公共定义需贯穿多次快照与 GC。
        var deck = PublicBasicDeckResource;
        if (deck is null) return [];
        var paths = deck.get_deck_skills();
        foreach (var path in paths)
        {
            if (_publicBasicSkillDefinitions.TryGetValue(path, out var retained) && GodotObject.IsInstanceValid(retained)) continue;
            if (ResourceLoader.Load<ActiveSkillData>(path) is { } definition) _publicBasicSkillDefinitions[path] = definition;
        }
        foreach (var removed in _publicBasicSkillDefinitions.Keys.Where(path => !paths.Contains(path)).ToArray())
            _publicBasicSkillDefinitions.Remove(removed);
        return paths;
    }
    /// <summary>将旧化法版本路径恢复为原技能身份；实际化法效果由明确绑定生成。</summary>
    public string resolve_combat_skill_slot_path(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;
        path = get_base_combat_skill_path(path);
        var basicPaths = get_basic_deck_skill_paths_before_gongfa();
        if (GongfaSkillRewriteRuntime.TryResolveBasicSkillPath(this, basicPaths, path, out var rewrittenPath)) return rewrittenPath;
        foreach (var book in get_known_gongfas())
        {
            if (book is null) continue;
            var resolved = GongfaSkillRewriteRuntime.ResolveBookSkillPath(this, book, book == main_gongfa, path);
            if (resolved != path) return resolved;
        }
        return path;
    }
    /// <summary>从主修/辅修槽位中的秘籍取得开战自动生效的心法；心法不单独学习或装配，也不进入技能槽。</summary>
    public Array<string> get_combat_passive_paths()
    {
        var result = new Array<string>();
        if (human is null) return result;
        void AddSlotHeart(GongfaResource? book, bool isMain)
        {
            var skill = book?.get_heart_method_skill(isMain);
            if (skill is null || string.IsNullOrWhiteSpace(skill.ResourcePath)) return;
            if (result.Contains(skill.ResourcePath)) return;
            result.Add(skill.ResourcePath);
        }
        AddSlotHeart(main_gongfa, true);
        foreach (var book in sub_gongfa_list) AddSlotHeart(book, false);
        return result;
    }
    /// <summary>汇总已学心法机制标记和当前完整描述；主辅修均读取。</summary>
    public Dictionary get_combat_heart_method_upgrades()
    {
        var result = new Dictionary();
        if (human is null) return result;
        void AddBook(GongfaResource? book, bool isMain)
        {
            var heart = book?.get_heart_method_skill(isMain);
            if (book is null || heart is null) return;
            var learnedOrder = 0;
            foreach (var chapter in book.get_sorted_chapters())
            {
                if (!is_gongfa_chapter_learned(book, chapter.get_id())) continue;
                learnedOrder = Math.Max(learnedOrder, chapter.order);
                if (
                    string.IsNullOrWhiteSpace(chapter.heart_method_upgrade_id) ||
                    string.IsNullOrWhiteSpace(chapter.heart_method_upgrade_description)) continue;
                result[$"{book.gongfa_name}|{chapter.heart_method_upgrade_id}"] = chapter.heart_method_upgrade_description;
            }
            if (learnedOrder > 0)
                result[$"{book.gongfa_name}|current_description"] = book.get_heart_method_description(learnedOrder, isMain);
        }
        AddBook(main_gongfa, true);
        foreach (var book in sub_gongfa_list) AddBook(book, false);
        return result;
    }
    /// <summary>汇总已研读卷章的战斗修持；主辅修共用，不再按卷章分支筛选。</summary>
    public Godot.Collections.Dictionary get_active_combat_tempering_by_gongfa()
    {
        var result=new Godot.Collections.Dictionary();
        if(human is null)return result;
        void AddBook(GongfaResource? book,bool isMain)
        {
            if(book is null)return;
            var totals=new Godot.Collections.Dictionary();
            foreach(var chapter in book.get_sorted_chapters())
            {
                if(!is_gongfa_chapter_learned(book,chapter.get_id()))continue;
                foreach(Variant key in chapter.combat_tempering_bonuses.Keys)
                    totals[key]=totals.GetValueOrDefault(key,0f).AsSingle()+chapter.combat_tempering_bonuses[key].AsSingle();
            }
            if(totals.Count>0)result[book.gongfa_name]=totals;
        }
        AddBook(main_gongfa,true);
        foreach(var book in sub_gongfa_list)AddBook(book,false);
        return result;
    }
    public Array<string> get_combat_skill_slot_paths()
    {
        if (human is null) return [];
        if (human.combat_skill_references.Count == 0)
        {
            var legacy = human.combat_skill_slot_paths.ToList();
            if (legacy.Count == 0 && !human.combat_skill_slots_configured)
                legacy = get_default_combat_skill_paths().Concat(human.loaded_skill_paths).Distinct().Take(10).ToList();
            human.combat_skill_references = new Array<CombatSkillReference>(legacy.Take(10).Select(CombatSkillReference.FromKey));
        }
        while (human.combat_skill_references.Count < 10) human.combat_skill_references.Add(new());
        var result = new Array<string>(); var seen = new HashSet<string>();
        for (var i = 0; i < 10; i++)
        {
            var key = get_base_combat_skill_path(human.combat_skill_references[i]?.Key ?? "");
            if (key != (human.combat_skill_references[i]?.Key ?? "")) human.combat_skill_references[i] = CombatSkillReference.FromKey(key);
            // 下架技能在显示与战斗快照中留空，原存档引用保留，恢复开放后可继续使用。
            if (key.Length > 0 && !key.StartsWith("artifact:", StringComparison.Ordinal)
                && ResourceLoader.Exists(key) && ResourceLoader.Load<ActiveSkillData>(key) is { } archived
                && !GongfaAvailability.IsSkillEnabled(archived)) { result.Add(""); continue; }
            var skill = key.Length > 0 ? resolve_combat_skill(key) : null;
            if (key.Length > 0 && (!seen.Add(key) || skill is null || (i == 9 && has_xuanshu_heart_method_equipped() && CardRules.IsResponse(skill))))
            { key = ""; human.combat_skill_references[i] = new(); }
            result.Add(key);
        }
        return result;
    }
    public bool set_combat_skill_slot_paths(Array<string> paths)
    {
        if (HasMeta("artifact_battle_clone")) return false;
        if (human is null) return false;
        var next = new Array<CombatSkillReference>(); var seen = new HashSet<string>();
        foreach (var (path, index) in paths.Take(10).Select((path, index) => (path, index)))
        {
            var key = get_base_combat_skill_path(path);
            var skill = key.Length > 0 ? resolve_combat_skill(key) : null;
            if (key.Length > 0 && (!seen.Add(key) || skill is null || (index == 9 && has_xuanshu_heart_method_equipped() && CardRules.IsResponse(skill)))) key = "";
            next.Add(CombatSkillReference.FromKey(key));
        }
        while (next.Count < 10) next.Add(new());
        human.combat_skill_slots_configured = true;
        human.combat_skill_references = next;
        human.combat_skill_slot_paths = new Array<string>(next.Select(slot => slot.Key));
        return true;
    }
    public bool is_battle_skill_equipped(ActiveSkillData skill) => human is not null && (!human.battle_deck_configured || human.loaded_skill_paths.Any(path => resolve_combat_skill_slot_path(path) == skill.ResourcePath));
    public bool toggle_battle_skill(ActiveSkillData skill, int deck_limit = 40)
    {
        if (human is null || skill is null || string.IsNullOrEmpty(skill.ResourcePath)) return false;
        var loadedSourcePath = human.loaded_skill_paths.FirstOrDefault(path => resolve_combat_skill_slot_path(path) == skill.ResourcePath);
        if (!string.IsNullOrEmpty(loadedSourcePath)) { human.loaded_skill_paths.Remove(loadedSourcePath); return false; }
        if (!human.battle_deck_configured)
        {
            human.loaded_skill_paths.Clear();
            foreach (var card in get_available_battle_skills()) if (!string.IsNullOrEmpty(card.ResourcePath) && !human.loaded_skill_paths.Contains(card.ResourcePath)) human.loaded_skill_paths.Add(card.ResourcePath);
            human.battle_deck_configured = true;
        }
        if (human.loaded_skill_paths.Contains(skill.ResourcePath)) { human.loaded_skill_paths.Remove(skill.ResourcePath); return true; }
        if (CultivationDeckBuilder.collect_cards(this).Count >= deck_limit) return false;
        human.loaded_skill_paths.Add(skill.ResourcePath); return true;
    }
    /// <summary>研读取决于人的悟性、神魂与灵慧；灵根只决定能否吐纳，不干预读书式参悟。</summary>
    public float get_learning_rate()=>Mathf.Max(.01f,.55f+comprehension*.030f+soul*.012f+intelligence*.015f);
    /// <summary>当前环境中的最终纳气速率，单位为灵力缕/小时。</summary>
    public float get_cultivation_rate()
    {
        if(spirit_root_type==0||main_gongfa is null||!main_gongfa.is_wuxing_compatible(spirit_root_type))return 0;
        return get_cultivation_rate_at_pressure(get_current_lingqi_pressure());
    }
    public float get_cultivation_rate_at_pressure(float pressure)
    {
        if(spirit_root_type==0||main_gongfa is null||!main_gongfa.is_wuxing_compatible(spirit_root_type))return 0;
        const float baseRate=.0050f;
        // 功法章节的 cultivation_rate 是旧方案遗留；修炼快慢只由境界、灵根与灵力环境决定。
        return Mathf.Max(0,baseRate*get_realm_cultivation_multiplier()*get_element_efficiency(main_gongfa)*get_cultivation_environment_factor(pressure));
    }
    public float get_cultivation_progress()=>human?.cultivation_progress??0f;
    public Dictionary get_next_realm_progress()
    {
        var realms=GetNodeOrNull<RealmManager>("/root/RealmManager"); var next=realms?.get_next_realm_name(cultivation_realm)??"";
        var need=string.IsNullOrEmpty(next)?0:realms?.get_effective_requirements_for_next_realm(this).GetValueOrDefault("spirit_strands",0).AsInt32()??0;
        var origin=realms?.get_current_layer_spirit_origin(this,cultivation_realm)??0;
        return new Dictionary{{"next_realm",next},{"current",Mathf.Max(0,spirit_strands-origin)},{"required",Mathf.Max(0,need-origin)},{"partial",get_cultivation_progress()},{"is_current_layer",origin>0}};
    }
    /// <summary>主修功法的灵根倍率：灵根数量资质修正 × 五行匹配压缩系数。</summary>
    public float get_element_efficiency(GongfaResource? gongfa)
    {
        if(gongfa is null||spirit_root_type==0||!gongfa.is_wuxing_compatible(spirit_root_type))return 0;
        var quality=get_root_quality_multiplier();
        // 通用法门不挑五行，但仍应保留单、双、三、四、五灵根的先天差距。
        if(gongfa.wuxing is GongfaResource.Wuxing.NONE or GongfaResource.Wuxing.ALL)return quality;
        var required=gongfa.wuxing.ToString().Split('_');
        var weights=required.Select(get_effective_root_weight).ToArray();
        if(weights.Length==0||weights.Any(weight=>weight<=0))return 0;
        var raw=gongfa.wuxing_requirement_mode==GongfaResource.WuxingRequirementMode.AND?weights.Average():weights.Max();
        var match=.60f+raw*.80f;
        return quality*match;
    }
    public float get_current_lingqi_pressure()
    {
        if (!meditation_spirit_stone_supply || meditation_room_index < 0 || !has_spirit_stones_for_meditation()) return ambient_lingqi_pressure;
        return Mathf.Max(ambient_lingqi_pressure, 180f);
    }
    public bool has_spirit_stones_for_meditation()
    {
        foreach(var node in GetTree().GetNodesInGroup("main_scene")) if(node is MainScene main) return main.get_estate_resource_amount(EstateResourceSystem.SpiritStones)>0;
        return false;
    }
    public bool assign_meditation_room(int roomIndex)
    {
        if(human is null||roomIndex<0||is_in_breakthrough_retreat())return false;
        human.meditation_room_index=roomIndex; return true;
    }
    public void leave_meditation_room(){if(human is null||is_in_breakthrough_retreat())return;human.meditation_room_index=-1;human.meditation_spirit_stone_supply=false;human.meditation_spirit_stone_debt=0;}
    public void set_meditation_spirit_stone_enabled(bool enabled){if(human is null||meditation_room_index<0)return;human.meditation_spirit_stone_supply=enabled;}
    public float get_spirit_stone_consumption_estimate(float hours)=>meditation_room_index<0?0f:get_cultivation_rate_at_pressure(Mathf.Max(ambient_lingqi_pressure,180f))*Mathf.Max(0,hours)/SpiritStoneStoredLingqi;
    public float get_cultivation_environment_factor()
        =>get_cultivation_environment_factor(get_current_lingqi_pressure());
    public float get_cultivation_environment_factor(float pressure)
    {
        var required=get_realm_required_lingqi_pressure();
        var effectivePressure=Mathf.Max(0,pressure);
        // 先看环境是否满足当前境界的基本吐纳条件，再把富余灵力折为增益；总效能最多四倍。
        var adaptation=Mathf.Min(1f,effectivePressure/required);
        var surplus=Mathf.Min(4f,1f+Mathf.Max(0f,effectivePressure-required)/required*.75f);
        return Mathf.Min(4f,adaptation*surplus);
    }
    public (float adaptation,float surplus,float factor) get_cultivation_environment_breakdown(float pressure)
    {
        var required=get_realm_required_lingqi_pressure();
        var effectivePressure=Mathf.Max(0,pressure);
        var adaptation=Mathf.Min(1f,effectivePressure/required);
        var surplus=Mathf.Min(4f,1f+Mathf.Max(0f,effectivePressure-required)/required*.75f);
        return (adaptation,surplus,Mathf.Min(4f,adaptation*surplus));
    }
    public float get_effective_root_weight(string element)
    {
        var index=System.Array.IndexOf(new[]{"METAL","WOOD","WATER","FIRE","EARTH"},element);
        if(index<0||!has_effective_root(element))return 0;
        var weights=human?.root_weights;
        if(weights is not null&&weights.Count>index&&weights.Any(value=>value.AsSingle()>0))return Mathf.Clamp(weights[index].AsSingle()/100f,0,1);
        var count=get_effective_root_elements().Length;
        return count>0?1f/count:0;
    }
    private bool has_effective_root(string element)=>get_effective_root_elements().Contains(element);
    private string[] get_effective_root_elements()
    {
        if(spirit_root_type==0)return [];
        var encoded=((GongfaResource.Wuxing)spirit_root_type).ToString();
        return encoded.Split('_').Where(value=>value is "METAL" or "WOOD" or "WATER" or "FIRE" or "EARTH").ToArray();
    }
    private float get_root_quality_multiplier()=>get_effective_root_elements().Length switch{1=>2.55f,2=>1.38f,3=>1f,4=>.58f,5=>.35f,_=>0f};
    public float get_realm_cultivation_multiplier()=>get_realm_cultivation_multiplier_for_tier(get_realm_tier());
    public static float get_realm_cultivation_multiplier_for_tier(int tier)=>tier switch{1=>1f,2=>1.04f,3=>1.08f,4=>1.13f,5=>1.18f,6=>1.24f,7=>1.31f,8=>1.38f,9=>1.46f,10=>1.55f,11=>1.65f,12=>1.76f,13=>1.88f,_=>1f};
    private float get_realm_required_lingqi_pressure()=>get_realm_tier() switch{1=>100f,2=>108f,3=>116f,4=>124f,5=>132f,6=>140f,7=>150f,8=>160f,9=>170f,10=>180f,11=>188f,12=>195f,13=>200f,_=>100f};
    public float get_realm_required_lingqi_pressure_for_ui()=>get_realm_required_lingqi_pressure();
    private int get_realm_tier()
    {
        if (is_yuanxi_realm()) return 10;
        const string prefix="云息";
        if(!cultivation_realm.StartsWith(prefix,StringComparison.Ordinal)||!cultivation_realm.EndsWith("层",StringComparison.Ordinal))return 1;
        var layer=cultivation_realm[prefix.Length..^1];
        var tier=layer switch
        {
            "一"=>1,"二"=>2,"三"=>3,"四"=>4,"五"=>5,
            "六"=>6,"七"=>7,"八"=>8,"九"=>9,"十"=>10,
            _=>int.TryParse(layer,out var numericTier)?numericTier:1
        };
        return Mathf.Clamp(tier,1,13);
    }
    public bool can_cultivate()=>!is_yuanxi_realm()&&spirit_root_type!=0&&main_gongfa is not null&&main_gongfa.is_wuxing_compatible(spirit_root_type)&&!is_cultivation_blocked_by_breakthrough()&&!is_in_breakthrough_retreat();
    public int get_available_cultivation_hours(int requestedHours) => get_available_world_action_hours(WorldStaminaRules.CultivationCostPerGameHour, requestedHours);
    public int get_available_study_hours(int requestedHours) => get_available_world_action_hours(WorldStaminaRules.StudyCostPerGameHour, requestedHours);
    public Dictionary get_world_cultivation_preview_for_strands(int strands)
    {
        var wanted = Mathf.Clamp(strands, 0, get_strands_until_next_realm());
        var continuousHours = get_cultivation_hours_for_strands(wanted);
        var requestedHours = wanted <= 0 || double.IsInfinity(continuousHours) ? 0 : Mathf.CeilToInt((float)continuousHours);
        var hours = get_available_cultivation_hours(requestedHours);
        var simulation = simulate_cultivation_hours(hours);
        var actual = Mathf.Min(wanted, simulation.WholeStrands);
        if (actual <= 0) hours = 0;
        return new Dictionary { ["strands"] = actual, ["requested_strands"] = wanted, ["hours"] = hours, ["days"] = hours / 24f, ["rate"] = hours > 0 ? simulation.AverageRate : get_cultivation_rate_after_bottleneck(), ["stamina_cost"] = hours * WorldStaminaRules.CultivationCostPerGameHour, ["stamina_limited"] = actual < wanted };
    }
    public Dictionary get_world_cultivation_preview_for_hours(int requestedHours)
    {
        var hours = get_available_cultivation_hours(Mathf.Max(0, requestedHours));
        var simulation = simulate_cultivation_hours(hours);
        var actual = Mathf.Min(get_strands_until_next_realm(), simulation.WholeStrands);
        return new Dictionary { ["strands"] = actual, ["requested_strands"] = get_strands_until_next_realm(), ["hours"] = hours, ["requested_hours"] = Mathf.Max(0, requestedHours), ["days"] = hours / 24f, ["rate"] = hours > 0 ? simulation.AverageRate : get_cultivation_rate_after_bottleneck(), ["stamina_cost"] = hours * WorldStaminaRules.CultivationCostPerGameHour, ["stamina_limited"] = hours < requestedHours };
    }
    public Dictionary cultivate_with_world_stamina_for_strands(int requestedStrands)
    {
        if (!can_cultivate()) return new Dictionary { ["success"] = false };
        var preview = get_world_cultivation_preview_for_strands(requestedStrands);
        var strands = preview["strands"].AsInt32(); var hours = preview["hours"].AsInt32(); var staminaCost = preview["stamina_cost"].AsSingle();
        if (strands <= 0 || hours <= 0 || !try_spend_world_stamina(staminaCost)) return new Dictionary { ["success"] = false, ["reason"] = "精力不足" };
        var simulation = simulate_cultivation_hours(hours);
        apply_cultivation_simulation(simulation);
        var remainingProgress = simulation.TotalProgress - strands;
        human!.cultivation_progress = (float)Math.Clamp(remainingProgress, 0d, .999999d);
        spirit_strands += strands;
        GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.add_hours(hours);
        return new Dictionary { ["success"] = true, ["strands"] = strands, ["hours"] = hours, ["stamina_spent"] = staminaCost };
    }
    public Dictionary cultivate_with_world_stamina_for_hours(int requestedHours)
    {
        if (!can_cultivate()) return new Dictionary { ["success"] = false };
        var preview = get_world_cultivation_preview_for_hours(requestedHours);
        var strands = preview["strands"].AsInt32(); var hours = preview["hours"].AsInt32(); var staminaCost = preview["stamina_cost"].AsSingle();
        if (hours <= 0 || !try_spend_world_stamina(staminaCost)) return new Dictionary { ["success"] = false, ["reason"] = "精力不足" };
        var simulation = simulate_cultivation_hours(hours);
        apply_cultivation_simulation(simulation);
        if (simulation.WholeStrands > strands)
            human!.cultivation_progress = (float)simulation.FractionalProgress;
        else
            human!.cultivation_progress = (float)Math.Clamp(simulation.TotalProgress - strands, 0d, .999999d);
        if (strands > 0) spirit_strands += strands;
        GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.add_hours(hours);
        return new Dictionary { ["success"] = true, ["strands"] = strands, ["hours"] = hours, ["stamina_spent"] = staminaCost };
    }
    public Dictionary study_for_world_hours(int requestedHours)
    {
        if (human is null || requestedHours <= 0) return new Dictionary { ["success"] = false };
        var hours = get_available_study_hours(requestedHours);
        if (hours <= 0) return new Dictionary { ["success"] = false, ["reason"] = "精力不足", ["hours"] = 0 };
        if (_study_books.Count == 0 && !start_pending_intro_gongfa_study()) return new Dictionary { ["success"] = false };
        var book = _study_books.FirstOrDefault();
        if (book is null || (!book.is_studying_now && !book.start_study(this))) return new Dictionary { ["success"] = false };
        var staminaCost = hours * WorldStaminaRules.StudyCostPerGameHour;
        if (hours <= 0 || !try_spend_world_stamina(staminaCost)) return new Dictionary { ["success"] = false, ["reason"] = "精力不足", ["hours"] = 0 };
        var before = book.current_study_progress;
        var chapterId = book.studying_chapter_id;
        var requiredProgress = book.get_required_study_progress();
        var multiplier = consume_study_action_inspiration();
        advance_study_for_world_action(book, hours, multiplier);
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        time?.add_hours(hours);
        var completed = !book.is_studying_now;
        var progressGained = completed
            ? Mathf.Max(0f, requiredProgress - before)
            : Mathf.Max(0f, book.current_study_progress - before);
        if (progressGained > 0f)
            award_study_action_inspiration(book, chapterId, time?.get_total_game_hours() ?? 0);
        return new Dictionary { ["success"] = true, ["hours"] = hours, ["requested_hours"] = requestedHours, ["stamina_spent"] = staminaCost, ["progress_gained"] = progressGained, ["inspiration_multiplier"] = multiplier };
    }
    public bool manual_cultivate_with_world_stamina()
    {
        return manual_cultivate();
    }
    public bool is_cultivation_blocked_by_breakthrough(){var realms=GetNodeOrNull<RealmManager>("/root/RealmManager");return realms is not null&&!string.IsNullOrEmpty(realms.get_next_realm_name(cultivation_realm))&&spirit_strands>=realms.get_effective_requirements_for_next_realm(this).GetValueOrDefault("spirit_strands",int.MaxValue).AsInt32();}
    public void start_cultivation_session(float hours){if(can_cultivate())_session_hours=Mathf.Max(hours,0);}
    public void stop_cultivation_session()=>_session_hours=0;
    public bool is_cultivation_session_active()=>_session_hours>0;
    public float get_cultivation_session_remaining()=>_session_hours;
    public float get_next_strand_progress()=>0;
    /// <summary>手动吐纳按一小时结算，并保留未满一缕的小数进度。</summary>
    public bool manual_cultivate(){if(!can_cultivate()||!try_spend_world_stamina(WorldStaminaRules.CultivationCostPerGameHour))return false;advance_cultivation_hours(1f);GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.add_hours(1);return true;}
    public override void _Process(double delta){base._Process(delta);}
    /// <summary>供人物页与功法页复用的研读摘要。</summary>
    public Dictionary get_current_study_summary()
    {
        if (_study_books.Count == 0 || !GodotObject.IsInstanceValid(_study_books[0])) return new Dictionary{{"active",false}};
        var book = _study_books[0]; var required = book.get_required_study_progress(); var chapter = book.gongfa?.get_chapter_by_id(book.studying_chapter_id);
        var rate = get_learning_rate(); var remaining = Mathf.Max(0, required - book.current_study_progress);
        return new Dictionary{{"active",true},{"gongfa_name",book.gongfa?.gongfa_name??"无名秘籍"},{"chapter_id",book.studying_chapter_id},{"chapter_name",chapter?.chapter_name??book.studying_chapter_id},{"progress",book.current_study_progress},{"required",required},{"remaining",remaining},{"rate",rate}};
    }
    public bool is_exhausted_for_work()=>current_stamina<=0; public int spend_stamina(int amount,bool refresh_ui=true){var spent=Mathf.Min(Mathf.Max(amount,0),current_stamina);current_stamina-=spent;return spent;} public int spend_stamina_for_action(string action,float hours)=>spend_stamina(Mathf.CeilToInt(hours));
    public int get_strands_until_next_realm(){var progress=get_next_realm_progress();return Mathf.Max(0,progress.GetValueOrDefault("required",0).AsInt32()-progress.GetValueOrDefault("current",0).AsInt32());}
    public Dictionary get_cultivation_preview_for_strands(int strands)=>get_world_cultivation_preview_for_strands(strands);
    public Dictionary get_cultivation_preview_for_hours(int hours)=>get_world_cultivation_preview_for_hours(hours);
    public Dictionary cultivate_for_strands(int strands) => cultivate_with_world_stamina_for_strands(strands);
    public Dictionary cultivate_for_hours(int hours) => cultivate_with_world_stamina_for_hours(hours);

    // 单人模式不再提供研读位；内部只保留当前读本，以兼容已有存档进度。
    public Array<Dictionary> get_study_slot_snapshot(){var result=new Array<Dictionary>();var book=_study_books.FirstOrDefault();var slot=new Dictionary{{"slot_index",0},{"occupied",book is not null}};if(book is not null)slot["book"]=book;result.Add(slot);return result;} public bool has_free_study_slot()=>_study_books.Count<1;
    public GongfaResource? get_current_study_gongfa()=>_study_books.FirstOrDefault()?.gongfa is { } book&&GongfaAvailability.IsEnabled(book)?book:null;
    /// <summary>单人模式只有一部当前读本。切换时暂停原书，并保留其已累计的卷章进度。</summary>
    public bool select_gongfa_for_study(GongfaResource gongfa)
    {
        if (gongfa is null || !GongfaAvailability.IsEnabled(gongfa) || human is null) return false;
        var current = get_current_study_gongfa();
        if (current?.ResourcePath == gongfa.ResourcePath) return true;
        // 先校验新读本能否开始，成功后再暂停原读本，避免一次无效切换丢失当前进度。
        var next = new SecretBook { gongfa = gongfa, required_study_progress = gongfa.required_study_progress, can_be_interrupted = false, Name = "Study_" + gongfa.gongfa_name };
        AddChild(next);
        if (!next.start_study(this)) { next.QueueFree(); return false; }
        if (get_pending_intro_gongfa()?.ResourcePath == gongfa.ResourcePath)
            next.current_study_progress = Mathf.Clamp(human.intro_gongfa_study_progress, 0f, next.required_study_progress);
        if (_study_books.Count > 0) remove_from_study_slot(0);
        _study_books.Add(next); StudyStarted?.Invoke(this, gongfa);
        return true;
    }
    public bool try_start_secret_book_study(SecretBook book){if(book is null||!has_free_study_slot()||!book.start_study(this))return false;_study_books.Add(book);if(book.gongfa is not null)StudyStarted?.Invoke(this,book.gongfa);return true;} public bool remove_from_study_slot(int index){if(index<0||index>=_study_books.Count)return false;_study_books[index].pause_study();_study_books.RemoveAt(index);return true;} public bool is_studying()=>_study_books.Count>0;
    /// <summary>将已掌握秘籍设为当前读本；未满足下一卷条件时保留其进度。</summary>
    public bool arrange_gongfa_study(GongfaResource gongfa)
    {
        if (human is null || gongfa is null || !GongfaAvailability.IsEnabled(gongfa) || !has_free_study_slot() || _study_books.Any(book => book.gongfa == gongfa)) return false;
        var book = new SecretBook { gongfa = gongfa, required_study_progress = gongfa.required_study_progress, can_be_interrupted = false, Name = "Study_" + gongfa.gongfa_name };
        AddChild(book); _study_books.Add(book);
        book.start_study(this); // 条件未达标时保留当前读本，不丢失去向。
        StudyStarted?.Invoke(this, gongfa);
        return true;
    }
    /// <summary>序章交付选定秘籍的首卷。</summary>
    public bool grant_intro_gongfa(GongfaResource gongfa)
    {
        if (human is null || gongfa is null || !GongfaAvailability.IsEnabled(gongfa) || !gongfa.can_be_main) return false;
        var first=gongfa.get_sorted_chapters().FirstOrDefault(); if(first is null)return false;
        human.intro_gongfa_path=gongfa.ResourcePath;
        // 玄镜已点明首卷大半；玩家仍须亲自在静室完成余下部分。
        human.intro_gongfa_study_progress=first.required_study_progress * 0.8f;
        set_gongfa_chapter_study_progress(gongfa,first.get_id(),human.intro_gongfa_study_progress);
        // 启明只教第一卷；后续卷章要等云息一层后由玩家主动决定，不能被通用日程越过。
        study_auto_next_chapter = false;
        return true;
    }
    public GongfaResource? get_pending_intro_gongfa()
    {
        if(human is null||string.IsNullOrEmpty(human.intro_gongfa_path))return null;
        var gongfa=load_gongfa(human.intro_gongfa_path);
        return gongfa is null||has_learned_gongfa(gongfa)||_study_books.Any(book=>book.gongfa==gongfa)?null:gongfa;
    }
    /// <summary>序章只要求读成玄鉴传下的第一卷；后续卷章由玩家日后自行选择研读。</summary>
    public bool has_completed_intro_gongfa_first_volume()
    {
        if(human is null||string.IsNullOrEmpty(human.intro_gongfa_path))return false;
        var gongfa=load_gongfa(human.intro_gongfa_path); var first=gongfa?.get_sorted_chapters().FirstOrDefault();
        return gongfa is not null&&first is not null&&is_gongfa_chapter_learned(gongfa,first.get_id());
    }
    public bool start_pending_intro_gongfa_study()
    {
        var gongfa=get_pending_intro_gongfa(); if(gongfa is null||!has_free_study_slot())return false;
        var book=new SecretBook{gongfa=gongfa,required_study_progress=gongfa.required_study_progress,can_be_interrupted=false,Name="IntroStudy_"+gongfa.gongfa_name}; AddChild(book);
        if(!book.start_study(this)){book.QueueFree();return false;}
        var carried = human?.intro_gongfa_study_progress ?? 0f;
        book.current_study_progress=Mathf.Clamp(carried,0f,book.required_study_progress); _study_books.Add(book); StudyStarted?.Invoke(this,gongfa); return true;
    }
    /// <summary>静室中的主动研读。没有当前读本时，会接续玄鉴传下的首卷。</summary>
    public Dictionary study_for_hours(int hours)
    {
        if(human is null||hours<=0)return new Dictionary{{"success",false}};
        if(_study_books.Count==0&&!start_pending_intro_gongfa_study())return new Dictionary{{"success",false}};
        var book=_study_books.FirstOrDefault(); if(book is null)return new Dictionary{{"success",false}};
        if(!book.is_studying_now&&!book.start_study(this))return new Dictionary{{"success",false}};
        var actualHours=get_available_study_hours(hours);var staminaCost=actualHours*WorldStaminaRules.StudyCostPerGameHour;
        if(actualHours<=0||!try_spend_world_stamina(staminaCost))return new Dictionary{{"success",false},{"reason","精力不足"},{"hours",0}};
        var before=book.current_study_progress; var chapterId=book.studying_chapter_id; var requiredProgress=book.get_required_study_progress();
        var multiplier=consume_study_action_inspiration(); advance_study_for_world_action(book, actualHours, multiplier);
        var time=GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager"); time?.add_hours(actualHours);
        var completed=!book.is_studying_now; var progressGained=completed?Mathf.Max(0,requiredProgress-before):Mathf.Max(0,book.current_study_progress-before);
        if(progressGained>0)award_study_action_inspiration(book,chapterId,time?.get_total_game_hours()??0);
        return new Dictionary{{"success",true},{"hours",actualHours},{"requested_hours",hours},{"stamina_spent",staminaCost},{"progress_gained",progressGained},{"inspiration_multiplier",multiplier}};
    }
    /// <summary>由秘籍完成时调用：把已读章节写回角色资料，再通知剧情系统。</summary>
    public void _complete_gongfa_chapter_study(GongfaResource gongfa, string chapter_id, SecretBook book)
    {
        if (human is null || gongfa is null || string.IsNullOrWhiteSpace(chapter_id)) return;
        var chapters = human.learned_gongfa_chapters.GetValueOrDefault(gongfa.ResourcePath, new Array<string>()).AsGodotArray();
        if (!chapters.Contains(chapter_id)) chapters.Add(chapter_id);
        human.learned_gongfa_chapters[gongfa.ResourcePath] = chapters;
        human.gongfa_chapter_study_progress.Remove(gongfa.ResourcePath + ":" + chapter_id);
        if (!human.learned_gongfa_paths.Contains(gongfa.ResourcePath)) human.learned_gongfa_paths.Add(gongfa.ResourcePath);
        var chapterName = chapter_id;
        foreach (var chapter in gongfa.chapters) if (chapter.get_id() == chapter_id) { chapterName = chapter.chapter_name; break; }
        var appliedBonuses = apply_gongfa_chapter_permanent_bonuses(gongfa, chapter_id);
        refresh_gongfa_effects();
        // 订阅者收到通知时，永久属性和造诣已经入账并完成刷新。
        GongfaChapterCompleted?.Invoke(this, gongfa.gongfa_name, chapter_id, chapterName, appliedBonuses);
        // 玄鉴首卷完成后在此停下，避免序章研读动作无意续读到下一卷。
        if (gongfa.ResourcePath == human.intro_gongfa_path && gongfa.get_sorted_chapters().FirstOrDefault()?.get_id() == chapter_id) { _study_books.Remove(book); book.pause_study(); return; }
        // 后续秘籍同样不自动续读，等待玩家下一次主动研读。
        var hasNextChapter = gongfa.get_sorted_chapters().Any(next => !is_gongfa_chapter_learned(gongfa, next.get_id()));
        if (hasNextChapter) { book.pause_study(); return; }
        _study_books.Remove(book);
    }
    private void apply_missing_gongfa_permanent_bonuses()
    {
        if (human is null) return;
        foreach (var path in human.learned_gongfa_paths)
        {
            var book = load_gongfa(path); if (book is null) continue;
            foreach (var idValue in human.learned_gongfa_chapters.GetValueOrDefault(path, new Array<string>()).AsGodotArray()) apply_gongfa_chapter_permanent_bonuses(book, idValue.AsString());
        }
    }
    private Dictionary apply_gongfa_chapter_permanent_bonuses(GongfaResource gongfa, string chapterId)
    {
        Dictionary applied = [];
        if (human is null) return applied;
        var stamp = gongfa.ResourcePath + ":" + chapterId;
        var chapter = gongfa.get_chapter_by_id(chapterId); if (chapter is null) return applied;
        var revisionStamp = string.IsNullOrWhiteSpace(chapter.learn_bonus_revision)
            ? "" : stamp + ":" + chapter.learn_bonus_revision;
        if (human.applied_gongfa_permanent_bonuses.ContainsKey(stamp))
        {
            if (revisionStamp.Length > 0 && !human.applied_gongfa_permanent_bonuses.ContainsKey(revisionStamp))
            {
                foreach (Variant key in chapter.learn_permanent_bonuses.Keys)
                {
                    var previous = chapter.previous_learn_permanent_bonuses.GetValueOrDefault(key, 0f).AsSingle();
                    var difference = chapter.learn_permanent_bonuses[key].AsSingle() - previous;
                    if (difference > 0f)
                    {
                        apply_permanent_attribute(key.AsString(), difference);
                        add_applied_bonus(applied, key.AsString(), difference);
                    }
                }
                human.applied_gongfa_permanent_bonuses[revisionStamp] = true;
            }
            return applied;
        }
        foreach (Variant key in chapter.learn_permanent_bonuses.Keys)
        {
            var amount = chapter.learn_permanent_bonuses[key].AsSingle();
            apply_permanent_attribute(key.AsString(), amount);
            add_applied_bonus(applied, key.AsString(), amount);
        }
        var bookStamp = gongfa.ResourcePath + ":book";
        if (!human.applied_gongfa_permanent_bonuses.ContainsKey(bookStamp))
        {
            foreach (Variant key in gongfa.learn_permanent_bonuses.Keys)
            {
                var amount = gongfa.learn_permanent_bonuses[key].AsSingle();
                apply_permanent_attribute(key.AsString(), amount);
                add_applied_bonus(applied, key.AsString(), amount);
            }
            human.applied_gongfa_permanent_bonuses[bookStamp] = true;
        }
        human.applied_gongfa_permanent_bonuses[stamp] = true;
        if (revisionStamp.Length > 0) human.applied_gongfa_permanent_bonuses[revisionStamp] = true;
        return applied;
    }
    private static void add_applied_bonus(Dictionary applied, string key, float amount)
    {
        if (Mathf.IsZeroApprox(amount)) return;
        applied[key] = applied.GetValueOrDefault(key, 0f).AsSingle() + amount;
    }
    private void apply_permanent_attribute(string key, float value)
    {
        if (human is null || Mathf.IsZeroApprox(value)) return;
        switch (key)
        {
            case "physique": human.base_physique += value; break;
            case "intelligence": human.base_intelligence += value; break;
            case "agility": human.base_physique += value * .5f; human.base_soul += value * .5f; break;
            case "soul": human.base_soul += value; break;
            case "fortune": human.base_fortune += value; break;
            case "comprehension": human.base_comprehension += value; break;
            case "body_proficiency": human.base_body_proficiency += value; break;
            case "magic_proficiency": human.base_magic_proficiency += value; break;
            case "sword_proficiency": human.base_sword_proficiency += value; break;
            case "forging_proficiency": human.base_forging_proficiency += value; break;
            case "talisman_proficiency": human.base_talisman_proficiency += value; break;
            case "formation_proficiency": human.base_formation_proficiency += value; break;
            case "alchemy_proficiency": human.base_alchemy_proficiency += value; break;
        }
    }
    /// <summary>重算装配功法的纳气固定加成；永久加成已写入基础值，不在这里重复累积。</summary>
    public void refresh_gongfa_effects()
    {
        if (human is null) return;
        recalculate_attributes();
        // 以下四项不是 recalculate_attributes 的派生值；每次重算先归回战斗基础，
        // 再叠加已装配功法，避免打开页面或换装时反复累加。
        physical_defense = 0;
        physical_damage_reduction = 0f;
        healing_received_bonus = 0f;
        body_qi_blood_bonus = 0;
        crit_damage_mult = 1.5f;
        initiative_speed = get_base_initiative_speed();
        blood_furnace_value_cap = 100;
        human.sword_proficiency = human.base_sword_proficiency; human.blade_proficiency = human.base_blade_proficiency; human.spear_proficiency = human.base_spear_proficiency;
        human.body_proficiency = human.base_body_proficiency; human.magic_proficiency = human.base_magic_proficiency;
        human.forging_proficiency = calculate_realm_base_attribute("forging_proficiency", human.base_forging_proficiency) + human.forging_proficiency_training_bonus;
        human.talisman_proficiency = human.base_talisman_proficiency; human.formation_proficiency = human.base_formation_proficiency;
        human.alchemy_proficiency = calculate_realm_base_attribute("alchemy_proficiency", human.base_alchemy_proficiency) + human.alchemy_proficiency_training_bonus;
        ResetAttributeCalculations();
        foreach (var book in new[] { main_gongfa }.Concat(sub_gongfa_list))
        {
            if (book is null) continue; var isMain = book == main_gongfa;
            foreach (var chapter in book.get_sorted_chapters())
            {
                if (!is_gongfa_chapter_learned(book, chapter.get_id())) continue;
                using (TraceAttributeSource($"{book.gongfa_name}·{chapter.chapter_name}"))
                    foreach (Variant key in chapter.get_naqi_bonuses(isMain).Keys) apply_runtime_gongfa_bonus(key.AsString(), chapter.get_naqi_bonuses(isMain)[key].AsSingle());
            }
        }
        human.mana_regen_rate = human.base_mana_regen_rate * get_gongfa_naqi_multiplier("mana_regen_rate");
        human.mind_regen_rate = human.base_mind_regen_rate * get_gongfa_naqi_multiplier("mind_regen_rate");
        apply_body_refinement_rewards();
        apply_forged_equipment_base_bonuses();
        // 首批宗门凡器仅提供固定战斗属性；重算基线后应用，避免刷新叠加。
        foreach (var path in new[] { human.weapon_path })
            if (!string.IsNullOrWhiteSpace(path))
            if (ResourceLoader.Load<EquipmentData>(path) is { } equipment && equipment.definition_id.ToString().StartsWith("sect_sword_", StringComparison.Ordinal))
                using (TraceAttributeSource(equipment.entity_name))
                foreach (Variant key in equipment.fixed_bonuses.Keys)
                    apply_runtime_gongfa_bonus(key.AsString(), equipment.fixed_bonuses[key].AsSingle());
        TraceArtifactBondDefense(apply_artifact_bond_defense);
    }

    private void apply_body_refinement_rewards()
    {
        if (human is null) return;
        foreach (var part in BodyRefinementRules.Parts)
        {
            using var attributeSource = TraceAttributeSource("炼体·" + BodyRefinementRules.GetName(part));
            var progress = human.body_refinement_progress.GetValueOrDefault(part, 0f).AsSingle();
            var reward = BodyRefinementRules.GetReward(part, BodyRefinementRules.GetLevel(part, progress));
            physical_defense += reward.PhysicalDefense;
            dodge += reward.Dodge;
            max_health += reward.MaxHealth;
            body_qi_blood_bonus += reward.QiBlood;
            crit_resist += reward.CritResist;
            healing_received_bonus += reward.HealingBonus;
            physical_damage_reduction += reward.PhysicalDamageReduction;
        }
        physical_damage_reduction = Mathf.Clamp(physical_damage_reduction, 0f, .90f);
        current_health = Mathf.Min(current_health, max_health);
    }

    private void apply_runtime_gongfa_bonus(string key, float value)
    {
        if (human is null) return;
        // dodge 与 Godot 生成的成员名在部分编译器版本中会与 switch 标签冲突，单独处理。
        if (key == "dodge") { dodge += Mathf.RoundToInt(value); return; }
        switch (key)
        {
            case "physique": physique += value; break; case "intelligence": intelligence += value; break; case "agility": physique += value * .5f; soul += value * .5f; break; case "soul": soul += value; break;
            case "physical_defense": physical_defense += Mathf.RoundToInt(value); break; case "precision": precision += Mathf.RoundToInt(value); break;
            case "critical": critical += Mathf.RoundToInt(value); break;
            case "crit_resist": crit_resist += Mathf.RoundToInt(value); break; case "crit_damage_mult": crit_damage_mult += value; break;
            case "initiative_speed": initiative_speed += value; break; case "max_spirit_perception": max_spirit_perception += Mathf.RoundToInt(value); break;
            case "max_health": max_health += Mathf.RoundToInt(value); current_health = Mathf.Min(current_health, max_health); break;
            case "max_qi_blood": max_qi_blood += Mathf.RoundToInt(value); break;
            case "blood_furnace_value_cap": blood_furnace_value_cap += Mathf.RoundToInt(value); break;
            case "body_proficiency": human.body_proficiency += value; break; case "magic_proficiency": human.magic_proficiency += value; break; case "sword_proficiency": human.sword_proficiency += value; break;
            case "forging_proficiency": human.forging_proficiency += value; break; case "alchemy_proficiency": human.alchemy_proficiency += value; break;
        }
    }
    private float get_gongfa_naqi_multiplier(string key)
    {
        var total = 0f;
        foreach (var book in new[] { main_gongfa }.Concat(sub_gongfa_list))
        {
            if (book is null) continue; var isMain = book == main_gongfa;
            foreach (var chapter in book.get_sorted_chapters()) if (is_gongfa_chapter_learned(book, chapter.get_id())) total += chapter.get_naqi_multipliers(isMain).GetValueOrDefault(key, 0).AsSingle();
        }
        return 1f + total;
    }
    /// <summary>供战斗结算读取的功法物理伤害倍率；只统计已研读且已装配的卷章。</summary>
    public bool is_in_breakthrough_retreat()=>human?.is_in_breakthrough_retreat==true;
    /// <summary>六层至七层不走普通闭关；必须由凝核牌局结算后调用 complete_condensation。</summary>
    public bool requires_condensation_breakthrough()
    {
        var realms=GetNodeOrNull<RealmManager>("/root/RealmManager");
        return cultivation_realm=="云息六层"&&realms?.get_next_realm_name(cultivation_realm)=="云息七层";
    }
    public bool has_active_severe_injury()
    {
        if(human is null)return false;
        var hour=GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours()??0;
        return WorldStatusRules.active_records(human.world_status_records,hour).Any(record=>record.status_id==WorldStatusRules.SevereInjuryId);
    }
    public int get_active_severe_injury_remaining_rest_hours()
    {
        if (human is null) return 0;
        var hour = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
        var injury = WorldStatusRules.active_records(human.world_status_records, hour)
            .FirstOrDefault(record => record.status_id == WorldStatusRules.SevereInjuryId);
        return injury is null ? 0 : Math.Max(0, injury.recovery_hours_required - injury.recovery_hours_progress);
    }
    public bool can_attempt_condensation_breakthrough()=>requires_condensation_breakthrough()&&is_breakthrough_ready()&&!has_active_severe_injury();
    /// <summary>正式八回合凝核成功后的唯一写回点。</summary>
    public bool complete_condensation(int finalScore)
    {
        if(finalScore<CondensationOutcomeRules.SuccessScore)return false;
        return commit_condensation(finalScore,CondensationCoreState.MinimumVolume,CondensationCoreState.MinimumDensity,0f,"八回合凝核");
    }
    /// <summary>旧凝核验收夹具兼容入口；成功结果按最低成核线记账。</summary>
    public bool complete_condensation(float volume,float density,float impurity,string quality)
    {
        if(impurity>=CondensationCoreState.FailureImpurity)return false;
        return commit_condensation(CondensationOutcomeRules.SuccessScore,volume,density,impurity,quality);
    }
    private bool commit_condensation(int finalScore,float volume,float density,float impurity,string quality)
    {
        if(human is null||!can_attempt_condensation_breakthrough()||impurity>=CondensationCoreState.FailureImpurity)return false;
        human.condensation_final_score=finalScore;
        human.has_spirit_core=true;
        human.core_count=1;
        human.spirit_core_volume=Mathf.Max(CondensationCoreState.MinimumVolume,volume);
        human.spirit_core_density=Mathf.Max(CondensationCoreState.MinimumDensity,density);
        human.spirit_core_impurity=Mathf.Clamp(impurity,0f,CondensationCoreState.FailureImpurity-0.0001f);
        human.spirit_core_quality=quality;
        cultivation_realm="云息七层";
        _last_breakthrough_completion_hour = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? -1;
        refresh_gongfa_effects();
        refresh_shared_basic_skill_pool();
        EmitSignal(SignalName.breakthrough_completed,this,cultivation_realm);
        BreakthroughCompleted?.Invoke(this,cultivation_realm);
        return true;
    }
    public string get_spirit_core_summary()
    {
        if(!has_spirit_core)return "未凝";
        if(condensation_final_score>=CondensationOutcomeRules.SuccessScore)
            return $"凝核程度 {condensation_final_score}　体魄、灵慧、神魂各定额 +{get_condensation_flat_bonus("physique"):0.##}";
        var title=string.IsNullOrWhiteSpace(spirit_core_quality)?"云息灵核":spirit_core_quality;
        return $"{title}　体 {spirit_core_volume:0.00}　密 {spirit_core_density:0.00}　杂 {spirit_core_impurity:0.00}";
    }
    public bool is_breakthrough_ready(){var realms=GetNodeOrNull<RealmManager>("/root/RealmManager");return !is_in_breakthrough_retreat()&&is_cultivation_blocked_by_breakthrough()&&realms is not null&&realms.can_breakthrough(this);}
    public int get_breakthrough_base_hours()
    {
        var realms=GetNodeOrNull<RealmManager>("/root/RealmManager"); var next=realms?.get_next_realm_name(cultivation_realm)??"";
        var tier=next switch { "云息一层"=>1,"云息二层"=>2,"云息三层"=>3,"云息四层"=>4,"云息五层"=>5,"云息六层"=>6,"云息七层"=>7,"云息八层"=>8,"云息九层"=>9,"云息十层"=>10,_=>1 };
        var days=tier switch { 1=>1, 2=>2, 3=>3, 4=>5, 5=>6, 6=>8, 7=>12, 8=>10, 9=>13, 10=>18, 11=>15, 12=>20, 13=>28, _=>1 };
        return days*24;
    }
    public int get_breakthrough_retreat_hours(bool useMeditationRoom)=>get_breakthrough_retreat_hour_range(useMeditationRoom).GetValueOrDefault("base_hours",12).AsInt32();
    /// <summary>闭关时长会在此范围内随机确定；静室能缩短整段闭关所需时间。</summary>
    public Dictionary get_breakthrough_retreat_hour_range(bool useMeditationRoom)
    {
        var factor=useMeditationRoom?.80f:1f;
        var baseHours=Mathf.Max(12,Mathf.RoundToInt(get_breakthrough_base_hours()*factor));
        return new Dictionary{{"min_hours",Mathf.Max(12,Mathf.RoundToInt(baseHours*.8f))},{"max_hours",Mathf.Max(12,Mathf.RoundToInt(baseHours*1.2f))},{"base_hours",baseHours}};
    }
    public bool can_use_meditation_room_for_retreat()
    {
        foreach(var node in GetTree().GetNodesInGroup("main_scene")) if(node is MainScene main) return main.has_available_meditation_room(this);
        return false;
    }
    public bool start_breakthrough_retreat(bool useMeditationRoom=false, string selectedPillPath="")
    {
        if (human is null || !is_breakthrough_ready() || requires_condensation_breakthrough()) return false;
        var realms = GetNodeOrNull<RealmManager>("/root/RealmManager");
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        if (realms is null || time is null) return false;
        var next = realms.get_next_realm_name(cultivation_realm);
        if (string.IsNullOrEmpty(next)) return false;
        Dictionary? selectedPill = null;
        if (!string.IsNullOrEmpty(get_required_breakthrough_pill_path(next)))
        {
            var choices = get_breakthrough_pill_choices(next);
            foreach (var choice in choices)
            {
                var candidatePath = choice.GetValueOrDefault("path", "").AsString();
                if ((string.IsNullOrEmpty(selectedPillPath) && selectedPill is null) || candidatePath == selectedPillPath)
                {
                    selectedPill = choice;
                    if (!string.IsNullOrEmpty(selectedPillPath)) break;
                }
            }
            if (selectedPill is null) return false;
            selectedPillPath = selectedPill.GetValueOrDefault("path", "").AsString();
        }
        else selectedPillPath = "";
        if (useMeditationRoom && !can_use_meditation_room_for_retreat()) return false;
        if (useMeditationRoom && meditation_room_index < 0)
        {
            var assigned = false;
            foreach (var node in GetTree().GetNodesInGroup("main_scene"))
                if (node is MainScene main)
                {
                    assigned = main.assign_available_meditation_room(this);
                    break;
                }
            if (!assigned) return false;
        }
        var range = get_breakthrough_retreat_hour_range(useMeditationRoom);
        var minHours = range.GetValueOrDefault("min_hours", 12).AsInt32();
        var maxHours = range.GetValueOrDefault("max_hours", minHours).AsInt32();
        var retreatHours = new RandomNumberGenerator();
        retreatHours.Randomize();
        var actualHours = (int)retreatHours.RandiRange(minHours, maxHours);
        human.is_in_breakthrough_retreat = true;
        human.breakthrough_target_realm = next;
        human.breakthrough_started_hour = time.get_total_game_hours();
        human.breakthrough_finish_hour = human.breakthrough_started_hour + actualHours;
        human.breakthrough_uses_meditation_room = useMeditationRoom;
        human.breakthrough_pill_path = selectedPillPath;
        human.breakthrough_pill_id = selectedPill?.GetValueOrDefault("pill_id", "").AsString() ?? "";
        human.breakthrough_pill_tier = selectedPill?.GetValueOrDefault("tier", -1).AsInt32() ?? -1;
        human.breakthrough_pill_effect_snapshot = selectedPill?.GetValueOrDefault("effect", "").AsString() ?? "";
        human.breakthrough_pill_effect_value_snapshot = selectedPill?.GetValueOrDefault("effect_value", 0).AsInt32() ?? 0;
        EmitSignal(SignalName.breakthrough_started,this,next); time.add_hours(actualHours); return true;
    }
    public Dictionary get_breakthrough_retreat_summary()
    {
        if(human is null||!human.is_in_breakthrough_retreat)return new Dictionary{{"active",false}};
        var time=GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager"); var remaining=Mathf.Max(0,human.breakthrough_finish_hour-(time?.get_total_game_hours()??human.breakthrough_started_hour));
        return new Dictionary{{"active",true},{"target",human.breakthrough_target_realm},{"remaining_hours",remaining},{"total_hours",Mathf.Max(0,human.breakthrough_finish_hour-human.breakthrough_started_hour)},{"uses_meditation_room",human.breakthrough_uses_meditation_room},{"pill_path",human.breakthrough_pill_path},{"pill_effect",human.breakthrough_pill_effect_snapshot}};
    }
    private void process_breakthrough_retreat()
    {
        if(human is null||!human.is_in_breakthrough_retreat)return;
        var time=GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager"); if(time is null||time.get_total_game_hours()<human.breakthrough_finish_hour)return;
        finish_breakthrough_retreat();
    }
    private void finish_breakthrough_retreat()
    {
        if(human is null)return; var realms=GetNodeOrNull<RealmManager>("/root/RealmManager");
        var next=human.breakthrough_target_realm;
        if(string.IsNullOrEmpty(next)||realms is null||next!=realms.get_next_realm_name(cultivation_realm)||!realms.can_breakthrough(this)) { clear_breakthrough_retreat(); return; }
        var selectedPillPath = human.breakthrough_pill_path;
        var selectedPillId = human.breakthrough_pill_id;
        var selectedTier = human.breakthrough_pill_tier;
        var selectedEffectValue = human.breakthrough_pill_effect_value_snapshot;
        double toxicityBefore = 0;
        var legacyRequiredPath = get_required_breakthrough_pill_path(next);
        var consumed = string.IsNullOrEmpty(selectedPillPath)
            ? (string.IsNullOrEmpty(legacyRequiredPath) || has_inventory_item(legacyRequiredPath)) && consume_breakthrough_pill_on_success(next)
            : consume_selected_breakthrough_pill_on_success(next, selectedPillPath, selectedPillId, selectedTier, out toxicityBefore);
        if (!consumed) { clear_breakthrough_retreat(); return; }

        cultivation_realm=next;
        if (!string.IsNullOrEmpty(selectedPillPath))
        {
            if (selectedPillId == "dingmai")
                spirit_strands += PillToxicityRules.Absorb(selectedEffectValue, toxicityBefore);
            else if (selectedPillId == "guiyuan_dingxi")
            {
                human.yuanxi_duration_reduction = Math.Clamp(selectedEffectValue / 100d * PillToxicityRules.Efficiency(toxicityBefore), 0, .10);
                NotifyYuanxi();
            }
        }
        _last_breakthrough_completion_hour = human.breakthrough_finish_hour;
        clear_breakthrough_retreat(); refresh_gongfa_effects(); refresh_shared_basic_skill_pool(); EmitSignal(SignalName.breakthrough_completed,this,next); BreakthroughCompleted?.Invoke(this,next);
    }
    private void clear_breakthrough_retreat(){if(human is null)return;human.is_in_breakthrough_retreat=false;human.breakthrough_target_realm="";human.breakthrough_started_hour=0;human.breakthrough_finish_hour=0;human.breakthrough_uses_meditation_room=false;human.breakthrough_pill_path="";human.breakthrough_pill_id="";human.breakthrough_pill_tier=-1;human.breakthrough_pill_effect_snapshot="";human.breakthrough_pill_effect_value_snapshot=0;}
    /// <summary>按实际投入小时累计灵力；速率单位为“缕/小时”，小数部分进入存档而非被截断。</summary>
    private int advance_cultivation_hours(float hours)
    {
        if (human is null || hours <= 0 || !can_cultivate()) return 0;
        var simulation = simulate_cultivation_hours(hours);
        apply_cultivation_simulation(simulation);
        add_body_nourish_progress(hours);
        add_visualization_nourish_progress(hours);
        consume_meditation_spirit_stones(hours);
        var gained=simulation.WholeStrands;
        if (gained <= 0) return 0;
        spirit_strands+=gained;
        return gained;
    }

    private void add_body_nourish_progress(float hours)
    {
        if (human is null || main_gongfa is null || hours <= 0) return;
        var total = Mathf.Max(0f, main_gongfa.body_nourish_rate_per_day) * hours / 24f;
        if (total <= 0) return;
        var weights = main_gongfa.body_nourish_weights;
        var sum = 0f;
        foreach (var part in BodyRefinementRules.Parts) sum += Mathf.Max(0f, weights.GetValueOrDefault(part, 0f).AsSingle());
        if (sum <= 0)
        {
            foreach (var part in BodyRefinementRules.Parts) human.body_refinement_progress[part] = human.body_refinement_progress.GetValueOrDefault(part, 0f).AsSingle() + total / BodyRefinementRules.Parts.Length;
            return;
        }
        foreach (var part in BodyRefinementRules.Parts)
        {
            var weight = Mathf.Max(0f, weights.GetValueOrDefault(part, 0f).AsSingle());
            if (weight <= 0) continue;
            human.body_refinement_progress[part] = human.body_refinement_progress.GetValueOrDefault(part, 0f).AsSingle() + total * weight / sum;
        }
    }

    /// <summary>温观是修性养成的保底来源。当前主修景得六成，其余两景各得两成；未装配景永不被动增长。</summary>
    private void add_visualization_nourish_progress(float hours)
    {
        if (human is null || !is_visualization_cloud_breath() || hours <= 0f) return;
        ensure_visualization_state();
        const float totalRanksPerDay = .30f;
        var total = totalRanksPerDay * hours / 24f;
        foreach (var slot in VisualizationRules.Slots)
        {
            var id = human.equipped_visualizations.GetValueOrDefault(slot, "").AsString();
            if (VisualizationCatalog.Get(id) is null) continue;
            var weight = id == human.visualization_focus_id ? .60f : .20f;
            add_visualization_progress(id, total * weight);
        }
    }

    private float get_visualization_progress(string id) => human?.visualization_progress.GetValueOrDefault(id, 1f).AsSingle() ?? 1f;

    private float add_visualization_progress(string id, float amount)
    {
        if (human is null || VisualizationCatalog.Get(id) is null || amount <= 0f) return get_visualization_progress(id);
        var after = Mathf.Clamp(get_visualization_progress(id) + amount, 1f, VisualizationRules.MaxRank);
        human.visualization_progress[id] = after;
        return after;
    }

    public bool can_use_visualization() => human is not null && is_visualization_cloud_breath() && !is_in_breakthrough_retreat();

    public Dictionary get_visualization_state()
    {
        ensure_visualization_state();
        var scenes = new Array<Dictionary>();
        foreach (var scene in VisualizationCatalog.Defaults)
        {
            var progress = get_visualization_progress(scene.Id);
            var rank = VisualizationRules.GetRank(progress);
            scenes.Add(new Dictionary
            {
                ["id"] = scene.Id, ["slot"] = scene.Slot, ["slot_name"] = VisualizationRules.GetSlotName(scene.Slot),
                ["name"] = scene.Name, ["description"] = scene.Description, ["style"] = scene.Style,
                ["rank"] = rank, ["progress"] = progress, ["ratio"] = VisualizationRules.GetRankRatio(progress),
                ["node_count"] = VisualizationRules.GetCompletedNodeCount(rank), ["equipped"] = human?.equipped_visualizations.GetValueOrDefault(scene.Slot, "").AsString() == scene.Id,
                ["focused"] = human?.visualization_focus_id == scene.Id,
                ["soul_bonus"] = rank * scene.SoulPerRank, ["intelligence_bonus"] = rank * scene.IntelligencePerRank
            });
        }
        var sky = human?.equipped_visualizations.GetValueOrDefault(VisualizationRules.Sky, "").AsString() ?? "";
        var terrain = human?.equipped_visualizations.GetValueOrDefault(VisualizationRules.Terrain, "").AsString() ?? "";
        var thing = human?.equipped_visualizations.GetValueOrDefault(VisualizationRules.Thing, "").AsString() ?? "";
        var complete = new[] { sky, terrain, thing }.All(id => !string.IsNullOrEmpty(id));
        var manifestReady = complete && new[] { sky, terrain, thing }.All(id => VisualizationRules.GetRank(get_visualization_progress(id)) >= VisualizationRules.MaxRank);
        return new Dictionary
        {
            ["available"] = can_use_visualization(), ["scenes"] = scenes,
            ["focus_id"] = human?.visualization_focus_id ?? "", ["equipped"] = human?.equipped_visualizations.Duplicate(true) ?? new Dictionary(),
            ["combination_name"] = complete ? "《寒江照月图》" : "尚未合观", ["combination_ready"] = complete, ["manifest_ready"] = manifestReady,
            ["last_result"] = human?.last_visualization_result.Duplicate(true) ?? new Dictionary()
        };
    }

    public Dictionary get_visualization_methods(string sceneId)
    {
        var rank = VisualizationRules.GetRank(get_visualization_progress(sceneId));
        var result = new Array<Dictionary>();
        foreach (var method in VisualizationCatalog.GetMethods(sceneId, rank)) result.Add(new Dictionary
        {
            ["id"] = method.Id, ["name"] = method.Name, ["description"] = method.Description,
            ["duration_hours"] = method.DurationHours, ["rank_yield"] = method.RankYield, ["condition"] = method.ConditionText
        });
        return new Dictionary { ["methods"] = result };
    }

    public bool set_visualization_equipped(string slot, string sceneId)
    {
        if (human is null || !VisualizationRules.Slots.Contains(slot)) return false;
        var scene = VisualizationCatalog.Get(sceneId);
        if (scene is null || scene.Slot != slot || !human.learned_visualization_ids.Contains(sceneId)) return false;
        human.equipped_visualizations[slot] = sceneId;
        if (string.IsNullOrEmpty(human.visualization_focus_id)) human.visualization_focus_id = sceneId;
        refresh_gongfa_effects();
        return true;
    }

    public bool set_visualization_focus(string sceneId)
    {
        if (human is null || !human.learned_visualization_ids.Contains(sceneId) || VisualizationCatalog.Get(sceneId) is null) return false;
        human.visualization_focus_id = sceneId;
        return true;
    }

    /// <summary>静室观法是主动来源；首期只校验对应景与有效重数，天时地势保留给地点/事件系统调用。</summary>
    public Dictionary practice_visualization(string methodId)
    {
        if (!can_use_visualization()) return new Dictionary { ["success"] = false, ["reason"] = "当前无法温观。" };
        var method = VisualizationCatalog.GetMethod(methodId);
        if (method is null) return new Dictionary { ["success"] = false, ["reason"] = "未找到观法。" };
        var rank = VisualizationRules.GetRank(get_visualization_progress(method.SceneId));
        if (rank < method.MinRank || rank > method.MaxRank) return new Dictionary { ["success"] = false, ["reason"] = "此观法已不适合当前重数。" };
        var before = get_visualization_progress(method.SceneId);
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        time?.add_hours(method.DurationHours);
        var multiplier = VisualizationRules.GetPracticeMultiplier(rank);
        var after = add_visualization_progress(method.SceneId, method.RankYield * multiplier);
        var result = new Dictionary
        {
            ["success"] = true, ["method_name"] = method.Name, ["scene_id"] = method.SceneId,
            ["scene_name"] = VisualizationCatalog.Get(method.SceneId)?.Name ?? method.SceneId,
            ["before_rank"] = VisualizationRules.GetRank(before), ["after_rank"] = VisualizationRules.GetRank(after),
            ["rank_yield"] = after - before, ["practice_multiplier"] = multiplier, ["duration_hours"] = method.DurationHours
        };
        human!.last_visualization_result = result.Duplicate(true) as Dictionary ?? new Dictionary();
        refresh_gongfa_effects();
        return result;
    }

    private static BodyRefinementMethod? load_body_refinement_method(string path) =>
        string.IsNullOrEmpty(path) ? null : ResourceLoader.Load<BodyRefinementMethod>(path);

    public Array<BodyRefinementMethod> get_known_body_refinement_methods()
    {
        ensure_body_refinement_state();
        Array<BodyRefinementMethod> result = [];
        if (human is null) return result;
        foreach (var path in human.learned_body_refinement_method_paths)
        {
            var method = load_body_refinement_method(path);
            if (method is not null && !result.Any(item => item.ResourcePath == method.ResourcePath)) result.Add(method);
        }
        return result;
    }

    public Dictionary get_body_refinement_state()
    {
        ensure_body_refinement_state();
        var parts = new Array<Dictionary>();
        foreach (var part in BodyRefinementRules.Parts)
        {
            var progress = human?.body_refinement_progress.GetValueOrDefault(part, 0f).AsSingle() ?? 0f;
            var level = BodyRefinementRules.GetLevel(part, progress);
            var next = BodyRefinementRules.GetNextThreshold(part, level);
            parts.Add(new Dictionary
            {
                ["id"] = part,
                ["name"] = BodyRefinementRules.GetName(part),
                ["progress"] = progress,
                ["level"] = level,
                ["next_threshold"] = next,
                ["ratio"] = BodyRefinementRules.GetLevelProgressRatio(part, progress),
                ["reward"] = BodyRefinementRules.GetRewardText(part, level)
            });
        }
        var active = human is not null && !string.IsNullOrEmpty(human.active_body_refinement_method_path);
        var now = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
        return new Dictionary
        {
            ["parts"] = parts,
            ["active"] = active,
            ["method_name"] = active ? load_body_refinement_method(human!.active_body_refinement_method_path)?.method_name ?? "淬体" : "",
            ["material_path"] = active ? human!.active_body_refinement_material_path : "",
            ["remaining_hours"] = active ? Mathf.Max(0, human!.active_body_refinement_finish_hour - now) : 0,
            ["duration_hours"] = active ? human!.active_body_refinement_duration_hours : 0,
            ["last_result"] = human?.last_body_refinement_result ?? new Dictionary()
        };
    }

    public Dictionary get_last_body_refinement_result() => human?.last_body_refinement_result?.Duplicate(true) as Dictionary ?? new Dictionary();

    public Dictionary get_body_refinement_material_options(string method_path)
    {
        var method = load_body_refinement_method(method_path);
        var options = new Array<Dictionary>
        {
            new Dictionary { ["item_path"] = "", ["item_name"] = "不使用基础材料（效率 25%）", ["count"] = 0, ["efficiency"] = .25f }
        };
        if (method is null) return new Dictionary { ["options"] = options };
        var seen = new HashSet<string>();
        foreach (var entry in get_inventory_snapshot())
        {
            var path = entry.GetValueOrDefault("item_path", "").AsString();
            if (!seen.Add(path)) continue;
            var item = string.IsNullOrEmpty(path) ? null : ResourceLoader.Load<ItemData>(path);
            if (item is null || !method.CanUseMaterial(item)) continue;
            options.Add(new Dictionary
            {
                ["item_path"] = path,
                ["item_name"] = item.entity_name,
                ["count"] = get_inventory_total_count(path),
                ["efficiency"] = 1f,
                ["grade"] = item.get_grade_string()
            });
        }
        return new Dictionary { ["options"] = options };
    }

    public Dictionary get_body_refinement_preview(string method_path, string material_path = "")
    {
        var method = load_body_refinement_method(method_path);
        if (method is null) return new Dictionary { ["valid"] = false, ["reason"] = "未找到淬体法。" };
        var material = string.IsNullOrEmpty(material_path) ? null : ResourceLoader.Load<ItemData>(material_path);
        var validMaterial = material is not null && method.CanUseMaterial(material) && has_inventory_item(material_path);
        if (!string.IsNullOrEmpty(material_path) && !validMaterial) return new Dictionary { ["valid"] = false, ["reason"] = "所选材料不符合此淬体法的材料形态、功用或品阶要求。" };
        var efficiency = string.IsNullOrEmpty(material_path) ? .25f : 1f;
        return new Dictionary
        {
            ["valid"] = true,
            ["method_name"] = method.method_name,
            ["description"] = method.description,
            ["requirement"] = method.GetRequirementText(),
            ["allocation"] = method.GetAllocationText(),
            ["effective_range"] = method.GetEffectiveRangeText(),
            ["duration_min_hours"] = Math.Min(method.duration_min_hours, method.duration_max_hours),
            ["duration_max_hours"] = Math.Max(method.duration_min_hours, method.duration_max_hours),
            ["base_min"] = method.base_yield_min,
            ["base_max"] = method.base_yield_max,
            ["efficiency"] = efficiency,
            ["material_name"] = material?.entity_name ?? "无"
        };
    }

    public Dictionary start_body_refinement(string method_path, string material_path = "")
    {
        if (human is null || !string.IsNullOrEmpty(human.active_body_refinement_method_path)) return new Dictionary { ["success"] = false, ["reason"] = "当前已有淬体正在进行。" };
        if (is_in_breakthrough_retreat()) return new Dictionary { ["success"] = false, ["reason"] = "闭关突破期间不能另行淬体。" };
        var method = load_body_refinement_method(method_path);
        if (method is null || !get_known_body_refinement_methods().Any(item => item.ResourcePath == method.ResourcePath)) return new Dictionary { ["success"] = false, ["reason"] = "尚未掌握这本淬体法。" };
        ItemData? material = null;
        if (!string.IsNullOrEmpty(material_path))
        {
            material = ResourceLoader.Load<ItemData>(material_path);
            if (material is null || !method.CanUseMaterial(material) || !remove_item_from_inventory(material_path, 1)) return new Dictionary { ["success"] = false, ["reason"] = "所选基础材料不符合要求，或行囊中数量不足。" };
        }
        var rng = new RandomNumberGenerator(); rng.Randomize();
        var minHours = Math.Min(method.duration_min_hours, method.duration_max_hours);
        var maxHours = Math.Max(method.duration_min_hours, method.duration_max_hours);
        var duration = (int)rng.RandiRange(Math.Max(1, minHours), Math.Max(1, maxHours));
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        human.active_body_refinement_method_path = method.ResourcePath;
        human.active_body_refinement_material_path = material_path;
        human.active_body_refinement_started_hour = time?.get_total_game_hours() ?? 0;
        human.active_body_refinement_duration_hours = duration;
        human.active_body_refinement_finish_hour = human.active_body_refinement_started_hour + duration;
        if (time is null) settle_body_refinement();
        else
        {
            time.add_hours(duration);
            if (!string.IsNullOrEmpty(human.active_body_refinement_method_path) && time.get_total_game_hours() >= human.active_body_refinement_finish_hour) settle_body_refinement();
        }
        return new Dictionary { ["success"] = true, ["method_name"] = method.method_name, ["duration_hours"] = duration, ["material_name"] = material?.entity_name ?? "无" };
    }

    private void process_body_refinement()
    {
        if (human is null || string.IsNullOrEmpty(human.active_body_refinement_method_path)) return;
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        if (time is not null && time.get_total_game_hours() < human.active_body_refinement_finish_hour) return;
        settle_body_refinement();
    }

    private Dictionary settle_body_refinement()
    {
        if (human is null || string.IsNullOrEmpty(human.active_body_refinement_method_path)) return new Dictionary { ["success"] = false };
        var method = load_body_refinement_method(human.active_body_refinement_method_path);
        if (method is null) return new Dictionary { ["success"] = false, ["reason"] = "淬体法资源缺失。" };
        var rng = new RandomNumberGenerator(); rng.Randomize();
        var baseYield = (float)rng.RandiRange(Math.Min(method.base_yield_min, method.base_yield_max), Math.Max(method.base_yield_min, method.base_yield_max));
        var efficiency = string.IsNullOrEmpty(human.active_body_refinement_material_path) ? .25f : 1f;
        var effectiveYield = baseYield * efficiency;
        var allocation = roll_body_refinement_allocation(method, effectiveYield, rng);
        var effectiveAllocation = new Dictionary();
        var partEfficiencies = new Dictionary();
        var levelUps = new Array<Dictionary>();
        foreach (var part in BodyRefinementRules.Parts)
        {
            if (!allocation.ContainsKey(part)) continue;
            var before = human.body_refinement_progress.GetValueOrDefault(part, 0f).AsSingle();
            var beforeLevel = BodyRefinementRules.GetLevel(part, before);
            var partEfficiency = method.GetLevelEfficiency(part, beforeLevel);
            var partYield = allocation[part].AsSingle() * partEfficiency;
            effectiveAllocation[part] = partYield;
            partEfficiencies[part] = partEfficiency;
            var after = before + partYield;
            human.body_refinement_progress[part] = after;
            var afterLevel = BodyRefinementRules.GetLevel(part, after);
            if (afterLevel > beforeLevel) levelUps.Add(new Dictionary { ["part"] = BodyRefinementRules.GetName(part), ["from"] = beforeLevel, ["to"] = afterLevel });
        }
        var result = new Dictionary
        {
            ["success"] = true,
            ["method_name"] = method.method_name,
            ["base_yield"] = baseYield,
            ["effective_yield"] = effectiveAllocation.Values.Sum(value => value.AsSingle()),
            ["efficiency"] = efficiency,
            ["allocation"] = effectiveAllocation,
            ["raw_allocation"] = allocation,
            ["part_efficiencies"] = partEfficiencies,
            ["level_ups"] = levelUps,
            ["duration_hours"] = human.active_body_refinement_duration_hours
        };
        human.last_body_refinement_result = result.Duplicate(true) as Dictionary ?? new Dictionary();
        human.active_body_refinement_method_path = "";
        human.active_body_refinement_material_path = "";
        human.active_body_refinement_started_hour = 0;
        human.active_body_refinement_finish_hour = 0;
        human.active_body_refinement_duration_hours = 0;
        refresh_gongfa_effects();
        return result;
    }

    private static Dictionary roll_body_refinement_allocation(BodyRefinementMethod method, float amount, RandomNumberGenerator rng)
    {
        var parts = method.target_parts.Where(part => BodyRefinementRules.Parts.Contains(part)).Distinct().ToList();
        var result = new Dictionary();
        if (parts.Count == 0) return result;
        var remaining = 1f;
        for (var i = 0; i < parts.Count; i++)
        {
            var range = method.GetAllocationRange(parts[i]);
            var min = Mathf.Clamp(range.X, 0f, 1f);
            var max = Mathf.Clamp(Mathf.Max(min, range.Y), min, 1f);
            float ratio;
            if (i == parts.Count - 1) ratio = Mathf.Clamp(remaining, min, max);
            else
            {
                var minAfter = 0f; var maxAfter = 0f;
                for (var j = i + 1; j < parts.Count; j++)
                {
                    var next = method.GetAllocationRange(parts[j]);
                    minAfter += Mathf.Clamp(next.X, 0f, 1f);
                    maxAfter += Mathf.Clamp(Mathf.Max(next.X, next.Y), 0f, 1f);
                }
                var low = Mathf.Max(min, remaining - maxAfter);
                var high = Mathf.Min(max, remaining - minAfter);
                if (high < low) high = low;
                ratio = rng.RandfRange(low, high);
            }
            result[parts[i]] = ratio * amount;
            remaining -= ratio;
        }
        if (parts.Count == 1) result[parts[0]] = amount;
        return result;
    }
    private void consume_meditation_spirit_stones(float hours)
    {
        if(human is null||!meditation_spirit_stone_supply||meditation_room_index<0)return;
        human.meditation_spirit_stone_debt+=get_spirit_stone_consumption_estimate(hours);
        var due=Mathf.FloorToInt(human.meditation_spirit_stone_debt); if(due<=0)return;
        foreach(var node in GetTree().GetNodesInGroup("main_scene")) if(node is MainScene main)
        {
            var paid=main.consume_estate_resource(EstateResourceSystem.SpiritStones,due);
            human.meditation_spirit_stone_debt-=paid;
            if(paid<due)human.meditation_spirit_stone_supply=false;
            return;
        }
    }
    /// <summary>所有角色统一使用公共基础技能池；不读取主修秘籍或历史存档分类。</summary>
    public void refresh_shared_basic_skill_pool()
    {
        if (human is not null) human.basic_deck_path = PublicBasicSkillPoolPath;
    }
    public override void apply_battle_data(BattleEntityData snapshot)
    {
        base.apply_battle_data(snapshot);
        if(human is null)return;
        ApplyCombatBoardPreparation(snapshot);
        ApplyBattleSupplySnapshot(snapshot);
        human.body_proficiency=snapshot.body_proficiency;
        human.sword_proficiency=snapshot.sword_proficiency;
        human.magic_proficiency=snapshot.magic_proficiency;
        human.formation_proficiency=snapshot.formation_proficiency;
        human.formation_circuits=snapshot.formation_circuits.Duplicate();
        human.formation_manual_ids=snapshot.formation_manual_ids.Duplicate();
        human.basic_deck_path=snapshot.basic_deck_path;
        ensure_basic_deck();
        human.combat_skill_slot_paths=snapshot.combat_skill_slot_paths.Duplicate();
        human.combat_skill_references=snapshot.combat_skill_references.Duplicate(true);
        if (snapshot.combat_board_layout is not null)
            human.combat_board_layout=snapshot.combat_board_layout.CopyConfiguration();
        if (HasMeta("artifact_battle_clone") && snapshot.combat_board_catalog is not null)
            SetMeta("combat_board_catalog",snapshot.combat_board_catalog.Duplicate(true));
        if (HasMeta("artifact_battle_clone"))
        {
            human.artifact_inventory=snapshot.equipped_artifacts.Duplicate(true);
            human.equipped_artifact_ids=new Array<string>(human.artifact_inventory.Select(item => item.instance_id));
        }
        human.combat_skill_slots_configured=snapshot.combat_skill_slots_configured;
        human.combat_passive_paths=snapshot.combat_passive_paths.Duplicate();
        human.main_gongfa_path=snapshot.main_gongfa_path;
        human.sub_gongfa_paths=snapshot.sub_gongfa_paths.Duplicate();
        load_gongfa_data();
    }
    public override BattleEntityData get_snapshot_data()
    {
        var snapshot = base.get_snapshot_data();
        if (human is null) return snapshot;
        snapshot.pill_toxicity = (float)PillToxicity;
        ensure_basic_deck();
        refresh_shared_basic_skill_pool();
        snapshot.gender=human.gender; snapshot.dun_speed=human.dun_speed; snapshot.charisma=human.charisma; snapshot.social=human.social; snapshot.management=human.management;
        snapshot.artifact_bond_defense_applied = true;
        snapshot.body_proficiency=human.body_proficiency;
        snapshot.sword_proficiency=human.sword_proficiency;
        snapshot.magic_proficiency=human.magic_proficiency;
        snapshot.formation_proficiency=human.formation_proficiency;
        snapshot.formation_circuits=human.formation_circuits.Duplicate();
        snapshot.formation_manual_ids=human.formation_manual_ids.Duplicate();
        snapshot.basic_deck_path = human.basic_deck_path;
        snapshot.loaded_skill_paths = human.loaded_skill_paths.Duplicate();
        snapshot.combat_skill_slot_paths = get_combat_skill_slot_paths();
        snapshot.combat_board_layout = human.combat_board_layout?.CopyConfiguration();
        snapshot.combat_board_catalog = HasMeta("combat_board_catalog")
            ? GetMeta("combat_board_catalog").AsGodotObject() as CombatBoardCatalog
            : CombatBoardCatalog.FromCharacter(this);
        snapshot.combat_skill_references = human.combat_skill_references.Duplicate(true);
        snapshot.combat_resolved_skills = new Array<ActiveSkillData>(snapshot.combat_skill_slot_paths.Select(key => resolve_combat_skill(key) ?? ArtifactSkillFactory.EmptySlot()));
        snapshot.equipped_artifacts = new Array<ArtifactInstanceData>(equipped_combat_artifacts().Select(item => (ArtifactInstanceData)item.Duplicate(true)));
        snapshot.combat_skill_slots_configured = human.combat_skill_slots_configured;
        snapshot.combat_passive_paths = get_combat_passive_paths();
        snapshot.combat_heart_method_upgrades = get_combat_heart_method_upgrades();
        snapshot.combat_tempering_by_gongfa = get_active_combat_tempering_by_gongfa();
        snapshot.battle_deck_configured = human.battle_deck_configured;
        snapshot.main_gongfa_path = human.main_gongfa_path;
        snapshot.sub_gongfa_paths = human.sub_gongfa_paths.Duplicate();
        foreach (var skill in get_available_battle_skills()) if (!string.IsNullOrEmpty(skill.ResourcePath)) snapshot.active_skill_paths.Add(skill.ResourcePath);
        snapshot.pack_item_paths = pack_items.Duplicate();
        snapshot.pack_item_counts = pack_counts.Duplicate();
        CaptureBattleSupplySnapshot(snapshot);
        CaptureCombatBoardPreparation(snapshot);
        return snapshot;
    }
    public bool equip_main_gongfa(GongfaResource gongfa)=>switch_main_gongfa(gongfa);
    public bool switch_main_gongfa(GongfaResource gongfa)
    {
        if(gongfa is null||human is null||!has_learned_gongfa(gongfa)||!gongfa.can_be_main||!gongfa.is_wuxing_compatible(spirit_root_type))return false;
        remove_sub_gongfa(gongfa);main_gongfa=gongfa;human.main_gongfa_path=gongfa.ResourcePath;refresh_shared_basic_skill_pool();refresh_gongfa_effects();MainGongfaChanged?.Invoke(this,gongfa);return true;
    }
    public void unequip_main_gongfa(){main_gongfa=null;if(human is not null)human.main_gongfa_path="";refresh_gongfa_effects();}
    public bool add_sub_gongfa(GongfaResource gongfa)
    {
        if(gongfa is null||human is null||!has_learned_gongfa(gongfa)||!gongfa.can_be_sub||gongfa==main_gongfa||sub_gongfa_list.Contains(gongfa)||sub_gongfa_list.Count>=max_sub_slots)return false;
        sub_gongfa_list.Add(gongfa);human.sub_gongfa_paths.Add(gongfa.ResourcePath);refresh_gongfa_effects();return true;
    }
    public void remove_sub_gongfa(GongfaResource gongfa){sub_gongfa_list.Remove(gongfa);human?.sub_gongfa_paths.Remove(gongfa.ResourcePath);refresh_gongfa_effects();}
    /// <summary>兼容旧存档中的卷章分支记录；当前规则不再读取分支选择。</summary>
    public string get_sub_gongfa_chapter_route(GongfaResource gongfa,string chapterId)=>human?.equipped_gongfa_chapter_routes.GetValueOrDefault(gongfa.ResourcePath+":"+chapterId,"").AsString()??"";
    public void set_sub_gongfa_chapter_route(GongfaResource gongfa,string chapterId,string route){if(human is not null)human.equipped_gongfa_chapter_routes[gongfa.ResourcePath+":"+chapterId]=route;}
    public Array<string> get_learned_gongfa_chapter_ids(GongfaResource gongfa){var result=new Array<string>();if(human is null)return result;var values=human.learned_gongfa_chapters.GetValueOrDefault(gongfa.ResourcePath,new Array<string>()).AsGodotArray();foreach(var value in values)result.Add(value.AsString());return result;}
    public float get_gongfa_chapter_study_progress(GongfaResource gongfa,string id)=>human?.gongfa_chapter_study_progress.GetValueOrDefault(gongfa.ResourcePath+":"+id,0).AsSingle()??0; public void set_gongfa_chapter_study_progress(GongfaResource gongfa,string id,float value){if(human is not null)human.gongfa_chapter_study_progress[gongfa.ResourcePath+":"+id]=value;}
     public bool add_to_pack(string path,int count=1) => add_item_to_inventory(path,count);
     public Dictionary remove_from_pack(int index)
     {
         if (human is null || index < 0 || index >= human.inventory_item_paths.Count) return new Dictionary();
         var path = human.inventory_item_paths[index]; var count = human.inventory_item_counts[index];
         var physical = human.inventory_placements.FirstOrDefault(p => p.row_slot == human.inventory_item_positions[index]);
         if (physical is not null && has_nonempty_storage_bag(physical.instance_id)) return new Dictionary();
         if (get_inventory_total_count(path) - get_equipped_item_count(path) < count) return new Dictionary();
         var result = new Dictionary { ["item_path"] = path, ["count"] = count };
         human.inventory_item_paths.RemoveAt(index); human.inventory_item_counts.RemoveAt(index); human.inventory_item_positions.RemoveAt(index); notify_inventory_changed(); return result;
     }
     public Array<string> get_pack_item_paths() => pack_items.Duplicate();
}
