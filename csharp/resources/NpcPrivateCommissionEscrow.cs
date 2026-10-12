using Godot;

/// <summary>私委托管预留结构。尚无 NPC 贡献来源，不开放发布或自动扣款。</summary>
[GlobalClass]
public partial class NpcPrivateCommissionEscrow : Resource
{
    [Export] public string commission_id = "";
    [Export] public string template_id = "";
    [Export] public string issuer_id = "";
    [Export] public string assignee_id = "";
    [Export] public string state = "draft";
    [Export] public int reward_reserved;
    [Export] public int fee_reserved;
    [Export] public string reserve_transaction_id = "";
    [Export] public string fee_transaction_id = "";
    [Export] public string settlement_transaction_id = "";
    [Export] public string refund_transaction_id = "";
}
