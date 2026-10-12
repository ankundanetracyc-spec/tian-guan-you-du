using Godot;

/// <summary>
/// Authoritative, structured source record for one completed NPC-to-NPC East Woods battle.
/// The Yuxu case workflow may link to and submit this record but does not decide responsibility.
/// </summary>
[GlobalClass]
public partial class EastWoodsNpcIncidentRecord : Resource
{
    public const string StatusAvailable = "available";
    public const string StatusAssigned = "assigned";
    public const string StatusSubmittedPendingRuling = "submitted_pending_ruling";

    public const string ResultAttackerWon = "attacker_won";
    public const string ResultTargetWon = "target_won";
    public const string ResultNoDecision = "no_decisive_result";

    [Export] public string incident_id = "";
    [Export] public string occurred_at = "";
    [Export] public int occurred_game_hour;
    [Export] public string location_id = "";
    [Export] public string location_name = "";
    [Export] public string route_node_id = "";
    [Export] public string attacker_previous_node_id = "";
    [Export] public string attacker_node_id = "";
    [Export] public string target_previous_node_id = "";
    [Export] public string target_node_id = "";

    [Export] public string attacker_npc_id = "";
    [Export] public string attacker_npc_name = "";
    [Export] public string target_npc_id = "";
    [Export] public string target_npc_name = "";
    [Export] public string battle_result = ResultNoDecision;
    [Export] public bool attacker_died;
    [Export] public bool target_died;
    [Export] public Godot.Collections.Array<EastWoodsNpcItemTransferRecord> item_transfers = [];

    /// <summary>available → assigned → submitted_pending_ruling; no adjudication is performed here.</summary>
    [Export] public string case_status = StatusAvailable;
    [Export] public string assigned_duty_instance_id = "";
    [Export] public string submitted_at = "";
}
