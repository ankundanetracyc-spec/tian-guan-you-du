using Godot;
using Godot.Collections;

public partial class CellCharacterList : Control
{
    [Export] public bool is_persistent;
    private Container? _characters;
    private Control? _list_panel;
    private Button? _toggle_button;
    private Tween? _toggle_tween;
    private Label? _header_title, _header_count;
    private GodotObject? _last;
    private ulong _last_click;
    private bool _world_mode;
    private bool _expanded = true;
    private string _roster_context = "";
    private float _expanded_height = 150f;

    public override void _Ready()
    {
        AddToGroup("cell_character_list");
        MouseFilter = MouseFilterEnum.Pass;
        _list_panel = GetNodeOrNull<Control>("ListPanel");
        _characters = GetNodeOrNull<Container>("ListPanel/CharacterScroll/CharacterVBox");
        _toggle_button = GetNodeOrNull<Button>("ListPanel/ToggleButton");
        if (_list_panel is not null)
        {
            _list_panel.AddThemeStyleboxOverride("panel", row_style(new Color(.055f, .06f, .052f, .93f), new Color("a48b5b"), 8));
            build_header();
        }
        if (GetNodeOrNull<ScrollContainer>("ListPanel/CharacterScroll") is { } scroll)
        {
            scroll.OffsetLeft = 10;
            scroll.OffsetTop = 58;
            scroll.OffsetRight = -12;
            scroll.OffsetBottom = -10;
        }
        if (_toggle_button is not null)
        {
            _toggle_button.GetParent()?.RemoveChild(_toggle_button);
            AddChild(_toggle_button);
            _toggle_button.Pressed += toggle_expanded;
            _toggle_button.AddThemeStyleboxOverride("normal", transparent_button_style(new Color(.015f, .02f, .016f, .82f)));
            _toggle_button.AddThemeStyleboxOverride("hover", transparent_button_style(new Color(.12f, .13f, .12f, .94f)));
            _toggle_button.AddThemeStyleboxOverride("pressed", transparent_button_style(new Color(.18f, .18f, .15f, .98f)));
            _toggle_button.AddThemeColorOverride("font_color", new Color("f1dfad"));
            _toggle_button.AddThemeColorOverride("font_hover_color", new Color("fff4cc"));
            _toggle_button.AddThemeFontSizeOverride("font_size", 16);
        }
        apply_expanded_state();
        if (!is_persistent) Hide();
    }

    public Rect2 get_ui_rect() => GetNodeOrNull<Control>("ListPanel")?.GetGlobalRect() ?? GetGlobalRect();

    public void refresh_persistent()
    {
        is_persistent = true;
        var scene = GetTree().CurrentScene;
        var values = scene?.HasMethod("get_village_home_characters") == true
            ? scene.Call("get_village_home_characters").AsGodotArray()
            : new Godot.Collections.Array();
        show_characters(values);
    }

    private void build_header()
    {
        if (_list_panel is null) return;
        var header = new HBoxContainer { Name = "RosterHeader", MouseFilter = MouseFilterEnum.Ignore };
        header.AnchorLeft = 0;
        header.AnchorRight = 1;
        header.AnchorTop = 0;
        header.AnchorBottom = 0;
        header.OffsetLeft = 13;
        header.OffsetTop = 8;
        header.OffsetRight = -13;
        header.OffsetBottom = 48;
        header.AddThemeConstantOverride("separation", 8);
        _list_panel.AddChild(header);

        _header_title = new Label { Text = "当前位置", VerticalAlignment = VerticalAlignment.Center, SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        _header_title.AddThemeFontSizeOverride("font_size", 18);
        _header_title.AddThemeColorOverride("font_color", new Color("e5d09c"));
        header.AddChild(_header_title);

        var count_badge = new PanelContainer { Name = "CountBadge", CustomMinimumSize = new Vector2(58, 28), MouseFilter = MouseFilterEnum.Ignore };
        count_badge.AddThemeStyleboxOverride("panel", row_style(new Color(.23f, .19f, .12f, .88f), new Color("90764a"), 12));
        header.AddChild(count_badge);
        _header_count = new Label { Text = "0 位", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        _header_count.AddThemeFontSizeOverride("font_size", 13);
        _header_count.AddThemeColorOverride("font_color", new Color("f1e5c5"));
        count_badge.AddChild(_header_count);

        var divider = new ColorRect { Name = "HeaderDivider", Color = new Color("b49a65", .55f), MouseFilter = MouseFilterEnum.Ignore };
        divider.AnchorLeft = 0;
        divider.AnchorRight = 1;
        divider.AnchorTop = 0;
        divider.AnchorBottom = 0;
        divider.OffsetLeft = 12;
        divider.OffsetTop = 51;
        divider.OffsetRight = -12;
        divider.OffsetBottom = 52;
        _list_panel.AddChild(divider);
    }

    private void update_header(string title, int count)
    {
        if (_header_title is not null) _header_title.Text = title;
        if (_header_count is not null) _header_count.Text = $"{count} 位";
    }

    public void show_characters(Godot.Collections.Array characters, string _title_text = "")
    {
        _world_mode = false;
        _roster_context = "";
        clear_children();
        var count = 0;
        foreach (var value in characters)
        {
            var unit = value.AsGodotObject();
            if (!GodotObject.IsInstanceValid(unit)) continue;
            var name = unit!.Get("entity_name").AsString();
            var realm = unit.Get("cultivation_realm").AsString();
            var row = make_character_row(name, realm, portrait =>
            {
                if (unit is Character character) portrait.CallDeferred("show_character", character);
            }, () => click(unit));
            _characters?.AddChild(row);
            count++;
        }
        update_header(string.IsNullOrWhiteSpace(_title_text) ? "村中人物" : _title_text, count);
        _expanded = true;
        resize_panel(count);
        apply_expanded_state();
        show_and_raise();
    }

    /// <summary>复用青溪里左侧名单，显示当前世界节点或子场景的在场人物。</summary>
    public void show_world_npcs(Array<WorldNpcData> characters, string context_id = "")
    {
        // 地点内部已经接管左侧名单时，世界地图的刷新不能把子场景名单覆盖掉。
        if (_world_mode && _roster_context.StartsWith("interior:") && !context_id.StartsWith("interior:"))
            return;
        var context_changed = _roster_context != context_id;
        _world_mode = true;
        _roster_context = context_id;
        clear_children();
        var count = 0;
        foreach (var npc in characters)
        {
            if (npc is null || !GodotObject.IsInstanceValid(npc)) continue;
            _characters?.AddChild(make_world_row(npc));
            count++;
        }
        if (count == 0)
            _characters?.AddChild(make_empty_state());
        update_header(context_id.StartsWith("world-cell:") ? "当前位置" : context_id.StartsWith("world:") ? "当前地点" : "在场人物", count);
        if (context_changed && !context_id.StartsWith("world-cell:")) _expanded = true;
        resize_panel(count);
        apply_expanded_state();
        show_and_raise();
    }

    /// <summary>按世界节点与内部空间读取同一份运行时 NPC 名单。</summary>
    public void show_world_context(string node_id, string subscene_id = "", string context_id = "")
    {
        var manager = GetNodeOrNull<NpcWorldManager>("/root/NpcWorldManager");
        var roster = string.IsNullOrWhiteSpace(subscene_id)
            ? manager?.get_node_roster(node_id) ?? new Array<WorldNpcData>()
            : manager?.get_subscene_roster(node_id, subscene_id) ?? new Array<WorldNpcData>();
        show_world_npcs(roster, context_id);
    }

    /// <summary>供地点内部场景复用同一角色列表；当前没有登记 NPC 时保持纯空列表。</summary>
    public void show_static_rows(Godot.Collections.Array labels, string context_id = "")
    {
        _world_mode = true;
        _roster_context = context_id;
        clear_children();
        foreach (var value in labels)
        {
            var text = value.AsString();
            if (string.IsNullOrWhiteSpace(text)) continue;
            _characters?.AddChild(make_row(text, 15));
        }
        update_header("在场人物", labels.Count);
        _expanded = true;
        resize_panel(labels.Count);
        apply_expanded_state();
        show_and_raise();
    }

    /// <summary>地点场景将可交谈人物交给同一份左侧名单，不再另建人物面板。</summary>
    public void show_action_rows(IEnumerable<(string label, Action action)> rows, string context_id = "")
    {
        _world_mode = true;
        _roster_context = context_id;
        clear_children();
        var count = 0;
        foreach (var (label, action) in rows)
        {
            var row = make_row(label, 15);
            row.Pressed += action;
            _characters?.AddChild(row);
            count++;
        }
        update_header("可交谈人物", count);
        _expanded = true;
        resize_panel(count);
        apply_expanded_state();
        show_and_raise();
    }

    public void hide_world_npcs()
    {
        if (!_world_mode) return;
        _world_mode = false;
        _roster_context = "";
        ZIndex = 0;
        Hide();
    }

    private Button make_world_row(WorldNpcData npc)
    {
        var display = string.IsNullOrWhiteSpace(npc.npc_name) ? "无名" : npc.npc_name.Trim();
        var identity = string.IsNullOrWhiteSpace(npc.formal_identity) ? "身份未详" : npc.formal_identity.Trim();
        var realm = string.IsNullOrWhiteSpace(npc.cultivation_realm) ? "凡人" : npc.cultivation_realm.Trim();
        return make_character_row(display, $"{identity}  ·  {realm}", portrait => portrait.CallDeferred("show_world_npc", npc), () => click(npc));
    }

    private Button make_character_row(string title, string subtitle, Action<PortraitRenderer> configure_portrait, Action on_pressed)
    {
        var row = new Button
        {
            Text = "",
            CustomMinimumSize = new Vector2(0, 72),
            MouseFilter = MouseFilterEnum.Stop,
            FocusMode = FocusModeEnum.None,
            ClipContents = true,
        };
        row.AddThemeColorOverride("font_color", new Color("f1dfad"));
        row.AddThemeStyleboxOverride("normal", row_style(new Color(.105f, .115f, .095f, .91f), new Color(.55f, .47f, .31f, .65f)));
        row.AddThemeStyleboxOverride("hover", row_style(new Color(.21f, .19f, .13f, .97f), new Color("c3a66b")));
        row.AddThemeStyleboxOverride("pressed", row_style(new Color(.17f, .15f, .11f, .98f), new Color("d0b579")));

        var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 7);
        margin.AddThemeConstantOverride("margin_right", 7);
        margin.AddThemeConstantOverride("margin_top", 5);
        margin.AddThemeConstantOverride("margin_bottom", 5);
        row.AddChild(margin);

        var content = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        content.AddThemeConstantOverride("separation", 10);
        margin.AddChild(content);

        var portrait_frame = new PanelContainer { CustomMinimumSize = new Vector2(48, 60), MouseFilter = MouseFilterEnum.Ignore };
        portrait_frame.AddThemeStyleboxOverride("panel", row_style(new Color(.78f, .72f, .59f, .96f), new Color("b59a63"), 4));
        content.AddChild(portrait_frame);
        var portrait = new PortraitRenderer { CustomMinimumSize = new Vector2(44, 56), MouseFilter = MouseFilterEnum.Ignore };
        portrait_frame.AddChild(portrait);
        configure_portrait(portrait);

        var text_column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
        text_column.AddThemeConstantOverride("separation", 3);
        content.AddChild(text_column);
        var name_label = new Label { Text = title, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis, MouseFilter = MouseFilterEnum.Ignore };
        name_label.AddThemeFontSizeOverride("font_size", 16);
        name_label.AddThemeColorOverride("font_color", new Color("f1dfad"));
        text_column.AddChild(name_label);
        var details_label = new Label { Text = subtitle, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis, MouseFilter = MouseFilterEnum.Ignore };
        details_label.AddThemeFontSizeOverride("font_size", 12);
        details_label.AddThemeColorOverride("font_color", new Color("c7c2ad"));
        text_column.AddChild(details_label);

        row.Pressed += on_pressed;
        return row;
    }

    private static Control make_empty_state()
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(0, 56), MouseFilter = MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", row_style(new Color(.12f, .12f, .10f, .55f), new Color(.53f, .48f, .36f, .42f), 5));
        var label = new Label
        {
            Text = "此处暂无其他人物",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", 14);
        label.AddThemeColorOverride("font_color", new Color("c9c2af"));
        panel.AddChild(label);
        return panel;
    }

    private Button make_row(string text, int font_size)
    {
        var row = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(0, 38),
            MouseFilter = MouseFilterEnum.Stop,
            Alignment = HorizontalAlignment.Center,
        };
        row.AddThemeFontSizeOverride("font_size", font_size);
        row.AddThemeColorOverride("font_color", new Color("f1dfad"));
        row.AddThemeColorOverride("font_hover_color", new Color("fff4cc"));
        row.AddThemeStyleboxOverride("normal", row_style(new Color(.12f, .13f, .1f, .86f), new Color(.68f, .55f, .3f, .62f)));
        row.AddThemeStyleboxOverride("hover", row_style(new Color(.31f, .24f, .13f, .94f), new Color(.76f, .58f, .3f, .9f)));
        return row;
    }

    private void toggle_expanded()
    {
        _expanded = !_expanded;
        apply_expanded_state(true);
    }

    private void apply_expanded_state(bool animate = false)
    {
        if (_toggle_button is not null)
        {
            _toggle_button.Text = _expanded ? "◀" : "▶";
            _toggle_button.TooltipText = _expanded ? "收起角色列表" : "展开角色列表";
            _toggle_button.Size = new Vector2(30, Mathf.Max(60, _expanded_height * .5f));
            _toggle_button.ZIndex = 1;
            var targetPosition = new Vector2(_expanded ? 270 : 0, (_expanded_height - _toggle_button.Size.Y) * .5f);
            if (animate)
            {
                _toggle_tween?.Kill();
                _toggle_tween = CreateTween();
                _toggle_tween.SetTrans(Tween.TransitionType.Cubic);
                _toggle_tween.SetEase(Tween.EaseType.InOut);
                _toggle_tween.TweenProperty(_toggle_button, "position", targetPosition, .2);
            }
            else _toggle_button.Position = targetPosition;
        }
        Size = new Vector2(300, _expanded_height);
        if (_list_panel is not null)
        {
            _list_panel.AnchorLeft = 0;
            _list_panel.AnchorRight = 0;
            _list_panel.AnchorTop = 0;
            _list_panel.AnchorBottom = 1;
            _list_panel.OffsetLeft = 0;
            _list_panel.OffsetRight = 270;
            _list_panel.Visible = true;
            if (_characters is not null) _characters.Visible = _expanded;
            if (animate)
            {
                _toggle_tween?.Kill();
                _toggle_tween = CreateTween();
                _toggle_tween.SetTrans(Tween.TransitionType.Cubic);
                _toggle_tween.SetEase(Tween.EaseType.InOut);
                _toggle_tween.TweenProperty(_list_panel, "position", _expanded ? Vector2.Zero : new Vector2(-270, 0), .2);
            }
            else _list_panel.Position = _expanded ? Vector2.Zero : new Vector2(-270, 0);
        }
    }

    private void resize_panel(int count)
    {
        var viewport_height = GetViewportRect().Size.Y;
        var max_height = Mathf.Max(150f, viewport_height - 190f);
        // 侧栏按内容收高，人数较多时才启用内部滚动，避免单人名单留下一整块空黑面板。
        _expanded_height = Mathf.Min(max_height, Mathf.Max(142f, 70f + Mathf.Max(1, count) * 80f));
        Size = new Vector2(300, _expanded_height);
    }

    private void clear_children()
    {
        if (_characters is null) return;
        foreach (var child in _characters.GetChildren()) child.QueueFree();
    }

    private void show_and_raise()
    {
        ZIndex = 10;
        var viewportHeight = GetViewportRect().Size.Y;
        Position = new Vector2(0, Mathf.Max(118f, (viewportHeight - Size.Y) * 0.5f));
        Show();
        MoveToFront();
    }

    private static StyleBoxFlat row_style(Color background, Color border, int radius = 4)
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

    private static StyleBoxFlat transparent_button_style(Color? background = null)
    {
        return new StyleBoxFlat { BgColor = background ?? new Color(0, 0, 0, 0) };
    }

    private void click(GodotObject unit)
    {
        var now = Time.GetTicksMsec();
        if (unit == _last && now - _last_click < 400)
        {
            foreach (var panel in GetTree().GetNodesInGroup("human_info_panel"))
            {
                if (panel is Node node && node.HasMethod("show_entity"))
                {
                    // 世界 NPC 复用人物档案的外框，但由面板进入只读的 NPC 模式；
                    // 青溪里等实体角色仍走原有的完整人物页。
                    if (unit is WorldNpcData npc && node.HasMethod("show_npc")) node.Call("show_npc", npc);
                    else if (unit is not WorldNpcData) node.Call("show_entity", unit);
                    break;
                }
            }
            _last = null;
            return;
        }
        if (unit is WorldNpcData worldNpc)
        {
            // 通过主场景统一分发交谈、交易等操作，避免内景全屏控件或
            // 场景加载顺序让事件连接失效。
            var main = GetTree().GetFirstNodeInGroup("main_scene");
            if (main is Node mainNode && mainNode.HasMethod("open_world_npc_actions"))
                mainNode.Call("open_world_npc_actions", worldNpc);
        }
        _last = unit;
        _last_click = now;
    }
}
