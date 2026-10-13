using Godot;

public partial class InfoPanel
{
    private string _skillQuery = "", _skillSource = "", _skillKeyword = "";
    private VBoxContainer? _characterSkillList;
    private GridContainer? _characterSkillSlots;
    private Label? _characterSkillCount, _characterSkillMatches;
    private int _characterDropTarget = -1;

    private List<ActiveSkillData> character_skill_catalog(Character? human) => human?.get_available_battle_skills()
        .Where(Character.is_combat_skill_slot_eligible).Where(skill => !string.IsNullOrWhiteSpace(skill.ResourcePath))
        .Concat(human.get_combat_skill_slot_paths().Where(path => ResourceLoader.Exists(path)).Select(path => GD.Load<ActiveSkillData>(path)).OfType<ActiveSkillData>())
        .GroupBy(skill => skill.ResourcePath).Select(group => group.First()).ToList() ?? [];

    private string character_skill_source(Character? human, ActiveSkillData skill)
    {
        if (!string.IsNullOrWhiteSpace(skill.source_gongfa_name)) return skill.source_gongfa_name;
        if (human is not null)
            foreach (var book in character_manuals(human))
                if (character_manual_skills(book).Any(candidate => candidate.ResourcePath == skill.ResourcePath)) return book.gongfa_name;
        return "通用";
    }

    private string character_skill_detail(Character? human, ActiveSkillData skill) =>
        $"{battle_skill_category(skill)}\n关键词：{(skill.tags.Count > 0 ? string.Join(" · ", skill.tags) : "无")}\n精力 {skill.energy_cost}　法力 {skill.mana_cost}\n目标：{gongfa_skill_target(skill)}\n使用次数：{CardPresentation.CombatUseLimitText(skill, current_card, null)}\n{CardPresentation.CombatCooldownText(skill, current_card, null)}\n\n{CardPresentation.Face(skill, current_card)}\n\n来源：{character_skill_source(human, skill)}\n\n{CardPresentation.Details(skill, current_card)}";

    private void build_character_battle(Creature unit)
    {
        var page = _characterPages[2]; CharacterClear(page); var human = unit as Character;
        var header = CharacterPanel(page, 0, 0, 1350, 76); var top = CharacterBox(header);
        var filters = new HBoxContainer(); filters.AddThemeConstantOverride("separation", 14); top.AddChild(filters);
        var search = CharacterSearch("搜索技能名称或效果"); search.Text = _skillQuery; filters.AddChild(search);
        search.TextChanged += value => { _skillQuery = value; refresh_character_battle(unit); };
        var source = CharacterButton("来源秘籍：" + (_skillSource.Length == 0 ? "全部来源" : _skillSource) + " ▾", 20);
        source.CustomMinimumSize = new Vector2(300, 44); source.ClipText = true; filters.AddChild(source);
        source.Pressed += () => CharacterChoose("检索来源秘籍", character_skill_catalog(human).Select(skill => character_skill_source(human, skill)).Distinct().Prepend("全部来源"), value => { _skillSource = value == "全部来源" ? "" : value; source.Text = "来源秘籍：" + value + " ▾"; refresh_character_battle(unit); });
        var keyword = CharacterButton("关键词：" + (_skillKeyword.Length == 0 ? "全部" : _skillKeyword) + " ▾", 20); keyword.CustomMinimumSize = new Vector2(240, 44); keyword.ClipText = true; filters.AddChild(keyword);
        keyword.Pressed += () => CharacterChoose("检索技能关键词", character_skill_catalog(human).SelectMany(skill => skill.tags).Distinct().Prepend("全部"), value => { _skillKeyword = value == "全部" ? "" : value; keyword.Text = "关键词：" + value + " ▾"; refresh_character_battle(unit); });
        var reset = CharacterButton("重置", 20); reset.CustomMinimumSize = new Vector2(100, 44); filters.AddChild(reset);
        reset.Pressed += () => { _skillQuery = _skillSource = _skillKeyword = ""; _battleFilter = "all"; build_character_battle(unit); };
        var library = CharacterPanel(page, 0, 94, 354, 701); var box = CharacterBox(library, "已学技能");
        var categories = new HBoxContainer(); categories.AddThemeConstantOverride("separation", 4); box.AddChild(categories);
        foreach (var (title, id) in new[] { ("全部", "all"), ("攻伐", "attack"), ("术诀", "technique"), ("守御", "defense") })
        {
            var button = CharacterButton(title, 19, _battleFilter == id); button.SizeFlagsHorizontal = SizeFlags.ExpandFill; categories.AddChild(button);
            button.Pressed += () => { _battleFilter = id; foreach (var sibling in categories.GetChildren().OfType<Button>()) sibling.AddThemeStyleboxOverride("normal", CharacterButtonStyle(sibling == button)); refresh_character_battle(unit); };
        }
        _characterSkillMatches = CharacterText("", 17, true); box.AddChild(_characterSkillMatches);
        _characterSkillList = CharacterBox(CharacterScroll(box)); _characterSkillList.AddThemeConstantOverride("separation", 6);
        var loadout = CharacterPanel(page, 372, 94, 978, 701); var slotsBox = CharacterBox(loadout);
        var heading = new HBoxContainer(); slotsBox.AddChild(heading); var titleLabel = CharacterText("装配技能", 28); titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill; heading.AddChild(titleLabel);
        _characterSkillCount = CharacterText("", 23); _characterSkillCount.CustomMinimumSize = new Vector2(100, 0); _characterSkillCount.HorizontalAlignment = HorizontalAlignment.Right; heading.AddChild(_characterSkillCount); slotsBox.AddChild(new HSeparator());
        // 十个固定技能位直接分配两行空间，不再套滚动容器。
        _characterSkillSlots = new GridContainer { Columns = 5, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        _characterSkillSlots.AddThemeConstantOverride("h_separation", 14); _characterSkillSlots.AddThemeConstantOverride("v_separation", 14); slotsBox.AddChild(_characterSkillSlots);
        slotsBox.AddChild(CharacterText("悬停查看技能信息 · 点击招式卸载 · 拖拽调整顺序", 18, true));
        refresh_character_battle(unit);
    }

    private void refresh_character_battle(Creature unit)
    {
        if (_characterSkillList is null || _characterSkillSlots is null) return;
        hide_tooltip(); var human = unit as Character; CharacterClear(_characterSkillList); CharacterClear(_characterSkillSlots);
        var catalog = character_skill_catalog(human); var paths = human?.get_combat_skill_slot_paths().ToList() ?? [];
        var shown = catalog.Where(skill => _battleFilter == "all" || battle_skill_category(skill) == (_battleFilter switch { "attack" => "攻伐", "technique" => "术诀", _ => "守御" }))
            .Where(skill => _skillSource.Length == 0 || character_skill_source(human, skill) == _skillSource)
            .Where(skill => _skillKeyword.Length == 0 || skill.tags.Contains(_skillKeyword))
            .Where(skill => CharacterMatches($"{skill.skill_name} {string.Join(" ", skill.tags)} {skill.get_skill_description(current_card)}", _skillQuery)).ToList();
        _characterSkillMatches!.Text = $"匹配 {shown.Count} / {catalog.Count} 项"; _characterSkillCount!.Text = $"{paths.Count} / 10";
        foreach (var skill in shown)
        {
            var equipped = paths.Contains(skill.ResourcePath); var button = CharacterButton("", selected: equipped);
            button.Disabled = human is null || !equipped && paths.Count >= 10;
            button.CustomMinimumSize = new Vector2(0, 66); _characterSkillList.AddChild(button);
            button.AddChild(CombatSkillBar.CreateSkillIconVisual(skill, new Vector2(50, 50), new Vector2(8, 8)));
            var name = CharacterClippedText(skill.skill_name, 22); button.AddChild(name); CharacterRect(name, 70, 3, 230, 31);
            var tags = CharacterClippedText(skill.tags.Count > 0 ? string.Join(" · ", skill.tags) : battle_skill_category(skill), 16, true); button.AddChild(tags); CharacterRect(tags, 70, 34, 230, 26);
            CharacterHover(button, skill.skill_name, character_skill_detail(human, skill), CharacterLoadIcon(skill.icon_path));
            if (human is not null) bind_character_skill_drag(button, human, skill.ResourcePath, -1);
            button.Pressed += () =>
            {
                if (human is null || button.GetMeta("character_skill_dragging", false).AsBool()) return;
                if (_characterDropTarget >= 0 && !equipped && paths.Count < 10)
                { var next = paths.ToList(); next.Insert(Math.Min(_characterDropTarget, next.Count), skill.ResourcePath); human.set_combat_skill_slot_paths(new Godot.Collections.Array<string>(next)); _characterDropTarget = -1; refresh_character_battle(unit); }
                else toggle_battle_skill_slot(human, skill);
            };
        }
        if (shown.Count == 0) _characterSkillList.AddChild(CharacterParagraph("没有符合条件的技能\n调整条件或点击重置。", 21, true));
        for (var i = 0; i < 10; i++)
        {
            var index = i; var skill = i < paths.Count ? catalog.FirstOrDefault(skill => skill.ResourcePath == paths[i]) : null;
            var select = CharacterButton("", 21, index == _characterDropTarget, 4);
            select.CustomMinimumSize = new Vector2(174, 238); select.SizeFlagsHorizontal = select.SizeFlagsVertical = SizeFlags.ExpandFill;
            select.ClipContents = true; _characterSkillSlots.AddChild(select);
            if (human is not null) bind_character_skill_drag(select, human, skill?.ResourcePath ?? "", index);
            if (skill is not null)
            {
                var icon = new TextureRect { Texture = CharacterLoadIcon(skill.icon_path),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                    MouseFilter = MouseFilterEnum.Ignore };
                select.AddChild(icon); icon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
                icon.OffsetLeft = icon.OffsetTop = 4; icon.OffsetRight = -4; icon.OffsetBottom = -42;
                var name = CharacterClippedText(skill.skill_name, 22); name.HorizontalAlignment = HorizontalAlignment.Center; select.AddChild(name);
                name.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide); name.OffsetLeft = 6; name.OffsetRight = -6; name.OffsetTop = -38; name.OffsetBottom = -5;
                CharacterHover(select, skill.skill_name, character_skill_detail(human, skill), CharacterLoadIcon(skill.icon_path));
                select.Pressed += () =>
                {
                    if (human is null || select.GetMeta("character_skill_dragging", false).AsBool()) return;
                    _characterDropTarget = -1; remove_battle_skill(human, index);
                };
            }
            else
            {
                select.Text = "＋"; select.AddThemeFontSizeOverride("font_size", 42);
                var name = CharacterText(index == _characterDropTarget ? "选择左侧技能" : "空闲", 18, true); name.HorizontalAlignment = HorizontalAlignment.Center; select.AddChild(name);
                name.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide); name.OffsetLeft = 4; name.OffsetRight = -4; name.OffsetTop = -38; name.OffsetBottom = -5;
                select.Pressed += () => { _characterDropTarget = index; refresh_character_battle(unit); };
            }
        }
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
                var preview = CharacterText(ResourceLoader.Load<ActiveSkillData>(sourcePath)?.skill_name ?? "技能", 24);
                button.SetDragPreview(preview);
                return new Godot.Collections.Dictionary { ["character_skill"] = sourcePath, ["owner"] = human.GetInstanceId() };
            }),
            Callable.From<Vector2, Variant, bool>((position, data) => data.VariantType == Variant.Type.Dictionary
                && data.AsGodotDictionary().ContainsKey("character_skill")
                && data.AsGodotDictionary().GetValueOrDefault("owner", 0).AsUInt64() == human.GetInstanceId() && targetIndex >= 0),
            Callable.From<Vector2, Variant>((position, data) =>
            {
                var path = data.AsGodotDictionary()["character_skill"].AsString();
                var paths = human.get_combat_skill_slot_paths().ToList(); var source = paths.IndexOf(path);
                if (source < 0 && paths.Count >= 10) return;
                if (source < 0 && !character_skill_catalog(human).Any(skill => skill.ResourcePath == path)) return;
                if (source >= 0) paths.RemoveAt(source);
                paths.Insert(Math.Min(targetIndex, paths.Count), path);
                human.set_combat_skill_slot_paths(new Godot.Collections.Array<string>(paths));
                _characterDropTarget = -1; refresh_character_battle(human);
            }));
    }
}
