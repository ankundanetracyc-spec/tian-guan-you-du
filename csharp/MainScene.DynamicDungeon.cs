using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Lifecycle and play-loop integration for limited-time world dungeon opportunities.</summary>
public partial class MainScene
{
    private static readonly Vector2I[] DynamicWorldDirections = [Vector2I.Right, Vector2I.Down, Vector2I.Left, Vector2I.Up];
    private long _dynamicDungeonTravelStartedHour = -1;
    private int _dynamicDungeonTravelCompletedSteps;
    private bool _dynamicDungeonMarkersInitialized;

    private int current_game_hour() => GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;

    private void synchronize_dynamic_dungeon_opportunities()
    {
        if (_dynamic_dungeon_state.Count == 0)
            _dynamic_dungeon_state = DynamicDungeonExpedition.CreateEmpty(current_game_hour());

        if (_east_woods_dungeon?.Visible == true && _east_woods_dungeon.is_dynamic_instance)
            store_active_dynamic_dungeon_route();

        var activated = _dynamic_dungeon_state.GetValueOrDefault("activated", false).AsBool();
        var canInitialize = tile_world_map is not null && tile_world_map.IsNodeReady() && has_open_world_access();
        if (!activated && !canInitialize) return;

        var changed = DynamicDungeonExpedition.Synchronize(
            _dynamic_dungeon_state,
            current_game_hour(),
            canInitialize ? find_dynamic_dungeon_world_candidates : static () => Array.Empty<Vector2I>());

        if (tile_world_map is not null && tile_world_map.IsNodeReady() && (changed || !_dynamicDungeonMarkersInitialized))
        {
            tile_world_map.Call("set_dynamic_dungeon_entries", DynamicDungeonExpedition.OpenEntries(_dynamic_dungeon_state, current_game_hour()));
            _dynamicDungeonMarkersInitialized = true;
        }

        if (_east_woods_dungeon?.Visible == true && _east_woods_dungeon.is_dynamic_instance &&
            DynamicDungeonExpedition.FindInstance(_dynamic_dungeon_state, _east_woods_dungeon.dynamic_instance_id) is { } current)
            _east_woods_dungeon.update_dynamic_instance(current);

        if (changed) mark_save_dirty();
    }

    private IReadOnlyList<Vector2I> find_dynamic_dungeon_world_candidates()
    {
        if (tile_world_map is null || !tile_world_map.IsNodeReady()) return Array.Empty<Vector2I>();
        var start = tile_world_map.Call("get_location_anchor_cell", "yuxu_xianzong_core").AsVector2I();
        if (start.X < 0 || start.Y < 0 || !tile_world_map.Call("is_cell_traversable", start).AsBool())
            return Array.Empty<Vector2I>();

        var occupied = _npc_world?.get_ordinary_grid_population_counts() ?? new Godot.Collections.Dictionary();
        var existing = new HashSet<Vector2I>();
        foreach (var value in _dynamic_dungeon_state.GetValueOrDefault("instances", new Godot.Collections.Array()).AsGodotArray())
            if (value.VariantType == Variant.Type.Dictionary)
            {
                var instance = value.AsGodotDictionary();
                if (instance.GetValueOrDefault("entry_open", false).AsBool())
                    existing.Add(instance.GetValueOrDefault("world_cell", Vector2I.Zero).AsVector2I());
            }

        var distances = new Dictionary<Vector2I, int> { [start] = 0 };
        var queue = new Queue<Vector2I>();
        queue.Enqueue(start);
        var candidates = new List<Vector2I>();
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            var distance = distances[cell];
            if (distance > 0 && distance <= 10 &&
                tile_world_map.Call("is_cell_traversable", cell).AsBool() &&
                (tile_world_map.Call("get_cell_terrain", cell).AsString() is "forest" or "mountain") &&
                string.IsNullOrWhiteSpace(tile_world_map.Call("get_location_node_id_at_cell", cell).AsString()) &&
                !occupied.ContainsKey(cell) && !existing.Contains(cell) && cell != _player_world_grid_cell)
                candidates.Add(cell);

            if (distance >= 10) continue;
            foreach (var direction in DynamicWorldDirections)
            {
                var next = cell + direction;
                if (distances.ContainsKey(next) || !tile_world_map.Call("is_cell_traversable", next).AsBool()) continue;
                distances[next] = distance + 1;
                queue.Enqueue(next);
            }
        }
        return candidates.OrderBy(cell => cell.Y).ThenBy(cell => cell.X).ToArray();
    }

    private void on_dynamic_dungeon_enter_requested(string instanceId)
    {
        if (tile_world_map?.Visible != true || tile_world_map.Call("is_traveling").AsBool() ||
            DynamicDungeonExpedition.FindInstance(_dynamic_dungeon_state, instanceId) is not { } instance ||
            !DynamicDungeonExpedition.IsEntryOpen(instance, current_game_hour()) ||
            instance.GetValueOrDefault("world_cell", Vector2I.Zero).AsVector2I() != _player_world_grid_cell ||
            tile_world_map.Call("get_player_cell").AsVector2I() != _player_world_grid_cell)
            return;

        var route = DynamicDungeonExpedition.GetRoute(instance);
        route["inside"] = true;
        DynamicDungeonExpedition.StoreRoute(instance, route);
        _npc_world?.set_simulation_enabled(false);
        (GetTree().GetFirstNodeInGroup("cell_character_list") as CellCharacterList)?.hide_world_npcs();
        open_dynamic_dungeon(instanceId);
        mark_save_dirty();
    }

    private void open_dynamic_dungeon(string instanceId)
    {
        if (DynamicDungeonExpedition.FindInstance(_dynamic_dungeon_state, instanceId) is not { } instance) return;
        var isAlreadyInside = instance.GetValueOrDefault("inside", false).AsBool();
        if (!isAlreadyInside && !DynamicDungeonExpedition.IsEntryOpen(instance, current_game_hour())) return;

        estate?.Hide();
        location_interior?.hide_roster();
        location_interior?.Hide();
        tile_world_map?.Hide();
        node_map?.hide_world_roster();
        node_map?.Hide();
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
        (GetTree().GetFirstNodeInGroup("cell_character_list") as CellCharacterList)?.hide_world_npcs();
        mark_save_dirty();
        open_world();
    }

    private void store_active_dynamic_dungeon_route()
    {
        if (_east_woods_dungeon?.is_dynamic_instance != true) return;
        var instance = DynamicDungeonExpedition.FindInstance(_dynamic_dungeon_state, _east_woods_dungeon.dynamic_instance_id);
        if (instance is null) return;
        DynamicDungeonExpedition.StoreRoute(instance, _east_woods_dungeon.get_route_state());
    }

    private void on_dynamic_dungeon_time_changed()
        => synchronize_dynamic_dungeon_opportunities();

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
            dungeon.show_action_result("行囊已满", "无法收纳本次采集物，没有消耗时间、精力或储量。");
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

        if (action.Hours > 0) time.advance_time(action.Hours, "dynamic_dungeon_action");
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
        var template = GD.Load<BeastTemplate>("res://resources/beast_templates/林地凶狼.tres");
        var playerScene = player?.pack_for_battle();
        if (transition is null || template is null || playerScene is null || _east_woods_dungeon is null) return;
        var encounter = action.EncounterIdentity;
        var beast = new Beast
        {
            Name = "MistHiddenValleyWolf",
            unique_id = "temp_enemy_mist_hidden_valley_" + encounter,
            beast_template = template,
            entity_data = new LivingData(),
            faction = (int)WorldEntity.Faction.ENEMY,
            Visible = false,
        };
        AddChild(beast);
        var scene = beast.pack_for_battle();
        var snapshot = beast.get_snapshot_data();
        RemoveChild(beast);
        beast.Free();
        if (scene is null || snapshot is null) return;
        snapshot.dun_speed = 100;

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
            ["route_state"] = route.Duplicate(true),
            ["title"] = "雾隐幽谷 · 林地凶狼",
            ["allow_prebattle_cancel"] = true,
            ["loot_rewards"] = new Godot.Collections.Array
            {
                new Godot.Collections.Dictionary { ["item_path"] = "res://resources/entities/items/兽骨.tres", ["count"] = 1, ["chance"] = 1f },
                new Godot.Collections.Dictionary { ["item_path"] = "res://resources/entities/items/兽皮.tres", ["count"] = 1, ["chance"] = 1f },
            },
        });
        transition.start_team_battle(
            new Godot.Collections.Array<PackedScene> { playerScene },
            new Godot.Collections.Array<PackedScene> { scene },
            new Godot.Collections.Array { player!.get_snapshot_data() },
            new Godot.Collections.Array { snapshot },
            GetTree().CurrentScene?.SceneFilePath ?? "res://tscn/scene/MainScene.tscn");
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
        dungeon.show_action_result("幽谷遭遇", victory
            ? "林地凶狼已被击败。战利品按正式战后结算领取；此个体不会复活。"
            : "你回到刚才驻足的地格。妖兽仍在，可以绕行或稍后再战；主角战败沿用现有规则。");
        synchronize_dynamic_dungeon_opportunities();
        mark_save_dirty();
    }
}
