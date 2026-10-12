using Godot;

/// <summary>One actual carried-item movement settled after an East Woods NPC conflict.</summary>
[GlobalClass]
public partial class EastWoodsNpcItemTransferRecord : Resource
{
    [Export] public string item_path = "";
    [Export] public int quantity;
    [Export] public string from_npc_id = "";
    [Export] public string from_npc_name = "";
    [Export] public string to_npc_id = "";
    [Export] public string to_npc_name = "";
}
