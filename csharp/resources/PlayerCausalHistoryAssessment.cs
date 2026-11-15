using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.Linq;

public enum PlayerCausalHistoryConclusion
{
    NoRecord,
    CitableFacts,
    NoVerifiableConclusion
}

public enum YuxuHardRuleConclusion
{
    /// <summary>No event is recorded as supporting a hard rule; this is not proof that no violation occurred.</summary>
    NoRecordedEvidence,
    /// <summary>The existing hard-rule catalogue or the event evidence is insufficient for a conclusion.</summary>
    NoVerifiableConclusion,
    ConfirmedHardRuleFact
}

/// <summary>Pure-data result for presenting traceable player history without inferring character morality or deciding admission.</summary>
public sealed class PlayerCausalHistoryAssessment
{
    public PlayerCausalHistoryConclusion history_conclusion { get; }
    public YuxuHardRuleConclusion yuxu_hard_rule_conclusion { get; }
    public Array<PlayerCausalEventRecord> citable_facts { get; }
    public Array<PlayerCausalEventRecord> confirmed_hard_rule_facts { get; }
    public Array<PlayerCausalEventRecord> unverified_hard_rule_facts { get; }
    public bool has_incomplete_records { get; }

    internal PlayerCausalHistoryAssessment(
        PlayerCausalHistoryConclusion historyConclusion,
        YuxuHardRuleConclusion hardRuleConclusion,
        Array<PlayerCausalEventRecord> citableFacts,
        Array<PlayerCausalEventRecord> confirmedHardRuleFacts,
        Array<PlayerCausalEventRecord> unverifiedHardRuleFacts,
        bool hasIncompleteRecords)
    {
        history_conclusion = historyConclusion;
        yuxu_hard_rule_conclusion = hardRuleConclusion;
        citable_facts = citableFacts;
        confirmed_hard_rule_facts = confirmedHardRuleFacts;
        unverified_hard_rule_facts = unverifiedHardRuleFacts;
        has_incomplete_records = hasIncompleteRecords;
    }

    /// <summary>Builds factual, non-scoring lines suitable for the Yuxu gate welcome interaction.</summary>
    public Array<string> get_yuxu_gate_judgment_lines()
    {
        var lines = new Array<string>();
        switch (history_conclusion)
        {
            case PlayerCausalHistoryConclusion.NoRecord:
                lines.Add("长老静静听你说明来意，神色如常。");
                break;
            case PlayerCausalHistoryConclusion.NoVerifiableConclusion:
                // Incomplete records are not themselves evidence or an unverified clue.
                break;
            case PlayerCausalHistoryConclusion.CitableFacts:
                foreach (var fact in citable_facts)
                {
                    var formattedFact = PlayerCausalHistoryEvaluator.format_citable_fact(fact);
                    if (!string.IsNullOrWhiteSpace(formattedFact)) lines.Add(formattedFact);
                }
                break;
        }

        switch (yuxu_hard_rule_conclusion)
        {
            case YuxuHardRuleConclusion.NoVerifiableConclusion when unverified_hard_rule_facts.Count > 0:
                lines.Add("长老的神色微微一凝，似有一处牵连尚待辨明，终未下断语。");
                break;
            case YuxuHardRuleConclusion.ConfirmedHardRuleFact:
                lines.Add("长老神色转肃，显是认出了其中牵涉的一条现行门规。");
                break;
        }

        return lines;
    }
}

/// <summary>Pure-data evaluation and formatting. It never reads karma, renown, attitude, or relationships.</summary>
public static class PlayerCausalHistoryEvaluator
{
    /// <summary>
    /// Evaluates saved events. A hard-rule fact is confirmed only when its explicit evidence flag is set,
    /// its event is citable, and its opaque rule ID occurs in the caller's trusted existing-rule IDs.
    /// A null/empty rule-ID set intentionally makes every hard-rule claim unverifiable.
    /// </summary>
    public static PlayerCausalHistoryAssessment evaluate(
        Array<PlayerCausalEventRecord>? records,
        IReadOnlyCollection<string>? verifiedExistingYuxuHardRuleIds = null)
    {
        var citableFacts = new Array<PlayerCausalEventRecord>();
        var confirmedHardRuleFacts = new Array<PlayerCausalEventRecord>();
        var unverifiedHardRuleFacts = new Array<PlayerCausalEventRecord>();
        var verifiedRuleIds = verifiedExistingYuxuHardRuleIds is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>(verifiedExistingYuxuHardRuleIds.Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
        var hasVerifiedHardRuleCatalogue = verifiedRuleIds.Count > 0;

        var foundRecord = false;
        var hasIncompleteRecords = false;
        var hasUnverifiedHardRuleClaim = false;

        if (records is not null)
        {
            foreach (var record in records)
            {
                if (record is null) continue;
                foundRecord = true;

                var isCitable = is_citable_fact(record);
                if (isCitable)
                    citableFacts.Add(record);
                else
                    hasIncompleteRecords = true;

                var hasHardRuleClaim = record.supports_yuxu_existing_hard_rule || !string.IsNullOrWhiteSpace(record.yuxu_hard_rule_id);
                if (!hasHardRuleClaim) continue;

                var isVerifiedHardRuleFact = isCitable &&
                    record.supports_yuxu_existing_hard_rule &&
                    !string.IsNullOrWhiteSpace(record.yuxu_hard_rule_id) &&
                    verifiedRuleIds.Contains(record.yuxu_hard_rule_id);
                if (isVerifiedHardRuleFact)
                    confirmedHardRuleFacts.Add(record);
                else
                {
                    unverifiedHardRuleFacts.Add(record);
                    hasUnverifiedHardRuleClaim = true;
                }
            }
        }

        var historyConclusion = !foundRecord
            ? PlayerCausalHistoryConclusion.NoRecord
            : citableFacts.Count > 0
                ? PlayerCausalHistoryConclusion.CitableFacts
                : PlayerCausalHistoryConclusion.NoVerifiableConclusion;
        var hardRuleConclusion = confirmedHardRuleFacts.Count > 0
            ? YuxuHardRuleConclusion.ConfirmedHardRuleFact
            : !hasVerifiedHardRuleCatalogue || hasUnverifiedHardRuleClaim
                ? YuxuHardRuleConclusion.NoVerifiableConclusion
                : YuxuHardRuleConclusion.NoRecordedEvidence;

        return new PlayerCausalHistoryAssessment(
            historyConclusion,
            hardRuleConclusion,
            citableFacts,
            confirmedHardRuleFacts,
            unverifiedHardRuleFacts,
            hasIncompleteRecords);
    }

    /// <summary>A fact is citable only with a stable ID, absolute game time, event type, and recorded outcome.</summary>
    public static bool is_citable_fact(PlayerCausalEventRecord? record)
        => record is not null &&
           !string.IsNullOrWhiteSpace(record.event_id) &&
           record.occurred_at_total_game_hours >= 0 &&
           !string.IsNullOrWhiteSpace(record.event_type_id) &&
           !string.IsNullOrWhiteSpace(record.event_type_name) &&
           !string.IsNullOrWhiteSpace(record.outcome);

    /// <summary>Formats only recorded fields; absent optional details are omitted rather than inferred.</summary>
    public static string format_citable_fact(PlayerCausalEventRecord? record)
    {
        if (!is_citable_fact(record) || record is null) return "";

        var line = $"{format_game_hour(record.occurred_at_total_game_hours)}，小友曾有「{record.event_type_name.Trim()}」一事";
        if (!string.IsNullOrWhiteSpace(record.related_entity_name))
            line += $"，与{record.related_entity_name.Trim()}有关";
        if (!string.IsNullOrWhiteSpace(record.location_name))
            line += $"，事发于{record.location_name.Trim()}";
        if (!string.IsNullOrWhiteSpace(record.context))
            line += $"。其缘由是{record.context.Trim()}";
        line += $"；结果是{record.outcome.Trim()}。";
        return line;
    }

    private static string format_game_hour(long totalHours)
    {
        var totalDays = totalHours / 24;
        var year = totalDays / (12 * 30) + 1;
        var month = totalDays / 30 % 12 + 1;
        var day = totalDays % 30 + 1;
        return $"第{year}年{month}月{day}日";
    }
}
