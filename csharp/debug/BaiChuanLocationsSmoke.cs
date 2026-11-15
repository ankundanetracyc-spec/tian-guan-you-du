using Godot;
using System;
using System.Linq;

public partial class BaiChuanLocationsSmoke : Node
{
    public override void _Ready()
    {
        try
        {
            var map = GD.Load<WorldNodeMapData>("res://resources/world/bai_chuan/bai_chuan_locations_20261101.tres");
            if (map.nodes.Count != 123 || map.nodes.Select(n => n.node_id).Distinct().Count() != 123)
                throw new InvalidOperationException("地点数量或身份错误");
            if (map.get_node("home_village") is not null || map.get_node("guanshan_prefecture") is not null)
                throw new InvalidOperationException("旧图废案仍在地点目录");
            if (map.get_node("yuxu_xianzong_core") is null || map.get_node("east_woods") is null || map.get_node("bc_place_014") is null)
                throw new InvalidOperationException("新图地点未接入");
            var script = GD.Load<GDScript>("res://gdscripts/world/BaiChuanNodeGraph.gd");
            var graph = script.New().AsGodotObject();
            if (!graph.Call("load_layout").AsBool()) throw new InvalidOperationException("节点路网加载失败");
            foreach (var node in map.nodes)
            {
                var graphId = graph.Call("node_for_runtime_id", node.node_id).AsString();
                if (string.IsNullOrEmpty(graphId) || graph.Call("runtime_id_for_node",graphId).AsString() != node.node_id)
                    throw new InvalidOperationException($"地点身份不一致：{node.node_id}");
            }
            graph.Dispose();
            GD.Print("BAICHUAN_LOCATIONS_SMOKE_OK");
            GetTree().Quit();
        }
        catch (Exception ex) { GD.PushError("BAICHUAN_LOCATIONS_SMOKE_FAILED: " + ex); GetTree().Quit(1); }
    }
}
