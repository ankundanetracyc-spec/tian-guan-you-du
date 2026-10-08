using Godot;
using GDict = Godot.Collections.Dictionary;

/// <summary>
/// 世界地图的 C# 兼容桥接层。
/// 地图内容与 WorldMapViewport 运行视图已经合并在本场景；本类只保留
/// MainScene 现有调用的信号、状态和 NPC 数据同步，不再负责地图绘制。
/// </summary>
public partial class NodeWorldMap : Control
{
    [Signal] public delegate void node_double_clickedEventHandler(string node_id);
    [Signal] public delegate void node_hoveredEventHandler(string node_id);
    [Signal] public delegate void territory_hoveredEventHandler(string territory_id);
    [Signal] public delegate void route_hoveredEventHandler(string route_id);
    [Signal] public delegate void node_enter_requestedEventHandler(string node_id);
    [Signal] public delegate void return_requestedEventHandler();

    private WorldNodeMapData? _map_data;
    [Export]
    public WorldNodeMapData? map_data
    {
        get => _map_data;
        set
        {
            _map_data = value;
            if (IsInsideTree())
            {
                configure_renderer();
            }
        }
    }

    private Node? _renderer;
    private NpcWorldManager? _npc_world;
    private NpcTravelManager? _travel_mgr;
    private CellCharacterList? _cell_roster;
    private readonly Dictionary<string, int> _player_counts = [];
    private Godot.Collections.Array<GDict> _player_travelers = [];
    private string _current_node_id = "";
    private string _target_node_id = "";

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Pass;
        _renderer = GetNodeOrNull<Node>("WorldMapViewport");
        connect_renderer_signals();
        configure_renderer();
    }

    private CellCharacterList? get_cell_roster()
    {
        if (_cell_roster is not null && GodotObject.IsInstanceValid(_cell_roster)) return _cell_roster;
        _cell_roster = GetTree().GetFirstNodeInGroup("cell_character_list") as CellCharacterList;
        return _cell_roster;
    }

    private void connect_renderer_signals()
    {
        if (_renderer is null) return;
        _renderer.Connect("node_double_clicked", Callable.From<string>(on_renderer_node_double_clicked));
        _renderer.Connect("node_enter_requested", Callable.From<string>(on_renderer_node_enter_requested));
        _renderer.Connect("return_requested", Callable.From(on_renderer_return_requested));
    }

    private void configure_renderer()
    {
        if (_renderer is null) return;
        if (_map_data is not null) _renderer.Call("set_map_data", _map_data);
        _renderer.Call("set_player_location", _current_node_id);
        if (!string.IsNullOrEmpty(_target_node_id)) _renderer.Call("set_target_node", _target_node_id);
        push_player_counts();
        push_population_counts();
        _renderer.Call("set_player_travelers", _player_travelers);
    }

    private void on_renderer_node_double_clicked(string node_id) => EmitSignal(SignalName.node_double_clicked, node_id);
    private void on_renderer_node_enter_requested(string node_id) => EmitSignal(SignalName.node_enter_requested, node_id);
    private void on_renderer_return_requested() => EmitSignal(SignalName.return_requested);

    public bool is_node_unlocked(string node_id)
    {
        var node = _map_data?.get_node(node_id);
        return node is not null && (!node.requires_unlock || node.unlocked);
    }

    public void reset_view() => _renderer?.Call("reset_view");
    public void select_node(string node_id) => _renderer?.Call("select_node", node_id);
    /// <summary>单击地点只显示从当前位置出发的最短行程估算。</summary>
    public void show_node_preview(string node_id) => _renderer?.Call("show_node_preview", node_id);
    public void select_route(string route_id) => _renderer?.Call("select_route", route_id);
    public void select_territory(string territory_id) => _renderer?.Call("select_territory", territory_id);

    public void set_player_location(string node_id)
    {
        if (_map_data?.get_node(node_id) is null) return;
        _current_node_id = node_id;
        _renderer?.Call("set_player_location", node_id);
        get_cell_roster()?.show_world_npcs(_npc_world?.get_node_roster(node_id) ?? new Godot.Collections.Array<WorldNpcData>(), $"world:{node_id}");
    }

    public void hide_world_roster() => get_cell_roster()?.hide_world_npcs();

    public string get_player_location() => _current_node_id;

    public void set_target_node(string node_id)
    {
        _target_node_id = _map_data?.get_node(node_id) is not null ? node_id : "";
        _renderer?.Call("set_target_node", _target_node_id);
    }

    public string get_target_node() => _target_node_id;

    public void clear_target_node()
    {
        _target_node_id = "";
        _renderer?.Call("clear_target_node");
    }

    public void refresh_map() => _renderer?.Call("refresh_map");
    public void set_return_button_text(string text) => _renderer?.Call("set_return_button_text", text);

    public void set_travel_manager(NpcTravelManager mgr)
    {
        if (_travel_mgr is not null) _travel_mgr.travelers_changed -= refresh_counts;
        _travel_mgr = mgr;
        if (_travel_mgr is not null) _travel_mgr.travelers_changed += refresh_counts;
        refresh_counts();
    }

    public void set_npc_world_manager(NpcWorldManager? manager)
    {
        if (_npc_world is not null) _npc_world.world_npcs_changed -= refresh_counts;
        _npc_world = manager;
        if (_npc_world is not null) _npc_world.world_npcs_changed += refresh_counts;
        if (!string.IsNullOrWhiteSpace(_current_node_id))
            get_cell_roster()?.show_world_npcs(_npc_world?.get_node_roster(_current_node_id) ?? new Godot.Collections.Array<WorldNpcData>(), $"world:{_current_node_id}");
        push_population_counts();
    }

    public void refresh_counts()
    {
        push_player_counts();
        push_population_counts();
        _renderer?.Call("set_player_travelers", _player_travelers);
        if (!string.IsNullOrWhiteSpace(_current_node_id))
            get_cell_roster()?.show_world_npcs(_npc_world?.get_node_roster(_current_node_id) ?? new Godot.Collections.Array<WorldNpcData>(), $"world:{_current_node_id}");
    }

    public void set_player_node_count(string node_id, int count)
    {
        if (!string.IsNullOrEmpty(node_id)) _player_counts[node_id] = Mathf.Max(0, count);
        push_player_counts();
    }

    public void set_player_travelers(Godot.Collections.Array<GDict> travelers)
    {
        _player_travelers = (Godot.Collections.Array<GDict>)travelers.Duplicate(true);
        _renderer?.Call("set_player_travelers", _player_travelers);
    }

    public void add_traveler(string npc_name, string from_id, string to_id, float progress = 0)
        => _renderer?.Call("add_traveler", npc_name, from_id, to_id, progress);

    public void update_traveler(string npc_name, float progress)
        => _renderer?.Call("update_traveler", npc_name, progress);

    /// <summary>让地图上的队伍标记沿起止节点之间的实际道路折线移动。</summary>
    public void animate_traveler(string label, string from_id, string to_id, float duration = 1.0f)
        => _renderer?.Call("animate_traveler", label, from_id, to_id, duration);

    /// <summary>让队伍沿最短路径包含的多段道路连续移动。</summary>
    public void animate_traveler_path(string label, Godot.Collections.Array<string> node_ids, float duration = 1.0f)
        => _renderer?.Call("animate_traveler_path", label, node_ids, duration);

    public void remove_traveler(string npc_name)
        => _renderer?.Call("remove_traveler", npc_name);

    private void push_player_counts()
    {
        if (_renderer is null) return;
        var values = new GDict();
        foreach (var item in _player_counts) values[item.Key] = item.Value;
        _renderer.Call("set_player_counts", values);
    }

    private void push_population_counts()
    {
        if (_renderer is null || _map_data is null) return;
        var values = new GDict();
        foreach (var node in _map_data.get_all_nodes())
            values[node.node_id] = _npc_world?.get_node_roster(node.node_id).Count ?? 0;
        _renderer.Call("set_population_counts", values);
    }
}
