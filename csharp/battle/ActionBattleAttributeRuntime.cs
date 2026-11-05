using Godot;
using Godot.Collections;
using System;

/// <summary>Scene-independent attribute adapter. Costs and spatial contact stay in the action arena.</summary>
public partial class ActionBattleAttributeRuntime : Node
{
    public const float MoveSpeed = 235f;
    public const float StaminaRecoveryBase = .5f;
    private BattleEntityData _initialPlayer = null!, _initialEnemy = null!;
    private ActionBattleSnapshots _battle = null!;
    private int _qiBloodCapacity;
    public Action? CloseRequested;

    public void Configure(BattleEntityData player, BattleEntityData enemy)
    {
        // Validate and isolate before any battle initialization; never mutate the world character.
        var initial = new ActionBattleSnapshots(player, enemy);
        _initialPlayer = initial.Player;
        _initialEnemy = initial.Enemy;
        reset_battle();
    }

    public void reset_battle()
    {
        _battle = new(_initialPlayer, _initialEnemy);
        _qiBloodCapacity = InitializeActionSnapshot(_battle.Player);
        InitializeActionSnapshot(_battle.Enemy);
    }

    internal static int InitializeActionSnapshot(BattleEntityData unit)
    {
        ApplyOpeningAttributes(unit);
        unit.max_stamina = unit.current_stamina = 6;
        unit.current_mana = Math.Max(0, unit.max_mana);
        var body = new Creature { entity_data = new LivingData() };
        try
        {
            body.apply_battle_data(unit);
            body.initialize_body_combat_state(true);
            return body.max_qi_blood;
        }
        finally { body.Free(); }
    }

    private static void ApplyOpeningAttributes(BattleEntityData snapshot)
    {
        // Use the same opening stat modifiers as the queue battle, without entering
        // its scene lifecycle or triggering any heart-method events.
        var body = new Creature { entity_data = new LivingData() };
        try
        {
            body.apply_battle_data(snapshot);
            body.apply_combat_tempering_stats();
            snapshot.max_health = body.max_health; snapshot.current_health = body.current_health;
            snapshot.max_mana = body.max_mana;
            snapshot.precision = body.precision; snapshot.dodge = body.dodge; snapshot.critical = body.critical;
        }
        finally { body.Free(); }
    }

    public float get_move_speed() => MoveSpeed;
    public float get_stamina_recovery_base() => StaminaRecoveryBase;
    public void request_close() => CloseRequested?.Invoke();
    // Public qi cycling only. Heart-method follow-ups are migrated separately.
    public void restore_action_mana() => _battle.Player.current_mana = _battle.Player.max_mana;
    public Dictionary get_state() => new()
    {
        ["hp"] = _battle.Player.current_health, ["max_hp"] = _battle.Player.max_health,
        ["enemy_hp"] = _battle.Enemy.current_health, ["enemy_max_hp"] = _battle.Enemy.max_health,
        ["physique"] = _battle.Player.physique, ["soul"] = _battle.Player.soul,
        ["intelligence"] = _battle.Player.intelligence,
        ["wolf_physique"] = _battle.Enemy.physique,
        ["mana"] = _battle.Player.current_mana, ["mana_capacity"] = _battle.Player.max_mana,
        ["qi_blood_capacity"] = _qiBloodCapacity,
        ["player_name"] = _battle.Player.entity_name, ["enemy_name"] = _battle.Enemy.entity_name,
        ["precision"] = _battle.Player.precision, ["dodge"] = _battle.Player.dodge,
        ["enemy_precision"] = _battle.Enemy.precision, ["enemy_dodge"] = _battle.Enemy.dodge,
    };

    public Dictionary resolve_attack(bool playerAttacks, string skillName, bool guarding) => ResolveAttack(playerAttacks, skillName, guarding, null);

    public Dictionary resolve_combo_attack(int baseDamage)
    {
        if (baseDamage <= 0) throw new ArgumentOutOfRangeException(nameof(baseDamage));
        return ResolveAttack(true, "拳掌", false, baseDamage);
    }

    private Dictionary ResolveAttack(bool playerAttacks, string skillName, bool guarding, int? comboBaseDamage)
    {
        var attacker = playerAttacks ? _battle.Player : _battle.Enemy;
        var defender = playerAttacks ? _battle.Enemy : _battle.Player;
        if (attacker.current_health <= 0 || defender.current_health <= 0) return new() { ["inactive"] = true };
        var path = skillName switch
        {
            "拳掌" when playerAttacks => "res://resources/activeskills/应敌.tres",
            "痛击" when playerAttacks => "res://resources/activeskills/痛击.tres",
            "撕咬" when !playerAttacks => "res://resources/activeskills/妖兽/林地凶狼/撕咬.tres",
            _ => throw new ArgumentException("未迁移的招式不能通过属性适配层施放。", nameof(skillName))
        };
        var skill = GD.Load<ActiveSkillData>(path);
        if (comboBaseDamage is { } segmentDamage)
        {
            // Segment base damage is snapshotted at stage start. Scale before
            // crit, defuse and defense; never mutate the shared queue resource.
            skill = (ActiveSkillData)skill.Duplicate(true);
            skill.base_damage = segmentDamage;
            skill.damage_formula = "";
            skill.damage_bonuses = [];
        }
        var block = !playerAttacks && guarding
            ? 3 + Mathf.FloorToInt(defender.physique / 4f) + Mathf.FloorToInt(Math.Min(defender.physique * .25f, defender.soul * .20f))
            : 0;
        // Reuse the live game's probability, critical and physical-defense pipeline.
        var result = BattleActionResolver.resolve_hit(attacker, defender, skill, block);
        defender.current_health = Math.Max(0, defender.current_health - result.DamageBeforeShield);
        return new()
        {
            ["inactive"] = false, ["evaded"] = result.Evaded, ["critical"] = result.Critical,
            ["damage"] = result.DamageBeforeShield, ["blocked"] = result.BlockedDamage,
            ["guarded"] = !playerAttacks && guarding && !result.Evaded,
            ["hp"] = _battle.Player.current_health, ["enemy_hp"] = _battle.Enemy.current_health,
        };
    }
}
