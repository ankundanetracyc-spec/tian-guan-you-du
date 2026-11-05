using Godot;
using System;
using System.Linq;

public partial class MainScene
{
    private int _socialMembershipMonth = -1, _socialVisitMonth = -1;
    // Scene progress persists this month-scoped idempotency cursor; each NPC also carries
    // a per-NPC month stamp so older saves can recover an already processed visitor.
    private int _socialProcessedVisitMonth = -1;
    private readonly HashSet<string> _socialVisitedNpcIds = new(StringComparer.Ordinal);
    private double _socialVisitPoll;
    private void sync_social_membership_month()
    {
        if (player?.entity_data is not HumanData human || GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager") is not { } time) return;
        if (human.sect_affiliation_id == YuxuSectId && _socialMembershipMonth < 0) { _socialMembershipMonth = get_game_month_index(time); mark_save_dirty(); }
        else if (human.sect_affiliation_id != YuxuSectId && _socialMembershipMonth >= 0) { _socialMembershipMonth = _socialVisitMonth = -1; mark_save_dirty(); }
    }
    private bool can_contact_social_npc(WorldNpcData npc)
    {
        return string.IsNullOrEmpty(social_contact_failure_reason(npc));
    }
    private string social_contact_failure_reason(WorldNpcData? npc)
    {
        if (npc is null || !GodotObject.IsInstanceValid(npc)) return "对方已不在场，人物名单可能刚刚更新。";
        if (!npc.is_alive) return "对方已不在场。";
        if (baishi_present_actor(npc.npc_id) is not null) return "";
        if (player is null) return "当前没有可以进行交互的角色。";
        if (_nodeWorldMap?.IsVisibleInTree() == true && _nodeWorldMap.HasMethod("can_contact_map_npc"))
        {
            if (!string.IsNullOrWhiteSpace(npc.dungeon_id)) return "对方正在其他历练之中。";
            return get_free_world_roster().Any(n => n.npc_id == npc.npc_id) ? "" : "对方距离较远，请先靠近。";
        }
        if (npc.is_traveling) return "对方正在旅途中，抵达后才能交谈。";
        if (_east_woods_dungeon?.Visible == true)
            return npc.dungeon_id == EastWoodsDungeonRules.DungeonId && npc.dungeon_node_id == _east_woods_dungeon.get_current_route_node_id()
                ? ""
                : "对方不在你所在的副本区域。";
        if (!string.IsNullOrWhiteSpace(npc.dungeon_id)) return "对方正在其他历练之中。";
        if (_nodeWorldMap?.Visible == true)
            return (_npc_world?.get_world_grid_roster(_player_world_grid_cell, _player_world_node_id).Any(n => n.npc_id == npc.npc_id)).GetValueOrDefault()
                ? ""
                : "对方已离开当前世界节点。";
        if (npc.current_node_id != _player_world_node_id) return "对方不在当前地点。";
        if (location_interior?.Visible == true)
            return location_interior.is_npc_in_current_space(npc) ? "" : "对方不在当前室内空间。";
        if (GetNodeOrNull<Control>("SceneLayer/YuxuXianzongOverview/PeakInterior")?.IsVisibleInTree() == true)
            return npc.current_subscene_id == _player_current_yuxu_peak_id ? "" : "对方不在当前峰内区域。";
        return string.IsNullOrWhiteSpace(npc.current_subscene_id) ? "" : "对方不在当前室内空间。";
    }
    private void begin_npc_social_talk(WorldNpcData npc, bool proactive, bool testMode = false)
    {
        if (player?.entity_data is not HumanData human)
        {
            if (!testMode) show_message("暂不能交谈", "当前没有可进行交互的角色。");
            return;
        }
        if (story_dialogue is null)
        {
            if (!testMode) show_message("暂不能交谈", "交谈界面尚未准备就绪，请稍后再试。");
            return;
        }
        var contactFailure = social_contact_failure_reason(npc);
        if (contactFailure.Length > 0)
        {
            npc_interactions?.close();
            if (!testMode) show_message(npc?.npc_name ?? "暂不能交谈", contactFailure);
            return;
        }
        var nowHour = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
        if (npc.is_in_seclusion_at_hour(nowHour)) { explain_npc_seclusion(npc); return; }
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
            var responseHour = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
            if (!testMode && npc.is_in_seclusion_at_hour(responseHour))
            {
                story_dialogue.show_social_result(npc, new("对方已入静室闭关，本次未能交谈。", "静室门已闭，暂不便打扰。", []));
                return;
            }
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
        // proactive describes an NPC-initiated visit, not the player's approach.
        var knowsName = NpcSocialRules.KnowsName(socialNpc);
        var confirm = proactive ? testMode ? "听听来意" : "接受" : knowsName && !testMode ? "聊聊近况" : "上前搭话";
        var opening = testMode
            ? proactive ? "对方向你走来，停步问候。" : "你准备上前与对方搭话。"
            : NpcSocialRules.Opening(socialNpc, human, place, proactive);
        story_dialogue.show_social_choices(player, socialNpc, opening,
            new (string, Action)[] {
                (testMode ? confirm : $"{confirm} · 精力 {cost:0.#}", () => Answer("introduce")),
                (proactive ? "婉拒" : "作罢", () => Answer("leave"))
            }, () => Answer("leave"), proactive ? "来访" : knowsName ? "闲谈" : "结识", proactive ? "右键婉拒" : "右键作罢", testMode);
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
        if (month <= _socialMembershipMonth || _npc_world is null || social_visit_count(month) >= social_visit_limit(human)) return;
        if (_npc_world.HasPendingSettlement || time.IsSmoothAdvanceBusy || action_transition?.IsPlaying == true || GetTree().Paused || _suppress_persistent_chrome) return;
        if (story_dialogue?.Visible == true || info?.Visible == true || trade_panel?.Visible == true || cultivation_ui?.Visible == true || _restPanel?.Visible == true || journal?.Visible == true || realm_panel?.Visible == true || condensation_panel?.Visible == true || storage?.Visible == true || baigong?.Visible == true || _letters?.Visible == true || npc_interactions?.Visible == true || hud?.player_profile_visible == true) return;
        if (_player_world_node_id != "yuxu_xianzong_core" || string.IsNullOrWhiteSpace(_player_current_yuxu_peak_id) ||
            GetNodeOrNull<Control>("SceneLayer/YuxuXianzongOverview/PeakInterior")?.IsVisibleInTree() != true) return;
        var candidate = social_visit_candidate(human, month, _player_current_yuxu_peak_id);
        if (candidate is null) return;
        mark_social_visit_processed(candidate, month);
        if (can_npc_purchase(candidate)) request_npc_purchase(candidate, true);
        else if (can_sect_spar(candidate) && NpcSparringRules.WantsToInvite(candidate, human)) request_npc_spar(candidate, true);
        else begin_npc_social_talk(candidate, true);
    }
    private int social_visit_limit(HumanData human)
    {
        var renown = RegionalRenownSystem.get_bai_chuan_total(human);
        return renown >= 200 ? 3 : renown >= 60 ? 2 : 1;
    }

    private int social_visit_count(int month)
    {
        sync_social_visit_processed_month(month);
        var count = _socialVisitedNpcIds.Count;
        // Older saves only had one scene-level month marker. Count that legacy visit once
        // until the first per-NPC stamp is present; new visits are recorded on the NPC.
        if (count == 0 && _socialVisitMonth == month) return 1;
        return count;
    }

    private void sync_social_visit_processed_month(int month)
    {
        if (_socialProcessedVisitMonth != month)
        {
            _socialProcessedVisitMonth = month;
            _socialVisitedNpcIds.Clear();
        }
        if (_npc_world is null) return;
        foreach (var npc in _npc_world.npcs)
            if (npc is not null && npc.last_player_visit_month == month && !string.IsNullOrWhiteSpace(npc.npc_id))
                _socialVisitedNpcIds.Add(npc.npc_id);
    }

    private void mark_social_visit_processed(WorldNpcData npc, int month)
    {
        sync_social_visit_processed_month(month);
        if (!string.IsNullOrWhiteSpace(npc.npc_id)) _socialVisitedNpcIds.Add(npc.npc_id);
        npc.last_player_visit_month = month;
        _socialVisitMonth = month;
        mark_save_dirty();
    }

    private WorldNpcData? social_visit_candidate(HumanData human, int month, string peakId)
    {
        if (_npc_world is null) return null;
        var now = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? month * 30 * 24;
        var publiclyKnown = human.sect_current_rank is "真传" or "真传弟子";
        var candidates = _npc_world.get_subscene_roster("yuxu_xianzong_core", peakId)
            .Where(n => n.faction_id == YuxuSectId && !n.is_yuxu_lineage_staff && is_supported_yuxu_life_rank(n.formal_identity) &&
                n.last_player_visit_month != month && !_socialVisitedNpcIds.Contains(n.npc_id) &&
                n.player_social_memory.GetValueOrDefault("last_month", -1).AsInt32() < month &&
                !n.is_in_seclusion_at_hour(now) &&
                !CharacterRelationshipRules.AvoidsContact(n, CharacterRelationshipRules.PlayerCharacterId) &&
                NpcPersonalityRules.WillVisit(n, publiclyKnown))
            .OrderBy(n => n.player_social_memory.GetValueOrDefault("last_month", -1).AsInt32())
            .ThenByDescending(n => NpcPersonalityRules.Is(n, "sociability", "外向"))
            .ThenByDescending(n => NpcSocialRules.KnowsName(n) && NpcPersonalityRules.Is(n, "attachment", "重情"))
            .ThenByDescending(n => NpcPersonalityRules.HasVisitPriority(n, publiclyKnown))
            .ThenByDescending(n => NpcPlayerImpressionRules.WillingContact(n))
            .ThenBy(n => n.npc_id, StringComparer.Ordinal).ToList();
        if (candidates.Count == 0) return null;

        // 保留既有轮转次序作为基础权重；五年结识窗口按方案将对应候选权重乘 1.5。
        var weights = candidates.Select((npc, index) =>
        {
            var baseWeight = 1.0 / (index + 1.0);
            return baseWeight * (npc.acquaintance_window_until_hour > now ? 1.5 : 1.0);
        }).ToArray();
        var totalWeight = weights.Sum();
        var seedText = $"social-visit:{month}:{social_visit_count(month)}:{_player_world_node_id}:{peakId}";
        ulong seed = 14695981039346656037UL;
        foreach (var ch in seedText) seed = (seed ^ ch) * 1099511628211UL;
        var rng = new RandomNumberGenerator { Seed = seed };
        var pick = rng.Randf() * (float)totalWeight;
        for (var i = 0; i < candidates.Count; i++)
        {
            pick -= (float)weights[i];
            if (pick <= 0) return candidates[i];
        }
        return candidates[^1];
    }
}
