using Godot;
using System.Collections.Generic;

/// <summary>
/// 世界节点的统一地点内部界面。
///
/// 世界地图只负责旅行与节点选择；抵达后由此处承接地点说明、子场景和在场人物，
/// 避免为每个普通节点额外拆一个场景。需要专属表现的一级势力仍可使用自己的内部场景。
/// </summary>
public partial class WorldLocationInteriorPanel : Control
{
    public event System.Action? Closed;

    private WorldNodeData? _node;
    private NpcWorldManager? _npc_world;
    private Label? _title;
    private Label? _meta;
    private Label? _description;
    private Label? _subscene_detail;
    private VBoxContainer? _subscene_list;
    private CellCharacterList? _cell_roster;
    private PanelContainer? _generic_frame;
    private PanelContainer? _east_woods_frame;
    private Control? _east_woods_map;
    private Control? _east_woods_panorama;
    private Control? _east_woods_route_layer;
    private VBoxContainer? _east_woods_actions;
    private Label? _east_woods_selection;
    private Label? _east_woods_clue;
    private Label? _east_woods_progress;
    private readonly HashSet<string> _east_woods_visited = ["forest_edge"];
    private readonly HashSet<string> _east_woods_inspected = [];
    private string _selected_subscene_id = "";
    public bool is_npc_in_current_space(WorldNpcData npc) => Visible && _node?.node_id == npc.current_node_id &&
        (string.IsNullOrEmpty(_selected_subscene_id) || npc.current_subscene_id == _selected_subscene_id);
    private string _east_woods_current = "forest_edge";

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        ZIndex = 200;
        build();
        _cell_roster = GetTree().GetFirstNodeInGroup("cell_character_list") as CellCharacterList;
        Hide();
    }

    public void show_for(WorldNodeData node, NpcWorldManager? npc_world)
    {
        _node = node;
        _npc_world = npc_world;
        _selected_subscene_id = "";
        _cell_roster ??= GetTree().GetFirstNodeInGroup("cell_character_list") as CellCharacterList;
        if (node.node_id == "east_woods")
        {
            ensure_east_woods_view();
            if (_generic_frame is not null) _generic_frame.Hide();
            if (_east_woods_frame is not null) _east_woods_frame.Show();
            refresh_east_woods_view();
            Show();
            MoveToFront();
            return;
        }
        if (_east_woods_frame is not null) _east_woods_frame.Hide();
        if (_generic_frame is not null) _generic_frame.Show();
        refresh_content();
        Show();
        MoveToFront();
    }

    public void close()
    {
        hide_roster();
        Hide();
        Closed?.Invoke();
    }

    public void hide_roster() => _cell_roster?.hide_world_npcs();

    private void build()
    {
        var shade = new ColorRect
        {
            Color = new Color(0.025f, 0.035f, 0.032f, 0.82f),
            MouseFilter = MouseFilterEnum.Stop,
        };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);

        var frame = new PanelContainer
        {
            Name = "GenericLocationFrame",
            CustomMinimumSize = new Vector2(760, 500),
            MouseFilter = MouseFilterEnum.Stop,
        };
        _generic_frame = frame;
        frame.SetAnchorsPreset(LayoutPreset.Center);
        frame.Position = new Vector2(-380, -250);
        frame.AddThemeStyleboxOverride("panel", panel_style(
            new Color(0.12f, 0.12f, 0.095f, 0.98f), new Color("b99554"), 10));
        AddChild(frame);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 28);
        margin.AddThemeConstantOverride("margin_right", 28);
        margin.AddThemeConstantOverride("margin_top", 22);
        margin.AddThemeConstantOverride("margin_bottom", 22);
        frame.AddChild(margin);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 12);
        margin.AddChild(column);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 12);
        column.AddChild(header);
        _title = label("地点内部", 28, new Color("f1dfad"));
        _title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(_title);
        var close_button = button("返回地图", 120, 38);
        close_button.Pressed += close;
        header.AddChild(close_button);

        _meta = label("", 15, new Color("d7c49c"));
        column.AddChild(_meta);
        add_rule(column);

        _description = label("", 17, new Color("e2d8c4"));
        _description.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _description.CustomMinimumSize = new Vector2(0, 72);
        column.AddChild(_description);

        var body = new HBoxContainer();
        body.SizeFlagsVertical = SizeFlags.ExpandFill;
        body.AddThemeConstantOverride("separation", 16);
        column.AddChild(body);

        var subscene_panel = section_panel("内部空间", out _subscene_list);
        subscene_panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        body.AddChild(subscene_panel);

        _subscene_detail = label("选择一处空间查看说明。", 14, new Color("c4b89f"));
        _subscene_detail.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _subscene_detail.CustomMinimumSize = new Vector2(0, 48);
        column.AddChild(_subscene_detail);
    }

    private void ensure_east_woods_view()
    {
        if (_east_woods_frame is not null) return;

        var frame = new PanelContainer
        {
            Name = "EastWoodsOverviewFrame",
            CustomMinimumSize = new Vector2(1740, 930),
            MouseFilter = MouseFilterEnum.Stop,
        };
        frame.SetAnchorsPreset(LayoutPreset.Center);
        frame.Position = new Vector2(-870, -465);
        frame.AddThemeStyleboxOverride("panel", panel_style(
            new Color(0.09f, 0.105f, 0.09f, 0.985f), new Color("b99554"), 10));
        AddChild(frame);
        _east_woods_frame = frame;

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 24);
        margin.AddThemeConstantOverride("margin_right", 24);
        margin.AddThemeConstantOverride("margin_top", 18);
        margin.AddThemeConstantOverride("margin_bottom", 18);
        frame.AddChild(margin);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 12);
        margin.AddChild(column);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 12);
        column.AddChild(header);
        var title = label("东林地 · 循痕入林", 27, new Color("f1dfad"));
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(title);
        var close_button = button("返回地图", 120, 38);
        close_button.Pressed += close;
        header.AddChild(close_button);

        var meta = label("前期野外探索　·　沿路线深入林地；溪沟处分路，深林会合", 15, new Color("d7c49c"));
        column.AddChild(meta);
        add_rule(column);

        var body = new HBoxContainer();
        body.SizeFlagsVertical = SizeFlags.ExpandFill;
        body.AddThemeConstantOverride("separation", 16);
        column.AddChild(body);

        var map_frame = new PanelContainer
        {
            CustomMinimumSize = new Vector2(1220, 687),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
        };
        map_frame.AddThemeStyleboxOverride("panel", panel_style(
            new Color(0.12f, 0.13f, 0.11f, 1), new Color("806b45"), 6));
        body.AddChild(map_frame);

        _east_woods_map = new Control { CustomMinimumSize = new Vector2(1200, 675), ClipContents = true };
        map_frame.AddChild(_east_woods_map);
        var background = new TextureRect
        {
            Texture = ResourceLoader.Load<Texture2D>("res://textures/world_locations/east_woods_overview_v1.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _east_woods_map.AddChild(background);

        var overlay = new Control { MouseFilter = MouseFilterEnum.Ignore };
        overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _east_woods_map.AddChild(overlay);
        add_east_woods_hotspot(overlay, "林缘日照带", "forest_edge", 0.20f, 0.76f);
        add_east_woods_hotspot(overlay, "巡界石标", "boundary_stone", 0.20f, 0.48f);
        add_east_woods_hotspot(overlay, "断叶溪沟", "leaf_creek", 0.53f, 0.56f);
        add_east_woods_hotspot(overlay, "静冠洼地", "quiet_grove", 0.77f, 0.34f);

        var sidebar = new PanelContainer
        {
            CustomMinimumSize = new Vector2(410, 0),
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        sidebar.AddThemeStyleboxOverride("panel", panel_style(
            new Color(0.07f, 0.08f, 0.07f, 0.96f), new Color("806b45"), 6));
        body.AddChild(sidebar);

        var side_margin = new MarginContainer();
        side_margin.AddThemeConstantOverride("margin_left", 18);
        side_margin.AddThemeConstantOverride("margin_right", 18);
        side_margin.AddThemeConstantOverride("margin_top", 16);
        side_margin.AddThemeConstantOverride("margin_bottom", 16);
        sidebar.AddChild(side_margin);
        var side_column = new VBoxContainer();
        side_column.AddThemeConstantOverride("separation", 10);
        side_margin.AddChild(side_column);
        side_column.AddChild(label("林中所见", 18, new Color("d9bd7c")));
        add_rule(side_column);
        _east_woods_selection = label("林缘日照带", 22, new Color("f1dfad"));
        side_column.AddChild(_east_woods_selection);
        _east_woods_clue = label("日光落在疏林间，泥土仍带潮意。浅足迹绕开林根，树根间隐约有一缕回流灵息。", 16, new Color("e2d8c4"));
        _east_woods_clue.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _east_woods_clue.SizeFlagsVertical = SizeFlags.ExpandFill;
        side_column.AddChild(_east_woods_clue);
        _east_woods_progress = label("已辨明 2 / 4 处地貌", 14, new Color("c4b89f"));
        side_column.AddChild(_east_woods_progress);
        add_rule(side_column);
        side_column.AddChild(label("可做之事", 18, new Color("d9bd7c")));
        _east_woods_actions = new VBoxContainer();
        _east_woods_actions.AddThemeConstantOverride("separation", 8);
        side_column.AddChild(_east_woods_actions);
        side_column.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        var note = label("当前版本：地貌考察与线索揭示。采集、战斗和时间结算将在各自规则接入后开放。", 13, new Color("bdb39e"));
        note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        side_column.AddChild(note);

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 12);
        column.AddChild(footer);
        var footer_note = label("林海由疏至密；越过溪沟后，兽迹便不再沿着水声走。", 14, new Color("c9d8d2"));
        footer_note.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        footer.AddChild(footer_note);
        var roster_button = button("在场人物", 120, 36);
        roster_button.Pressed += () => show_east_woods_roster(_east_woods_current);
        footer.AddChild(roster_button);

        _east_woods_map.Resized += layout_east_woods_hotspots;
        layout_east_woods_hotspots();
        refresh_east_woods_view();
    }

    private void add_east_woods_hotspot(Control parent, string title, string id, float x, float y)
    {
        var hotspot = button(title, 148, 38);
        hotspot.Name = $"Hotspot_{id}";
        hotspot.Size = new Vector2(148, 38);
        hotspot.SetMeta("east_woods_subscene_id", id);
        hotspot.SetMeta("east_woods_x", x);
        hotspot.SetMeta("east_woods_y", y);
        hotspot.Pressed += () => select_east_woods_hotspot(id);
        parent.AddChild(hotspot);
    }

    private void layout_east_woods_hotspots()
    {
        if (_east_woods_map is null) return;
        var overlay = _east_woods_map.GetChildOrNull<Control>(1);
        if (overlay is null) return;
        foreach (var child in overlay.GetChildren())
        {
            if (child is not Button hotspot || !hotspot.HasMeta("east_woods_subscene_id")) continue;
            var point = new Vector2(
                hotspot.GetMeta("east_woods_x").AsSingle(),
                hotspot.GetMeta("east_woods_y").AsSingle());
            hotspot.Position = _east_woods_map.Size * point - hotspot.Size / 2.0f;
        }
    }

    private void select_east_woods_hotspot(string subscene_id)
    {
        if (!_east_woods_visited.Contains(subscene_id)) return;
        _east_woods_current = subscene_id;
        refresh_east_woods_view();
        show_east_woods_roster(subscene_id);
    }

    private void refresh_east_woods_view()
    {
        if (_east_woods_frame is null || _east_woods_actions is null) return;
        foreach (var child in _east_woods_actions.GetChildren()) child.QueueFree();
        var names = new Dictionary<string, string>
        {
            ["forest_edge"] = "林缘日照带",
            ["boundary_stone"] = "巡界石标",
            ["leaf_creek"] = "断叶溪沟",
            ["quiet_grove"] = "静冠洼地",
        };
        var clues = new Dictionary<string, string>
        {
            ["forest_edge"] = "日光落在疏林间，泥土仍带潮意。浅足迹绕开林根，树根间隐约有一缕回流灵息。",
            ["boundary_stone"] = "石面覆苔，只余几道断续刻痕。这里有人巡过山林，却没有驻营的迹象。",
            ["leaf_creek"] = "溪沟早已断流，叶片却逆着坡势聚拢。兽迹在这里分成两道：一道贴地，一道绕开灵息回流处。",
            ["quiet_grove"] = "古树根系交错成环，风声到了林下便忽然消失。浅痕沿根外缘绕行，深处暂时看不清。",
        };
        _east_woods_selection!.Text = names.GetValueOrDefault(_east_woods_current, "林缘日照带");
        _east_woods_clue!.Text = clues.GetValueOrDefault(_east_woods_current, "暂时没有更多线索。");
        _east_woods_progress!.Text = $"已辨明 {_east_woods_visited.Count} / 4 处地貌";

        foreach (var child in _east_woods_map!.GetChild(1).GetChildren())
            if (child is Button hotspot && hotspot.HasMeta("east_woods_subscene_id"))
            {
                var id = hotspot.GetMeta("east_woods_subscene_id").AsString();
                hotspot.Disabled = !_east_woods_visited.Contains(id);
                hotspot.Modulate = hotspot.Disabled ? new Color(0.72f, 0.75f, 0.73f, 0.72f) : Colors.White;
            }

        switch (_east_woods_current)
        {
            case "forest_edge":
                add_east_woods_action(_east_woods_visited.Contains("leaf_creek") ? "林缘已考察" : "考察林缘", () =>
                {
                    _east_woods_visited.Add("leaf_creek");
                    _east_woods_current = "leaf_creek";
                    refresh_east_woods_view();
                    show_east_woods_roster(_east_woods_current);
                }, _east_woods_visited.Contains("leaf_creek"));
                break;
            case "boundary_stone":
                add_east_woods_action("辨认巡界刻痕", () =>
                {
                    _east_woods_clue!.Text = "刻痕的朝向指向溪沟上游，最近的一道浅痕却绕开了那条路。石标只留下线索，不代表此处有固定驻守。";
                });
                break;
            case "leaf_creek":
                add_east_woods_action(_east_woods_visited.Contains("quiet_grove") ? "循迹已探明" : "循痕探查", () =>
                {
                    _east_woods_visited.Add("quiet_grove");
                    _east_woods_current = "quiet_grove";
                    refresh_east_woods_view();
                    show_east_woods_roster(_east_woods_current);
                }, _east_woods_visited.Contains("quiet_grove"));
                break;
            case "quiet_grove":
                add_east_woods_action("观察林根浅痕", () =>
                {
                    _east_woods_clue!.Text = "浅痕在一株古树前突然折返。树根下的灵息并非从林中涌来，而像是被什么牵引着流向更深处。";
                });
                break;
        }
        if (_east_woods_current is "leaf_creek" or "quiet_grove")
            add_east_woods_action("退回林缘", () =>
            {
                _east_woods_current = "forest_edge";
                refresh_east_woods_view();
                show_east_woods_roster(_east_woods_current);
            });
    }

    private void add_east_woods_action(string title, System.Action action, bool disabled = false)
    {
        var action_button = button(title, 0, 42);
        action_button.Disabled = disabled;
        action_button.Pressed += action;
        _east_woods_actions!.AddChild(action_button);
    }

    private void show_east_woods_roster(string subscene_id)
    {
        if (_node is null) return;
        _selected_subscene_id = subscene_id;
        refresh_current_roster();
    }

    private PanelContainer section_panel(string title, out VBoxContainer content)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0, 210),
        };
        panel.AddThemeStyleboxOverride("panel", panel_style(
            new Color(0.065f, 0.065f, 0.052f, 0.88f), new Color("806b45"), 6));
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        panel.AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);
        margin.AddChild(column);
        column.AddChild(label(title, 18, new Color("d9bd7c")));
        add_rule(column);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
        column.AddChild(scroll);
        content = new VBoxContainer();
        content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        content.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(content);
        return panel;
    }

    private void refresh_content()
    {
        if (_node is null || _title is null || _meta is null || _description is null || _subscene_list is null)
            return;
        clear_children(_subscene_list);

        _title.Text = _node.node_name;
        var type_name = _node.node_type switch
        {
            WorldNodeData.NodeType.City => "城镇",
            WorldNodeData.NodeType.Sect => "宗门",
            WorldNodeData.NodeType.Resource => "资源地",
            WorldNodeData.NodeType.Dungeon => "秘境",
            WorldNodeData.NodeType.War => "战场",
            WorldNodeData.NodeType.Home => "居所",
            WorldNodeData.NodeType.Wilderness => "野外",
            _ => "村落",
        };
        _meta.Text = $"{type_name}　·　地貌：{(string.IsNullOrWhiteSpace(_node.terrain) ? "未详" : _node.terrain)}　·　灵压 {_node.spirit_pressure}　·　危险 {_node.danger_level}";
        _description.Text = string.IsNullOrWhiteSpace(_node.description)
            ? "这里暂时没有更多记载。沿着地点内部的分区行动，可能触发人物、事件或新的线索。"
            : _node.description;

        if (_node.subscenes.Count == 0)
            _subscene_list.AddChild(label("（尚未划分内部空间）", 15, new Color("9e998c")));
        else
            foreach (var subscene in _node.subscenes)
            {
                if (subscene is null || !subscene.unlocked) continue;
                var item = subscene;
                var subscene_button = button(item.subscene_name, 0, 34);
                subscene_button.Alignment = HorizontalAlignment.Left;
                subscene_button.Pressed += () => show_subscene(item);
                _subscene_list.AddChild(subscene_button);
            }

        refresh_current_roster();
        if (_subscene_detail is not null) _subscene_detail.Text = "选择一处空间查看说明。";
    }

    private void show_subscene(WorldSubsceneData subscene)
    {
        if (_subscene_detail is null) return;
        _selected_subscene_id = subscene.subscene_id;
        refresh_current_roster();
        _subscene_detail.Text = string.IsNullOrWhiteSpace(subscene.description)
            ? $"{subscene.subscene_name}：暂未留下详细说明。"
            : $"{subscene.subscene_name}：{subscene.description}";
    }

    private void refresh_current_roster()
    {
        if (_node is null) return;
        _cell_roster ??= GetTree().GetFirstNodeInGroup("cell_character_list") as CellCharacterList;
        if (_cell_roster is null) return;
        var roster = string.IsNullOrWhiteSpace(_selected_subscene_id)
            ? _npc_world?.get_node_roster(_node.node_id) ?? new Godot.Collections.Array<WorldNpcData>()
            : _npc_world?.get_subscene_roster(_node.node_id, _selected_subscene_id) ?? new Godot.Collections.Array<WorldNpcData>();
        var context = string.IsNullOrWhiteSpace(_selected_subscene_id)
            ? $"interior:{_node.node_id}"
            : $"interior:{_node.node_id}:{_selected_subscene_id}";
        _cell_roster.show_world_npcs(roster, context);
    }

    private static Label label(string text, int font_size, Color color)
    {
        var result = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        if (font_size > 0) result.AddThemeFontSizeOverride("font_size", font_size);
        result.AddThemeColorOverride("font_color", color);
        return result;
    }

    private static Button button(string text, int width, int height)
    {
        var result = new Button { Text = text, CustomMinimumSize = new Vector2(width, height), MouseFilter = Control.MouseFilterEnum.Stop };
        if (width <= 0) result.CustomMinimumSize = new Vector2(0, height);
        result.AddThemeFontSizeOverride("font_size", 15);
        result.AddThemeColorOverride("font_color", new Color("eadfca"));
        result.AddThemeStyleboxOverride("normal", panel_style(new Color(0.13f, 0.11f, 0.08f, 0.9f), new Color("75603b"), 6));
        result.AddThemeStyleboxOverride("hover", panel_style(new Color(0.24f, 0.18f, 0.1f, 0.96f), new Color("c09a52"), 6));
        return result;
    }

    private static StyleBoxFlat panel_style(Color background, Color border, int radius)
    {
        var style = new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
        };
        style.SetCornerRadiusAll(radius);
        return style;
    }

    private static void add_rule(Container parent)
    {
        var separator = new HSeparator { Modulate = new Color(0.57f, 0.46f, 0.28f, 0.7f) };
        parent.AddChild(separator);
    }

    private static void clear_children(Node parent)
    {
        foreach (var child in parent.GetChildren()) child.QueueFree();
    }
}
