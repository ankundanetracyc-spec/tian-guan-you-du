using Godot;

public partial class Character
{
    private void CaptureCombatBoardPreparation(BattleEntityData snapshot)
    {
        if (human is null) return;
        snapshot.combat_board_preparation = human.combat_board_preparation?.CopyConfiguration();
        snapshot.combat_board_presets = CombatBoardPreparationService.GetLibrary(this);
        if (!HasMeta("artifact_battle_clone") && human.combat_board_preparation is { } opening)
            snapshot.combat_board_layout = opening.CopyConfiguration();
    }
    private void ApplyCombatBoardPreparation(BattleEntityData snapshot)
    {
        // 世界人物的默认与模板不接受战斗回传；仅为战斗替身构建只读来源副本。
        if (human is null || !HasMeta("artifact_battle_clone")) return;
        human.combat_board_preparation = snapshot.combat_board_preparation?.CopyConfiguration();
        human.combat_board_presets = snapshot.combat_board_presets?.CopyConfiguration();
    }
}
