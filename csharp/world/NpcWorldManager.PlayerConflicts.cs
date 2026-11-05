using Godot;

public partial class NpcWorldManager
{
    public void apply_player_conflict_snapshot(BattleEntityData snapshot, string location, string incidentId)
    {
        if (!snapshot.uid.StartsWith("worldnpc:")) return;
        var npc = get_npc(snapshot.uid[9..]);
        if (npc is null || !npc.is_alive) return;
        var before = npc.current_health;
        npc.current_health = Mathf.Clamp(snapshot.current_health, 0, npc.max_health);
        if (npc.current_health <= 0)
        {
            var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
            mark_dead(npc, time is null ? 0 : month_index(time), "与玩家交手身亡", $"在{location}遭到玩家攻击，战后伤重身亡。冲突记录：{incidentId}。");
        }
        else if (npc.current_health < before) npc.current_state = "交手后负伤";
        EmitSignal(SignalName.world_npcs_changed);
    }

    public void record_player_conflict_loot(string incidentId, WorldNpcData loser, string path, int count, string playerName)
    {
        var incident = get_east_woods_incident(incidentId);
        if (incident is null || incident.attacker_npc_id != "player" || incident.target_npc_id != loser.npc_id || count <= 0) return;
        incident.item_transfers.Add(new EastWoodsNpcItemTransferRecord { item_path = path, quantity = count,
            from_npc_id = loser.npc_id, from_npc_name = loser.npc_name, to_npc_id = "player", to_npc_name = playerName });
        incident.evidence_source_refs.Add("claimed-transfer:" + (incident.item_transfers.Count - 1));
        EmitSignal(SignalName.world_npcs_changed);
    }
}
