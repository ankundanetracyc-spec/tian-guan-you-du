using Godot;
using Godot.Collections;
using System;
using System.Linq;

/// <summary>Four isolated human snapshots; spatial contact, payment and clocks belong to each fighter.</summary>
public partial class ActionHumanSparringRuntime : Node
{
    [Export] public int DemoOpponentCount { get; set; } = 1;
    protected System.Collections.Generic.List<BattleEntityData> InitialUnits = [], Units = [];
    private readonly int[] _bloodCapacity = new int[4];
    private ActiveSkillData? _fistSkill, _heavySkill;
    private readonly System.Collections.Generic.Dictionary<int, ActiveSkillData> _segmentSkills = [];

    public override void _Ready()
    {
        if (InitialUnits.Count == 0) ConfigureDemo();
        GetParent().Set("attribute_runtime", this);
    }

    public void Configure(BattleEntityData player, Array<BattleEntityData> opponents)
    {
        if (opponents.Count is < 1 or > 3) throw new ArgumentException("人类陪练支持一至三名NPC。", nameof(opponents));
        ConfigureUnits(player, opponents);
    }

    protected void ConfigureUnits(BattleEntityData player, Array<BattleEntityData> opponents)
    {
        if (opponents.Count is < 1 or > 3) throw new ArgumentException("即时对练支持一至三名NPC。", nameof(opponents));
        var all = new Array<BattleEntityData> { player };
        foreach (var opponent in opponents)
        {
            _ = new ActionBattleSnapshots(player, opponent);
            all.Add(opponent);
        }
        if (all.Select(unit => unit.uid).Distinct().Count() != all.Count)
            throw new ArgumentException("参战人物须有不同身份。");
        InitialUnits = all.Select(Copy).ToList();
        reset_battle();
    }

    private void ConfigureDemo()
    {
        BattleEntityData Snapshot(HumanData data, int index)
        {
            var body = new Character { Visible = false, entity_data = data, entity_name = data.entity_name };
            AddChild(body);
            try
            {
                var snapshot = body.get_snapshot_data();
                snapshot.uid = "human-sparring:" + index + ":" + Guid.NewGuid().ToString("N");
                snapshot.current_health = snapshot.max_health;
                return snapshot;
            }
            finally { body.Free(); }
        }

        // Preserve the existing standalone player's explicit sample values.
        var player = Snapshot(new HumanData { entity_name = "演练者", cultivation_realm = "凡人",
            base_physique = 8, base_intelligence = 8, base_soul = 8 }, 0);
        player.current_health = player.max_health = 100;
        player.current_mana = player.max_mana = 3;
        var generator = new CharacterGenerator();
        var opponents = new Array<BattleEntityData>();
        for (var index = 1; index <= Math.Clamp(DemoOpponentCount, 1, 3); index++)
            opponents.Add(Snapshot(generator.generate("凡人", 2), index));
        Configure(player, opponents);
    }

    private static BattleEntityData Copy(BattleEntityData source) => (BattleEntityData)source.Duplicate(true);

    public void reset_battle()
    {
        Units = InitialUnits.Select(Copy).ToList();
        for (var index = 0; index < Units.Count; index++)
            _bloodCapacity[index] = ActionBattleAttributeRuntime.InitializeActionSnapshot(Units[index]);
    }

    public int get_unit_count() => Units.Count;
    public BattleEntityData get_snapshot(int index) => Copy(Units[index]);
    public float get_move_speed() => ActionBattleAttributeRuntime.MoveSpeed;
    public float get_stamina_recovery_base() => ActionBattleAttributeRuntime.StaminaRecoveryBase;
    public void request_close() => GetTree().CallDeferred("change_scene_to_file", "res://tscn/scene/StartMenu.tscn");
    public void restore_action_mana() => Units[0].current_mana = Units[0].max_mana;

    public Dictionary get_unit_state(int index)
    {
        var unit = Units[index];
        return new()
        {
            ["index"] = index, ["uid"] = unit.uid, ["name"] = unit.entity_name,
            ["hp"] = unit.current_health, ["max_hp"] = unit.max_health,
            ["physique"] = unit.physique, ["soul"] = unit.soul, ["intelligence"] = unit.intelligence,
            ["precision"] = unit.precision, ["dodge"] = unit.dodge,
            ["mana"] = unit.current_mana, ["mana_capacity"] = unit.max_mana,
            ["qi_blood_capacity"] = _bloodCapacity[index],
            ["realm"] = unit.cultivation_realm, ["is_human"] = unit.portrait_data is HumanData,
        };
    }

    public Dictionary get_state()
    {
        var player = Units[0];
        return new()
        {
            ["hp"] = player.current_health, ["max_hp"] = player.max_health,
            ["enemy_hp"] = Units.Skip(1).Sum(unit => unit.current_health),
            ["enemy_max_hp"] = Units.Skip(1).Sum(unit => unit.max_health),
            ["physique"] = player.physique, ["soul"] = player.soul, ["intelligence"] = player.intelligence,
            ["dodge"] = player.dodge, ["precision"] = player.precision,
            ["mana"] = player.current_mana, ["mana_capacity"] = player.max_mana,
            ["qi_blood_capacity"] = _bloodCapacity[0], ["wolf_physique"] = 0f,
            ["player_name"] = player.entity_name,
            ["enemy_name"] = Units.Count == 4 ? "三名人类陪练" : Units.Count == 3 ? "两名人类陪练" : "一名人类陪练",
        };
    }

    public Dictionary resolve_contact(int attackerIndex, int defenderIndex, string skillName, int segmentDamage, bool guarding)
    {
        if (attackerIndex < 0 || attackerIndex >= Units.Count || defenderIndex < 0 || defenderIndex >= Units.Count ||
            attackerIndex == defenderIndex || (attackerIndex != 0 && defenderIndex != 0))
            throw new ArgumentException("只允许玩家与三名NPC之间的攻击。");
        var attacker = Units[attackerIndex];
        var defender = Units[defenderIndex];
        if (attacker.current_health <= 0 || defender.current_health <= 0)
            return new() { ["inactive"] = true };
        var skill = skillName switch
        {
            "拳掌" => _fistSkill ??= GD.Load<ActiveSkillData>("res://resources/activeskills/应敌.tres"),
            "痛击" => _heavySkill ??= GD.Load<ActiveSkillData>("res://resources/activeskills/痛击.tres"),
            _ => throw new ArgumentException("人类模拟只接受已迁移的人物攻击。", nameof(skillName))
        };
        if (skillName == "拳掌")
        {
            if (segmentDamage <= 0) throw new ArgumentOutOfRangeException(nameof(segmentDamage));
            if (!_segmentSkills.TryGetValue(segmentDamage, out var segment))
            {
                segment = (ActiveSkillData)skill.Duplicate(true);
                segment.base_damage = segmentDamage;
                segment.damage_formula = "";
                segment.damage_bonuses = [];
                _segmentSkills.Add(segmentDamage, segment);
            }
            skill = segment;
        }
        var block = guarding ? 3 + Mathf.FloorToInt(defender.physique / 4f) +
            Mathf.FloorToInt(Math.Min(defender.physique * .25f, defender.soul * .20f)) : 0;
        var hit = BattleActionResolver.resolve_hit(attacker, defender, skill, block);
        defender.current_health = Math.Max(0, defender.current_health - hit.DamageBeforeShield);
        return new()
        {
            ["inactive"] = false, ["evaded"] = hit.Evaded, ["critical"] = hit.Critical,
            ["damage"] = hit.DamageBeforeShield, ["blocked"] = hit.BlockedDamage,
            ["guarded"] = guarding && !hit.Evaded,
        };
    }
}
