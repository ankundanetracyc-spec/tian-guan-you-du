using Godot;

/// <summary>真实 NPC 的一笔月度需求；完成记录不因查看、拒绝或读档重置。</summary>
[GlobalClass]
public partial class NpcPurchaseRequest : Resource
{
    [Export] public string request_id = "";
    [Export] public string item_path = "";
    [Export] public int quantity;
    [Export] public int unit_price;
    [Export] public int created_month = -1;
    [Export] public int completed_month = -1;
    [Export] public string state = "open";
    [Export] public string reason = "";
}
