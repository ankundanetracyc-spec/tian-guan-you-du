using Godot;
using System;
using System.Linq;

public partial class MainScene
{
    private int _socialMembershipMonth = -1, _socialVisitMonth = -1;
    private double _socialVisitPoll;
    private void sync_social_membership_month()
    {
        if (player?.entity_data is not HumanData human || GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager") is not { } time) return;
        if (human.sect_affiliation_id == YuxuSectId && _socialMembershipMonth < 0) { _socialMembershipMonth = get_game_month_index(time); mark_save_dirty(); }
        else if (human.sect_affiliation_id != YuxuSectId && _socialMembershipMonth >= 0) { _socialMembershipMonth = _socialVisitMonth = -1; mark_save_dirty(); }
    }
    private bool can_contact_social_npc(WorldNpcData npc)
    {
        if (!npc.is_alive || npc.is_traveling || player is null) return false;
        if (_east_woods_dungeon?.Visible == true)
            return npc.dungeon_id == EastWoodsDungeonRules.DungeonId && npc.dungeon_node_id == _east_woods_dungeon.get_current_route_node_id();
        if (!string.IsNullOrWhiteSpace(npc.dungeon_id)) return false;
        if (tile_world_map?.Visible == true)
            return (_npc_world?.get_world_grid_roster(_player_world_grid_cell, _player_world_node_id).Any(n => n.npc_id == npc.npc_id)).GetValueOrDefault();
        if (npc.current_node_id != _player_world_node_id) return false;
        if (location_interior?.Visible == true) return location_interior.is_npc_in_current_space(npc);
        if (GetNodeOrNull<Control>("SceneLayer/YuxuXianzongOverview/PeakInterior")?.IsVisibleInTree() == true)
            return npc.current_subscene_id == _player_current_yuxu_peak_id;
        return string.IsNullOrWhiteSpace(npc.current_subscene_id);
    }
    private void begin_npc_social_talk(WorldNpcData npc, bool proactive)
    {
        if (player?.entity_data is not HumanData human || story_dialogue is null || !can_contact_social_npc(npc)) return;
        info?.hide_panel(); npc_interactions?.close();
        NpcSocialRules.HearSectReputation(npc, human, proactive);
        var place = get_current_profile_location();
        void Answer(string choice)
        {
            if (!can_contact_social_npc(npc)) { story_dialogue.show_social_result(npc, new("对方已离开。", "还有些事，先走一步。", [])); return; }
            if (choice != "leave" && !player.can_spend_world_stamina(WorldStaminaRules.ConversationCost))
            {
                story_dialogue.show_social_result(npc, new("你有些疲惫，便先告辞了。", "那便改日再聊。", []));
                return;
            }
            var response = NpcSocialRules.Evaluate(npc, human, choice);
            if (choice != "leave") { player.try_spend_world_stamina(WorldStaminaRules.ConversationCost); response = response with { StaminaSpent = WorldStaminaRules.ConversationCost, ActionName = NpcSocialRules.KnowsName(npc) ? "闲谈" : "结识" }; }
            var month = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager") is { } time ? get_game_month_index(time) : 0;
            NpcSocialRules.Remember(npc, human, choice, place, month, response);
            mark_save_dirty();
            story_dialogue.show_social_result(npc, response);
        }
        var cost = WorldStaminaRules.ConversationCost;
        story_dialogue.show_social_choices(player, npc, NpcSocialRules.Opening(npc, human, place, proactive),
            new (string, Action)[] {
                ($"接受 · 体力 {cost:0.#}", () => Answer("introduce")),
                ("婉拒", () => Answer("leave"))
            }, () => Answer("leave"));
    }
    private void process_npc_social_visit(double delta)
    {
        _socialVisitPoll += delta;
        if (_socialVisitPoll < .5) return;
        _socialVisitPoll = 0;
        if (player?.entity_data is not HumanData human || GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager") is not { } time) return;
        if (human.sect_affiliation_id != YuxuSectId) { if (_socialMembershipMonth >= 0) { _socialMembershipMonth = _socialVisitMonth = -1; mark_save_dirty(); } return; }
        var month = get_game_month_index(time);
        if (_socialMembershipMonth < 0) { _socialMembershipMonth = month; mark_save_dirty(); return; }
        if (month <= _socialMembershipMonth || _socialVisitMonth >= month || _npc_world is null) return;
        if (_npc_world.HasPendingSettlement || time.IsSmoothAdvanceBusy || action_transition?.IsPlaying == true || GetTree().Paused || _suppress_persistent_chrome) return;
        if (story_dialogue?.Visible == true || info?.Visible == true || trade_panel?.Visible == true || cultivation_ui?.Visible == true || _restPanel?.Visible == true || journal?.Visible == true || realm_panel?.Visible == true || condensation_panel?.Visible == true || storage?.Visible == true || baigong?.Visible == true || npc_interactions?.Visible == true || hud?.player_profile_visible == true) return;
        if (_player_world_node_id != "yuxu_xianzong_core" || string.IsNullOrWhiteSpace(_player_current_yuxu_peak_id) ||
            GetNodeOrNull<Control>("SceneLayer/YuxuXianzongOverview/PeakInterior")?.IsVisibleInTree() != true) return;
        var candidate = _npc_world.get_subscene_roster("yuxu_xianzong_core", _player_current_yuxu_peak_id)
            .Where(n => n.faction_id == YuxuSectId && !n.is_yuxu_lineage_staff && is_supported_yuxu_life_rank(n.formal_identity) && n.player_relationship >= 0 &&
                n.player_social_memory.GetValueOrDefault("last_month", -1).AsInt32() < month &&
                n.player_social_memory.GetValueOrDefault("relationship", "").AsString() != "enemy" &&
                NpcPersonalityRules.WillVisit(n, human.sect_current_rank is "真传" or "真传弟子"))
            .OrderBy(n => n.player_social_memory.GetValueOrDefault("last_month", -1).AsInt32())
            .ThenByDescending(n => NpcPersonalityRules.Is(n, "sociability", "外向"))
            .ThenByDescending(n => NpcSocialRules.KnowsName(n) && NpcPersonalityRules.Is(n, "attachment", "重情"))
            .ThenBy(n => n.player_social_memory.GetValueOrDefault("last_month", -1).AsInt32())
            .ThenBy(n => n.npc_id, StringComparer.Ordinal).FirstOrDefault();
        if (candidate is null) return;
        _socialVisitMonth = month;
        mark_save_dirty();
        begin_npc_social_talk(candidate, true);
    }
}
