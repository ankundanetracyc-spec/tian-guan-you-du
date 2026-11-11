using Godot;

public partial class BattleEntityData
{
    [Export] public CombatBoardLayout? combat_board_preparation;
    [Export] public CombatBoardPresetLibrary? combat_board_presets;
    private void CopyCombatBoardPreparationFrom(BattleEntityData source)
    {
        combat_board_preparation = source.combat_board_preparation?.CopyConfiguration();
        combat_board_presets = source.combat_board_presets?.CopyConfiguration();
    }
}
