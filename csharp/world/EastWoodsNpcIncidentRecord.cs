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
    /// <summary>Faction snapshot used only to establish which body may hear the case.</summary>
    [Export] public string attacker_faction_id = "";
    [Export] public string target_npc_id = "";
    [Export] public string target_npc_name = "";
    /// <summary>Faction snapshot used only to establish which body may hear the case.</summary>
    [Export] public string target_faction_id = "";
    /// <summary>Structured event cause; never reconstructed from personality, relationship, or free text.</summary>
    [Export] public string attack_trigger_id = "";
    /// <summary>Empty means the simulator recorded no authorization for this event.</summary>
    [Export] public string attack_authorization_id = "";
    /// <summary>True only for new incident records assembled from the battle result, death and transfer ledgers.</summary>
    [Export] public bool source_facts_complete;
    [Export] public string battle_result = ResultNoDecision;
    [Export] public bool attacker_died;
    [Export] public bool target_died;
    [Export] public Godot.Collections.Array<EastWoodsNpcItemTransferRecord> item_transfers = [];
    [Export] public Godot.Collections.Array<string> evidence_source_refs = [];

    /// <summary>available → assigned → submitted_pending_ruling; no adjudication is performed here.</summary>
    [Export] public string case_status = StatusAvailable;
    [Export] public string assigned_duty_instance_id = "";
    [Export] public string submitted_at = "";

    /// <summary>Legal state is separate from the legacy evidence-review task state above.</summary>
    [Export] public string legal_status = YuxuSectDraftLawEvaluator.LegalStatusNotApplicable;
    [Export] public Godot.Collections.Array<string> ruling_rule_ids = [];
    [Export] public Godot.Collections.Array<string> ruling_sanction_ids = [];
    [Export] public string ruling_offender_npc_id = "";
    [Export] public string ruling_at = "";
    [Export] public string ruling_findings = "";
    [Export] public int prior_final_offense_count;
    [Export] public string appeal_status = YuxuSectDraftLawEvaluator.AppealStatusNone;
    [Export] public string appellant_npc_id = "";
    [Export] public string appeal_ground_id = "";
    [Export] public string appeal_filed_at = "";
    [Export] public string appeal_reviewed_at = "";
    [Export] public string appeal_outcome = "";
    [Export] public Godot.Collections.Array<YuxuSectCaseAppealRecord> appeal_records = [];
    [Export] public bool capture_warrant_authorized;
    [Export] public string capture_warrant_target_npc_id = "";
    [Export] public string capture_warrant_authorized_at = "";
    [Export] public bool capture_warrant_kill_authorized;
    [Export] public bool sanctions_executed;
    [Export] public string sanctions_executed_at = "";
    [Export] public Godot.Collections.Array<YuxuSectCaseTaskRecord> field_tasks = [];
}
