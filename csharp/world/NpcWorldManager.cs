using Godot;
using Godot.Collections;

/// <summary>
/// 百川区域 NPC 的唯一运行时入口。它只在玩家主动推进时间并跨月时结算，
/// 并把每次状态变化写成可查询的行动记录。
/// </summary>
public partial class NpcWorldManager : Node
{
    [Signal] public delegate void world_npcs_changedEventHandler();
    [Signal] public delegate void npc_action_recordedEventHandler(string npc_id, NpcActionRecord record);

    [Export] public bool simulation_enabled;
    [Export] public Array<WorldNpcData> npcs = [];

    private int _last_processed_month = -1;
    private WorldNodeMapData? _map_data;
    private Node? _tile_world_map;
    private int _last_world_grid_hour = -1;

    public override void _Ready()
    {
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        if (time is not null)
        {
            _last_processed_month = month_index(time);
            _last_world_grid_hour = time.get_total_game_hours();
            time.time_changed += on_time_changed;
        }
    }

    public void set_world_map(WorldNodeMapData map_data)
    {
        _map_data = map_data;
        foreach (var npc in npcs)
            normalize_npc_location(npc);
    }

    public void set_tile_world_map(Node tileWorldMap)
    {
        _tile_world_map = tileWorldMap;
        foreach (var npc in npcs)
        {
            normalize_npc_location(npc);
            initialize_world_grid_position(npc);
        }
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        if (time is not null) _last_world_grid_hour = time.get_total_game_hours();
        EmitSignal(SignalName.world_npcs_changed);
    }

    public bool has_world_grid_anchor(string nodeId)
    {
        if (_tile_world_map is null) return true; // Preserve data-only use in tests and non-map tools.
        var cell = get_location_anchor_cell(nodeId);
        return is_valid_world_grid_cell(cell) && _tile_world_map.Call("is_cell_traversable", cell).AsBool();
    }

    public void set_simulation_enabled(bool enabled)
    {
        var was_enabled = simulation_enabled;
        simulation_enabled = enabled;
        if (enabled && !was_enabled && GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager") is { } time)
        {
            _last_processed_month = month_index(time);
            _last_world_grid_hour = time.get_total_game_hours();
        }
    }

    public int get_last_processed_month() => _last_processed_month;

    public void clear_for_new_game()
    {
        simulation_enabled = false;
        npcs.Clear();
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        _last_processed_month = time is null ? -1 : month_index(time);
        _last_world_grid_hour = time is null ? -1 : time.get_total_game_hours();
        EmitSignal(SignalName.world_npcs_changed);
    }

    public void restore_persistent_state(Array<WorldNpcData> savedNpcs, bool enabled, int lastProcessedMonth)
    {
        npcs.Clear();
        foreach (var savedNpc in savedNpcs)
        {
            if (savedNpc is null) continue;
            var copy = savedNpc.Duplicate(true) as WorldNpcData;
            if (copy is not null) npcs.Add(copy);
        }
        simulation_enabled = enabled;
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        _last_processed_month = lastProcessedMonth >= 0
            ? lastProcessedMonth
            : time is null ? -1 : month_index(time);
        _last_world_grid_hour = time is null ? -1 : time.get_total_game_hours();
        if (_map_data is not null)
            foreach (var npc in npcs)
            {
                normalize_npc_location(npc);
                initialize_world_grid_position(npc);
            }
        EmitSignal(SignalName.world_npcs_changed);
    }

    public void register_npc(WorldNpcData npc)
    {
        if (npc is null || string.IsNullOrWhiteSpace(npc.npc_id)) return;
        if (!normalize_npc_location(npc)) return;
        initialize_world_grid_position(npc);
        var existing = get_npc(npc.npc_id);
        if (existing is not null) npcs.Remove(existing);
        npcs.Add(npc);
        EmitSignal(SignalName.world_npcs_changed);
    }

    public WorldNpcData? get_npc(string npc_id) => npcs.FirstOrDefault(npc => npc.npc_id == npc_id);

    /// <summary>返回当前已登记、仍在世的人物；青溪里序章人物不在此登记表内。</summary>
    public Array<WorldNpcData> get_active_npcs()
    {
        var result = new Array<WorldNpcData>();
        foreach (var npc in npcs)
            if (npc.is_alive) result.Add(npc);
        return result;
    }

    public Array<WorldNpcData> get_persistent_npcs()
    {
        var result = new Array<WorldNpcData>();
        foreach (var npc in npcs)
            if (npc is not null && npc.Duplicate(true) is WorldNpcData copy)
                result.Add(copy);
        return result;
    }

    public int get_node_npc_count(string node_id) => get_node_roster(node_id).Count;

    /// <summary>世界地图节点人数包含驻留者，以及当前格正处于该节点占地内的行旅者。</summary>
    public int get_world_location_population_count(string nodeId)
    {
        if (_tile_world_map is null) return get_node_npc_count(nodeId);
        var count = 0;
        foreach (var npc in npcs)
        {
            if (npc is null || !npc.is_alive || !string.IsNullOrEmpty(npc.dungeon_id) || !npc.has_world_grid_position) continue;
            if (!npc.is_traveling && npc.current_node_id == nodeId)
            {
                count++;
                continue;
            }
            if (npc.is_traveling && get_location_node_id_at_cell(npc.current_grid_cell) == nodeId) count++;
        }
        return count;
    }

    /// <summary>普通格的人数徽标只统计当前实际经过该格的行旅者。</summary>
    public Godot.Collections.Dictionary get_ordinary_grid_population_counts()
    {
        var counts = new Godot.Collections.Dictionary();
        if (_tile_world_map is null) return counts;
        foreach (var npc in npcs)
        {
            if (npc is null || !npc.is_alive || !npc.is_traveling || !npc.has_world_grid_position || !string.IsNullOrEmpty(npc.dungeon_id)) continue;
            if (!string.IsNullOrEmpty(get_location_node_id_at_cell(npc.current_grid_cell))) continue;
            var cell = npc.current_grid_cell;
            counts[cell] = counts.ContainsKey(cell) ? counts[cell].AsInt32() + 1 : 1;
        }
        return counts;
    }

    /// <summary>地点占地内共用稳定节点名单；普通格只显示当前真实落在该格的人物。</summary>
    public Array<WorldNpcData> get_world_grid_roster(Vector2I cell, string locationNodeId = "")
    {
        var result = new Array<WorldNpcData>();
        foreach (var npc in npcs)
        {
            if (npc is null || !npc.is_alive || !npc.has_world_grid_position || !string.IsNullOrEmpty(npc.dungeon_id)) continue;
            var isNodeResident = !npc.is_traveling && !string.IsNullOrEmpty(locationNodeId) && npc.current_node_id == locationNodeId;
            var isTravelingInsideThisLocation = npc.is_traveling && !string.IsNullOrEmpty(locationNodeId) &&
                get_location_node_id_at_cell(npc.current_grid_cell) == locationNodeId;
            var isAtOrdinaryCell = string.IsNullOrEmpty(locationNodeId) && npc.current_grid_cell == cell;
            if (isNodeResident || isTravelingInsideThisLocation || isAtOrdinaryCell) result.Add(npc);
        }
        return result;
    }

    /// <summary>返回地点下的空间划分，供地点人物名单按子场景分组。</summary>
    public Array<WorldSubsceneData> get_node_subscenes(string node_id)
    {
        var result = new Array<WorldSubsceneData>();
        var node = _map_data?.get_node(node_id);
        if (node is null) return result;
        foreach (var subscene in node.subscenes)
            if (subscene is not null) result.Add(subscene);
        return result;
    }

    public string get_subscene_name(string node_id, string subscene_id)
        => get_node_subscenes(node_id).FirstOrDefault(subscene => subscene.subscene_id == subscene_id)?.subscene_name
           ?? (string.IsNullOrWhiteSpace(subscene_id) ? "地点内" : subscene_id);

    public string get_node_name(string node_id)
        => _map_data?.get_node(node_id)?.node_name
           ?? (string.IsNullOrWhiteSpace(node_id) ? "未定地点" : node_id);

    /// <summary>把人物放入一个已存在的节点/子场景；后续旅行系统也复用这一入口。</summary>
    public bool place_npc(string npc_id, string node_id, string subscene_id = "", string state = "空闲")
    {
        var npc = get_npc(npc_id);
        var node = _map_data?.get_node(node_id);
        if (npc is null || node is null) return false;
        if (!string.IsNullOrWhiteSpace(subscene_id) && node.subscenes.All(item => item is null || item.subscene_id != subscene_id)) return false;
        npc.current_node_id = node_id;
        npc.current_subscene_id = subscene_id;
        npc.travel_origin_node_id = "";
        npc.travel_destination_node_id = "";
        npc.travel_days_remaining = 0;
        npc.travel_days_total = 0;
        npc.is_traveling = false;
        npc.world_travel_path.Clear();
        npc.world_travel_next_cell_index = 1;
        npc.world_travel_cell_progress_hours = 0f;
        set_npc_grid_position(npc, get_location_anchor_cell(node_id));
        npc.current_state = state;
        npc.dungeon_id = "";
        npc.dungeon_node_id = "";
        npc.dungeon_previous_node_id = "";
        npc.dungeon_travel_progress = 0f;
        EmitSignal(SignalName.world_npcs_changed);
        return true;
    }

    public Array<NpcActionRecord> get_npc_history(string npc_id)
    {
        var result = new Array<NpcActionRecord>();
        var npc = get_npc(npc_id);
        if (npc is null) return result;
        foreach (var record in npc.action_history)
            if (record is not null) result.Add(record);
        return result;
    }

    /// <summary>节点名单包含该节点所有子场景；在途与死亡人物均不列入。</summary>
    public Array<WorldNpcData> get_node_roster(string node_id)
    {
        var result = new Array<WorldNpcData>();
        foreach (var npc in npcs)
            if (npc.is_alive && !npc.is_traveling && npc.current_node_id == node_id) result.Add(npc);
        return result;
    }

    /// <summary>子场景名单只返回实际在场的在世人物。</summary>
    public Array<WorldNpcData> get_subscene_roster(string node_id, string subscene_id)
    {
        var result = new Array<WorldNpcData>();
        foreach (var npc in npcs)
            if (npc.is_alive && !npc.is_traveling && string.IsNullOrWhiteSpace(npc.dungeon_id) && npc.current_node_id == node_id && npc.current_subscene_id == subscene_id) result.Add(npc);
        return result;
    }

    /// <summary>首次进入东林地路线时，把已在林缘的固定 NPC 纳入副本；再次进入不重置其位置。</summary>
    public void enter_east_woods_route()
    {
        var changed = false;
        foreach (var npc in npcs.Where(npc => npc is not null && npc.is_alive && !npc.is_traveling && npc.current_node_id == "east_woods"))
        {
            if (npc.dungeon_id == EastWoodsDungeonRules.DungeonId)
            {
                if (!string.IsNullOrWhiteSpace(npc.current_subscene_id))
                {
                    npc.current_subscene_id = "";
                    changed = true;
                }
                continue;
            }
            if (!string.IsNullOrWhiteSpace(npc.dungeon_id)) continue;
            npc.dungeon_id = EastWoodsDungeonRules.DungeonId;
            npc.dungeon_node_id = "forest_edge";
            npc.dungeon_previous_node_id = "";
            npc.dungeon_travel_progress = 0f;
            npc.dungeon_step_index = 0;
            npc.current_subscene_id = "";
            npc.dun_speed = Mathf.Max(0.01f, npc.dun_speed);
            npc.current_state = "深入东林地";
            changed = true;
        }
        if (changed) EmitSignal(SignalName.world_npcs_changed);
    }

    public Array<WorldNpcData> get_dungeon_node_roster(string dungeonId, string routeNodeId)
    {
        var result = new Array<WorldNpcData>();
        foreach (var npc in npcs)
            if (npc is not null && npc.is_alive && npc.dungeon_id == dungeonId && npc.dungeon_node_id == routeNodeId)
                result.Add(npc);
        return result;
    }

    /// <summary>玩家走过一条路线边后，按各自遁速累积 NPC 行程，并返回本次唯一遭遇。</summary>
    public Dictionary advance_east_woods_route(string playerFromNodeId, string playerRouteNodeId, float elapsedGameHours, HumanData? playerData)
    {
        var moved = new HashSet<string>();
        var previousNodes = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal);
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        var actionStamp = time?.get_total_game_hours() ?? 0;
        foreach (var npc in npcs.Where(npc => npc is not null && npc.is_alive && npc.dungeon_id == EastWoodsDungeonRules.DungeonId).OrderBy(npc => npc.npc_id, StringComparer.Ordinal))
        {
            previousNodes[npc.npc_id] = npc.dungeon_node_id;
            npc.dungeon_travel_progress += Mathf.Max(0f, elapsedGameHours);
            var edgeHours = EastWoodsDungeonRules.npc_edge_cost(npc.dun_speed);
            var stepsThisAction = 0;
            while (npc.dungeon_travel_progress + 0.0001f >= edgeHours && stepsThisAction < 8)
            {
                npc.dungeon_travel_progress -= edgeHours;
                var from = npc.dungeon_node_id;
                var to = choose_next_dungeon_node(npc);
                if (string.IsNullOrWhiteSpace(to)) break;
                npc.dungeon_previous_node_id = from;
                npc.dungeon_node_id = to;
                npc.dungeon_step_index++;
                npc.current_state = "林中行进";
                record_dungeon_action(npc, "dungeon_move", "林中行进", $"依遁速 {npc.dun_speed:0.#} 沿副本路线前往下一处。", $"由 {from} 抵达 {to}。", to, fromRouteNodeId: from, toRouteNodeId: to);
                moved.Add(npc.npc_id);
                stepsThisAction++;
            }
        }

        EmitSignal(SignalName.world_npcs_changed);
        if (playerData is not null)
        {
            foreach (var npc in npcs.Where(npc => npc is not null && npc.is_alive && npc.dungeon_id == EastWoodsDungeonRules.DungeonId).OrderBy(npc => npc.npc_id, StringComparer.Ordinal))
            {
                var crossedPlayer = previousNodes.TryGetValue(npc.npc_id, out var previousNode) &&
                    previousNode == playerRouteNodeId && npc.dungeon_node_id == playerFromNodeId;
                if (npc.dungeon_node_id != playerRouteNodeId && !crossedPlayer) continue;
                var chance = EastWoodsDungeonRules.attack_chance(
                    npc,
                    playerData.sect_affiliation_id,
                    RegionalRenownSystem.get_bai_chuan_total(playerData),
                    get_relationship_score(npc, "player", npc.player_relationship),
                    EastWoodsDungeonRules.realm_score(playerData.cultivation_realm));
                var progressStamp = Mathf.RoundToInt(npc.dungeon_travel_progress * 10000f);
                if (!EastWoodsDungeonRules.roll_attack($"{actionStamp}|{npc.npc_id}|player|{npc.dungeon_step_index}|{progressStamp}|{playerRouteNodeId}", chance)) continue;
                npc.current_state = "向玩家出手";
                record_dungeon_action(npc, "dungeon_attack", "突袭", $"性情、百川声望、势力态度、私人关系与境界共同影响出手判断；短测概率 {chance:P0}。", "在副本路线与玩家相遇后主动发起战斗。", npc.dungeon_node_id, "player");
                EmitSignal(SignalName.world_npcs_changed);
                return new Dictionary { ["attacker_npc_id"] = npc.npc_id, ["target_player"] = true, ["route_node_id"] = playerRouteNodeId };
            }
        }

        var local = npcs.Where(npc => npc is not null && npc.is_alive && npc.dungeon_id == EastWoodsDungeonRules.DungeonId)
            .OrderBy(npc => npc.npc_id, StringComparer.Ordinal).ToArray();
        for (var leftIndex = 0; leftIndex < local.Length; leftIndex++)
        for (var rightIndex = leftIndex + 1; rightIndex < local.Length; rightIndex++)
        {
            var first = local[leftIndex];
            var second = local[rightIndex];
            var sharedNode = first.dungeon_node_id == second.dungeon_node_id &&
                (moved.Contains(first.npc_id) || moved.Contains(second.npc_id));
            var crossedPaths = previousNodes.TryGetValue(first.npc_id, out var firstPrevious) &&
                previousNodes.TryGetValue(second.npc_id, out var secondPrevious) &&
                firstPrevious == second.dungeon_node_id && secondPrevious == first.dungeon_node_id &&
                moved.Contains(first.npc_id) && moved.Contains(second.npc_id);
            if (!sharedNode && !crossedPaths) continue;
            if (should_npc_attack(first, second, actionStamp, out var firstChance))
            {
                record_dungeon_action(first, "dungeon_attack", "突袭", $"与 {second.npc_name} 相遇；短测概率 {firstChance:P0}。", "沿途 NPC 冲突进入正式战斗。", first.dungeon_node_id, second.npc_id);
                return new Dictionary { ["attacker_npc_id"] = first.npc_id, ["target_npc_id"] = second.npc_id, ["target_player"] = false, ["route_node_id"] = first.dungeon_node_id };
            }
            if (should_npc_attack(second, first, actionStamp, out var secondChance))
            {
                record_dungeon_action(second, "dungeon_attack", "突袭", $"与 {first.npc_name} 相遇；短测概率 {secondChance:P0}。", "沿途 NPC 冲突进入正式战斗。", second.dungeon_node_id, first.npc_id);
                return new Dictionary { ["attacker_npc_id"] = second.npc_id, ["target_npc_id"] = first.npc_id, ["target_player"] = false, ["route_node_id"] = second.dungeon_node_id };
            }
        }
        return [];
    }

    public void apply_dungeon_battle_snapshot(BattleEntityData snapshot)
    {
        if (snapshot is null || !snapshot.uid.StartsWith("worldnpc:", StringComparison.Ordinal)) return;
        var npc = get_npc(snapshot.uid["worldnpc:".Length..]);
        if (npc is null || !npc.is_alive) return;
        npc.current_health = Mathf.Clamp(snapshot.current_health, 0, npc.max_health);
        if (npc.current_health <= 0)
        {
            var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
            mark_dead(npc, time is null ? 0 : month_index(time), "东林地副本冲突", "在副本遭遇战中伤重身亡；人物 ID 与死因保留在百川 NPC 存档。 ");
        }
        else
        {
            npc.current_state = "遭遇后负伤";
            record_dungeon_action(npc, "dungeon_battle", "副本冲突", "与另一名修行者发生战斗。", $"战后生命 {npc.current_health}/{npc.max_health}。", npc.dungeon_node_id);
        }
        EmitSignal(SignalName.world_npcs_changed);
    }

    public string remove_one_carried_item(WorldNpcData npc)
    {
        if (npc is null || npc.carried_items.Count == 0) return "";
        var choices = new List<string>();
        foreach (var key in npc.carried_items.Keys)
        {
            var path = key.AsString();
            if (npc.get_carried_item_count(path) > 0 && ResourceLoader.Load<ItemData>(path) is not null) choices.Add(path);
        }
        choices.Sort(StringComparer.Ordinal);
        if (choices.Count == 0) return "";
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        var seed = EastWoodsDungeonRules.stable_seed($"loot|{npc.npc_id}|{npc.dungeon_step_index}|{time?.get_total_game_hours() ?? 0}");
        var index = (int)(seed % (ulong)choices.Count);
        var selected = choices[index];
        npc.change_carried_item(selected, -1);
        EmitSignal(SignalName.world_npcs_changed);
        return selected;
    }

    public void add_carried_item(WorldNpcData npc, string itemPath, int count = 1)
    {
        if (npc is null || count <= 0 || ResourceLoader.Load<ItemData>(itemPath) is null) return;
        npc.change_carried_item(itemPath, count);
        EmitSignal(SignalName.world_npcs_changed);
    }

    public int transfer_all_carried_items(WorldNpcData from, WorldNpcData to)
    {
        if (from is null || to is null) return 0;
        var moved = 0;
        var paths = new List<string>();
        foreach (var key in from.carried_items.Keys) paths.Add(key.AsString());
        foreach (var path in paths)
        {
            var count = from.get_carried_item_count(path);
            if (count <= 0 || ResourceLoader.Load<ItemData>(path) is null) continue;
            from.change_carried_item(path, -count);
            to.change_carried_item(path, count);
            moved += count;
        }
        if (moved > 0) EmitSignal(SignalName.world_npcs_changed);
        return moved;
    }

    public void record_dungeon_battle_outcome(WorldNpcData npc, string targetId, string outcome, string itemSummary)
    {
        if (npc is null) return;
        var text = string.IsNullOrWhiteSpace(itemSummary) ? outcome : $"{outcome}；{itemSummary}";
        record_dungeon_action(npc, "dungeon_battle_result", "战斗结算", "依据正式战斗结果结算伤亡与随身物品。", text, npc.dungeon_node_id, targetId, !npc.is_alive);
        EmitSignal(SignalName.world_npcs_changed);
    }

    public Array<WorldNpcData> get_travelers()
    {
        var result = new Array<WorldNpcData>();
        foreach (var npc in npcs) if (npc.is_alive && npc.is_traveling) result.Add(npc);
        return result;
    }

    private void on_time_changed()
    {
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        if (time is null) return;
        var nowHour = time.get_total_game_hours();
        var targetMonth = month_index(time);
        if (!simulation_enabled)
        {
            _last_world_grid_hour = nowHour;
            return;
        }
        if (_last_world_grid_hour < 0 || _last_processed_month < 0)
        {
            _last_world_grid_hour = nowHour;
            _last_processed_month = targetMonth;
            return;
        }
        if (nowHour < _last_world_grid_hour || targetMonth < _last_processed_month)
        {
            // 读档/测试回拨历法时不倒放 NPC 行程，也不补演未来月份。
            _last_world_grid_hour = nowHour;
            _last_processed_month = targetMonth;
            return;
        }

        var cursorHour = _last_world_grid_hour;
        for (var month = _last_processed_month + 1; month <= targetMonth; month++)
        {
            var monthBoundaryHour = month * 30 * 24;
            if (monthBoundaryHour > cursorHour)
            {
                advance_world_grid_travel(monthBoundaryHour - cursorHour, cursorHour);
                cursorHour = monthBoundaryHour;
            }
            simulate_month(month);
            _last_processed_month = month;
        }
        if (nowHour > cursorHour)
            advance_world_grid_travel(nowHour - cursorHour, cursorHour);
        _last_world_grid_hour = nowHour;
    }

    private void advance_world_grid_travel(int elapsedHours, int segmentStartHour)
    {
        if (_tile_world_map is null || elapsedHours <= 0) return;
        var anyCellChanged = false;
        foreach (var npc in npcs
                     .Where(item => item is not null && item.is_alive && item.is_traveling && item.has_world_grid_position && string.IsNullOrEmpty(item.dungeon_id))
                     .OrderBy(item => item.npc_id, StringComparer.Ordinal))
        {
            var remainingHours = (double)elapsedHours;
            var usedHours = 0.0;
            var guard = 0;
            while (remainingHours > 0.0001 && npc.world_travel_next_cell_index < npc.world_travel_path.Count && guard++ < 40000)
            {
                var nextCell = npc.world_travel_path[npc.world_travel_next_cell_index];
                var edgeHours = get_npc_cell_travel_hours(nextCell, npc.dun_speed);
                if (!double.IsFinite(edgeHours) || edgeHours <= 0.0) break;
                var neededHours = Math.Max(0.0, edgeHours - npc.world_travel_cell_progress_hours);
                if (remainingHours + 0.0001 < neededHours)
                {
                    npc.world_travel_cell_progress_hours += (float)remainingHours;
                    usedHours += remainingHours;
                    remainingHours = 0.0;
                    break;
                }

                remainingHours -= neededHours;
                usedHours += neededHours;
                npc.current_grid_cell = nextCell;
                npc.world_travel_cell_progress_hours = 0f;
                npc.world_travel_next_cell_index++;
                anyCellChanged = true;
                if (npc.world_travel_next_cell_index >= npc.world_travel_path.Count)
                {
                    var arrivalHour = segmentStartHour + usedHours;
                    var arrivalMonth = Math.Max(0, (int)Math.Floor(arrivalHour / (30.0 * 24.0)));
                    complete_world_grid_travel(npc, arrivalMonth);
                    break;
                }
            }

            if (npc.is_traveling)
            {
                var remainingTripHours = get_remaining_world_travel_hours(npc);
                npc.travel_days_remaining = Math.Max(1, Mathf.CeilToInt((float)(remainingTripHours / 24.0)));
            }
        }
        if (anyCellChanged) EmitSignal(SignalName.world_npcs_changed);
    }

    private void complete_world_grid_travel(WorldNpcData npc, int month)
    {
        var origin = npc.travel_origin_node_id;
        var destination = npc.travel_destination_node_id;
        npc.current_node_id = destination;
        var destinationNode = _map_data?.get_node(destination);
        npc.current_subscene_id = destinationNode is null ? "" : choose_arrival_subscene(destinationNode, month, npc.npc_id);
        npc.current_state = "抵达";
        npc.is_traveling = false;
        npc.travel_origin_node_id = "";
        npc.travel_destination_node_id = "";
        var totalDays = Math.Max(1, npc.travel_days_total);
        npc.travel_days_remaining = 0;
        npc.travel_days_total = 0;
        npc.world_travel_path.Clear();
        npc.world_travel_next_cell_index = 1;
        npc.world_travel_cell_progress_hours = 0f;
        var subsceneNote = string.IsNullOrWhiteSpace(npc.current_subscene_id)
            ? ""
            : $"，进入 {get_subscene_name(destination, npc.current_subscene_id)}";
        record(npc, month, "arrive", "抵达", "按遁速沿固定格网完成逐格行旅。", $"抵达 {destination}{subsceneNote}。", 0, "", false, origin, destination, totalDays, 0);
    }

    private double get_remaining_world_travel_hours(WorldNpcData npc)
    {
        if (_tile_world_map is null || npc.world_travel_path.Count == 0) return 0.0;
        var totalHours = 0.0;
        for (var index = npc.world_travel_next_cell_index; index < npc.world_travel_path.Count; index++)
            totalHours += get_npc_cell_travel_hours(npc.world_travel_path[index], npc.dun_speed);
        return Math.Max(0.0, totalHours - npc.world_travel_cell_progress_hours);
    }

    private void initialize_world_grid_position(WorldNpcData npc)
    {
        if (_tile_world_map is null || npc is null) return;
        if (!npc.is_traveling)
        {
            if (!npc.has_world_grid_position || !is_valid_world_grid_cell(npc.current_grid_cell))
                set_npc_grid_position(npc, get_location_anchor_cell(npc.current_node_id));
            return;
        }

        if (npc.has_world_grid_position && is_valid_world_grid_cell(npc.current_grid_cell) &&
            npc.world_travel_path.Count > 1 && npc.world_travel_next_cell_index < npc.world_travel_path.Count)
            return;

        if (!npc.has_world_grid_position || !is_valid_world_grid_cell(npc.current_grid_cell))
            set_npc_grid_position(npc, get_location_anchor_cell(npc.travel_origin_node_id));
        if (!npc.has_world_grid_position) return;
        var destinationCell = get_location_anchor_cell(npc.travel_destination_node_id);
        var path = get_npc_grid_path(npc.current_grid_cell, destinationCell);
        if (path.Count > 1)
        {
            npc.world_travel_path.Clear();
            foreach (var cell in path) npc.world_travel_path.Add(cell);
            npc.world_travel_next_cell_index = 1;
            npc.world_travel_cell_progress_hours = 0f;
            var remainingDays = Math.Max(1, Mathf.CeilToInt((float)(get_remaining_world_travel_hours(npc) / 24.0)));
            npc.travel_days_remaining = remainingDays;
            npc.travel_days_total = Math.Max(npc.travel_days_total, remainingDays);
        }
    }

    private void set_npc_grid_position(WorldNpcData npc, Vector2I cell)
    {
        npc.has_world_grid_position = _tile_world_map is not null && is_valid_world_grid_cell(cell) &&
            _tile_world_map.Call("is_cell_traversable", cell).AsBool();
        npc.current_grid_cell = npc.has_world_grid_position ? cell : new Vector2I(-1, -1);
        if (!npc.is_traveling)
        {
            npc.world_travel_path.Clear();
            npc.world_travel_next_cell_index = 1;
            npc.world_travel_cell_progress_hours = 0f;
        }
    }

    private Vector2I get_location_anchor_cell(string nodeId)
    {
        if (_tile_world_map is null || string.IsNullOrWhiteSpace(nodeId)) return new Vector2I(-1, -1);
        return _tile_world_map.Call("get_location_anchor_cell", nodeId).AsVector2I();
    }

    private string get_location_node_id_at_cell(Vector2I cell)
        => _tile_world_map is null ? "" : _tile_world_map.Call("get_location_node_id_at_cell", cell).AsString();

    private Array<Vector2I> get_npc_grid_path(Vector2I fromCell, Vector2I toCell)
    {
        var result = new Array<Vector2I>();
        if (_tile_world_map is null) return result;
        var path = _tile_world_map.Call("get_npc_grid_path", fromCell, toCell).AsGodotArray<Vector2I>();
        foreach (var cell in path) result.Add(cell);
        return result;
    }

    private double get_npc_cell_travel_hours(Vector2I cell, float dunSpeed)
        => _tile_world_map is null ? 2400.0 / Math.Max(0.01, dunSpeed) : _tile_world_map.Call("get_npc_cell_travel_hours", cell, dunSpeed).AsDouble();

    private static bool is_valid_world_grid_cell(Vector2I cell)
        => cell.X >= 0 && cell.X < 200 && cell.Y >= 0 && cell.Y < 200;

    /// <summary>把遗漏的月份逐月结算；只由主动时间推进或测试调用。</summary>
    public void simulate_until_month(int target_month)
    {
        if (!simulation_enabled || target_month < 0) return;
        if (_last_processed_month < 0)
        {
            _last_processed_month = target_month;
            return;
        }
        // 读档或测试重置时间时，新的月份不应沿用旧世界的进度。
        if (target_month < _last_processed_month)
        {
            _last_processed_month = target_month;
            return;
        }
        for (var month = _last_processed_month + 1; month <= target_month; month++) simulate_month(month);
        _last_processed_month = target_month;
    }

    private void simulate_month(int month)
    {
        foreach (var npc in npcs)
        {
            if (!npc.is_alive) continue;
            update_age(npc, month);
            if (!npc.is_alive) continue;
            // 副本行程由同一遁速时间轴推进；不能在月演算里把人抽走到大世界地点。
            if (!string.IsNullOrWhiteSpace(npc.dungeon_id)) continue;
            resolve_month_action(npc, month);
        }
        EmitSignal(SignalName.world_npcs_changed);
    }

    private void update_age(WorldNpcData npc, int month)
    {
        if (month % 12 != 0) return;
        npc.current_age++;
        if (npc.current_age < npc.max_age) return;
        var rng = new RandomNumberGenerator { Seed = stable_seed(month, npc.npc_id, "lifespan") };
        var chance = Mathf.Clamp(.18f + (npc.current_age - npc.max_age) * .16f, .18f, .92f);
        if (rng.Randf() < chance) mark_dead(npc, month, "寿尽", "寿元已尽，生机自然断绝。");
    }

    private void resolve_month_action(WorldNpcData npc, int month)
    {
        var rng = new RandomNumberGenerator { Seed = stable_seed(month, npc.npc_id, "action") };
        if (npc.is_traveling)
        {
            advance_travel(npc, month);
            return;
        }

        var roll = rng.RandiRange(0, 99);
        if (roll < 28) cultivate(npc, month, rng);
        else if (roll < 53) work(npc, month, rng);
        else if (roll < 72) socialize(npc, month, rng);
        else if (roll < 88) explore(npc, month, rng);
        else travel(npc, month, rng);
    }

    private void cultivate(WorldNpcData npc, int month, RandomNumberGenerator rng)
    {
        var gain = npc.spirit_root_name == "无灵根" ? 0 : rng.RandiRange(1, 3);
        npc.spirit_strands += gain;
        npc.current_state = gain > 0 ? "修行中" : "调息";
        record(npc, month, "cultivate", npc.current_state,
            gain > 0 ? "以自身灵根与已学功法修行。" : "无灵根，改为调养气血。",
            gain > 0 ? $"灵力增加 {gain} 缕。" : "本月未积累灵力。", gain);
    }

    private void work(WorldNpcData npc, int month, RandomNumberGenerator rng, string reason = "")
    {
        var gain = rng.RandiRange(1, 4);
        npc.change_resource("灵石", gain);
        npc.current_state = "营生";
        record(npc, month, "work", "营生", string.IsNullOrWhiteSpace(reason) ? "依照身份、技能与所在地点寻找稳定收入。" : reason, $"获得灵石 {gain}。", gain);
    }

    private void socialize(WorldNpcData npc, int month, RandomNumberGenerator rng)
    {
        npc.current_state = "访人";
        var target = npcs.Where(other => other != npc && other.is_alive && !other.is_traveling && other.current_node_id == npc.current_node_id)
            .OrderBy(other => stable_seed(month, npc.npc_id, other.npc_id)).FirstOrDefault();
        var result = target is null ? "此处暂无可拜访的熟人，转为打听消息。" : $"与 {target.npc_name} 往来，交换近况与消息。";
        record(npc, month, "socialize", "访人", "根据性情与当前地点寻找可交往对象。", result, 0, target?.npc_id ?? "");
    }

    private void explore(WorldNpcData npc, int month, RandomNumberGenerator rng)
    {
        npc.current_state = "探索";
        var damage = rng.RandiRange(0, 12);
        npc.current_health = Mathf.Max(0, npc.current_health - damage);
        if (npc.current_health == 0)
        {
            mark_dead(npc, month, "战斗身亡", "探索途中遭遇战斗，伤势过重而亡。");
            return;
        }
        var gain = rng.RandiRange(0, 3);
        npc.change_resource("杂材", gain);
        record(npc, month, "explore", "探索", "基于目标、所在地点与可用体力进行探索。", damage > 0 ? $"受伤 {damage} 点，获得杂材 {gain}。" : $"平安归来，获得杂材 {gain}。", gain - damage);
    }

    private void travel(WorldNpcData npc, int month, RandomNumberGenerator rng)
    {
        var neighbors = get_available_travel_neighbors(npc.current_node_id);
        if (neighbors.Count == 0)
        {
            work(npc, month, rng, "当前地点没有开放且可通行的路线，本月改为营生。");
            return;
        }
        var destination = neighbors[rng.RandiRange(0, neighbors.Count - 1)];
        var route = _map_data?.get_route_between(npc.current_node_id, destination);
        var origin = npc.current_node_id;
        var travel_days = Mathf.Max(1, route?.travel_days ?? 1);
        if (_tile_world_map is not null)
        {
            initialize_world_grid_position(npc);
            var destinationCell = get_location_anchor_cell(destination);
            var path = get_npc_grid_path(npc.current_grid_cell, destinationCell);
            if (!is_valid_world_grid_cell(npc.current_grid_cell) || path.Count < 2)
            {
                work(npc, month, rng, "相邻地点在当前固定格网中没有可通行路径，本月改为营生。");
                return;
            }
            npc.world_travel_path.Clear();
            foreach (var cell in path) npc.world_travel_path.Add(cell);
            npc.world_travel_next_cell_index = 1;
            npc.world_travel_cell_progress_hours = 0f;
            npc.has_world_grid_position = true;
            var totalHours = get_remaining_world_travel_hours(npc);
            travel_days = Math.Max(1, Mathf.CeilToInt((float)(totalHours / 24.0)));
        }
        npc.travel_origin_node_id = npc.current_node_id;
        npc.is_traveling = true;
        npc.travel_destination_node_id = destination;
        npc.travel_days_remaining = travel_days;
        npc.travel_days_total = travel_days;
        npc.current_subscene_id = "";
        npc.current_state = "途中";
        record(npc, month, "travel", "启程", "沿已知且开放的相邻路线移动。", $"自 {npc.travel_origin_node_id} 前往 {destination}，预计 {travel_days} 日。", 0, "", false, npc.travel_origin_node_id, destination, travel_days, travel_days);
    }

    private void advance_travel(WorldNpcData npc, int month)
    {
        var origin = npc.travel_origin_node_id;
        var destination = npc.travel_destination_node_id;
        var total = Mathf.Max(1, npc.travel_days_total);
        if (_tile_world_map is not null)
        {
            npc.travel_days_remaining = Math.Max(1, Mathf.CeilToInt((float)(get_remaining_world_travel_hours(npc) / 24.0)));
            npc.current_state = "途中";
            record(npc, month, "travel_progress", "途中", "行程尚未完成，继续沿已知路线前行。", $"仍需 {npc.travel_days_remaining} 日抵达。", 0, "", false, origin, destination, total, npc.travel_days_remaining);
            return;
        }

        // 无固定格网的纯数据测试场景保留旧兼容路径；实际世界地图通过上面的逐格时间轴行进。
        npc.travel_days_remaining = Mathf.Max(0, npc.travel_days_remaining - 30);
        if (npc.travel_days_remaining > 0)
        {
            npc.current_state = "途中";
            record(npc, month, "travel_progress", "途中", "行程尚未完成，继续沿已知路线前行。", $"仍需 {npc.travel_days_remaining} 日抵达。", 0, "", false, origin, destination, total, npc.travel_days_remaining);
            return;
        }

        npc.current_node_id = destination;
        var destination_node = _map_data?.get_node(destination);
        npc.current_subscene_id = destination_node is null ? "" : choose_arrival_subscene(destination_node, month, npc.npc_id);
        npc.current_state = "抵达";
        npc.is_traveling = false;
        npc.travel_origin_node_id = "";
        npc.travel_destination_node_id = "";
        npc.travel_days_total = 0;
        npc.world_travel_path.Clear();
        npc.world_travel_next_cell_index = 1;
        npc.world_travel_cell_progress_hours = 0f;
        var subscene_note = string.IsNullOrWhiteSpace(npc.current_subscene_id)
            ? ""
            : $"，进入 {get_subscene_name(destination, npc.current_subscene_id)}";
        record(npc, month, "arrive", "抵达", "沿既定路线完成行程，并在目的地点选择可用的内部空间。", $"抵达 {destination}{subscene_note}。", 0, "", false, origin, destination, total, 0);
    }

    private void mark_dead(WorldNpcData npc, int month, string cause, string outcome)
    {
        npc.is_alive = false;
        npc.is_traveling = false;
        npc.travel_origin_node_id = "";
        npc.travel_destination_node_id = "";
        npc.travel_days_remaining = 0;
        npc.travel_days_total = 0;
        npc.world_travel_path.Clear();
        npc.world_travel_next_cell_index = 1;
        npc.world_travel_cell_progress_hours = 0f;
        npc.current_state = "已故";
        npc.death_cause = cause;
        record(npc, month, "death", "死亡", cause, outcome, 0, "", true);
    }

    private void record(WorldNpcData npc, int month, string actionId, string actionName, string reason, string outcome, int resourceDelta, string targetNpcId = "", bool death = false, string fromNodeId = "", string toNodeId = "", int travelDays = 0, int travelDaysRemaining = 0)
    {
        var record = new NpcActionRecord
        {
            month_index = month,
            date_label = format_month(month),
            action_id = actionId,
            action_name = actionName,
            node_id = npc.current_node_id,
            subscene_id = npc.current_subscene_id,
            from_node_id = fromNodeId,
            to_node_id = toNodeId,
            travel_days = travelDays,
            travel_days_remaining = travelDaysRemaining,
            target_npc_id = targetNpcId,
            reason = reason,
            outcome = outcome,
            resource_delta = resourceDelta,
            is_death_record = death,
        };
        npc.record(record);
        EmitSignal(SignalName.npc_action_recorded, npc.npc_id, record);
    }

    private Array<string> get_available_travel_neighbors(string node_id)
    {
        var result = new Array<string>();
        if (_map_data is null) return result;
        if (_tile_world_map is not null && !has_world_grid_anchor(node_id)) return result;
        foreach (var neighbor_id in _map_data.get_neighbors(node_id))
        {
            var neighbor = _map_data.get_node(neighbor_id);
            var route = _map_data.get_route_between(node_id, neighbor_id);
            if (neighbor is null || route is null || !route.unlocked) continue;
            if (neighbor.requires_unlock && !neighbor.unlocked) continue;
            if (_tile_world_map is not null && !has_world_grid_anchor(neighbor_id)) continue;
            if (!result.Contains(neighbor_id)) result.Add(neighbor_id);
        }
        return result;
    }

    /// <summary>抵达地点后自动选一个已解锁子场景；选择使用稳定种子，便于行动记录复现。</summary>
    private string choose_arrival_subscene(WorldNodeData node, int month, string npc_id)
    {
        var available = new Array<string>();
        foreach (var subscene in node.subscenes)
            if (subscene is not null && subscene.unlocked && !string.IsNullOrWhiteSpace(subscene.subscene_id))
                available.Add(subscene.subscene_id);
        if (available.Count == 0) return "";
        var rng = new RandomNumberGenerator { Seed = stable_seed(month, npc_id, $"arrival:{node.node_id}") };
        return available[rng.RandiRange(0, available.Count - 1)];
    }

    private bool normalize_npc_location(WorldNpcData npc)
    {
        if (_map_data is null) return true;
        var node = _map_data.get_node(npc.current_node_id);
        if (node is null)
        {
            GD.PushWarning($"npc_location_invalid npc={npc.npc_id} node={npc.current_node_id}");
            return false;
        }
        if (!string.IsNullOrWhiteSpace(npc.current_subscene_id) && node.subscenes.All(item => item is null || item.subscene_id != npc.current_subscene_id))
        {
            GD.PushWarning($"npc_subscene_missing npc={npc.npc_id} node={npc.current_node_id} subscene={npc.current_subscene_id}");
            npc.current_subscene_id = "";
        }
        initialize_world_grid_position(npc);
        return true;
    }

    private static string choose_next_dungeon_node(WorldNpcData npc)
    {
        var options = new List<string>(EastWoodsDungeonRules.get_neighbors(npc.dungeon_node_id));
        if (options.Count > 1 && options.Contains(npc.dungeon_previous_node_id)) options.Remove(npc.dungeon_previous_node_id);
        if (options.Count == 0) return npc.dungeon_previous_node_id;
        options.Sort(StringComparer.Ordinal);
        var seed = EastWoodsDungeonRules.stable_seed($"route|{npc.npc_id}|{npc.dungeon_step_index + 1}");
        return options[(int)(seed % (ulong)options.Count)];
    }

    private static bool should_npc_attack(WorldNpcData attacker, WorldNpcData target, long actionStamp, out float chance)
    {
        chance = EastWoodsDungeonRules.attack_chance(
            attacker,
            target.faction_id,
            target.get_regional_renown(RegionalRenownSystem.BaiChuanRegionId),
            get_relationship_score(attacker, target.npc_id),
            EastWoodsDungeonRules.realm_score(target.cultivation_realm));
        return EastWoodsDungeonRules.roll_attack(
            $"{actionStamp}|{attacker.npc_id}|{target.npc_id}|{attacker.dungeon_step_index}|{target.dungeon_step_index}|{Mathf.RoundToInt(attacker.dungeon_travel_progress * 10000f)}",
            chance);
    }

    private static int get_relationship_score(WorldNpcData npc, string targetNpcId, int fallback = 0)
    {
        var score = npc.personal_relationships.GetValueOrDefault(targetNpcId, fallback).AsInt32();
        if (npc.relation_tags.Contains("仇敌:" + targetNpcId)) score = Math.Min(score, -30);
        else if (npc.relation_tags.Contains("盟友:" + targetNpcId)) score = Math.Max(score, 30);
        else if (npc.relation_tags.Contains("熟识:" + targetNpcId)) score = Math.Max(score, 10);
        if (npc.action_history.Any(entry => entry is not null && entry.target_npc_id == targetNpcId && entry.action_id.StartsWith("dungeon_", StringComparison.Ordinal)))
            score = Math.Min(score, -10);
        return score;
    }

    private void record_dungeon_action(
        WorldNpcData npc,
        string actionId,
        string actionName,
        string reason,
        string outcome,
        string nodeId,
        string targetId = "",
        bool death = false,
        string fromRouteNodeId = "",
        string toRouteNodeId = "")
    {
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        var month = time is null ? 0 : month_index(time);
        var date = time is null ? format_month(month) : $"{time.year}年{time.month}月{time.day}日 {time.hour:D2}时";
        var entry = new NpcActionRecord
        {
            month_index = month,
            date_label = date,
            action_id = actionId,
            action_name = actionName,
            node_id = npc.current_node_id,
            subscene_id = string.IsNullOrWhiteSpace(npc.dungeon_id) ? npc.current_subscene_id : $"dungeon:{npc.dungeon_id}:{nodeId}",
            from_node_id = fromRouteNodeId,
            to_node_id = toRouteNodeId,
            target_npc_id = targetId,
            reason = reason,
            outcome = outcome,
            is_death_record = death,
        };
        npc.record(entry);
        EmitSignal(SignalName.npc_action_recorded, npc.npc_id, entry);
    }

    private static int month_index(GameTimeMgr time) => (time.year - 1) * 12 + time.month - 1;
    private static string format_month(int month) => $"{month / 12 + 1}年{month % 12 + 1}月";
    private static ulong stable_seed(int month, string first, string second)
    {
        unchecked
        {
            ulong hash = 1469598103934665603;
            foreach (var c in $"{month}|{first}|{second}") { hash ^= c; hash *= 1099511628211; }
            return hash;
        }
    }
}
