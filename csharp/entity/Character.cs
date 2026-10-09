using Godot;
using Godot.Collections;

/// <summary>人类角色：统一承载成长、功法、装备与秘籍研读的运行时状态。</summary>
public partial class Character : Creature
{
    public const int InventorySlotCapacity = 30;
    private const string InitialDeckPath = "res://resources/basic_decks/初始牌库.tres";
    private const string LegacyInitialDeckPath = "res://resources/basic_decks/拳脚路.tres";
    private const string BodyDeckPath = "res://resources/basic_decks/锻躯路.tres";
    // 早期灵石的可用储灵量：三灵根在密度 180、每日修炼十二时的条件下约可持续十日。
    // 灵石只提供环境，实际消耗仍随角色在该环境中的纳气速率变化。
    private const float SpiritStoneStoredLingqi = .36f;
    [Signal] public delegate void gongfa_chapter_completedEventHandler(Character character,string gongfa_name,string chapter_id,string chapter_name);
    [Signal] public delegate void breakthrough_startedEventHandler(Character character,string target_realm);
    [Signal] public delegate void breakthrough_completedEventHandler(Character character,string new_realm);
    [Signal] public delegate void inventory_changedEventHandler();
    private HumanData? human => entity_data as HumanData;
    /// <summary>供家族剧情和界面订阅的运行时通知；不依赖面板轮询研读状态。</summary>
    public event Action<Character, string, string, string>? GongfaChapterCompleted;
    public event Action<Character, GongfaResource>? StudyStarted;
    public event Action<Character>? InventoryChanged;
    public event Action<Character>? WorldStaminaChanged;
    /// <summary>境界实际变更后的通知；用于阶段任务。</summary>
    public event Action<Character, string>? BreakthroughCompleted;
    public event Action<Character, GongfaResource>? MainGongfaChanged;
    public GongfaResource? main_gongfa; public Array<GongfaResource> sub_gongfa_list=[];
    /// <summary>旧调用点兼容属性；数据实际保存在 HumanData 的玩家行囊字段中。</summary>
    public Array<string> pack_items => human?.inventory_item_paths ?? new Array<string>();
    public Array<int> pack_counts => human?.inventory_item_counts ?? new Array<int>();
    public Array<ArtifactInstanceData> artifact_inventory => human?.artifact_inventory ?? new Array<ArtifactInstanceData>();
    public int max_pack_slots { get => InventorySlotCapacity; set { } }
    private readonly Array<SecretBook> _study_books=[]; private float _session_hours; private int _last_schedule_hour; private readonly Dictionary _last_schedule_report=[];
    private readonly RandomNumberGenerator _qi_insight_rng = new();
    private bool is_qi_cloud_breath() => cultivation_realm.StartsWith("云息", StringComparison.Ordinal);
    private bool is_visualization_cloud_breath() => cultivation_realm.StartsWith("云息", StringComparison.Ordinal);
    public int spirit_root_type {get=>human?.spirit_root_type??0;set{if(human is not null)human.spirit_root_type=value;}} public string spirit_root_name{get=>human?.spirit_root_name??"无灵根";set{if(human is not null)human.spirit_root_name=value;}} public int dao_bearing{get=>human?.dao_bearing??0;set{if(human is not null)human.dao_bearing=value;}} public int core_count{get=>human?.core_count??0;set{if(human is not null)human.core_count=value;}}
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
    public float ambient_lingqi_pressure{get=>human?.ambient_lingqi_pressure??100;set{if(human is not null)human.ambient_lingqi_pressure=value;}} public string basic_deck_path{get=>human?.basic_deck_path??"";set{if(human is not null)human.basic_deck_path=value;}} public int max_sub_slots{get=>human?.max_sub_slots??2;set{if(human is not null)human.max_sub_slots=value;}} public float mana_regen_rate{get=>human?.mana_regen_rate??.04f;set{if(human is not null)human.mana_regen_rate=value;}}
    public int meditation_room_index{get=>human?.meditation_room_index??-1;}
    public bool meditation_spirit_stone_supply{get=>human?.meditation_spirit_stone_supply??false;}
    public override void _Ready(){entity_data ??=new HumanData();if(entity_data is not HumanData)entity_data=new HumanData{entity_name=entity_data.entity_name};ensure_basic_deck();ensure_spirit_core_state();ensure_body_refinement_state();ensure_qi_state();ensure_visualization_state();ensure_inventory_state();_qi_insight_rng.Randomize();load_gongfa_data();refresh_dao_bearing_nodes();apply_missing_gongfa_permanent_bonuses();base._Ready();refresh_gongfa_effects();var time=GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");if(time is not null){_last_schedule_hour=time.get_total_game_hours();time.time_changed+=process_breakthrough_retreat;time.time_changed+=process_body_refinement;}}
    public override void load_from_data(EntityData source){var data=source as HumanData??new HumanData{entity_name=source.entity_name};entity_data=data.Duplicate(true) as HumanData;ensure_basic_deck();ensure_spirit_core_state();ensure_body_refinement_state();ensure_qi_state();ensure_visualization_state();ensure_inventory_state();_qi_insight_rng.Randomize();load_gongfa_data();refresh_dao_bearing_nodes();apply_missing_gongfa_permanent_bonuses();base.load_from_data(entity_data!);refresh_gongfa_effects();}
    private void ensure_basic_deck(){if(human is null)return;if(string.IsNullOrEmpty(human.basic_deck_path)||human.basic_deck_path==LegacyInitialDeckPath)human.basic_deck_path=InitialDeckPath;if(Mathf.IsEqualApprox(human.dun_speed,100.0f)){var legacy_speed=!Mathf.IsEqualApprox(human.dun_speed_cells_per_day,1.0f)?human.dun_speed_cells_per_day:human.travel_speed_cells_per_day;if(!Mathf.IsEqualApprox(legacy_speed,1.0f))human.dun_speed=legacy_speed*100.0f;}human.dun_speed=Mathf.Max(0.01f,human.dun_speed);human.dun_speed_cells_per_day=1.0f;human.travel_speed_cells_per_day=1.0f;}
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
    private void ensure_qi_state()
    {
        if (human is null) return;
        human.qi_inspirations ??= [];
        human.qi_node_levels ??= [];
        foreach (var node in QiNodeCatalog.CurrentRealm)
            if (human.qi_node_levels.ContainsKey(node.Id)) human.qi_node_levels[node.Id] = Mathf.Clamp(human.qi_node_levels[node.Id].AsInt32(), 0, node.MaxLevel);
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
        foreach (var path in new[] { human.weapon_path, human.headwear_path, human.clothes_path, human.ornament_1_path, human.footwear_path, human.ornament_2_path })
            if (!string.IsNullOrWhiteSpace(path) && ResourceLoader.Load<ItemData>(path) is not null && get_inventory_total_count(path) == 0)
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
        sync_artifact_spirit_perception();
    }
    private ItemData? load_inventory_item(string path) => string.IsNullOrWhiteSpace(path) || !ResourceLoader.Exists(path) ? null : ResourceLoader.Load<ItemData>(path);
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
            result.Add(new Dictionary
            {
                ["slot_index"] = human.inventory_item_positions[i], ["item_path"] = path, ["count"] = human.inventory_item_counts[i],
                ["item_name"] = item?.entity_name ?? path, ["category"] = item?.item_category ?? "",
                ["equipped"] = is_item_equipped(path)
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
    public int get_inventory_free_slot_count() => Math.Max(0, max_pack_slots - get_inventory_slot_count());
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
    public bool add_item_to_inventory(string path, int count = 1)
    {
        if (human is null || count <= 0) return false;
        ensure_inventory_state();
        var item = load_inventory_item(path);
        if (item is null) return false;
        var remaining = count;
        var maxStack = item.can_stack ? Math.Max(1, item.max_stack) : 1;
        if (item.can_stack)
            for (var i = 0; i < human.inventory_item_paths.Count && remaining > 0; i++)
                if (human.inventory_item_paths[i] == path) remaining -= Math.Min(Math.Max(0, maxStack - human.inventory_item_counts[i]), remaining);
        var requiredNewSlots = item.can_stack ? Mathf.CeilToInt(remaining / (float)maxStack) : remaining;
        if (requiredNewSlots > get_inventory_free_slot_count()) return false;
        remaining = count;
        if (item.can_stack)
        {
            for (var i = 0; i < human.inventory_item_paths.Count && remaining > 0; i++)
            {
                if (human.inventory_item_paths[i] != path) continue;
                var available = Math.Max(0, maxStack - human.inventory_item_counts[i]);
                var moved = Math.Min(available, remaining);
                human.inventory_item_counts[i] += moved; remaining -= moved;
            }
        }
        while (remaining > 0)
        {
            var moved = item.can_stack ? Math.Min(maxStack, remaining) : 1;
            human.inventory_item_paths.Add(path); human.inventory_item_counts.Add(moved); human.inventory_item_positions.Add(find_free_inventory_position()); remaining -= moved;
        }
        notify_inventory_changed();
        return true;
    }
    public bool remove_item_from_inventory(string path, int count = 1)
    {
        if (human is null || count <= 0 || get_inventory_total_count(path) - get_equipped_item_count(path) < count) return false;
        var remaining = count;
        for (var i = human.inventory_item_paths.Count - 1; i >= 0 && remaining > 0; i--)
        {
            if (human.inventory_item_paths[i] != path) continue;
            var moved = Math.Min(Math.Max(0, human.inventory_item_counts[i]), remaining);
            human.inventory_item_counts[i] -= moved; remaining -= moved;
            if (human.inventory_item_counts[i] <= 0) { human.inventory_item_paths.RemoveAt(i); human.inventory_item_counts.RemoveAt(i); human.inventory_item_positions.RemoveAt(i); }
        }
        if (remaining > 0) return false;
        notify_inventory_changed();
        return true;
    }
    public Array<string> get_equipped_item_paths()
    {
        var result = new Array<string>();
        if (human is null) return result;
        foreach (var path in new[] { human.weapon_path, human.headwear_path, human.clothes_path, human.ornament_1_path, human.footwear_path, human.ornament_2_path })
            if (!string.IsNullOrWhiteSpace(path) && !result.Contains(path)) result.Add(path);
        return result;
    }
    private int get_equipped_item_count(string path) => human is null ? 0 : new[] { human.weapon_path, human.headwear_path, human.clothes_path, human.ornament_1_path, human.footwear_path, human.ornament_2_path }.Count(item => item == path);
    public bool is_item_equipped(string path) => get_equipped_item_count(path) > 0;
    public bool equip_inventory_item(string path, string slot = "")
    {
        if (human is null || !has_inventory_item(path) || load_inventory_item(path) is not EquipmentData equipment) return false;
        var target = slot.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(target)) target = equipment.equip_type switch { 0 => "weapon", 1 => "clothes", 2 => string.IsNullOrEmpty(human.ornament_1_path) ? "ornament_1" : string.IsNullOrEmpty(human.ornament_2_path) ? "ornament_2" : "ornament_1", _ => "" };
        if (target is not ("weapon" or "headwear" or "clothes" or "ornament_1" or "footwear" or "ornament_2")) return false;
        switch (target) { case "weapon": human.weapon_path = path; break; case "headwear": human.headwear_path = path; break; case "clothes": human.clothes_path = path; break; case "ornament_1": human.ornament_1_path = path; break; case "footwear": human.footwear_path = path; break; case "ornament_2": human.ornament_2_path = path; break; }
        notify_inventory_changed();
        return true;
    }
    public bool unequip_inventory_item(string path)
    {
        if (human is null || !is_item_equipped(path)) return false;
        if (human.weapon_path == path) human.weapon_path = "";
        if (human.headwear_path == path) human.headwear_path = "";
        if (human.clothes_path == path) human.clothes_path = "";
        if (human.ornament_1_path == path) human.ornament_1_path = "";
        if (human.footwear_path == path) human.footwear_path = "";
        if (human.ornament_2_path == path) human.ornament_2_path = "";
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
    {
        if (human is null || artifact is null) return false;
        ensure_inventory_state(); artifact.ensure_instance_id();
        if (find_artifact(artifact.instance_id) is not null) return false;
        if (get_inventory_slot_count() >= max_pack_slots) return false;
        if (artifact.current_durability <= 0 && artifact.max_durability > 0) artifact.current_durability = artifact.max_durability;
        human.artifact_inventory.Add(artifact); human.artifact_inventory_positions.Add(find_free_inventory_position()); notify_inventory_changed(); return true;
    }
    public bool remove_artifact_from_inventory(string instanceId)
    {
        if (human is null || is_artifact_equipped(instanceId)) return false;
        var index = -1;
        for (var i = 0; i < human.artifact_inventory.Count; i++) if (human.artifact_inventory[i]?.instance_id == instanceId) { index = i; break; }
        if (index < 0) return false;
        human.artifact_inventory.RemoveAt(index); human.artifact_inventory_positions.RemoveAt(index); notify_inventory_changed(); return true;
    }
    public int get_used_artifact_spirit() => human?.equipped_artifact_ids.Sum(id => find_artifact(id)?.spirit_cost ?? 0) ?? 0;
    public bool equip_artifact(string instanceId)
    {
        if (human is null || find_artifact(instanceId) is not { } artifact || is_artifact_equipped(instanceId)) return false;
        if (human.artifact_slot_count <= human.equipped_artifact_ids.Count) return false;
        if (human.max_spirit_perception > 0 && get_used_artifact_spirit() + Math.Max(0, artifact.spirit_cost) > human.max_spirit_perception) return false;
        if (artifact.max_durability > 0 && artifact.current_durability <= 0) return false;
        human.equipped_artifact_ids.Add(instanceId); notify_inventory_changed(); return true;
    }
    public bool unequip_artifact(string instanceId)
    {
        if (human is null || !human.equipped_artifact_ids.Remove(instanceId)) return false;
        notify_inventory_changed(); return true;
    }
    private void sync_artifact_spirit_perception() { if (human is not null) human.used_spirit_perception = get_used_artifact_spirit(); }
    private void load_gongfa_data(){if(human is null)return;main_gongfa=load_gongfa(human.main_gongfa_path);lock_dao_bearing_from_main();sub_gongfa_list.Clear();foreach(var path in human.sub_gongfa_paths){var g=load_gongfa(path);if(g is not null)sub_gongfa_list.Add(g);}}
    private static GongfaResource? load_gongfa(string path)=>string.IsNullOrEmpty(path)?null:ResourceLoader.Load<GongfaResource>(path);
    public bool has_learned_gongfa(GongfaResource gongfa)=>human?.learned_gongfa_paths.Contains(gongfa.ResourcePath)==true;
    public Array<GongfaResource> get_known_gongfas()
    {
        Array<GongfaResource> result=[];
        void Add(GongfaResource? value){if(value is not null&&!result.Any(item=>item.ResourcePath==value.ResourcePath))result.Add(value);}
        Add(main_gongfa); foreach(var value in sub_gongfa_list)Add(value);
        if(human is not null)foreach(var path in human.learned_gongfa_paths)Add(load_gongfa(path));
        return result;
    }
    /// <summary>汇总已研读卷章授予的可调整主动技能。心法由当前主修/辅修槽位单独提供，不作为习得技能。</summary>
    public Array<ActiveSkillData> get_available_battle_skills()
    {
        refresh_dao_bearing_nodes();
        Array<ActiveSkillData> result = [];
        void Add(Resource? resource)
        {
            if (resource is not ActiveSkillData skill || skill.skill_type == ActiveSkillData.SkillType.POWER || string.Equals(skill.slot_category, "passive", StringComparison.OrdinalIgnoreCase) || result.Any(card => card.ResourcePath == skill.ResourcePath)) return;
            result.Add(skill);
        }
        foreach (var skill in active_skills) Add(skill);
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
        if (human is null || gongfa is null || chapter is null) return false;
        // 研读同样必须先具备该书要求的灵根；灵根权重只在实际修炼速率中结算。
        if (!gongfa.is_wuxing_compatible(spirit_root_type)) return false;
        if (!string.IsNullOrEmpty(chapter.required_realm))
        {
            var realms = GetNodeOrNull<RealmManager>("/root/RealmManager");
            if (realms is not null && !realms.is_realm_after_or_equal(cultivation_realm, chapter.required_realm)) return false;
            if (realms is null && cultivation_realm != chapter.required_realm) return false;
        }
        foreach (Variant key in chapter.required_attributes.Keys)
        {
            var value = Get(key.AsString());
            if (value.VariantType is not Variant.Type.Int and not Variant.Type.Float || value.AsSingle() < chapter.required_attributes[key].AsSingle()) return false;
        }
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
        if (skill.skill_type is ActiveSkillData.SkillType.POWER or ActiveSkillData.SkillType.DRAW or ActiveSkillData.SkillType.CURSE) return false;
        if (string.Equals(skill.slot_category, "passive", StringComparison.OrdinalIgnoreCase)) return false;
        if (skill.keywords.Any(keyword => keyword.Contains("固有", StringComparison.Ordinal))) return false;
        if (skill.special_effects.Any(effect => effect.GetValueOrDefault("effect", "").AsString() == "draw")) return false;
        // 即时战斗的精力恢复由系统行动“休整”提供，旧基础库中的“养息”不再占用技能槽。
        if (skill.skill_name == "养息") return false;
        return true;
    }
    /// <summary>返回当前基础技能库可转为固定技能槽的去重路径；基础牌的数量仍由凝核牌局/旧技能库逻辑另行读取。</summary>
    public Array<string> get_default_combat_skill_paths()
    {
        if (human is null || (human.basic_deck_path != InitialDeckPath && human.basic_deck_path != BodyDeckPath)) return [];
        var paths = get_basic_deck_skill_paths_before_gongfa();
        paths = GongfaSkillRewriteRuntime.ApplyBasicSkillRewrites(this, paths);
        var result = new Array<string>();
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path) || result.Contains(path)) continue;
            var skill = ResourceLoader.Load<ActiveSkillData>(path);
            if (is_combat_skill_slot_eligible(skill)) result.Add(path);
            if (result.Count >= 10) break;
        }
        return result;
    }
    private Array<string> get_basic_deck_skill_paths_before_gongfa()
    {
        if (human is null || (human.basic_deck_path != InitialDeckPath && human.basic_deck_path != BodyDeckPath)) return [];
        var deck = ResourceLoader.Load<BasicDeckResource>(human.basic_deck_path);
        if (deck is null) return [];
        var paths = deck.get_deck_skills();
        foreach (var upgradeId in human.basic_deck_upgrade_ids)
        {
            var upgrade = deck.get_upgrade_by_id(upgradeId);
            if (upgrade is not null) paths = upgrade.apply_to_paths(paths);
        }
        return paths;
    }
    /// <summary>将已保存的旧技能路径解析为当前化法版本，兼容化法学习或主辅修变化前保存的槽位。</summary>
    public string resolve_combat_skill_slot_path(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;
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
    /// <summary>从主修/辅修槽位中的锻躯秘籍取得开战自动生效的心法；心法不单独学习或装配，也不进入技能槽。</summary>
    public Array<string> get_combat_passive_paths()
    {
        var result = new Array<string>();
        if (human is null) return result;
        void AddSlotHeart(GongfaResource? book, bool isMain)
        {
            var skill = book?.get_heart_method_skill(isMain);
            if (skill is null || string.IsNullOrWhiteSpace(skill.ResourcePath) || !BodyCardRuntime.IsBody(skill)) return;
            if (result.Contains(skill.ResourcePath)) return;
            result.Add(skill.ResourcePath);
        }
        AddSlotHeart(main_gongfa, true);
        foreach (var book in sub_gongfa_list) AddSlotHeart(book, false);
        return result;
    }
    /// <summary>汇总已研读且当前槽位启用的淬炼，只把左支淬炼计入战斗快照。</summary>
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
                if(!isMain&&get_sub_gongfa_chapter_route(book,chapter.get_id())!="left")continue;
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
        var result = new Array<string>();
        foreach (var path in human.combat_skill_slot_paths)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            var resolvedPath = resolve_combat_skill_slot_path(path);
            var skill = ResourceLoader.Load<ActiveSkillData>(resolvedPath);
            if (!is_combat_skill_slot_eligible(skill) || result.Contains(resolvedPath)) continue;
            result.Add(resolvedPath); if (result.Count >= 10) break;
        }
        if (result.Count > 0) return result;
        // 玩家已经确认过装配后，空数组就是主动清空，不再被默认技能库重新填回。
        if (human.combat_skill_slots_configured) return result;
        // 基础技能库迁移为固定技能槽的默认技能，再用角色已装配的功法技能补足剩余槽位。
        foreach (var path in get_default_combat_skill_paths())
        {
            if (string.IsNullOrWhiteSpace(path) || result.Contains(path)) continue;
            result.Add(path); if (result.Count >= 10) break;
        }
        foreach (var path in human.loaded_skill_paths)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            var resolvedPath = resolve_combat_skill_slot_path(path);
            var skill = ResourceLoader.Load<ActiveSkillData>(resolvedPath);
            if (!is_combat_skill_slot_eligible(skill) || result.Contains(resolvedPath)) continue;
            result.Add(resolvedPath); if (result.Count >= 10) break;
        }
        return result;
    }
    public bool set_combat_skill_slot_paths(Array<string> paths)
    {
        if (human is null) return false;
        human.combat_skill_slots_configured = true;
        human.combat_skill_slot_paths.Clear();
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            var resolvedPath = resolve_combat_skill_slot_path(path);
            var skill = ResourceLoader.Load<ActiveSkillData>(resolvedPath);
            if (!is_combat_skill_slot_eligible(skill) || human.combat_skill_slot_paths.Contains(resolvedPath) || human.combat_skill_slot_paths.Count >= 10) continue;
            human.combat_skill_slot_paths.Add(resolvedPath);
        }
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
        const float baseRate=.0020f;
        // 功法章节的 cultivation_rate 是旧方案遗留；修炼快慢只由境界、灵根与灵力环境决定。
        return Mathf.Max(0,baseRate*get_realm_cultivation_multiplier()*get_element_efficiency(main_gongfa)*get_cultivation_environment_factor(pressure));
    }
    public float get_cultivation_progress()=>human?.cultivation_progress??0f;
    public Dictionary get_next_realm_progress()
    {
        var realms=GetNodeOrNull<RealmManager>("/root/RealmManager"); var next=realms?.get_next_realm_name(cultivation_realm)??"";
        var need=string.IsNullOrEmpty(next)?0:realms?.get_effective_requirements_for_next_realm(this).GetValueOrDefault("spirit_strands",0).AsInt32()??0;
        return new Dictionary{{"next_realm",next},{"current",spirit_strands},{"required",need},{"partial",get_cultivation_progress()}};
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
    private float get_realm_cultivation_multiplier()=>get_realm_tier() switch{1=>1f,2=>1.04f,3=>1.08f,4=>1.13f,5=>1.18f,6=>1.24f,7=>1.31f,8=>1.38f,9=>1.46f,10=>1.55f,11=>1.65f,12=>1.76f,13=>1.88f,_=>1f};
    private float get_realm_required_lingqi_pressure()=>get_realm_tier() switch{1=>100f,2=>108f,3=>116f,4=>124f,5=>132f,6=>140f,7=>150f,8=>160f,9=>170f,10=>180f,11=>188f,12=>195f,13=>200f,_=>100f};
    public float get_realm_required_lingqi_pressure_for_ui()=>get_realm_required_lingqi_pressure();
    private int get_realm_tier()
    {
        const string prefix="云息";
        if(!cultivation_realm.StartsWith(prefix)||!cultivation_realm.EndsWith("层"))return 1;
        return int.TryParse(cultivation_realm[prefix.Length..^1],out var tier)?Mathf.Clamp(tier,1,13):1;
    }
    public bool can_cultivate()=>spirit_root_type!=0&&main_gongfa is not null&&main_gongfa.is_wuxing_compatible(spirit_root_type)&&!is_cultivation_blocked_by_breakthrough()&&!is_in_breakthrough_retreat();
    public int get_available_cultivation_hours(int requestedHours) => get_available_world_action_hours(WorldStaminaRules.CultivationCostPerGameHour, requestedHours);
    public int get_available_study_hours(int requestedHours) => get_available_world_action_hours(WorldStaminaRules.StudyCostPerGameHour, requestedHours);
    public Dictionary get_world_cultivation_preview_for_strands(int strands)
    {
        var wanted = Mathf.Clamp(strands, 0, get_strands_until_next_realm());
        var rate = Mathf.Max(0.0001f, get_cultivation_rate());
        var requestedHours = wanted <= 0 ? 0 : Mathf.CeilToInt(wanted / rate);
        var hours = get_available_cultivation_hours(requestedHours);
        var actual = Mathf.Min(wanted, Mathf.FloorToInt(rate * hours + 0.0001f));
        if (actual <= 0) hours = 0;
        return new Dictionary { ["strands"] = actual, ["requested_strands"] = wanted, ["hours"] = hours, ["days"] = hours / 24f, ["rate"] = rate, ["stamina_cost"] = hours * WorldStaminaRules.CultivationCostPerGameHour, ["stamina_limited"] = actual < wanted };
    }
    public Dictionary get_world_cultivation_preview_for_hours(int requestedHours)
    {
        var hours = get_available_cultivation_hours(Mathf.Max(0, requestedHours));
        var actual = Mathf.Min(get_strands_until_next_realm(), Mathf.FloorToInt(get_cultivation_rate() * hours));
        return new Dictionary { ["strands"] = actual, ["requested_strands"] = get_strands_until_next_realm(), ["hours"] = hours, ["requested_hours"] = Mathf.Max(0, requestedHours), ["days"] = hours / 24f, ["rate"] = get_cultivation_rate(), ["stamina_cost"] = hours * WorldStaminaRules.CultivationCostPerGameHour, ["stamina_limited"] = hours < requestedHours };
    }
    public Dictionary cultivate_with_world_stamina_for_strands(int requestedStrands)
    {
        if (!can_cultivate()) return new Dictionary { ["success"] = false };
        var preview = get_world_cultivation_preview_for_strands(requestedStrands);
        var strands = preview["strands"].AsInt32(); var hours = preview["hours"].AsInt32(); var staminaCost = preview["stamina_cost"].AsSingle();
        if (strands <= 0 || hours <= 0 || !try_spend_world_stamina(staminaCost)) return new Dictionary { ["success"] = false, ["reason"] = "体力不足" };
        human!.cultivation_progress = 0f; spirit_strands += strands;
        GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.add_hours(hours);
        var qiGained = settle_qi_inspiration_from_cultivation(hours);
        return new Dictionary { ["success"] = true, ["strands"] = strands, ["hours"] = hours, ["stamina_spent"] = staminaCost, ["qi_inspiration_gained"] = qiGained };
    }
    public Dictionary cultivate_with_world_stamina_for_hours(int requestedHours)
    {
        if (!can_cultivate()) return new Dictionary { ["success"] = false };
        var preview = get_world_cultivation_preview_for_hours(requestedHours);
        var strands = preview["strands"].AsInt32(); var hours = preview["hours"].AsInt32(); var staminaCost = preview["stamina_cost"].AsSingle();
        if (hours <= 0 || !try_spend_world_stamina(staminaCost)) return new Dictionary { ["success"] = false, ["reason"] = "体力不足" };
        human!.cultivation_progress = 0f; if (strands > 0) spirit_strands += strands;
        GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.add_hours(hours);
        var qiGained = settle_qi_inspiration_from_cultivation(hours);
        return new Dictionary { ["success"] = true, ["strands"] = strands, ["hours"] = hours, ["stamina_spent"] = staminaCost, ["qi_inspiration_gained"] = qiGained };
    }
    public Dictionary study_for_world_hours(int requestedHours)
    {
        if (human is null || requestedHours <= 0) return new Dictionary { ["success"] = false };
        var hours = get_available_study_hours(requestedHours);
        if (hours <= 0) return new Dictionary { ["success"] = false, ["reason"] = "体力不足", ["hours"] = 0 };
        if (_study_books.Count == 0 && !start_pending_intro_gongfa_study()) return new Dictionary { ["success"] = false };
        var book = _study_books.FirstOrDefault();
        if (book is null || (!book.is_studying_now && !book.start_study(this))) return new Dictionary { ["success"] = false };
        var staminaCost = hours * WorldStaminaRules.StudyCostPerGameHour;
        if (hours <= 0 || !try_spend_world_stamina(staminaCost)) return new Dictionary { ["success"] = false, ["reason"] = "体力不足", ["hours"] = 0 };
        var before = book.current_study_progress;
        book.advance_study_hours(hours);
        GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.add_hours(hours);
        return new Dictionary { ["success"] = true, ["hours"] = hours, ["requested_hours"] = requestedHours, ["stamina_spent"] = staminaCost, ["progress_gained"] = Mathf.Max(0f, book.current_study_progress - before) };
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
    public bool manual_cultivate(){if(!can_cultivate()||!try_spend_world_stamina(WorldStaminaRules.CultivationCostPerGameHour))return false;advance_cultivation_hours(1f);GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.add_hours(1);settle_qi_inspiration_from_cultivation(1f);return true;}
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
    public Dictionary get_cultivation_preview_for_strands(int strands){var actual=Mathf.Clamp(strands,0,get_strands_until_next_realm());var rate=Mathf.Max(.0001f,get_cultivation_rate());var hours=actual<=0?0:Mathf.CeilToInt(actual/rate);return new Dictionary{{"strands",actual},{"hours",hours},{"days",hours/24f},{"rate",rate}};}
    public Dictionary get_cultivation_preview_for_hours(int hours){var safeHours=Mathf.Max(0,hours);var actual=Mathf.Min(get_strands_until_next_realm(),Mathf.FloorToInt(get_cultivation_rate()*safeHours));return new Dictionary{{"strands",actual},{"hours",safeHours},{"days",safeHours/24f},{"rate",get_cultivation_rate()}};}
    public Dictionary cultivate_for_strands(int strands) => cultivate_with_world_stamina_for_strands(strands);
    public Dictionary cultivate_for_hours(int hours) => cultivate_with_world_stamina_for_hours(hours);

    private float get_qi_insight_value(string element) => element switch
    {
        "木" => human?.qi_wood_insight ?? 0f,
        "火" => human?.qi_fire_insight ?? 0f,
        "土" => human?.qi_earth_insight ?? 0f,
        "金" => human?.qi_metal_insight ?? 0f,
        "水" => human?.qi_water_insight ?? 0f,
        _ => 0f
    };

    private void add_qi_insight_value(string element, float amount)
    {
        if (human is null || amount == 0f || !QiNodeCatalog.IsTrait(element)) return;
        switch (element)
        {
            case "木": human.qi_wood_insight = Mathf.Max(0f, human.qi_wood_insight + amount); break;
            case "火": human.qi_fire_insight = Mathf.Max(0f, human.qi_fire_insight + amount); break;
            case "土": human.qi_earth_insight = Mathf.Max(0f, human.qi_earth_insight + amount); break;
            case "金": human.qi_metal_insight = Mathf.Max(0f, human.qi_metal_insight + amount); break;
            case "水": human.qi_water_insight = Mathf.Max(0f, human.qi_water_insight + amount); break;
        }
    }

    public Dictionary get_qi_insight_snapshot() => new()
    {
        ["木"] = get_qi_insight_value("木"),
        ["火"] = get_qi_insight_value("火"),
        ["土"] = get_qi_insight_value("土"),
        ["金"] = get_qi_insight_value("金"),
        ["水"] = get_qi_insight_value("水"),
        ["total"] = QiNodeCatalog.Traits.Sum(get_qi_insight_value)
    };

    private int qi_now() => GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;

    private void prune_qi_inspirations()
    {
        if (human is null) return;
        var now = qi_now();
        for (var index = human.qi_inspirations.Count - 1; index >= 0; index--)
        {
            var value = human.qi_inspirations[index];
            if (value.VariantType != Variant.Type.Dictionary)
            {
                human.qi_inspirations.RemoveAt(index);
                continue;
            }
            var entry = value.AsGodotDictionary();
            if (entry.GetValueOrDefault("expires_at_hour", now).AsInt32() <= now) human.qi_inspirations.RemoveAt(index);
        }
    }

    /// <summary>返回当前仍在窗口内的灵感；绝对失效时刻保存在存档中，参悟本身不推进时间。</summary>
    public Godot.Collections.Array get_qi_inspirations()
    {
        var result = new Godot.Collections.Array();
        // 灵感只属于云息期；离开当前大境界后不再把旧条目带回任何后续界面。
        if (human is null || !is_qi_cloud_breath()) return result;
        prune_qi_inspirations();
        var now = qi_now();
        foreach (Variant value in human.qi_inspirations)
        {
            if (value.VariantType != Variant.Type.Dictionary) continue;
            var entry = value.AsGodotDictionary().Duplicate(true);
            var remainingHours = Mathf.Max(0, entry.GetValueOrDefault("expires_at_hour", now).AsInt32() - now);
            entry["remaining_hours"] = remainingHours;
            entry["remaining_days"] = remainingHours / 24f;
            result.Add(entry);
        }
        return result;
    }

    /// <summary>所有活动统一从九品起步；传入 grade 仅用于已确定品质的稀有事件。</summary>
    public Dictionary grant_qi_inspiration(string source, string element = "", int grade = 0)
    {
        if (human is null || !is_qi_cloud_breath() || main_gongfa is null || is_in_breakthrough_retreat())
            return new Dictionary{{"success",false},{"reason","当前无法获得行炁灵感"}};
        prune_qi_inspirations();
        var trait = QiNodeCatalog.IsTrait(element) ? element : QiNodeCatalog.Traits[(int)_qi_insight_rng.RandiRange(0, QiNodeCatalog.Traits.Count - 1)];
        var actualGrade = grade <= 0 ? QiInspirationCatalog.RollGrade(source, _qi_insight_rng) : QiInspirationCatalog.ClampGrade(grade);
        var now = qi_now();
        var entry = new Dictionary
        {
            {"trait", trait},
            {"grade", actualGrade},
            {"grade_name", QiInspirationCatalog.GradeName(actualGrade)},
            {"insight", QiInspirationCatalog.InsightReward(actualGrade)},
            {"source", QiInspirationCatalog.SourceName(source)},
            {"created_at_hour", now},
            {"expires_at_hour", now + QiInspirationCatalog.DurationHours(actualGrade)}
        };
        human.qi_inspirations.Add(entry);
        return new Dictionary{{"success",true},{"inspiration",entry.Duplicate(true)},{"trait",trait},{"grade",actualGrade},{"grade_name",QiInspirationCatalog.GradeName(actualGrade)}};
    }

    /// <summary>供论道、切磋、探索等活动调用的语义别名。</summary>
    public Dictionary gain_qi_inspiration(string source, string element = "") => grant_qi_inspiration(source, element);

    /// <summary>
    /// 活动结算入口。只有活动真正完成时才会发放灵感；失败、撤退或未完成的尝试不生成条目。
    /// 品级仍由来源的九品权重决定，特殊事件可传入已确定的 grade。
    /// </summary>
    public Dictionary settle_qi_activity_inspiration(string source, bool completed, string element = "", int grade = 0)
    {
        if (!completed)
            return new Dictionary { ["success"] = false, ["triggered"] = false, ["reason"] = "活动未完成，不产生灵感" };
        var result = grant_qi_inspiration(source, element, grade);
        result["triggered"] = result.GetValueOrDefault("success", false).AsBool();
        return result;
    }

    public bool can_practice_qi_insight() => human is not null && is_qi_cloud_breath() && main_gongfa is not null && !is_in_breakthrough_retreat() && get_qi_inspirations().Count > 0;

    /// <summary>参悟一条已获得的灵感，立即把其收益写入对应五行特性，不消耗时间。</summary>
    public Dictionary consume_qi_inspiration(int index)
    {
        if (human is null || !is_qi_cloud_breath() || main_gongfa is null || is_in_breakthrough_retreat()) return new Dictionary{{"success",false},{"reason","当前无法参悟灵感"}};
        prune_qi_inspirations();
        if (index < 0 || index >= human.qi_inspirations.Count) return new Dictionary{{"success",false},{"reason","该灵感已失效"}};
        var entry = human.qi_inspirations[index].AsGodotDictionary();
        var element = entry.GetValueOrDefault("trait", "").AsString();
        var amount = entry.GetValueOrDefault("insight", 0f).AsSingle();
        if (!QiNodeCatalog.IsTrait(element) || amount <= 0f) return new Dictionary{{"success",false},{"reason","灵感数据无效"}};
        add_qi_insight_value(element, amount);
        human.qi_inspirations.RemoveAt(index);
        return new Dictionary{{"success",true},{"element",element},{"insight",amount},{"grade",entry.GetValueOrDefault("grade",1).AsInt32()},{"grade_name",entry.GetValueOrDefault("grade_name","九品").AsString()},{"source",entry.GetValueOrDefault("source","未知活动").AsString()}};
    }

    /// <summary>兼容旧界面调用：按特性选择当前最优的一条灵感，但不再消耗固定时间。</summary>
    public Dictionary practice_qi_insight(string element)
    {
        if (!can_practice_qi_insight() || !QiNodeCatalog.IsTrait(element)) return new Dictionary{{"success",false},{"reason","当前没有可参悟的对应灵感"}};
        var inspirations = get_qi_inspirations();
        var selected = -1; var bestGrade = -1;
        for (var index = 0; index < inspirations.Count; index++)
        {
            var entry = inspirations[index].AsGodotDictionary();
            if (entry.GetValueOrDefault("trait", "").AsString() != element) continue;
            var grade = entry.GetValueOrDefault("grade", 1).AsInt32();
            if (grade > bestGrade) { bestGrade = grade; selected = index; }
        }
        return selected < 0 ? new Dictionary{{"success",false},{"reason","当前没有可参悟的对应灵感"}} : consume_qi_inspiration(selected);
    }

    /// <summary>修炼每满二十四小时进行一次灵感判定；连续八次未触发后第九次保底。</summary>
    private float settle_qi_inspiration_from_cultivation(float hours)
    {
        if (human is null || !is_qi_cloud_breath() || hours <= 0f) return 0f;
        human.qi_cultivation_hours += hours;
        var wholeDays = Mathf.FloorToInt(human.qi_cultivation_hours / 24f);
        if (wholeDays <= 0) return 0f;
        human.qi_cultivation_hours -= wholeDays * 24f;
        var gained = 0f;
        for (var index = 0; index < wholeDays; index++)
        {
            var trigger = human.qi_cultivation_failed_rolls >= 8 || _qi_insight_rng.Randf() < 0.08f;
            if (!trigger)
            {
                human.qi_cultivation_failed_rolls++;
                continue;
            }
            var result = grant_qi_inspiration("修炼");
            if (result.GetValueOrDefault("success", false).AsBool()) gained++;
            human.qi_cultivation_failed_rolls = 0;
        }
        return gained;
    }

    public Dictionary get_qi_node_levels()
    {
        var result = new Dictionary();
        if (human is null) return result;
        foreach (Variant key in human.qi_node_levels.Keys) result[key.AsString()] = human.qi_node_levels[key].AsInt32();
        return result;
    }

    public int get_qi_node_level(string nodeId)
    {
        var node = QiNodeCatalog.Get(nodeId);
        if (human is null || node is null) return 0;
        return Mathf.Clamp(human.qi_node_levels.GetValueOrDefault(nodeId, 0).AsInt32(), 0, node.MaxLevel);
    }

    private static int qi_prerequisite_required_level(QiNodeDefinition node, QiNodeDefinition prerequisite, int targetLevel)
    {
        return node.Kind switch
        {
            "生" or "克" => targetLevel >= 2 ? 2 : 1,
            "用" => prerequisite.Kind == "根" ? 2 : targetLevel >= 2 ? 2 : 1,
            "合" => targetLevel >= 2 ? 2 : 1,
            _ => 1
        };
    }

    private bool qi_node_prerequisites_met(QiNodeDefinition node, int targetLevel, out Array<string> missing)
    {
        missing = [];
        foreach (var prerequisite in node.Prerequisites)
        {
            var prerequisiteNode = QiNodeCatalog.Get(prerequisite);
            var requiredLevel = prerequisiteNode is null ? 1 : qi_prerequisite_required_level(node, prerequisiteNode, targetLevel);
            if (get_qi_node_level(prerequisite) < requiredLevel) missing.Add(prerequisite);
        }
        return missing.Count == 0;
    }

    public Dictionary get_qi_node_status(string nodeId)
    {
        var node = QiNodeCatalog.Get(nodeId);
        if (node is null) return new Dictionary{{"valid",false}};
        var level = get_qi_node_level(nodeId);
        var nextLevel = level + 1;
        var status = new Dictionary
        {
            ["valid"] = true,
            ["level"] = level,
            ["max_level"] = node.MaxLevel,
            ["next_level"] = nextLevel,
            ["maxed"] = level >= node.MaxLevel
        };
        if (level >= node.MaxLevel)
        {
            status["available"] = false;
            status["affordable"] = false;
            status["cost"] = 0f;
            status["cost_text"] = "已达最高阶";
            status["missing_prerequisites"] = new Array<string>();
            return status;
        }

        qi_node_prerequisites_met(node, nextLevel, out var missing);
        var missingText = string.Join("、", missing.Select(id =>
        {
            var prerequisite = QiNodeCatalog.Get(id);
            var requiredLevel = prerequisite is null ? 1 : qi_prerequisite_required_level(node, prerequisite, nextLevel);
            return $"{prerequisite?.Name ?? id}（需{requiredLevel}阶）";
        }));
        var perTraitCost = node.GetLevelCost(nextLevel);
        var affordable = node.CostElements.All(element => get_qi_insight_value(element) + 0.0001f >= perTraitCost);
        status["available"] = missing.Count == 0;
        status["affordable"] = affordable;
        status["cost"] = perTraitCost;
        status["cost_text"] = node.GetCostText(nextLevel);
        status["missing_prerequisites"] = missing;
        status["missing_prerequisites_text"] = missingText;
        return status;
    }

    public bool can_unlock_qi_node(string nodeId)
    {
        if (!is_qi_cloud_breath() || human is null || main_gongfa is null || is_in_breakthrough_retreat()) return false;
        var status = get_qi_node_status(nodeId);
        return status.GetValueOrDefault("valid", false).AsBool()
            && status.GetValueOrDefault("available", false).AsBool()
            && status.GetValueOrDefault("affordable", false).AsBool();
    }

    /// <summary>提升一个节点一级，分别从所有参与特性中扣除同额参悟值。</summary>
    public Dictionary unlock_qi_node(string nodeId)
    {
        var node = QiNodeCatalog.Get(nodeId);
        if (node is null || !can_unlock_qi_node(nodeId)) return new Dictionary{{"success",false}};
        var nextLevel = get_qi_node_level(nodeId) + 1;
        var cost = node.GetLevelCost(nextLevel);
        foreach (var element in node.CostElements) add_qi_insight_value(element, -cost);
        human!.qi_node_levels[nodeId] = nextLevel;
        return new Dictionary{{"success",true},{"node_id",nodeId},{"level",nextLevel},{"cost",node.GetCostText(nextLevel)}};
    }
    // 单人模式不再提供研读位；内部只保留当前读本，以兼容已有存档进度。
    public Array<Dictionary> get_study_slot_snapshot(){var result=new Array<Dictionary>();var book=_study_books.FirstOrDefault();var slot=new Dictionary{{"slot_index",0},{"occupied",book is not null}};if(book is not null)slot["book"]=book;result.Add(slot);return result;} public bool has_free_study_slot()=>_study_books.Count<1;
    public GongfaResource? get_current_study_gongfa()=>_study_books.FirstOrDefault()?.gongfa;
    /// <summary>单人模式只有一部当前读本。切换时暂停原书，并保留其已累计的卷章进度。</summary>
    public bool select_gongfa_for_study(GongfaResource gongfa)
    {
        if (gongfa is null || human is null) return false;
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
        if (human is null || gongfa is null || !has_free_study_slot() || _study_books.Any(book => book.gongfa == gongfa)) return false;
        var book = new SecretBook { gongfa = gongfa, required_study_progress = gongfa.required_study_progress, can_be_interrupted = false, Name = "Study_" + gongfa.gongfa_name };
        AddChild(book); _study_books.Add(book);
        book.start_study(this); // 条件未达标时保留当前读本，不丢失去向。
        StudyStarted?.Invoke(this, gongfa);
        return true;
    }
    /// <summary>序章交付选定秘籍的首卷。</summary>
    public bool grant_intro_gongfa(GongfaResource gongfa)
    {
        if (human is null || gongfa is null || !gongfa.can_be_main) return false;
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
    /// <summary>测试跳过序章时写入一个与正常流程等价的起始状态：首卷已读、主修已装配，并落在云息三层门槛。</summary>
    public bool prepare_prologue_skip_state(GongfaResource gongfa)
    {
        if (human is null || gongfa is null || !gongfa.can_be_main) return false;
        var first = gongfa.get_sorted_chapters().FirstOrDefault();
        if (first is null) return false;

        human.intro_gongfa_path = gongfa.ResourcePath;
        human.intro_gongfa_study_progress = 0f;
        var learned = human.learned_gongfa_chapters.GetValueOrDefault(gongfa.ResourcePath, new Array<string>()).AsGodotArray();
        if (!learned.Contains(first.get_id())) learned.Add(first.get_id());
        human.learned_gongfa_chapters[gongfa.ResourcePath] = learned;
        human.gongfa_chapter_study_progress.Remove(gongfa.ResourcePath + ":" + first.get_id());
        if (!human.learned_gongfa_paths.Contains(gongfa.ResourcePath)) human.learned_gongfa_paths.Add(gongfa.ResourcePath);
        apply_gongfa_chapter_permanent_bonuses(gongfa, first.get_id());

        cultivation_realm = "云息三层";
        spirit_strands = 36;
        human.cultivation_progress = 0f;
        clear_breakthrough_retreat();
        if (!switch_main_gongfa(gongfa)) return false;
        refresh_gongfa_effects();
        return main_gongfa == gongfa && is_gongfa_chapter_learned(gongfa, first.get_id());
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
        if(actualHours<=0||!try_spend_world_stamina(staminaCost))return new Dictionary{{"success",false},{"reason","体力不足"},{"hours",0}};
        var before=book.current_study_progress; book.advance_study_hours(actualHours); GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.add_hours(actualHours);
        return new Dictionary{{"success",true},{"hours",actualHours},{"requested_hours",hours},{"stamina_spent",staminaCost},{"progress_gained",Mathf.Max(0,book.current_study_progress-before)}};
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
        GongfaChapterCompleted?.Invoke(this, gongfa.gongfa_name, chapter_id, chapterName);
        apply_gongfa_chapter_permanent_bonuses(gongfa, chapter_id);
        refresh_gongfa_effects();
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
    private void apply_gongfa_chapter_permanent_bonuses(GongfaResource gongfa, string chapterId)
    {
        if (human is null) return;
        var stamp = gongfa.ResourcePath + ":" + chapterId;
        if (human.applied_gongfa_permanent_bonuses.ContainsKey(stamp)) return;
        var chapter = gongfa.get_chapter_by_id(chapterId); if (chapter is null) return;
        foreach (Variant key in chapter.learn_permanent_bonuses.Keys) apply_permanent_attribute(key.AsString(), chapter.learn_permanent_bonuses[key].AsSingle());
        var bookStamp = gongfa.ResourcePath + ":book";
        if (!human.applied_gongfa_permanent_bonuses.ContainsKey(bookStamp))
        {
            foreach (Variant key in gongfa.learn_permanent_bonuses.Keys) apply_permanent_attribute(key.AsString(), gongfa.learn_permanent_bonuses[key].AsSingle());
            human.applied_gongfa_permanent_bonuses[bookStamp] = true;
        }
        human.applied_gongfa_permanent_bonuses[stamp] = true;
    }
    private void apply_permanent_attribute(string key, float value)
    {
        if (human is null || Mathf.IsZeroApprox(value)) return;
        switch (key)
        {
            case "physique": human.base_physique += value; break;
            case "intelligence": human.base_intelligence += value; break;
            case "agility": human.base_agility += value; break;
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
        human.body_proficiency = human.base_body_proficiency; human.magic_proficiency = human.base_magic_proficiency; human.forging_proficiency = human.base_forging_proficiency;
        human.talisman_proficiency = human.base_talisman_proficiency; human.formation_proficiency = human.base_formation_proficiency; human.alchemy_proficiency = human.base_alchemy_proficiency;
        foreach (var book in new[] { main_gongfa }.Concat(sub_gongfa_list))
        {
            if (book is null) continue; var isMain = book == main_gongfa;
            foreach (var chapter in book.get_sorted_chapters())
            {
                if (!is_gongfa_chapter_learned(book, chapter.get_id())) continue;
                foreach (Variant key in chapter.get_naqi_bonuses(isMain).Keys) apply_runtime_gongfa_bonus(key.AsString(), chapter.get_naqi_bonuses(isMain)[key].AsSingle());
            }
        }
        human.mana_regen_rate = human.base_mana_regen_rate * get_gongfa_naqi_multiplier("mana_regen_rate");
        human.mind_regen_rate = human.base_mind_regen_rate * get_gongfa_naqi_multiplier("mind_regen_rate");
        apply_body_refinement_rewards();
    }

    private void apply_body_refinement_rewards()
    {
        if (human is null) return;
        foreach (var part in BodyRefinementRules.Parts)
        {
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
            case "physique": physique += value; break; case "intelligence": intelligence += value; break; case "agility": agility += value; break; case "soul": soul += value; break;
            case "physical_defense": physical_defense += Mathf.RoundToInt(value); break; case "precision": precision += Mathf.RoundToInt(value); break;
            case "critical": critical += Mathf.RoundToInt(value); break;
            case "crit_resist": crit_resist += Mathf.RoundToInt(value); break; case "crit_damage_mult": crit_damage_mult += value; break;
            case "initiative_speed": initiative_speed += value; break; case "max_spirit_perception": max_spirit_perception += Mathf.RoundToInt(value); break;
            case "max_health": max_health += Mathf.RoundToInt(value); current_health = Mathf.Min(current_health, max_health); break;
            case "max_qi_blood": max_qi_blood += Mathf.RoundToInt(value); break;
            case "blood_furnace_value_cap": blood_furnace_value_cap += Mathf.RoundToInt(value); break;
            case "body_proficiency": human.body_proficiency += value; break; case "magic_proficiency": human.magic_proficiency += value; break; case "sword_proficiency": human.sword_proficiency += value; break;
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
    /// <summary>凝核牌局成功后的唯一写回点。不会改变既有道承。</summary>
    public bool complete_condensation(float volume,float density,float impurity,string quality)
    {
        if(human is null||!requires_condensation_breakthrough()||!is_breakthrough_ready()||impurity>=CondensationCoreState.FailureImpurity)return false;
        human.has_spirit_core=true;
        human.core_count=1;
        human.spirit_core_volume=Mathf.Max(CondensationCoreState.MinimumVolume,volume);
        human.spirit_core_density=Mathf.Max(CondensationCoreState.MinimumDensity,density);
        human.spirit_core_impurity=Mathf.Clamp(impurity,0f,CondensationCoreState.FailureImpurity-0.0001f);
        human.spirit_core_quality=quality;
        cultivation_realm="云息七层";
        recalculate_attributes();
        refresh_basic_deck_for_bearing();
        EmitSignal(SignalName.breakthrough_completed,this,cultivation_realm);
        BreakthroughCompleted?.Invoke(this,cultivation_realm);
        return true;
    }
    public string get_spirit_core_summary()
    {
        if(!has_spirit_core)return "未凝";
        var title=string.IsNullOrWhiteSpace(spirit_core_quality)?"云息灵核":spirit_core_quality;
        return $"{title}　体 {spirit_core_volume:0.00}　密 {spirit_core_density:0.00}　杂 {spirit_core_impurity:0.00}";
    }
    public bool is_breakthrough_ready(){var realms=GetNodeOrNull<RealmManager>("/root/RealmManager");return !is_in_breakthrough_retreat()&&is_cultivation_blocked_by_breakthrough()&&realms is not null&&realms.can_breakthrough(this);}
    public int get_breakthrough_base_hours()
    {
        var realms=GetNodeOrNull<RealmManager>("/root/RealmManager"); var next=realms?.get_next_realm_name(cultivation_realm)??"";
        var tier=next.StartsWith("云息")&&next.EndsWith("层")&&int.TryParse(next[2..^1],out var value)?value:1;
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
    public bool start_breakthrough_retreat(bool useMeditationRoom=false)
    {
        if(human is null||!is_breakthrough_ready())return false;
        if(useMeditationRoom&&!can_use_meditation_room_for_retreat())return false;
        if(useMeditationRoom&&meditation_room_index<0)
        {
            foreach(var node in GetTree().GetNodesInGroup("main_scene")) if(node is MainScene main && !main.assign_available_meditation_room(this)) return false;
        }
        var realms=GetNodeOrNull<RealmManager>("/root/RealmManager"); var time=GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        if(realms is null||time is null)return false;
        var next=realms.get_next_realm_name(cultivation_realm); if(string.IsNullOrEmpty(next))return false;
        var range=get_breakthrough_retreat_hour_range(useMeditationRoom); var minHours=range.GetValueOrDefault("min_hours",12).AsInt32(); var maxHours=range.GetValueOrDefault("max_hours",minHours).AsInt32(); var retreatHours=new RandomNumberGenerator(); retreatHours.Randomize(); var actualHours=(int)retreatHours.RandiRange(minHours,maxHours); human.is_in_breakthrough_retreat=true; human.breakthrough_target_realm=next; human.breakthrough_started_hour=time.get_total_game_hours(); human.breakthrough_finish_hour=human.breakthrough_started_hour+actualHours; human.breakthrough_uses_meditation_room=useMeditationRoom;
        EmitSignal(SignalName.breakthrough_started,this,next); time.add_hours(actualHours); return true;
    }
    public Dictionary get_breakthrough_retreat_summary()
    {
        if(human is null||!human.is_in_breakthrough_retreat)return new Dictionary{{"active",false}};
        var time=GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager"); var remaining=Mathf.Max(0,human.breakthrough_finish_hour-(time?.get_total_game_hours()??human.breakthrough_started_hour));
        return new Dictionary{{"active",true},{"target",human.breakthrough_target_realm},{"remaining_hours",remaining},{"total_hours",Mathf.Max(0,human.breakthrough_finish_hour-human.breakthrough_started_hour)},{"uses_meditation_room",human.breakthrough_uses_meditation_room}};
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
        var next=human.breakthrough_target_realm; if(string.IsNullOrEmpty(next)||realms is null||!realms.can_breakthrough(this)){clear_breakthrough_retreat();return;}
        cultivation_realm=next; clear_breakthrough_retreat(); recalculate_attributes(); refresh_basic_deck_for_bearing(); EmitSignal(SignalName.breakthrough_completed,this,next); BreakthroughCompleted?.Invoke(this,next);
    }
    private void clear_breakthrough_retreat(){if(human is null)return;human.is_in_breakthrough_retreat=false;human.breakthrough_target_realm="";human.breakthrough_started_hour=0;human.breakthrough_finish_hour=0;human.breakthrough_uses_meditation_room=false;}
    /// <summary>按实际投入小时累计灵力；速率单位为“缕/小时”，小数部分进入存档而非被截断。</summary>
    private int advance_cultivation_hours(float hours)
    {
        if (human is null || hours <= 0 || !can_cultivate()) return 0;
        human.cultivation_progress += get_cultivation_rate() * hours;
        add_body_nourish_progress(hours);
        add_visualization_nourish_progress(hours);
        consume_meditation_spirit_stones(hours);
        var gained=Mathf.FloorToInt(human.cultivation_progress);
        if (gained <= 0) return 0;
        human.cultivation_progress-=gained; spirit_strands+=gained;
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

    /// <summary>温观是蕴神的保底来源。当前主修景得六成，其余两景各得两成；未装配景永不被动增长。</summary>
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
        if (!string.IsNullOrEmpty(material_path) && !validMaterial) return new Dictionary { ["valid"] = false, ["reason"] = "所选材料不符合此淬体法的质地、功用或品阶要求。" };
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
    public string get_dao_bearing_name()=>dao_bearing switch{1=>"锻躯",2=>"行炁",3=>"蕴神",_=>"未定"}; public bool has_dao_bearing()=>dao_bearing!=0; public bool needs_dao_bearing_for_next_realm()=>false; public bool can_set_dao_bearing(int bearing)=>bearing is >=1 and <=3; public string get_dao_bearing_material_name(int bearing)=>bearing switch{1=>"妖丹",2=>"灵石",3=>"魂玉",_=>""};
    public IReadOnlyList<DaoBearingNodeDefinition> get_dao_bearing_nodes() => DaoBearingNodeCatalog.For(dao_bearing);
    public bool is_dao_node_unlocked(string nodeId) => human?.dao_node_ids.Contains(nodeId) == true;
    public bool is_dao_node_available(string nodeId)
    {
        if (human is null || dao_bearing == 0) return false;
        var node = get_dao_bearing_nodes().FirstOrDefault(item => item.Id == nodeId);
        if (node is null || node.IsPlanned || is_dao_node_unlocked(node.Id)) return false;
        if (!node.PrerequisiteIds.All(is_dao_node_unlocked)) return false;
        return DaoBearingNodeCatalog.MeetsAttributes(this, node);
    }
    /// <summary>按当前属性自动纳入道承节点；未定稿的预留节点不会被自动解锁。</summary>
    public void refresh_dao_bearing_nodes()
    {
        if (human is null || dao_bearing == 0) return;
        human.dao_node_ids ??= [];
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var node in get_dao_bearing_nodes())
            {
                if (node.IsPlanned || is_dao_node_unlocked(node.Id)) continue;
                if (!node.PrerequisiteIds.All(is_dao_node_unlocked) || !DaoBearingNodeCatalog.MeetsAttributes(this, node)) continue;
                human.dao_node_ids.Add(node.Id);
                add_dao_node_reward_skills(node);
                changed = true;
            }
        }
    }
    private void add_dao_node_reward_skills(DaoBearingNodeDefinition node)
    {
        foreach (var path in node.RewardSkillPaths)
        {
            var skill = ResourceLoader.Load<ActiveSkillData>(path);
            if (skill is null || active_skills.Any(item => item is not null && item.ResourcePath == path)) continue;
            active_skills.Add(skill);
        }
    }
    private void lock_dao_bearing_from_main()
    {
        if (human is null) return;
        if (human.dao_bearing == 0) set_cultivation_focus_from_main();
        else refresh_basic_deck_for_bearing();
    }
    private void set_cultivation_focus_from_main()
    {
        if (human is null || main_gongfa is null || !main_gongfa.can_be_main) return;
        var candidates = new List<int>();
        foreach (var bearing in main_gongfa.bearing_compatibility)
        {
            if (bearing is >= 1 and <= 3 && !candidates.Contains(bearing)) candidates.Add(bearing);
        }
        if (candidates.Count == 1 || candidates.Count > 1 && !candidates.Contains(human.dao_bearing)) human.dao_bearing = candidates[0];
        refresh_dao_bearing_nodes();
        refresh_basic_deck_for_bearing();
    }
    /// <summary>当前主要侧重决定正在使用的基础技能库；它不限制其他养成线，也不清除既有进度。</summary>
    public void refresh_basic_deck_for_bearing()
    {
        if (human is null) return;
        var routePath = human.dao_bearing switch
        {
            1 => BodyDeckPath,
            2 => "res://resources/basic_decks/纳气路.tres",
            3 => "res://resources/basic_decks/神魂路.tres",
            _ => ""
        };
        var routeDeck = string.IsNullOrEmpty(routePath) ? null : ResourceLoader.Load<BasicDeckResource>(routePath);
        human.basic_deck_path = routeDeck is not null && routeDeck.is_unlocked_for(this) ? routePath : InitialDeckPath;
    }
    public override void apply_battle_data(BattleEntityData snapshot)
    {
        base.apply_battle_data(snapshot);
        if(human is null)return;
        human.body_proficiency=(int)snapshot.body_proficiency;
        human.sword_proficiency=(int)snapshot.sword_proficiency;
        human.magic_proficiency=(int)snapshot.magic_proficiency;
        human.formation_proficiency=snapshot.formation_proficiency;
        human.formation_circuits=snapshot.formation_circuits.Duplicate();
        human.formation_manual_ids=snapshot.formation_manual_ids.Duplicate();
        human.dao_bearing=snapshot.dao_bearing;
        human.basic_deck_path=snapshot.basic_deck_path;
        human.combat_skill_slot_paths=snapshot.combat_skill_slot_paths.Duplicate();
        human.combat_skill_slots_configured=snapshot.combat_skill_slots_configured;
        human.combat_passive_paths=snapshot.combat_passive_paths.Duplicate();
        human.main_gongfa_path=snapshot.main_gongfa_path;
        human.sub_gongfa_paths=snapshot.sub_gongfa_paths.Duplicate();
        load_gongfa_data();
        refresh_dao_bearing_nodes();
    }
    public override BattleEntityData get_snapshot_data()
    {
        var snapshot = base.get_snapshot_data();
        if (human is null) return snapshot;
        ensure_basic_deck();
        refresh_dao_bearing_nodes();
        refresh_basic_deck_for_bearing();
        snapshot.dao_bearing = human.dao_bearing;
        snapshot.body_proficiency=human.body_proficiency;
        snapshot.sword_proficiency=human.sword_proficiency;
        snapshot.magic_proficiency=human.magic_proficiency;
        snapshot.formation_proficiency=human.formation_proficiency;
        snapshot.formation_circuits=human.formation_circuits.Duplicate();
        snapshot.formation_manual_ids=human.formation_manual_ids.Duplicate();
        snapshot.basic_deck_path = human.basic_deck_path;
        snapshot.basic_deck_upgrade_ids = human.basic_deck_upgrade_ids.Duplicate();
        snapshot.loaded_skill_paths = human.loaded_skill_paths.Duplicate();
        snapshot.combat_skill_slot_paths = get_combat_skill_slot_paths();
        snapshot.combat_skill_slots_configured = human.combat_skill_slots_configured;
        snapshot.combat_passive_paths = get_combat_passive_paths();
        snapshot.combat_tempering_by_gongfa = get_active_combat_tempering_by_gongfa();
        snapshot.battle_deck_configured = human.battle_deck_configured;
        snapshot.main_gongfa_path = human.main_gongfa_path;
        snapshot.sub_gongfa_paths = human.sub_gongfa_paths.Duplicate();
        foreach (var skill in get_available_battle_skills()) if (!string.IsNullOrEmpty(skill.ResourcePath)) snapshot.active_skill_paths.Add(skill.ResourcePath);
        snapshot.pack_item_paths = pack_items.Duplicate();
        snapshot.pack_item_counts = pack_counts.Duplicate();
        return snapshot;
    }
    public bool equip_main_gongfa(GongfaResource gongfa)=>switch_main_gongfa(gongfa);
    public bool switch_main_gongfa(GongfaResource gongfa)
    {
        if(gongfa is null||human is null||!has_learned_gongfa(gongfa)||!gongfa.can_be_main||!gongfa.is_wuxing_compatible(spirit_root_type))return false;
        remove_sub_gongfa(gongfa);main_gongfa=gongfa;human.main_gongfa_path=gongfa.ResourcePath;set_cultivation_focus_from_main();refresh_gongfa_effects();MainGongfaChanged?.Invoke(this,gongfa);return true;
    }
    public void unequip_main_gongfa(){main_gongfa=null;if(human is not null)human.main_gongfa_path="";refresh_gongfa_effects();}
    public bool add_sub_gongfa(GongfaResource gongfa)
    {
        if(gongfa is null||human is null||!has_learned_gongfa(gongfa)||!gongfa.can_be_sub||gongfa==main_gongfa||sub_gongfa_list.Contains(gongfa)||sub_gongfa_list.Count>=max_sub_slots)return false;
        sub_gongfa_list.Add(gongfa);human.sub_gongfa_paths.Add(gongfa.ResourcePath);refresh_gongfa_effects();return true;
    }
    public void remove_sub_gongfa(GongfaResource gongfa){sub_gongfa_list.Remove(gongfa);human?.sub_gongfa_paths.Remove(gongfa.ResourcePath);refresh_gongfa_effects();}
    public string get_sub_gongfa_chapter_route(GongfaResource gongfa,string chapterId)=>human?.equipped_gongfa_chapter_routes.GetValueOrDefault(gongfa.ResourcePath+":"+chapterId,"").AsString()??"";
    public void set_sub_gongfa_chapter_route(GongfaResource gongfa,string chapterId,string route){if(human is not null)human.equipped_gongfa_chapter_routes[gongfa.ResourcePath+":"+chapterId]=route;}
    public Array<string> get_learned_gongfa_chapter_ids(GongfaResource gongfa){var result=new Array<string>();if(human is null)return result;var values=human.learned_gongfa_chapters.GetValueOrDefault(gongfa.ResourcePath,new Array<string>()).AsGodotArray();foreach(var value in values)result.Add(value.AsString());return result;}
    public float get_gongfa_chapter_study_progress(GongfaResource gongfa,string id)=>human?.gongfa_chapter_study_progress.GetValueOrDefault(gongfa.ResourcePath+":"+id,0).AsSingle()??0; public void set_gongfa_chapter_study_progress(GongfaResource gongfa,string id,float value){if(human is not null)human.gongfa_chapter_study_progress[gongfa.ResourcePath+":"+id]=value;}
     public bool add_to_pack(string path,int count=1) => add_item_to_inventory(path,count);
     public Dictionary remove_from_pack(int index)
     {
         if (human is null || index < 0 || index >= human.inventory_item_paths.Count) return new Dictionary();
         var path = human.inventory_item_paths[index]; var count = human.inventory_item_counts[index];
         if (get_inventory_total_count(path) - get_equipped_item_count(path) < count) return new Dictionary();
         var result = new Dictionary { ["item_path"] = path, ["count"] = count };
         human.inventory_item_paths.RemoveAt(index); human.inventory_item_counts.RemoveAt(index); human.inventory_item_positions.RemoveAt(index); notify_inventory_changed(); return result;
     }
     public Array<string> get_pack_item_paths() => pack_items.Duplicate();
}
