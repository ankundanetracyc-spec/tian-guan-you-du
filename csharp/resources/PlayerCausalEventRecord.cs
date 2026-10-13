using Godot;

/// <summary>
/// A traceable fact from the player's life in this world. This stores evidence, not a moral score.
/// </summary>
[GlobalClass]
public partial class PlayerCausalEventRecord : Resource
{
    /// <summary>Stable, caller-supplied ID for this occurrence; used for deduplication and lookup.</summary>
    [Export] public string event_id = "";
    /// <summary>Absolute game hour from GameTimeMgr.get_total_game_hours(); -1 means unknown.</summary>
    [Export] public long occurred_at_total_game_hours = -1;

    /// <summary>Stable type key supplied by the event source; it does not imply a moral or sect classification.</summary>
    [Export] public string event_type_id = "";
    [Export] public string event_type_name = "";

    [Export] public string related_entity_id = "";
    [Export] public string related_entity_name = "";
    [Export] public string location_id = "";
    [Export] public string location_name = "";

    /// <summary>Known reason or context. Empty means the reason was not recorded, not that there was none.</summary>
    [Export(PropertyHint.MultilineText)] public string context = "";
    /// <summary>Observed outcome of the event; do not replace this with an inferred judgment.</summary>
    [Export(PropertyHint.MultilineText)] public string outcome = "";

    /// <summary>
    /// Explicit evidence flag for an already-defined Yuxu hard rule. This flag alone never identifies or proves a rule.
    /// </summary>
    [Export] public bool supports_yuxu_existing_hard_rule;
    /// <summary>
    /// Opaque reference to an existing rule ID supplied by a trusted caller. Empty or unknown IDs remain unverified;
    /// this field does not define new Yuxu rules.
    /// </summary>
    [Export] public string yuxu_hard_rule_id = "";
}
