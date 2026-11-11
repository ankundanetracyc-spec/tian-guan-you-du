using System;
using System.Collections.Generic;
using Godot;

/// <summary>守攻试运行的真实人物桥接；人物可以不入场景树，不读取存档或创造战斗时钟。</summary>
public sealed class FormationGuardActor : IFormationGuardActor
{
    private readonly Character _owner;
    private readonly Character _enemy;
    private readonly List<Exception> _notificationErrors = new();
    public IReadOnlyList<Exception> NotificationErrors => _notificationErrors.AsReadOnly();

    public FormationGuardActor(Character owner, Character enemy)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(enemy);
        if (!GodotObject.IsInstanceValid(owner) || !GodotObject.IsInstanceValid(enemy) || ReferenceEquals(owner, enemy))
            throw new ArgumentException("守攻运行须提供两个有效且不同的人物。");
        _owner = owner;
        _enemy = enemy;
        Shields = owner.ShieldSources;
    }

    public int Health
    {
        get => GodotObject.IsInstanceValid(_owner) ? _owner.current_health : 0;
        set
        {
            if (GodotObject.IsInstanceValid(_owner)) _owner.current_health = Math.Clamp(value, 0, Math.Max(0, _owner.max_health));
        }
    }
    public int MaxHealth => GodotObject.IsInstanceValid(_owner) ? _owner.max_health : 0;
    public int Mana => GodotObject.IsInstanceValid(_owner) ? _owner.current_mana : 0;
    public bool IsAlive => GodotObject.IsInstanceValid(_owner) && _owner.current_health > 0;
    public FormationShieldLedger Shields { get; }
    public int EnemyHealth => GodotObject.IsInstanceValid(_enemy) ? _enemy.current_health : 0;

    /// <summary>仅提交真实法术伤害，先用现行防御公式，再消耗敌方公共盾；费用由运行器事务支付。</summary>
    public bool TryAttack(int amount)
    {
        if (amount <= 0 || !IsAlive || !GodotObject.IsInstanceValid(_enemy) || _enemy.current_health <= 0) return false;
        var damage = BattleActionResolver.apply_defense(amount, "magic", _enemy);
        var absorbed = _enemy.ShieldSources.Absorb(damage);
        var before = _enemy.current_health;
        var lost = Math.Min(before, damage - absorbed);
        _enemy.current_health = before - lost;
        try
        {
            if (_enemy.IsInsideTree() && _enemy.HasMethod("update_ui")) _enemy.Call("update_ui");
        }
        catch (Exception error) { _notificationErrors.Add(error); }
        return absorbed > 0 || lost > 0;
    }
}
