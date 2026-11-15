using Godot;

/// <summary>迁移中未摆入的旧来源。保留原引用和位置，不删技能、不转换物品。</summary>
public partial class CombatBoardPendingEntry : Resource
{
    [Export] public string source_key = "";
    [Export] public string original_target_key = "";
    [Export] public int legacy_slot_index = -1;
    [Export] public string reason = "";
}
