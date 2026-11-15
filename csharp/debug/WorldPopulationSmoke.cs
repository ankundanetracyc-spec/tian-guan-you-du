using Godot;
using System;
using System.Linq;

public partial class WorldPopulationSmoke : Node
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    public override async void _Ready()
    {
        try
        {
            var manager = GetNode<NpcWorldManager>("/root/NpcWorldManager");
            manager.clear_for_new_game();
            var mapData = GD.Load<WorldNodeMapData>("res://resources/world/yunqu_state_nodes.tres");
            var map = GD.Load<PackedScene>("res://tscn/world/TileWorldMapPresentation.tscn").Instantiate<Control>();
            AddChild(map); map.Hide();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            manager.set_world_map(mapData); manager.set_tile_world_map(map);
            var clock = GetNode<GameTimeMgr>("/root/GameTimeManager");
            var hour = clock.get_precise_game_hours();
            CloudStateNpcCatalog.activate(manager, mapData);
            int Origin(string id) => manager.npcs.Count(n => !n.is_yuxu_lineage_staff && n.origin_node_id == id);
            int AuthoredPopulation(string id)
            {
                var node = mapData.get_node(id)!;
                return CloudStateNpcCatalog.population_for(node, (int)node.display_level);
            }
            Check(Origin("yuxu_xianzong_core") == AuthoredPopulation("yuxu_xianzong_core"), "玉虚人口与现有地点配置不符");
            Check(Origin("fulan_temple") == AuthoredPopulation("fulan_temple"), "伏岚观人口与现有地点配置不符");
            Check(CloudStateNpcCatalog.population_for(new WorldNodeData(), 1) == 60 &&
                CloudStateNpcCatalog.population_for(new WorldNodeData(), 2) == 30, "不同地图显示层的人口原型错误");
            Check(Origin("east_woods") == 10, "东林地人口错误");
            var teachers = manager.npcs.Where(n => n.faction_id == "yuxu_xianzong" &&
                n.relation_tags.Contains("师承候选") && (n.cultivation_realm is "金丹后期" or "金丹圆满")).ToArray();
            var gateElder = manager.npcs.Single(n => n.relation_tags.Contains("山门接引"));
            var gateGuide = manager.npcs.Single(n => n.relation_tags.Contains("山门接引弟子"));
            Check(teachers.Length > 0 && teachers.All(n => n.npc_id.StartsWith("runtime_npc_") &&
                manager.get_npc(n.sect_assigned_guide_npc_id)?.sect_teacher_npc_id == n.npc_id) &&
                gateElder.npc_id.StartsWith("runtime_npc_") && gateGuide.npc_id.StartsWith("runtime_npc_"),
                "师长与山门接引须是世界中具备真实关联的随机人物");
            Check(CloudStateNpcCatalog.population_for(new WorldNodeData { node_type = WorldNodeData.NodeType.City }, 0) == 50, "普通城池规模错误");
            Check(CloudStateNpcCatalog.population_for(new WorldNodeData { node_type = WorldNodeData.NodeType.Village }, 0) == 12, "聚居地规模错误");
            Check(CloudStateNpcCatalog.population_for(new WorldNodeData { node_type = WorldNodeData.NodeType.Resource }, 0) == 6, "资源点规模错误");
            var travelers = manager.npcs.Where(n => n.is_traveling).ToArray();
            Check(travelers.Length > 0 && manager.get_ordinary_grid_population_counts().Count > 0, "开局野外仍无人");
            foreach (var n in travelers)
            {
                Check(n.world_travel_path.Count > 2 && n.world_travel_next_cell_index > 1 && n.world_travel_next_cell_index < n.world_travel_path.Count, "行旅不是实际路线中途");
                Check(n.current_grid_cell == n.world_travel_path[n.world_travel_next_cell_index - 1], "NPC坐标与路线不一致");
                Check(!n.is_yuxu_lineage_staff && !n.formal_identity.Contains("执事") && !n.formal_identity.Contains("护山"), "职能人物被抽走");
            }
            Check(clock.get_precise_game_hours() == hour, "初始化推进了玩家时间");
            var count = manager.npcs.Count;
            foreach (var n in teachers) n.is_alive = false;
            Check(!CloudStateNpcCatalog.ensure_population(manager, mapData) && manager.npcs.Count == count && teachers.All(n => !n.is_alive),
                "候选死亡后不得重抽、复活或补人");
            foreach (var n in teachers) n.is_alive = true;
            var original = manager.npcs.First(n => n.origin_node_id == "east_woods");
            var oldId = original.npc_id;
            original.npc_name = "原档保留人物"; original.is_alive = false;
            Check(!CloudStateNpcCatalog.ensure_population(manager, mapData) && manager.npcs.Count == count && !manager.get_npc(oldId)!.is_alive, "重进补人或复活死者");
            // Simulate one undersized legacy roster, including a real in-flight route and dead person.
            var existingTraveler = travelers[0];
            var beforeRoute = existingTraveler.world_travel_path.Duplicate();
            var beforeCell = existingTraveler.current_grid_cell;
            var beforeIndex = existingTraveler.world_travel_next_cell_index;
            var beforeProgress = existingTraveler.world_travel_cell_progress_hours;
            manager.npcs.Clear(); manager.npcs.Add(original); manager.npcs.Add(existingTraveler);
            manager.population_version = 0;
            CloudStateNpcCatalog.ensure_population(manager, mapData);
            Check(ReferenceEquals(manager.get_npc(oldId), original) && original.npc_name == "原档保留人物" && !original.is_alive, "补齐覆盖旧身份或死亡");
            Check(existingTraveler.current_grid_cell == beforeCell && existingTraveler.world_travel_next_cell_index == beforeIndex &&
                existingTraveler.world_travel_cell_progress_hours == beforeProgress && existingTraveler.world_travel_path.SequenceEqual(beforeRoute), "补齐重置已有行程");
            Check(manager.npcs.Select(n => n.npc_id).Distinct().Count() == manager.npcs.Count, "补齐ID冲突");
            Check(Origin("east_woods") == 10, "补齐没有按包含死者的原人口计数");
            var save = new PersistentGameSave { world_npcs = manager.get_persistent_npcs(), npc_population_version = manager.population_version };
            const string path = "user://world_population_smoke_temp.tres";
            Check(ResourceSaver.Save(save, path) == Error.Ok, "快照保存失败");
            var loaded = ResourceLoader.Load<PersistentGameSave>(path, cacheMode: ResourceLoader.CacheMode.Ignore);
            Check(loaded is not null && loaded.npc_population_version == CloudStateNpcCatalog.PopulationVersion, "人口版本未保存");
            manager.restore_persistent_state(loaded!.world_npcs, false, manager.get_last_processed_month(), savedPopulationVersion: loaded.npc_population_version);
            count = manager.npcs.Count;
            Check(!CloudStateNpcCatalog.ensure_population(manager, mapData) && manager.npcs.Count == count, "读档重复补齐");
            var restoredTraveler = manager.get_npc(existingTraveler.npc_id)!;
            Check(restoredTraveler.current_grid_cell == beforeCell && restoredTraveler.world_travel_next_cell_index == beforeIndex && restoredTraveler.world_travel_cell_progress_hours == beforeProgress, "读档丢失行旅进度");
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
            var oldTeacher = new WorldNpcData { npc_id = "yuxu_lineage_teacher_late", npc_name = "旧档师长", faction_id = "yuxu_xianzong",
                is_yuxu_lineage_staff = true, is_alive = false, cultivation_realm = "金丹后期", sect_assigned_guide_npc_id = "yuxu_lineage_guide_late" };
            oldTeacher.relation_tags.Add("师承候选");
            var oldGuide = new WorldNpcData { npc_id = "yuxu_lineage_guide_late", faction_id = "yuxu_xianzong", sect_teacher_npc_id = oldTeacher.npc_id };
            var oldElder = new WorldNpcData { npc_id = "yuxu_gate_elder", faction_id = "yuxu_xianzong" }; oldElder.relation_tags.Add("山门接引");
            var oldGateGuide = new WorldNpcData { npc_id = "yuxu_gate_guide", faction_id = "yuxu_xianzong" }; oldGateGuide.relation_tags.Add("山门接引弟子");
            manager.npcs.Clear(); manager.npcs.Add(oldTeacher); manager.npcs.Add(oldGuide); manager.npcs.Add(oldElder); manager.npcs.Add(oldGateGuide);
            manager.population_version = 1;
            Check(CloudStateNpcCatalog.ensure_population(manager, mapData) && manager.npcs.Count == 4 &&
                ReferenceEquals(manager.get_npc(oldTeacher.npc_id), oldTeacher) && !oldTeacher.is_alive &&
                oldTeacher.sect_assigned_guide_npc_id == oldGuide.npc_id && oldGuide.sect_teacher_npc_id == oldTeacher.npc_id,
                "旧固定编号人物、死亡状态和师门关系迁移时原样保留，不补人或重配");
            GD.Print($"WORLD_POPULATION PASS: {count} people, {travelers.Length} initial travelers; quotas, real wilderness coordinates, protected staff, no clock advance, legacy migration, stable IDs/deaths/routes, Resource save/load, no refill");
            manager.clear_for_new_game(); map.QueueFree();
            GetTree().Quit();
        }
        catch (Exception ex) { GD.PushError(ex.ToString()); GetTree().Quit(1); }
    }
}
