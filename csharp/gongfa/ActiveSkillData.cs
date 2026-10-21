using Godot;
using Godot.Collections;

/// <summary>Serializable skill definition shared by即时战斗技能槽、世界技能与凝核牌局。</summary>
public partial class ActiveSkillData : Resource
{
    // 来源身份与法器运行时分支；不伪造 ResourcePath，不写入永久技能列表。
    [Export] public string combat_source_key = "";
    [Export] public string artifact_instance_id = "";
    [Export] public ActiveSkillData? artifact_summon_skill;
    [Export] public bool artifact_is_summon;
    [Export] public bool combat_empty_slot;
    [Export] public float physical_defense_ignore_ratio;
    [Export] public int artifact_block_bonus;
    public enum SkillType { ATTACK, MOVEMENT, DRAW, BUFF, WAIT, SPECIAL, POWER, CURSE }
    public enum CardConditionType { NONE, MAIN_GONGFA, SUB_GONGFA, ANY_GONGFA }

    [ExportGroup("基础信息")]
    [Export] public string skill_name = "";
    [Export] public SkillType skill_type = SkillType.ATTACK;
    [Export] public string icon_path = "";
    [Export(PropertyHint.MultilineText)] public string description = "";
    [Export] public int element;
    [Export] public string source_gongfa_name = "";
    [Export] public Dictionary required_attributes = [];
    [Export] public string required_realm = "";
    [ExportGroup("消耗条件")]
    /// <summary>局内体力费用，范围为 0 至 3。</summary>
    [Export(PropertyHint.Range, "0,3,1")] public int energy_cost;
    [Export] public int mana_cost;
    /// <summary>仅祭出持续实体时使用；普通术法与神魂牌不得支付灵识。</summary>
    [Export(PropertyHint.Range, "0,3,1")] public int spirit_occupy;
    [Export] public int required_equip_slot = -1;
    [Export] public int required_weapon_type;
    [Export] public int condition_type;
    [Export] public string condition_value = "";
    [ExportGroup("目标范围")]
    [Export] public int target_type;
    [Export] public int range_min = 1;
    [Export] public int range_max = 3;
    [Export] public int aoe_range;
    [Export] public bool can_target_empty;
    [Export] public int move_distance;
    /// <summary>显式出手方式分类：melee 为近身，ranged 为远程；只用于招式联动，不代表战斗距离或射程。</summary>
    [Export] public string attack_style = "";
    /// <summary>运行时派生的补充伤害段，不重复触发原技能的附加效果。</summary>
    [Export] public bool combat_is_gongfa_bonus_damage_segment;
    /// <summary>运行时派生的血臂追击段；主修增幅只作用于该段。</summary>
    [Export] public bool combat_is_blood_arm_segment;
    /// <summary>运行时派生的血臂演出段；化法复制段也可由血臂执行演出。</summary>
    [Export] public bool combat_is_blood_arm_visual_segment;
    [ExportGroup("即时战斗时序")]
    /// <summary>旧战斗流程兼容字段；即时队列战斗不使用准备时长。</summary>
    [Export(PropertyHint.Range, "0,1440,1")] public int preparation_frames;
    /// <summary>旧战斗流程兼容字段；即时队列战斗不使用收招时长。</summary>
    [Export(PropertyHint.Range, "0,720,1")] public int recovery_frames;
    /// <summary>旧战斗流程兼容字段；即时队列战斗以每轮或每场次数限制招式。</summary>
    [Export(PropertyHint.Range, "0,3600,1")] public int cooldown_frames;
    /// <summary>成功使用后跳过的完整回合数；0表示不设冷却。使用于R回合时，最早R+N+1回合恢复。</summary>
    [Export(PropertyHint.Range, "0,99,1")] public int cooldown_rounds;
    /// <summary>有限次数技能的最大使用次数；0 表示不限制次数。</summary>
    [Export(PropertyHint.Range, "0,99,1")] public int max_charges;
    /// <summary>每个规划段的使用次数上限；0 表示不限制。旧 max_charges 继续作为每场上限兼容字段。</summary>
    [Export(PropertyHint.Range, "0,99,1")] public int uses_per_round;
    /// <summary>每场战斗的使用次数上限；0 表示不限制。未配置时沿用旧 max_charges。</summary>
    [Export(PropertyHint.Range, "0,99,1")] public int uses_per_battle;
    /// <summary>技能槽布局提示：active、response、utility 或 passive。</summary>
    [Export] public string slot_category = "active";
    [ExportGroup("构筑关键词")]
    /// <summary>用于牌面展示和构筑检索的关键词；不参与战斗规则判定。</summary>
    [Export] public Array<string> tags = [];
    [ExportGroup("机制标记")]
    /// <summary>运行时规则标记；不作为构筑关键词显示。</summary>
    [Export] public Array<string> keywords = [];
    /// <summary>由已研读化法附加到本招式上的运行时效果 ID，不作为玩家可见关键词。</summary>
    [Export] public Array<string> gongfa_rewrite_effect_ids = [];
    [Export] public string momentum_condition_type = "";
    [Export] public string momentum_effect_params = "";
    [Export] public string flow_condition_type = "";
    [Export] public string counter_condition = "";
    [Export] public Godot.Collections.Array flow_condition_params = [];
    [ExportGroup("伤害公式")]
    [Export] public float base_damage;
    [Export] public string damage_formula = "";
    [Export] public Array<DamageBonus> damage_bonuses = [];
    [Export] public string damage_type = "physical";
    [Export(PropertyHint.Range, "0,3,0.05")] public float soul_confrontation_weight = 1.0f;
    [Export(PropertyHint.Range, "0,100,1")] public float soul_confrontation_buffer = 20.0f;
    [Export(PropertyHint.Range, "0,2,0.05")] public float soul_confrontation_min = 0.5f;
    [Export(PropertyHint.Range, "1,3,0.05")] public float soul_confrontation_max = 1.5f;
    [ExportGroup("附加效果")]
    [Export] public Array<Dictionary> special_effects = [];
    [Export] public Array<Dictionary> status_effects = [];
    [Export] public string stacks_formula = "";
    [Export] public string move_formula = "";

    public float calculate_damage(GodotObject? character)
    {
        if (!string.IsNullOrWhiteSpace(damage_formula)) return CardRules.Evaluate(damage_formula, character);
        var total = base_damage;
        if (!GodotObject.IsInstanceValid(character)) return total;
        foreach (var bonus in damage_bonuses)
        {
            if (bonus is null || string.IsNullOrEmpty(bonus.attribute) || Mathf.IsZeroApprox(bonus.multiplier)) continue;
            total += CardRules.Number(character, bonus.attribute) * bonus.multiplier;
        }
        return total;
    }

    public int get_energy_cost() => energy_cost;
    public string get_cost_description() => skill_type == SkillType.POWER ? "" : $"体力: {energy_cost}，法力: {mana_cost}";

    /// <summary>牌面展示关键词；应对机制同时作为可检索标签显示。</summary>
    public Array<string> get_presentation_tags()
    {
        var result = new Array<string>();
        foreach (var tag in tags)
            if (!string.IsNullOrWhiteSpace(tag) && !result.Contains(tag)) result.Add(tag);
        if (keywords.Contains("应对") && !result.Contains("应对")) result.Add("应对");
        return result;
    }

    /// <summary>所有卡牌共用的牌面短描述：只写目标、数值与关键机制，不使用叙事性长句。</summary>
    public string get_brief_description(GodotObject? owner = null)
    {
        var lines = new List<string>();
        if (!Mathf.IsZeroApprox(base_damage) || damage_bonuses.Count > 0 || !string.IsNullOrWhiteSpace(damage_formula))
            lines.Add($"对一名指定敌人造成 {Mathf.RoundToInt(BattleActionResolver.preview_damage(owner,this))} 点{damage_name()}伤害。");
        foreach (var effect in special_effects)
        {
            var text = brief_effect(effect, owner);
            if (!string.IsNullOrEmpty(text) && !lines.Contains(text)) lines.Add(text);
        }
        foreach (var effect in status_effects)
        {
            var status = effect.GetValueOrDefault("status_id", "").AsString();
            var count = effect.GetValueOrDefault("stacks", 1).AsInt32();
            if(status=="化解")count=CardRules.Block(this,owner,count);
            if (!string.IsNullOrEmpty(status)) lines.Add($"获得 {count} 层{status}。");
        }
        if (keywords.Contains("应对")) lines.Insert(0, "应对触发：当前槽遇到匹配来袭时立即触发。" + (CardRules.NormalCast(this) is null ? "未触发则空放，不消耗费用。" : "未触发则结算独立的普通打出效果与费用。"));
        return lines.Count == 0 ? (string.IsNullOrWhiteSpace(description)?"施展本技能效果。":description) : string.Join("\n", lines);
    }
    public string get_skill_description(GodotObject? owner = null) => CardPresentation.Face(this, owner);

    public string get_card_category() => skill_type switch
    {
        SkillType.ATTACK => "攻伐", SkillType.CURSE => "业障", SkillType.POWER => "心法", SkillType.BUFF => "守御",
        _ when keywords.Contains("固有") => "心法", _ when keywords.Contains("应对") => "守御", _ => "术诀"
    };
    public string get_skill_category() => get_card_category();

    private string brief_effect(Dictionary effect, GodotObject? owner)
    {
        var id = effect.GetValueOrDefault("effect", "").AsString();
        var count = Mathf.Max(1, effect.GetValueOrDefault("count", 1).AsInt32());
        var baseValue = CardRules.Evaluate(effect.GetValueOrDefault("formula", "").AsString(), owner);
        return id switch
        {
            "shield" => $"获得 {baseValue:0.##} 点护盾。",
            "restore_stamina" => $"恢复 {baseValue:0.##} 点体力。",
            "gain_energy" => $"获得 {count} 点体力。",
            "restore_mana" => $"恢复 {baseValue:0.##} 点法力。",
            "draw" => $"抽取 {count} 张卡牌。",
            "counter_block" => $"获得 {CardRules.Block(this, owner, count)} 层化解。",
            "gain_punch_frame" => $"获得 {count} 层拳架。",
            "gain_qi_blood" => $"获得 {count} 层气血。",
            "gain_qi" => $"获得炁 {count}。",
            "disperse_qi_blood" => $"打散目标 {count} 层气血。",
            "assign_nian_to_next_skill" => $"为行动队列中下一项不同的主动技能实例赋念 {count} 层；若没有合宜后续招式，本招不结算。",
            "precision_if_guarding" => "自身有护持、化解或借力时提高精准。",
            "spend_qi_blood" => "消耗 1 层气血。",
            "evasion" => "本次受击提高闪避。",
            _ => description
        };
    }

    private static float formula_base(string formula)
    {
        foreach (var piece in formula.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = piece.Split(':', 2);
            if (pair.Length == 2 && pair[0] == "base" && float.TryParse(pair[1], out var value)) return value;
        }
        return 0;
    }

    private string damage_name() => damage_type switch { "magic" => "法术", "soul" or "spiritual" => "神魂", _ => "物理" };
}
// Segment counter extras.
