using Godot;
using Godot.Collections;

/// <summary>大世界节点、道路和临时地点的运行时数据容器。</summary>
public partial class WorldNodeMapData : Resource
{
    [Export] public string map_name = "";
    [Export] public Vector2 map_size = new(1100, 750);
    [Export] public Texture2D? background_texture;
    [Export] public Array<Texture2D> background_tiles = [];
    [Export] public Vector2I background_grid = new(1, 1);
    [Export] public Array<Texture2D> territory_id_mask_tiles = [];
    [Export] public Vector2I territory_id_mask_grid = new(1, 1);
    [Export] public Array<WorldTerritoryData> territories = [];
    [Export] public Array<WorldNodeData> nodes = [];
    [Export] public Array<WorldRouteData> routes = [];
    [Export] public Array<WorldNodeData> ephemeral_node_templates = [];
    [Export] public bool use_catalog = true;

    public Array<WorldNodeData> active_ephemeral_nodes = [];

    public Array<WorldNodeData> get_all_nodes()
    {
        var result = new Array<WorldNodeData>(nodes);
        result.AddRange(active_ephemeral_nodes);
        return result;
    }

    public WorldNodeData? spawn_ephemeral_node(string template_id, Vector2 position)
    {
        foreach (var template in ephemeral_node_templates)
        {
            if (template.node_id != template_id) continue;
            var spawned = template.Duplicate() as WorldNodeData;
            if (spawned is null) return null;
            spawned.node_id = $"{template_id}_{Time.GetTicksMsec()}";
            spawned.map_position = position;
            active_ephemeral_nodes.Add(spawned);
            return spawned;
        }
        return null;
    }

    public Array<WorldNodeData> get_ephemeral_templates_for(string territory_id, string terrain_tag)
    {
        var result = new Array<WorldNodeData>();
        foreach (var template in ephemeral_node_templates)
        {
            var territoryMatches = template.spawn_territory_ids.Count == 0 || template.spawn_territory_ids.Contains(territory_id);
            var terrainMatches = template.spawn_terrain_tags.Count == 0 || template.spawn_terrain_tags.Contains(terrain_tag);
            if (territoryMatches && terrainMatches) result.Add(template);
        }
        return result;
    }

    public void clear_ephemeral_nodes() => active_ephemeral_nodes.Clear();

    public WorldNodeData? get_node(string node_id)
    {
        foreach (var node in get_all_nodes())
            if (node.node_id == node_id) return node;
        return null;
    }

    public WorldTerritoryData? get_territory(string territory_id)
    {
        foreach (var territory in territories)
            if (territory.territory_id == territory_id) return territory;
        return null;
    }

    public Array<Dictionary> get_roads()
    {
        var roads = new Array<Dictionary>();
        var seen = new HashSet<string>();

        void add_road(string from_id, string to_id, WorldRouteData? route)
        {
            if (get_node(from_id) is null || get_node(to_id) is null) return;
            var key = string.CompareOrdinal(from_id, to_id) <= 0 ? $"{from_id}|{to_id}" : $"{to_id}|{from_id}";
            if (!seen.Add(key)) return;
            var road = new Dictionary
            {
                ["from"] = route is not null && !string.IsNullOrEmpty(route.from_node_id) ? route.from_node_id : from_id,
                ["to"] = route is not null && !string.IsNullOrEmpty(route.to_node_id) ? route.to_node_id : to_id,
            };
            if (route is not null)
            {
                road["route_id"] = route.route_id;
                road["route_name"] = route.route_name;
                road["description"] = route.description;
                road["travel_days"] = route.travel_days;
                road["unlocked"] = route.unlocked;
            }
            roads.Add(road);
        }

        foreach (var node in get_all_nodes())
        foreach (var targetId in node.connected_nodes)
            add_road(node.node_id, targetId, get_route_between(node.node_id, targetId));

        // Route data is authoritative for long-distance links. This fallback keeps a
        // route visible and clickable even when an authored node forgot the reciprocal
        // connected_nodes entry.
        foreach (var route in routes)
            add_road(route.from_node_id, route.to_node_id, route);

        return roads;
    }

    public Array<string> get_neighbors(string node_id)
    {
        var result = get_node(node_id) is { } node ? new Array<string>(node.connected_nodes) : new Array<string>();
        foreach (var route in routes)
        {
            var other = route.from_node_id == node_id ? route.to_node_id : route.to_node_id == node_id ? route.from_node_id : "";
            if (!string.IsNullOrEmpty(other) && !result.Contains(other)) result.Add(other);
        }
        return result;
    }

    public Vector2 get_road_midpoint(string from_id, string to_id)
    {
        var from = get_node(from_id);
        var to = get_node(to_id);
        return from is not null && to is not null ? (from.map_position + to.map_position) * 0.5f : Vector2.Zero;
    }

    public WorldRouteData? get_route(string route_id)
    {
        foreach (var route in routes)
            if (route.route_id == route_id) return route;
        return null;
    }

    /// <summary>返回当前已开放道路上的最短节点路径。</summary>
    public Array<string> get_shortest_node_path(string from_id, string to_id)
    {
        var result = new Array<string>();
        if (!try_get_shortest_path(from_id, to_id, out var node_path, out _)) return result;
        foreach (var node_id in node_path) result.Add(node_id);
        return result;
    }

    /// <summary>返回当前已开放道路上的最短路线段，顺序与节点路径一致。</summary>
    public Array<WorldRouteData> get_shortest_route_path(string from_id, string to_id)
    {
        var result = new Array<WorldRouteData>();
        if (!try_get_shortest_path(from_id, to_id, out _, out var route_path)) return result;
        foreach (var route in route_path) result.Add(route);
        return result;
    }

    /// <summary>按已开放路线的 travel_days 求最短可通行耗时；不可达返回 -1。</summary>
    public int get_shortest_travel_days(string from_id, string to_id)
    {
        if (!try_get_shortest_path(from_id, to_id, out _, out var route_path)) return -1;
        var days = 0;
        foreach (var route in route_path) days += Math.Max(1, route.travel_days);
        return days;
    }

    private bool try_get_shortest_path(string from_id, string to_id, out List<string> node_path, out List<WorldRouteData> route_path)
    {
        node_path = [];
        route_path = [];
        var start = get_node(from_id);
        var target = get_node(to_id);
        if (start is null || target is null || !is_travel_available(start) || !is_travel_available(target)) return false;
        if (from_id == to_id)
        {
            node_path.Add(from_id);
            return true;
        }

        var adjacency = new System.Collections.Generic.Dictionary<string, List<(string to, WorldRouteData route)>>();
        foreach (var node in get_all_nodes()) adjacency[node.node_id] = [];
        var seen_edges = new HashSet<string>();

        void add_edge(WorldRouteData route)
        {
            if (string.IsNullOrEmpty(route.from_node_id) || string.IsNullOrEmpty(route.to_node_id)) return;
            if (!adjacency.ContainsKey(route.from_node_id) || !adjacency.ContainsKey(route.to_node_id)) return;
            var first = string.CompareOrdinal(route.from_node_id, route.to_node_id) <= 0 ? route.from_node_id : route.to_node_id;
            var second = string.CompareOrdinal(route.from_node_id, route.to_node_id) <= 0 ? route.to_node_id : route.from_node_id;
            if (!seen_edges.Add($"{first}|{second}")) return;
            adjacency[route.from_node_id].Add((route.to_node_id, route));
            adjacency[route.to_node_id].Add((route.from_node_id, route));
        }

        foreach (var route in routes) add_edge(route);
        // connected_nodes remains a useful authoring fallback, but only a
        // matching route resource can make an edge traversable and give it a cost.
        foreach (var node in get_all_nodes())
        foreach (var neighbor_id in node.connected_nodes)
            if (get_route_between(node.node_id, neighbor_id) is { } route) add_edge(route);

        var distances = new System.Collections.Generic.Dictionary<string, int> { [from_id] = 0 };
        var previous_node = new System.Collections.Generic.Dictionary<string, string>();
        var previous_route = new System.Collections.Generic.Dictionary<string, WorldRouteData>();
        var unvisited = new HashSet<string>(adjacency.Keys);

        while (unvisited.Count > 0)
        {
            var current = "";
            var current_distance = int.MaxValue;
            foreach (var candidate in unvisited)
            {
                if (!distances.TryGetValue(candidate, out var distance) || distance >= current_distance) continue;
                current = candidate;
                current_distance = distance;
            }
            if (current == "") break;
            unvisited.Remove(current);
            if (current == to_id) break;
            foreach (var edge in adjacency[current])
            {
                var edge_node = get_node(edge.to);
                if (!edge.route.unlocked || edge_node is null || !is_travel_available(edge_node)) continue;
                var candidate_distance = current_distance + Math.Max(1, edge.route.travel_days);
                if (distances.TryGetValue(edge.to, out var known_distance) && candidate_distance >= known_distance) continue;
                distances[edge.to] = candidate_distance;
                previous_node[edge.to] = current;
                previous_route[edge.to] = edge.route;
            }
        }

        if (!distances.ContainsKey(to_id)) return false;
        var cursor = to_id;
        node_path.Add(cursor);
        while (cursor != from_id)
        {
            if (!previous_node.TryGetValue(cursor, out var previous) || !previous_route.TryGetValue(cursor, out var route)) return false;
            route_path.Insert(0, route);
            cursor = previous;
            node_path.Insert(0, cursor);
        }
        return true;
    }

    private static bool is_travel_available(WorldNodeData node)
        => !node.requires_unlock || node.unlocked;

    public WorldRouteData? get_route_between(string from_id, string to_id)
    {
        foreach (var route in routes)
        {
            var sameDirection = route.from_node_id == from_id && route.to_node_id == to_id;
            var reverseDirection = route.from_node_id == to_id && route.to_node_id == from_id;
            if (sameDirection || reverseDirection) return route;
        }
        return null;
    }
}
