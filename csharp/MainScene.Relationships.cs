using Godot;
using System.Linq;

public partial class MainScene
{
    private int _relationshipSettlementMonth = -1;

    private void process_relationship_month()
    {
        if (player?.entity_data is not HumanData human || GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager") is not { } time)
            return;
        var month = (time.year - 1) * 12 + time.month - 1;
        if (month <= _relationshipSettlementMonth) return;
        if (time.IsSmoothAdvanceBusy || _npc_world?.HasPendingSettlement == true || action_transition?.IsPlaying == true)
            return;

        var changes = CharacterRelationshipRules.SettleMonthWithChanges(human, month, out var settledEvidenceCount);
        var date = $"{time.year}年{time.month}月{time.day}日";
        foreach (var group in changes.GroupBy(change => (change.TargetCharacterId, change.Axis)))
        {
            var npc = _npc_world?.get_npc(group.Key.TargetCharacterId);
            if (npc is null) continue;
            var first = group.First();
            var last = group.Last();
            var changeDescription = CharacterRelationshipRules.DescribeValueChange(group.Key.Axis, first.PreviousValue, last.CurrentValue);
            if (string.IsNullOrWhiteSpace(changeDescription)) continue;
            npc.record(new NpcActionRecord
            {
                month_index = month, date_label = date, action_id = $"relationship_settlement:{month}:{group.Key.Axis}",
                action_name = "关系月结", node_id = npc.current_node_id, subscene_id = npc.current_subscene_id,
                target_npc_id = CharacterRelationshipRules.PlayerCharacterId,
                reason = "此前记录的交往证据已归纳。",
                outcome = $"你对{npc.npc_name}的{changeDescription}。"
            });
        }
        if (settledEvidenceCount > 0) info?.refresh_relationships();

        var settled = changes.Count;
        if (_npc_world is not null)
            foreach (var npc in _npc_world.npcs)
                if (npc is not null)
                    settled += CharacterRelationshipRules.SettleMonth(npc, month);
        _relationshipSettlementMonth = month;
        if (settledEvidenceCount > 0 || settled > 0) mark_save_dirty();
    }
}
