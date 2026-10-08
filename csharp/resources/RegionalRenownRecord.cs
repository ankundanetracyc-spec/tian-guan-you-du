using Godot;

/// <summary>可追溯的区域公开事迹声望流水；不承载势力态度或 NPC 私交。</summary>
[GlobalClass]
public partial class RegionalRenownRecord : Resource
{
    [Export] public string record_id = "";
    [Export] public string source_transaction_id = "";
    [Export] public string region_id = "";
    [Export] public string source_title = "";
    [Export] public string occurred_at = "";
    [Export] public int renown_delta;
    [Export] public bool is_public = true;
}
