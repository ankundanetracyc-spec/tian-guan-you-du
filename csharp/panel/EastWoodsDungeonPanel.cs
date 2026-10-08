using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>东林地横向路线探索原型。路线数据与横向背景分离，热点仅表示可达节点。</summary>
public partial class EastWoodsDungeonPanel : Control
{
    private sealed record RouteNode(string Title, string RoutePrompt, string Description, Vector2 Position, string[] Next);

    private static readonly Dictionary<string, RouteNode> Route = new()
    {
        ["forest_edge"] = new("林缘", "沿溪沟向前", "日光仍能穿过疏林。浅足迹沿着林地边缘，前方可见一段被落叶遮住的溪沟。", new Vector2(0.045f, 0.53f), ["leaf_creek"]),
        ["leaf_creek"] = new("断叶溪沟", "进入断叶溪沟", "溪沟已经断流，叶片却聚在沟底。前方分出几条可行的小径：有的沿溪深入，有的绕过交错树根，也有一条穿过蕨草低地。", new Vector2(0.15f, 0.53f), ["creek_path", "root_path", "fern_lowland"]),
        ["creek_path"] = new("", "顺溪转入林深处", "溪道在林中拐了个弯，潮湿石面上留有新鲜的兽爪印。", new Vector2(0.27f, 0.30f), ["stream_canopy"]),
        ["stream_canopy"] = new("", "穿过低垂树冠", "枝叶压低了去路，溪声仍在前方断续传来。", new Vector2(0.39f, 0.22f), ["deep_meet"]),
        ["root_path"] = new("", "绕过盘根", "盘错的树根抬起地面，几处新折枝指向林内；绕行处没有明显异常。", new Vector2(0.27f, 0.53f), ["root_hollow"]),
        ["root_hollow"] = new("", "穿过树根窄隙", "树根间形成一条仅容一人通过的窄隙，前方逐渐开阔。", new Vector2(0.39f, 0.55f), ["deep_meet"]),
        ["fern_lowland"] = new("", "穿过蕨草低地", "低处蕨叶密生，草间有浅浅水痕，未见明显踩踏或折损。", new Vector2(0.27f, 0.76f), ["fallen_canopy"]),
        ["fallen_canopy"] = new("", "从倒木旁绕过", "一截倒木横在林间，绕过后能望见前方树冠重新合拢。", new Vector2(0.39f, 0.84f), ["deep_meet"]),
        ["deep_meet"] = new("深林会合", "走向深林会合处", "几条林径在合拢的树冠下重新相接。这里可直往洼地，也可沿上侧林径或下侧浅沟多巡一段。", new Vector2(0.51f, 0.53f), ["quiet_grove", "upper_ridge", "shallow_gully"]),
        ["upper_ridge"] = new("", "走上侧林径", "地势略高，树根间散落着湿润苔痕，视线能越过前方灌木。", new Vector2(0.62f, 0.29f), ["moss_bridge"]),
        ["moss_bridge"] = new("", "踏过苔石", "几块平石跨过浅沟，周围没有新鲜的争斗痕迹。", new Vector2(0.73f, 0.22f), ["quiet_grove"]),
        ["shallow_gully"] = new("", "绕入下侧浅沟", "浅沟里积着薄薄的水，岸边草木被风压向同一侧。", new Vector2(0.62f, 0.77f), ["dry_pool"]),
        ["dry_pool"] = new("", "穿过干涸水洼", "洼底只剩潮湿泥纹，几道浅痕延伸向前方。", new Vector2(0.73f, 0.84f), ["quiet_grove"]),
        ["quiet_grove"] = new("静冠洼地", "抵达静冠洼地复核", "古树根系在洼地边缘交错成环。这里是巡线复核点，也可沿会合处方向继续查看林径。", new Vector2(0.85f, 0.53f), ["deep_meet"]),
    };

    public event Action? Closed;
    public event Action<string>? RouteNodeEntered;
    public event Action<string, string>? RouteStepped;

    private Control? _viewport;
    private Control? _panorama;
    private Control? _route_layer;
    private Label? _current_title;
    private Label? _description;
    private VBoxContainer? _choices;
    private string _current = "forest_edge";
    private readonly HashSet<string> _visited = ["forest_edge"];
    private readonly HashSet<string> _inspected = [];
    private string _activeDutyId = "";
    private int _activeDutyStage;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        ZIndex = 210;
        build();
        Hide();
    }

    public void show_dungeon()
    {
        Show();
        MoveToFront();
        CallDeferred(nameof(layout_panorama));
        refresh();
    }

    public void set_active_duty_context(string dutyId, int stage)
    {
        _activeDutyId = dutyId;
        _activeDutyStage = Math.Max(0, stage);
        if (Visible) refresh();
    }

    public void close()
    {
        Hide();
        Closed?.Invoke();
    }

    public string get_current_route_node_id() => _current;

    public Godot.Collections.Dictionary get_route_state()
    {
        var visited = new Godot.Collections.Array<string>();
        foreach (var id in _visited.OrderBy(id => id, StringComparer.Ordinal)) visited.Add(id);
        return new Godot.Collections.Dictionary { ["current"] = _current, ["visited"] = visited };
    }

    public void restore_route_state(Godot.Collections.Dictionary state)
    {
        if (state.Count == 0) return;
        var current = state.GetValueOrDefault("current", "forest_edge").AsString();
        if (!Route.ContainsKey(current)) current = "forest_edge";
        _current = current;
        _visited.Clear();
        _visited.Add("forest_edge");
        var values = state.GetValueOrDefault("visited", new Godot.Collections.Array<string>());
        if (values.VariantType == Variant.Type.Array)
            foreach (var value in values.AsGodotArray())
                if (Route.ContainsKey(value.AsString())) _visited.Add(value.AsString());
        _visited.Add(_current);
        if (Visible) refresh();
    }

    private void build()
    {
        _viewport = new Control { ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
        _viewport.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_viewport);
        _panorama = new Control { MouseFilter = MouseFilterEnum.Ignore };
        _viewport.AddChild(_panorama);
        var background = new TextureRect
        {
            Name = "PanoramaBackground",
            Texture = ResourceLoader.Load<Texture2D>("res://textures/world_locations/east_woods_dungeon_panorama_v1.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _panorama.AddChild(background);
        _route_layer = new Control { MouseFilter = MouseFilterEnum.Ignore };
        _panorama.AddChild(_route_layer);

        var header = make_label("东林地 · 深入林地", 30, new Color("f1dfad"));
        header.Position = new Vector2(48, 28);
        AddChild(header);
        var subheader = make_label("沿林径前行；溪沟处分路，深林会合", 16, new Color("e3d6b6"));
        subheader.Position = new Vector2(52, 76);
        AddChild(subheader);
        var return_button = make_button("返回地点", 132, 42);
        return_button.SetAnchorsPreset(LayoutPreset.TopRight);
        return_button.Position = new Vector2(-180, 32);
        return_button.Pressed += close;
        AddChild(return_button);

        var info = overlay_panel(new Vector2(660, 205));
        info.SetAnchorsPreset(LayoutPreset.BottomLeft);
        info.Position = new Vector2(42, -245);
        AddChild(info);
        var info_margin = overlay_margin(info, 20, 16, 20, 16);
        var info_column = new VBoxContainer();
        info_column.AddThemeConstantOverride("separation", 8);
        info_margin.AddChild(info_column);
        _current_title = make_label("林缘", 21, new Color("f1dfad"));
        info_column.AddChild(_current_title);
        _description = make_label("", 16, new Color("eee4d1"));
        _description.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _description.SizeFlagsVertical = SizeFlags.ExpandFill;
        info_column.AddChild(_description);

        var choices_panel = overlay_panel(new Vector2(430, 250));
        choices_panel.SetAnchorsPreset(LayoutPreset.BottomRight);
        choices_panel.Position = new Vector2(-470, -290);
        AddChild(choices_panel);
        var choices_margin = overlay_margin(choices_panel, 18, 16, 18, 16);
        var choices_column = new VBoxContainer();
        choices_column.AddThemeConstantOverride("separation", 8);
        choices_margin.AddChild(choices_column);
        choices_column.AddChild(make_label("前方路线", 19, new Color("d9bd7c")));
        _choices = new VBoxContainer();
        _choices.AddThemeConstantOverride("separation", 8);
        choices_column.AddChild(_choices);
        var retreat = make_button("暂时撤出", 0, 38);
        retreat.Pressed += close;
        choices_column.AddChild(retreat);

        _viewport.Resized += layout_panorama;
        CallDeferred(nameof(layout_panorama));
    }

    private static PanelContainer overlay_panel(Vector2 size)
    {
        var result = new PanelContainer { CustomMinimumSize = size, MouseFilter = MouseFilterEnum.Stop };
        result.AddThemeStyleboxOverride("panel", panel_style(new Color(0.055f, 0.065f, 0.06f, 0.84f), new Color("b99554"), 8));
        return result;
    }

    private static MarginContainer overlay_margin(Control parent, int left, int top, int right, int bottom)
    {
        var result = new MarginContainer();
        result.AddThemeConstantOverride("margin_left", left);
        result.AddThemeConstantOverride("margin_top", top);
        result.AddThemeConstantOverride("margin_right", right);
        result.AddThemeConstantOverride("margin_bottom", bottom);
        parent.AddChild(result);
        return result;
    }

    private void layout_panorama()
    {
        if (_viewport is null || _panorama is null || _route_layer is null) return;
        var view = _viewport.Size;
        if (view.X < 100 || view.Y < 100) return;
        var canvas = new Vector2(Mathf.Max(view.X, view.Y * 3.0f), view.Y);
        _panorama.Size = canvas;
        _panorama.GetNode<TextureRect>("PanoramaBackground").Size = canvas;
        _route_layer.Size = canvas;
        refresh();
    }

    private void refresh()
    {
        if (_panorama is null || _route_layer is null || _viewport is null || _choices is null) return;
        foreach (var child in _route_layer.GetChildren()) child.QueueFree();
        foreach (var child in _choices.GetChildren()) child.QueueFree();
        var current = Route[_current];
        _current_title!.Text = string.IsNullOrWhiteSpace(current.Title) ? "林间途中" : current.Title;
        _description!.Text = _activeDutyId == "east_woods_patrol"
            ? $"{current.Description}\n巡线进度：{Math.Min(_activeDutyStage, YuxuSectPrototypeBalance.EastWoodsPatrolRequiredNodeMoves)}/{YuxuSectPrototypeBalance.EastWoodsPatrolRequiredNodeMoves} 个节点（抵达静冠洼地复核）；沿途只核查异常，不采集资源。"
            : current.Description;
        var visible = new HashSet<string>(_visited) { _current };
        foreach (var next in current.Next) visible.Add(next);

        foreach (var from_id in visible)
        {
            if (!Route.TryGetValue(from_id, out var from) || (!_visited.Contains(from_id) && from_id != _current)) continue;
            foreach (var to_id in from.Next)
            {
                if (!visible.Contains(to_id)) continue;
                var line = new Line2D
                {
                    Width = 4,
                    DefaultColor = _visited.Contains(to_id) ? new Color("e0c788") : new Color(0.86f, 0.80f, 0.62f, 0.86f),
                    Points = [route_point(from.Position), route_point(Route[to_id].Position)],
                };
                _route_layer.AddChild(line);
            }
        }

        foreach (var id in visible)
        {
            var node = Route[id];
            var is_current = id == _current;
            var named = !string.IsNullOrWhiteSpace(node.Title);
            var markerSize = named ? new Vector2(140, 38) : new Vector2(30, 30);
            var marker = make_route_marker(node, named);
            marker.Position = route_point(node.Position) - markerSize * 0.5f;
            marker.Disabled = is_current || (_visited.Contains(id) && !current.Next.Contains(id));
            marker.Modulate = is_current ? Colors.White : _visited.Contains(id) ? new Color(0.92f, 0.82f, 0.57f) : Colors.White;
            if (!is_current && current.Next.Contains(id)) marker.Pressed += () => travel_to(id);
            _route_layer.AddChild(marker);

            var npcCount = GetNodeOrNull<NpcWorldManager>("/root/NpcWorldManager")?.get_dungeon_node_roster(EastWoodsDungeonRules.DungeonId, id).Count ?? 0;
            if (npcCount > 0)
            {
                var count = make_label($"{npcCount} 人", 13, new Color("fff0c4"));
                count.Position = route_point(node.Position) + new Vector2(markerSize.X * 0.5f + 4, -markerSize.Y * 0.5f - 6);
                _route_layer.AddChild(count);
            }
        }

        if (current.Next.Length > 0)
        {
            foreach (var id in current.Next)
            {
                var prompt = string.IsNullOrWhiteSpace(Route[id].RoutePrompt) ? "沿林径继续" : Route[id].RoutePrompt;
                var caption = current.Next.Length > 1 ? $"选择：{prompt}" : $"前往：{prompt}";
                add_choice(caption, () => travel_to(id));
            }
        }
        else
        {
            add_choice("结束此次探索", close);
        }
        if (_current == "quiet_grove")
            add_choice("结束此次探索", close);

        if (_current == "root_path" && !_inspected.Contains("root_path"))
            add_choice("查看交错林根", () =>
            {
                _inspected.Add("root_path");
                _description!.Text = "几道新折枝从树根间穿过，方向都指向深林。绕行的路更曲折，但前方地势较开阔。";
            });

        var max_offset = Mathf.Max(0, _panorama.Size.X - _viewport.Size.X);
        var focus_x = route_point(current.Position).X - _viewport.Size.X * 0.43f;
        _panorama.Position = new Vector2(-Mathf.Clamp(focus_x, 0, max_offset), 0);
    }

    private Vector2 route_point(Vector2 normalized) => _panorama!.Size * normalized;

    private void travel_to(string node_id)
    {
        if (!Route.TryGetValue(_current, out var current) || !Array.Exists(current.Next, id => id == node_id)) return;
        var previous = _current;
        _current = node_id;
        _visited.Add(node_id);
        refresh();
        RouteStepped?.Invoke(previous, node_id);
        RouteNodeEntered?.Invoke(node_id);
    }

    private void add_choice(string text, Action action)
    {
        var choice = make_button(text, 0, 42);
        choice.Pressed += action;
        _choices!.AddChild(choice);
    }

    private static Button make_route_marker(RouteNode node, bool named)
    {
        var button = make_button(named ? node.Title : "", named ? 140 : 30, named ? 38 : 30);
        button.TooltipText = string.IsNullOrWhiteSpace(node.Title)
            ? $"林间路径\n{node.Description}"
            : $"{node.Title}\n{node.Description}";
        if (!named)
        {
            button.AddThemeStyleboxOverride("normal", panel_style(new Color(0.10f, 0.12f, 0.10f, 0.94f), new Color("b99554"), 15));
            button.AddThemeStyleboxOverride("hover", panel_style(new Color(0.30f, 0.25f, 0.15f, 0.98f), new Color("e0c788"), 15));
            button.AddThemeStyleboxOverride("disabled", panel_style(new Color(0.20f, 0.20f, 0.16f, 0.82f), new Color("8b7953"), 15));
        }
        return button;
    }

    private static Label make_label(string text, int size, Color color)
    {
        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static Button make_button(string text, int width, int height)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(width, height) };
        button.AddThemeFontSizeOverride("font_size", 15);
        button.AddThemeColorOverride("font_color", new Color("eadfca"));
        button.AddThemeStyleboxOverride("normal", panel_style(new Color(0.13f, 0.11f, 0.08f, 0.94f), new Color("75603b"), 6));
        button.AddThemeStyleboxOverride("hover", panel_style(new Color(0.24f, 0.18f, 0.1f, 0.97f), new Color("c09a52"), 6));
        return button;
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
}
