using Godot;
using System;
using System.Linq;

public partial class EastWoodsDungeonPanel
{
    private static Texture2D? _roguelikeNodeIconAtlas;

    // Appearance only: movement times and graph adjacency still come from DungeonRules.
    private (int Index, string Name) terrain_kind(string id) => !_dynamicMode ? id switch
    {
        "fern_lowland" or "forest_edge" => (1, "草地"),
        "creek_path" or "upper_ridge" or "moss_bridge" => (2, "岩地"),
        "shallow_gully" or "stream_canopy" => (3, "浅溪"),
        "leaf_creek" or "mid_split" => (4, "干溪"),
        "south_turn" or "lower_join" or "dry_pool" or "lower_approach" => (5, "湿地"),
        _ => terrain_from_definition(EastWoodsDungeonRules.get_node(id)),
    } : terrain_from_definition(route_node(id));

    private static (int Index, string Name) terrain_from_definition(EastWoodsDungeonRules.NodeDefinition? node)
    {
        var terrain = node?.Terrain ?? 0;
        return (terrain, terrain switch { 1 => "草地", 2 => "岩地", 3 => "浅溪", 5 => "湿地", 6 => "竹林", 7 => "阴岩地面", _ => "林地" });
    }

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
        var seed = route_layout_seed;
        var dungeonId = _dynamicMode ? route_dungeon_id : seed == "legacy" ? EastWoodsDungeonRules.DungeonId : EastWoodsDungeonRules.DungeonId + "|" + seed;
        return DungeonTerrainVisuals.Get(terrain, dungeonId + "|" + seed, id);
    }

    private static int roguelike_node_icon_index(string key) => key switch
    {
        "entry" => 0,
        "exit" => 1,
        "battle" => 2,
        "boss" => 3,
        "rest" => 4,
        "herb" => 5,
        "fungus" => 6,
        "wood" => 7,
        "mine" => 8,
        "beast_nest" => 9,
        "cultivation" => 10,
        "manual" => 11,
        "alchemy" => 12,
        "forging" => 13,
        "resource" => 14,
        "shop" => 15,
        "event" => 15,
        _ => 14,
    };

    private static string roguelike_node_icon_key(EastWoodsDungeonRules.NodeDefinition node)
    {
        if (node.Feature == "beast_nest") return "beast_nest";
        if (node.ContentType == DynamicDungeonExpedition.ResourceNodeType &&
            node.ResourceNodeId.StartsWith("rogue_material_", StringComparison.Ordinal))
        {
            var parts = node.Id.Split('_');
            var layer = parts.Length > 1 && int.TryParse(parts[1], out var parsedLayer) ? parsedLayer : 0;
            return DynamicDungeonExpedition.RoguelikeMaterialGroupForLayer(layer);
        }
        if (node.ContentType == DynamicDungeonExpedition.ResourceNodeType) return "resource";
        return node.ContentType switch
        {
            DynamicDungeonExpedition.BattleNodeType => "battle",
            DynamicDungeonExpedition.BossNodeType => "boss",
            DynamicDungeonExpedition.RestNodeType => "rest",
            "entry" => "entry",
            "exit" => "exit",
            "cultivation" => "cultivation",
            "manual" => "manual",
            "shop" => "shop",
            "alchemy" => "alchemy",
            "forging" => "forging",
            "event" => "event",
            _ => "resource",
        };
    }

    private void build_roguelike_node_icon(Button button, EastWoodsDungeonRules.NodeDefinition node)
    {
        _roguelikeNodeIconAtlas ??= ResourceLoader.Load<Texture2D>("res://textures/dungeon/roguelike_node_icons_xianxia_v1.png");
        if (_roguelikeNodeIconAtlas is null) return;
        var index = roguelike_node_icon_index(roguelike_node_icon_key(node));
        var atlasCellSize = new Vector2(_roguelikeNodeIconAtlas.GetWidth() / 4f, _roguelikeNodeIconAtlas.GetHeight() / 4f);
        var icon = new TextureRect
        {
            Name = "RoguelikeNodeIcon",
            Texture = new AtlasTexture
            {
                Atlas = _roguelikeNodeIconAtlas,
                Region = new Rect2(new Vector2((index % 4) * atlasCellSize.X, (index / 4) * atlasCellSize.Y), atlasCellSize),
                FilterClip = true,
            },
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Modulate = node.Feature switch
            {
                "cultivation_physique" => new Color("d89a69"),
                "cultivation_intelligence" => new Color("80a985"),
                "cultivation_soul" => new Color("8e9cbd"),
                _ => Colors.White,
            },
            MouseFilter = MouseFilterEnum.Ignore,
        };
        icon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        button.AddChild(icon);
    }

    private Line2D? add_roguelike_connection(EastWoodsDungeonRules.NodeDefinition from, EastWoodsDungeonRules.NodeDefinition to, bool isPath)
    {
        if (_connectionLayer is null) return null;
        var direction = (node_center(to) - node_center(from)).Normalized();
        var start = node_center(from) + direction * (_cellSize * .43f);
        var end = node_center(to) - direction * (_cellSize * .43f);
        var line = new Line2D
        {
            Width = isPath ? 3.5f : 2f,
            DefaultColor = isPath ? new Color("c18d4d") : new Color("665f50", .76f),
            Points = [start, end],
        };
        _connectionLayer.AddChild(line);
        return line;
    }

    private Line2D? add_terrain_connection(EastWoodsDungeonRules.NodeDefinition from, EastWoodsDungeonRules.NodeDefinition to, bool isPath)
    {
        if (_connectionLayer is null) return null;
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

        var line = new Line2D
        {
            Width = isPath ? 5 : Mathf.Max(2, _cellSize * 0.025f),
            DefaultColor = isPath ? new Color("e6bd68") : new Color(0.22f, 0.23f, 0.19f, 0.72f),
            Points = [node_center(from), node_center(to)],
        };
        _connectionLayer.AddChild(line);
        return line;

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
        _hoverTimer = new Godot.Timer { WaitTime = .10, OneShot = true };
        AddChild(_hoverTimer);
        _hoverTimer.Timeout += show_hover;
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
        if (_nodeButtons.TryGetValue(_hoveredNode, out var button)) HoverInfoManager.HideFor(button);
        _hoveredNode = "";
        _hoverTimer?.Stop();
    }

    private void show_hover()
    {
        var node = route_node(_hoveredNode);
        if (!Visible || node is null || !is_node_visible(node.Id) || !_nodeButtons.TryGetValue(node.Id, out var anchor)) return;
        HoverInfoManager.SetInfo(anchor, build_node_hover_content(node), true);
    }

    private HoverInfoManager.HoverInfoContent build_node_hover_content(EastWoodsDungeonRules.NodeDefinition node)
    {
        var (_, terrainName) = terrain_kind(node.Id);
        var title = string.IsNullOrWhiteSpace(node.Title) ? terrainName : $"{node.Title} · {terrainName}";
        if (!_dynamicMode) title = EastWoodsDungeonRules.forest_region(node.Id) + " · " + title;
        var showContents = _dynamicMode || node.Id == _current;
        var description = showContents ? node.Description : forest_ambience(node.Id);
        if (is_tower_instance && node.Id != route_entry_node_id)
            description += $"\n前往此节点消耗 {DynamicDungeonExpedition.MovementStaminaCost(_dynamicInstanceState, node.Id):0.#} 点精力。";
        var resourceNode = _dynamicMode
            ? EastWoodsResourceNodes.Find(node.ResourceNodeId)
            : EastWoodsResourceNodes.ForNode(node);
        if (showContents && resourceNode is not null)
            description += $"\n资源类别：{resourceNode.CategoryLabel}（{resourceNode.DisplayName}）";
        var availableAction = showContents ? available_actions(node.Id).FirstOrDefault() : null;
        if (availableAction is not null)
            description += "\n" + availableAction.Description;
        else if (showContents && (_dynamicMode ? node.ResourceNodeId.Length > 0 || node.Feature.Length > 0 || DynamicDungeonExpedition.AvailableActions(_dynamicInstanceState, node.Id, _actionProgress).Count > 0 : EastWoodsActions.Content(node.Id) is not null))
            description += "\n此处行动已完成或资源已采尽。";
        if (!_dynamicMode && _activeDutyId == "east_woods_patrol")
            description += _activeDutyReady ? "\n巡查复核完成，沿出口离开后回和光峰回报。" : $"\n巡查：{Math.Min(_activeDutyStage, YuxuSectPrototypeBalance.EastWoodsPatrolRequiredNodeMoves)}/{YuxuSectPrototypeBalance.EastWoodsPatrolRequiredNodeMoves} 格；静冠洼地复核后回宗回报。";
        if (node.Id != _current)
        {
            var path = find_shortest_path(_current, node.Id);
            if (path.Count > 1)
                description += $"\n预计耗时：{format_hours((path.Count - 1) * EastWoodsDungeonRules.player_edge_hours(_playerDunSpeed))}";
        }
        return new HoverInfoManager.HoverInfoContent(title, description);
    }

    public override void _Process(double delta)
    {
        if (Visible) update_map_camera();
    }
}
