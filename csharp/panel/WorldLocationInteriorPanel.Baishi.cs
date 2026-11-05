using Godot;

public partial class WorldLocationInteriorPanel
{
    public event System.Action<string>? BaishiLocationActionRequested;
    private Control? _baishiView;
    private Godot.Collections.Array<string> _baishiActions = ["notice.lecture", "notice.city", "notice.admission", "books.catalog"];

    private void show_baishi_city()
    {
        if (_baishiView is null)
        {
            _baishiView = GD.Load<PackedScene>("res://tscn/world/bai_chuan/BaishiCityOverview.tscn").Instantiate<Control>();
            AddChild(_baishiView);
            _baishiView.Connect("close_requested", Callable.From(close));
            _baishiView.Connect("subscene_selected", Callable.From<string>(id =>
            {
                _selected_subscene_id = id;
                refresh_current_roster();
                if (_node is not null) SubsceneEntered?.Invoke(_node.node_id, id);
            }));
            _baishiView.Connect("location_action_requested",
                Callable.From<string>(id => BaishiLocationActionRequested?.Invoke(id)));
        }
        var available = new Godot.Collections.Array<string>();
        foreach (var space in _node?.subscenes ?? [])
            if (space is not null && space.unlocked) available.Add(space.subscene_id);
        _baishiView.Call("configure_spaces", available);
        _baishiView.Call("configure_actions", _baishiActions);
        _baishiView.Call("open_overview");
        _generic_frame?.Hide();
        _east_woods_frame?.Hide();
        _sceneShade?.Hide();
        _baishiView.Show();
        refresh_current_roster();
    }

    public string get_baishi_space_id() =>
        _node?.node_id == "white_stone_town" && _baishiView?.IsVisibleInTree() == true
            ? _baishiView.Call("get_current_space_id").AsString() : "";

    public void set_baishi_actions(Godot.Collections.Array<string> actions)
    {
        _baishiActions = actions;
        _baishiView?.Call("configure_actions", actions);
    }

    public void show_baishi_space(WorldNodeData node, NpcWorldManager? world, string space)
    {
        if (node.node_id != "white_stone_town") return;
        if (_node?.node_id != node.node_id || _baishiView is null || _npc_world != world)
            show_for(node, world);
        _npcBackdrop?.Hide();
        _generic_frame?.Hide();
        _sceneShade?.Hide();
        Show();
        _baishiView?.Show();
        _baishiView?.Call("open_space", space);
        MoveToFront();
    }

    public bool return_one_level() =>
        _node?.node_id == "white_stone_town" && _baishiView?.IsVisibleInTree() == true &&
        _baishiView.Call("handle_back").AsBool();
}
