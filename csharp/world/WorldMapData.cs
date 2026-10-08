using Godot;
using Godot.Collections;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GenericDictionary = System.Collections.Generic.Dictionary<Godot.Vector2I, WorldMapData.HexCell>;

/// <summary>六边形大世界的运行时地形、地点和迷雾事件数据。</summary>
public partial class WorldMapData : Resource
{
    public enum TerrainType { PLAINS_GRASS, PLAINS_FOREST, PLAINS_WASTELAND, PLAINS_DESERT, WATER, MOUNTAIN, VILLAGE, SPECIAL }
    public enum SiteType { NONE, SETTLEMENT, RESOURCE, DUNGEON, FACTION }
    public enum MysteryType { RANDOM = -1, DUNGEON, BATTLE, ELITE, EVENT, RESOURCE }

    public partial class HexCell : RefCounted
    {
        public Vector2I coord;
        public int terrain;
        public bool explored;
        public string name = "";
        public string description = "";
        public int site_type = (int)SiteType.NONE;
        public string site_subtype = "";
        public string faction_id = "";
        public string faction_name = "";
        public int exploration_progress;
        public int exploration_required;
        public string resource_type = "";
        public int resource_amount;
        public int resource_stock;
        public float gather_hours = 8.0f;
        public string dungeon_scene = "";
        public string event_id = "";
        public Array<Dictionary> event_choices = [];
        public Array<Vector2I> road_neighbors = [];

        public HexCell() { }
        public HexCell(Vector2I p_coord, int p_terrain) { coord = p_coord; terrain = p_terrain; }
    }

    public partial class MysterySite : RefCounted
    {
        public Vector2I coord;
        public int type;
        public bool resolved;
        public string enemy_id = "";
        public int enemy_count = 1;
        public string event_id = "";
        public string resource_type = "";
        public int resource_amount;
        public string dungeon_scene = "";

        public MysterySite() { }
        public MysterySite(Vector2I p_coord, int p_type) { coord = p_coord; type = p_type; }
    }

    public static readonly GDictionary SITE_TYPE_NAMES = new() { [(int)SiteType.NONE] = "", [(int)SiteType.SETTLEMENT] = "聚集地", [(int)SiteType.RESOURCE] = "资源地", [(int)SiteType.DUNGEON] = "秘境", [(int)SiteType.FACTION] = "势力据点" };
    public static readonly GDictionary TERRAIN_NAMES = new() { [(int)TerrainType.PLAINS_GRASS] = "平原草地", [(int)TerrainType.PLAINS_FOREST] = "平原森林", [(int)TerrainType.PLAINS_WASTELAND] = "平原荒地", [(int)TerrainType.PLAINS_DESERT] = "平原沙漠", [(int)TerrainType.WATER] = "平原水域", [(int)TerrainType.MOUNTAIN] = "山地", [(int)TerrainType.VILLAGE] = "村庄", [(int)TerrainType.SPECIAL] = "特殊地块" };
    public static readonly GDictionary TERRAIN_MOVE_MULT = new() { [(int)TerrainType.PLAINS_GRASS] = 1.0f, [(int)TerrainType.PLAINS_FOREST] = 1.4f, [(int)TerrainType.PLAINS_WASTELAND] = 1.2f, [(int)TerrainType.PLAINS_DESERT] = 1.7f, [(int)TerrainType.WATER] = 99.0f, [(int)TerrainType.MOUNTAIN] = 2.6f, [(int)TerrainType.VILLAGE] = 0.6f, [(int)TerrainType.SPECIAL] = 1.4f };

    public int grid_radius = 8;
    public GenericDictionary cells = [];
    public Vector2I home_coord = Vector2I.Zero;
    private readonly System.Collections.Generic.Dictionary<Vector2I, MysterySite> _active_mysteries = [];
    private const int _max_mysteries = 6;

    public WorldMapData() { }
    public WorldMapData(int p_radius) { grid_radius = p_radius; }

    public static string get_resource_display_name(string resource_id) => resource_id switch { "mat_herb" or "herb" or "药草" or "凝血草" => "凝血草", "mat_ore" or "ore" or "矿石" => "矿石", "mat_stone" or "stone" or "石材" => "石材", "mat_hemp" or "hemp" or "山麻" => "山麻", _ => resource_id };
    public static string normalize_resource_id(string resource_id) => resource_id switch { "herb" or "药草" or "凝血草" => "mat_herb", "ore" or "矿石" => "mat_ore", "stone" or "石材" => "mat_stone", "hemp" or "山麻" => "mat_hemp", _ => resource_id };
    public static string get_site_type_name(int site_type) => SITE_TYPE_NAMES.TryGetValue(site_type, out var value) ? value.AsString() : "";

    public virtual void generate_terrain() { }
    public void add_cell(Vector2I coord, int terrain, string cell_name = "", bool explored = false) => cells[coord] = new HexCell(coord, terrain) { name = cell_name, explored = explored };
    public HexCell? get_cell(Vector2I coord) => cells.TryGetValue(coord, out var cell) ? cell : null;
    public bool is_site_exploration_complete(HexCell? cell) => cell is not null && cell.exploration_required > 0 && cell.exploration_progress >= cell.exploration_required;
    public bool add_site_exploration(Vector2I coord, int amount = 1)
    {
        var cell = get_cell(coord); if (cell is null || cell.exploration_required <= 0 || amount <= 0) return false;
        var wasComplete = is_site_exploration_complete(cell);
        cell.exploration_progress = Mathf.Clamp(cell.exploration_progress + amount, 0, cell.exploration_required);
        return !wasComplete && is_site_exploration_complete(cell);
    }
    public float get_move_hours(Vector2I coord, float footwork)
    {
        var cell = get_cell(coord); if (cell is null) return 99.0f;
        var mult = TERRAIN_MOVE_MULT.TryGetValue(cell.terrain, out var value) ? value.AsSingle() : 1.0f;
        return mult * 4.0f / Mathf.Max(footwork / 25.0f, 0.1f);
    }
    public void explore(Vector2I coord)
    {
        if (get_cell(coord) is { } current) current.explored = true;
        foreach (var neighbour in new[] { new Vector2I(coord.X + 1, coord.Y), new Vector2I(coord.X + 1, coord.Y - 1), new Vector2I(coord.X, coord.Y - 1), new Vector2I(coord.X - 1, coord.Y), new Vector2I(coord.X - 1, coord.Y + 1), new Vector2I(coord.X, coord.Y + 1) })
            if (get_cell(neighbour) is { } cell) cell.explored = true;
    }
    public Array<Vector2I> get_explored_cells() { var result = new Array<Vector2I>(); foreach (var pair in cells) if (pair.Value.explored) result.Add(pair.Key); return result; }
    public Array<Vector2I> get_all_cells() { var result = new Array<Vector2I>(); foreach (var coord in cells.Keys) result.Add(coord); return result; }
    public void set_road_connection(Vector2I a, Vector2I b) { var ac = get_cell(a); var bc = get_cell(b); if (ac is null || bc is null) return; if (!ac.road_neighbors.Contains(b)) ac.road_neighbors.Add(b); if (!bc.road_neighbors.Contains(a)) bc.road_neighbors.Add(a); }
    public bool has_road_connection(Vector2I a, Vector2I b) => get_cell(a)?.road_neighbors.Contains(b) ?? false;
    public bool cell_has_road(Vector2I coord) => get_cell(coord)?.road_neighbors.Count > 0;

    public Array<MysterySite> get_active_mysteries() { var result = new Array<MysterySite>(); foreach (var mystery in _active_mysteries.Values) if (!mystery.resolved) result.Add(mystery); return result; }
    public bool has_mystery_at(Vector2I coord) => _active_mysteries.TryGetValue(coord, out var mystery) && !mystery.resolved;
    public MysterySite? get_mystery_at(Vector2I coord) => _active_mysteries.TryGetValue(coord, out var mystery) ? mystery : null;
    public void resolve_mystery(Vector2I coord) { if (_active_mysteries.TryGetValue(coord, out var mystery)) { mystery.resolved = true; _active_mysteries.Remove(coord); } }
    public int spawn_mysteries(GArray explored, GArray all_cells)
    {
        var count = _max_mysteries - get_active_mysteries().Count; if (count <= 0) return 0;
        var candidates = new List<Vector2I>(); foreach (var item in all_cells) { var coord = item.AsVector2I(); var cell = get_cell(coord); if (cell is not null && cell.terrain != (int)TerrainType.VILLAGE && cell.terrain != (int)TerrainType.WATER && !_active_mysteries.ContainsKey(coord) && (cell.name == "" || cell.name == "???") && (explored.Contains(coord) || is_near_explored(coord, explored))) candidates.Add(coord); }
        for (var i = candidates.Count - 1; i > 0; i--) { var j = GD.RandRange(0, i); (candidates[i], candidates[j]) = (candidates[j], candidates[i]); }
        var types = new[] { MysteryType.DUNGEON, MysteryType.BATTLE, MysteryType.ELITE, MysteryType.EVENT, MysteryType.RESOURCE }; var spawned = 0;
        foreach (var coord in candidates.Take(count)) { var mystery = new MysterySite(coord, (int)types[GD.RandRange(0, types.Length - 1)]); randomize_mystery(mystery); _active_mysteries[coord] = mystery; spawned++; }
        return spawned;
    }
    private static bool is_near_explored(Vector2I coord, GArray explored) { foreach (var item in explored) { var other = item.AsVector2I(); if (Mathf.Abs(coord.X - other.X) <= 2 && Mathf.Abs(coord.Y - other.Y) <= 2) return true; } return false; }
    private static void randomize_mystery(MysterySite mystery)
    {
        switch ((MysteryType)mystery.type) {
            case MysteryType.DUNGEON: mystery.dungeon_scene = "res://csharp/explore/EastWoodsDungeon.cs"; break;
            case MysteryType.BATTLE: { var options = new[] { ("wild_boar", 1), ("forest_wolf", 2), ("giant_toad", 1) }; var v = options[GD.RandRange(0, options.Length - 1)]; mystery.enemy_id = v.Item1; mystery.enemy_count = v.Item2; break; }
            case MysteryType.ELITE: { var options = new[] { ("demon_ape", 1), ("poison_snake", 1) }; var v = options[GD.RandRange(0, options.Length - 1)]; mystery.enemy_id = v.Item1; mystery.enemy_count = v.Item2; break; }
            case MysteryType.EVENT: mystery.event_id = new[] { "injured_traveler", "strange_altar", "old_hermit" }[GD.RandRange(0, 2)]; break;
            case MysteryType.RESOURCE: { var options = new[] { ("mat_herb", 4), ("mat_ore", 3) }; var v = options[GD.RandRange(0, options.Length - 1)]; mystery.resource_type = v.Item1; mystery.resource_amount = v.Item2; break; }
        }
    }
}
