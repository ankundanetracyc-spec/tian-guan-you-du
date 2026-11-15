using Godot;

public partial class InfoPanel
{
    private string _skillQuery = "", _skillSource = "", _skillKeyword = "";
    private bool _showSkillNodeNetwork;
    private string _selectedSkillNodeId = "", _selectedSkillNodeFaceId = "";
    private float _skillNodeZoom = 1f;
    private Control? _skillNodeZoomViewport, _skillNodeZoomCanvas;
    private Label? _skillNodeZoomLabel;
    private bool _skillNodeMiddleZooming;
    private const string SmallSkillNodeTexturePath = "res://textures/ui/skill_nodes/skill_node_small_ink_circle_v1.png";
    private const string MultiSkillNodeTexturePath = "res://textures/ui/skill_nodes/skill_node_multi_ink_circle_v1.png";
    private const string SkillSlotLockTexturePath = "res://textures/ui/skill_slot_lock_v1.svg";
    private const float SkillNodeMinZoom = .65f, SkillNodeMaxZoom = 1.8f, SkillNodeZoomStep = .12f;
    private VBoxContainer? _characterSkillList;
    private GridContainer? _characterSkillSlots;
    private Label? _characterSkillCount, _characterSkillMatches;
    private int _characterDropTarget = -1;

    private List<ActiveSkillData> character_skill_catalog(Character? human)
    {
        if (human is null) return [];

        var keys = human.get_available_battle_skills().Select(Character.combat_skill_key)
            .Concat(human.get_default_combat_skill_paths()).Concat(human.get_combat_skill_slot_paths())
            .Concat(human.get_artifact_skills().Select(Character.combat_skill_key));
        return keys.Where(key => key.Length > 0).Distinct().Select(human.resolve_combat_skill)
            .OfType<ActiveSkillData>().Where(Character.is_combat_skill_slot_eligible).ToList();
    }

    private string character_skill_source(Character? human, ActiveSkillData skill)
    {
        if (skill.artifact_instance_id.Length > 0 || skill.forged_source_instance_id.Length > 0) return "法器";
        if (!string.IsNullOrWhiteSpace(skill.source_gongfa_name)) return skill.source_gongfa_name;
        if (human is not null)
        {
            foreach (var book in character_manuals(human))
                if (character_manual_skills(book).Any(candidate => Character.combat_skill_key(candidate) == Character.combat_skill_key(skill))) return book.gongfa_name;
            foreach (var node in human.get_skill_node_configs())
                if (human.has_unlocked_skill_node(node.node_id)
                    && node.get_skill_paths().Any(path => path == skill.ResourcePath || path == Character.combat_skill_key(skill)))
                    return "技能节点 · " + node.node_name;
        }
        return "通用";
    }

    private string character_skill_detail(Character? human, ActiveSkillData skill)
    {
        if (skill.has_board_definition())
        {
            var basis = ResourceLoader.Exists(skill.get_board_skill_key()) ? ResourceLoader.Load<ActiveSkillData>(skill.get_board_skill_key()) : skill;
            return CombatBoardPresentation.Skill(basis ?? skill, current_card)
                + "\n\n来源：" + character_skill_source(human, basis ?? skill);
        }
        return $"{battle_skill_category(skill)}\n关键词：{(skill.get_presentation_tags().Count > 0 ? string.Join(" · ", skill.get_presentation_tags()) : "无")}\n体力 {skill.energy_cost}　法力 {skill.mana_cost}\n目标：{gongfa_skill_target(skill)}\n使用次数：{CardPresentation.CombatUseLimitText(skill, current_card, null)}\n{CardPresentation.CombatCooldownText(skill, current_card, null)}\n\n{CardPresentation.Underline(CardPresentation.Face(skill, current_card))}{skill_rewrite_assignment_detail(human, skill)}\n\n来源：{character_skill_source(human, skill)}\n\n规则详解\n{CardPresentation.Underline(CardPresentation.Details(skill, current_card))}";
    }

    private void build_character_legacy_battle(Creature unit)
    {
        clear_skill_rewrite_drag_state();
        var page = _characterPages[2]; CharacterClear(page); var human = unit as Character;
        _characterSkillList = null; _characterSkillSlots = null; _characterSkillRewritePanel = null;
        _showSkillNodeNetwork = false;

        var filters = new HBoxContainer(); filters.AddThemeConstantOverride("separation", 12);
        CharacterBox(CharacterPanel(page, 0, 0, 1382, 76)).AddChild(filters);
        var search = CharacterSearch("搜索技能名称或效果"); search.Text = _skillQuery; filters.AddChild(search);
        search.TextChanged += value => { _skillQuery = value; refresh_character_battle(unit); };
        var source = CharacterButton("来源：" + (_skillSource.Length == 0 ? "全部" : _skillSource) + " ▾", 20);
        source.CustomMinimumSize = new Vector2(280, 44); source.ClipText = true; filters.AddChild(source);
        source.Pressed += () => CharacterChoose("检索技能来源", character_skill_catalog(human)
            .Select(skill => character_skill_source(human, skill)).Distinct().Prepend("全部来源"), value =>
            { _skillSource = value == "全部来源" ? "" : value; source.Text = "来源：" + (_skillSource.Length == 0 ? "全部" : _skillSource) + " ▾"; refresh_character_battle(unit); });
        var keyword = CharacterButton("关键词：" + (_skillKeyword.Length == 0 ? "全部" : _skillKeyword) + " ▾", 20);
        keyword.CustomMinimumSize = new Vector2(228, 44); keyword.ClipText = true; filters.AddChild(keyword);
        keyword.Pressed += () => CharacterChoose("检索技能关键词", character_skill_catalog(human)
            .SelectMany(skill => skill.get_presentation_tags()).Distinct().Prepend("全部"), value =>
            { _skillKeyword = value == "全部" ? "" : value; keyword.Text = "关键词：" + value + " ▾"; refresh_character_battle(unit); });
        var reset = CharacterButton("重置", 20); reset.CustomMinimumSize = new Vector2(90, 44); filters.AddChild(reset);
        reset.Pressed += () => { _skillQuery = _skillSource = _skillKeyword = ""; _battleFilter = "all"; build_character_battle(unit); };

        var library = CharacterBox(CharacterPanel(page, 0, 92, 330, 703), "已学技能");
        _battleFilter = "all";
        _characterSkillMatches = CharacterText("", 17, true); library.AddChild(_characterSkillMatches);
        _characterSkillList = CharacterBox(CharacterScroll(library)); _characterSkillList.Name = "CharacterSkillLibrary";
        _characterSkillList.AddThemeConstantOverride("separation", 6);

        var loadout = CharacterBox(CharacterPanel(page, 346, 92, 726, 703));
        var heading = new HBoxContainer(); loadout.AddChild(heading);
        var titleLabel = CharacterText("兽战装配", 28); titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill; heading.AddChild(titleLabel);
        _characterSkillCount = CharacterText("", 23); _characterSkillCount.Name = "CombatSlotCount";
        _characterSkillCount.HorizontalAlignment = HorizontalAlignment.Right; heading.AddChild(_characterSkillCount);
        loadout.AddChild(new HSeparator());
        _characterSkillSlots = new GridContainer { Name = "CombatSkillSlots", Columns = 5,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        _characterSkillSlots.AddThemeConstantOverride("h_separation", 10); _characterSkillSlots.AddThemeConstantOverride("v_separation", 14);
        loadout.AddChild(_characterSkillSlots);
        loadout.AddChild(CharacterParagraph("点击空槽后选择技能 · 点击已装配技能卸下 · 拖拽换位", 17, true));
        loadout.AddChild(CharacterText("人类对战在战前及每轮结束配置招式盘。", 17, true));
        build_character_skill_rewrites(page, human);
        refresh_character_battle(unit);
    }

    private void build_character_proficiency(Character? human)
    {
        clear_skill_rewrite_drag_state();
        var page = _characterPages[4]; CharacterClear(page);
        _showSkillNodeNetwork = true;
        build_character_skill_node_network(page, human);
    }

    private void build_character_skill_node_network(Control page, Character? human)
    {
        CharacterPanel(page, 0, 0, 1382, 795, 14);
        var graph = new Control { Name = "SkillNodeNetwork", ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
        page.AddChild(graph);
        CharacterRect(graph, 0, 0, 1382, 795); CharacterResponsive(graph, new Vector2(1382, 795));

        var viewport = new Control { Name = "SkillNodeViewport", ClipContents = true, MouseFilter = MouseFilterEnum.Stop };
        graph.AddChild(viewport); CharacterRect(viewport, 0, 0, 900, 795);
        var graphCanvas = new Control { Name = "SkillNodeCanvas", MouseFilter = MouseFilterEnum.Ignore };
        viewport.AddChild(graphCanvas); CharacterRect(graphCanvas, 0, 0, 900, 737);
        graphCanvas.Scale = Vector2.One * _skillNodeZoom;
        graphCanvas.Position = (viewport.Size - graphCanvas.Size * _skillNodeZoom) * .5f;

        if (human is null)
        {
            var empty = CharacterParagraph("当前人物没有可参悟的技能节点。", 24, true);
            graphCanvas.AddChild(empty); CharacterRect(empty, 125, 310, 650, 60);
            build_skill_node_zoom_controls(graph, viewport, graphCanvas);
            return;
        }

        var nodes = human.get_skill_node_configs().ToArray();
        var faces = skill_node_faces(human, nodes);
        if (faces.Count > 0 && !faces.Any(face => face.face_id == _selectedSkillNodeFaceId))
            _selectedSkillNodeFaceId = faces[0].face_id;
        build_skill_node_face_switcher(graph, human, faces, nodes);
        if (nodes.Length == 0)
        {
            var empty = CharacterParagraph("造诣图谱尚无可参悟内容。", 24, true);
            graphCanvas.AddChild(empty); CharacterRect(empty, 125, 310, 650, 60);
            build_skill_node_zoom_controls(graph, viewport, graphCanvas);
            return;
        }

        var nodesById = nodes.ToDictionary(node => node.node_id);
        var involvedFacesByNode = nodes.ToDictionary(node => node.node_id,
            node => skill_node_involved_face_ids(node, nodesById));
        var seamNodes = nodes.Where(node => involvedFacesByNode[node.node_id].Count > 1
            && involvedFacesByNode[node.node_id].Contains(_selectedSkillNodeFaceId)).ToArray();
        var seamNodeIds = seamNodes.Select(node => node.node_id).ToHashSet(StringComparer.Ordinal);
        var visibleNodeIds = nodes.Where(node => node.face_id == _selectedSkillNodeFaceId
                || seamNodeIds.Contains(node.node_id))
            .Select(node => node.node_id).ToHashSet(StringComparer.Ordinal);
        foreach (var seam in seamNodes)
            foreach (var parentId in seam.prerequisite_node_ids)
                if (nodesById.ContainsKey(parentId)) visibleNodeIds.Add(parentId);
        var visibleNodes = nodes.Where(node => visibleNodeIds.Contains(node.node_id)).ToArray();
        if (!visibleNodes.Any(node => node.node_id == _selectedSkillNodeId))
            _selectedSkillNodeId = visibleNodes.FirstOrDefault()?.node_id ?? "";

        var positions = visibleNodes.ToDictionary(node => node.node_id, node => node.graph_position);
        foreach (var node in visibleNodes)
        {
            var parents = node.prerequisite_node_ids.Where(visibleNodeIds.Contains).ToArray();
            foreach (var parentId in parents)
                add_skill_node_connection(graphCanvas, positions[parentId], node.graph_position, parents.Length > 1);
        }

        if (visibleNodes.Length == 0)
        {
            var empty = CharacterParagraph("此脉面尚无参悟节点。", 24, true);
            graphCanvas.AddChild(empty); CharacterRect(empty, 140, 310, 620, 60);
        }

        foreach (var node in visibleNodes)
        {
            var unlocked = human.has_unlocked_skill_node(node.node_id);
            var canUnlock = human.can_unlock_skill_node(node.node_id, out var reason);
            var major = node.prerequisite_node_ids.Count > 1;
            var size = major ? new Vector2(204, 204) : new Vector2(190, 190);
            var selected = node.node_id == _selectedSkillNodeId;
            var seam = involvedFacesByNode[node.node_id].Count > 1;
            var markerTexture = GD.Load<Texture2D>(major ? MultiSkillNodeTexturePath : SmallSkillNodeTexturePath);
            var marker = new TextureButton
            {
                Name = "SkillNode_" + node.node_id,
                TextureNormal = markerTexture,
                TextureHover = markerTexture,
                TexturePressed = markerTexture,
                IgnoreTextureSize = true,
                StretchMode = TextureButton.StretchModeEnum.Scale,
                FocusMode = FocusModeEnum.All,
                CustomMinimumSize = size,
                Size = size,
                Position = node.graph_position - size * .5f,
                SelfModulate = skill_node_marker_tint(unlocked, canUnlock, selected)
                    * (node.face_id == _selectedSkillNodeFaceId || seam ? Colors.White : new Color(1, 1, 1, .58f)),
                MouseDefaultCursorShape = CursorShape.PointingHand
            };
            marker.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
            marker.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
            marker.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
            marker.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
            graphCanvas.AddChild(marker);
            var name = CharacterText(node.node_name, 23);
            name.HorizontalAlignment = HorizontalAlignment.Center; name.VerticalAlignment = VerticalAlignment.Center; name.MouseFilter = MouseFilterEnum.Ignore;
            marker.AddChild(name); CharacterRect(name, 12, size.Y * .40f, size.X - 24, size.Y * .20f);
            var statusText = unlocked ? "已参悟" : canUnlock ? "可参悟" : "待契合";
            var state = CharacterText(statusText, 17, true);
            state.HorizontalAlignment = HorizontalAlignment.Center; state.VerticalAlignment = VerticalAlignment.Center; state.MouseFilter = MouseFilterEnum.Ignore;
            marker.AddChild(state); CharacterRect(state, 12, size.Y * .62f, size.X - 24, size.Y * .11f);
            marker.Pressed += () => { _selectedSkillNodeId = node.node_id; build_character_proficiency(human); };
            var contextualTint = node.face_id == _selectedSkillNodeFaceId || seam ? Colors.White : new Color(1, 1, 1, .58f);
            marker.MouseEntered += () => marker.SelfModulate = skill_node_marker_tint(unlocked, canUnlock, selected, true) * contextualTint;
            marker.MouseExited += () => marker.SelfModulate = skill_node_marker_tint(unlocked, canUnlock, selected) * contextualTint;
        }

        build_skill_node_zoom_controls(graph, viewport, graphCanvas);
        if (_selectedSkillNodeId.Length > 0)
            build_skill_node_details(graph, human, nodes.First(node => node.node_id == _selectedSkillNodeId));
        else
            build_skill_node_empty_details(graph, _selectedSkillNodeFaceId, faces);
    }

    private static List<SkillNodeFaceConfig> skill_node_faces(Character human, SkillNodeConfig[] nodes)
    {
        var faces = human.get_skill_node_face_configs().ToList();
        var knownIds = faces.Select(face => face.face_id).ToHashSet(StringComparer.Ordinal);
        foreach (var node in nodes.Where(node => node.face_id.Length > 0 && !knownIds.Contains(node.face_id)))
        {
            faces.Add(new SkillNodeFaceConfig { face_id = node.face_id, face_name = node.node_name, sort_order = faces.Count });
            knownIds.Add(node.face_id);
        }
        return faces.OrderBy(face => face.sort_order).ThenBy(face => face.face_name, StringComparer.Ordinal).ToList();
    }

    private void build_skill_node_face_switcher(Control graph, Character human, IReadOnlyList<SkillNodeFaceConfig> faces, SkillNodeConfig[] nodes)
    {
        var caption = CharacterText("脉面", 17, true);
        graph.AddChild(caption); CharacterRect(caption, 932, 16, 48, 28);
        var scroll = new ScrollContainer
        {
            Name = "SkillNodeFaceScroll",
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
            ClipContents = true
        };
        graph.AddChild(scroll); CharacterRect(scroll, 982, 5, 350, 52);
        var row = new HBoxContainer { Name = "SkillNodeFaces", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 0);
        scroll.AddChild(row);
        for (var i = 0; i < faces.Count; i++)
        {
            var face = faces[i];
            var faceNodeCount = nodes.Count(node => node.face_id == face.face_id);
            // Keep the four current face labels visible together; once the graph grows
            // beyond four faces, the existing horizontal scroll handles the overflow.
            var width = faces.Count <= 4
                ? Mathf.Min(118f, 350f / Mathf.Max(1, faces.Count))
                : Mathf.Clamp(76f + faceNodeCount * 3f, 88f, 118f);
            var active = face.face_id == _selectedSkillNodeFaceId;
            var button = CharacterButton(face.face_name, 17, active, 0);
            button.Name = "SkillNodeFace_" + face.face_id; button.CustomMinimumSize = new Vector2(width, 48);
            row.AddChild(button);
            button.Pressed += () =>
            {
                _selectedSkillNodeFaceId = face.face_id;
                _selectedSkillNodeId = nodes.FirstOrDefault(node => node.face_id == face.face_id)?.node_id ?? "";
                build_character_proficiency(human);
            };
        }
    }

    private static HashSet<string> skill_node_involved_face_ids(SkillNodeConfig node, IReadOnlyDictionary<string, SkillNodeConfig> nodesById)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (node.face_id.Length > 0) result.Add(node.face_id);
        foreach (var parentId in node.prerequisite_node_ids)
            if (nodesById.TryGetValue(parentId, out var parent) && parent.face_id.Length > 0)
                result.Add(parent.face_id);
        return result;
    }

    private void build_skill_node_empty_details(Control graph, string faceId, IReadOnlyList<SkillNodeFaceConfig> faces)
    {
        var faceName = faces.FirstOrDefault(face => face.face_id == faceId)?.face_name ?? "当前脉面";
        var frame = CharacterPanel(graph, 924, 58, 410, 705, 16);
        var column = CharacterBox(frame); column.AddThemeConstantOverride("separation", 8);
        column.AddChild(CharacterText(faceName, 25));
        column.AddChild(new HSeparator());
        column.AddChild(CharacterParagraph("此脉面尚无参悟节点。加入节点后，会沿所属脉面展开；与其他脉面相连的节点会显示在交界处。", 18, true));
    }

    private void build_skill_node_zoom_controls(Control graph, Control viewport, Control canvas)
    {
        _skillNodeZoomViewport = viewport;
        _skillNodeZoomCanvas = canvas;
        var controls = new HBoxContainer { Name = "SkillNodeZoomControls", MouseFilter = MouseFilterEnum.Stop };
        controls.AddThemeConstantOverride("separation", 6);
        graph.AddChild(controls); CharacterRect(controls, 20, 741, 245, 40);
        var zoomOut = CharacterButton("−", 22); zoomOut.Name = "SkillNodeZoomOut"; zoomOut.CustomMinimumSize = new Vector2(42, 38); controls.AddChild(zoomOut);
        var zoomLabel = CharacterText($"{Mathf.RoundToInt(_skillNodeZoom * 100)}%", 17, true);
        zoomLabel.CustomMinimumSize = new Vector2(62, 38); zoomLabel.HorizontalAlignment = HorizontalAlignment.Center;
        zoomLabel.VerticalAlignment = VerticalAlignment.Center;
        zoomLabel.TooltipText = "滚轮缩放；按住鼠标中键上下拖动缩放";
        _skillNodeZoomLabel = zoomLabel;
        controls.AddChild(zoomLabel);
        var zoomIn = CharacterButton("＋", 22); zoomIn.Name = "SkillNodeZoomIn"; zoomIn.CustomMinimumSize = new Vector2(42, 38); controls.AddChild(zoomIn);
        var reset = CharacterButton("复位", 16); reset.Name = "SkillNodeZoomReset"; reset.CustomMinimumSize = new Vector2(64, 38); controls.AddChild(reset);
        zoomOut.Pressed += () => set_skill_node_zoom(canvas, viewport, zoomLabel, _skillNodeZoom / (1 + SkillNodeZoomStep), viewport.Size * .5f);
        zoomIn.Pressed += () => set_skill_node_zoom(canvas, viewport, zoomLabel, _skillNodeZoom * (1 + SkillNodeZoomStep), viewport.Size * .5f);
        reset.Pressed += () => set_skill_node_zoom(canvas, viewport, zoomLabel, 1f, viewport.Size * .5f);
    }

    public override void _Input(InputEvent @event)
    {
        handle_skill_rewrite_pointer_input(@event);
        var viewport = _skillNodeZoomViewport;
        var canvas = _skillNodeZoomCanvas;
        var label = _skillNodeZoomLabel;
        if (viewport is null || canvas is null || label is null
            || !GodotObject.IsInstanceValid(viewport) || !GodotObject.IsInstanceValid(canvas) || !GodotObject.IsInstanceValid(label)
            || !viewport.IsInsideTree() || !viewport.IsVisibleInTree())
        {
            _skillNodeMiddleZooming = false;
            return;
        }

        var pointer = GetGlobalMousePosition();
        var pointerInsideGraph = viewport.GetGlobalRect().HasPoint(pointer);
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.Middle)
            {
                if (mouseButton.Pressed && pointerInsideGraph)
                {
                    _skillNodeMiddleZooming = true;
                    GetViewport().SetInputAsHandled();
                }
                else if (!mouseButton.Pressed && _skillNodeMiddleZooming)
                {
                    _skillNodeMiddleZooming = false;
                    GetViewport().SetInputAsHandled();
                }
                return;
            }

            if (mouseButton.Pressed && pointerInsideGraph
                && mouseButton.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
            {
                var nextZoom = mouseButton.ButtonIndex == MouseButton.WheelUp
                    ? _skillNodeZoom * (1 + SkillNodeZoomStep)
                    : _skillNodeZoom / (1 + SkillNodeZoomStep);
                set_skill_node_zoom(canvas, viewport, label, nextZoom, viewport.GetLocalMousePosition());
                GetViewport().SetInputAsHandled();
            }
            return;
        }

        if (_skillNodeMiddleZooming && @event is InputEventMouseMotion motion)
        {
            var nextZoom = _skillNodeZoom * Mathf.Pow(1.005f, -motion.Relative.Y);
            set_skill_node_zoom(canvas, viewport, label, nextZoom, viewport.GetLocalMousePosition());
            GetViewport().SetInputAsHandled();
        }
    }

    private void set_skill_node_zoom(Control canvas, Control viewport, Label label, float zoom, Vector2 anchor)
    {
        var contentAnchor = (anchor - canvas.Position) / _skillNodeZoom;
        _skillNodeZoom = Mathf.Clamp(zoom, SkillNodeMinZoom, SkillNodeMaxZoom);
        canvas.Scale = Vector2.One * _skillNodeZoom;
        canvas.Position = anchor - contentAnchor * _skillNodeZoom;
        label.Text = $"{Mathf.RoundToInt(_skillNodeZoom * 100)}%";
    }

    private static void add_skill_node_connection(Control graph, Vector2 from, Vector2 to, bool major)
    {
        var wash = new Line2D { Width = major ? 9 : 7, DefaultColor = new Color("49453f", .13f), Antialiased = true };
        wash.AddPoint(from); wash.AddPoint(to); graph.AddChild(wash);
        var ink = new Line2D { Width = major ? 3.5f : 2.5f, DefaultColor = new Color("625e56", .58f), Antialiased = true };
        ink.AddPoint(from); ink.AddPoint(to); graph.AddChild(ink);
    }

    private static Color skill_node_marker_tint(bool unlocked, bool canUnlock, bool selected, bool hovering = false)
    {
        var tint = unlocked ? new Color("ffffff") : canUnlock ? new Color("e6e6e2") : new Color("b5b3af");
        if (selected) tint = tint.Lerp(new Color("d9d2bf"), .22f);
        if (hovering) tint = tint.Lightened(.12f);
        return tint;
    }

    private void build_skill_node_details(Control graph, Character human, SkillNodeConfig node)
    {
        var grantedSkills = node.get_skill_paths()
            .Select(path => ResourceLoader.Load<ActiveSkillData>(path))
            .OfType<ActiveSkillData>().ToArray();
        var unlocked = human.has_unlocked_skill_node(node.node_id);
        var canUnlock = human.can_unlock_skill_node(node.node_id, out var reason);
        var major = node.prerequisite_node_ids.Count > 1;
        var nodesById = human.get_skill_node_configs().ToDictionary(candidate => candidate.node_id);
        var facesById = skill_node_faces(human, nodesById.Values.ToArray()).ToDictionary(face => face.face_id);
        var involvedFaceIds = skill_node_involved_face_ids(node, nodesById)
            .OrderBy(id => facesById.TryGetValue(id, out var face) ? face.sort_order : int.MaxValue)
            .ThenBy(id => id, StringComparer.Ordinal).ToArray();
        var faceNames = involvedFaceIds.Select(id => facesById.TryGetValue(id, out var face) ? face.face_name : id).ToArray();
        var faceSummary = faceNames.Length > 1
            ? "交界节点 · " + string.Join(" × ", faceNames)
            : (major ? "多前置节点" : node.prerequisite_node_ids.Count == 0 ? "初始根节点" : "单前置节点")
                + (faceNames.Length == 1 ? " · " + faceNames[0] : "");
        var frame = CharacterPanel(graph, 924, 58, 410, 705, 16);
        var column = CharacterBox(frame); column.AddThemeConstantOverride("separation", 8);
        column.AddChild(CharacterText("参悟详情", 25));
        column.AddChild(new HSeparator());

        var heading = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        heading.AddThemeConstantOverride("separation", 12); column.AddChild(heading);
        var markerTexture = GD.Load<Texture2D>(major ? MultiSkillNodeTexturePath : SmallSkillNodeTexturePath);
        var markerPreview = new Control { CustomMinimumSize = new Vector2(84, 84), MouseFilter = MouseFilterEnum.Ignore };
        var markerPreviewFrame = new TextureRect
        {
            Texture = markerTexture, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore
        };
        markerPreview.AddChild(markerPreviewFrame); markerPreviewFrame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        heading.AddChild(markerPreview);
        var identity = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        identity.AddThemeConstantOverride("separation", 3); heading.AddChild(identity);
        identity.AddChild(CharacterText(node.node_name, 28));
        identity.AddChild(CharacterClippedText(faceSummary, 17, true));

        var description = CharacterParagraph(node.description, 18, true);
        description.CustomMinimumSize = new Vector2(0, 54); column.AddChild(description);
        column.AddChild(new HSeparator());

        var scroll = CharacterScroll(column);
        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.Fill };
        content.AddThemeConstantOverride("separation", 6); scroll.AddChild(content);
        content.AddChild(CharacterText("解锁技能", 20));
        if (grantedSkills.Length == 0) content.AddChild(CharacterText("未配置技能", 17, true));
        else foreach (var grantedSkill in grantedSkills)
            content.AddChild(CharacterParagraph("· " + grantedSkill.skill_name, 17, true));

        content.AddChild(CharacterText("关联脉面", 20));
        if (involvedFaceIds.Length == 0) content.AddChild(CharacterText("未指定脉面", 17, true));
        else
        {
            foreach (var faceId in involvedFaceIds)
            {
                var faceName = facesById.TryGetValue(faceId, out var face) ? face.face_name : faceId;
                var relation = faceId == node.face_id ? "节点所属" : "前置来源";
                content.AddChild(CharacterParagraph($"· {faceName}　{relation}", 17, true));
            }
        }

        content.AddChild(CharacterText("前置节点", 20));
        if (node.prerequisite_node_ids.Count == 0) content.AddChild(CharacterText("无 · 可由此脉络直接参悟", 17, true));
        else
        {
            foreach (var parentId in node.prerequisite_node_ids)
            {
                var parentName = nodesById.TryGetValue(parentId, out var parent) ? parent.node_name : parentId;
                var parentState = human.has_unlocked_skill_node(parentId) ? "已参悟" : "尚未参悟";
                content.AddChild(CharacterParagraph($"· {parentName}  {parentState}", 17, true));
            }
        }

        content.AddChild(CharacterText("属性与造诣", 20));
        if (node.required_attributes.Count == 0) content.AddChild(CharacterText("无额外要求", 17, true));
        foreach (Variant key in node.required_attributes.Keys)
        {
            var id = key.AsString(); var required = node.required_attributes[id].AsSingle();
            var current = CardRules.Number(human, id); var met = current + .0001f >= required;
            var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            var label = CharacterText(skill_node_attribute_name(id), 18, true); label.SizeFlagsHorizontal = SizeFlags.ExpandFill; row.AddChild(label);
            var value = CharacterText($"{current:0.#} / {required:0.#}", 18);
            value.HorizontalAlignment = HorizontalAlignment.Right;
            value.AddThemeColorOverride("font_color", met ? new Color("536e5c") : new Color("8a5147")); row.AddChild(value);
            content.AddChild(row);
        }

        content.AddChild(CharacterText("当前状态", 20));
        content.AddChild(CharacterParagraph(unlocked ? "已参悟。授予技能已收入技能库，仍需自行装配。"
            : canUnlock ? "条件已齐，可以直接参悟；不消耗修习点。" : reason, 17, true));

        var learn = CharacterButton(unlocked ? "已参悟" : "参悟此节点", 19, unlocked, 7);
        learn.Name = "SkillNodeLearn";
        learn.CustomMinimumSize = new Vector2(0, 42); learn.Disabled = unlocked || !canUnlock; column.AddChild(learn);
        learn.Pressed += () => { if (human.unlock_skill_node(node.node_id)) build_character_proficiency(human); };
    }

    private static string skill_node_attribute_name(string id) => id switch
    {
        "physique" => "体魄",
        "intelligence" => "灵慧",
        "soul" => "神魂",
        "body_proficiency" => "体道造诣",
        "magic_proficiency" => "术法造诣",
        "sword_proficiency" => "剑道造诣",
        _ => id
    };

    private void refresh_character_battle(Creature unit)
    {
        if (_combatBoardPage is not null && GodotObject.IsInstanceValid(_combatBoardPage) && _combatBoardPage.Actor == unit)
        { _combatBoardPage.Refresh(); return; }
        if (_showSkillNodeNetwork || _characterSkillList is null || _characterSkillSlots is null) return;
        hide_tooltip(); var human = unit as Character;
        CharacterClear(_characterSkillList); CharacterClear(_characterSkillSlots);
        prepare_skill_rewrite_ui_state(human);
        var catalog = character_skill_catalog(human);
        var paths = human?.get_combat_skill_slot_paths().ToList() ?? Enumerable.Repeat("", 10).ToList();
        var shown = catalog.Where(skill => _battleFilter == "all" || battle_skill_category(skill) == (_battleFilter switch { "attack" => "攻伐", "technique" => "术诀", _ => "守御" }))
            .Where(skill => _skillSource.Length == 0 || character_skill_source(human, skill) == _skillSource)
            .Where(skill => _skillKeyword.Length == 0 || skill.get_presentation_tags().Contains(_skillKeyword))
            .Where(skill => CharacterMatches($"{skill.skill_name} {string.Join(" ", skill.get_presentation_tags())} {skill.get_skill_description(unit)}", _skillQuery)).ToList();
        _characterSkillMatches!.Text = $"匹配 {shown.Count} / {catalog.Count} 项";
        _characterSkillCount!.Text = $"{paths.Count(path => path.Length > 0)} / 10";
        foreach (var skill in shown)
        {
            var key = Character.combat_skill_key(skill); var equipped = paths.Contains(key);
            var button = CharacterButton("", selected: equipped); button.SetMeta("character_skill_key", key);
            button.Disabled = human is null || !equipped && !paths.Contains("");
            button.CustomMinimumSize = new Vector2(0, 66); button.ClipContents = true; _characterSkillList.AddChild(button);
            button.AddChild(CombatSkillBar.CreateSkillIconVisual(skill, new Vector2(50, 50), new Vector2(8, 8)));
            var name = CharacterClippedText(skill.skill_name, 22); button.AddChild(name); CharacterRect(name, 70, 3, 215, 31);
            var tags = CharacterClippedText(skill.get_presentation_tags().Count > 0 ? string.Join(" · ", skill.get_presentation_tags()) : battle_skill_category(skill), 16, true);
            button.AddChild(tags); CharacterRect(tags, 70, 34, 215, 26);
            CharacterHover(button, skill.skill_name, character_skill_detail(human, skill), CharacterLoadIcon(skill.icon_path));
            if (human is not null) bind_character_skill_drag(button, human, key, -1);
            button.Pressed += () =>
            {
                if (human is null || button.GetMeta("character_skill_dragging", false).AsBool()) return;
                if (_characterDropTarget >= 0 && !equipped)
                {
                    paths[_characterDropTarget] = key;
                    human.set_combat_skill_slot_paths(new Godot.Collections.Array<string>(paths));
                    _characterDropTarget = -1; refresh_character_battle(unit);
                }
                else { _characterDropTarget = -1; toggle_battle_skill_slot(human, skill); }
            };
        }
        if (shown.Count == 0) _characterSkillList.AddChild(CharacterParagraph("没有符合条件的技能\n调整条件或点击重置。", 21, true));
        for (var i = 0; i < 10; i++)
        {
            var index = i; var skill = human?.resolve_combat_skill(paths[index]);
            var automatic = index == 9 && human?.has_xuanshu_heart_method_equipped() == true;
            var slot = CharacterButton("", 21, index == _characterDropTarget, 4);
            slot.Name = "CombatSkillSlot_" + (index + 1); slot.SetMeta("character_skill_key", paths[index]);
            slot.CustomMinimumSize = new Vector2(120, 222); slot.SizeFlagsHorizontal = slot.SizeFlagsVertical = SizeFlags.ExpandFill;
            slot.ClipContents = true; _characterSkillSlots.AddChild(slot);
            if (human is not null) bind_character_skill_drag(slot, human, skill is null ? "" : paths[index], index);
            if (skill is not null)
            {
                var icon = CharacterIcon(CharacterLoadIcon(skill.icon_path)); slot.AddChild(icon);
                icon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); icon.OffsetLeft = icon.OffsetTop = 4; icon.OffsetRight = -4; icon.OffsetBottom = -42;
                var name = CharacterClippedText(skill.skill_name, 22); name.HorizontalAlignment = HorizontalAlignment.Center; slot.AddChild(name);
                name.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide); name.OffsetLeft = 6; name.OffsetRight = -6; name.OffsetTop = -38; name.OffsetBottom = -5;
                CharacterHover(slot, skill.skill_name, character_skill_detail(human, skill), CharacterLoadIcon(skill.icon_path));
                var badgeText = skill_rewrite_badge_text(human, skill);
                if (badgeText.Length > 0)
                {
                    var badge = create_skill_rewrite_badge(badgeText, skill_rewrite_badge_active(human, skill)); slot.AddChild(badge);
                    CharacterRect(badge, 7, 7, 30, 26);
                }
                slot.Pressed += () =>
                {
                    if (human is null || slot.GetMeta("character_skill_dragging", false).AsBool()) return;
                    _characterDropTarget = -1; remove_battle_skill(human, index);
                };
            }
            else
            {
                slot.Text = "＋"; slot.AddThemeFontSizeOverride("font_size", 42);
                var name = CharacterText(index == _characterDropTarget ? "选择左侧技能" : "空闲", 17, true);
                name.HorizontalAlignment = HorizontalAlignment.Center; slot.AddChild(name);
                name.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide); name.OffsetLeft = 4; name.OffsetRight = -4; name.OffsetTop = -38; name.OffsetBottom = -5;
                slot.Pressed += () => { _characterDropTarget = index; refresh_character_battle(unit); };
            }
            if (automatic) add_skill_slot_lock_overlay(slot, new Vector2(84, 7), new Vector2(28, 28));
        }
        refresh_character_skill_rewrites(human);
        update_skill_rewrite_drag_slot_visuals();
    }

    private static void add_skill_slot_lock_overlay(Control slot, Vector2 position, Vector2 size, int zIndex = 10)
    {
        var overlay=new TextureRect
        {
            Name="AutomaticSkillSlotLock",
            ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,
            Texture=GD.Load<Texture2D>(SkillSlotLockTexturePath),
            Position=position,
            Size=size,
            MouseFilter=MouseFilterEnum.Ignore,
            ZIndex=zIndex
        };
        slot.AddChild(overlay);
    }

    private void bind_character_skill_drag(Button button, Character human, string sourcePath, int targetIndex)
    {
        button.GuiInput += input =>
        {
            if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
                button.SetMeta("character_skill_dragging", false);
        };
        button.SetDragForwarding(
            Callable.From<Vector2, Variant>(position =>
            {
                if (sourcePath.Length == 0) return default;
                hide_tooltip(); button.SetMeta("character_skill_dragging", true);
                var preview = CharacterText(human.resolve_combat_skill(sourcePath)?.skill_name ?? "技能", 24);
                button.SetDragPreview(preview);
                return new Godot.Collections.Dictionary { ["character_skill"] = sourcePath, ["owner"] = human.GetInstanceId() };
            }),
            Callable.From<Vector2, Variant, bool>((position, data) =>
            {
                if (can_drop_skill_rewrite_on_slot(targetIndex, data, human)) return true;
                if (data.VariantType != Variant.Type.Dictionary || !data.AsGodotDictionary().ContainsKey("character_skill")
                    || data.AsGodotDictionary().GetValueOrDefault("owner", 0).AsUInt64() != human.GetInstanceId() || targetIndex < 0) return false;
                var skill = human.resolve_combat_skill(data.AsGodotDictionary()["character_skill"].AsString());
                return !(targetIndex==9&&human.has_xuanshu_heart_method_equipped()&&skill is not null&&CardRules.IsResponse(skill));
            }),
            Callable.From<Vector2, Variant>((position, data) =>
            {
                if (data.VariantType == Variant.Type.Dictionary && data.AsGodotDictionary().ContainsKey("skill_rewrite_id"))
                {
                    drop_skill_rewrite_on_slot(targetIndex, data, human);
                    return;
                }
                var path = data.AsGodotDictionary()["character_skill"].AsString();
                var paths = human.get_combat_skill_slot_paths().ToList(); var source = paths.IndexOf(path);
                
                if (source < 0 && !character_skill_catalog(human).Any(skill => Character.combat_skill_key(skill) == path)) return;
                if(targetIndex==9&&human.has_xuanshu_heart_method_equipped()&&human.resolve_combat_skill(path) is { } draggedSkill&&CardRules.IsResponse(draggedSkill))return;
                if (source >= 0) (paths[source], paths[targetIndex]) = (paths[targetIndex], paths[source]);
                else paths[targetIndex] = path;
                human.set_combat_skill_slot_paths(new Godot.Collections.Array<string>(paths));
                _characterDropTarget = -1; refresh_character_battle(human);
            }));
    }
}
