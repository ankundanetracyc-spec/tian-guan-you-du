using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Lifecycle and play-loop integration for limited-time world dungeon opportunities.</summary>
public partial class MainScene
{
    private long _dynamicDungeonTravelStartedHour = -1;
    private int _dynamicDungeonTravelCompletedSteps;

    private int current_game_hour() => GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;

    private void synchronize_dynamic_dungeon_opportunities()
    {
        if (_dynamic_dungeon_state.Count == 0)
            _dynamic_dungeon_state = DynamicDungeonExpedition.CreateEmpty(current_game_hour());

        if (_east_woods_dungeon?.Visible == true && _east_woods_dungeon.is_dynamic_instance)
            store_active_dynamic_dungeon_route();

        var changed = DynamicDungeonExpedition.SynchronizeTowerOpportunities(_dynamic_dungeon_state, current_game_hour());
        synchronize_dynamic_dungeon_world_nodes();

        if (_east_woods_dungeon?.Visible == true && _east_woods_dungeon.is_dynamic_instance &&
            DynamicDungeonExpedition.FindInstance(_dynamic_dungeon_state, _east_woods_dungeon.dynamic_instance_id) is { } current)
            _east_woods_dungeon.update_dynamic_instance(current);

        if (changed) mark_save_dirty();
    }

    private void on_dynamic_dungeon_enter_requested(string instanceId)
    {
        var instance = DynamicDungeonExpedition.FindInstance(_dynamic_dungeon_state, instanceId);
        if (instance is null || DynamicDungeonExpedition.IsCompactInstance(instance) ||
            !DynamicDungeonExpedition.IsEntryOpen(instance, current_game_hour())) return;
        if (_player_world_node_id != DynamicDungeonExpedition.GetWorldNodeId(instance)) return;
        location_interior?.Hide();
        _nodeWorldMap?.QueueFree();
        _nodeWorldMap = null;
        open_dynamic_dungeon(instanceId);
        mark_save_dirty();
    }

    private void open_dynamic_dungeon(string instanceId)
    {
        if (DynamicDungeonExpedition.FindInstance(_dynamic_dungeon_state, instanceId) is not { } instance) return;
        var isAlreadyInside = instance.GetValueOrDefault("inside", false).AsBool();
        if (!isAlreadyInside && !DynamicDungeonExpedition.IsEntryOpen(instance, current_game_hour())) return;


        location_interior?.hide_roster();
        location_interior?.Hide();
        _nodeWorldMap?.Hide();
        hide_world_character_list();

        refresh_yuxu_life_context();

        var dungeon = get_or_create_east_woods_dungeon();
        var route = DynamicDungeonExpedition.GetRoute(instance);
        route["inside"] = true;
        DynamicDungeonExpedition.StoreRoute(instance, route);
        DynamicDungeonExpedition.SynchronizeInstanceEcology(instance, current_game_hour());
        dungeon.configure_dynamic_instance(instance);
        dungeon.set_player_dun_speed(player?.dun_speed ?? EastWoodsDungeonRules.BaseDunSpeed);
        dungeon.set_active_duty_context("", 0, false);
        dungeon.show_dungeon();
        store_active_dynamic_dungeon_route();
    }

    private void return_from_dynamic_dungeon()
    {
        store_active_dynamic_dungeon_route();
        var instance = _east_woods_dungeon?.is_dynamic_instance == true
            ? DynamicDungeonExpedition.FindInstance(_dynamic_dungeon_state, _east_woods_dungeon.dynamic_instance_id)
            : null;
        if (instance is not null && DynamicDungeonExpedition.IsExitNode(instance,
                _east_woods_dungeon?.get_current_route_node_id() ?? ""))
        {
            instance["completed"] = true;
            instance["entry_open"] = false;
            if (_world_node_data?.get_node(DynamicDungeonExpedition.GetWorldNodeId(instance)) is { } node)
                node.enterable = false;
        }
        (GetTree().GetFirstNodeInGroup("cell_character_list") as CellCharacterList)?.hide_world_npcs();
        synchronize_dynamic_dungeon_opportunities();
        mark_save_dirty();
        open_world();
    }

    private void synchronize_dynamic_dungeon_world_nodes()
    {
        if (_world_node_data is null) return;
        _world_node_data.active_ephemeral_nodes.Clear();
        var savedCurrent = _nodeWorldState.GetValueOrDefault("current_node", "").AsString();
        foreach (var value in _dynamic_dungeon_state.GetValueOrDefault("instances", new Godot.Collections.Array()).AsGodotArray())
        {
            if (value.VariantType != Variant.Type.Dictionary) continue;
            var instance = value.AsGodotDictionary();
            if (DynamicDungeonExpedition.IsCompactInstance(instance)) continue;
            var nodeId = DynamicDungeonExpedition.GetWorldNodeId(instance);
            var isActive = DynamicDungeonExpedition.IsEntryOpen(instance, current_game_hour()) &&
                !instance.GetValueOrDefault("completed", false).AsBool();
            var isCurrent = savedCurrent == nodeId || _player_world_node_id == nodeId;
            if (!isActive && !isCurrent) continue;
            var worldNode = new WorldNodeData
            {
                node_id = nodeId,
                node_name = DynamicDungeonExpedition.GetDisplayName(instance),
                node_type = WorldNodeData.NodeType.Dungeon,
                display_level = WorldNodeData.DisplayLevel.Ephemeral,
                territory_id = "",
                enterable = isActive,
                unlocked = true,
                map_position = instance.GetValueOrDefault("world_position", Vector2.Zero).AsVector2(),
                terrain = "山野秘境",
                spirit_pressure = 2,
                danger_level = 2,
                description = isActive ? "大世界中偶然现身的一处小型秘境。抵达后可查看预计境界，再决定是否进入。" : "此处的秘境已探索完毕。",
            };
            _world_node_data.active_ephemeral_nodes.Add(worldNode);
        }
    }

    private void sync_dynamic_dungeon_map_markers()
    {
        if (_nodeWorldMap?.HasMethod("add_dynamic_dungeon_marker") != true) return;
        synchronize_dynamic_dungeon_world_nodes();
        var savedCurrent = _nodeWorldState.GetValueOrDefault("current_node", "").AsString();
        var changed = false;
        foreach (var value in _dynamic_dungeon_state.GetValueOrDefault("instances", new Godot.Collections.Array()).AsGodotArray())
        {
            if (value.VariantType != Variant.Type.Dictionary) continue;
            var instance = value.AsGodotDictionary();
            if (DynamicDungeonExpedition.IsCompactInstance(instance)) continue;
            var nodeId = DynamicDungeonExpedition.GetWorldNodeId(instance);
            var canEnter = DynamicDungeonExpedition.IsEntryOpen(instance, current_game_hour()) &&
                !instance.GetValueOrDefault("completed", false).AsBool();
            var isCurrent = savedCurrent == nodeId || _player_world_node_id == nodeId;
            if (!canEnter && !isCurrent) continue;
            var position = instance.GetValueOrDefault("world_position", new Vector2(-1, -1)).AsVector2();
            var result = _nodeWorldMap.Call("add_dynamic_dungeon_marker", nodeId,
                DynamicDungeonExpedition.GetDisplayName(instance), instance.GetValueOrDefault("seed", "").AsString(), position, canEnter);
            if (result.VariantType != Variant.Type.Vector2) continue;
            var resolved = result.AsVector2();
            if (resolved.X < 0 || resolved.Y < 0) continue;
            if (position != resolved)
            {
                instance["world_position"] = resolved;
                changed = true;
            }
            if (_world_node_data?.get_node(nodeId) is { } worldNode) worldNode.map_position = resolved;
        }
        if (changed) mark_save_dirty();
    }

    private void store_active_dynamic_dungeon_route()
    {
        if (_east_woods_dungeon?.is_dynamic_instance != true) return;
        var instance = DynamicDungeonExpedition.FindInstance(_dynamic_dungeon_state, _east_woods_dungeon.dynamic_instance_id);
        if (instance is null) return;
        DynamicDungeonExpedition.StoreRoute(instance, _east_woods_dungeon.get_route_state());
    }

    private void on_dynamic_dungeon_time_changed()
    {
        // Continuous routes synchronize once when the player reaches the next
        // world cell or finishes a dungeon route. Avoid cloning and walking every
        // dynamic instance for each intermediate movement edge.
        if (_nodeWorldMap?.Visible == true && _nodeWorldMap.Call("is_traveling").AsBool()) return;
        if (_east_woods_dungeon?.is_route_traveling() == true) return;
        synchronize_dynamic_dungeon_opportunities();
    }

    private void on_dynamic_dungeon_route_stepped(string destinationNodeId)
    {
        store_active_dynamic_dungeon_route();
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        if (time is null)
        {
            show_message("行进暂停", "游戏历法尚未就绪；未推进雾隐幽谷路线。");
            return;
        }
        var elapsed = EastWoodsDungeonRules.player_edge_hours(player?.dun_speed ?? EastWoodsDungeonRules.BaseDunSpeed);
        _dynamicDungeonTravelCompletedSteps++;
        time.advance_time(elapsed, "dynamic_dungeon_route");
        store_active_dynamic_dungeon_route();
        mark_save_dirty();
    }

    private bool try_spend_dynamic_dungeon_movement_stamina(string destinationNodeId)
    {
        var dungeon = _east_woods_dungeon;
        var instance = dungeon?.is_dynamic_instance == true
            ? DynamicDungeonExpedition.FindInstance(_dynamic_dungeon_state, dungeon.dynamic_instance_id)
            : null;
        if (instance is null || player is null) return false;
        var cost = DynamicDungeonExpedition.MovementStaminaCost(instance, destinationNodeId);
        if (cost <= 0) return true;
        if (player.try_spend_world_stamina(cost))
        {
            mark_save_dirty();
            return true;
        }
        show_message("精力不足", $"前往此节点需要 {cost:0.#} 点精力，当前精力为 {player.current_world_stamina:0.#}。没有移动到下一层。");
        return false;
    }

    private void on_dynamic_dungeon_travel_started()
    {
        _dynamicDungeonTravelStartedHour = current_game_hour();
        _dynamicDungeonTravelCompletedSteps = 0;
    }

    private void on_dynamic_dungeon_travel_finished(string _nodeId)
    {
        synchronize_dynamic_dungeon_opportunities();
        store_active_dynamic_dungeon_route();
        if (_dynamicDungeonTravelCompletedSteps > 0)
        {
            var hour = current_game_hour();
            award_player_renewable_inspiration(
                $"travel:mist_hidden_valley:{_dynamicDungeonTravelStartedHour}:{_east_woods_dungeon?.dynamic_instance_id}:{hour}:{_dynamicDungeonTravelCompletedSteps}",
                "journey.step");
        }
        _dynamicDungeonTravelStartedHour = -1;
        _dynamicDungeonTravelCompletedSteps = 0;
    }

    private void on_dynamic_dungeon_action(string nodeId, string actionId)
    {
        var dungeon = _east_woods_dungeon;
        var instance = dungeon is null ? null : DynamicDungeonExpedition.FindInstance(_dynamic_dungeon_state, dungeon.dynamic_instance_id);
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        if (dungeon?.is_dynamic_instance != true || instance is null || player is null || time is null ||
            nodeId != dungeon.get_current_route_node_id()) return;

        DynamicDungeonExpedition.SynchronizeInstanceEcology(instance, current_game_hour());
        dungeon.update_dynamic_instance(instance);
        var action = dungeon.find_action(nodeId, actionId);
        if (action is null || dungeon.action_remaining(nodeId, action) < Math.Max(1, action.Quantity)) return;
        if (action.Battle)
        {
            start_dynamic_dungeon_beast(nodeId, action, instance);
            return;
        }
        if (action.Rest && player.current_world_stamina >= player.max_world_stamina)
        {
            dungeon.show_action_result("精力已满", "当前精力已满，无需在此休息。");
            return;
        }

        var reservedInspiration = action.ItemPath.Length > 0
            ? player.get_armed_cultivation_inspiration_for_action(CultivationInspirationTuning.GatheringDomain)
            : null;
        var extraYield = reservedInspiration is null ? 0 : CultivationInspirationTuning.gathering_extra_yield(reservedInspiration.quality_rank);
        var finalQuantity = action.Quantity + extraYield;
        if (extraYield > 0 && dungeon.action_remaining(nodeId, action) < finalQuantity)
        {
            dungeon.show_action_result("储量不足", "当前剩余资源不足以完成已预留灵感强化的整次采集；灵感保留，可取消预留或等待资源恢复。");
            return;
        }
        if (action.ItemPath.Length > 0 && !valid_dungeon_item(action.ItemPath))
        {
            dungeon.show_action_result("暂不可采集", "当前物品或图标无法加载，未消耗时间、精力或储量。");
            return;
        }
        if (!player.can_spend_world_stamina(action.Stamina))
        {
            dungeon.show_action_result("精力不足", $"本次行动需要 {action.Stamina:0.#} 点精力；没有扣除费用。");
            return;
        }
        if (action.ItemPath.Length > 0 && !player.can_add_item_to_inventory(action.ItemPath, finalQuantity))
        {
            dungeon.show_action_result("行囊容量不足", "无法收纳本次采集物，没有消耗时间、精力或储量。");
            return;
        }
        if (!player.try_spend_world_stamina(action.Stamina))
        {
            dungeon.show_action_result("精力不足", "本次行动尚未开始，没有消耗时间或储量。");
            return;
        }
        if (reservedInspiration is not null &&
            !player.try_consume_armed_cultivation_inspiration_for_action(CultivationInspirationTuning.GatheringDomain, out _))
        {
            player.restore_world_stamina(action.Stamina);
            dungeon.show_action_result("行动未开始", "预留灵感已不可用，精力与储量均未消耗。");
            return;
        }

        if (action.Hours > 0) time.advance_time(action.Hours, action.Rest ? "dynamic_dungeon_rest" : "dynamic_dungeon_action");
        if (action.Rest && action.Hours > 0)
            player.restore_world_stamina(action.Hours * WorldStaminaRules.RestRecoveryPerGameHour);
        if (action.ItemPath.Length > 0 && !player.add_item_to_inventory(action.ItemPath, finalQuantity))
        {
            dungeon.show_action_result("行动未完成", "行囊无法收纳，资源储量未扣除；已投入的时间和精力不退还。");
            return;
        }
        dungeon.complete_action(nodeId, actionId, finalQuantity);
        var completedHour = current_game_hour();
        if (action.ItemPath.Length > 0)
            award_player_renewable_inspiration($"gather:{dungeon.dynamic_instance_id}:{nodeId}:{actionId}:{completedHour}", "field.gathering");
        store_active_dynamic_dungeon_route();
        synchronize_dynamic_dungeon_opportunities();
        mark_save_dirty();
        dungeon.show_action_result(action.Name + "结束", action.Result);
        dungeon.interrupt_travel(false);
    }

    private void start_dynamic_dungeon_beast(string nodeId, EastWoodsActions.ActionDef action, Godot.Collections.Dictionary instance)
    {
        var transition = GetNodeOrNull<BattleTransitionManager>("/root/BattleTransitionManager");
        var playerScene = player?.pack_for_battle();
        if (transition is null || playerScene is null || _east_woods_dungeon is null) return;
        var encounter = action.EncounterIdentity;
        var creatureIds = DynamicDungeonExpedition.GetEncounterCreatureIds(instance, encounter);
        if (creatureIds.Length == 0) return;
        var enemyScenes = new Godot.Collections.Array<PackedScene>();
        var enemySnapshots = new Godot.Collections.Array();
        for (var index = 0; index < creatureIds.Length; index++)
        {
            if (!try_create_dynamic_dungeon_beast(creatureIds[index], encounter, index, out var scene, out var snapshot)) return;
            enemyScenes.Add(scene!);
            enemySnapshots.Add(snapshot!);
        }

        _east_woods_dungeon.interrupt_travel();
        store_active_dynamic_dungeon_route();
        var route = _east_woods_dungeon.get_route_state();
        route["inside"] = true;
        DynamicDungeonExpedition.StoreRoute(instance, route);
        mark_save_dirty();
        transition.set_battle_return_context(new Godot.Collections.Dictionary
        {
            ["kind"] = "dynamic_dungeon_beast",
            ["instance_id"] = _east_woods_dungeon.dynamic_instance_id,
            ["node"] = nodeId,
            ["action"] = action.Id,
            ["encounter_name"] = action.Name.Replace("挑战", "").Trim(),
            ["route_state"] = route.Duplicate(true),
            ["title"] = DynamicDungeonExpedition.GetDisplayName(instance) + " · " + action.Name,
            ["allow_prebattle_cancel"] = true,
            ["loot_rewards"] = new Godot.Collections.Array
            {
                new Godot.Collections.Dictionary { ["item_path"] = "res://resources/entities/items/兽骨.tres", ["count"] = 1, ["chance"] = 1f },
                new Godot.Collections.Dictionary { ["item_path"] = "res://resources/entities/items/兽皮.tres", ["count"] = 1, ["chance"] = 1f },
            },
        });
        transition.start_team_battle(
            new Godot.Collections.Array<PackedScene> { playerScene },
            enemyScenes,
            new Godot.Collections.Array { player!.get_snapshot_data() },
            enemySnapshots,
            GetTree().CurrentScene?.SceneFilePath ?? "res://tscn/scene/MainScene.tscn");
    }

    private bool try_create_dynamic_dungeon_beast(string creatureId, string encounter, int index, out PackedScene? scene, out BattleEntityData? snapshot)
    {
        scene = null;
        snapshot = null;
        var templatePath = creatureId switch
        {
            "gray_back_boar" => "res://resources/beast_templates/灰背野猪_试玩.tres",
            "forest_bei" => "res://resources/beast_templates/林地妖狈.tres",
            "qing_tail_fox" => "res://resources/beast_templates/青尾狐.tres",
            "red_mane_wolf" => "res://resources/beast_templates/赤鬃狼.tres",
            "rock_back_badger" => "res://resources/beast_templates/岩背獾.tres",
            _ => "res://resources/beast_templates/林地凶狼.tres",
        };
        var template = GD.Load<BeastTemplate>(templatePath);
        if (template is null)
        {
            GD.PushError($"副本遭遇无法加载生灵模板：{templatePath}");
            return false;
        }
        var beast = new Beast
        {
            Name = $"DynamicDungeonBeast_{creatureId}_{index}",
            unique_id = $"temp_enemy_mist_hidden_valley_{encounter}_{creatureId}_{index}",
            beast_template = template,
            entity_data = new LivingData(),
            faction = (int)WorldEntity.Faction.ENEMY,
            Visible = false,
        };
        AddChild(beast);
        scene = beast.pack_for_battle();
        snapshot = beast.get_snapshot_data();
        RemoveChild(beast);
        beast.Free();
        if (scene is null || snapshot is null) return false;
        snapshot.dun_speed = 100;
        return true;
    }

    private void restore_dynamic_dungeon_beast_result(Godot.Collections.Dictionary summary, Godot.Collections.Dictionary context)
    {
        var instanceId = context.GetValueOrDefault("instance_id", "").AsString();
        var instance = DynamicDungeonExpedition.FindInstance(_dynamic_dungeon_state, instanceId);
        if (instance is null) return;
        var dungeon = get_or_create_east_woods_dungeon();
        dungeon.configure_dynamic_instance(instance);
        dungeon.restore_route_state(context.GetValueOrDefault("route_state", new Godot.Collections.Dictionary()).AsGodotDictionary());
        var victory = summary.GetValueOrDefault("player_victory", false).AsBool();
        if (victory)
            dungeon.complete_action(context.GetValueOrDefault("node", "").AsString(), context.GetValueOrDefault("action", "").AsString(), 1);
        dungeon.set_player_dun_speed(player?.dun_speed ?? EastWoodsDungeonRules.BaseDunSpeed);
        dungeon.set_active_duty_context("", 0, false);
        dungeon.show_dungeon();
        store_active_dynamic_dungeon_route();
        var encounterName = context.GetValueOrDefault("encounter_name", "妖兽").AsString();
        dungeon.show_action_result("副本遭遇", victory
            ? $"{encounterName}已被击败。战利品按正式战后结算领取；此个体不会立即复活。"
            : $"你回到刚才驻足的地格。{encounterName}仍在，可以稍后再战；主角战败沿用现有规则。");
        synchronize_dynamic_dungeon_opportunities();
        mark_save_dirty();
    }
}
