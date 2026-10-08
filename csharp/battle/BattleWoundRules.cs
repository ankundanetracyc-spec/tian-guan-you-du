using Godot;
using Godot.Collections;

/// <summary>业障伤牌的识别与数值惩罚规则。</summary>
public partial class BattleWoundRules : RefCounted
{
    private const string ExternalWoundName = "外伤";
    private const string InternalWoundName = "内伤";
    private const string ExternalWoundPath = "res://resources/activeskills/业障/外伤.tres";
    private const string InternalWoundPath = "res://resources/activeskills/业障/内伤.tres";
    private const float ActionSpeedReductionPerCard = .05f, ActionSpeedReductionMax = .30f;
    private const float ExternalMoveReductionPerCard = .10f, ExternalEnergyCostIncreasePerCard = .10f, ExternalDodgeReductionPerCard = .08f;
    private const float InternalManaCostIncreasePerCard = .10f, InternalCritResistReductionPerCard = .08f;
    private const int EffectiveCardCap = 4;

    public static bool is_external_wound(GodotObject? skill) => is_wound(skill, ExternalWoundName);
    public static bool is_internal_wound(GodotObject? skill) => is_wound(skill, InternalWoundName);
    public static string get_wound_path(string wound_name) => wound_name == ExternalWoundName ? ExternalWoundPath : wound_name == InternalWoundName ? InternalWoundPath : "";

    public static Dictionary count_wounds_in_skills(Godot.Collections.Array skills)
    {
        var counts = new Dictionary { ["external"] = 0, ["internal"] = 0 };
        foreach (var value in skills)
        {
            var skill = value.AsGodotObject();
            if (is_external_wound(skill)) counts["external"] = counts["external"].AsInt32() + 1;
            else if (is_internal_wound(skill)) counts["internal"] = counts["internal"].AsInt32() + 1;
        }
        return counts;
    }

    public static int get_total_wounds(Dictionary counts) => count(counts, "external") + count(counts, "internal");
    public static float get_action_speed_multiplier(Dictionary counts) => Mathf.Max(0, 1 - Mathf.Min(get_total_wounds(counts) * ActionSpeedReductionPerCard, ActionSpeedReductionMax));
    public static float get_move_distance_multiplier(Dictionary counts) => Mathf.Max(0, 1 - capped(counts, "external") * ExternalMoveReductionPerCard);
    public static float get_dodge_multiplier(Dictionary counts) => Mathf.Max(0, 1 - capped(counts, "external") * ExternalDodgeReductionPerCard);
    public static float get_crit_resist_multiplier(Dictionary counts) => Mathf.Max(0, 1 - capped(counts, "internal") * InternalCritResistReductionPerCard);
    public static float get_energy_cost_multiplier(Dictionary counts, GodotObject? skill) => is_skill_type(skill, 1) ? 1 + capped(counts, "external") * ExternalEnergyCostIncreasePerCard : 1;
    public static float get_mana_cost_multiplier(Dictionary counts, GodotObject? skill) => GodotObject.IsInstanceValid(skill) && skill!.Get("mana_cost").AsInt32() > 0 ? 1 + capped(counts, "internal") * InternalManaCostIncreasePerCard : 1;

    private static bool is_wound(GodotObject? skill, string woundName) => is_skill_type(skill, 7) && skill!.Get("skill_name").AsString() == woundName;
    private static bool is_skill_type(GodotObject? skill, int type) => GodotObject.IsInstanceValid(skill) && skill!.Get("skill_type").AsInt32() == type;
    private static int count(Dictionary counts, string key) => counts.TryGetValue(key, out var value) ? value.AsInt32() : 0;
    private static int capped(Dictionary counts, string key) => Mathf.Min(count(counts, key), EffectiveCardCap);
}
