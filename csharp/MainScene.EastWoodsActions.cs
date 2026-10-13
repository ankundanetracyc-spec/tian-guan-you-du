using Godot;
using System;

public partial class MainScene
{
    private void on_east_woods_action(string node, string id)
    {
        var dungeon = _east_woods_dungeon;
        synchronize_east_woods_ecology();
        var action = dungeon?.find_action(node, id);
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        if (dungeon is null || player is null || time is null || action is null || node != dungeon.get_current_route_node_id()) return;
        if (dungeon.action_remaining(node, action) < Math.Max(1, action.Quantity)) return;
        if (action.Battle) { start_east_woods_beast(node, action); return; }
        var reservedGatheringInspiration = action.ItemPath.Length > 0
            ? player.get_armed_cultivation_inspiration_for_action(CultivationInspirationTuning.GatheringDomain)
            : null;
        var extraYield = reservedGatheringInspiration is null
            ? 0 : CultivationInspirationTuning.gathering_extra_yield(reservedGatheringInspiration.quality_rank);
        var finalQuantity = action.Quantity + extraYield;
        if (extraYield > 0 && dungeon.action_remaining(node, action) < finalQuantity)
        { dungeon.show_action_result("储量不足", "当前剩余资源不足以完成已预留灵感强化的整次采集；灵感仍保留，可取消预留或等待资源恢复。"); return; }
        // Validate resources and inventory capacity before committing either time or stamina.
        if (action.ItemPath.Length > 0 && !valid_dungeon_item(action.ItemPath))
        { dungeon.show_action_result("暂不可采集", "当前物品或专属图标无法加载，未消耗时间、体力或储量。"); return; }
        if (!player.can_spend_world_stamina(action.Stamina))
        { dungeon.show_action_result("体力不足", $"本次行动需要 {action.Stamina:0.#} 点体力。可以离开林地休息后再来；没有扣除费用。"); return; }
        if (action.ItemPath.Length > 0 && !player.can_add_item_to_inventory(action.ItemPath, finalQuantity))
        { dungeon.show_action_result("行囊已满", "无法收纳本次采集物，没有消耗时间、体力或储量。"); return; }
        if (!player.try_spend_world_stamina(action.Stamina))
        { dungeon.show_action_result("体力不足", $"本次行动需要 {action.Stamina:0.#} 点体力。可以离开林地休息后再来；没有扣除费用。"); return; }
        if (reservedGatheringInspiration is not null &&
            !player.try_consume_armed_cultivation_inspiration_for_action(CultivationInspirationTuning.GatheringDomain, out _))
        {
            // Expiry or a stale reservation can invalidate the selection after the UI was opened.
            // Roll back the just-paid stamina because this action has not begun yet.
            player.restore_world_stamina(action.Stamina);
            dungeon.show_action_result("采集未开始", "预留灵感已不可用，未开始本次采集。体力与储量均未消耗。");
            return;
        }
        var remaining = action.Hours;
        while (remaining > .0001f)
        {
            // Never skip an NPC route edge even during a stationary action.
            var slice = Math.Min(remaining, next_east_woods_npc_step());
            time.advance_time(slice, "east_woods_action");
            remaining -= slice;
            var encounter = _npc_world?.advance_east_woods_route(node, node, slice, player.entity_data as HumanData) ?? new Godot.Collections.Dictionary();
            if (encounter.Count == 0) continue;
            dungeon.interrupt_travel();
            _east_woods_dungeon_state = dungeon.get_route_state();
            mark_save_dirty(); refresh_east_woods_route_roster(node);
            CallDeferred(nameof(start_east_woods_encounter), encounter);
            return; // Interrupted: no harvest, investigation progress or stock depletion.
        }
        if (action.ItemPath.Length > 0 && !player.add_item_to_inventory(action.ItemPath, finalQuantity))
        { dungeon.show_action_result("采集未完成", "行囊无法收纳，资源储量未扣除。已投入的时间和体力不退还。"); return; }
        dungeon.complete_action(node, id, finalQuantity);
        var completedAt = time.get_total_game_hours();
        if (action.ItemPath.Length > 0)
            award_player_renewable_inspiration($"gather:{node}:{id}:{completedAt}", "field.gathering");
        _east_woods_dungeon_state = dungeon.get_route_state();
        mark_save_dirty(); refresh_east_woods_route_roster(node);
        RefreshJournalTracker();
        refresh_journal_entries();
        var detail = action.Result;
        dungeon.show_action_result(action.Name + "结束", detail);
        dungeon.interrupt_travel(false);
    }

    private float next_east_woods_npc_step()
    {
        var slice = .25f;
        if (_npc_world is null) return slice;
        foreach (var npc in _npc_world.npcs)
            if (npc.is_alive && npc.dungeon_id == EastWoodsDungeonRules.DungeonId)
                slice = Math.Min(slice, Math.Max(.0001f, EastWoodsDungeonRules.npc_edge_cost(npc.dun_speed) - npc.dungeon_travel_progress));
        return slice;
    }

    private static bool valid_dungeon_item(string path)
    {
        var item = EastWoodsActions.Item(path);
        return item is not null && !string.IsNullOrWhiteSpace(item.icon_path) && ResourceLoader.Exists(item.icon_path);
    }

    private void start_east_woods_beast(string node, EastWoodsActions.ActionDef action)
    {
        var transition = GetNodeOrNull<BattleTransitionManager>("/root/BattleTransitionManager");
        var template = GD.Load<BeastTemplate>("res://resources/beast_templates/林地凶狼.tres");
        var playerScene = player?.pack_for_battle();
        if (transition is null || template is null || playerScene is null) return;
        var encounterIdentity = string.IsNullOrWhiteSpace(action.EncounterIdentity) ? node + "_" + action.Id : action.EncounterIdentity;
        var beast = new Beast { Name = "EastWoodsBeast", unique_id = "temp_enemy_east_woods_" + encounterIdentity,
            beast_template = template, entity_data = new LivingData(), faction = (int)WorldEntity.Faction.ENEMY, Visible = false };
        AddChild(beast);
        var scene = beast.pack_for_battle(); var snapshot = beast.get_snapshot_data();
        RemoveChild(beast); beast.Free();
        if (scene is null || snapshot is null) return;
        snapshot.dun_speed = 100; // This encounter's short-test value; not a global species default.
        _east_woods_dungeon!.interrupt_travel();
        _east_woods_dungeon_state = _east_woods_dungeon.get_route_state();
        mark_save_dirty();
        transition.set_battle_return_context(new Godot.Collections.Dictionary {
            ["kind"] = "east_woods_beast", ["node"] = node, ["action"] = action.Id,
            ["route_state"] = _east_woods_dungeon_state.Duplicate(true), ["title"] = node == "root_hollow" ? "盘根深处 · 林地凶狼" : "林间兽踪 · 林地凶狼", ["allow_prebattle_cancel"] = true,
            ["loot_rewards"] = new Godot.Collections.Array {
                new Godot.Collections.Dictionary { ["item_path"] = "res://resources/entities/items/兽骨.tres", ["count"] = 1, ["chance"] = 1f },
                new Godot.Collections.Dictionary { ["item_path"] = "res://resources/entities/items/兽皮.tres", ["count"] = 1, ["chance"] = 1f }
            }
        });
        transition.start_team_battle(new Godot.Collections.Array<PackedScene> { playerScene },
            new Godot.Collections.Array<PackedScene> { scene }, new Godot.Collections.Array { player!.get_snapshot_data() },
            new Godot.Collections.Array { snapshot }, GetTree().CurrentScene.SceneFilePath);
    }

    private void restore_east_woods_beast_result(Godot.Collections.Dictionary summary, Godot.Collections.Dictionary context)
    {
        var dungeon = get_or_create_east_woods_dungeon();
        dungeon.restore_route_state(context.GetValueOrDefault("route_state", new Godot.Collections.Dictionary()).AsGodotDictionary());
        var victory = summary.GetValueOrDefault("player_victory", false).AsBool();
        if (victory) dungeon.complete_action(context.GetValueOrDefault("node", "").AsString(), context.GetValueOrDefault("action", "").AsString());
        _east_woods_dungeon_state = dungeon.get_route_state();
        dungeon.set_player_dun_speed(player?.dun_speed ?? 100);
        dungeon.show_dungeon(); refresh_east_woods_route_roster(dungeon.get_current_route_node_id());
        dungeon.show_action_result("林中遭遇", victory ? "林地凶狼已被击败。战利品由正式战后结算领取；此格不会因重进副本重新刷出妖兽。" : "你回到了刚才驻足的地格。妖兽仍在；可以绕行或日后再来。主角战败沿用现有规则。");
        mark_save_dirty();
    }
}
