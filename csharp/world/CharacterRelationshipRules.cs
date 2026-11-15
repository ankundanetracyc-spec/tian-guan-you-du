using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>方向性人物私交的事件登记、月度归纳与行为判定入口。</summary>
public static class CharacterRelationshipRules
{
    public sealed record RelationshipValueChange(string TargetCharacterId, string Axis, int PreviousValue, int CurrentValue);

    public const string PlayerCharacterId = "player";
    public const string Closeness = "closeness";
    public const string Trust = "trust";
    public const string Respect = "respect";
    public const string Resentment = "resentment";
    public const string SuccessfulConversation = "successful_conversation";
    public const string FairTrade = "fair_trade";
    public const string CompletedSpar = "completed_spar";
    public const string ExplicitHarm = "explicit_harm";
    public const string MajorAid = "major_aid";
    public const int ConversationBaseAmount = 3;
    public const int FairTradeBaseAmount = 3;
    public const int SparBaseAmount = 4;
    public const int HarmBaseAmount = 18;
    public const int AidTrustBaseAmount = 12;

    private static Godot.Collections.Dictionary relationship_map(HumanData actor)
        => actor.directed_relationships ??= [];
    private static Godot.Collections.Dictionary relationship_map(WorldNpcData actor)
        => actor.directed_relationships ??= [];

    public static CharacterRelationshipRecord? Get(HumanData actor, string targetId)
        => get_record(relationship_map(actor), targetId);

    public static CharacterRelationshipRecord? Get(WorldNpcData actor, string targetId)
        => get_record(relationship_map(actor), targetId);

    public static CharacterRelationshipRecord GetOrCreate(HumanData actor, string targetId)
        => get_or_create(relationship_map(actor), targetId, "");

    public static CharacterRelationshipRecord GetOrCreate(WorldNpcData actor, string targetId)
        => get_or_create(relationship_map(actor), targetId, legacy_marker(actor, targetId));

    private static CharacterRelationshipRecord? get_record(Godot.Collections.Dictionary map, string targetId)
    {
        if (string.IsNullOrWhiteSpace(targetId) || !map.TryGetValue(targetId, out var value)) return null;
        return value.AsGodotObject() as CharacterRelationshipRecord;
    }

    private static CharacterRelationshipRecord get_or_create(Godot.Collections.Dictionary map, string targetId, string legacyMarker)
    {
        if (string.IsNullOrWhiteSpace(targetId)) throw new ArgumentException("关系目标必须使用稳定人物 ID", nameof(targetId));
        var existing = get_record(map, targetId);
        if (existing is not null)
        {
            if (existing.legacy_marker.Length == 0 && legacyMarker.Length > 0) existing.legacy_marker = legacyMarker;
            return existing;
        }
        var created = new CharacterRelationshipRecord { target_character_id = targetId, legacy_marker = legacyMarker };
        map[targetId] = created;
        return created;
    }

    private static string legacy_marker(WorldNpcData actor, string targetId)
    {
        if (targetId == PlayerCharacterId)
        {
            var old = actor.player_social_memory.GetValueOrDefault("relationship", "").AsString();
            return old is "friend" or "enemy" ? old : "";
        }
        if (actor.relation_tags.Contains("仇敌:" + targetId)) return "enemy";
        if (actor.relation_tags.Contains("盟友:" + targetId)) return "friend";
        return "";
    }

    public static void PreserveLegacyMarker(WorldNpcData actor, string targetId, string marker)
    {
        if (string.IsNullOrWhiteSpace(targetId) || marker is not ("friend" or "enemy")) return;
        GetOrCreate(actor, targetId).legacy_marker = marker;
    }

    public static string LegacyMarker(WorldNpcData actor, string targetId)
        => Get(actor, targetId)?.legacy_marker ?? legacy_marker(actor, targetId);

    public static int Value(HumanData actor, string targetId, string axis)
        => axis_value(Get(actor, targetId), axis);

    public static int Value(WorldNpcData actor, string targetId, string axis)
        => axis_value(Get(actor, targetId), axis);

    private static int axis_value(CharacterRelationshipRecord? record, string axis) => record is null ? 0 : axis switch
    {
        Closeness => record.closeness,
        Trust => record.trust,
        Respect => record.respect,
        Resentment => record.resentment,
        _ => 0,
    };

    public static int StageIndex(int value) => value switch
    {
        < 10 => 0,
        < 30 => 1,
        < 55 => 2,
        < 80 => 3,
        _ => 4,
    };

    public static string StageLabel(string axis, int value)
    {
        var stage = StageIndex(Math.Clamp(value, 0, 100));
        return axis switch
        {
            Closeness => new[] { "尚无私交", "略有往来", "渐渐相熟", "彼此亲近", "深厚情谊" }[stage],
            Trust => new[] { "尚无托付", "尚可往来", "有所信赖", "可托实事", "深度信赖" }[stage],
            Respect => new[] { "未见敬意", "略有认可", "颇为敬重", "深深敬重", "由衷钦佩" }[stage],
            Resentment => new[] { "未结怨", "微有芥蒂", "心存不满", "怨意深重", "结怨难解" }[stage],
            _ => "未详",
        };
    }

    public static string AxisLabel(string axis) => axis switch
    {
        Closeness => "亲近",
        Trust => "信任",
        Respect => "敬重",
        Resentment => "怨结",
        _ => "关系",
    };

    /// <summary>显示玩家这一侧刚登记的关系证据，不泄露对方的内在关系。</summary>
    public static string DescribePlayerEvidence(HumanData actor, string targetId, string eventId, string whenNotRecorded = "")
    {
        var relation = Get(actor, targetId);
        if (relation is null) return whenNotRecorded;
        var id = normalize_event_id(eventId);
        var evidence = relation.pending_evidence.FirstOrDefault(item => item is not null &&
            (item.event_id == id || item.event_id == id + ":player"));
        return evidence is null ? whenNotRecorded : DescribePendingEvidence(relation, evidence);
    }

    public static string DescribePendingEvidence(CharacterRelationshipRecord relation, RelationshipEvidenceRecord evidence)
    {
        var axis = AxisLabel(evidence.axis);
        if (evidence.event_kind == SuccessfulConversation)
        {
            var conversationMonths = relation.settled_conversation_months
                .Concat(relation.pending_evidence.Where(item => item is not null && item.event_kind == SuccessfulConversation)
                    .Select(item => item.month_index))
                .Where(month => month >= 0).Distinct().Count();
            if (conversationMonths < 2)
                return $"亲近记事 +{evidence.base_amount}（待月结）；还需在另一个月份聊成，归纳后才开始增加亲近。";
        }
        return $"{axis}记事 +{evidence.base_amount}（待月结归纳；实际增量按同类经历递减结算）。";
    }

    public static string DescribeValueChange(string axis, int previousValue, int currentValue)
    {
        var delta = currentValue - previousValue;
        if (delta == 0) return "";
        var previousStage = StageLabel(axis, previousValue);
        var currentStage = StageLabel(axis, currentValue);
        var stageChange = previousStage == currentStage
            ? $"阶段仍为「{currentStage}」"
            : $"阶段「{previousStage}」→「{currentStage}」";
        return $"{AxisLabel(axis)} {delta:+#;-#;0}；{stageChange}";
    }

    public static bool AvoidsContact(WorldNpcData actor, string targetId)
        => LegacyMarker(actor, targetId) == "enemy" || Value(actor, targetId, Resentment) >= 30;

    public static bool IsHostile(WorldNpcData actor, string targetId)
        => LegacyMarker(actor, targetId) == "enemy" || Value(actor, targetId, Resentment) >= 55;

    /// <summary>供副本出手判断使用的怨结偏置；不含亲近、信任或敬重。</summary>
    public static int HostilityBias(WorldNpcData actor, string targetId)
    {
        if (LegacyMarker(actor, targetId) == "enemy") return -30;
        var resentment = Value(actor, targetId, Resentment);
        return resentment >= 55 ? -30 : resentment >= 30 ? -10 : 0;
    }

    public static bool RecordConversation(HumanData first, WorldNpcData second, string eventId, int month)
    {
        var id = normalize_event_id(eventId);
        var amount = player_social_amount(second, ConversationBaseAmount);
        var changed = record_evidence(first, second.npc_id, id + ":player", SuccessfulConversation, Closeness, month, amount, "聊成并互通近况。", false);
        changed |= record_evidence(second, PlayerCharacterId, id + ":npc", SuccessfulConversation, Closeness, month, amount, "与主角聊成并互通近况。", false);
        return changed;
    }

    public static bool RecordConversation(WorldNpcData first, WorldNpcData second, string eventId, int month)
    {
        var id = normalize_event_id(eventId);
        var changed = record_evidence(first, second.npc_id, id + ":first", SuccessfulConversation, Closeness, month, ConversationBaseAmount, "与另一人物聊成并互通近况。", false);
        changed |= record_evidence(second, first.npc_id, id + ":second", SuccessfulConversation, Closeness, month, ConversationBaseAmount, "与另一人物聊成并互通近况。", false);
        return changed;
    }

    public static bool RecordFairTrade(HumanData player, WorldNpcData npc, string eventId, int month)
    {
        var id = normalize_event_id(eventId);
        var amount = player_social_amount(npc, FairTradeBaseAmount);
        var changed = record_evidence(player, npc.npc_id, id + ":player", FairTrade, Trust, month, amount, "双方按接近公允的价值完成交易。", false);
        changed |= record_evidence(npc, PlayerCharacterId, id + ":npc", FairTrade, Trust, month, amount, "与主角按接近公允的价值完成交易。", false);
        return changed;
    }

    public static bool IsFairTrade(float playerValue, float npcValue)
    {
        if (float.IsNaN(playerValue) || float.IsInfinity(playerValue) ||
            float.IsNaN(npcValue) || float.IsInfinity(npcValue) || playerValue < 0 || npcValue < 0)
            return false;
        if (npcValue == 0) return playerValue == 0;
        var ratio = playerValue / npcValue;
        return ratio >= .90f && ratio <= 1.10f;
    }

    public static bool RecordCompletedSpar(HumanData player, WorldNpcData npc, string eventId, int month)
    {
        var id = normalize_event_id(eventId);
        var amount = player_social_amount(npc, SparBaseAmount);
        var changed = record_evidence(player, npc.npc_id, id + ":player", CompletedSpar, Respect, month, amount, "完成一次点到为止的切磋。", false);
        changed |= record_evidence(npc, PlayerCharacterId, id + ":npc", CompletedSpar, Respect, month, amount, "与主角完成一次点到为止的切磋。", false);
        return changed;
    }

    /// <summary>五年窗口只加在已经实际发生并登记的关系证据上；日常来信或留帖不调用此入口。</summary>
    private static int player_social_amount(WorldNpcData npc, int baseAmount)
    {
        var now = Godot.Engine.GetMainLoop() is SceneTree tree
            ? tree.Root.GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0
            : 0;
        return npc.acquaintance_window_until_hour > now ? baseAmount + 1 : baseAmount;
    }

    /// <summary>只更新实际受到明确伤害／背弃的一方对行为人的怨结。</summary>
    public static bool RecordImmediateGrievance(HumanData victim, string aggressorId, string eventId, int month, string note = "遭受明确伤害。")
        => record_evidence(victim, aggressorId, normalize_event_id(eventId), ExplicitHarm, Resentment, month, HarmBaseAmount, note, true);

    public static bool RecordImmediateGrievance(WorldNpcData victim, string aggressorId, string eventId, int month, string note = "遭受明确伤害。")
        => record_evidence(victim, aggressorId, normalize_event_id(eventId), ExplicitHarm, Resentment, month, HarmBaseAmount, note, true);

    /// <summary>救助只改变受助者对施助者的信任；另一方的感受须由独立事实另行结算。</summary>
    public static bool RecordImmediateAid(HumanData beneficiary, string helperId, string eventId, int month, string note = "获得重大救助。")
        => record_evidence(beneficiary, helperId, normalize_event_id(eventId), MajorAid, Trust, month, AidTrustBaseAmount, note, true);

    public static bool RecordImmediateAid(WorldNpcData beneficiary, string helperId, string eventId, int month, string note = "获得重大救助。")
        => record_evidence(beneficiary, helperId, normalize_event_id(eventId), MajorAid, Trust, month, AidTrustBaseAmount, note, true);

    private static string normalize_event_id(string eventId)
        => string.IsNullOrWhiteSpace(eventId) ? Guid.NewGuid().ToString("N") : eventId.Trim();

    private static bool record_evidence(HumanData actor, string targetId, string eventId, string kind, string axis, int month, int amount, string note, bool immediate)
        => record_evidence(GetOrCreate(actor, targetId), eventId, kind, axis, month, amount, note, immediate);

    private static bool record_evidence(WorldNpcData actor, string targetId, string eventId, string kind, string axis, int month, int amount, string note, bool immediate)
        => record_evidence(GetOrCreate(actor, targetId), eventId, kind, axis, month, amount, note, immediate);

    private static bool record_evidence(CharacterRelationshipRecord relation, string eventId, string kind, string axis, int month, int amount, string note, bool immediate)
    {
        if (month < 0 || amount <= 0 || !axis_is_valid(axis) || has_event(relation, eventId)) return false;
        if (kind == SuccessfulConversation && (relation.settled_conversation_months.Contains(month) ||
            relation.pending_evidence.Any(item => item is not null && item.event_kind == SuccessfulConversation && item.month_index == month)))
            return false;
        if (immediate)
        {
            apply_amount(relation, kind, axis, amount);
            relation.processed_event_ids.Add(eventId);
            return true;
        }
        relation.pending_evidence.Add(new RelationshipEvidenceRecord
        {
            event_id = eventId,
            event_kind = kind,
            axis = axis,
            month_index = month,
            base_amount = amount,
            note = note,
        });
        return true;
    }

    private static bool axis_is_valid(string axis) => axis is Closeness or Trust or Respect or Resentment;

    private static bool has_event(CharacterRelationshipRecord relation, string eventId)
        => relation.processed_event_ids.Contains(eventId) || relation.pending_evidence.Any(item => item is not null && item.event_id == eventId);

    public static int SettleMonth(HumanData actor, int month)
    {
        if (month < 0 || month <= actor.relationship_last_processed_month) return 0;
        actor.relationship_last_processed_month = month;
        return settle_month(relationship_map(actor), month);
    }

    public static IReadOnlyList<RelationshipValueChange> SettleMonthWithChanges(HumanData actor, int month, out int settledEvidenceCount)
    {
        settledEvidenceCount = 0;
        if (month < 0 || month <= actor.relationship_last_processed_month) return [];
        actor.relationship_last_processed_month = month;
        var map = relationship_map(actor);
        settledEvidenceCount = map.Values
            .Select(value => value.AsGodotObject() as CharacterRelationshipRecord)
            .Where(relation => relation is not null)
            .Sum(relation => relation!.pending_evidence.Count(item => item is not null && item.month_index < month));
        var changes = new List<RelationshipValueChange>();
        settle_month(map, month, changes);
        return changes;
    }

    public static int SettleMonth(WorldNpcData actor, int month)
    {
        if (month < 0 || month <= actor.relationship_last_processed_month) return 0;
        actor.relationship_last_processed_month = month;
        return settle_month(relationship_map(actor), month);
    }

    private static int settle_month(Godot.Collections.Dictionary map, int month, List<RelationshipValueChange>? changes = null)
    {
        var settled = 0;
        foreach (var value in map.Values)
        {
            if (value.AsGodotObject() is not CharacterRelationshipRecord relation || relation.pending_evidence.Count == 0) continue;
            var ready = relation.pending_evidence.Where(item => item is not null && item.month_index < month)
                .OrderBy(item => item.month_index).ThenBy(item => item.event_id, StringComparer.Ordinal).ToList();
            if (ready.Count == 0) continue;
            foreach (var item in ready)
            {
                relation.pending_evidence.Remove(item);
                if (has_processed_event(relation, item.event_id)) continue;
                relation.processed_event_ids.Add(item.event_id);
                if (item.event_kind == SuccessfulConversation)
                {
                    if (relation.settled_conversation_months.Contains(item.month_index)) continue;
                    relation.settled_conversation_months.Add(item.month_index);
                    if (relation.settled_conversation_months.Count < 2) continue;
                }
                var previousValue = axis_value(relation, item.axis);
                apply_amount(relation, item.event_kind, item.axis, item.base_amount);
                var currentValue = axis_value(relation, item.axis);
                if (currentValue != previousValue)
                    changes?.Add(new RelationshipValueChange(relation.target_character_id, item.axis, previousValue, currentValue));
                settled++;
            }
        }
        return settled;
    }

    private static bool has_processed_event(CharacterRelationshipRecord relation, string eventId)
        => relation.processed_event_ids.Contains(eventId);

    private static void apply_amount(CharacterRelationshipRecord relation, string kind, string axis, int baseAmount)
    {
        var count = relation.category_application_counts.GetValueOrDefault(kind, 0).AsInt32();
        var amount = Math.Max(1, (int)Math.Ceiling(baseAmount * 2.0 / (count + 2.0)));
        relation.category_application_counts[kind] = count + 1;
        switch (axis)
        {
            case Closeness: relation.closeness = Math.Clamp(relation.closeness + amount, 0, 100); break;
            case Trust: relation.trust = Math.Clamp(relation.trust + amount, 0, 100); break;
            case Respect: relation.respect = Math.Clamp(relation.respect + amount, 0, 100); break;
            case Resentment: relation.resentment = Math.Clamp(relation.resentment + amount, 0, 100); break;
        }
    }

    public static bool RecordDeathFor(HumanData observer, string deceasedId, int month, string cause)
        => add_life_event(Get(observer, deceasedId), deceasedId, month, cause);

    public static bool RecordDeathFor(WorldNpcData observer, string deceasedId, int month, string cause)
        => add_life_event(Get(observer, deceasedId), deceasedId, month, cause);

    private static bool add_life_event(CharacterRelationshipRecord? relation, string deceasedId, int month, string cause)
    {
        if (relation is null || month < 0) return false;
        var eventId = $"death:{deceasedId}:{month}";
        if (relation.life_events.Any(item => item is not null && item.event_id == eventId)) return false;
        relation.life_events.Add(new RelationshipLifeEventRecord
        {
            event_id = eventId,
            event_type = "known_person_death",
            related_character_id = deceasedId,
            month_index = month,
            cause = cause ?? "",
        });
        return true;
    }
}
