using Godot;

public partial class HumanData
{
    /// <summary>局外应用的开局意图，与会留下空瓶、符痕的战中运行盘分别保存。</summary>
    [Export] public CombatBoardLayout? combat_board_preparation;
    [Export] public CombatBoardPresetLibrary? combat_board_presets;
}
