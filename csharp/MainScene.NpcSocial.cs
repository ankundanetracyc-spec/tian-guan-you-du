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
    private void begin_npc_social_talk(WorldNpcData npc, bool proactive, bool testMode = false)
    {
        if (player?.entity_data is not HumanData human || story_dialogue is null || !can_contact_social_npc(npc)) return;
        info?.hide_panel(); npc_interactions?.close();
        var socialNpc = testMode ? npc.Duplicate(true) as WorldNpcData ?? npc : npc;
        // 调试模拟只读取当前人物，不记录身份传闻，也不改 NPC 的六轴或交往记忆。
        if (!testMode) NpcSocialRules.HearSectReputation(npc, human, proactive);
        var place = get_current_profile_location();
        var eventId = Guid.NewGuid().ToString("N");
        var answered = false;
        void Answer(string choice)
        {
            if (answered) return;
            answered = true;
            if (!can_contact_social_npc(npc)) { story_dialogue.show_social_result(npc, new("对方已离开。", "还有些事，先走一步。", [])); return; }
            if (testMode)
            {
                var simulation = NpcSocialPrototypeRules.Evaluate(socialNpc, human, proactive, choice != "leave");
                story_dialogue.show_social_result(socialNpc, simulation);
                return;
            }
            if (choice != "leave" && !player.can_spend_world_stamina(WorldStaminaRules.ConversationCost))
            {
                story_dialogue.show_social_result(npc, new("你有些疲惫，便先告辞了。", "那便改日再聊。", [])
                    { RelationshipFeedback = "亲近：本次未能聊成，没有新增关系记事。" });
                return;
            }
            var response = NpcSocialRules.Evaluate(npc, human, choice, proactive);
            if (choice != "leave") { player.try_spend_world_stamina(WorldStaminaRules.ConversationCost); response = response with { StaminaSpent = WorldStaminaRules.ConversationCost, ActionName = NpcSocialRules.KnowsName(npc) ? "闲谈" : "结识" }; }
            var month = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager") is { } time ? get_game_month_index(time) : 0;
            // 在登记本次经历前选旧事，防止刚聊上便把本次说成“上回”。
            response = NpcPersonalRecallRules.Attach(npc, response, month);
            response = NpcSocialRules.Remember(npc, human, choice, place, month, response, eventId);
            mark_save_dirty();
            story_dialogue.show_social_result(npc, response);
        }
        var cost = WorldStaminaRules.ConversationCost;
        var opening = testMode
            ? proactive ? "对方向你走来，停步问候。" : "你准备上前与对方搭话。"
            : NpcSocialRules.Opening(socialNpc, human, place, proactive);
        story_dialogue.show_social_choices(player, socialNpc, opening,
            new (string, Action)[] {
                (testMode ? proactive ? "听听来意" : "上前搭话" : $"接受 · 体力 {cost:0.#}", () => Answer("introduce")),
                (testMode && !proactive ? "作罢" : "婉拒", () => Answer("leave"))
            }, () => Answer("leave"), proactive ? "来访" : "结识", proactive ? "右键婉拒" : "右键作罢", testMode);
    }
    private void process_npc_social_visit(double delta)
    {
        _socialVisitPoll += delta;
        if (_socialVisitPoll < .5) return;
        _socialVisitPoll = 0;
        if (player?.entity_data is not HumanData human || GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager") is not { } time) return;
        if (_letterMailbox.closure_pending)
        {
            if (_npc_world?.HasPendingSettlement == true || time.IsSmoothAdvanceBusy || action_transition?.IsPlaying == true || GetTree().Paused) return;
            process_closed_visit_letters(human, time);
        }
        if (human.sect_affiliation_id != YuxuSectId) { if (_socialMembershipMonth >= 0) { _socialMembershipMonth = _socialVisitMonth = -1; mark_save_dirty(); } return; }
        var month = get_game_month_index(time);
        if (_socialMembershipMonth < 0) { _socialMembershipMonth = month; mark_save_dirty(); return; }
        if (month <= _socialMembershipMonth || _socialVisitMonth >= month || _npc_world is null) return;
        if (_npc_world.HasPendingSettlement || time.IsSmoothAdvanceBusy || action_transition?.IsPlaying == true || GetTree().Paused || _suppress_persistent_chrome) return;
        if (story_dialogue?.Visible == true || info?.Visible == true || trade_panel?.Visible == true || cultivation_ui?.Visible == true || _restPanel?.Visible == true || journal?.Visible == true || realm_panel?.Visible == true || condensation_panel?.Visible == true || storage?.Visible == true || baigong?.Visible == true || _letters?.Visible == true || npc_interactions?.Visible == true || hud?.player_profile_visible == true) return;
        if (_player_world_node_id != "yuxu_xianzong_core" || string.IsNullOrWhiteSpace(_player_current_yuxu_peak_id) ||
            GetNodeOrNull<Control>("SceneLayer/YuxuXianzongOverview/PeakInterior")?.IsVisibleInTree() != true) return;
        var candidate = social_visit_candidate(human, month, _player_current_yuxu_peak_id);
        if (candidate is null) return;
        _socialVisitMonth = month;
        mark_save_dirty();
        if (can_npc_purchase(candidate)) request_npc_purchase(candidate, true);
        else if (can_sect_spar(candidate) && NpcSparringRules.WantsToInvite(candidate, human)) request_npc_spar(candidate, true);
        else begin_npc_social_talk(candidate, true);
    }
    private WorldNpcData? social_visit_candidate(HumanData human, int month, string peakId) =>
        _npc_world?.get_subscene_roster("yuxu_xianzong_core", peakId)
            .Where(n => n.faction_id == YuxuSectId && !n.is_yuxu_lineage_staff && is_supported_yuxu_life_rank(n.formal_identity) &&
                !CharacterRelationshipRules.AvoidsContact(n, CharacterRelationshipRules.PlayerCharacterId) &&
                n.player_social_memory.GetValueOrDefault("last_month", -1).AsInt32() < month &&
                NpcPersonalityRules.WillVisit(n, human.sect_current_rank is "真传" or "真传弟子"))
            .OrderBy(n => n.player_social_memory.GetValueOrDefault("last_month", -1).AsInt32())
            .ThenByDescending(n => NpcPersonalityRules.Is(n, "sociability", "外向"))
            .ThenByDescending(n => NpcSocialRules.KnowsName(n) && NpcPersonalityRules.Is(n, "attachment", "重情"))
            .ThenByDescending(n => NpcPersonalityRules.HasVisitPriority(n, human.sect_current_rank is "真传" or "真传弟子"))
            .ThenByDescending(n => NpcPlayerImpressionRules.WillingContact(n))
            .ThenBy(n => n.npc_id, StringComparer.Ordinal).FirstOrDefault();
}
