using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>东林地正交节点地图。每个方形节点承载地貌与实际在场棋子。</summary>
public partial class EastWoodsDungeonPanel : Control
{
    public event Action? Closed;
    public event Action<string>? RouteNodeEntered;
    public event Action<string, string>? RouteStepped;
    public event Action? RouteTravelStarted;
    public event Action<string>? RouteTravelFinished;

    private readonly Dictionary<string, Button> _nodeButtons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Control> _pawnLayers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Label> _nodeTitles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Vector2> _pawnLayerSizeStamps = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<WorldNpcData>> _npcsByNode = new(StringComparer.Ordinal);
    private readonly HashSet<string> _pathEdges = new(StringComparer.Ordinal);
    private readonly HashSet<string> _pathNodes = new(StringComparer.Ordinal);
    private sealed class ConnectionVisual
    {
        public required Line2D Line;
        public bool IsPath;
    }
    private readonly Dictionary<string, ConnectionVisual> _connectionVisuals = new(StringComparer.Ordinal);
    private Control? _mapRoot;
    private Control? _connectionLayer;
    private Texture2D? _pawnTexture;
    private PanelContainer? _hoverPanel;
    private Label? _hoverTitle;
    private Label? _hoverDescription;
    private Godot.Timer? _hoverTimer;
    private string _hoveredNode = "";
    private PanelContainer? _routePanel;
    private Label? _pathSummary;
    private string _current = "forest_edge";
    private bool _insideDungeon;
    private string _selected = "forest_edge";
    private readonly HashSet<string> _visited = ["forest_edge"];
    private string _activeDutyId = "";
    private int _activeDutyStage;
    private bool _activeDutyReady;
    private float _playerDunSpeed = EastWoodsDungeonRules.BaseDunSpeed;
    private bool _isTraveling;
    private bool _npcEncounterBusy;
    private int _travelGeneration;
    private List<string> _pendingPath = [];
    private string _pendingTarget = "";
    private Vector2 _gridOrigin;
    private float _cellSize = 120f;
    private float _cellGap = 10f;
    private string _connectionLayoutStamp = "";
    private Godot.Collections.Dictionary? _cachedLayoutState;
    private string _cachedLayoutSeed = "";
    private string _actionBarStamp = "";
    private readonly Dictionary<string, (Color Edge, int Radius, bool Emphasized)> _nodeStyleStamps = new(StringComparer.Ordinal);
    private int _minGridX;
    private int _minGridY;
    private bool _routeStepUpdating;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        ZIndex = 210;
        // Both maps share the same non-destructive crop of the user's original PNG.
        _pawnTexture = ResourceLoader.Load<Texture2D>("res://textures/ui/world/player_pawn_marker_v6.tres");
        build();
        bind_npc_motion();
        Hide();
    }

    public void show_dungeon()
    {
        clear_npc_motion();
        finish_pawn_step();
        _npcEncounterBusy = false;
        _insideDungeon = true;
        _travelGeneration++;
        _isTraveling = false;
        close_action_details();
        Show();
        reset_map_camera();
        MoveToFront();
        CallDeferred(nameof(layout_map));
        refresh();
    }

    public void set_player_dun_speed(float dunSpeed)
    {
        _playerDunSpeed = Mathf.Max(0.01f, dunSpeed);
        if (Visible) refresh();
    }

    public void set_active_duty_context(string dutyId, int stage, bool ready = false)
    {
        _activeDutyId = dutyId;
        _activeDutyStage = Math.Max(0, stage);
        _activeDutyReady = ready;
        if (Visible) refresh();
    }

    public void close()
    {
        if (!can_leave()) return;
        clear_npc_motion();
        _insideDungeon = false;
        _travelGeneration++;
        _isTraveling = false;
        _pendingPath.Clear();
        _pendingTarget = "";
        clear_hover();
        close_action_details();
        Hide();
        Closed?.Invoke();
    }

    public string get_current_route_node_id() => _current;
    public bool is_route_traveling() => _isTraveling;

    public void set_npc_encounter_busy(bool busy)
    {
        if (_npcEncounterBusy == busy) return;
        _npcEncounterBusy = busy;
        if (Visible) refresh_action_bar();
    }

    public Godot.Collections.Dictionary get_route_state()
    {
        var visited = new Godot.Collections.Array<string>();
        foreach (var id in _visited.OrderBy(id => id, StringComparer.Ordinal)) visited.Add(id);
        var discovered = new Godot.Collections.Array<string>();
        foreach (var id in _discoveredNodes.OrderBy(id => id, StringComparer.Ordinal)) discovered.Add(id);
        if (_cachedLayoutState is null || _cachedLayoutSeed != EastWoodsDungeonRules.LayoutSeed)
        {
            _cachedLayoutState = EastWoodsDungeonRules.get_layout_state();
            _cachedLayoutSeed = EastWoodsDungeonRules.LayoutSeed;
        }
        return new Godot.Collections.Dictionary { ["current"] = _current, ["visited"] = visited, ["discovered"] = discovered, ["actions"] = _actionProgress.Duplicate(true), ["inside"] = _insideDungeon, ["layout"] = _cachedLayoutState };
    }

    public void restore_route_state(Godot.Collections.Dictionary state)
    {
        clear_npc_motion();
        finish_pawn_step();
        if (state.Count == 0) return;
        EastWoodsDungeonRules.restore_layout(state.GetValueOrDefault("layout", new Godot.Collections.Dictionary()).AsGodotDictionary());
        _cachedLayoutState = null;
        _cachedLayoutSeed = "";
        rebuild_map_nodes();
        _insideDungeon = state.GetValueOrDefault("inside", false).AsBool();
        _actionProgress = state.GetValueOrDefault("actions", new Godot.Collections.Dictionary()).AsGodotDictionary().Duplicate(true);
        var current = state.GetValueOrDefault("current", "forest_edge").AsString();
        if (EastWoodsDungeonRules.get_node(current) is null) current = "forest_edge";
        _current = current;
        _selected = current;
        _pendingPath.Clear();
        _pendingTarget = "";
        _visited.Clear();
        _visited.Add("forest_edge");
        var values = state.GetValueOrDefault("visited", new Godot.Collections.Array<string>());
        if (values.VariantType == Variant.Type.Array)
            foreach (var value in values.AsGodotArray())
                if (EastWoodsDungeonRules.get_node(value.AsString()) is not null) _visited.Add(value.AsString());
        _visited.Add(_current);
        restore_discovery(state);
        if (Visible) refresh();
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (!Visible || _isTraveling || _npcEncounterBusy || _travelPawn is not null || _actionBusy || _actionModal?.Visible == true || @event is not InputEventKey key || !key.Pressed || key.Echo) return;
        var direction = key.Keycode switch
        {
            Key.W or Key.Up => new Vector2I(0, -1),
            Key.S or Key.Down => new Vector2I(0, 1),
            Key.A or Key.Left => new Vector2I(-1, 0),
            Key.D or Key.Right => new Vector2I(1, 0),
            _ => Vector2I.Zero,
        };
        if (direction == Vector2I.Zero) return;

        var currentNode = EastWoodsDungeonRules.get_node(_current);
        if (currentNode is null) return;
        var neighbor = EastWoodsDungeonRules.get_neighbors(_current)
            .Select(EastWoodsDungeonRules.get_node)
            .FirstOrDefault(node => node is not null && node.GridPosition - currentNode.GridPosition == direction);
        if (neighbor is null) return;

        move_one_node(neighbor.Id);
        GetViewport().SetInputAsHandled();
    }

    private void build()
    {
        var background = new TextureRect
        {
            Name = "DungeonBackground",
            Texture = ResourceLoader.Load<Texture2D>("res://textures/world_locations/east_woods_ink_20261011/forest_background_v1.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var mist = new ColorRect { Color = new Color(.91f, .88f, .78f, .42f), MouseFilter = MouseFilterEnum.Ignore };
        mist.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(mist);

        build_map_viewport();
        _connectionLayer = new Control { Name = "Connections", MouseFilter = MouseFilterEnum.Ignore };
        _mapRoot!.AddChild(_connectionLayer);

        build_map_nodes();

        var back = make_button("查看出口", 132, 42);
        back.Name = "ReturnToLocation";
        back.SetAnchorsPreset(LayoutPreset.TopRight);
        back.Position = new Vector2(-174, 28);
        back.Pressed += locate_exit;
        AddChild(back);

        var notes = make_button("林间札记", 132, 42);
        notes.Name = "ExplorationNotes";
        notes.SetAnchorsPreset(LayoutPreset.TopRight);
        notes.Position = new Vector2(-174, 78);
        notes.Pressed += () => { if (!_isTraveling && !_actionBusy && _travelPawn is null) show_action_result("林间札记", exploration_notes()); };
        AddChild(notes);

        var footer = new PanelContainer
        {
            Name = "RoutePreview", MouseFilter = MouseFilterEnum.Ignore,
            AnchorLeft = 0.30f, AnchorRight = 0.70f,
            AnchorTop = 0.76f, AnchorBottom = 0.76f,
        };
        _routePanel = footer;
        footer.AddThemeStyleboxOverride("panel", WorldHudStyle.Paper());
        AddChild(footer);
        var footerMargin = new MarginContainer();
        footerMargin.AddThemeConstantOverride("margin_left", 18);
        footerMargin.AddThemeConstantOverride("margin_top", 10);
        footerMargin.AddThemeConstantOverride("margin_right", 18);
        footerMargin.AddThemeConstantOverride("margin_bottom", 10);
        footer.AddChild(footerMargin);
        var footerRows = new VBoxContainer();
        footerRows.AddThemeConstantOverride("separation", 4);
        footerMargin.AddChild(footerRows);
        _pathSummary = WorldHudStyle.Text("", 17);
        _pathSummary.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _pathSummary.HorizontalAlignment = HorizontalAlignment.Center;
        footerRows.AddChild(_pathSummary);
        build_hover();
        build_actions();

        Resized += layout_map;
        CallDeferred(nameof(layout_map));
    }

    private void rebuild_map_nodes()
    {
        if (_mapRoot is null) return;
        clear_hover();
        foreach (var button in _nodeButtons.Values) { _mapRoot.RemoveChild(button); button.QueueFree(); }
        _nodeButtons.Clear(); _pawnLayers.Clear(); _nodeTitles.Clear(); _nodeStyleStamps.Clear(); _pawnLayerSizeStamps.Clear();
        clear_connections(); _actionBarStamp = "";
        build_map_nodes();
        CallDeferred(nameof(layout_map));
    }

    private void build_map_nodes()
    {
        if (_mapRoot is null) return;
        foreach (var node in EastWoodsDungeonRules.get_nodes())
        {
            var button = new Button
            {
                Name = $"Node_{node.Id}",
                Text = "",
                FocusMode = FocusModeEnum.None,
                MouseFilter = MouseFilterEnum.Stop,
                ClipContents = true,
                MouseDefaultCursorShape = CursorShape.PointingHand,
            };
            button.Pressed += () => on_node_clicked(node.Id);
            button.GuiInput += handle_map_camera_input;
            button.MouseEntered += () => begin_hover(node.Id);
            button.MouseExited += clear_hover;
            _mapRoot.AddChild(button);
            _nodeButtons[node.Id] = button;

            var terrain = new TextureRect
            {
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                Name = "Terrain", Texture = load_terrain(node.Id),
                StretchMode = TextureRect.StretchModeEnum.Scale,
                MouseFilter = MouseFilterEnum.Ignore,
                Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://resources/shaders/dungeon_terrain_round.gdshader") },
            };
            terrain.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            terrain.OffsetLeft = terrain.OffsetTop = 3;
            terrain.OffsetRight = terrain.OffsetBottom = -3;
            button.AddChild(terrain);

            if (node.Feature == "spring")
            {
                var overlay = new TextureRect { Name="SpringFeature", Texture=DungeonTerrainVisuals.SpringOverlay,
                    ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize, StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,
                    MouseFilter=MouseFilterEnum.Ignore };
                overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
                overlay.AnchorLeft=overlay.AnchorTop=.15f; overlay.AnchorRight=overlay.AnchorBottom=.85f;
                button.AddChild(overlay);
            }

            var title = WorldHudStyle.Text("", 14);
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.Position = new Vector2(4, 4);
            title.Size = new Vector2(100, 22);
            button.AddChild(title);
            _nodeTitles[node.Id] = title;

            var pawns = new Control { Name = "Pawns", MouseFilter = MouseFilterEnum.Ignore, ClipContents = true };
            button.AddChild(pawns);
            _pawnLayers[node.Id] = pawns;
        }

    }

    private void layout_map()
    {
        if (_mapRoot is null) return;
        var view = Size;
        if (view.X < 100 || view.Y < 100) return;
        var nodes = EastWoodsDungeonRules.get_nodes();
        _minGridX = nodes.Min(node => node.GridPosition.X);
        _minGridY = nodes.Min(node => node.GridPosition.Y);
        var columns = nodes.Max(node => node.GridPosition.X) - _minGridX + 1;
        var rows = nodes.Max(node => node.GridPosition.Y) - _minGridY + 1;
        var gapRatio = 0.27f;
        _cellSize = Mathf.Clamp(view.Y * .14f, 90, 170);
        _cellGap = _cellSize * gapRatio;
        var totalWidth = columns * _cellSize + Mathf.Max(0, columns - 1) * _cellGap;
        var totalHeight = rows * _cellSize + Mathf.Max(0, rows - 1) * _cellGap;
        _gridOrigin = Vector2.Zero;
        _mapRoot.Size = new Vector2(totalWidth, totalHeight);

        foreach (var node in nodes)
        {
            var grid = node.GridPosition;
            var position = _gridOrigin + new Vector2((grid.X - _minGridX) * (_cellSize + _cellGap), (grid.Y - _minGridY) * (_cellSize + _cellGap));
            var button = _nodeButtons[node.Id];
            button.Position = position;
            button.Size = new Vector2(_cellSize, _cellSize);
            button.CustomMinimumSize = button.Size;
            ((ShaderMaterial)button.GetNode<TextureRect>("Terrain").Material).SetShaderParameter("tile_size", new Vector2(_cellSize - 6, _cellSize - 6));
            _nodeTitles[node.Id].Size = new Vector2(_cellSize - 8, 22);
            _pawnLayers[node.Id].Position = new Vector2(5, 24);
            _pawnLayers[node.Id].Size = new Vector2(_cellSize - 10, _cellSize - 29);
            _nodeTitles[node.Id].AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(Mathf.Clamp(_cellSize * 0.105f, 11, 15)));
        }
        refresh();
        update_map_camera();
    }

    private void refresh()
    {
        // RouteStepped synchronously advances time and NPCs. Their change signals
        // may request another refresh before move_one_node() finishes the step.
        // The caller refreshes once after all route callbacks have settled.
        if (_routeStepUpdating) return;
        if (_mapRoot is null || _connectionLayer is null) return;
        update_visibility();
        var visibleNodes = EastWoodsDungeonRules.get_nodes();
        _pathEdges.Clear();
        _pathNodes.Clear();
        for (var index = 1; index < _pendingPath.Count; index++)
        {
            _pathEdges.Add(edge_key(_pendingPath[index - 1], _pendingPath[index]));
            _pathNodes.Add(_pendingPath[index - 1]);
            _pathNodes.Add(_pendingPath[index]);
        }

        refresh_connections(visibleNodes, _pathEdges);

        foreach (var nodeNpcs in _npcsByNode.Values) nodeNpcs.Clear();
        var npcManager = GetNodeOrNull<NpcWorldManager>("/root/NpcWorldManager");
        if (npcManager is not null)
        {
            foreach (var npc in npcManager.npcs)
            {
                if (npc is null || !npc.is_alive || npc.dungeon_id != EastWoodsDungeonRules.DungeonId) continue;
                if (!_npcsByNode.TryGetValue(npc.dungeon_node_id, out var nodeNpcs))
                {
                    nodeNpcs = [];
                    _npcsByNode[npc.dungeon_node_id] = nodeNpcs;
                }
                nodeNpcs.Add(npc);
            }
            foreach (var nodeNpcs in _npcsByNode.Values)
                nodeNpcs.Sort((left, right) => string.CompareOrdinal(left.npc_id, right.npc_id));
        }

        foreach (var node in visibleNodes)
        {
            var button = _nodeButtons[node.Id];
            var discovered = is_node_discovered(node.Id);
            if (button.Visible != discovered) button.Visible = discovered;
            var nodeModulate = is_node_visible(node.Id) ? Colors.White : new Color(.48f, .51f, .48f);
            if (button.Modulate != nodeModulate) button.Modulate = nodeModulate;
            var isCurrent = node.Id == _current;
            var isSelected = node.Id == _selected;
            var inPath = _pathNodes.Contains(node.Id);
            var edge = isCurrent ? new Color("995d46") : isSelected || inPath ? new Color("99784b") : new Color("68776b");
            var radius = Mathf.RoundToInt(_cellSize * .07f);
            var emphasized = isCurrent || isSelected;
            var styleStamp = (edge, radius, emphasized);
            if (!_nodeStyleStamps.TryGetValue(node.Id, out var oldStamp) || oldStamp != styleStamp)
            {
                _nodeStyleStamps[node.Id] = styleStamp;
                button.AddThemeStyleboxOverride("normal", panel_style(new Color("ded8bf"), edge, radius, emphasized ? 3 : 1));
                button.AddThemeStyleboxOverride("hover", panel_style(new Color("e8e1cc"), new Color("995d46"), radius, 3));
                button.AddThemeStyleboxOverride("pressed", panel_style(new Color("c9cbb6"), new Color("995d46"), radius, 3));
            }
            var dutyTarget = _activeDutyId == "east_woods_patrol" && node.Id == (_activeDutyReady ? "forest_edge" : "quiet_grove");
            var title = dutyTarget ? (_activeDutyReady ? "出口 · 回宗回报" : "巡查 · 静冠洼地") : node.ShowTitleOnMap ? node.Title : "";
            if (is_node_visible(node.Id) && available_actions(node.Id).FirstOrDefault() is { } content)
                title += (title.Length > 0 ? " · " : "") + (content.Battle ? "兽踪" : content.ItemPath.Length > 0 ? "可采集" : "异状");
            var titleLabel = _nodeTitles[node.Id];
            if (titleLabel.Text != title) titleLabel.Text = title;
            var titleVisible = !string.IsNullOrWhiteSpace(title);
            if (titleLabel.Visible != titleVisible) titleLabel.Visible = titleVisible;

            var pawns = _pawnLayers[node.Id];
            List<WorldNpcData>? nodeNpcs = null;
            if (is_node_visible(node.Id) && _npcsByNode.TryGetValue(node.Id, out var foundNpcs) && foundNpcs.Count > 0)
                nodeNpcs = foundNpcs;
            if (!pawn_occupants_match(node.Id, pawns, isCurrent, nodeNpcs))
            {
                while (pawns.GetChildCount() > 0)
                {
                    var child = pawns.GetChild(0);
                    pawns.RemoveChild(child);
                    child.QueueFree();
                }
                var occupants = new List<(bool IsPlayer, WorldNpcData? Npc)>();
                if (isCurrent) occupants.Add((true, null));
                if (nodeNpcs is not null)
                    foreach (var npc in nodeNpcs)
                        if (!_npcMotions.ContainsKey(npc.npc_id)) occupants.Add((false, npc));
                _pawnLayerSizeStamps[node.Id] = pawns.Size;
                add_pawns(pawns, occupants);
            }
        }

        update_details();
        refresh_action_bar();
    }

    private bool pawn_occupants_match(string nodeId, Control layer, bool hasPlayer, List<WorldNpcData>? nodeNpcs)
    {
        if (!_pawnLayerSizeStamps.TryGetValue(nodeId, out var previousSize) || previousSize != layer.Size) return false;
        var expectedCount = hasPlayer ? 1 : 0;
        if (nodeNpcs is not null)
            foreach (var npc in nodeNpcs)
                if (!_npcMotions.ContainsKey(npc.npc_id)) expectedCount++;
        if (layer.GetChildCount() != expectedCount) return false;

        var childIndex = 0;
        if (hasPlayer && layer.GetChild(childIndex++).Name != "PlayerPawn") return false;
        if (nodeNpcs is not null)
            foreach (var npc in nodeNpcs)
            {
                if (_npcMotions.ContainsKey(npc.npc_id)) continue;
                var child = layer.GetChild(childIndex++);
                if (!child.HasMeta("npc_id") || child.GetMeta("npc_id").AsString() != npc.npc_id) return false;
            }
        return true;
    }

    private void refresh_connections(IReadOnlyList<EastWoodsDungeonRules.NodeDefinition> nodes, HashSet<string> pathEdges)
    {
        if (_connectionLayer is null) return;
        var layoutStamp = $"{_cellSize:R}:{_cellGap:R}";
        if (_connectionLayoutStamp != layoutStamp)
        {
            clear_connections();
            _connectionLayoutStamp = layoutStamp;
        }

        // Discovery is monotonic during play. Add only newly revealed links and update
        // the small route overlay in place; rebuilding every known edge on each step
        // made later travel progressively more expensive.
        foreach (var node in nodes)
        {
            if (!is_node_discovered(node.Id)) continue;
            foreach (var neighborId in EastWoodsDungeonRules.get_neighbors(node.Id))
            {
                if (string.CompareOrdinal(node.Id, neighborId) >= 0 || !is_node_discovered(neighborId)) continue;
                var neighbor = EastWoodsDungeonRules.get_node(neighborId);
                if (neighbor is null) continue;
                var key = edge_key(node.Id, neighborId);
                var isPath = pathEdges.Contains(key);
                if (!_connectionVisuals.TryGetValue(key, out var visual))
                {
                    var line = add_terrain_connection(node, neighbor, isPath);
                    if (line is not null) _connectionVisuals[key] = new ConnectionVisual { Line = line, IsPath = isPath };
                }
                else if (visual.IsPath != isPath)
                {
                    visual.IsPath = isPath;
                    visual.Line.Width = isPath ? 5 : Mathf.Max(2, _cellSize * 0.025f);
                    visual.Line.DefaultColor = isPath ? new Color("e6bd68") : new Color(0.22f, 0.23f, 0.19f, 0.72f);
                }
            }
        }
    }

    private void clear_connections()
    {
        if (_connectionLayer is not null)
            foreach (var child in _connectionLayer.GetChildren()) { _connectionLayer.RemoveChild(child); child.QueueFree(); }
        _connectionVisuals.Clear();
        _connectionLayoutStamp = "";
    }

    private void add_pawns(Control layer, List<(bool IsPlayer, WorldNpcData? Npc)> occupants)
    {
        if (occupants.Count == 0 || _pawnTexture is null) return;
        var width = layer.Size.X;
        var height = layer.Size.Y;
        if (width <= 0 || height <= 0) return;
        var seed = EastWoodsDungeonRules.stable_seed($"standing|{EastWoodsDungeonRules.LayoutSeed}|{layer.GetParent().Name}|" +
            string.Join("|", occupants.Select(o => o.IsPlayer ? "player" : o.Npc!.npc_id)));
        var rects = pack_pawns(new Vector2(width, height), occupants.Count, seed);

        for (var index = 0; index < occupants.Count; index++)
        {
            var (isPlayer, npc) = occupants[index];
            var token = create_pawn_token(isPlayer, npc, rects[index].Size);
            token.Position = rects[index].Position;
            layer.AddChild(token);
        }
    }

    internal static List<Rect2> pack_pawns(Vector2 bounds, int count, ulong seed)
    {
        var result = new List<Rect2>();
        if (count <= 0) return result;
        using var rng = new RandomNumberGenerator { Seed = seed };
        var h = Mathf.Min(80, Mathf.Min(bounds.Y * .88f,
            Mathf.Sqrt(bounds.X * bounds.Y * .38f / (count * .54f))));
        // Rejection packing keeps irregular spacing, with deterministic shrink/retry for crowds.
        while (true)
        {
            result.Clear();
            var size = new Vector2(h * .54f, h);
            for (var i = 0; i < count; i++)
            {
                var found = false;
                for (var attempt = 0; attempt < 256; attempt++)
                {
                    var position = count == 1 ? (bounds - size) * .5f :
                        new Vector2(rng.RandfRange(0, bounds.X - size.X), rng.RandfRange(0, bounds.Y - size.Y));
                    var candidate = new Rect2(position, size);
                    if (result.Any(rect => rect.Grow(h * .025f).Intersects(candidate))) continue;
                    result.Add(candidate);
                    found = true;
                    break;
                }
                if (!found) break;
            }
            if (result.Count == count) return result;
            h *= .9f;
        }
    }

    private Control create_pawn_token(bool isPlayer, WorldNpcData? npc, Vector2 size)
    {
        var token = new Control { CustomMinimumSize = size, Size = size, MouseFilter = MouseFilterEnum.Ignore, TooltipText = isPlayer ? "你" : npc?.npc_name ?? "修行者" };
        if (isPlayer) { token.Name = "PlayerPawn"; token.Visible = _travelPawn is null; }
        else if (npc is not null) { token.SetMeta("npc_id", npc.npc_id); token.Visible = !_npcMotions.ContainsKey(npc.npc_id); }
        var figure = new TextureRect
        {
            Texture = _pawnTexture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Position = new Vector2(0, 0),
            Size = size,
            MouseFilter = MouseFilterEnum.Ignore,
            Modulate = isPlayer ? Colors.White : new Color(0.88f, 0.91f, 0.89f, 0.94f),
            FlipH = isPlayer ? _playerFacingLeft : npc is not null && _npcFacingLeft.GetValueOrDefault(npc.npc_id),
        };
        token.AddChild(figure);
        return token;
    }

    private void update_details()
    {
        if (_pathSummary is null || _routePanel is null) return;
        _routePanel.Visible = _isTraveling || _pendingPath.Count > 1;
        if (_pendingPath.Count <= 1)
        {
            _pathSummary.Text = _isTraveling
                ? "行进中……"
                : "";
            return;
        }
        var steps = _pendingPath.Count - 1;
        var hours = steps * EastWoodsDungeonRules.player_edge_hours(_playerDunSpeed);
        _pathSummary.Text = $"最短路径：{steps} 格　·　预计耗时：{format_hours(hours)}\n再次点击目标地格启程";
    }

    private void on_node_clicked(string nodeId)
    {
        if (!is_node_discovered(nodeId)) return;
        if (_isTraveling || _npcEncounterBusy || _travelPawn is not null || _actionBusy || _actionModal?.Visible == true || nodeId == _current) return;
        if (_pendingTarget == nodeId && _pendingPath.Count > 1)
        {
            start_pending_path();
            return;
        }

        var path = find_shortest_path(_current, nodeId);
        if (path.Count < 2) return;
        _selected = nodeId;
        _pendingTarget = nodeId;
        _pendingPath = path;
        refresh();
    }

    private void start_pending_path()
    {
        if (_isTraveling || _npcEncounterBusy || _travelPawn is not null || _pendingPath.Count < 2) return;
        var path = _pendingPath.ToArray();
        // Route simulation updates NPC positions edge by edge. Drop any in-flight
        // NPC presentation first so it cannot trigger extra map refreshes while
        // the player's continuous route is already refreshing each edge.
        clear_npc_motion();
        var generation = ++_travelGeneration;
        _isTraveling = true;
        RouteTravelStarted?.Invoke();
        _pendingPath.Clear();
        _pendingTarget = "";
        refresh();
        travel_along_path(path, generation);
    }

    private async void travel_along_path(string[] path, int generation)
    {
        for (var index = 1; index < path.Length; index++)
        {
            if (generation != _travelGeneration || !GodotObject.IsInstanceValid(this) || !IsInsideTree() || !Visible) break;
            move_one_node(path[index]);
            if (generation != _travelGeneration || !GodotObject.IsInstanceValid(this) || !IsInsideTree() || !Visible) break;
            await ToSignal(GetTree().CreateTimer(PawnStepSeconds), SceneTreeTimer.SignalName.Timeout);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        if (!GodotObject.IsInstanceValid(this) || !IsInsideTree() || generation != _travelGeneration) return;
        _isTraveling = false;
        _selected = _current;
        refresh();
        RouteTravelFinished?.Invoke(_current);
    }

    private void move_one_node(string nodeId)
    {
        if (_npcEncounterBusy || _travelPawn is not null || _actionBusy || _actionModal?.Visible == true || CanMove?.Invoke() == false) return;
        if (!EastWoodsDungeonRules.get_neighbors(_current).Contains(nodeId)) return;
        if (!_isTraveling)
        {
            _pendingPath.Clear();
            _pendingTarget = "";
        }
        var previous = _current;
        var direction = EastWoodsDungeonRules.get_node(nodeId)!.GridPosition - EastWoodsDungeonRules.get_node(previous)!.GridPosition;
        if (direction.X != 0) _playerFacingLeft = direction.X < 0;
        _travelCameraPosition = node_center(EastWoodsDungeonRules.get_node(previous)!);
        var source = _pawnLayers[previous].GetNodeOrNull<Control>("PlayerPawn");
        var sourcePosition = source is null ? node_center(EastWoodsDungeonRules.get_node(previous)!) : pawn_map_position(source);
        var generation = _travelGeneration;
        _current = nodeId;
        _selected = nodeId;
        _visited.Add(nodeId);
        _routeStepUpdating = true;
        try
        {
            RouteStepped?.Invoke(previous, nodeId);
            RouteNodeEntered?.Invoke(nodeId);
        }
        finally { _routeStepUpdating = false; }
        refresh();
        if (generation == _travelGeneration && Visible) animate_player_step(previous, sourcePosition);
    }

    private List<string> find_shortest_path(string from, string to)
    {
        var result = new List<string>();
        if (from == to) return result;
        var queue = new Queue<string>();
        var previous = new Dictionary<string, string?>(StringComparer.Ordinal) { [from] = null };
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == to) break;
            foreach (var neighbor in EastWoodsDungeonRules.get_neighbors(current))
            {
                if (!is_node_discovered(neighbor)) continue;
                if (previous.ContainsKey(neighbor)) continue;
                previous[neighbor] = current;
                queue.Enqueue(neighbor);
            }
        }
        if (!previous.ContainsKey(to)) return result;
        for (string? cursor = to; cursor is not null; cursor = previous[cursor]) result.Add(cursor);
        result.Reverse();
        return result;
    }

    private Vector2 node_center(EastWoodsDungeonRules.NodeDefinition node)
    {
        var x = (node.GridPosition.X - _minGridX) * (_cellSize + _cellGap) + _cellSize * 0.5f;
        var y = (node.GridPosition.Y - _minGridY) * (_cellSize + _cellGap) + _cellSize * 0.5f;
        return _gridOrigin + new Vector2(x, y);
    }

    private static string edge_key(string left, string right)
        => string.CompareOrdinal(left, right) < 0 ? $"|{left}|{right}|" : $"|{right}|{left}|";

    private static string format_hours(float hours)
    {
        if (hours >= 24f)
        {
            var days = (int)(hours / 24f);
            var remaining = hours - days * 24f;
            return remaining < 0.01f ? $"{days} 天" : $"{days} 天 {remaining:0.#} 小时";
        }
        return $"{hours:0.#} 小时";
    }

    private static Label make_label(string text, int size, Color color)
    {
        var label = new Label { Text = text, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static Button make_button(string text, int width, int height)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(width, height) };
        WorldHudStyle.StyleSmallButton(button);
        button.AddThemeStyleboxOverride("normal", WorldHudStyle.Paper());
        button.AddThemeStyleboxOverride("hover", WorldHudStyle.Paper(true));
        return button;
    }

    private static StyleBoxFlat panel_style(Color background, Color border, int radius, int borderWidth = 1)
    {
        var style = new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = borderWidth,
            BorderWidthTop = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthBottom = borderWidth,
        };
        style.SetCornerRadiusAll(radius);
        return style;
    }
}
