using Godot;
using System;

public partial class EastWoodsDungeonPanel
{
    // Appearance only: movement times and graph adjacency still come from DungeonRules.
    private static (int Index, string Name) terrain_kind(string id) => id switch
    {
        "fern_lowland" or "forest_edge" => (1, "草地"),
        "creek_path" or "upper_ridge" or "moss_bridge" => (2, "岩地"),
        "shallow_gully" or "stream_canopy" => (3, "浅溪"),
        "leaf_creek" or "mid_split" => (4, "干溪"),
        "south_turn" or "lower_join" or "dry_pool" or "lower_approach" => (5, "湿地"),
        _ => (EastWoodsDungeonRules.get_node(id)?.Terrain ?? 0, (EastWoodsDungeonRules.get_node(id)?.Terrain ?? 0) switch { 1 => "草地", 2 => "岩地", 3 => "浅溪", 5 => "湿地", 6 => "竹林", 7 => "阴岩地面", _ => "林地" }),
    };

    private Texture2D load_terrain(string id)
    {
        var (index, _) = terrain_kind(id);
        var terrain = index switch
        {
            0 => DungeonTerrainVisuals.Terrain.Woodland,
            2 => DungeonTerrainVisuals.Terrain.Rock,
            3 => DungeonTerrainVisuals.Terrain.Water,
            5 => DungeonTerrainVisuals.Terrain.Wetland,
            6 => DungeonTerrainVisuals.Terrain.Bamboo,
            7 => DungeonTerrainVisuals.Terrain.CaveFloor,
            _ => DungeonTerrainVisuals.Terrain.Grass,
        };
        var seed = EastWoodsDungeonRules.LayoutSeed;
        return DungeonTerrainVisuals.Get(terrain, seed == "legacy" ? EastWoodsDungeonRules.DungeonId : EastWoodsDungeonRules.DungeonId + "|" + seed, id);
    }

    private void add_terrain_connection(EastWoodsDungeonRules.NodeDefinition from, EastWoodsDungeonRules.NodeDefinition to)
    {
        if (_connectionLayer is null) return;
        var start = node_center(from);
        var end = node_center(to);
        var direction = (end - start).Normalized();
        start += direction * (_cellSize * .5f - 1);
        end -= direction * (_cellSize * .5f - 1);
        var middle = (start + end) * .5f;
        var thickness = Mathf.Max(8, _cellSize * .16f);
        // Narrow ground strips make the link a terrain transition, not a floating black wire.
        add_half(start, middle, from.Id);
        add_half(middle, end, to.Id);
        void add_half(Vector2 a, Vector2 b, string id)
        {
            var vertical = Mathf.Abs(a.Y - b.Y) > .01f;
            var strip = new TextureRect
            {
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                Texture = connection_ground(id),
                Position = vertical ? new Vector2(a.X - thickness / 2, Mathf.Min(a.Y, b.Y)) : new Vector2(Mathf.Min(a.X, b.X), a.Y - thickness / 2),
                Size = vertical ? new Vector2(thickness, a.DistanceTo(b)) : new Vector2(a.DistanceTo(b), thickness),
                Modulate = new Color(1, 1, 1, .80f),
                MouseFilter = MouseFilterEnum.Ignore,
            };
            _connectionLayer.AddChild(strip);
        }
    }

    private Texture2D connection_ground(string id)
    {
        var tile = (AtlasTexture)load_terrain(id);
        // Take only the empty central ground, not trees/rocks stretched into a thin link.
        return new AtlasTexture
        {
            Atlas = tile.Atlas, FilterClip = true,
            Region = new Rect2(tile.Region.Position + tile.Region.Size * .45f, tile.Region.Size * .10f),
        };
    }

    private void build_hover()
    {
        _hoverTimer = new Godot.Timer { WaitTime = .45, OneShot = true };
        AddChild(_hoverTimer);
        _hoverTimer.Timeout += show_hover;
        _hoverPanel = new PanelContainer
        {
            Name = "TerrainHoverInfo", MouseFilter = MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(310, 0), ZIndex = 1,
        };
        _hoverPanel.AddThemeStyleboxOverride("panel", WorldHudStyle.Paper());
        AddChild(_hoverPanel);
        var rows = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        rows.AddThemeConstantOverride("separation", 8);
        _hoverPanel.AddChild(rows);
        _hoverTitle = WorldHudStyle.Text("", 22);
        rows.AddChild(_hoverTitle);
        rows.AddChild(WorldHudStyle.Rule());
        _hoverDescription = WorldHudStyle.Text("", 17, true);
        _hoverDescription.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _hoverDescription.CustomMinimumSize = new Vector2(280, 0);
        rows.AddChild(_hoverDescription);
        _hoverPanel.Hide();
    }

    private void begin_hover(string id)
    {
        clear_hover();
        if (!is_node_visible(id)) return;
        _hoveredNode = id;
        _hoverTimer?.Start();
    }

    private void clear_hover()
    {
        _hoveredNode = "";
        _hoverTimer?.Stop();
        _hoverPanel?.Hide();
    }

    private void show_hover()
    {
        var node = EastWoodsDungeonRules.get_node(_hoveredNode);
        if (!Visible || node is null || !is_node_visible(node.Id) || _hoverPanel is null || _hoverTitle is null || _hoverDescription is null) return;
        var (_, terrainName) = terrain_kind(node.Id);
        _hoverTitle.Text = string.IsNullOrWhiteSpace(node.Title) ? terrainName : $"{node.Title} · {terrainName}";
        _hoverDescription.Text = node.Description;
        if (EastWoodsActions.Content(node.Id) is { } action)
            _hoverDescription.Text += action_remaining(node.Id, action) > 0 ? "\n" + action.Description : "\n此处行动已完成或资源已采尽。";
        if (_activeDutyId == "east_woods_patrol")
            _hoverDescription.Text += _activeDutyReady ? "\n巡查复核完成，沿出口离开后回和光峰回报。" : $"\n巡查：{Math.Min(_activeDutyStage, YuxuSectPrototypeBalance.EastWoodsPatrolRequiredNodeMoves)}/{YuxuSectPrototypeBalance.EastWoodsPatrolRequiredNodeMoves} 格；静冠洼地复核后回宗回报。";
        if (node.Id != _current)
        {
            var path = find_shortest_path(_current, node.Id);
            if (path.Count > 1)
                _hoverDescription.Text += $"\n预计耗时：{format_hours((path.Count - 1) * EastWoodsDungeonRules.player_edge_hours(_playerDunSpeed))}";
        }
        _hoverPanel.Size = new Vector2(310, 0);
        _hoverPanel.Show();
        position_hover();
    }

    public override void _Process(double delta)
    {
        if (Visible) update_map_camera();
        if (Visible && _hoverPanel?.Visible == true) position_hover();
    }

    private void position_hover()
    {
        if (_hoverPanel is null) return;
        var mouse = GetLocalMousePosition();
        var panelSize = _hoverPanel.Size;
        var x = mouse.X + 18;
        if (x + panelSize.X > Size.X - 12) x = mouse.X - panelSize.X - 18;
        var position = new Vector2(
            Mathf.Clamp(x, 12, Mathf.Max(12, Size.X - panelSize.X - 12)),
            Mathf.Clamp(mouse.Y + 18, 12, Mathf.Max(12, Size.Y - panelSize.Y - 12)));
        if (!_hoverPanel.Position.IsEqualApprox(position)) _hoverPanel.Position = position;
    }
}
