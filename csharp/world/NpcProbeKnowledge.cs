using Godot;
using D = Godot.Collections.Dictionary;

/// <summary>玩家最后一次成功所见；不是NPC当前属性的免费实时接口。</summary>
public static class NpcProbeKnowledge
{
    public const string SnapshotKey = "attribute_probe_snapshot";
    public const string AttemptsKey = "attribute_probe_attempts";
    public static D Snapshot(WorldNpcData npc) => npc.player_known_npc_info.GetValueOrDefault(SnapshotKey, new D()).AsGodotDictionary()
        .GetValueOrDefault("snapshot", new D()).AsGodotDictionary();
    public static bool HasSnapshot(WorldNpcData npc) => Snapshot(npc).GetValueOrDefault("event_id", "").AsString().Length > 0;
    public static bool HasAttempt(WorldNpcData npc, string eventId) =>
        npc.player_known_npc_info.GetValueOrDefault(AttemptsKey, new D()).AsGodotDictionary()
            .GetValueOrDefault("events", new D()).AsGodotDictionary().ContainsKey(eventId);
    public static void RememberAttempt(WorldNpcData npc, string eventId, bool success, bool detected, string date, int month)
    {
        var attempts = npc.player_known_npc_info.GetValueOrDefault(AttemptsKey, new D()).AsGodotDictionary()
            .GetValueOrDefault("events", new D()).AsGodotDictionary();
        attempts[eventId] = new D { ["success"] = success, ["detected"] = detected, ["date"] = date };
        npc.player_known_npc_info[AttemptsKey] = new D { ["value"] = "曾运用灵识探查", ["source"] = "亲历", ["month"] = month, ["events"] = attempts };
    }
    public static void Capture(WorldNpcData npc, string eventId, string date, int hour)
    {
        var snapshot = new D {
            ["event_id"] = eventId, ["date"] = date, ["hour"] = hour, ["realm"] = npc.cultivation_realm,
            ["physique"] = npc.physique, ["intelligence"] = npc.intelligence, ["soul"] = npc.soul,
            ["health"] = npc.current_health, ["max_health"] = npc.max_health, ["max_age"] = npc.max_age,
            ["root"] = npc.spirit_root_name, ["root_weights"] = npc.root_weights.Duplicate(), ["strands"] = npc.spirit_strands
        };
        npc.player_known_npc_info[SnapshotKey] = new D { ["value"] = "已探得一份属性记录", ["source"] = "灵识探查", ["month"] = hour / 720, ["snapshot"] = snapshot };
    }
}
