using Godot;

/// <summary>One auditable movement of an item owned by Yuxu Sect.</summary>
[GlobalClass]
public partial class YuxuSectTreasuryRecord : Resource
{
    public const string KindDeposit = "deposit";
    public const string KindWithdrawal = "withdrawal";

    [Export] public string record_id = "";
    [Export] public string source_transaction_id = "";
    [Export] public string source_duty_id = "";
    [Export] public string movement_kind = "";
    [Export] public string item_path = "";
    [Export] public int quantity;
    [Export] public string source_title = "";
    [Export] public string occurred_at = "";

    public bool is_valid()
        => !string.IsNullOrWhiteSpace(record_id) &&
           !string.IsNullOrWhiteSpace(source_transaction_id) &&
           !string.IsNullOrWhiteSpace(source_duty_id) &&
           (movement_kind is KindDeposit or KindWithdrawal) &&
           !string.IsNullOrWhiteSpace(item_path) &&
           quantity > 0 &&
           !string.IsNullOrWhiteSpace(source_title) &&
           !string.IsNullOrWhiteSpace(occurred_at);
}
