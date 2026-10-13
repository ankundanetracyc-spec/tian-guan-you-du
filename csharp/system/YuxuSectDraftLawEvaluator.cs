using System;
using System.Linq;
using Godot;

/// <summary>Applies the current configurable draft rules to structured incident facts.</summary>
public static class YuxuSectDraftLawEvaluator
{
    public const string LegalStatusNotApplicable = "not_applicable";
    public const string LegalStatusPending = "pending_ruling";
    public const string LegalStatusRuled = "ruled";
    public const string LegalStatusAppealPending = "appeal_pending";
    public const string LegalStatusAppealReviewed = "appeal_reviewed";
    public const string LegalStatusEnforcementPending = "enforcement_pending";
    public const string LegalStatusClosed = "closed";

    public const string AppealStatusNone = "none";
    public const string AppealStatusPending = "pending";
    public const string AppealStatusUpheld = "upheld";
    public const string AppealStatusChanged = "changed";

    public sealed class Decision
    {
        public bool HasJurisdiction { get; set; }
        public bool EvidenceSufficient { get; set; }
        public string OffenderNpcId { get; set; } = "";
        public int PriorFinalFindings { get; set; }
        public Godot.Collections.Array<string> RuleIds { get; set; } = [];
        public Godot.Collections.Array<string> SanctionIds { get; set; } = [];
        public string FindingText { get; set; } = "";
        public bool CaptureWarrantAuthorized { get; set; }
        public bool CaptureKillAuthorized { get; set; }
    }

    public static YuxuSectLawDraftConfig LoadConfig()
    {
        var loaded = ResourceLoader.Load<YuxuSectLawDraftConfig>("res://resources/yuxu_sect_law_draft_config.tres");
        return loaded ?? new YuxuSectLawDraftConfig();
    }

    public static Decision Evaluate(
        EastWoodsNpcIncidentRecord incident,
        Godot.Collections.Array<EastWoodsNpcIncidentRecord> caseHistory,
        YuxuSectLawDraftConfig config)
    {
        var noJurisdiction = new Decision
        {
            HasJurisdiction = false,
            EvidenceSufficient = false,
            FindingText = "双方在案发时不同时属于玉虚仙宗，或案卷缺少可验证的管辖事实；执中峰不据此作宗内责任认定。"
        };
        if (incident is null || config is null ||
            string.IsNullOrWhiteSpace(incident.incident_id) ||
            string.IsNullOrWhiteSpace(incident.attacker_npc_id) ||
            string.IsNullOrWhiteSpace(incident.target_npc_id) ||
            string.IsNullOrWhiteSpace(incident.occurred_at) ||
            string.IsNullOrWhiteSpace(incident.location_id) ||
            string.IsNullOrWhiteSpace(incident.battle_result) ||
            incident.attacker_faction_id != config.sect_faction_id ||
            incident.target_faction_id != config.sect_faction_id)
            return noJurisdiction;

        // Faction snapshots establish only the scope of the sect's jurisdiction.
        // They are never used as proof of culpability.
        if (incident.source_facts_complete != true)
            return new Decision
            {
                HasJurisdiction = true,
                EvidenceSufficient = false,
                FindingText = "案卷的结构化来源事实不完整；暂不定责，不得以身份标签或关系记录补足证据。"
            };

        var decision = new Decision
        {
            HasJurisdiction = true,
            EvidenceSufficient = true,
            OffenderNpcId = incident.attacker_npc_id
        };

        var attackIsUnauthorized =
            incident.attack_trigger_id == config.automatic_attack_trigger_id &&
            string.IsNullOrWhiteSpace(incident.attack_authorization_id);
        if (attackIsUnauthorized)
        {
            var prior = count_prior_final_findings(caseHistory, incident.incident_id,
                config.unauthorized_attack_rule_id, incident.attacker_npc_id);
            var occurrence = prior + 1;
            var sanction = occurrence >= Math.Max(1, config.expulsion_after_final_findings)
                ? config.repeat_offense_sanction_id
                : occurrence == 2 ? config.second_offense_sanction_id : config.first_offense_sanction_id;
            decision.PriorFinalFindings = prior;
            decision.RuleIds.Add(config.unauthorized_attack_rule_id);
            if (!string.IsNullOrWhiteSpace(sanction)) decision.SanctionIds.Add(sanction);
            decision.FindingText = $"结构化战斗记录证明发起方先行发起斗法，且该次自动突袭没有关联授权案卷；按门规草案认定“{config.unauthorized_attack_title}”成立。";
            if (incident.attacker_died || incident.target_died)
                decision.FindingText += " " + config.fatal_outcome_finding_text;
            decision.CaptureWarrantAuthorized =
                sanction == config.repeat_offense_sanction_id && config.expulsion_creates_capture_warrant;
            decision.CaptureKillAuthorized = decision.CaptureWarrantAuthorized && config.capture_task_kill_authorized;
        }
        else
        {
            decision.FindingText = string.IsNullOrWhiteSpace(incident.attack_authorization_id)
                ? "来源记录未能证明本案适用的门规违例；不作有罪认定。"
                : "案卷记有有效授权关联；不按“无令先行斗法”定责。";
            if (incident.attacker_died || incident.target_died)
                decision.FindingText += " " + config.fatal_outcome_finding_text;
        }

        // A transfer is an objective fact, but only a transfer from the attacked
        // party to the recorded initiator is covered by this restitution rule.
        // Transfers in the opposite direction are not treated as proof of theft.
        var attackerTookFromTarget = incident.item_transfers?.Any(transfer =>
            transfer is not null && transfer.quantity > 0 &&
            transfer.from_npc_id == incident.target_npc_id &&
            transfer.to_npc_id == incident.attacker_npc_id) == true;
        if (attackIsUnauthorized && attackerTookFromTarget)
        {
            decision.RuleIds.Add(config.restitution_rule_id);
            if (!string.IsNullOrWhiteSpace(config.restitution_sanction_id))
                decision.SanctionIds.Add(config.restitution_sanction_id);
            decision.FindingText += $" 案卷中的实际物品流水同时满足“{config.restitution_title}”条件，按原物数量追还。";
        }

        if (decision.RuleIds.Count == 0)
            decision.FindingText += " 仅凭战斗结果、死亡、境界、性情、派系或人物关系，不推定主观故意或其他罪名。";
        return decision;
    }

    private static int count_prior_final_findings(
        Godot.Collections.Array<EastWoodsNpcIncidentRecord> cases,
        string currentIncidentId,
        string ruleId,
        string offenderNpcId)
    {
        if (cases is null || string.IsNullOrWhiteSpace(ruleId) || string.IsNullOrWhiteSpace(offenderNpcId)) return 0;
        return cases.Count(item => item is not null && item.incident_id != currentIncidentId &&
            item.ruling_offender_npc_id == offenderNpcId &&
            item.ruling_rule_ids?.Contains(ruleId) == true &&
            item.legal_status is LegalStatusRuled or LegalStatusAppealReviewed or LegalStatusEnforcementPending or LegalStatusClosed &&
            item.appeal_status != AppealStatusChanged);
    }
}
