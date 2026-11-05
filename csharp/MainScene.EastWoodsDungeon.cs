using Godot;
using Godot.Collections;
using System;
using System.Linq;

public partial class MainScene
{
    private EastWoodsDungeonPanel? _east_woods_dungeon;
    private Godot.Collections.Dictionary _east_woods_dungeon_state = [];
    private long _eastWoodsTravelStartedHour = -1;
    private int _eastWoodsTravelCompletedSteps;

    private EastWoodsDungeonPanel get_or_create_east_woods_dungeon()
    {
        if (_east_woods_dungeon is not null) return _east_woods_dungeon;
        _east_woods_dungeon = new EastWoodsDungeonPanel { Name = "EastWoodsDungeonPanel" };
        GetNode<CanvasLayer>("SceneLayer").AddChild(_east_woods_dungeon);
        _east_woods_dungeon.Closed += () =>
        {
            if (_east_woods_dungeon?.is_dynamic_instance == true) return_from_dynamic_dungeon();
            else return_from_east_woods_dungeon();
        };
        _east_woods_dungeon.RouteNodeEntered += on_east_woods_route_node_entered;
        _east_woods_dungeon.RouteStepped += on_east_woods_route_stepped;
        _east_woods_dungeon.RouteTravelStarted += on_east_woods_route_travel_started;
        _east_woods_dungeon.RouteTravelFinished += on_east_woods_route_travel_finished;
        _east_woods_dungeon.ActionRequested += on_east_woods_action;
        _east_woods_dungeon.CompactRouteRequested += open_compact_fern_path;
        bind_forest_wolf_event(_east_woods_dungeon);
        _east_woods_dungeon.CanMove = () => GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager") is not null;
        return _east_woods_dungeon;
    }

    private void open_east_woods_dungeon()
    {
        estate?.Hide();
        location_interior?.hide_roster();
        location_interior?.Hide();
        _nodeWorldMap?.Hide();
        hide_world_character_list();

        refresh_yuxu_life_context();
        _npc_world?.enter_east_woods_route();
        mark_save_dirty();
        var dungeon = get_or_create_east_woods_dungeon();
        dungeon.use_east_woods_instance();
        var state = _east_woods_dungeon_state.Duplicate(true);
        if (!state.GetValueOrDefault("inside", false).AsBool()) state["current"] = "forest_edge";
        sync_east_woods_ecology_state(state, state.GetValueOrDefault("current", "forest_edge").AsString());
        dungeon.restore_route_state(state);
        dungeon.set_player_dun_speed(player?.dun_speed ?? EastWoodsDungeonRules.BaseDunSpeed);
        if (player?.entity_data is HumanData human)
            dungeon.set_active_duty_context(human.sect_active_yuxu_duty_id, human.sect_active_yuxu_duty_stage, human.sect_active_yuxu_duty_ready);
        var forestWindow = dungeon.open_forest_record();
        dungeon.show_dungeon();
        if (forestWindow.Count > 0 && player is not null) dungeon.show_forest_event(forestWindow, player.get_snapshot_data());
        _east_woods_dungeon_state = dungeon.get_route_state();
        refresh_east_woods_route_roster(dungeon.get_current_route_node_id());
    }

    private void return_from_east_woods_dungeon()
    {
        if (_east_woods_dungeon is not null) _east_woods_dungeon_state = _east_woods_dungeon.get_route_state();
        mark_save_dirty();
        (GetTree().GetFirstNodeInGroup("cell_character_list") as CellCharacterList)?.hide_world_npcs();
        open_world();
    }

    private void on_east_woods_route_stepped(string _fromNodeId, string toNodeId)
    {
        if (_east_woods_dungeon?.is_dynamic_instance == true)
        {
            on_dynamic_dungeon_route_stepped(toNodeId);
            return;
        }
        if (_east_woods_dungeon is not null) _east_woods_dungeon_state = _east_woods_dungeon.get_route_state();
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        var elapsedHours = EastWoodsDungeonRules.player_edge_hours(player?.dun_speed ?? EastWoodsDungeonRules.BaseDunSpeed);
        if (time is null)
        {
            show_message("行进暂停", "游戏历法尚未就绪；没有推进副本路线或 NPC 行程。");
            return;
        }

        time.advance_time(elapsedHours, "east_woods_dungeon");
        _eastWoodsTravelCompletedSteps++;
        update_yuxu_case_transport_dungeon_position(toNodeId, elapsedHours);
        var encounter = _npc_world?.advance_east_woods_route(_fromNodeId, toNodeId, elapsedHours, player?.entity_data as HumanData) ?? new Godot.Collections.Dictionary();
        mark_save_dirty();
        if (_east_woods_dungeon?.is_route_traveling() != true) refresh_east_woods_route_roster(toNodeId);
        if (encounter.Count > 0)
        {
            var targetsPlayer = encounter.GetValueOrDefault("target_player", false).AsBool();
            var attackerId = encounter.GetValueOrDefault("attacker_npc_id", "").AsString();
            if (targetsPlayer && try_mark_yuxu_case_transport_dungeon_interception(attackerId))
            {
                _east_woods_dungeon?.interrupt_travel();
                CallDeferred(nameof(start_yuxu_case_transport_interception_battle), attackerId, "east_woods_dungeon", _player_world_grid_cell);
            }
            else
            {
                _east_woods_dungeon?.interrupt_travel();
                CallDeferred(nameof(start_east_woods_encounter), encounter);
            }
        }
    }

    private void on_east_woods_route_travel_started()
    {
        if (_east_woods_dungeon?.is_dynamic_instance == true)
        {
            on_dynamic_dungeon_travel_started();
            return;
        }
        _eastWoodsTravelStartedHour = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
        _eastWoodsTravelCompletedSteps = 0;
        _npc_world?.begin_east_woods_route_signal_batch();
        (GetTree().GetFirstNodeInGroup("cell_character_list") as CellCharacterList)?.suspend_world_npcs();
    }

    private void on_east_woods_route_travel_finished(string routeNodeId)
    {
        if (_east_woods_dungeon?.is_dynamic_instance == true)
        {
            on_dynamic_dungeon_travel_finished(routeNodeId);
            return;
        }
        _npc_world?.end_east_woods_route_signal_batch();
        synchronize_east_woods_ecology();
        synchronize_dynamic_dungeon_opportunities();
        refresh_east_woods_route_roster(routeNodeId);
        if (_eastWoodsTravelCompletedSteps > 0)
        {
            var hour = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
            award_player_renewable_inspiration($"travel:eastwoods:{_eastWoodsTravelStartedHour}:{routeNodeId}:{hour}:{_eastWoodsTravelCompletedSteps}", "journey.step");
        }
        if (routeNodeId == "forest_edge" && player?.entity_data is HumanData human &&
            _npc_world?.get_active_yuxu_case_transport(human) is { journey_status: YuxuSectCaseTaskRecord.JourneyExitingDungeon, temporary_party_member_active: true } &&
            _east_woods_dungeon?.Visible == true)
        {
            _east_woods_dungeon.close();
            CallDeferred(nameof(start_yuxu_case_transport_world_route));
        }
        _eastWoodsTravelStartedHour = -1;
        _eastWoodsTravelCompletedSteps = 0;
    }

    public void finish_east_woods_spectator_battle()
    {
        _east_woods_dungeon?.set_npc_encounter_busy(false);
    }

    private void refresh_east_woods_route_roster(string routeNodeId)
    {
        var roster = GetTree().GetFirstNodeInGroup("cell_character_list") as CellCharacterList;
        if (roster is null) return;
        roster.show_world_npcs(
            _npc_world?.get_dungeon_node_roster(EastWoodsDungeonRules.DungeonId, routeNodeId) ?? new Godot.Collections.Array<WorldNpcData>(),
            $"interior:east_woods_dungeon:{routeNodeId}");
    }

    private void start_east_woods_encounter(Godot.Collections.Dictionary encounter)
    {
        if (player is null || _npc_world is null) return;
        var transition = GetNodeOrNull<BattleTransitionManager>("/root/BattleTransitionManager");
        if (transition is null) return;
        var attacker = _npc_world.get_npc(encounter.GetValueOrDefault("attacker_npc_id", "").AsString());
        var targetsPlayer = encounter.GetValueOrDefault("target_player", false).AsBool();
        var defender = targetsPlayer ? null : _npc_world.get_npc(encounter.GetValueOrDefault("target_npc_id", "").AsString());
        if (attacker is null || (!targetsPlayer && defender is null)) return;

        var firstFaction = targetsPlayer ? (int)WorldEntity.Faction.ENEMY : (int)WorldEntity.Faction.PLAYER;
        var (attackerScene, attackerSnapshot) = create_npc_battle_package(attacker, firstFaction);
        if (attackerScene is null || attackerSnapshot is null) return;

        PackedScene? defenderScene;
        BattleEntityData? defenderSnapshot;
        if (targetsPlayer)
        {
            defenderScene = player.pack_for_battle();
            defenderSnapshot = player.get_snapshot_data();
        }
        else
        {
            (defenderScene, defenderSnapshot) = create_npc_battle_package(defender!, (int)WorldEntity.Faction.ENEMY);
        }
        if (defenderScene is null || defenderSnapshot is null) return;

        var routeState = _east_woods_dungeon?.get_route_state() ?? _east_woods_dungeon_state;
        transition.set_battle_return_context(new Godot.Collections.Dictionary
        {
            ["kind"] = "east_woods_npc_encounter",
            ["target_player"] = targetsPlayer,
            ["attacker_npc_id"] = attacker.npc_id,
            ["target_npc_id"] = defender?.npc_id ?? "",
            ["incident_id"] = targetsPlayer ? "" : Guid.NewGuid().ToString("N"),
            ["route_node_id"] = encounter.GetValueOrDefault("route_node_id", attacker.dungeon_node_id).AsString(),
            ["attacker_previous_node_id"] = attacker.dungeon_previous_node_id,
            ["attacker_node_id"] = attacker.dungeon_node_id,
            ["target_previous_node_id"] = defender?.dungeon_previous_node_id ?? "",
            ["target_node_id"] = defender?.dungeon_node_id ?? "",
            ["attack_trigger_id"] = encounter.GetValueOrDefault("attack_trigger_id", "").AsString(),
            ["attack_authorization_id"] = encounter.GetValueOrDefault("attack_authorization_id", "").AsString(),
            ["route_state"] = routeState.Duplicate(true),
        });
        var attackerScenes = new Godot.Collections.Array<PackedScene> { attackerScene };
        var defenderScenes = new Godot.Collections.Array<PackedScene> { defenderScene };
        var attackerData = new Godot.Collections.Array { attackerSnapshot };
        var defenderData = new Godot.Collections.Array { defenderSnapshot };
        var worldPath = GetTree().CurrentScene?.SceneFilePath ?? "res://tscn/scene/MainScene.tscn";
        if (targetsPlayer)
        {
            transition.start_team_battle(attackerScenes, defenderScenes, attackerData, defenderData, worldPath);
        }
        else
        {
            _east_woods_dungeon?.set_npc_encounter_busy(true);
            if (!transition.start_spectator_team_battle(attackerScenes, defenderScenes, attackerData, defenderData, worldPath))
                _east_woods_dungeon?.set_npc_encounter_busy(false);
        }
    }

    private (PackedScene? Scene, BattleEntityData? Snapshot) create_npc_battle_package(WorldNpcData npc, int battleFaction)
    {
        var data = new HumanData
        {
            entity_name = npc.npc_name,
            entity_type = (int)WorldEntity.EntityType.CHARACTER,
            faction = battleFaction,
            unique_id = "worldnpc:" + npc.npc_id,
            appearance_seed = npc.appearance_seed,
            faction_name = npc.faction_id,
            gender = npc.gender,
            cultivation_realm = npc.cultivation_realm,
            spirit_root_name = npc.spirit_root_name,
            spirit_strands = npc.spirit_strands,
            current_age = npc.current_age,
            max_age = npc.max_age,
            base_physique = npc.physique,
            base_intelligence = npc.intelligence,
            base_soul = npc.soul,
            physique = npc.physique,
            intelligence = npc.intelligence,
            soul = npc.soul,
            current_health = npc.current_health,
            max_health = npc.max_health,
            current_stamina = 6,
            max_stamina = 6,
            current_mana = npc.cultivation_realm == "凡人" ? 0 : 3,
            max_mana = npc.cultivation_realm == "凡人" ? 0 : 3,
            basic_deck_path = "res://resources/basic_decks/公共基础技能池.tres",
            sect_affiliation_id = npc.faction_id,
            sect_current_rank = npc.formal_identity,
            dun_speed = npc.dun_speed,
        };
        NpcCombatBuildPlanner.ImportLearning(npc, data);
        foreach (var key in npc.carried_items.Keys)
        {
            var itemPath = key.AsString();
            var count = npc.get_carried_item_count(itemPath);
            if (count <= 0 || ResourceLoader.Load<ItemData>(itemPath) is null) continue;
            data.inventory_item_paths.Add(itemPath);
            data.inventory_item_counts.Add(count);
        }

        var actor = new Character { Name = "DungeonNpc_" + npc.npc_id };
        AddChild(actor);
        actor.load_from_data(data);
        actor.unique_id = data.unique_id;
        actor.entity_type = WorldEntity.EntityType.CHARACTER;
        actor.faction = battleFaction;
        actor.current_health = npc.current_health;
        actor.max_health = npc.max_health;
        actor.current_stamina = 6;
        actor.max_stamina = 6;
        actor.current_mana = data.current_mana;
        actor.max_mana = data.max_mana;
        if (data.learned_gongfa_paths.Count > 0 || data.unlocked_skill_node_ids.Count > 0)
        {
            var build = NpcCombatBuildPlanner.Equip(actor);
            npc.combat_main_gongfa_path = build.Main;
            npc.combat_sub_gongfa_paths = new Godot.Collections.Array<string>(build.Subs);
            npc.combat_chapter_routes = actor.entity_data is HumanData equipped ? equipped.equipped_gongfa_chapter_routes.Duplicate(true) : [];
            npc.combat_skill_paths = new Godot.Collections.Array<string>(build.Skills);
        }
        var scene = actor.pack_for_battle();
        var snapshot = actor.get_snapshot_data();
        RemoveChild(actor);
        actor.Free();
        return (scene, snapshot);
    }

    /// <summary>CombatTrigger 在既有主角战败处理之后回调；东林地 NPC 状态与掠夺在此一次性落账。</summary>
    public void handle_persistent_battle_return(Godot.Collections.Array attackers, Godot.Collections.Array defenders, Godot.Collections.Dictionary summary, Godot.Collections.Dictionary context)
    {
        if (handle_player_npc_attack_return(attackers, defenders, summary, context)) return;
        apply_player_severe_injury(summary, context);
        award_successful_combat_inspiration(summary, context);
        if (handle_world_encounter_battle_return(summary, context)) return;
        if (handle_yuxu_case_transport_battle_return(attackers, defenders, summary, context)) return;
        if (context.GetValueOrDefault("kind", "").AsString() == "dynamic_dungeon_beast")
        {
            restore_dynamic_dungeon_beast_result(summary, context);
            return;
        }
        if (context.GetValueOrDefault("kind", "").AsString() == "east_woods_beast")
        {
            if (context.ContainsKey("forest_event_key"))
            {
                var key = context["forest_event_key"].AsString();
                var wolf = ForestWolfEvent.Wolf(get_or_create_east_woods_dungeon().forest_record(key));
                foreach (var value in defenders)
                    if (value.AsGodotObject() is BattleEntityData result && wolf is not null && result.uid == wolf.uid)
                        context["forest_wolf_result"] = result;
            }
            restore_east_woods_beast_result(summary, context);
            return;
        }
        if (context.GetValueOrDefault("kind", "").AsString() != "east_woods_npc_encounter" || _npc_world is null) return;
        foreach (var value in attackers.Concat(defenders))
            if (value.AsGodotObject() is BattleEntityData snapshot) _npc_world.apply_dungeon_battle_snapshot(snapshot);

        var attackerNpc = _npc_world.get_npc(context.GetValueOrDefault("attacker_npc_id", "").AsString());
        var targetsPlayer = context.GetValueOrDefault("target_player", false).AsBool();
        var defenderNpc = targetsPlayer ? null : _npc_world.get_npc(context.GetValueOrDefault("target_npc_id", "").AsString());
        var attackersAlive = summary.GetValueOrDefault("attackers_alive", 0).AsInt32();
        var defendersAlive = summary.GetValueOrDefault("defenders_alive", 0).AsInt32();
        var attackerWon = attackersAlive > 0 && defendersAlive <= 0;
        var defenderWon = defendersAlive > 0 && attackersAlive <= 0;
        var lootSummary = "双方均未被击倒，未发生物品转移。";
        var incidentId = context.GetValueOrDefault("incident_id", "").AsString();
        var battleId = summary.GetValueOrDefault("battle_id", "").AsString();
        var harmEventId = "dungeon-harm:" + (string.IsNullOrWhiteSpace(incidentId) ? battleId : incidentId);
        if (harmEventId == "dungeon-harm:") harmEventId += Guid.NewGuid().ToString("N");
        var month = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager") is { } clock ? get_game_month_index(clock) : 0;
        var itemTransfers = new Godot.Collections.Array<EastWoodsNpcItemTransferRecord>();

        if (targetsPlayer && attackerNpc is not null)
        {
            if (attackerWon)
            {
                var itemPath = take_player_loot_item();
                if (!string.IsNullOrWhiteSpace(itemPath))
                {
                    _npc_world.add_carried_item(attackerNpc, itemPath);
                    lootSummary = $"对方从你的行囊中夺走 {ResourceLoader.Load<ItemData>(itemPath)?.entity_name ?? "一件物品"} ×1。";
                }
                else lootSummary = "对方未能从你的行囊中夺得可掠夺物品。";
                var relationshipFeedback = "";
                if (player?.entity_data is HumanData victimData)
                {
                    var previousResentment = CharacterRelationshipRules.Value(victimData, attackerNpc.npc_id, CharacterRelationshipRules.Resentment);
                    if (CharacterRelationshipRules.RecordImmediateGrievance(victimData, attackerNpc.npc_id,
                        harmEventId + ":player", month, "在东林地遭遇战中落败并遭受掠夺。"))
                    {
                        var currentResentment = CharacterRelationshipRules.Value(victimData, attackerNpc.npc_id, CharacterRelationshipRules.Resentment);
                        relationshipFeedback = "\n关系记事（即时）：你对" + attackerNpc.npc_name + "的" +
                            CharacterRelationshipRules.DescribeValueChange(CharacterRelationshipRules.Resentment, previousResentment, currentResentment) + "。";
                    }
                }
                lootSummary += relationshipFeedback;
                _npc_world.record_dungeon_battle_outcome(attackerNpc, "player", "战胜玩家并完成一次随身物品掠夺", lootSummary);
                show_message("东林地遇袭", $"战斗结束。主角依既有战败规则恢复至低生命状态；{lootSummary}");
            }
            else if (defenderWon)
            {
                lootSummary = context.GetValueOrDefault("loot_handled_by_settlement",false).AsBool()?"战利品已由战后结算记录收取，未收部分保留待领。":transfer_npc_loot_to_player(attackerNpc, !attackerNpc.is_alive);
                CharacterRelationshipRules.RecordImmediateGrievance(attackerNpc, "player", harmEventId + ":attacker", month, "在东林地遭遇战中被玩家击退。");
                _npc_world.record_dungeon_battle_outcome(attackerNpc, "player", attackerNpc.is_alive ? "被玩家击退" : "被玩家击杀", lootSummary);
                show_message("东林地遭遇", $"你赢下了这场战斗。{lootSummary}");
            }
            else show_message("东林地遭遇", summary.GetValueOrDefault("escaped",false).AsBool()?"你已成功撤离，双方分开；没有结算胜利奖励或掠夺。":"战斗提前结束，双方暂时分开；没有结算胜负或掠夺。");
        }
        else if (attackerNpc is not null && defenderNpc is not null)
        {
            if (attackerWon)
            {
                lootSummary = transfer_npc_loot_to_npc(attackerNpc, defenderNpc, !defenderNpc.is_alive, out itemTransfers);
                CharacterRelationshipRules.RecordImmediateGrievance(defenderNpc, attackerNpc.npc_id, harmEventId + ":defender", month, "在东林地遭遇战中败于对方并遭受掠夺。");
                _npc_world.record_dungeon_battle_outcome(defenderNpc, attackerNpc.npc_id, defenderNpc.is_alive ? "败于沿途冲突" : "在沿途冲突中身亡", lootSummary, incidentId);
                _npc_world.record_dungeon_battle_outcome(attackerNpc, defenderNpc.npc_id, "战胜同行", lootSummary, incidentId);
            }
            else if (defenderWon)
            {
                lootSummary = transfer_npc_loot_to_npc(defenderNpc, attackerNpc, !attackerNpc.is_alive, out itemTransfers);
                CharacterRelationshipRules.RecordImmediateGrievance(attackerNpc, defenderNpc.npc_id, harmEventId + ":attacker", month, "在东林地遭遇战中被对方击退。");
                _npc_world.record_dungeon_battle_outcome(attackerNpc, defenderNpc.npc_id, attackerNpc.is_alive ? "败于沿途冲突" : "在沿途冲突中身亡", lootSummary, incidentId);
                _npc_world.record_dungeon_battle_outcome(defenderNpc, attackerNpc.npc_id, "击退沿途袭击", lootSummary, incidentId);
            }
            else
            {
                lootSummary = "本场结束时未分出胜负，未发生物品转移。";
                _npc_world.record_dungeon_battle_outcome(attackerNpc, defenderNpc.npc_id, "冲突结束，未分胜负", lootSummary, incidentId);
                _npc_world.record_dungeon_battle_outcome(defenderNpc, attackerNpc.npc_id, "冲突结束，未分胜负", lootSummary, incidentId);
            }
            record_east_woods_npc_incident(context, attackerNpc, defenderNpc, attackerWon
                ? EastWoodsNpcIncidentRecord.ResultAttackerWon
                : defenderWon ? EastWoodsNpcIncidentRecord.ResultTargetWon : EastWoodsNpcIncidentRecord.ResultNoDecision,
                itemTransfers);
        }

        _east_woods_dungeon_state = context.GetValueOrDefault("route_state", new Godot.Collections.Dictionary()).AsGodotDictionary();
        var dungeon = get_or_create_east_woods_dungeon();
        dungeon.restore_route_state(_east_woods_dungeon_state);
        dungeon.set_player_dun_speed(player?.dun_speed ?? EastWoodsDungeonRules.BaseDunSpeed);
        dungeon.show_dungeon();
        dungeon.set_npc_encounter_busy(false);
        if (player?.entity_data is HumanData human) dungeon.set_active_duty_context(human.sect_active_yuxu_duty_id, human.sect_active_yuxu_duty_stage, human.sect_active_yuxu_duty_ready);
        refresh_east_woods_route_roster(dungeon.get_current_route_node_id());
        mark_save_dirty();
    }

    private string transfer_npc_loot_to_player(WorldNpcData loser, bool takeAll)
    {
        if (_npc_world is null || player is null) return "没有物品转移。";
        var transferred = new List<string>();
        if (takeAll)
        {
            var paths = loser.carried_items.Keys.Select(key => key.AsString()).ToArray();
            foreach (var path in paths)
            {
                var count = loser.get_carried_item_count(path);
                if (count > 0 && ResourceLoader.Load<ItemData>(path) is not null && player.add_item_to_inventory(path, count))
                {
                    loser.change_carried_item(path, -count);
                    transferred.Add($"{ResourceLoader.Load<ItemData>(path)?.entity_name ?? "物品"} ×{count}");
                }
            }
        }
        else
        {
            var path = _npc_world.remove_one_carried_item(loser);
            if (!string.IsNullOrWhiteSpace(path))
            {
                if (player.add_item_to_inventory(path)) transferred.Add($"{ResourceLoader.Load<ItemData>(path)?.entity_name ?? "物品"} ×1");
                else _npc_world.add_carried_item(loser, path);
            }
        }
        return transferred.Count > 0 ? "掠得 " + string.Join("、", transferred) + "。" : "对方没有可转移的随身物品，或你的行囊已满。";
    }

    private string transfer_npc_loot_to_npc(
        WorldNpcData winner,
        WorldNpcData loser,
        bool takeAll,
        out Godot.Collections.Array<EastWoodsNpcItemTransferRecord> itemTransfers)
    {
        itemTransfers = [];
        if (_npc_world is null) return "没有物品转移。";
        if (takeAll)
        {
            itemTransfers = _npc_world.transfer_all_carried_items_with_records(loser, winner);
            var count = itemTransfers.Sum(transfer => transfer.quantity);
            return count > 0 ? $"掠得随身物品共 {count} 件。" : "败者没有可掠夺的随身物品。";
        }
        var path = _npc_world.remove_one_carried_item(loser);
        if (string.IsNullOrWhiteSpace(path)) return "败者没有可掠夺的随身物品。";
        _npc_world.add_carried_item(winner, path);
        itemTransfers.Add(new EastWoodsNpcItemTransferRecord
        {
            item_path = path,
            quantity = 1,
            from_npc_id = loser.npc_id,
            from_npc_name = loser.npc_name,
            to_npc_id = winner.npc_id,
            to_npc_name = winner.npc_name,
        });
        return $"掠得 {ResourceLoader.Load<ItemData>(path)?.entity_name ?? "一件物品"} ×1。";
    }

    private void record_east_woods_npc_incident(
        Godot.Collections.Dictionary context,
        WorldNpcData attacker,
        WorldNpcData target,
        string result,
        Godot.Collections.Array<EastWoodsNpcItemTransferRecord> itemTransfers)
    {
        if (_npc_world is null) return;
        var incidentId = context.GetValueOrDefault("incident_id", "").AsString();
        if (string.IsNullOrWhiteSpace(incidentId)) incidentId = Guid.NewGuid().ToString("N");
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        var incident = new EastWoodsNpcIncidentRecord
        {
            incident_id = incidentId,
            occurred_at = time is null ? get_yuxu_contribution_timestamp() : $"{time.year}年{time.month}月{time.day}日",
            occurred_game_hour = time?.get_total_game_hours() ?? 0,
            location_id = EastWoodsDungeonRules.DungeonId,
            location_name = "东林地·深入林地",
            route_node_id = context.GetValueOrDefault("route_node_id", attacker.dungeon_node_id).AsString(),
            attacker_previous_node_id = context.GetValueOrDefault("attacker_previous_node_id", attacker.dungeon_previous_node_id).AsString(),
            attacker_node_id = context.GetValueOrDefault("attacker_node_id", attacker.dungeon_node_id).AsString(),
            target_previous_node_id = context.GetValueOrDefault("target_previous_node_id", target.dungeon_previous_node_id).AsString(),
            target_node_id = context.GetValueOrDefault("target_node_id", target.dungeon_node_id).AsString(),
            attacker_npc_id = attacker.npc_id,
            attacker_npc_name = attacker.npc_name,
            attacker_faction_id = attacker.faction_id,
            target_npc_id = target.npc_id,
            target_npc_name = target.npc_name,
            target_faction_id = target.faction_id,
            attack_trigger_id = context.GetValueOrDefault("attack_trigger_id", "").AsString(),
            attack_authorization_id = context.GetValueOrDefault("attack_authorization_id", "").AsString(),
            source_facts_complete = true,
            battle_result = result,
            attacker_died = !attacker.is_alive,
            target_died = !target.is_alive,
            case_status = EastWoodsNpcIncidentRecord.StatusAvailable,
        };
        incident.evidence_source_refs.Add($"incident:{incidentId}:battle_result");
        if (incident.attacker_died) incident.evidence_source_refs.Add($"incident:{incidentId}:death:{attacker.npc_id}");
        if (incident.target_died) incident.evidence_source_refs.Add($"incident:{incidentId}:death:{target.npc_id}");
        foreach (var transfer in itemTransfers)
            if (transfer is not null && transfer.quantity > 0 && !string.IsNullOrWhiteSpace(transfer.item_path))
            {
                incident.item_transfers.Add(transfer.Duplicate(true) as EastWoodsNpcItemTransferRecord ?? transfer);
                incident.evidence_source_refs.Add($"incident:{incidentId}:transfer:{incident.item_transfers.Count - 1}");
            }
        _npc_world.record_east_woods_npc_incident(incident);
    }

    private string take_player_loot_item()
    {
        if (player is null || _npc_world is null) return "";
        var candidates = player.pack_items
            .Where(path => player.get_inventory_total_count(path) > 0 && ResourceLoader.Load<ItemData>(path) is not null)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        if (candidates.Length == 0) return "";
        var hour = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
        var start = (int)(EastWoodsDungeonRules.stable_seed($"player-loot|{hour}|{candidates.Length}") % (ulong)candidates.Length);
        for (var offset = 0; offset < candidates.Length; offset++)
        {
            var selected = candidates[(start + offset) % candidates.Length];
            if (player.remove_item_from_inventory(selected, 1)) return selected;
        }
        return "";
    }
}
