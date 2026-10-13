using Godot;

/// <summary>
/// Tunable first-pass rules for the Yuxu sect case loop. This is an implementation
/// proposal, not a claim that these are final in-world laws.
/// </summary>
[GlobalClass]
public partial class YuxuSectLawDraftConfig : Resource
{
    [Export] public string sect_faction_id = "yuxu_xianzong";
    [Export] public string automatic_attack_trigger_id = "npc_opportunistic_attack";

    [Export] public string unauthorized_attack_rule_id = "unauthorized_first_strike";
    [Export] public string unauthorized_attack_title = "无令先行斗法";
    [Export] public string first_offense_sanction_id = "admonition";
    [Export] public string second_offense_sanction_id = "recorded_demerit";
    [Export] public string repeat_offense_sanction_id = "expulsion";
    [Export] public int expulsion_after_final_findings = 3;

    [Export] public string restitution_rule_id = "combat_loot_restitution";
    [Export] public string restitution_title = "斗法夺物，依案追还";
    [Export] public string restitution_sanction_id = "restitution";

    [Export] public string fatal_outcome_finding_id = "fatal_outcome_review";
    [Export] public string fatal_outcome_finding_text = "案卷记录有死亡结果；现有证据不证明杀意，须由长老另行复核，不据此直接定故意杀人。";

    [Export] public int appeals_per_party = 1;
    [Export] public bool appeals_close_when_enforcement_starts = true;
    [Export] public string appeal_review_basis = "按已存结构化事实重新核对门规适用；没有新证据时不得改写原始事实。";

    [Export] public bool expulsion_creates_capture_warrant = true;
    [Export] public bool capture_task_kill_authorized = false;
    [Export] public int field_task_contribution_reward;
}
