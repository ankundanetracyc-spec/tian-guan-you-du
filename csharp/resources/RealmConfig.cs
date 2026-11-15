using Godot;
using Godot.Collections;

/// <summary>境界突破门槛与属性成长配置。每层记录成长倍率增量、定额加成；界面按当前境界累计展示。</summary>
public partial class RealmConfig : Resource
{
    private static readonly HashSet<string> RequiredAttributeKeys =
    [
        "current_age", "gender", "physique", "intelligence", "soul", "fortune", "comprehension", "charisma", "spirit_strands", "core_count", "max_health", "max_mana", "max_stamina", "max_mind", "physical_defense", "precision", "dodge", "crit_resist", "initiative_speed", "cards_per_turn", "initial_hand_size", "sub_slot_increase", "health_regen_rate", "stamina_regen_rate", "mind_regen_rate", "learning_rate", "cultivation_rate", "critical", "crit_damage_mult", "sword_proficiency", "blade_proficiency", "spear_proficiency", "body_proficiency", "magic_proficiency", "forging_proficiency", "talisman_proficiency", "formation_proficiency", "alchemy_proficiency", "mana_regen_rate", "max_spirit_perception", "max_age"
    ];

    internal static readonly HashSet<string> MultiplierKeys =
    [
        "max_age", "physique", "intelligence", "soul", "fortune", "comprehension", "charisma", "spirit_strands", "core_count", "max_health", "max_mana", "max_stamina", "max_mind", "learning_rate", "cultivation_rate", "critical", "crit_damage_mult", "physical_defense", "precision", "dodge", "crit_resist", "sword_proficiency", "blade_proficiency", "spear_proficiency", "body_proficiency", "magic_proficiency", "forging_proficiency", "talisman_proficiency", "formation_proficiency", "alchemy_proficiency", "mana_regen_rate", "initiative_speed", "cards_per_turn", "sub_slot_increase", "initial_hand_size", "health_regen_rate", "stamina_regen_rate", "mind_regen_rate", "max_spirit_perception", "gender", "current_age"
    ];

    internal static readonly HashSet<string> FixedBonusKeys = new(MultiplierKeys);

    [Export] public string realm_name = "";
    [Export] public bool is_mortal;
    [Export] public bool requires_main_gongfa = true;
    private Dictionary _required_attributes = [];
    private Dictionary _realm_multiplier_bonuses = [];
    private Dictionary _realm_fixed_bonuses = [];

    [Export]
    public Dictionary required_attributes
    {
        get => _required_attributes;
        set => _required_attributes = normalize(value, RequiredAttributeKeys, _required_attributes);
    }

    [Export] public Dictionary battle_base_attributes = [];

    [Export]
    public Dictionary realm_multiplier_bonuses
    {
        get => _realm_multiplier_bonuses;
        set => _realm_multiplier_bonuses = normalize(value, MultiplierKeys, _realm_multiplier_bonuses);
    }

    [Export]
    public Dictionary realm_fixed_bonuses
    {
        get => _realm_fixed_bonuses;
        set => _realm_fixed_bonuses = normalize(value, FixedBonusKeys, _realm_fixed_bonuses);
    }

    public void sync_defaults()
    {
        required_attributes = required_attributes;
        realm_multiplier_bonuses = realm_multiplier_bonuses;
        realm_fixed_bonuses = realm_fixed_bonuses;
    }

    internal static Dictionary normalize(Dictionary input, HashSet<string> allowedKeys, Dictionary current)
    {
        if (input.Count == 0) return current;
        var result = new Dictionary();
        foreach (var key in input.Keys)
        {
            var attribute = key.AsString();
            var value = input[key];
            if (allowedKeys.Contains(attribute) && !is_default(value)) result[attribute] = value;
        }
        return result;
    }

    private static bool is_default(Variant value) => value.VariantType switch
    {
        Variant.Type.Int => value.AsInt64() == 0,
        Variant.Type.Float => Mathf.IsZeroApprox(value.AsSingle()),
        _ => false,
    };
}
