using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>One hover for an attribute name and value. All formulas are detailed sources.</summary>
public static class CharacterAttributePresentation
{
    private sealed record Field(string Key, string Effect);
    private static readonly Dictionary<string, Field> Fields = new()
    {
        ["PhysiqueRow"] = new("physique", "提高生命上限。\n影响行动速度。\n影响精准。\n影响身法。\n影响会心。\n影响护心。\n与神魂共同影响身心协调。\n影响以体魄计算的技能效果。"),
        ["IntelligenceRow"] = new("intelligence", "提高生命上限。\n影响行动速度。\n影响精准。\n影响身法。\n影响会心。\n影响护心。\n影响秘籍研读效率。\n影响以灵慧计算的技能效果。"),
        ["SoulRow"] = new("soul", "提高生命上限。\n影响行动速度。\n影响精准。\n影响身法。\n影响会心。\n影响护心。\n与体魄共同影响身心协调。\n影响秘籍研读效率。\n影响以神魂计算的技能效果。"),
        ["FortuneRow"] = new("fortune", "参与事件、探索与收获的福缘判定。"),
        ["ComprehensionRow"] = new("comprehension", "影响秘籍研读效率。\n参与相关事件的悟性判定。"),
        ["CharismaRow"] = new("charisma", "参与交涉、招揽与人物事件。"),
        ["SocialRow"] = new("social", "参与往来、交易与外务事件。"),
        ["ManagementRow"] = new("management", "参与事务调度与经营事件。"),
        ["HealthRow"] = new("max_health", "生命归零时无法继续行动。"),
        ["StaminaRow"] = new("max_stamina", "用于施展招式。\n每回合补给3点，未用余额保留，不超过上限。\n休整占一个行动位并回复2点体力。"),
        ["ManaRow"] = new("max_mana", "用于术法等行动。\n开战按有效容量初始化，不自动回复；只有明确效果可以恢复。"),
        ["MindRow"] = new("initiative_speed", "决定行动条的推进速度。\n行动进度达到1000时结算当前行动。"),
        ["SpiritPerceptionRow"] = new("max_spirit_perception", "持续御使器体时占用灵识。\n收回、损毁或驱散后释放占用。"),
        ["PhysicalDefenseRow"] = new("physical_defense", "减轻受到的物理伤害。"),
        ["PrecisionRow"] = new("precision", "影响参与命中判定的攻击与技艺；与目标身法对抗。"),
        ["DodgeRow"] = new("dodge", "影响属性闪避判定；与来袭者精准对抗。"),
        ["CriticalRow"] = new("critical", "影响会心判定。"),
        ["CritResistRow"] = new("crit_resist", "抵抗敌方会心。"),
        ["CritDamageRow"] = new("crit_damage_mult", "会心触发时使用的伤害倍率。"),
        ["StaminaRegenRow"] = new("physical_damage_reduction", "减轻受到的物理伤害。"),
        ["ManaRegenRow"] = new("dun_speed", "影响大世界移动与撤退，不影响战斗行动条。"),
        ["PhysicalReductionRow"] = new("physical_damage_reduction", "按比例减轻受到的物理伤害，与固定物理防御分别结算。"),
        ["HealingReceivedRow"] = new("healing_received_bonus", "提高实际受到的治疗；不代表自动恢复生命。"),
        ["TravelSpeedRow"] = new("dun_speed", "影响大世界行旅与撤退，不影响战斗行动条。"),
        ["LearningRow"] = new("learning", "影响秘籍研读的推进速度。"),
        ["CultivationRow"] = new("cultivation", "影响灵力积累速度。"),
        ["StrandsRow"] = new("spirit_strands", "修行积累，达到要求后可尝试突破。")
    };
    private static string N(float value) => Character.AttributeNumber(value);

    public static void Bind(Control anchor, Creature unit, string row, string name)
    {
        anchor.TooltipText = "";
        HoverInfoManager.SetInfo(anchor, Build(unit, row, name));
        anchor.SetMeta("attribute_hover_unit", unit); anchor.SetMeta("attribute_hover_row", row); anchor.SetMeta("attribute_hover_name", name);
        if (anchor.HasMeta("attribute_hover_bound")) return;
        anchor.SetMeta("attribute_hover_bound", true);
        anchor.MouseEntered += () =>
        {
            if (anchor.GetMeta("attribute_hover_unit").AsGodotObject() is Creature current && GodotObject.IsInstanceValid(current))
                HoverInfoManager.SetInfo(anchor, Build(current, anchor.GetMeta("attribute_hover_row").AsString(), anchor.GetMeta("attribute_hover_name").AsString()));
        };
    }
    public static HoverInfoManager.HoverInfoContent Build(Creature unit, string row, string name)
    {
        var human = unit as Character;
        if (row == "RealmRow") return Card(name, "当前境界：" + unit.cultivation_realm, "决定修行阶段与突破门槛。", []);
        if (row == "SpiritRootRow") return Card(name, "当前灵根：" + (human?.spirit_root_name ?? "未详"), "影响修行资质与功法适配。", []);
        if (row.StartsWith("RootWeight:"))
        {
            var index = int.Parse(row[11..]); var roots = (unit.entity_data as HumanData)?.root_weights;
            var weight = roots is not null && roots.Count > index ? roots[index].AsSingle() : 0;
            return Card(name, $"当前值：{N(weight)} 计算公式：灵根权重[{N(weight)}]＝{N(weight)}", "表示该五行在灵根中的权重。", []);
        }
        var field = Fields.GetValueOrDefault(row) ?? new Field(row, $"影响{name}相关技艺与手段的掌握。");
        string expression; float value; IReadOnlyList<string> sources = [];
        var calculation = human?.GetAttributeCalculation(field.Key);
        if (calculation is not null) { expression = calculation.Expression; value = calculation.Result; sources = calculation.Sources; }
        else if (field.Key == "learning" && human is not null)
        {
            value = human.get_learning_rate();
            expression = $"0.55＋悟性[{N(unit.comprehension)}]×0.030＋神魂[{N(unit.soul)}]×0.012＋灵慧[{N(unit.intelligence)}]×0.015";
        }
        else if (field.Key == "cultivation" && human is not null)
        {
            value = human.get_cultivation_rate();
            var compatible = human.spirit_root_type != 0 && human.main_gongfa is not null && human.main_gongfa.is_wuxing_compatible(human.spirit_root_type);
            if (!compatible) { expression = "当前修行条件未满足，修炼效率为0"; sources = ["需具备灵根并装配适配的主修功法。"]; }
            else
            {
                // This is the existing realm multiplier table, not realm attribute growth.
                var realm = human.get_realm_cultivation_multiplier();
                var root = human.get_element_efficiency(human.main_gongfa);
                var environment = human.get_cultivation_environment_factor();
                expression = $"0.005×境界系数[{N(realm)}]×灵根适配[{N(root)}]×环境系数[{N(environment)}]";
            }
        }
        else
        {
            var property = unit.entity_data!.GetPropertyList().Any(p => p["name"].AsString() == field.Key);
            if (!property && field.Key != "dun_speed") return Card(name, "当前值：未配置", field.Effect, []);
            value = property ? unit.entity_data.Get(field.Key).AsSingle() : 0;
            if (field.Key == "dun_speed" && human is not null) value = human.dun_speed;
            var baseKey = "base_" + field.Key;
            var hasBase = unit.entity_data.GetPropertyList().Any(p => p["name"].AsString() == baseKey);
            var basis = hasBase ? unit.entity_data.Get(baseKey).AsSingle() : value;
            // Direct stored values have no invented generation history. Personality
            // and permanent rewards already reside in the saved base value.
            expression = hasBase && Mathf.IsEqualApprox(value, basis) ? $"基础值[{N(basis)}]" : $"当前记录值[{N(value)}]";
        }
        var formula = expression + $"＝{N(value)}";
        var heading = $"当前值：{N(value)} 计算公式：{formula}";
        if (field.Key is "physical_damage_reduction" or "healing_received_bonus") heading = $"当前值：{N(value * 100)}% 计算公式：({expression})×100%＝{N(value * 100)}%";
        if (field.Key == "crit_damage_mult") heading = $"当前值：{N(value)}倍 计算公式：{formula}\n实际会心结算还读取双方属性。";
        if (field.Key is "learning" or "cultivation") heading = $"当前值：{N(value)}/小时 计算公式：{formula}";
        if (field.Key is "max_health" or "max_stamina" or "max_mana" or "max_spirit_perception")
        {
            var current = field.Key switch { "max_health" => unit.current_health, "max_stamina" => unit.current_stamina, "max_mana" => unit.current_mana, _ => unit.used_spirit_perception };
            heading = $"{(field.Key == "max_spirit_perception" ? "当前占用" : "当前值")}：{current}/{N(value)}\n上限：{N(value)} 计算公式：{formula}";
        }
        return Card(name, heading, field.Effect, sources, Icon(row));
    }
    private static Texture2D? Icon(string row)
    {
        var index = Array.IndexOf(new[] { "PhysiqueRow", "IntelligenceRow", "SoulRow", "FortuneRow", "ComprehensionRow" }, row);
        return index < 0 ? null : new AtlasTexture { Atlas = GD.Load<Texture2D>("res://textures/ui/infopanel_attribute_icons_ink_v2.png"), Region = new Rect2(index % 3 * 512, index / 3 * 512, 512, 512) };
    }
    private static HoverInfoManager.HoverInfoContent Card(string name, string heading, string effect, IReadOnlyList<string> sources, Texture2D? icon = null)
        => HoverInfoManager.Structured(name, icon, "", new("", heading), new("", "作用：\n" + effect), new("", sources.Count == 0 ? "加成明细：无" : "加成明细：\n" + string.Join("\n", sources)))
            with { CardWidth = icon is null ? 420f : 340f, IconSize = 96f, ShowRules = false };
}
