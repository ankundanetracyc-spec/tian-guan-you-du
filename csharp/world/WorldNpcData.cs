using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;

/// <summary>百川区域人物的静态根源与动态状态。所有世界行为都必须能回溯到这里或行动记录。</summary>
public partial class WorldNpcData : Resource
{
    [Export] public string npc_id = "";
    [Export] public string npc_name = "";
    [Export] public string species = "人族";
    [Export] public int birth_year = 1;
    [Export(PropertyHint.Range, "1,12,1")] public int birth_month = 1;
    [Export] public string origin_node_id = "";
    [Export] public string origin_description = "";

    [Export] public string spirit_root_name = "无灵根";
    [Export] public Array<int> root_weights = [];
    [Export] public string cultivation_realm = "凡人";
    [Export] public int spirit_strands;
    [Export] public float physique = 5f;
    [Export] public float intelligence = 5f;
    [Export] public float agility = 5f;
    [Export] public float soul = 5f;
    [Export] public int current_health = 100;
    [Export] public int max_health = 100;
    /// <summary>大世界与副本共用的遁速评分；当前原型默认 100，公式沿用后续角色遁速细案。</summary>
    [Export] public float dun_speed = 100.0f;
    [Export] public int current_age = 18;
    [Export] public int max_age = 80;
    /// <summary>单一有符号业力值；不等同于逐势力态度或区域声望。</summary>
    [Export] public int karma_value;
    /// <summary>新心境轴值（-50～+50）；与旧战斗心境数值分开保存。</summary>
    [Export(PropertyHint.Range, "-50,50,1")] public int mood_value;

    [Export] public string faction_id = "";
    [Export] public string formal_identity = "";
    /// <summary>玉虚师承候选之间的稳定关联；不与私人关系分数合并。</summary>
    [Export] public bool is_yuxu_lineage_staff;
    [Export] public string sect_assigned_guide_npc_id = "";
    [Export] public string sect_teacher_npc_id = "";
    [Export] public Array<string> learned_gongfa_ids = [];
    [Export] public Array<string> equipment_ids = [];
    [Export] public Dictionary resources = [];
    /// <summary>当前原型的可交易物品库存；键为 ItemData 资源路径，值为数量。</summary>
    [Export] public Dictionary trade_inventory = [];
    /// <summary>个人实际随身物品，与交易库存、抽象资源余额分开保存；键为 ItemData 资源路径。</summary>
    [Export] public Dictionary carried_items = [];
    [Export] public int purchase_last_processed_month = -1;
    [Export] public NpcPurchaseRequest? purchase_request;
    /// <summary>只预留正式存档结构；不自动补贡献或发布私委。</summary>
    [Export] public Array<NpcPrivateCommissionEscrow> private_commission_escrows = [];

    [Export] public string personality = "";
    /// <summary>六轴定性性情；与心境、业力及关系分开，保存后不重抽。</summary>
    [Export] public Dictionary social_personality = [];
    [Export] public Array<string> social_priorities = [];
    /// <summary>玩家通过交际实际获知的信息和行为印象；不是 NPC 对玩家的记忆。</summary>
    [Export] public Dictionary player_known_npc_info = [];
    [Export] public string long_term_goal = "";
    [Export] public string short_term_goal = "";
    [Export] public Array<string> behavior_tags = [];
    [Export] public Array<string> relation_tags = [];
    /// <summary>Official case outcome state; never used as evidence or inferred from relationships.</summary>
    [Export] public int sect_case_demerit_count;
    [Export] public string sect_case_expelled_by_case_id = "";
    [Export] public string sect_case_former_faction_id = "";
    /// <summary>v6 单一关系分兼容字段；v7 后不参与关系、交往或交易判定。</summary>
    [Export] public int player_relationship;
    /// <summary>与主角实际交往的记忆；姓名、境界须经交流获知，不由好感分数推导。</summary>
    [Export] public Dictionary player_social_memory = [];
    /// <summary>这个 NPC 过月归纳的个人观感；与关系和玩家认知分开保存。</summary>
    [Export] public NpcPlayerImpressionData? player_impression;
    /// <summary>v6 NPC 间单一关系分兼容字段；v7 后不拆分、不参与判定。</summary>
    [Export] public Dictionary personal_relationships = [];
    /// <summary>NPC 对其他稳定人物 ID 的稀疏单向关系记录。</summary>
    [Export] public Dictionary directed_relationships = [];
    /// <summary>此 NPC 的关系证据已归纳至的月份。</summary>
    [Export] public int relationship_last_processed_month = -1;
    /// <summary>区域公开声望，按 region_id 分开；不与势力态度或私人关系合并。</summary>
    [Export] public Dictionary regional_renown = [];
    [Export] public Dictionary faction_reputations = [];

    [Export] public string current_node_id = "";
    [Export] public string current_subscene_id = "";
    [Export] public string current_state = "空闲";
    [Export] public bool is_traveling;
    [Export] public string travel_origin_node_id = "";
    [Export] public string travel_destination_node_id = "";
    [Export] public int travel_days_remaining;
    [Export] public int travel_days_total;
    /// <summary>当前百川固定格坐标；具名地点中的常驻人物以地点占地中心格作为落点。</summary>
    [Export] public Vector2I current_grid_cell = new(-1, -1);
    [Export] public bool has_world_grid_position;
    /// <summary>具名地点之间的逐格行旅路径，包含起点与终点；用于同格人物名单和读档续行。</summary>
    [Export] public Array<Vector2I> world_travel_path = [];
    [Export] public int world_travel_next_cell_index = 1;
    /// <summary>当前格边内已行进的游戏小时数。</summary>
    [Export] public float world_travel_cell_progress_hours;
    [Export] public bool is_alive = true;
    [Export] public string death_cause = "";
    /// <summary>Non-empty while this saved NPC is temporarily travelling as a player's case-task party member.</summary>
    [Export] public string temporary_party_task_id = "";
    /// <summary>副本内持久位置与行程；不覆盖大世界 node/subscene 位置。</summary>
    [Export] public string dungeon_id = "";
    [Export] public string dungeon_node_id = "";
    [Export] public string dungeon_previous_node_id = "";
    [Export] public float dungeon_travel_progress;
    [Export] public int dungeon_step_index;
    [Export] public int dungeon_placement_version;
    [Export] public Array<NpcActionRecord> action_history = [];
    private HashSet<string>? _dungeonActionTargets;

    public int get_resource(string resource_id) => resources.TryGetValue(resource_id, out var value) ? value.AsInt32() : 0;
    public void change_resource(string resource_id, int delta) => resources[resource_id] = Mathf.Max(0, get_resource(resource_id) + delta);
    public int get_carried_item_count(string itemPath) => carried_items.TryGetValue(itemPath, out var value) ? Math.Max(0, value.AsInt32()) : 0;
    public void change_carried_item(string itemPath, int delta)
    {
        if (string.IsNullOrWhiteSpace(itemPath) || delta == 0) return;
        var next = Math.Max(0, get_carried_item_count(itemPath) + delta);
        if (next == 0) carried_items.Remove(itemPath);
        else carried_items[itemPath] = next;
    }
    public long get_regional_renown(string regionId) => regional_renown.TryGetValue(regionId, out var value) ? value.AsInt64() : 0L;
    public bool has_dungeon_action_with_target(string targetNpcId)
    {
        if (_dungeonActionTargets is null)
        {
            _dungeonActionTargets = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in action_history)
                if (entry is not null && entry.action_id.StartsWith("dungeon_", StringComparison.Ordinal))
                    _dungeonActionTargets.Add(entry.target_npc_id);
        }
        return _dungeonActionTargets.Contains(targetNpcId);
    }

    public void record(NpcActionRecord entry)
    {
        action_history.Add(entry);
        NpcPlayerImpressionRules.OnRecorded(this, entry);
        if (_dungeonActionTargets is not null && entry.action_id.StartsWith("dungeon_", StringComparison.Ordinal))
            _dungeonActionTargets.Add(entry.target_npc_id);
    }
}
