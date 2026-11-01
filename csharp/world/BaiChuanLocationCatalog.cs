using Godot;
using Godot.Collections;

/// <summary>Build the formal location catalogue from the reviewed Bai Chuan map.</summary>
public static class BaiChuanLocationCatalog
{
    private const string PlanPath = "res://docs/prototypes/百川宗门城镇资源规划数据_20261027.json";
    private const string LayoutPath = "res://textures/world/bai_chuan/acceptance_20261030/display_layout_v3.json";
    private const string IdPath = "res://resources/world/bai_chuan/bai_chuan_node_runtime_ids_20261031.json";

    public static WorldNodeMapData Build(WorldNodeMapData? existing)
    {
        var plan = Read(PlanPath);
        var layout = Read(LayoutPath);
        var ids = Read(IdPath);
        if (!plan.ContainsKey("nodes") || !layout.ContainsKey("locations") || ids.Count == 0)
            throw new System.InvalidOperationException("百川节点地点数据加载失败");

        var runtimeIds = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.Ordinal);
        foreach (var key in ids.Keys)
            runtimeIds[ids[key].AsString()] = key.AsString();

        var result = new WorldNodeMapData { map_name = "百川", map_size = new Vector2(3762, 3762), use_catalog = false };
        var positions = layout["locations"].AsGodotDictionary();
        foreach (var item in plan["nodes"].AsGodotArray())
        {
            var entry = item.AsGodotDictionary();
            var graphId = entry["id"].AsString();
            if (!runtimeIds.TryGetValue(graphId, out var id) || !positions.ContainsKey(graphId))
                throw new System.InvalidOperationException($"百川地点缺少身份或位置：{graphId}");

            // Keep authored interiors, unlock conditions and faction identities for matching locations.
            var previous = existing?.get_node(id);
            var node = previous?.Duplicate(true) as WorldNodeData ?? new WorldNodeData();
            node.node_id = id;
            node.node_name = entry["name"].AsString();
            node.node_type = TypeFor(entry["type"].AsString());
            var level = entry.GetValueOrDefault("level", 3).AsInt32();
            node.display_level = (WorldNodeData.DisplayLevel)System.Math.Clamp(level, 1, 3);
            var pos = positions[graphId].AsGodotDictionary()["drawPos"].AsGodotArray();
            node.map_position = new Vector2(pos[0].AsSingle(), pos[1].AsSingle());
            if (previous is null)
            {
                node.enterable = true;
                // No location-specific pressure or danger figures have been authored yet.
                node.spirit_pressure = 0;
                node.danger_level = 0;
                node.terrain = entry.GetValueOrDefault("terrainRegion", "").AsString();
                node.description = entry.GetValueOrDefault("note", entry.GetValueOrDefault("role", "")).AsString();
            }
            // The old direct location links describe a different map; travel uses the reviewed graph.
            node.connected_nodes.Clear();
            result.nodes.Add(node);
        }
        if (result.nodes.Count != positions.Count)
            throw new System.InvalidOperationException("百川地点目录与地图不一致");
        return result;
    }

    private static Dictionary Read(string path)
    {
        using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
        if (file is null) throw new System.InvalidOperationException($"无法打开百川地点文件：{path}");
        var data = Json.ParseString(file.GetAsText());
        if (data.VariantType != Variant.Type.Dictionary)
            throw new System.InvalidOperationException($"百川地点文件格式错误：{path}");
        return data.AsGodotDictionary();
    }

    private static WorldNodeData.NodeType TypeFor(string type) => type switch
    {
        "sect" => WorldNodeData.NodeType.Sect,
        "city" => WorldNodeData.NodeType.City,
        "wild" => WorldNodeData.NodeType.Wilderness,
        _ => WorldNodeData.NodeType.Village,
    };
}
