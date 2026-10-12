using Godot;
using System;
using System.Collections.Generic;

/// <summary>东林地路线与 NPC 遭遇的首轮短测规则；概率和节点基础耗时都不是最终平衡值。</summary>
public static partial class EastWoodsDungeonRules
{
    public sealed record NodeDefinition(string Id, string Title, string Description, Vector2I GridPosition, bool ShowTitleOnMap = false, int Terrain = 0, string Feature = "");

    public const string DungeonId = "east_woods_route";
    public const float BaseDunSpeed = 100.0f;
    public const float BaseHoursPerNode = 1.0f;
    public const float BaseAttackChance = 0.08f;
    public const float MaximumAttackChance = 0.55f;
    public const int PrototypeLootCount = 1;

    private static readonly NodeDefinition[] OriginalNodes =
    {
        new("forest_edge", "林缘", "日光仍能穿过疏林。浅足迹沿着林地边缘，前方可见被落叶遮住的溪沟。", new Vector2I(0, 2), true),
        new("leaf_creek", "断叶溪沟", "溪沟已经断流，叶片聚在沟底。数条林间小径从这里分开。", new Vector2I(1, 2), true),
        new("north_turn", "", "沿溪沟北侧绕行，林间视野逐渐收窄。", new Vector2I(1, 1)),
        new("creek_path", "溪石径", "潮湿石面留有新鲜兽爪印，溪声断续传来。", new Vector2I(2, 1)),
        new("stream_canopy", "", "枝叶压低了去路，溪声仍在前方断续传来。", new Vector2I(3, 1)),
        new("upper_join", "", "高处林径沿坡折向前方的会合处。", new Vector2I(4, 1)),
        new("root_path", "盘根径", "盘错的树根抬起地面，几处新折枝指向林内。", new Vector2I(2, 2)),
        new("root_hollow", "", "树根间形成一条窄隙，前方逐渐开阔。", new Vector2I(3, 2)),
        new("south_turn", "", "林径向南侧浅地绕开，草木间留有湿润水痕。", new Vector2I(1, 3)),
        new("fern_lowland", "蕨草低地", "低处蕨叶密生，草间有浅浅水痕，未见明显踩踏或折损。", new Vector2I(2, 3)),
        new("fallen_canopy", "", "一截倒木横在林间，绕过后能望见前方树冠重新合拢。", new Vector2I(3, 3)),
        new("lower_join", "", "低处浅沟绕回林径交会处。", new Vector2I(4, 3)),
        new("deep_meet", "深林会合", "几条林径在合拢的树冠下重新相接，可继续深入或沿侧径绕行。", new Vector2I(4, 2), true),
        new("mid_split", "", "树根与浅沟在此交错，前方又分出高低两条林径。", new Vector2I(5, 2)),
        new("upper_ridge", "上侧林径", "地势略高，树根间散落着湿润苔痕，视线能越过前方灌木。", new Vector2I(5, 1)),
        new("moss_bridge", "苔石径", "几块平石跨过浅沟，周围没有新鲜争斗痕迹。", new Vector2I(6, 1)),
        new("upper_approach", "", "上侧林径再次向下折回，前方树冠渐渐稀疏。", new Vector2I(7, 1)),
        new("shallow_gully", "下侧浅沟", "浅沟里积着薄薄的水，岸边草木被风压向同一侧。", new Vector2I(5, 3)),
        new("dry_pool", "干涸水洼", "洼底只剩潮湿泥纹，几道浅痕延伸向前方。", new Vector2I(6, 3)),
        new("lower_approach", "", "低处林径绕过湿地，向前方洼地靠拢。", new Vector2I(7, 3)),
        new("quiet_grove", "静冠洼地", "古树根系在洼地边缘交错成环，是本段巡线的复核点。", new Vector2I(7, 2), true),
    };

    private static readonly (string From, string To)[] OriginalEdges =
    [
        ("forest_edge", "leaf_creek"),
        ("leaf_creek", "north_turn"), ("north_turn", "creek_path"), ("creek_path", "stream_canopy"), ("stream_canopy", "upper_join"), ("upper_join", "deep_meet"),
        ("leaf_creek", "root_path"), ("root_path", "root_hollow"), ("root_hollow", "deep_meet"),
        ("leaf_creek", "south_turn"), ("south_turn", "fern_lowland"), ("fern_lowland", "fallen_canopy"), ("fallen_canopy", "lower_join"), ("lower_join", "deep_meet"),
        ("deep_meet", "mid_split"),
        ("mid_split", "upper_ridge"), ("upper_ridge", "moss_bridge"), ("moss_bridge", "upper_approach"), ("upper_approach", "quiet_grove"),
        ("mid_split", "shallow_gully"), ("shallow_gully", "dry_pool"), ("dry_pool", "lower_approach"), ("lower_approach", "quiet_grove"),
    ];

    private static NodeDefinition[] Nodes = build_outer_map();
    private static Dictionary<string, NodeDefinition> NodesById = Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
    private static Dictionary<string, List<string>> Neighbors = build_neighbors();

    public static IReadOnlyList<NodeDefinition> get_nodes() => Nodes;

    public static NodeDefinition? get_node(string nodeId)
        => NodesById.TryGetValue(nodeId, out var node) ? node : null;

    public static IReadOnlyList<string> get_neighbors(string nodeId)
        => Neighbors.TryGetValue(nodeId, out var values) ? values : Array.Empty<string>();

    private static Dictionary<string, List<string>> build_neighbors()
    {
        var result = Nodes.ToDictionary(node => node.Id, _ => new List<string>(), StringComparer.Ordinal);
        var edges = OriginalEdges.ToList();
        var byPosition = Nodes.ToDictionary(node => node.GridPosition);
        foreach (var node in Nodes)
            foreach (var offset in new[] { Vector2I.Right, Vector2I.Down })
                if (byPosition.TryGetValue(node.GridPosition + offset, out var adjacent) && (node.Id.StartsWith("outer_", StringComparison.Ordinal) || adjacent.Id.StartsWith("outer_", StringComparison.Ordinal)))
                    edges.Add((node.Id, adjacent.Id));
        foreach (var (from, to) in edges)
        {
            if (!NodesById.ContainsKey(from) || !NodesById.ContainsKey(to))
                throw new InvalidOperationException($"东林地路线引用了未定义节点：{from} → {to}");
            var fromPosition = NodesById[from].GridPosition;
            var toPosition = NodesById[to].GridPosition;
            if (Math.Abs(fromPosition.X - toPosition.X) + Math.Abs(fromPosition.Y - toPosition.Y) != 1)
                throw new InvalidOperationException($"东林地路线只能连接正交相邻格：{from} → {to}");
            result[from].Add(to);
            result[to].Add(from);
        }
        return result;
    }

    private static NodeDefinition[] build_outer_map()
    {
        // Fixed template, not rerolled on entry. The original 21 IDs/positions remain untouched.
        var nodes = OriginalNodes.ToList();
        var occupied = nodes.Select(n => n.GridPosition).ToHashSet();
        var gaps = new HashSet<Vector2I>();
        for (var y = 0; y < 10; y++) if (y != 2) gaps.Add(new Vector2I(0, y));
        foreach (var (x,y) in new (int,int)[] { (9,0),(10,0),(14,0),(15,0),(10,1),(14,1),(15,1),(10,2),(11,2),(15,2),(3,4),(4,4),(11,4),(3,5),(4,5),(11,5),(12,5),(7,6),(8,6),(7,7),(13,8),(14,8),(15,9) })
            gaps.Add(new Vector2I(x,y));
        for (var y = 0; y < 10; y++)
        for (var x = 0; x < 16; x++)
        {
            var position = new Vector2I(x,y);
            if (occupied.Contains(position) || gaps.Contains(position)) continue;
            var terrain = x >= 11 && y < 4 ? 2 : x >= 8 && y >= 5 && y <= 7 ? 5
                : x == 8 && y <= 4 ? 3 : (x + y * 2) % 7 == 0 ? 1 : 0;
            var description = terrain switch
            {
                1 => "疏林间露出一片草地，低矮草簇间可留意足迹与根材。",
                2 => "林间石坡渐多，背阴石缝与潮湿苔痕值得留意。",
                3 => "一段林溪穿过低地，可沿已标出的通路涉过。",
                5 => "浅洼常年阴湿，苔与蕨生在水痕边缘。",
                _ => "林径在树影间延伸，可留意附近兽迹与可采根材。",
            };
            nodes.Add(new NodeDefinition($"outer_{x:00}_{y:00}", "", description, position, Terrain: terrain));
        }
        if (nodes.Count != 128) throw new InvalidOperationException("东林地外围模板必须有128格。");
        return nodes.ToArray();
    }

    /// <summary>东林地副本跨越一个相连节点的耗时；统一按遁速缩放，不为单条边写固定时长。</summary>
    public static float hours_per_node(float dunSpeed)
        => BaseHoursPerNode * BaseDunSpeed / Mathf.Max(0.01f, dunSpeed);

    public static float player_edge_hours(float dunSpeed)
        => hours_per_node(dunSpeed);

    public static float npc_edge_cost(float dunSpeed)
        => hours_per_node(dunSpeed);

    /// <summary>将性格、行为、目标声望、势力态度、私人关系与境界差独立计入试运行概率。</summary>
    public static float attack_chance(
        WorldNpcData attacker,
        string targetFactionId,
        long targetRegionalRenown,
        int personalRelationship,
        int targetRealmScore)
    {
        if (attacker is null) return 0f;

        var chance = BaseAttackChance;
        if (!string.IsNullOrWhiteSpace(targetFactionId) && attacker.faction_id == targetFactionId)
            chance -= 0.06f;
        chance += attacker.personality switch
        {
            "多疑" => 0.12f,
            "果决" => 0.08f,
            "机敏" => 0.04f,
            "温厚" or "圆融" or "随和" => -0.04f,
            _ => 0f
        };
        if (attacker.behavior_tags.Contains("劫掠")) chance += 0.24f;
        if (attacker.behavior_tags.Contains("好斗")) chance += 0.16f;

        var attitude = get_attitude(attacker, targetFactionId);
        if (attitude >= 0)
            chance += attitude switch { 0 => 0.18f, 1 => 0.13f, 2 => 0.07f, 3 => 0f, 4 => -0.04f, 5 => -0.08f, _ => -0.12f };

        chance += personalRelationship switch
        {
            <= -30 => 0.18f,
            <= -10 => 0.10f,
            >= 30 => -0.16f,
            >= 10 => -0.08f,
            _ => 0f
        };

        if (targetRegionalRenown >= 300) chance -= 0.12f;
        else if (targetRegionalRenown >= 100) chance -= 0.07f;
        else if (targetRegionalRenown < 0) chance += 0.04f;

        var realmGap = realm_score(attacker.cultivation_realm) - targetRealmScore;
        if (realmGap >= 8) chance += 0.08f;
        else if (realmGap <= -8) chance -= 0.08f;
        return Mathf.Clamp(chance, 0.01f, MaximumAttackChance);
    }

    public static int realm_score(string realm)
    {
        if (string.IsNullOrWhiteSpace(realm) || realm.Contains("凡人")) return 0;
        if (realm.StartsWith("云息", StringComparison.Ordinal)) return 10 + parse_tier(realm);
        if (realm.StartsWith("玄基", StringComparison.Ordinal)) return 30 + parse_tier(realm) * 3;
        if (realm.StartsWith("金丹", StringComparison.Ordinal)) return 75 + parse_tier(realm) * 6;
        if (realm.StartsWith("神胎", StringComparison.Ordinal)) return 160 + parse_tier(realm) * 12;
        if (realm.StartsWith("天人", StringComparison.Ordinal)) return 400 + parse_tier(realm) * 20;
        return 0;
    }

    public static ulong stable_seed(string value)
    {
        unchecked
        {
            ulong hash = 1469598103934665603;
            foreach (var character in value) { hash ^= character; hash *= 1099511628211; }
            return hash == 0 ? 1UL : hash;
        }
    }

    public static bool roll_attack(string seed, float chance)
    {
        var rng = new RandomNumberGenerator { Seed = stable_seed(seed) };
        return rng.Randf() < Mathf.Clamp(chance, 0f, 1f);
    }

    private static int get_attitude(WorldNpcData npc, string factionId)
    {
        if (string.IsNullOrWhiteSpace(factionId) || !npc.faction_reputations.TryGetValue(factionId, out var value)) return -1;
        return Mathf.Clamp(value.AsInt32(), 0, 6);
    }

    private static int parse_tier(string realm)
    {
        var start = realm.Length - 1;
        while (start >= 0 && char.IsDigit(realm[start])) start--;
        return int.TryParse(realm[(start + 1)..], out var tier) ? Mathf.Clamp(tier, 0, 99) : 0;
    }
}
