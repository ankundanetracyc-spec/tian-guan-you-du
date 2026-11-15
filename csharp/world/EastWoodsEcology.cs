using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// East Woods source recovery. State is nested under the existing route "actions"
/// dictionary so discovery, layout, one-time events, NPC records and task progress
/// remain independent and untouched.
/// </summary>
public static class EastWoodsEcology
{
    private const string StateKey = "__east_woods_ecology";
    private const string ResourceCursorKey = "resource_cursors";
    private const string BeastCursorKey = "ordinary_beast_cursor_hour";
    private const string BeastSequenceKey = "next_beast_identity";
    private const string BeastRecordsKey = "ordinary_beast_encounters";
    private const string PendingBeastChecksKey = "pending_beast_checks";
    private const int HoursPerDay = 24;
    private const int OrdinaryBeastCheckDays = 14;

    public static string ActionProgressKey(string node, string actionId) => node + ":" + actionId;
    private const string DispersedKey = "__dispersed_sources";
    public static bool IsDispersed(Dictionary actions, string node, string actionId)
        => actions.GetValueOrDefault(DispersedKey, new Dictionary()).AsGodotDictionary().ContainsKey(ActionProgressKey(node, actionId));
    public static void MarkEncounterDrivenOff(Dictionary actions, string node, string actionId, string identity, int hour)
    {
        var dispersed = actions.GetValueOrDefault(DispersedKey, new Dictionary()).AsGodotDictionary();
        dispersed[ActionProgressKey(node, actionId)] = hour;
        actions[DispersedKey] = dispersed;
        var ecology = actions.GetValueOrDefault(StateKey, new Dictionary()).AsGodotDictionary();
        foreach (var value in ecology.GetValueOrDefault(BeastRecordsKey, new Godot.Collections.Array()).AsGodotArray())
        {
            var record = value.AsGodotDictionary();
            if (identity.Length == 0 || record.GetValueOrDefault("encounter_id", "").AsString() != identity) continue;
            record["status"] = "driven_off"; record["resolved_hour"] = hour;
        }
        if (ecology.Count > 0) actions[StateKey] = ecology;
    }

    /// <summary>
    /// Applies due recovery to a route snapshot. A missing ecology record is an old
    /// save: cursors begin at the current absolute game hour, with no retroactive gain.
    /// </summary>
    public static bool Synchronize(Dictionary routeState, int currentGameHour, string playerRouteNode)
    {
        var changed = false;
        var actions = routeState.GetValueOrDefault("actions", new Dictionary()).AsGodotDictionary();
        var ecology = actions.GetValueOrDefault(StateKey, new Dictionary()).AsGodotDictionary();
        if (!actions.ContainsKey(StateKey) || ecology.Count == 0)
        {
            ecology = new Dictionary
            {
                [ResourceCursorKey] = new Dictionary(),
                [BeastCursorKey] = currentGameHour,
                [BeastSequenceKey] = 1,
                [BeastRecordsKey] = new Godot.Collections.Array(),
                [PendingBeastChecksKey] = new Dictionary(),
            };
            changed = true;
        }

        var resourceCursors = ecology.GetValueOrDefault(ResourceCursorKey, new Dictionary()).AsGodotDictionary();
        foreach (var (node, action) in EastWoodsActions.ResourcePoints())
        {
            var key = ActionProgressKey(node, action.Id);
            if (!resourceCursors.ContainsKey(key))
            {
                // New resource point or legacy save: establish a baseline, do not
                // credit time from before this recovery source was enabled.
                resourceCursors[key] = currentGameHour;
                changed = true;
                continue;
            }

            var lastHour = resourceCursors.GetValueOrDefault(key, currentGameHour).AsInt32();
            if (currentGameHour < lastHour)
            {
                resourceCursors[key] = currentGameHour;
                changed = true;
                continue;
            }

            var (periodHours, fraction) = RecoveryRate(action.Recovery);
            var elapsed = currentGameHour - lastHour;
            var cycles = elapsed / periodHours;
            if (cycles <= 0) continue;

            var recoveryPerCycle = Math.Max(1, (int)Math.Ceiling(action.Stock * fraction));
            var progress = Math.Max(0, actions.GetValueOrDefault(key, 0).AsInt32());
            // Resource stock is represented by capacity minus harvested quantity.
            // Saturation discards overflow while the cursor still advances by full cycles.
            var recovered = (long)cycles * recoveryPerCycle;
            var nextProgress = (int)Math.Max(0L, progress - recovered);
            if (nextProgress != progress)
            {
                actions[key] = nextProgress;
                changed = true;
            }
            resourceCursors[key] = lastHour + cycles * periodHours;
            changed = true;
        }

        if (!ecology.ContainsKey(BeastCursorKey))
        {
            ecology[BeastCursorKey] = currentGameHour;
            changed = true;
        }
        else
        {
            var lastHour = ecology.GetValueOrDefault(BeastCursorKey, currentGameHour).AsInt32();
            if (currentGameHour < lastHour)
            {
                ecology[BeastCursorKey] = currentGameHour;
                changed = true;
            }
            else
            {
                var periodHours = OrdinaryBeastCheckDays * HoursPerDay;
                var cycles = (currentGameHour - lastHour) / periodHours;
                if (cycles > 0)
                {
                    var pending = ecology.GetValueOrDefault(PendingBeastChecksKey, new Dictionary()).AsGodotDictionary();
                    for (var cycle = 1; cycle <= cycles; cycle++)
                    {
                        var checkHour = lastHour + cycle * periodHours;
                        changed |= refill_ordinary_beasts(actions, ecology, pending, checkHour, playerRouteNode);
                    }
                    ecology[BeastCursorKey] = lastHour + cycles * periodHours;
                    ecology[PendingBeastChecksKey] = pending;
                    changed = true;
                }
            }
        }

        var pendingBeastChecks = ecology.GetValueOrDefault(PendingBeastChecksKey, new Dictionary()).AsGodotDictionary();
        if (!ecology.ContainsKey(PendingBeastChecksKey)) changed = true;
        changed |= refill_pending_ordinary_beasts(actions, ecology, pendingBeastChecks, playerRouteNode, currentGameHour);

        ecology[ResourceCursorKey] = resourceCursors;
        ecology[PendingBeastChecksKey] = pendingBeastChecks;
        actions[StateKey] = ecology;
        routeState["actions"] = actions;
        return changed;
    }

    public static List<EastWoodsActions.ActionDef> AvailableRegeneratedBeasts(string node, Dictionary actions)
    {
        var result = new List<EastWoodsActions.ActionDef>();
        var ecology = actions.GetValueOrDefault(StateKey, new Dictionary()).AsGodotDictionary();
        var records = ecology.GetValueOrDefault(BeastRecordsKey, new Godot.Collections.Array()).AsGodotArray();
        foreach (var value in records)
        {
            if (value.VariantType != Variant.Type.Dictionary) continue;
            var record = value.AsGodotDictionary();
            if (record.GetValueOrDefault("node", "").AsString() != node ||
                record.GetValueOrDefault("status", "active").AsString() != "active") continue;
            var actionId = record.GetValueOrDefault("action_id", "").AsString();
            var identity = record.GetValueOrDefault("encounter_id", "").AsString();
            if (actionId.Length == 0 || identity.Length == 0 || actions.GetValueOrDefault(ActionProgressKey(node, actionId), 0).AsInt32() > 0) continue;
            if (EastWoodsActions.RegeneratedBeast(node, actionId, identity) is { } action) result.Add(action);
        }
        return result;
    }

    // A map refresh queries many nodes. Read the persistent history only once,
    // keeping resolved encounters in the save while indexing current actions.
    internal static System.Collections.Generic.Dictionary<string, List<EastWoodsActions.ActionDef>> IndexAvailableRegeneratedBeasts(Dictionary actions)
    {
        var result = new System.Collections.Generic.Dictionary<string, List<EastWoodsActions.ActionDef>>(StringComparer.Ordinal);
        var ecology = actions.GetValueOrDefault(StateKey, new Dictionary()).AsGodotDictionary();
        var records = ecology.GetValueOrDefault(BeastRecordsKey, new Godot.Collections.Array()).AsGodotArray();
        foreach (var value in records)
        {
            if (value.VariantType != Variant.Type.Dictionary) continue;
            var record = value.AsGodotDictionary();
            if (record.GetValueOrDefault("status", "active").AsString() != "active") continue;
            var node = record.GetValueOrDefault("node", "").AsString();
            var actionId = record.GetValueOrDefault("action_id", "").AsString();
            var identity = record.GetValueOrDefault("encounter_id", "").AsString();
            if (actionId.Length == 0 || identity.Length == 0 || actions.GetValueOrDefault(ActionProgressKey(node, actionId), 0).AsInt32() > 0) continue;
            if (EastWoodsActions.RegeneratedBeast(node, actionId, identity) is not { } action) continue;
            if (!result.TryGetValue(node, out var list)) result[node] = list = [];
            list.Add(action);
        }
        return result;
    }

    public static EastWoodsActions.ActionDef? FindAction(string node, string actionId, Dictionary actions)
    {
        if (EastWoodsActions.Find(node, actionId) is { } fixedAction) return fixedAction;
        return AvailableRegeneratedBeasts(node, actions).FirstOrDefault(action => action.Id == actionId);
    }

    public static void MarkEncounterDefeated(Dictionary actions, string encounterIdentity, int currentGameHour)
    {
        if (string.IsNullOrWhiteSpace(encounterIdentity)) return;
        var ecology = actions.GetValueOrDefault(StateKey, new Dictionary()).AsGodotDictionary();
        var records = ecology.GetValueOrDefault(BeastRecordsKey, new Godot.Collections.Array()).AsGodotArray();
        foreach (var value in records)
        {
            if (value.VariantType != Variant.Type.Dictionary) continue;
            var record = value.AsGodotDictionary();
            if (record.GetValueOrDefault("encounter_id", "").AsString() != encounterIdentity) continue;
            record["status"] = "defeated";
            record["resolved_hour"] = currentGameHour;
            ecology[BeastRecordsKey] = records;
            actions[StateKey] = ecology;
            return;
        }
    }

    private static bool refill_ordinary_beasts(Dictionary actions, Dictionary ecology, Dictionary pending, int checkHour, string playerRouteNode)
    {
        var records = ecology.GetValueOrDefault(BeastRecordsKey, new Godot.Collections.Array()).AsGodotArray();
        var sequence = Math.Max(1, ecology.GetValueOrDefault(BeastSequenceKey, 1).AsInt32());
        var changed = false;

        foreach (var (node, source) in EastWoodsActions.OrdinaryBeastPoints())
        {
            // Static initial encounters remain one-time actions. They are never
            // reset; replenished encounters get their own persistent action ID.
            if (actions.GetValueOrDefault(ActionProgressKey(node, source.Id), 0).AsInt32() <= 0 && !IsDispersed(actions, node, source.Id)) continue;
            var capacity = Math.Max(1, source.OrdinaryBeastCapacity);
            var active = count_active_beasts(actions, records, node);
            if (active >= capacity) continue;
            if (node == playerRouteNode)
            {
                // Keep the missed check in route state. It can be fulfilled after
                // the player leaves, without waiting for another 14-day boundary.
                pending[node] = Math.Min(capacity, pending.GetValueOrDefault(node, 0).AsInt32() + 1);
                changed = true;
                continue;
            }

            add_regenerated_beast(records, node, checkHour, ref sequence);
            changed = true;
        }

        if (changed)
        {
            ecology[BeastSequenceKey] = sequence;
            ecology[BeastRecordsKey] = records;
            ecology[PendingBeastChecksKey] = pending;
        }
        return changed;
    }

    private static bool refill_pending_ordinary_beasts(Dictionary actions, Dictionary ecology, Dictionary pending, string playerRouteNode, int currentGameHour)
    {
        if (pending.Count == 0) return false;
        var records = ecology.GetValueOrDefault(BeastRecordsKey, new Godot.Collections.Array()).AsGodotArray();
        var sequence = Math.Max(1, ecology.GetValueOrDefault(BeastSequenceKey, 1).AsInt32());
        var changed = false;

        foreach (var key in pending.Keys.ToArray())
        {
            var node = key.AsString();
            if (node == playerRouteNode) continue;
            var deferredChecks = Math.Max(0, pending.GetValueOrDefault(node, 0).AsInt32());
            var source = EastWoodsActions.OrdinaryBeastPoints().FirstOrDefault(point => point.Node == node);
            if (deferredChecks == 0 || source.Action is null ||
                actions.GetValueOrDefault(ActionProgressKey(node, source.Action.Id), 0).AsInt32() <= 0 && !IsDispersed(actions, node, source.Action.Id))
            {
                pending.Remove(node);
                changed = true;
                continue;
            }

            var capacity = Math.Max(1, source.Action.OrdinaryBeastCapacity);
            var available = Math.Max(0, capacity - count_active_beasts(actions, records, node));
            var spawnCount = Math.Min(deferredChecks, available);
            for (var index = 0; index < spawnCount; index++)
                add_regenerated_beast(records, node, currentGameHour, ref sequence);

            // A source has a hard active capacity. Any excess deferred checks are
            // saturated here rather than stored as an unbounded future queue.
            pending.Remove(node);
            changed = true;
        }

        if (changed)
        {
            ecology[BeastSequenceKey] = sequence;
            ecology[BeastRecordsKey] = records;
            ecology[PendingBeastChecksKey] = pending;
        }
        return changed;
    }

    private static int count_active_beasts(Dictionary actions, Godot.Collections.Array records, string node)
        => records.Count(value => value.VariantType == Variant.Type.Dictionary &&
            value.AsGodotDictionary().GetValueOrDefault("node", "").AsString() == node &&
            value.AsGodotDictionary().GetValueOrDefault("status", "active").AsString() == "active" &&
            actions.GetValueOrDefault(ActionProgressKey(node, value.AsGodotDictionary().GetValueOrDefault("action_id", "").AsString()), 0).AsInt32() <= 0);

    private static void add_regenerated_beast(Godot.Collections.Array records, string node, int createdHour, ref int sequence)
    {
        var candidateSequence = sequence;
        while (records.Any(value => value.VariantType == Variant.Type.Dictionary &&
               value.AsGodotDictionary().GetValueOrDefault("encounter_id", "").AsString() == $"ewb_{candidateSequence:D6}"))
            candidateSequence++;
        var encounterId = $"ewb_{candidateSequence:D6}";
        var actionId = $"wolf_recovery_{candidateSequence:D6}";
        sequence = candidateSequence + 1;
        records.Add(new Dictionary
        {
            ["encounter_id"] = encounterId,
            ["action_id"] = actionId,
            ["node"] = node,
            ["creature_id"] = "forest_wolf",
            ["created_hour"] = createdHour,
            ["resolved_hour"] = -1,
            ["status"] = "active",
        });
    }

    internal static (int PeriodHours, double Fraction) RecoveryRate(EastWoodsActions.ResourceRecoveryGroup group)
        => group switch
        {
            EastWoodsActions.ResourceRecoveryGroup.Common => (7 * HoursPerDay, 0.50),
            EastWoodsActions.ResourceRecoveryGroup.SlowGrowth => (30 * HoursPerDay, 0.25),
            EastWoodsActions.ResourceRecoveryGroup.Mineral => (90 * HoursPerDay, 0.25),
            _ => throw new ArgumentOutOfRangeException(nameof(group), group, "No recovery rule is defined for this resource group."),
        };
}
