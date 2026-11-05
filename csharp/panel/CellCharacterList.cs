using Godot;
using Godot.Collections;

public partial class CellCharacterList : Control
{
    private const float PanelWidth = 312f;
    private const float RowHeight = 76f;
    private const int RowSeparation = 2;
    private const float ContentTop = 72f;
    private const float ContentBottom = 16f;
    [Export] public bool is_persistent;
    private Container? _characters;
    private Control? _list_panel;
    private Control? _status_panel;
    private float _status_top = float.NaN;
    private Button? _toggle_button;
    private Tween? _toggle_tween;
    private Label? _header_title, _header_count;
    private GodotObject? _last;
    private ulong _last_click;
    private bool _world_mode;
    private bool _expanded = true;
    private string _roster_context = "";
    private float _expanded_height = 150f;
    private readonly System.Collections.Generic.Dictionary<Control, Action> _pendingPortraits = new();
    private string _worldRosterStamp = "";
    private bool _worldNpcRosterCached;

    public event Action? RosterClosed;

    // Offscreen portraits are not composed. Limit visible portrait work to one per frame.
    public override void _Process(double delta)
    {
        if (!_expanded || !IsVisibleInTree()) return;
        var status_top = _status_panel?.IsVisibleInTree() == true ? _status_panel.GlobalPosition.Y : GetViewportRect().Size.Y - 172;
        if (!Mathf.IsEqualApprox(_status_top, status_top))
        {
            _status_top = status_top;
            refresh_layout();
        }
        var scroll = GetNodeOrNull<ScrollContainer>("ListPanel/CharacterScroll");
        if (scroll is null) return;
        foreach (var entry in _pendingPortraits)
        {
            if (!GodotObject.IsInstanceValid(entry.Key)) { _pendingPortraits.Remove(entry.Key); break; }
            if (!entry.Key.GetGlobalRect().Intersects(scroll.GetGlobalRect())) continue;
            _pendingPortraits.Remove(entry.Key);
            entry.Value();
            break;
        }
    }

    public override void _Ready()
    {
        AddToGroup("cell_character_list");
        VisibilityChanged += on_roster_visibility_changed;
        MouseFilter = MouseFilterEnum.Pass;
        MouseForcePassScrollEvents = false;
        ClipContents = true;
        _list_panel = GetNodeOrNull<Control>("ListPanel");
        _status_panel = GetParent()?.GetNodeOrNull<Control>("MainHUD/PlayerStatus");
        _characters = GetNodeOrNull<Container>("ListPanel/CharacterScroll/CharacterVBox");
        _toggle_button = GetNodeOrNull<Button>("ListPanel/ToggleButton");
        if (_list_panel is not null)
        {
            _list_panel.GuiInput += _GuiInput;
            _list_panel.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
            var paper = new WorldHudFrame { Name = "RosterInkSurface" };
            _list_panel.AddChild(paper);
            paper.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            _list_panel.MoveChild(paper, 0);
            build_header();
        }
        if (GetNodeOrNull<ScrollContainer>("ListPanel/CharacterScroll") is { } scroll)
        {
            scroll.VerticalScrollMode = ScrollContainer.ScrollMode.Auto;
            scroll.MouseForcePassScrollEvents = false;
            scroll.OffsetLeft = 14;
            scroll.OffsetTop = ContentTop;
            scroll.OffsetRight = -14;
            scroll.OffsetBottom = -ContentBottom;
        }
        _characters?.AddThemeConstantOverride("separation", RowSeparation);
        GetViewport().SizeChanged += refresh_layout;
        if (_toggle_button is not null)
        {
            _toggle_button.GetParent()?.RemoveChild(_toggle_button);
            AddChild(_toggle_button);
            _toggle_button.Pressed += toggle_expanded;
            WorldHudStyle.StylePaperButton(_toggle_button);
        }
        apply_expanded_state();
        if (!is_persistent) Hide();
    }

    public Rect2 get_ui_rect() => GetNodeOrNull<Control>("ListPanel")?.GetGlobalRect() ?? GetGlobalRect();

    /// <summary>Visible roster bounds expressed in viewport coordinates, including its canvas transform.</summary>
    public bool contains_roster_point(Vector2 viewport_position) => IsVisibleInTree() && _expanded &&
        _list_panel is not null && new Rect2(Vector2.Zero, _list_panel.Size).HasPoint(
            _list_panel.GetGlobalTransformWithCanvas().AffineInverse() * viewport_position);

    public override void _GuiInput(InputEvent input)
    {
        // Also scroll over the title and padding. At either end consume the wheel,
        // rather than letting it zoom the map underneath (including short lists).
        if (!_expanded || !IsVisibleInTree() || input is not InputEventMouseButton wheel ||
            wheel.ButtonIndex is not (MouseButton.WheelUp or MouseButton.WheelDown)) return;
        if (wheel.Pressed && GetNodeOrNull<ScrollContainer>("ListPanel/CharacterScroll") is { } scroll)
        {
            var amount = Mathf.RoundToInt((RowHeight + RowSeparation) * (wheel.Factor > 0 ? wheel.Factor : 1));
            scroll.ScrollVertical += wheel.ButtonIndex == MouseButton.WheelDown ? amount : -amount;
        }
        AcceptEvent();
    }

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
        var headingInk = WorldHudArt.Mark("brush_band", new Vector2(4, 6), new Vector2(PanelWidth - 8, 57));
        headingInk.Name = "RosterHeadingInk";
        _list_panel.AddChild(headingInk);
        var header = new HBoxContainer { Name = "RosterHeader", MouseFilter = MouseFilterEnum.Ignore };
        header.AnchorLeft = 0;
        header.AnchorRight = 1;
        header.AnchorTop = 0;
        header.AnchorBottom = 0;
        header.OffsetLeft = 18;
        header.OffsetTop = 18;
        header.OffsetRight = -18;
        header.OffsetBottom = 52;
        header.AddThemeConstantOverride("separation", 8);
        _list_panel.AddChild(header);

        _header_title = new Label { Text = "当前位置", VerticalAlignment = VerticalAlignment.Center, SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        _header_title.AddThemeFontSizeOverride("font_size", 22);
        _header_title.AddThemeColorOverride("font_color", new Color("f5f0e3"));
        header.AddChild(_header_title);

        var count_badge = new PanelContainer { Name = "CountBadge", CustomMinimumSize = new Vector2(60, 32), MouseFilter = MouseFilterEnum.Ignore };
        count_badge.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        header.AddChild(count_badge);
        _header_count = new Label { Text = "0 位", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        _header_count.AddThemeFontSizeOverride("font_size", 16);
        _header_count.AddThemeColorOverride("font_color", new Color("e1e5d9"));
        count_badge.AddChild(_header_count);

        var divider = new ColorRect { Name = "HeaderDivider", Color = Colors.Transparent, MouseFilter = MouseFilterEnum.Ignore };
        divider.AnchorLeft = 0;
        divider.AnchorRight = 1;
        divider.AnchorTop = 0;
        divider.AnchorBottom = 0;
        divider.OffsetLeft = 18;
        divider.OffsetTop = 60;
        divider.OffsetRight = -18;
        divider.OffsetBottom = 61;
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
        resize_panel();
        apply_expanded_state();
        show_and_raise();
    }

    /// <summary>复用青溪里左侧名单，显示当前世界节点或子场景的在场人物。</summary>
    public void show_world_npcs(Array<WorldNpcData> characters, string context_id = "")
    {
        // 地点内部已经接管左侧名单时，世界地图的刷新不能把子场景名单覆盖掉。
        if (_world_mode && _roster_context.StartsWith("interior:") && !context_id.StartsWith("interior:"))
            return;
        var rosterParts = new System.Collections.Generic.List<string>();
        foreach (var npc in characters)
        {
            if (npc is null || !GodotObject.IsInstanceValid(npc)) continue;
            npc.ensure_appearance_identity();
            rosterParts.Add($"{npc.GetInstanceId()}:{npc.npc_name}:{npc.formal_identity}:{npc.cultivation_realm}:{npc.current_state}:{npc.seclusion_started_month}:{npc.seclusion_until_month}:{npc.appearance_seed}:{npc.current_age}:{npc.gender}:{npc.faction_id}");
        }
        var validCount = rosterParts.Count;
        var stamp = string.Join(";", rosterParts);
        var headerTitle = context_id.StartsWith("world-cell:") ? "当前位置" : context_id.StartsWith("world:") ? "当前地点" : "在场人物";
        if (_world_mode && _worldNpcRosterCached && stamp == _worldRosterStamp)
        {
            var cachedSameContext = _roster_context == context_id;
            _roster_context = context_id;
            if (!cachedSameContext && GetNodeOrNull<ScrollContainer>("ListPanel/CharacterScroll") is { } cachedScroll) cachedScroll.ScrollVertical = 0;
            update_header(headerTitle, validCount);
            show_and_raise();
            return;
        }
        var sameContext = _world_mode && _roster_context == context_id;
        _world_mode = true;
        _roster_context = context_id;
        clear_children();
        _worldRosterStamp = stamp;
        _worldNpcRosterCached = true;
        if (!sameContext && GetNodeOrNull<ScrollContainer>("ListPanel/CharacterScroll") is { } scroll) scroll.ScrollVertical = 0;
        var count = 0;
        foreach (var npc in characters)
        {
            if (npc is null || !GodotObject.IsInstanceValid(npc)) continue;
            // Keep the complete scroll extent, but defer rich rows as well as portraits.
            var slot = new Control { CustomMinimumSize = new Vector2(0, RowHeight), MouseFilter = MouseFilterEnum.Ignore };
            _characters?.AddChild(slot);
            _pendingPortraits[slot] = () =>
            {
                var row = make_world_row(npc);
                slot.AddChild(row);
                row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            };
            count++;
        }
        if (count == 0)
            _characters?.AddChild(make_empty_state());
        update_header(headerTitle, count);
        // Movement updates the roster, not the user's expand/collapse preference.
        resize_panel();
        apply_expanded_state();
        show_and_raise();
    }

    /// <summary>Route travel keeps the last roster cached, but delays portrait work until arrival.</summary>
    public void suspend_world_npcs()
    {
        if (!_world_mode) return;
        RosterClosed?.Invoke();
        Hide();
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
        resize_panel();
        apply_expanded_state();
        show_and_raise();
    }

    /// <summary>地点场景将可交谈人物交给同一份左侧名单，不再另建人物面板。</summary>
    public void show_npc_action_rows(IEnumerable<(WorldNpcData npc, Action action)> rows, string context_id = "")
    {
        _world_mode = true;
        _roster_context = context_id;
        clear_children();
        var count = 0;
        foreach (var (npc, action) in rows)
        {
            var row = make_character_row(npc.npc_name, $"{npc.formal_identity}  ·  {npc.cultivation_realm}",
                portrait => portrait.CallDeferred("show_world_npc", npc), action);
            _characters?.AddChild(row);
            count++;
        }
        update_header("可交谈人物", count);
        _expanded = true;
        resize_panel();
        apply_expanded_state();
        show_and_raise();
    }

    public void hide_world_npcs()
    {
        if (!_world_mode) return;
        RosterClosed?.Invoke();
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
        var nowHour = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
        var activity = npc.is_in_seclusion_at_hour(nowHour) ? "闭关中" : npc.current_state;
        if (string.IsNullOrWhiteSpace(activity) || activity == "空闲") activity = "";
        var subtitle = $"{identity}  ·  {realm}" + (activity.Length > 0 ? $"  ·  {activity}" : "");
        return make_character_row(display, subtitle, portrait => portrait.CallDeferred("show_world_npc", npc), () => click(npc));
    }

    private Button make_character_row(string title, string subtitle, Action<PortraitRenderer> configure_portrait, Action on_pressed)
    {
        var row = new Button
        {
            Text = "",
            CustomMinimumSize = new Vector2(0, RowHeight),
            MouseFilter = MouseFilterEnum.Stop,
            FocusMode = FocusModeEnum.None,
            ClipContents = true,
        };
        row.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        row.AddThemeStyleboxOverride("hover", WorldHudStyle.QuietPaper(true, false));
        row.AddThemeStyleboxOverride("pressed", WorldHudStyle.QuietPaper(true, false));
        row.MouseDefaultCursorShape = CursorShape.PointingHand;
        var rule = WorldHudStyle.Rule();
        row.AddChild(rule);
        rule.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
        rule.OffsetTop = -1;

        var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 7);
        margin.AddThemeConstantOverride("margin_right", 7);
        margin.AddThemeConstantOverride("margin_top", 5);
        margin.AddThemeConstantOverride("margin_bottom", 5);
        row.AddChild(margin);

        var content = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        content.AddThemeConstantOverride("separation", 12);
        margin.AddChild(content);

        var portrait_frame = new PanelContainer { CustomMinimumSize = new Vector2(60, 60), MouseFilter = MouseFilterEnum.Ignore };
        portrait_frame.AddThemeStyleboxOverride("panel", new StyleBoxEmpty
        {
            ContentMarginLeft = 2, ContentMarginRight = 2, ContentMarginTop = 2, ContentMarginBottom = 2
        });
        var portraitPaper = new WorldHudFrame { Name = "PortraitInkFrame", ShowBehindParent = true };
        portrait_frame.AddChild(portraitPaper);
        content.AddChild(portrait_frame);
        var avatar_crop = new Control { Name = "AvatarCrop", ClipContents = true, CustomMinimumSize = new Vector2(60, 60), MouseFilter = MouseFilterEnum.Ignore };
        portrait_frame.AddChild(avatar_crop);
        _pendingPortraits[avatar_crop] = () =>
        {
            var portrait = new PortraitRenderer { CustomMinimumSize = new Vector2(60, 60), MouseFilter = MouseFilterEnum.Ignore };
            portrait.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            portrait.set_headshot_mode(true);
            avatar_crop.AddChild(portrait);
            configure_portrait(portrait);
        };

        var text_column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
        text_column.AddThemeConstantOverride("separation", 3);
        content.AddChild(text_column);
        var name_label = new Label { Text = title, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis, MouseFilter = MouseFilterEnum.Ignore };
        name_label.AddThemeFontSizeOverride("font_size", 20);
        name_label.AddThemeColorOverride("font_color", WorldHudStyle.Ink);
        text_column.AddChild(name_label);
        var details_label = new Label { Text = subtitle, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis, MouseFilter = MouseFilterEnum.Ignore };
        details_label.AddThemeFontSizeOverride("font_size", 15);
        details_label.AddThemeColorOverride("font_color", WorldHudStyle.SecondaryInk);
        text_column.AddChild(details_label);

        row.Pressed += on_pressed;
        return row;
    }

    private static Control make_empty_state()
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(0, 56), MouseFilter = MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        var label = new Label
        {
            Text = "此处暂无其他人物",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", 14);
        label.AddThemeColorOverride("font_color", new Color("6c614d"));
        panel.AddChild(label);
        return panel;
    }

    private Button make_row(string text, int font_size)
    {
        var row = new Button
        {
            Text = "",
            CustomMinimumSize = new Vector2(0, 38),
            MouseFilter = MouseFilterEnum.Stop,
            FocusMode = FocusModeEnum.None,
            ClipContents = true,
        };
        row.AddThemeFontSizeOverride("font_size", font_size);
        row.AddThemeColorOverride("font_color", new Color("453a27"));
        row.AddThemeColorOverride("font_hover_color", new Color("332818"));
        row.AddThemeStyleboxOverride("normal", row_style(new Color(.91f, .86f, .75f, .91f), new Color(.49f, .41f, .29f, .62f), 6));
        row.AddThemeStyleboxOverride("hover", row_style(new Color(.98f, .93f, .82f, .98f), new Color(.63f, .49f, .28f, .92f), 6));
        var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_top", 2);
        margin.AddThemeConstantOverride("margin_bottom", 2);
        var label = new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            ClipContents = true,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", font_size);
        label.AddThemeColorOverride("font_color", new Color("453a27"));
        margin.AddChild(label);
        row.AddChild(margin);
        return row;
    }

    private void toggle_expanded()
    {
        _expanded = !_expanded;
        if (!_expanded) RosterClosed?.Invoke();
        apply_expanded_state(true);
    }

    private void on_roster_visibility_changed()
    {
        if (!IsVisibleInTree()) RosterClosed?.Invoke();
    }

    private void apply_expanded_state(bool animate = false)
    {
        _toggle_tween?.Kill();
        _toggle_tween = null;
        if (animate)
        {
            _toggle_tween = CreateTween().SetParallel();
            _toggle_tween.SetTrans(Tween.TransitionType.Cubic);
            _toggle_tween.SetEase(Tween.EaseType.InOut);
        }
        if (_toggle_button is not null)
        {
            _toggle_button.Text = _expanded ? "◀" : "▶";
            _toggle_button.TooltipText = _expanded ? "收起人物名单" : "展开人物名单";
            _toggle_button.Size = new Vector2(30, 48);
            _toggle_button.ZIndex = 1;
            var targetPosition = new Vector2(_expanded ? PanelWidth : 0, (_expanded_height - _toggle_button.Size.Y) * .5f);
            if (animate)
            {
                _toggle_tween!.TweenProperty(_toggle_button, "position", targetPosition, .2);
            }
            else _toggle_button.Position = targetPosition;
        }
        Size = new Vector2(_expanded ? PanelWidth + 30 : 30, _expanded_height);
        if (_list_panel is not null)
        {
            _list_panel.AnchorLeft = 0;
            _list_panel.AnchorRight = 0;
            _list_panel.AnchorTop = 0;
            _list_panel.AnchorBottom = 1;
            _list_panel.OffsetLeft = 0;
            _list_panel.OffsetRight = PanelWidth;
            _list_panel.Visible = true;
            if (_characters is not null) _characters.Visible = _expanded;
            if (animate)
            {
                _toggle_tween!.TweenProperty(_list_panel, "position", _expanded ? Vector2.Zero : new Vector2(-PanelWidth, 0), .2);
            }
            else _list_panel.Position = _expanded ? Vector2.Zero : new Vector2(-PanelWidth, 0);
        }
    }

    private void resize_panel()
    {
        var viewport_height = GetViewportRect().Size.Y;
        var max_height = Mathf.Max(164f, viewport_height - 332f);
        if (_status_panel?.IsVisibleInTree() == true)
            max_height = Mathf.Min(max_height, Mathf.Max(164f, _status_panel.GlobalPosition.Y - 132 - 18));
        var count = Math.Clamp(_characters?.GetChildCount() ?? 0, 1, 7);
        var content_height = ContentTop + count * RowHeight + (count - 1) * RowSeparation + ContentBottom;
        _expanded_height = Mathf.Min(max_height, content_height);
        Size = new Vector2(PanelWidth + 30, _expanded_height);
    }

    private void refresh_layout()
    {
        resize_panel();
        apply_expanded_state();
        Position = new Vector2(22, 132);
    }

    public override void _ExitTree()
    {
        GetViewport().SizeChanged -= refresh_layout;
        VisibilityChanged -= on_roster_visibility_changed;
    }

    private void clear_children()
    {
        _pendingPortraits.Clear();
        _worldRosterStamp = "";
        _worldNpcRosterCached = false;
        if (_characters is null) return;
        foreach (var child in _characters.GetChildren()) { _characters.RemoveChild(child); child.QueueFree(); }
    }

    private void show_and_raise()
    {
        ZIndex = 10;
        Position = new Vector2(22, 132);
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
