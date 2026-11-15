using Godot;
using System;
using System.Linq;
using D = Godot.Collections.Dictionary;
using A = Godot.Collections.Array;

/// <summary>玩家主动交手；保留真实人物、实际伤亡和独立的随身物品账。</summary>
public partial class MainScene
{
    private bool is_npc_protected_from_hostility(WorldNpcData npc)
    {
        if (_baishiLecture is { } prologue && !prologue.finished &&
            (prologue.companion.npc_id == npc.npc_id || prologue.host_actor_id == npc.npc_id ||
             prologue.representatives.Any(n => n.npc_id == npc.npc_id))) return true;
        return npc.npc_id == "yuxu_gate_elder" || npc.is_yuxu_lineage_staff || player?.entity_data is HumanData human &&
            (npc.npc_id == human.sect_teacher_npc_id || npc.npc_id == human.sect_guide_npc_id);
    }
    private string npc_attack_unavailable_reason(WorldNpcData npc, bool pendingBattle = false)
    {
        var contact = social_contact_failure_reason(npc);
        if (contact.Length > 0) return contact;
        if (is_npc_in_seclusion_now(npc)) return "对方正在闭关，无法当面交手。";
        if (player?.entity_data is not HumanData human || player.current_health <= 0 || npc.current_health <= 0)
            return "当前无法交手。";
        if (current_location_forbids_fighting()) return "此地禁止私斗。";
        if (is_npc_protected_from_hostility(npc))
            return "对方承担接引或带教职责，暂不可攻击。";
        if (_npc_world?.get_npc(npc.npc_id) != npc) return "对方暂不参与江湖交手。";
        if (GetNodeOrNull<BattleTransitionManager>("/root/BattleTransitionManager") is not { } battle ||
            !pendingBattle && (battle.is_flow_transition_active() || battle.has_open_flow_view())) return "当前还有一场交手尚未结束。";
        return "";
    }

    public bool validate_pending_npc_attack(D context)
    {
        var target = _npc_world?.get_npc(context.GetValueOrDefault("npc_id", "").AsString());
        var reason = target is null ? "对方已离开。" : npc_attack_unavailable_reason(target, true);
        if (reason.Length == 0) return true;
        show_message("交手取消", reason);
        return false;
    }

    private void request_npc_attack(WorldNpcData npc)
    {
        var id = npc.npc_id;
        var reason = npc_attack_unavailable_reason(npc);
        if (reason.Length > 0) { show_message("暂不能攻击", reason); return; }
        info?.hide_panel();
        npc_interactions?.show_npc_page(npc, () => _npc_world?.get_npc(id), () =>
        {
            var current = _npc_world?.get_npc(id);
            var failure = current is null ? "对方已离开。" : npc_attack_unavailable_reason(current);
            return new NpcInteractionAction[] {
                new("attack.confirm", "确认攻击", () => start_npc_attack(id), failure.Length == 0, failure),
                new("attack.cancel", "收手", () => { if (_npc_world?.get_npc(id) is { } target) open_world_npc_actions(target); })
            };
        }, "你准备向对方动手。这场交手可能致人死亡；受伤、夺物和恩怨都会留下记录。确定要攻击吗？");
    }

    private void start_npc_attack(string npcId) => start_npc_hostile_battle(npcId, false, "");

    private bool start_npc_hostile_battle(string npcId, bool npcInitiated, string probeEventId)
    {
        var npc = _npc_world?.get_npc(npcId);
        if (npc is null) return false;
        var reason = npc_attack_unavailable_reason(npc);
        if (reason.Length > 0) { show_message("未能交手", reason); return false; }
        if (player?.entity_data is not HumanData human) return false;
        var transition = GetNodeOrNull<BattleTransitionManager>("/root/BattleTransitionManager");
        var scene = player.pack_for_battle();
        var snapshot = player.get_snapshot_data();
        var (opponentScene, opponentSnapshot) = create_npc_battle_package(npc, (int)WorldEntity.Faction.ENEMY);
        if (transition is null || scene is null || snapshot is null || opponentScene is null || opponentSnapshot is null)
        { show_message("未能交手", "战斗尚未准备就绪。"); return false; }
        var context = npc_interaction_return_context();
        context["kind"] = "player_npc_attack";
        context["title"] = (npcInitiated ? "窥探引起争斗 · " : "攻击 · ") + npc.npc_name;
        context["allow_prebattle_cancel"] = !npcInitiated;
        context["npc_initiated"] = npcInitiated;
        context["probe_event_id"] = probeEventId;
        context["player_health_before"] = player.current_health;
        context["npc_id"] = npcId;
        context["incident_id"] = Guid.NewGuid().ToString("N");
        context["npc_health_before"] = npc.current_health;
        context["player_name"] = player.entity_name;
        context["player_faction"] = human.sect_affiliation_id;
        context["target_faction"] = npc.faction_id;
        context["target_carried_items"] = npc.carried_items.Duplicate(true);
        context["location_name"] = get_current_profile_location();
        transition.set_battle_return_context(context);
        transition.start_team_battle(new Godot.Collections.Array<PackedScene> { scene },
            new Godot.Collections.Array<PackedScene> { opponentScene }, new A { snapshot }, new A { opponentSnapshot },
            GetTree().CurrentScene?.SceneFilePath ?? "res://tscn/scene/MainScene.tscn");
        return true;
    }

    private D npc_interaction_return_context() => new()
    {
        ["origin_node"] = _player_world_node_id,
        ["origin_peak"] = GetNodeOrNull<Control>("SceneLayer/YuxuXianzongOverview/PeakInterior")?.IsVisibleInTree() == true
            ? _player_current_yuxu_peak_id : "",
        ["origin_space"] = current_conflict_space(),
        ["origin_interior"] = location_interior?.Visible == true,
        ["origin_dungeon"] = _east_woods_dungeon?.Visible == true,
    };

    private void restore_npc_interaction_location(D context)
    {
        Callable.From(() =>
        {
            var origin = context.GetValueOrDefault("origin_node", _player_world_node_id).AsString();
            if (_player_world_node_id != origin) return;
            var peak = context.GetValueOrDefault("origin_peak", "").AsString();
            if (context.GetValueOrDefault("origin_dungeon", false).AsBool()) open_east_woods_dungeon();
            else if (origin == "yuxu_xianzong_core" && peak.Length > 0)
            {
                 _nodeWorldMap?.Hide(); location_interior?.Hide();
                open_faction_overview_if_available(origin);
                GetNodeOrNull<Node>("SceneLayer/YuxuXianzongOverview")?.Call("_on_peak_pressed", peak);
            }
            else if (context.GetValueOrDefault("origin_interior", false).AsBool())
            {
                on_node_enter_requested(origin);
                var space = context.GetValueOrDefault("origin_space", "").AsString();
                if (origin == BaishiNodeId && space.Length > 0 && _world_node_data?.get_node(origin) is { } node)
                {
                    location_interior?.show_baishi_space(node, _npc_world, space);
                    _baishiLectureSpaceOpen = space == "lecture";
                    if (_baishiLectureSpaceOpen) show_baishi_roster();
                }
            }
        }).CallDeferred();
    }

    private bool handle_player_npc_attack_return(A attackers, A defenders, D summary, D context)
    {
        if (context.GetValueOrDefault("kind", "").AsString() != "player_npc_attack") return false;
        if (_npc_world is null || player?.entity_data is not HumanData human) return true;
        var incidentId = context.GetValueOrDefault("incident_id", "").AsString();
        var battleId = summary.GetValueOrDefault("battle_id", "").AsString();
        if (incidentId.Length == 0 || battleId.Length == 0 || _npc_world.get_east_woods_incident(incidentId) is not null) return true;
        var npc = _npc_world.get_npc(context.GetValueOrDefault("npc_id", "").AsString());
        if (npc is null) return true;
        var result = attackers.Concat(defenders).Select(v => v.AsGodotObject()).OfType<BattleEntityData>()
            .FirstOrDefault(s => s.uid == "worldnpc:" + npc.npc_id);
        if (result is null) return true;
        apply_player_severe_injury(summary, context);
        award_successful_combat_inspiration(summary, context);
        var time = GetNode<GameTimeMgr>("/root/GameTimeManager");
        var month = get_game_month_index(time);
        var date = $"{time.year}年{time.month}月{time.day}日";
        var place = context.GetValueOrDefault("location_name", "当前地点").AsString();
        var origin = context.GetValueOrDefault("origin_node", _player_world_node_id).AsString();
        var escaped = summary.GetValueOrDefault("escaped", false).AsBool();
        var victory = !escaped && summary.GetValueOrDefault("player_victory", false).AsBool();
        var defeat = !escaped && summary.GetValueOrDefault("player_defeated", false).AsBool();
        var outcome = escaped ? "你已撤离，双方分开。" : summary.GetValueOrDefault("is_draw", false).AsBool()
            ? "双方俱伤，此战未分胜负。" : victory ? "你赢下了这场交手。" : defeat ? "你败下阵来。" : "交手结束，未分胜负。";
        var npcInitiated = context.GetValueOrDefault("npc_initiated", false).AsBool();
        _npc_world.apply_player_conflict_snapshot(result, place, incidentId, npcInitiated);
        var harmed = result.current_health < context.GetValueOrDefault("npc_health_before", result.current_health).AsInt32();
        if (harmed) CharacterRelationshipRules.RecordImmediateGrievance(npc, "player", "player-harm:" + incidentId, month,
            npcInitiated ? "在探查引起的交手中被玩家所伤。" : "遭到玩家主动攻击并受伤。");
        var playerResult = attackers.Concat(defenders).Select(v => v.AsGodotObject()).OfType<BattleEntityData>()
            .FirstOrDefault(s => s.uid == player.unique_id);
        if (npcInitiated && playerResult is not null && playerResult.current_health < context.GetValueOrDefault("player_health_before", playerResult.current_health).AsInt32())
            CharacterRelationshipRules.RecordImmediateGrievance(human, npc.npc_id, "npc-retaliation-harm:" + incidentId, month, "探查被发现后遭对方出手，身体受伤。");
        var incident = new EastWoodsNpcIncidentRecord
        {
            incident_id = incidentId, occurred_at = date, occurred_game_hour = time.get_total_game_hours(),
            location_id = origin, location_name = place, route_node_id = context.GetValueOrDefault("origin_space", "").AsString(),
            attacker_npc_id = npcInitiated ? npc.npc_id : "player",
            attacker_npc_name = npcInitiated ? npc.npc_name : context.GetValueOrDefault("player_name", player.entity_name).AsString(),
            attacker_faction_id = context.GetValueOrDefault(npcInitiated ? "target_faction" : "player_faction", "").AsString(),
            target_npc_id = npcInitiated ? "player" : npc.npc_id,
            target_npc_name = npcInitiated ? player.entity_name : npc.npc_name,
            target_faction_id = context.GetValueOrDefault(npcInitiated ? "player_faction" : "target_faction", "").AsString(),
            attack_trigger_id = npcInitiated ? "npc_retaliation_to_probe" : "player_initiated_attack", source_facts_complete = true,
            battle_result = victory ? npcInitiated ? EastWoodsNpcIncidentRecord.ResultTargetWon : EastWoodsNpcIncidentRecord.ResultAttackerWon
                : defeat && npc.is_alive ? npcInitiated ? EastWoodsNpcIncidentRecord.ResultAttackerWon : EastWoodsNpcIncidentRecord.ResultTargetWon
                : EastWoodsNpcIncidentRecord.ResultNoDecision,
            target_died = !npcInitiated && !npc.is_alive, attacker_died = npcInitiated && !npc.is_alive,
            // 玩家案件不交给仅处理NPC的自动门规/差事；保留事实，等相应裁断规则接入。
            case_status = EastWoodsNpcIncidentRecord.StatusPlayerConflictPendingReview,
            legal_status = YuxuSectDraftLawEvaluator.LegalStatusPending,
        };
        incident.evidence_source_refs.Add("battle:" + battleId);
        if (npcInitiated) incident.evidence_source_refs.Add("player-probe:" + context.GetValueOrDefault("probe_event_id", "").AsString());
        if (!npc.is_alive) incident.evidence_source_refs.Add("death:" + npc.npc_id);
        var loot = victory ? context.GetValueOrDefault("attack_loot_available", false).AsBool()
            ? "可收取的随身物品已列入战利品。" : "对方没有可收取的随身物品。" : "双方没有转移物品。";
        if (defeat && npc.is_alive)
        {
            var path = take_player_loot_item();
            if (path.Length > 0)
            {
                _npc_world.add_carried_item(npc, path);
                incident.item_transfers.Add(new EastWoodsNpcItemTransferRecord { item_path = path, quantity = 1,
                    from_npc_id = "player", from_npc_name = player.entity_name, to_npc_id = npc.npc_id, to_npc_name = npc.npc_name });
                incident.evidence_source_refs.Add("transfer:0");
                loot = $"对方取走了你随身的{ResourceLoader.Load<ItemData>(path)?.entity_name ?? "物品"} ×1。";
            }
        }
        _npc_world.record_east_woods_npc_incident(incident);
        npc.record(new NpcActionRecord { month_index = month, date_label = date, action_id = "player_attack_result",
            action_name = npcInitiated ? "因窥探出手" : "遭遇攻击", node_id = origin, subscene_id = incident.route_node_id, target_npc_id = "player",
            dungeon_incident_id = incidentId, reason = npcInitiated ? "玩家先行窥探，NPC发起实际交手。" : "玩家主动发起交手。", outcome = outcome + loot,
            is_death_record = !npc.is_alive });
        record_player_causal_event("player-attack:" + incidentId, npcInitiated ? "probe_retaliation" : "attack",
            npcInitiated ? "探查引发交手" : "主动攻击" + npc.npc_name, origin, place,
            npcInitiated ? "你先行窥探，" + npc.npc_name + "随后主动出手。" : "你向" + npc.npc_name + "主动发起交手。",
            outcome + (!npc.is_alive ? "对方伤重身亡。" : "对方仍然存活。"));
        finish_probe_retaliation(context, outcome);
        restore_npc_interaction_location(context);
        mark_save_dirty();
        // 有待领取物品时不再叠加第二张结果页。
        if (!npcInitiated && !has_active_item_claim()) show_message("交手结束", outcome + (!npc.is_alive ? "对方伤重身亡。" : "") + "\n" + loot + "\n这次冲突已留下记录，尚待裁断。");
        return true;
    }
}
