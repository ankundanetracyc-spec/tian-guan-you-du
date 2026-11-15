using Godot;
using System;
using System.Collections;
using System.Reflection;

public partial class BaiChuanNpcNodeSmoke : Node
{
    public override void _Ready()
    {
        try
        {
            var manager = new NpcWorldManager();
            AddChild(manager);
            manager.initialize_node_world_graph();
            var from = manager.node_world_cell_for_node_id("bc_place_001");
            var to = manager.node_world_cell_for_node_id("bc_place_002");
            if (from.X < 0 || to.X < 0) throw new Exception("node graph initialization failed");
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var path = (Godot.Collections.Array<Vector2I>)typeof(NpcWorldManager).GetMethod("get_npc_grid_path", flags)!.Invoke(manager, [from, to])!;
            if (path.Count < 2) throw new Exception("node graph path missing");
            var nodeCount = path.Count;
            var npc = new WorldNpcData {
                npc_id = "node_smoke", npc_name = "测试", is_alive = true,
                current_node_id = "yuxu_xianzong_core", current_grid_cell = from, has_world_grid_position = true,
                is_traveling = true, travel_origin_node_id = "yuxu_xianzong_core",
                travel_destination_node_id = "xuandu_tianfu_core", dun_speed = 100,
                world_travel_path = path, world_travel_next_cell_index = 1,
                travel_days_total = 30
            };
            manager.npcs.Add(npc);
            var hours = (double)typeof(NpcWorldManager).GetMethod("get_remaining_world_travel_hours", flags)!.Invoke(manager, [npc])!;
            if (!double.IsFinite(hours) || hours <= 0 || hours > 300) throw new Exception("route timing invalid: " + hours);
            var steps = (IEnumerable)typeof(NpcWorldManager).GetMethod("advance_world_grid_travel_steps", flags)!.Invoke(manager, [(int)Math.Ceiling(hours) + 1, 0])!;
            foreach (var _ in steps) { }
            if (npc.is_traveling || npc.current_node_id != "xuandu_tianfu_core" || npc.current_grid_cell != to)
                throw new Exception("NPC did not arrive on graph");
            GD.Print("BAICHUAN_NPC_TRAVEL_OK hours=", hours, " nodes=", nodeCount);
            manager.Free();
            GetTree().Quit();
        }
        catch (Exception e)
        {
            GD.PushError("BAICHUAN_NPC_TRAVEL_FAILED " + e);
            GetTree().Quit(1);
        }
    }
}
