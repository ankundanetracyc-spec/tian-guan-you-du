using Godot;
using Godot.Collections;

/// <summary>Serializable skill definition shared by即时战斗技能槽、世界技能与凝核牌局。</summary>
public partial class ActiveSkillData : Resource
{
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
    /// <summary>局内精力费用，范围为 0 至 3。</summary>
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
    [ExportGroup("即时战斗时序")]
    /// <summary>准备阶段基础时长；采用四倍后的清晰档位，时长改变后允许出现非整档实际值。</summary>
    [Export(PropertyHint.Range, "48,1440,1")] public int preparation_frames = 240;
    /// <summary>结算后的收招基础时长。</summary>
    [Export(PropertyHint.Range, "0,720,1")] public int recovery_frames = 96;
    /// <summary>技能完成结算后的再次可用间隔；0 表示收招完成后即可再次排队。</summary>
    [Export(PropertyHint.Range, "0,3600,1")] public int cooldown_frames;
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
    public string get_cost_description() => skill_type == SkillType.POWER ? "" : $"精力: {energy_cost}，法力: {mana_cost}" + (spirit_occupy > 0 ? $"，灵识占用: {spirit_occupy}" : "");

    /// <summary>所有卡牌共用的牌面短描述：只写目标、数值与关键机制，不使用叙事性长句。</summary>
    public string get_brief_description(GodotObject? owner = null)
    {
        var lines = new List<string>();
        if (!Mathf.IsZeroApprox(base_damage) || damage_bonuses.Count > 0)
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
        if (keywords.Contains("应对")) lines.Insert(0, "应对：队首遇到匹配来袭时立即触发；未触发则正常结算基础效果。");
        return lines.Count == 0 ? (string.IsNullOrWhiteSpace(description)?"施展本牌效果。":description) : string.Join("\n", lines);
    }
    public string get_skill_description(GodotObject? owner = null) => get_brief_description(owner) + get_counter_effect_description(owner);

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
            "shield" => $"获得 {baseValue:0.##} 点护持。",
            "restore_stamina" => $"恢复 {baseValue:0.##} 点精力。",
            "gain_energy" => $"获得 {count} 点精力。",
            "restore_mana" => $"恢复 {baseValue:0.##} 点法力。",
            "draw" => $"抽取 {count} 张卡牌。",
            "counter_block" => $"获得 {CardRules.Block(this, owner, count)} 层化解。",
            "gain_punch_frame" => $"获得 {count} 层拳架。",
            "gain_qi_blood" => $"获得 {count} 层气血。",
            "gain_sword_intent" => $"获得 {count} 层剑意。",
            "apply_sword_mark" => $"对目标施加 {count} 层剑痕。",
            "apply_sword_mark_if_absent" => $"目标无剑痕时施加 {count} 层剑痕。",
            "apply_sword_mark_to_attacker" => $"对攻击者施加 {count} 层剑痕。",
            "consume_marks_add_wound" => $"消耗剑痕并加入外伤。",
            "bonus_damage_if_target_marked" or "bonus_damage_if_pre_mark" => "目标带有剑痕时提高伤害。",
            "critical_if_target_marked" => "目标带有剑痕时提高会心。",
            "precision_if_target_marked" => "目标带有剑痕时提高精准。",
            "precision_if_guarding" => "自身有护持、化解或借力时提高精准。",
            "enter_sword_stance" => "进入折枝剑势。",
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

    private string damage_name() => damage_type switch { "magic" or "spiritual" => "法术", "soul" => "神魂", _ => "物理" };
}
// Segment counter extras.
