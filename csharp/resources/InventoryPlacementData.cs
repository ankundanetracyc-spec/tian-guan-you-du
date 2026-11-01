using Godot;

/// <summary>A persisted physical instance. Legacy row slots identify instances, not cells.</summary>
[GlobalClass]
public partial class InventoryPlacementData : Resource
{
    [Export] public string instance_id = "";
    [Export] public int row_slot = -1;
    [Export] public string item_path = "";
    [Export] public string artifact_id = "";
    [Export] public string region_id = "";
    [Export] public int cell_x = -1;
    [Export] public int cell_y = -1;
    [Export] public int rotation;
}
