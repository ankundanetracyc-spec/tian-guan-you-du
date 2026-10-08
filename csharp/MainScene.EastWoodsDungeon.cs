using Godot;
using Godot.Collections;
using System;
using System.Linq;

public partial class MainScene
{
    private EastWoodsLocationPanel? _east_woods_location;
    private EastWoodsDungeonPanel? _east_woods_dungeon;
    private Godot.Collections.Dictionary _east_woods_dungeon_state = [];

    private EastWoodsLocationPanel get_or_create_east_woods_location()
    {
        if (_east_woods_location is not null) return _east_woods_location;
        _east_woods_location = new EastWoodsLocationPanel { Name = "EastWoodsLocationPanel" };
        GetNode<CanvasLayer>("SceneLayer").AddChild(_east_woods_location);
        _east_woods_location.Closed += close_world_location;
        _east_woods_location.ExplorationRequested += open_east_woods_dungeon;
        return _east_woods_location;
    }

    private EastWoodsDungeonPanel get_or_create_east_woods_dungeon()
    {
        if (_east_woods_dungeon is not null) return _east_woods_dungeon;
        _east_woods_dungeon = new EastWoodsDungeonPanel { Name = "EastWoodsDungeonPanel" };
        GetNode<CanvasLayer>("SceneLayer").AddChild(_east_woods_dungeon);
        _east_woods_dungeon.Closed += return_to_east_woods_location;
        _east_woods_dungeon.RouteNodeEntered += on_east_woods_route_node_entered;
        _east_woods_dungeon.RouteStepped += on_east_woods_route_stepped;
        return _east_woods_dungeon;
    }

    private void open_east_woods_dungeon()
    {
        _east_woods_location?.Hide();
        refresh_yuxu_life_context();
        _npc_world?.enter_east_woods_route();
        mark_save_dirty();
        var dungeon = get_or_create_east_woods_dungeon();
        dungeon.restore_route_state(_east_woods_dungeon_state);
        if (player?.entity_data is HumanData human)
            dungeon.set_active_duty_context(human.sect_active_yuxu_duty_id, human.sect_active_yuxu_duty_stage);
        dungeon.show_dungeon();
        refresh_east_woods_route_roster(dungeon.get_current_route_node_id());
    }

    private void return_to_east_woods_location()
    {
        if (_east_woods_dungeon is not null) _east_woods_dungeon_state = _east_woods_dungeon.get_route_state();
        get_or_create_east_woods_location().show_location();
        if (GetTree().GetFirstNodeInGroup("cell_character_list") is CellCharacterList roster)
            roster.show_world_npcs(_npc_world?.get_subscene_roster("east_woods", "forest_edge") ?? new Godot.Collections.Array<WorldNpcData>(), "interior:east_woods:forest_edge");
    }

    private void on_east_woods_route_stepped(string _fromNodeId, string toNodeId)
    {
        if (_east_woods_dungeon is not null) _east_woods_dungeon_state = _east_woods_dungeon.get_route_state();
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        var elapsedHours = EastWoodsDungeonRules.player_edge_hours(player?.dun_speed ?? EastWoodsDungeonRules.BaseDunSpeed);
        if (time is null)
        {
            show_message("行进暂停", "游戏历法尚未就绪；没有推进副本路线或 NPC 行程。");
            return;
        }

        time.advance_time(elapsedHours, "east_woods_dungeon");
        var encounter = _npc_world?.advance_east_woods_route(_fromNodeId, toNodeId, elapsedHours, player?.entity_data as HumanData) ?? new Godot.Collections.Dictionary();
        mark_save_dirty();
        refresh_east_woods_route_roster(toNodeId);
        if (encounter.Count > 0)
            CallDeferred(nameof(start_east_woods_encounter), encounter);
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
            ["route_state"] = routeState.Duplicate(true),
        });
        transition.start_team_battle(
            new Godot.Collections.Array<PackedScene> { attackerScene },
            new Godot.Collections.Array<PackedScene> { defenderScene },
            new Godot.Collections.Array { attackerSnapshot },
            new Godot.Collections.Array { defenderSnapshot },
            GetTree().CurrentScene?.SceneFilePath ?? "res://tscn/scene/MainScene.tscn");
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
            gender = npc.gender,
            cultivation_realm = npc.cultivation_realm,
            spirit_root_name = npc.spirit_root_name,
            spirit_strands = npc.spirit_strands,
            current_age = npc.current_age,
            max_age = npc.max_age,
            base_physique = npc.physique,
            base_intelligence = npc.intelligence,
            base_agility = npc.agility,
            base_soul = npc.soul,
            physique = npc.physique,
            intelligence = npc.intelligence,
            agility = npc.agility,
            soul = npc.soul,
            current_health = npc.current_health,
            max_health = npc.max_health,
            current_stamina = 6,
            max_stamina = 6,
            current_mana = npc.cultivation_realm == "凡人" ? 0 : 3,
            max_mana = npc.cultivation_realm == "凡人" ? 0 : 3,
            basic_deck_path = "res://resources/basic_decks/初始牌库.tres",
            sect_affiliation_id = npc.faction_id,
            sect_current_rank = npc.formal_identity,
            dun_speed = npc.dun_speed,
        };
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
        var scene = actor.pack_for_battle();
        var snapshot = actor.get_snapshot_data();
        RemoveChild(actor);
        actor.Free();
        return (scene, snapshot);
    }

    /// <summary>CombatTrigger 在既有主角战败处理之后回调；东林地 NPC 状态与掠夺在此一次性落账。</summary>
    public void handle_persistent_battle_return(Godot.Collections.Array attackers, Godot.Collections.Array defenders, Godot.Collections.Dictionary summary, Godot.Collections.Dictionary context)
    {
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
                _npc_world.record_dungeon_battle_outcome(attackerNpc, "player", "战胜玩家并完成一次随身物品掠夺", lootSummary);
                show_message("东林地遇袭", $"战斗结束。主角依既有战败规则恢复至低生命状态；{lootSummary}");
            }
            else if (defenderWon)
            {
                lootSummary = transfer_npc_loot_to_player(attackerNpc, !attackerNpc.is_alive);
                _npc_world.record_dungeon_battle_outcome(attackerNpc, "player", attackerNpc.is_alive ? "被玩家击退" : "被玩家击杀", lootSummary);
                show_message("东林地遭遇", $"你赢下了这场战斗。{lootSummary}");
            }
            else show_message("东林地遭遇", "战斗提前结束，双方暂时分开；没有结算胜负或掠夺。");
        }
        else if (attackerNpc is not null && defenderNpc is not null)
        {
            if (attackerWon)
            {
                lootSummary = transfer_npc_loot_to_npc(attackerNpc, defenderNpc, !defenderNpc.is_alive);
                _npc_world.record_dungeon_battle_outcome(defenderNpc, attackerNpc.npc_id, defenderNpc.is_alive ? "败于沿途冲突" : "在沿途冲突中身亡", lootSummary);
                _npc_world.record_dungeon_battle_outcome(attackerNpc, defenderNpc.npc_id, "战胜同行并完成一次随身物品掠夺", lootSummary);
                show_message("林中冲突", $"{attackerNpc.npc_name} 战胜了 {defenderNpc.npc_name}。{lootSummary}");
            }
            else if (defenderWon)
            {
                lootSummary = transfer_npc_loot_to_npc(defenderNpc, attackerNpc, !attackerNpc.is_alive);
                _npc_world.record_dungeon_battle_outcome(attackerNpc, defenderNpc.npc_id, attackerNpc.is_alive ? "败于沿途冲突" : "在沿途冲突中身亡", lootSummary);
                _npc_world.record_dungeon_battle_outcome(defenderNpc, attackerNpc.npc_id, "击退沿途袭击并完成一次随身物品掠夺", lootSummary);
                show_message("林中冲突", $"{defenderNpc.npc_name} 战胜了 {attackerNpc.npc_name}。{lootSummary}");
            }
            else show_message("林中冲突", "双方暂时分开；没有结算胜负或物品转移。");
        }

        _east_woods_dungeon_state = context.GetValueOrDefault("route_state", new Godot.Collections.Dictionary()).AsGodotDictionary();
        _east_woods_location?.Hide();
        var dungeon = get_or_create_east_woods_dungeon();
        dungeon.restore_route_state(_east_woods_dungeon_state);
        dungeon.show_dungeon();
        if (player?.entity_data is HumanData human) dungeon.set_active_duty_context(human.sect_active_yuxu_duty_id, human.sect_active_yuxu_duty_stage);
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

    private string transfer_npc_loot_to_npc(WorldNpcData winner, WorldNpcData loser, bool takeAll)
    {
        if (_npc_world is null) return "没有物品转移。";
        if (takeAll)
        {
            var count = _npc_world.transfer_all_carried_items(loser, winner);
            return count > 0 ? $"掠得随身物品共 {count} 件。" : "败者没有可掠夺的随身物品。";
        }
        var path = _npc_world.remove_one_carried_item(loser);
        if (string.IsNullOrWhiteSpace(path)) return "败者没有可掠夺的随身物品。";
        _npc_world.add_carried_item(winner, path);
        return $"掠得 {ResourceLoader.Load<ItemData>(path)?.entity_name ?? "一件物品"} ×1。";
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
