using Godot;

/// <summary>
/// One auditable contribution transaction for Yuxu Sect.
/// The record is only a data container: no contribution rules or rewards are defined here.
/// </summary>
[GlobalClass]
public partial class YuxuSectContributionRecord : Resource
{
    public const string KindOfficialDutyReward = "official_duty_reward";
    public const string KindPrivateCommissionIncome = "private_commission_income";
    public const string KindPrivateCommissionReserve = "private_commission_reserve";
    public const string KindSectHandlingFee = "sect_handling_fee";
    public const string KindRefund = "refund";
    public const string KindExchangeSpend = "exchange_spend";

    [Export] public string record_id = "";
    [Export] public string source_transaction_id = "";
    /// <summary>Accounting category; kept separate from narrative text for balance/service queries.</summary>
    [Export] public string transaction_kind = "";
    [Export] public string source_title = "";
    [Export] public string occurred_at = "";
    [Export] public int contribution_delta;
    [Export] public string cost_summary = "";
    [Export] public string result_summary = "";
}
