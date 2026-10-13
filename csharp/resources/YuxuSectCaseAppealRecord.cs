using Godot;

/// <summary>One party's one-time appeal record for a Yuxu sect case.</summary>
[GlobalClass]
public partial class YuxuSectCaseAppealRecord : Resource
{
    public const string StatusPending = "pending";
    public const string StatusUpheld = "upheld";
    public const string StatusChanged = "changed";

    [Export] public string appellant_npc_id = "";
    [Export] public string ground_id = "";
    [Export] public string filed_at = "";
    [Export] public string reviewed_at = "";
    [Export] public string outcome = "";
    [Export] public string status = StatusPending;
}
