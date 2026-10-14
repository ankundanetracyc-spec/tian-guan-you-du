using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using GDictionary = Godot.Collections.Dictionary;

public static partial class EastWoodsDungeonRules
{
    // One active East Woods graph per journey. Other templates must have their own instance state.
    public static string LayoutSeed { get; private set; } = "legacy";
    private static readonly Vector2I[] Directions = [Vector2I.Right, Vector2I.Down, Vector2I.Left, Vector2I.Up];
    private static readonly Vector2I[] ActionAnchors =
    [new(3,6), new(6,4), new(9,7), new(12,3), new(5,6), new(12,6), new(13,6), new(8,8)];

    public static void use_legacy_layout()
    {
        Nodes = build_outer_map();
        NodesById = Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
        Neighbors = build_neighbors();
        LayoutSeed = "legacy";
    }

    public static bool is_action_anchor(string nodeId)
        => ActionAnchors.Any(position => nodeId == $"outer_{position.X:00}_{position.Y:00}");

    public static void generate_layout(string seed)
    {
        if (string.IsNullOrWhiteSpace(seed)) throw new ArgumentException("副本种子不能为空。", nameof(seed));
        var rng = new RandomNumberGenerator { Seed = stable_seed(seed) };
        var cells = OriginalNodes.Select(n => n.GridPosition).ToHashSet();
        // Connect each existing action anchor to the original task circuit before growing the outskirts.
        foreach (var target in ActionAnchors)
        {
            var cursor = cells.OrderBy(p => Math.Abs(p.X-target.X)+Math.Abs(p.Y-target.Y)).ThenBy(p => p.Y).ThenBy(p => p.X).First();
            while (cursor != target)
            {
                var horizontal = cursor.X != target.X && (cursor.Y == target.Y || rng.Randf() < .5f);
                cursor += horizontal ? new Vector2I(Math.Sign(target.X-cursor.X),0) : new Vector2I(0,Math.Sign(target.Y-cursor.Y));
                cells.Add(cursor);
            }
        }
        while (cells.Count < 128)
        {
            var frontier = cells.SelectMany(p => Directions.Select(d => p+d))
                .Where(p => p.X > 0 && p.X < 16 && p.Y >= 0 && p.Y < 10 && !cells.Contains(p))
                .Distinct().OrderBy(p => p.Y).ThenBy(p => p.X).ToArray();
            if (frontier.Length == 0) throw new InvalidOperationException("副本生成没有可用边界。");
            cells.Add(frontier[(int)rng.RandiRange(0,frontier.Length-1)]);
        }
        var original = OriginalNodes.ToDictionary(n => n.GridPosition);
        var patches = new (Vector2I Center, int Terrain)[]
        {
            (new((int)rng.RandiRange(3,7),(int)rng.RandiRange(5,8)),6), // bamboo remains woodland habitat
            (new((int)rng.RandiRange(9,13),(int)rng.RandiRange(5,8)),5),
            (new((int)rng.RandiRange(11,14),(int)rng.RandiRange(1,3)),7),
        };
        Nodes = cells.OrderBy(p => p.Y).ThenBy(p => p.X).Select(p =>
        {
            if (original.TryGetValue(p,out var fixedNode)) return fixedNode;
            var terrain = rng.Randf() < .18f ? 1 : 0;
            foreach (var patch in patches)
                if (Math.Abs(p.X-patch.Center.X)+Math.Abs(p.Y-patch.Center.Y) <= 3) terrain = patch.Terrain;
            if (p.X == 8 && p.Y <= 4) terrain = 3;
            // Existing gather points keep their documented habitat even when nearby biomes change.
            if (p == new Vector2I(3,6) || p == new Vector2I(6,4)) terrain = terrain == 6 ? 6 : 0;
            if (p == new Vector2I(9,7)) terrain = 5;
            if (p == new Vector2I(12,3)) terrain = 7;
            var description = terrain switch
            {
                1 => "疏林间的草地，可留意足迹与根材。",
                3 => "林溪穿过低地，可沿标出的通路涉过。",
                5 => "浅洼阴湿，苔与蕨生在水痕边缘。",
                6 => "疏竹沿林径生长，竹影间留有开阔地面。",
                7 => "背阴岩隙露出平缓石面，细碎裂纹间留有水痕。",
                _ => "林径在树影间延伸，可留意附近兽迹。",
            };
            return new NodeDefinition($"outer_{p.X:00}_{p.Y:00}","",description,p,Terrain:terrain);
        }).ToArray();
        // A location feature is visual only: no unapproved healing, resource or movement modifiers.
        var spring = Array.FindIndex(Nodes,n => n.Id.StartsWith("outer_",StringComparison.Ordinal) && n.Terrain == 5);
        if (spring >= 0) Nodes[spring] = Nodes[spring] with { Feature="spring", Description=Nodes[spring].Description+" 石缝间渗出一汪清泉。" };
        Nodes = configure_resource_nodes(Nodes);
        NodesById = Nodes.ToDictionary(n => n.Id,StringComparer.Ordinal);
        Neighbors = build_neighbors();
        LayoutSeed = seed;
    }

    public static GDictionary get_layout_state()
    {
        var nodes = new Godot.Collections.Array();
        foreach (var n in Nodes)
            nodes.Add(new GDictionary { ["id"]=n.Id,["title"]=n.Title,["description"]=n.Description,
                ["x"]=n.GridPosition.X,["y"]=n.GridPosition.Y,["show_title"]=n.ShowTitleOnMap,["terrain"]=n.Terrain,["feature"]=n.Feature,
                ["resource_node_id"]=n.ResourceNodeId,["resource_category"]=(int)n.ResourceCategory });
        var edges = new Godot.Collections.Array();
        foreach (var n in Nodes)
            foreach (var neighbor in get_neighbors(n.Id))
                if (string.CompareOrdinal(n.Id,neighbor)<0) edges.Add(new Godot.Collections.Array { n.Id,neighbor });
        return new GDictionary { ["version"]=2,["seed"]=LayoutSeed,["nodes"]=nodes,["edges"]=edges };
    }

    public static void restore_layout(GDictionary state)
        => apply_layout(state, true);

    public static void validate_layout(GDictionary state)
        => apply_layout(state, false);

    private static void apply_layout(GDictionary state, bool activate)
    {
        if (state.Count == 0) { if (activate) use_legacy_layout(); return; }
        var version = state.GetValueOrDefault("version",0).AsInt32();
        if (version is not (1 or 2)) throw new InvalidOperationException("无法读取此版本的副本地图，原存档未修改。");
        var seed = state["seed"].AsString();
        if (string.IsNullOrWhiteSpace(seed)) throw new InvalidOperationException("副本种子缺失。");
        var nodes = state["nodes"].AsGodotArray().Select(v =>
        {
            var n=v.AsGodotDictionary();
            var resourceCategoryValue = version >= 2 ? n.GetValueOrDefault("resource_category", 0).AsInt32() : 0;
            if (!Enum.IsDefined(typeof(EastWoodsResourceCategory), resourceCategoryValue))
                throw new InvalidOperationException("副本资源节点类别无效，原存档未修改。");
            return new NodeDefinition(n["id"].AsString(),n["title"].AsString(),n["description"].AsString(),
                new Vector2I(n["x"].AsInt32(),n["y"].AsInt32()),n["show_title"].AsBool(),n["terrain"].AsInt32(),n.GetValueOrDefault("feature","").AsString(),
                version >= 2 ? n.GetValueOrDefault("resource_node_id", "").AsString() : "",
                (EastWoodsResourceCategory)resourceCategoryValue);
        }).ToArray();
        if (version == 1) nodes = configure_resource_nodes(nodes);
        if (nodes.Length!=128 || nodes.Select(n=>n.Id).Distinct().Count()!=128 || nodes.Select(n=>n.GridPosition).Distinct().Count()!=128)
            throw new InvalidOperationException("副本地图格数或唯一标识损坏，拒绝重抽地图。");
        var byId = nodes.ToDictionary(n=>n.Id,StringComparer.Ordinal);
        foreach (var anchor in OriginalNodes)
            if (!byId.TryGetValue(anchor.Id,out var n) || n.GridPosition!=anchor.GridPosition)
                throw new InvalidOperationException("副本原有任务节点不匹配。");
        foreach (var p in ActionAnchors)
            if (!byId.TryGetValue($"outer_{p.X:00}_{p.Y:00}",out var n) || n.GridPosition!=p)
                throw new InvalidOperationException("副本原有行动节点不匹配。");
        if (nodes.Any(n=>n.GridPosition.X<0 || n.GridPosition.X>=16 || n.GridPosition.Y<0 || n.GridPosition.Y>=10 || n.Terrain<0 || n.Terrain>7 || n.Feature is not ("" or "spring")))
            throw new InvalidOperationException("副本地格配置损坏。");
        validate_resource_nodes(nodes);
        var neighbors = nodes.ToDictionary(n=>n.Id,_=>new List<string>(),StringComparer.Ordinal);
        foreach (var value in state["edges"].AsGodotArray())
        {
            var edge=value.AsGodotArray();
            if (edge.Count!=2) throw new InvalidOperationException("副本连线格式损坏。");
            var a=edge[0].AsString(); var b=edge[1].AsString();
            if (!byId.TryGetValue(a,out var from) || !byId.TryGetValue(b,out var to) ||
                Math.Abs(from.GridPosition.X-to.GridPosition.X)+Math.Abs(from.GridPosition.Y-to.GridPosition.Y)!=1 || neighbors[a].Contains(b))
                throw new InvalidOperationException("副本连线并非有效正交邻接。");
            neighbors[a].Add(b); neighbors[b].Add(a);
        }
        foreach (var (a,b) in OriginalEdges)
            if (!neighbors[a].Contains(b)) throw new InvalidOperationException("副本原有巡查路线缺失。");
        var reached = new HashSet<string> { "forest_edge" }; var queue = new Queue<string>(); queue.Enqueue("forest_edge");
        while(queue.TryDequeue(out var id)) foreach(var next in neighbors[id]) if(reached.Add(next)) queue.Enqueue(next);
        if(reached.Count!=128) throw new InvalidOperationException("副本存在无法返回出口的孤立地格。");
        // Commit only after validation; never silently reroll a damaged save.
        if (activate) { Nodes=nodes; NodesById=byId; Neighbors=neighbors; LayoutSeed=seed; }
    }

    private static NodeDefinition[] configure_resource_nodes(NodeDefinition[] source)
    {
        var nodes = source.Select(node => node with
        {
            ResourceNodeId = "",
            ResourceCategory = EastWoodsResourceCategory.None,
        }).ToArray();

        void Set(string nodeId, string resourceNodeId, EastWoodsResourceCategory category)
        {
            var index = Array.FindIndex(nodes, node => node.Id == nodeId);
            if (index < 0) throw new InvalidOperationException($"副本资源点缺少固定节点：{nodeId}");
            nodes[index] = nodes[index] with { ResourceNodeId = resourceNodeId, ResourceCategory = category };
        }

        Set("fern_lowland", "common_herb", EastWoodsResourceCategory.Plant);
        Set("outer_03_06", "common_herb", EastWoodsResourceCategory.Plant);
        Set("outer_06_04", "common_herb", EastWoodsResourceCategory.Plant);
        Set("dry_pool", "common_moss", EastWoodsResourceCategory.Plant);
        Set("outer_09_07", "common_moss", EastWoodsResourceCategory.Plant);
        Set("outer_12_03", "common_moss", EastWoodsResourceCategory.Plant);

        var wetlandPositions = nodes.Where(node => node.Terrain == 5).Select(node => node.GridPosition).ToHashSet();
        var slowGrowth = nodes
            .Where(node => node.Id.StartsWith("outer_", StringComparison.Ordinal) && (node.Terrain is 0 or 1) &&
                node.ResourceNodeId.Length == 0 && !is_action_anchor(node.Id) && node.Feature == "" &&
                wetlandPositions.Any(position => Math.Abs(position.X - node.GridPosition.X) + Math.Abs(position.Y - node.GridPosition.Y) == 1))
            .OrderBy(node => node.GridPosition.Y).ThenBy(node => node.GridPosition.X)
            .FirstOrDefault();
        if (slowGrowth is null) throw new InvalidOperationException("东林地地图没有可配置雾铃花的湿润林缘地格。");
        Set(slowGrowth.Id, "mistbell_slow", EastWoodsResourceCategory.Plant);

        var mineralVein = nodes
            .Where(node => node.Id.StartsWith("outer_", StringComparison.Ordinal) && (node.Terrain is 2 or 7) &&
                node.ResourceNodeId.Length == 0 && !is_action_anchor(node.Id) && node.Feature != "spring")
            .OrderBy(node => node.Terrain == 2 ? 0 : 1)
            .ThenBy(node => node.GridPosition.Y).ThenBy(node => node.GridPosition.X)
            .FirstOrDefault();
        if (mineralVein is null) throw new InvalidOperationException("东林地地图没有可配置专用矿脉的岩地节点。");
        Set(mineralVein.Id, "spirit_stone_vein", EastWoodsResourceCategory.Mineral);
        return nodes;
    }

    private static void validate_resource_nodes(NodeDefinition[] nodes)
    {
        foreach (var node in nodes)
        {
            if (node.ResourceNodeId.Length == 0 && node.ResourceCategory == EastWoodsResourceCategory.None) continue;
            var definition = EastWoodsResourceNodes.Find(node.ResourceNodeId);
            if (definition is null || definition.Category != node.ResourceCategory)
                throw new InvalidOperationException($"副本资源节点类别与配置不匹配：{node.Id}");
        }

        foreach (var (resourceNodeId, expectedCount) in new[]
        {
            ("common_herb", 3), ("common_moss", 3), ("mistbell_slow", 1), ("spirit_stone_vein", 1),
        })
            if (nodes.Count(node => node.ResourceNodeId == resourceNodeId) != expectedCount)
                throw new InvalidOperationException($"副本资源节点数量不匹配：{resourceNodeId}");

        var plantPoint = nodes.Single(node => node.ResourceNodeId == "mistbell_slow");
        var wetlandPositions = nodes.Where(node => node.Terrain == 5).Select(node => node.GridPosition).ToHashSet();
        if (plantPoint.ResourceCategory != EastWoodsResourceCategory.Plant || plantPoint.Terrain is not (0 or 1) ||
            !wetlandPositions.Any(position => Math.Abs(position.X - plantPoint.GridPosition.X) + Math.Abs(position.Y - plantPoint.GridPosition.Y) == 1) ||
            is_action_anchor(plantPoint.Id))
            throw new InvalidOperationException("雾铃花节点不在可用湿润林缘。");

        var mineralPoint = nodes.Single(node => node.ResourceNodeId == "spirit_stone_vein");
        if (mineralPoint.ResourceCategory != EastWoodsResourceCategory.Mineral || mineralPoint.Terrain is not (2 or 7) ||
            mineralPoint.Feature == "spring" || is_action_anchor(mineralPoint.Id))
            throw new InvalidOperationException("专用灵石脉节点不在合法岩地。");
    }
}
