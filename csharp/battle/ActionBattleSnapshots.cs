using Godot;
using System;

/// <summary>
/// Owns independent copies of the two existing battle snapshots for an action
/// battle. Victory and persistence remain the transition manager's job.
/// </summary>
public sealed class ActionBattleSnapshots
{
    public BattleEntityData Player { get; }
    public BattleEntityData Enemy { get; }

    public ActionBattleSnapshots(BattleEntityData player, BattleEntityData enemy)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(enemy);
        if (string.IsNullOrWhiteSpace(player.uid) || string.IsNullOrWhiteSpace(enemy.uid) || player.uid == enemy.uid)
            throw new ArgumentException("战斗双方必须具有不同的有效身份。");
        if (player.max_health <= 0 || enemy.max_health <= 0 || player.current_health <= 0 || enemy.current_health <= 0 ||
            player.current_health > player.max_health || enemy.current_health > enemy.max_health)
            throw new ArgumentException("战斗双方必须具有有效的生命快照。");
        Player = Copy(player);
        Enemy = Copy(enemy);
    }

    public void SetHealth(int playerHealth, int enemyHealth)
    {
        if (playerHealth < 0 || playerHealth > Player.max_health || enemyHealth < 0 || enemyHealth > Enemy.max_health)
            throw new ArgumentOutOfRangeException(nameof(playerHealth), "结算生命值超出有效范围。");
        Player.current_health = playerHealth;
        Enemy.current_health = enemyHealth;
    }

    // Returned copies prevent subsequent scene changes from mutating results.
    public Godot.Collections.Array PlayerResults() => new() { Copy(Player) };
    public Godot.Collections.Array EnemyResults() => new() { Copy(Enemy) };

    private static BattleEntityData Copy(BattleEntityData source) =>
        source.Duplicate(true) as BattleEntityData ?? throw new InvalidOperationException("无法复制战斗快照。");
}
