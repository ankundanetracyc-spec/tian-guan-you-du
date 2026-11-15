using Godot;
using Godot.Collections;
using System;
using System.Linq;

/// <summary>Action-only test snapshots and derived physical segments; never edits world learning or queue resources.</summary>
public partial class ActionManualBattleRuntime : ActionHumanSparringRuntime
{
    private readonly System.Collections.Generic.Dictionary<int, ActiveSkillData> _segments = [];
    private readonly float[] _testBearingResistance = new float[4];
    private ActiveSkillData? _segmentSource;

    public override void _Ready()
    {
        DemoOpponentCount = 3; // The setup screen may select up to three independent snapshots.
        base._Ready();
        var setup = GetParent().Get("manual_battle_setup").AsGodotDictionary();
        configure_manual_test(setup);
    }

    public void configure_manual_test(Dictionary setup)
    {
        if (setup.Count == 0) return;
        var playerOptions = setup["player"].AsGodotDictionary();
        var enemyOptions = setup["enemy"].AsGodotDictionary();
        var count = Math.Clamp(setup.GetValueOrDefault("enemy_count", 1).AsInt32(), 1, 3);
        // Retain the originally generated NPC identities for a same-group restart.
        var source = InitialUnits.Select(unit => (BattleEntityData)unit.Duplicate(true)).ToList();
        if (source.Count < count + 1) throw new ArgumentException("陪练快照不足，请重新进入配置页。");
        void Apply(BattleEntityData unit, Dictionary options, int index)
        {
            unit.cultivation_realm = options.GetValueOrDefault("realm", unit.cultivation_realm).AsString();
            if (options.GetValueOrDefault("custom_attributes", false).AsBool())
            {
                unit.physique = Math.Max(0, options["physique"].AsSingle());
                unit.soul = Math.Max(0, options["soul"].AsSingle());
                unit.intelligence = Math.Max(0, options["intelligence"].AsSingle());
                unit.max_health = Math.Max(1, options["health"].AsInt32());
                unit.max_mana = Math.Max(0, options["mana"].AsInt32());
                var coordination = Math.Min(unit.physique, unit.soul);
                unit.precision = Mathf.RoundToInt(unit.intelligence * 7 + unit.soul + unit.physique * .5f);
                unit.dodge = Mathf.RoundToInt(coordination * 4 + unit.soul * 2 + unit.intelligence + unit.physique * .5f);
                unit.critical = Mathf.RoundToInt(unit.physique * .5f + unit.soul * 2 + unit.intelligence * .3f);
                unit.crit_resist = Mathf.RoundToInt(unit.physique * 3 + unit.intelligence * 2 + coordination * 2 + unit.soul * 1.5f);
            }
            unit.current_health = unit.max_health;
            _testBearingResistance[index] = options.GetValueOrDefault("custom_attributes", false).AsBool()
                ? Math.Max(0, options.GetValueOrDefault("bearing_resistance_pp", 0f).AsSingle()) : 0f;
        }
        Apply(source[0], playerOptions, 0);
        var enemies = new Array<BattleEntityData>();
        for (var index = 1; index <= count; index++)
        {
            Apply(source[index], enemyOptions, index);
            enemies.Add(source[index]);
        }
        ConfigureUnits(source[0], enemies);
    }

    public void restore_unit_mana(int index) => Units[index].current_mana = Units[index].max_mana;
    public bool detailed_effects() => PlayerInfoText.Mode == PlayerInfoText.InformationMode.Detailed;

    public int heal_blood_fall(int index, int layers)
    {
        var unit = Units[index];
        if (unit.current_health <= 0 || layers <= 0) return 0;
        var bonus = 0f;
        foreach (Variant source in unit.combat_tempering_by_gongfa.Values)
            if (source.VariantType == Variant.Type.Dictionary)
                bonus += source.AsGodotDictionary().GetValueOrDefault("fall_heal_rate_pp", 0f).AsSingle();
        var requested = Math.Max(0, Mathf.RoundToInt(unit.max_health * layers * Math.Max(0, 5f + bonus) / 100f));
        var amount = requested <= 0 ? 0 : Math.Max(1, Mathf.RoundToInt(requested * (1f + Math.Max(0, unit.healing_received_bonus))));
        var healed = Math.Min(amount, unit.max_health - unit.current_health);
        unit.current_health += healed;
        return healed;
    }

    public Dictionary get_control_profile(int index)
    {
        var unit = Units[index];
        var resistance = _testBearingResistance[index];
        if (unit.portrait_data is HumanData human)
        {
            var progress = human.body_refinement_progress.GetValueOrDefault(BodyRefinementRules.Bone, 0f).AsSingle();
            // The current plan grants one percentage point at the bone's fifth node.
            if (BodyRefinementRules.GetLevel(BodyRefinementRules.Bone, progress) >= 5) resistance += 1f;
        }
        return new() { ["physique"] = unit.physique, ["realm_tier"] = MajorRealm(unit.cultivation_realm),
            ["bearing_resistance_pp"] = resistance };
    }

    private static int MajorRealm(string realm)
    {
        if (realm.StartsWith("神胎", StringComparison.Ordinal)) return 4;
        if (realm.StartsWith("金丹", StringComparison.Ordinal)) return 3;
        if (realm.StartsWith("玄基", StringComparison.Ordinal)) return 2;
        if (realm.StartsWith("云息", StringComparison.Ordinal)) return 1;
        return 0;
    }

    public Dictionary resolve_manual_contact(int attackerIndex, int defenderIndex, int baseDamage, int block)
    {
        if (attackerIndex < 0 || attackerIndex >= Units.Count || defenderIndex < 0 || defenderIndex >= Units.Count ||
            attackerIndex == defenderIndex || attackerIndex != 0 && defenderIndex != 0)
            throw new ArgumentException("非法即时对练目标。");
        var attacker = Units[attackerIndex];
        var defender = Units[defenderIndex];
        if (attacker.current_health <= 0 || defender.current_health <= 0) return new() { ["inactive"] = true };
        if (baseDamage <= 0) return new() { ["inactive"] = true };
        if (!_segments.TryGetValue(baseDamage, out var skill))
        {
            _segmentSource ??= GD.Load<ActiveSkillData>("res://resources/activeskills/应敌.tres");
            skill = (ActiveSkillData)_segmentSource.Duplicate(true);
            skill.base_damage = baseDamage;
            skill.damage_formula = "";
            skill.damage_bonuses = [];
            _segments[baseDamage] = skill;
        }
        var hit = BattleActionResolver.resolve_hit(attacker, defender, skill, Math.Max(0, block));
        defender.current_health = Math.Max(0, defender.current_health - hit.DamageBeforeShield);
        return new() { ["inactive"] = false, ["evaded"] = hit.Evaded, ["critical"] = hit.Critical,
            ["damage"] = hit.DamageBeforeShield, ["blocked"] = hit.BlockedDamage,
            ["guarded"] = block > 0 && !hit.Evaded };
    }
}
