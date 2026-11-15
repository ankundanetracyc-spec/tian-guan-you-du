using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class BaiChuanFreeWorldFlowSmoke
{
    private async Task OldMountainNavigation()
    {
        AtAnchor(); _time.pause_time(); _world.set_simulation_enabled(false);
        var previous = GD.Load<GDScript>("res://gdscripts/world/BaiChuanNodeGraph.gd").New().AsGodotObject();
        previous.Set("use_free_world", true);
        Require(previous.Call("load_layout", "res://resources/world/bai_chuan/free_layout_3x3_20261104.json").AsBool(), "真实旧8000公里地形档来源可加载");
        string from = "", to = "";
        var oldPoint = Vector2.Zero;
        var oldHours = 0.0;
        foreach (var key in _graph.Get("edge_points").AsGodotDictionary().Keys)
        {
            var pair = key.AsString().Split('~');
            var points = previous.Call("points_between", pair[0], pair[1]).AsVector2Array();
            foreach (var fraction in new[] { .37, .5, .7 })
            {
                var prefix = TerrainPrefix(points, fraction);
                if (!_nav.Call("previous_is_walkable", prefix.Point).AsBool() || _nav.Call("is_walkable", prefix.Point).AsBool()) continue;
                from = pair[0]; to = pair[1]; oldPoint = prefix.Point; oldHours = HoursForCost(prefix.Cost); break;
            }
            if (from.Length > 0) break;
        }
        Require(from.Length > 0, "真实旧NPC位置处于新完整山体范围");
        var path = _graph.Call("points_between", from, to).AsVector2Array();
        var closest = Vector2.Zero;
        var distance = double.PositiveInfinity;
        var expectedCost = 0.0;
        var prefixCost = 0.0;
        for (var i = 1; i < path.Length; i++)
        {
            var point = Geometry2D.GetClosestPointToSegment(oldPoint, path[i - 1], path[i]);
            var squared = oldPoint.DistanceSquaredTo(point);
            if (squared < distance) { distance = squared; closest = point; expectedCost = prefixCost + SegmentCost(path[i - 1], point); }
            prefixCost += SegmentCost(path[i - 1], path[i]);
        }
        var fixture = new WorldNpcData
        {
            npc_id = "qa_old_mountain_npc", npc_name = "旧山体路线人物", current_node_id = "yuxu_xianzong_core", origin_node_id = "yuxu_xianzong_core",
            is_traveling = true, has_world_grid_position = true, current_subscene_id = "", dun_speed = 100,
            current_grid_cell = _world.node_world_cell_for_node_id(from),
            world_travel_path = new Godot.Collections.Array<Vector2I> { _world.node_world_cell_for_node_id(from), _world.node_world_cell_for_node_id(to) },
            world_travel_next_cell_index = 1, world_travel_cell_progress_hours = (float)oldHours,
        };
        _world.register_npc(fixture);
        var saved = _main!.capture_battle_world_snapshot()!;
        saved.node_world_state.Remove("navigation_profile"); saved.node_world_state.Remove("npc_navigation_profile");
        saved.node_world_state["terrain_speed_profile"] = _terrainData["profile_id"];
        await StartMain(DiskRead(saved, "old_complete_mountain_npc"));
        var npc = _world.get_npc(fixture.npc_id);
        Require(npc is not null, "旧NPC身份和行程恢复");
        Check(Math.Abs(npc!.world_travel_cell_progress_hours - HoursForCost(expectedCost)) < .003, "真实读盘将旧加权进度投影到最近绕山点");
        var tile = (Node)Field(_world, "_tile_world_map")!;
        var projected = tile.Call("project_map_position", npc.current_grid_cell, npc.world_travel_path, 1, npc.world_travel_cell_progress_hours, 0f, 100f).AsVector2();
        Check(_nav.Call("is_walkable", projected).AsBool() && projected.DistanceTo(closest) < .03, "迁移后的真实NPC不落山面");
        var once = npc.world_travel_cell_progress_hours;
        saved = _main!.capture_battle_world_snapshot()!;
        Check(saved.node_world_state["npc_navigation_profile"].AsString() == _world.get_navigation_profile(), "完整世界快照记录NPC导航迁移完成");
        await StartMain(DiskRead(saved, "old_complete_mountain_npc_second_load"));
        npc = _world.get_npc(fixture.npc_id);
        Check(npc is not null && Math.Abs(npc.world_travel_cell_progress_hours - once) < .001, "旧NPC二次完整读盘不重复投影和结算");
        previous.Dispose(); _time.resume_time();
    }
}
