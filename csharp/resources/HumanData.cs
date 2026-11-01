using Godot;
using Godot.Collections;

/// <summary>人类角色的持久化数据，供世界、生成器与战斗场景共同使用。</summary>
public partial class HumanData : LivingData
{
    [Export] public Array<string> alchemy_records = [];
    [Export] public Array<string> forging_records = [];
    [Export] public Array<CombatSkillReference> combat_skill_references = [];
    /// <summary>化法稳定来源 ID → 玩家明确选择的原始技能标识；不绑定槽位序号。</summary>
    [Export] public Godot.Collections.Dictionary<string, string> skill_rewrite_bindings = new();
    /// <summary>局外阵盘中已启用的个人战斗阵图；其运行资源不会写回本字段。</summary>
    [Export] public Godot.Collections.Array<FormationCircuitData> formation_circuits = [];
    [Export] public Array<string> formation_manual_ids = [];
    [Export] public int spirit_element;
    [Export] public Godot.Collections.Array spirit_affinity = [];
    [Export] public int core_count;
    /// <summary>云息六层至七层凝成的统一灵核结果。</summary>
    [Export] public bool has_spirit_core;
    /// <summary>八回合凝核全部结算后的最终程度。0表示没有新制结果，不由旧体/密/杂推算。</summary>
    [Export] public int condensation_final_score;
    [Export] public float spirit_core_volume = 1.0f;
    [Export] public float spirit_core_density = 1.0f;
    [Export] public float spirit_core_impurity;
    [Export] public string spirit_core_quality = "";
    /// <summary>历史分类存档兼容字段；不参与战斗、技能池、秘籍资格或养成判定。</summary>
    [Export] public int dao_bearing;
    /// <summary>历史进阶节点存档兼容字段；读取后不参与任何运行时判定。</summary>
    [Export] public Array<string> dao_node_ids = [];
    /// <summary>历史修炼分类存档兼容字段；不参与战斗状态初始化。</summary>
    [Export] public bool body_path_awakened;
    /// <summary>历史修炼分类存档兼容字段；不参与秘籍或技能筛选。</summary>
    [Export] public int energy_lineage;
    [Export] public int spirit_root_type;
    [Export] public string spirit_root_name = "无灵根";
    [Export] public Godot.Collections.Array root_weights = [0, 0, 0, 0, 0];
    /// <summary>当前加入的宗门 ID；空值表示尚未加入宗门。</summary>
    [Export] public string sect_affiliation_id = "";
    /// <summary>依入宗时灵根确定的起始身份，加入后保留。</summary>
    [Export] public string sect_entry_rank = "";
    /// <summary>当前宗门身份；后续晋升系统可变更此值而不覆盖起始身份。</summary>
    [Export] public string sect_current_rank = "";
    /// <summary>玉虚入门时接引弟子的稳定人物 ID。</summary>
    [Export] public string sect_admission_guide_npc_id = "";
    /// <summary>玉虚正式录籍的绝对游戏时辰；未知或旧存档为 -1。</summary>
    [Export] public long sect_entry_total_game_hours = -1;
    /// <summary>真传讲席下次开放时辰；旧档首次接入时对齐到当前周期，不追溯记缺。</summary>
    [Export] public long sect_true_disciple_lecture_next_due_hour = -1;
    /// <summary>真传最近一次讲席到场时辰；-1 表示尚未到场。</summary>
    [Export] public long sect_true_disciple_lecture_last_attended_hour = -1;
    /// <summary>连续申请并获准免赴的真传讲席期数；到场或缺席后归零，最多允许连续两期。</summary>
    [Export] public int sect_true_disciple_lecture_consecutive_leave_count;
    /// <summary>最近一次真传讲席请假申请时辰。</summary>
    [Export] public long sect_true_disciple_lecture_last_leave_hour = -1;
    /// <summary>真传未在本期结束前开设讲席的累计缺席记档；作为师门履职声誉记录，不折算为百川区域声望。</summary>
    [Export] public int sect_true_disciple_lecture_missed_count;
    /// <summary>最近一次真传讲席缺席记档对应的期末时辰。</summary>
    [Export] public long sect_true_disciple_lecture_last_missed_hour = -1;
    /// <summary>玉虚真传当前师父与带教师兄/师姐的稳定世界人物 ID。</summary>
    [Export] public string sect_teacher_npc_id = "";
    [Export] public string sect_guide_npc_id = "";
    /// <summary>真传入门选定的师门研读秘籍；首卷完成后章节与技能仍走通用秘籍系统。</summary>
    [Export] public string sect_lineage_gongfa_path = "";
    /// <summary>本次师门借阅仅开放首卷时为 true；入门前已掌握的秘籍沿用原有卷章权限。</summary>
    [Export] public bool sect_lineage_first_volume_only_access;
    [Export] public bool sect_lineage_reported_first_volume;
    /// <summary>宗门职位与正式弟子身份分开；空值表示尚未登记职位。</summary>
    [Export] public string sect_office_title = "";
    /// <summary>旧存档兼容字段；仅作为逐势力态度摘要的旧文本，不代表百川区域声望。</summary>
    [Export] public string sect_reputation_summary = "";
    /// <summary>逐势力七阶态度记录；与百川区域声望及 NPC 个人关系分开。</summary>
    [Export] public Array<FactionAttitudeRecord> faction_attitude_records = [];
    /// <summary>主角对其他人物的稀疏单向关系记录；不含其他人物对主角的内在感受。</summary>
    [Export] public Dictionary directed_relationships = [];
    /// <summary>主角关系证据已归纳至的月份；与 NPC 月结算游标分开。</summary>
    [Export] public int relationship_last_processed_month = -1;
    /// <summary>各区域公开事迹流水；只参与对应地区的区域声望计算。</summary>
    [Export] public Array<RegionalRenownRecord> regional_renown_records = [];
    /// <summary>玩家在此世发生的可追溯经历事实；不由业力、声望或关系推导。</summary>
    [Export] public Array<PlayerCausalEventRecord> player_causal_event_records = [];
    /// <summary>单一有符号业力值；不等同于区域声望或可消费资源。</summary>
    [Export] public int karma_value;
    /// <summary>新心境轴值（-50～+50）；与旧战斗字段 current_mind 分开保存。</summary>
    [Export(PropertyHint.Range, "-50,50,1")] public int mood_value;
    /// <summary>可追溯宗门贡献流水；不因时间、身份或点击自动产生记录。</summary>
    [Export] public Array<YuxuSectContributionRecord> sect_contribution_records = [];
    /// <summary>已结算至的宗门月例月份序号；加入时初始化，旧存档首次接入不追溯补发。</summary>
    [Export] public int sect_allowance_month_cursor = -1;
    /// <summary>尚未领取的宗门月例灵石余额，不计利息。</summary>
    [Export] public int sect_unclaimed_spirit_stones;
    /// <summary>独立的家族客卿身份 ID；空值表示当前没有登记，不影响宗门身份。</summary>
    [Export] public string family_guest_affiliation_id = "";
    [Export] public string basic_deck_path = "res://resources/basic_decks/公共基础技能池.tres";
    [Export] public Array<string> basic_deck_upgrade_ids = [];
    /// <summary>角色已参悟的公共技能节点稳定 ID；旧存档默认为空。</summary>
    [Export] public Array<string> unlocked_skill_node_ids = [];
    /// <summary>即时战斗固定技能槽；凝核牌局仍使用独立的牌局数据。</summary>
    [Export] public Array<string> combat_skill_slot_paths = [];
    /// <summary>是否已经手动确认过即时战斗技能槽；用于区分默认装配与玩家主动清空。</summary>
    [Export] public bool combat_skill_slots_configured;
    [Export] public Array<string> combat_passive_paths = [];

    [Export] public float mana_regen_rate = 0.04f;
    [Export] public float base_mana_regen_rate = 0.04f;
    [Export] public float base_mind_regen_rate = 0.3f;
    [Export] public int lifespan_bonus;
    [Export] public int auxiliary_slots = 2;
    [Export] public float cultivation_tick_interval = 1.0f;
    [Export] public float external_lingqi_mult = 1.0f;
    [Export] public float ambient_lingqi_pressure = 100.0f;
    /// <summary>大世界遁速评分；无单位，基础值暂定为 100。</summary>
    [Export] public float dun_speed = 100.0f;
    /// <summary>旧版遁速字段；用于将旧存档的格/游戏日数值迁移到新评分。</summary>
    [Export] public float dun_speed_cells_per_day = 1.0f;
    /// <summary>更早版本的旧存档迁移字段。</summary>
    [Export] public float travel_speed_cells_per_day = 1.0f;
    /// <summary>是否能以飞行方式行进大世界；未获得飞行能力时为 false。</summary>
    [Export] public bool can_fly_world_map;
    /// <summary>大世界、地点与副本共用的持久精力；与即时战斗体力 current_stamina 分开保存。</summary>
    [Export] public float current_world_stamina = WorldStaminaRules.InitialCapacity;
    [Export] public float max_world_stamina = WorldStaminaRules.InitialCapacity;
    /// <summary>主角在大世界与地点活动时持续存在的状态；与即时战斗临时状态分开。</summary>
    [Export] public Godot.Collections.Array<WorldStatusRecord> world_status_records = [];
    /// <summary>服丹后持续积累，跨战斗保存；旧档缺字段默认为无丹毒。</summary>
    [Export] public double pill_toxicity;
    [Export] public double pill_toxicity_last_game_hour = -1;

    // 单人模式不再读取这些旧日程字段；保留它们仅用于兼容既有存档。
    [Export] public bool daily_schedule_enabled = false;
    [Export] public int daily_cultivation_hours = 6;
    [Export] public int daily_study_hours = 6;
    [Export] public int daily_rest_hours = 6;
    [Export] public int daily_labor_hours = 6;
    [Export] public int cultivation_priority = 25;
    [Export] public int study_priority = 25;
    [Export] public int rest_priority = 25;
    [Export] public int labor_priority = 25;
    // 旧档兼容字段。突破已改为玩家主动安排的闭关，不再由日程自动完成。
    [Export] public bool schedule_auto_breakthrough;
    [Export] public bool study_auto_next_chapter;
    [Export] public float labor_progress_hours;
    [Export] public string assigned_labor_site_id = "";

    [Export] public string residence_room_id = "";
    /// <summary>静室使用权与持续供给状态；静室是宅院设施，灵石仍从辖地账本结算。</summary>
    [Export] public int meditation_room_index = -1;
    [Export] public bool meditation_spirit_stone_supply;
    [Export] public float meditation_spirit_stone_debt;
    [Export] public string spouse_name = "";
    /// <summary>旧资源兼容字段，现行丹毒不按此值封顶。</summary>
    [Export] public float max_pill_toxicity = 100.0f;

    [Export] public float base_sword_proficiency = 5.0f;
    [Export] public float base_blade_proficiency = 5.0f;
    [Export] public float base_spear_proficiency = 5.0f;
    [Export] public float base_body_proficiency = 5.0f;
    [Export] public float base_magic_proficiency = 5.0f;
    [Export] public float sword_proficiency = 5.0f;
    [Export] public float blade_proficiency = 5.0f;
    [Export] public float spear_proficiency = 5.0f;
    [Export] public float body_proficiency = 5.0f;
    [Export] public float magic_proficiency = 5.0f;

    [Export] public float base_forging_proficiency = 5.0f;
    [Export] public float base_talisman_proficiency = 5.0f;
    [Export] public float base_formation_proficiency = 5.0f;
    [Export] public float base_alchemy_proficiency = 5.0f;
    [Export] public float forging_proficiency = 5.0f;
    [Export] public float talisman_proficiency = 5.0f;
    [Export] public float formation_proficiency = 5.0f;
    [Export] public float alchemy_proficiency = 5.0f;

    [Export] public float charisma = 50.0f;
    [Export] public int gender;
    [Export] public string personality = "";
    [Export] public string destiny_title = "";

    [Export] public int max_sub_slots = 2;
    [Export] public string main_gongfa_path = "";
    /// <summary>序章玄镜交予幼子的首卷；必须由玩家主动放入研读位。</summary>
    [Export] public string intro_gongfa_path = "";
    [Export] public float intro_gongfa_study_progress;
    /// <summary>未满一缕的修炼积累；与 spirit_strands 一同保存，避免日程结算丢失小数进度。</summary>
    [Export] public float cultivation_progress;
    // 归元息状态随完整人物资源保存；阶段0/1/2分别为周行、牵引、平衡。
    [Export] public int yuanxi_count;
    [Export] public bool yuanxi_active;
    [Export] public int yuanxi_target;
    [Export] public int yuanxi_phase;
    [Export] public double yuanxi_phase_hours;
    [Export] public int yuanxi_clock_hour;
    [Export] public string yuanxi_notice = "";
    /// <summary>旧五行参悟存档字段，仅供兼容读取；现行修命／修性系统不读取。</summary>
    [Export] public float qi_wood_insight;
    [Export] public float qi_fire_insight;
    [Export] public float qi_earth_insight;
    [Export] public float qi_metal_insight;
    [Export] public float qi_water_insight;
    /// <summary>旧五行参悟存档字段，仅供兼容读取；现行修命／修性系统不读取。</summary>
    [Export] public float qi_cultivation_hours;
    /// <summary>旧五行参悟存档字段，仅供兼容读取；现行修命／修性系统不读取。</summary>
    [Export] public int qi_cultivation_failed_rolls;
    /// <summary>旧五行参悟存档字段，仅供兼容读取；现行修命／修性系统不读取。</summary>
    [Export] public Godot.Collections.Array qi_inspirations = [];
    /// <summary>旧五行节点存档兼容字段；现行修命／修性系统不读取。</summary>
    [Export] public Dictionary qi_node_levels = [];
    /// <summary>六部位累计有效炼度；键使用 BodyRefinementRules 的稳定 ID。</summary>
    [Export] public Dictionary body_refinement_progress = [];
    /// <summary>已掌握的淬体图录路径；首期默认法门会在角色初始化时写入。</summary>
    [Export] public Array<string> learned_body_refinement_method_paths = [];
    /// <summary>正在进行的淬体行动，时长由对应淬体法决定。</summary>
    [Export] public string active_body_refinement_method_path = "";
    [Export] public string active_body_refinement_material_path = "";
    [Export] public int active_body_refinement_started_hour;
    [Export] public int active_body_refinement_finish_hour;
    [Export] public int active_body_refinement_duration_hours;
    [Export] public Dictionary last_body_refinement_result = [];
    /// <summary>修性三景：已纳入灵台的具体观景、各自独立的五十重进度，以及当前三景装配。</summary>
    [Export] public Array<string> learned_visualization_ids = [];
    [Export] public Dictionary visualization_progress = [];
    [Export] public Dictionary equipped_visualizations = [];
    [Export] public string visualization_focus_id = "";
    [Export] public Dictionary last_visualization_result = [];
    /// <summary>闭关不是一次点击结算：以下字段保存其目标、起止时刻与所占用的静室位。</summary>
    [Export] public bool is_in_breakthrough_retreat;
    [Export] public string breakthrough_target_realm = "";
    [Export] public int breakthrough_started_hour;
    [Export] public int breakthrough_finish_hour;
    [Export] public bool breakthrough_uses_meditation_room;
    [Export] public Array<string> sub_gongfa_paths = [];
    [Export] public Array<string> learned_gongfa_paths = [];
    [Export] public Dictionary learned_gongfa_chapters = [];
    /// <summary>已写入基础属性的卷章永久加成，避免读档或刷新时重复叠加。</summary>
    [Export] public Dictionary applied_gongfa_permanent_bonuses = [];
    [Export] public Dictionary gongfa_chapter_study_progress = [];
    [Export] public Dictionary equipped_gongfa_chapter_routes = [];
    [Export] public Array<string> loaded_skill_paths = [];
    /// <summary>首次手动调整战斗牌组后启用；未配置的旧存档保持原有“已解锁牌全部入组”行为。</summary>
    [Export] public bool battle_deck_configured;

    /// <summary>玩家唯一行囊。所有普通物品按定义路径和数量保存，材料、成品、秘籍与装备不再进入共享仓库。</summary>
    [Export] public Array<string> inventory_item_paths = [];
    [Export] public Array<BattleSupplySlotData> battle_supplies = [];
    [Export] public Array<int> inventory_item_counts = [];
    /// <summary>唯一法宝实例也归属于角色行囊，但保留独立实例数组，避免与普通物品堆叠。</summary>
    [Export] public Array<int> inventory_item_positions = [];
    [Export] public Array<ArtifactInstanceData> artifact_inventory = [];
    [Export] public Array<string> equipped_artifact_ids = [];
    [Export] public Array<int> artifact_inventory_positions = [];
    [Export] public Array<InventoryPlacementData> inventory_placements = [];

    [Export] public string weapon_path = "";
    // 武器已并入法宝体系；常规穿戴仅保留四个直观属性位。
    [Export] public string headwear_path = "";
    [Export] public string clothes_path = "";
    /// <summary>Cosmetic portrait clothing owned and currently worn; independent from stat-bearing clothes_path.</summary>
    [Export] public string portrait_clothing_item_path = "";
    [Export] public string ornament_1_path = "";
    [Export] public string footwear_path = "";
    [Export] public string ornament_2_path = ""; // 旧档兼容字段，后续迁移到饰品位。
    [Export] public int artifact_slot_count;
    [Export] public string vehicle_path = "";
    [Export] public float cold_tolerance = -15.0f;
    [Export] public float heat_tolerance = 40.0f;

    /// <summary>Append a traceable contribution record; this does not create rewards or passive gains.</summary>
    public bool record_yuxu_contribution(YuxuSectContributionRecord? record)
    {
        if (sect_affiliation_id != "yuxu_xianzong" || record is null ||
            string.IsNullOrWhiteSpace(record.record_id) || string.IsNullOrWhiteSpace(record.source_transaction_id) ||
            string.IsNullOrWhiteSpace(record.transaction_kind) ||
            string.IsNullOrWhiteSpace(record.source_title) || string.IsNullOrWhiteSpace(record.occurred_at) ||
            string.IsNullOrWhiteSpace(record.cost_summary) || string.IsNullOrWhiteSpace(record.result_summary) ||
            record.contribution_delta == 0)
            return false;

        sect_contribution_records ??= [];
        foreach (var existing in sect_contribution_records)
            if (existing is not null && (existing.record_id == record.record_id || existing.source_transaction_id == record.source_transaction_id))
                return false;
        sect_contribution_records.Add(record.Duplicate(true) as YuxuSectContributionRecord ?? record);
        return true;
    }

    public bool remove_yuxu_contribution(string recordId)
    {
        if (string.IsNullOrWhiteSpace(recordId) || sect_contribution_records is null) return false;
        for (var index = sect_contribution_records.Count - 1; index >= 0; index--)
        {
            if (sect_contribution_records[index] is not { } record || record.record_id != recordId) continue;
            sect_contribution_records.RemoveAt(index);
            return true;
        }
        return false;
    }

    public long get_yuxu_contribution_total()
    {
        long total = 0;
        foreach (var record in sect_contribution_records)
            if (record is not null)
                total += record.contribution_delta;
        return total;
    }

    /// <summary>Verified positive official-duty rewards, independent of spending and private transfers.</summary>
    public long get_yuxu_official_service_total()
    {
        long total = 0;
        foreach (var record in sect_contribution_records)
        {
            if (record is null) continue;
            var isLegacyOfficialDuty = string.IsNullOrWhiteSpace(record.transaction_kind) &&
                record.source_transaction_id.StartsWith("yuxu:official:", StringComparison.Ordinal);
            if (record.transaction_kind != YuxuSectContributionRecord.KindOfficialDutyReward && !isLegacyOfficialDuty)
                continue;
            total += Math.Max(0, record.contribution_delta);
        }
        return total;
    }

    public bool record_regional_renown(RegionalRenownRecord? record)
    {
        if (record is null || string.IsNullOrWhiteSpace(record.record_id) ||
            string.IsNullOrWhiteSpace(record.source_transaction_id) || string.IsNullOrWhiteSpace(record.region_id) ||
            string.IsNullOrWhiteSpace(record.source_title) || string.IsNullOrWhiteSpace(record.occurred_at) ||
            record.renown_delta == 0)
            return false;

        regional_renown_records ??= [];
        foreach (var existing in regional_renown_records)
            if (existing is not null && (existing.record_id == record.record_id || existing.source_transaction_id == record.source_transaction_id))
                return false;
        regional_renown_records.Add(record.Duplicate(true) as RegionalRenownRecord ?? record);
        return true;
    }

    public long get_regional_renown_total(string regionId)
    {
        long total = 0;
        foreach (var record in regional_renown_records)
            if (record is not null && record.region_id == regionId && record.is_public)
                total += record.renown_delta;
        return total;
    }

    public int get_faction_attitude_stage(string factionId)
    {
        foreach (var record in faction_attitude_records)
            if (record is not null && record.faction_id == factionId)
                return Math.Clamp(record.attitude_stage, 0, 6);
        return -1;
    }

    /// <summary>Append a deep-copied causal event once, keyed only by its stable event ID.</summary>
    public bool record_player_causal_event(PlayerCausalEventRecord? record)
    {
        if (record is null || string.IsNullOrWhiteSpace(record.event_id)) return false;
        player_causal_event_records ??= [];
        foreach (var existing in player_causal_event_records)
            if (existing is not null && string.Equals(existing.event_id, record.event_id, StringComparison.Ordinal))
                return false;
        player_causal_event_records.Add(record.Duplicate(true) as PlayerCausalEventRecord ?? record);
        return true;
    }

    /// <summary>Find a saved event by exact stable ID; returns null when absent.</summary>
    public PlayerCausalEventRecord? find_player_causal_event(string? eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId) || player_causal_event_records is null) return null;
        foreach (var record in player_causal_event_records)
            if (record is not null && string.Equals(record.event_id, eventId, StringComparison.Ordinal))
                return record;
        return null;
    }

    /// <summary>Evaluate recorded facts without consulting karma, renown, faction attitude, or relationships.</summary>
    public PlayerCausalHistoryAssessment assess_player_causal_history(
        IReadOnlyCollection<string>? verifiedExistingYuxuHardRuleIds = null)
        => PlayerCausalHistoryEvaluator.evaluate(player_causal_event_records, verifiedExistingYuxuHardRuleIds);

    /// <summary>Convenience API for the gate interaction to obtain factual, non-scoring judgment lines.</summary>
    public Array<string> get_yuxu_gate_judgment_lines(
        IReadOnlyCollection<string>? verifiedExistingYuxuHardRuleIds = null)
        => assess_player_causal_history(verifiedExistingYuxuHardRuleIds).get_yuxu_gate_judgment_lines();
}
