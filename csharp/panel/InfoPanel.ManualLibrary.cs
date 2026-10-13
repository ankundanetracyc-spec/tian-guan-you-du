using Godot;

public partial class InfoPanel
{
    private string _manualQuery = "", _manualFilter = "全部", _manualKeyword = "";
    private VBoxContainer? _manualList;
    private Control? _manualWorkspace;
    private HBoxContainer? _manualSlots;
    private Label? _manualCounter;
    private readonly Dictionary<string, Texture2D?> _manualIcons = [];

    private List<GongfaResource> character_manuals(Character human)
    {
        var books = human.get_known_gongfas().ToList();
        foreach (var path in human.pack_items)
            if (ResourceLoader.Exists(path) && ResourceLoader.Load<ItemData>(path)?.gongfa is GongfaResource book) books.Add(book);
        return books.GroupBy(book => book.ResourcePath).Select(group => group.First()).ToList();
    }

    private Texture2D? character_manual_icon(Character human, GongfaResource book)
    {
        if (_manualIcons.TryGetValue(book.ResourcePath, out var cached)) return cached;
        foreach (var path in human.pack_items)
            if (ResourceLoader.Exists(path) && ResourceLoader.Load<ItemData>(path) is { } item && item.gongfa?.ResourcePath == book.ResourcePath)
                return _manualIcons[book.ResourcePath] = CharacterLoadIcon(item.icon_path);
        var sheet = GD.Load<Texture2D>("res://textures/ui/inventory_20261012/category_icons_v1.png");
        return _manualIcons[book.ResourcePath] = new AtlasTexture { Atlas = sheet, Region = new Rect2(sheet.GetWidth() * .75f, 0, sheet.GetWidth() / 4f, sheet.GetHeight() / 3f) };
    }

    private static Texture2D? CharacterLoadIcon(string path) => !string.IsNullOrWhiteSpace(path) && ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;

    private static IEnumerable<ActiveSkillData> character_manual_skills(GongfaResource book) =>
        book.granted_active_skills.Concat(book.get_sorted_chapters().SelectMany(chapter => chapter.get_learned_skills(true).OfType<ActiveSkillData>()))
            .Concat(book.get_sorted_chapters().SelectMany(chapter => chapter.get_deck_rewrites(true).Where(rewrite => rewrite.enabled).Select(rewrite => rewrite.get_result_skill(true)).OfType<ActiveSkillData>()))
            .Concat(new[] { book.get_heart_method_skill(true), book.get_heart_method_skill(false) }.OfType<ActiveSkillData>());

    private static IEnumerable<string> character_manual_tags(GongfaResource book) => character_manual_skills(book).SelectMany(skill => skill.tags).Distinct();
    private static string character_manual_search(GongfaResource book) => string.Join(" ",
        new[] { book.gongfa_name, book.description }.Concat(character_manual_skills(book).Select(skill => $"{skill.skill_name} {string.Join(" ", skill.tags)} {skill.description}"))
            .Concat(book.get_sorted_chapters().Select(chapter => $"{chapter.chapter_name} {chapter.heart_method_description} {string.Join(" ", chapter.learned_world_spells.OfType<WorldSpellData>().Select(spell => spell.get_manual_detail_text(false)))}")));
    private static bool CharacterMatches(string haystack, string query) => query.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).All(term => haystack.Contains(term, StringComparison.OrdinalIgnoreCase));

    private void build_character_gongfa(Character human)
    {
        var page = _characterPages[3]; CharacterClear(page);
        var slotScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Auto, VerticalScrollMode = ScrollContainer.ScrollMode.Disabled };
        page.AddChild(slotScroll); CharacterRect(slotScroll, 0, 0, 1350, 80);
        _manualSlots = new HBoxContainer(); _manualSlots.AddThemeConstantOverride("separation", 12); slotScroll.AddChild(_manualSlots);
        var filters = new HBoxContainer(); filters.AddThemeConstantOverride("separation", 10); page.AddChild(filters); CharacterRect(filters, 0, 94, 1350, 44);
        var search = CharacterSearch("检索秘籍名、关键词、习得内容"); search.Text = _manualQuery; filters.AddChild(search);
        search.TextChanged += value => { _manualQuery = value; refresh_character_gongfa(human); };
        foreach (var text in new[] { "全部", "已学", "持有" })
        {
            var button = CharacterButton(text, 20, text == _manualFilter); button.CustomMinimumSize = new Vector2(80, 44); filters.AddChild(button);
            button.Pressed += () => { _manualFilter = text; foreach (var sibling in filters.GetChildren().OfType<Button>().Where(b => b.HasMeta("manual_status"))) sibling.AddThemeStyleboxOverride("normal", CharacterButtonStyle(sibling == button)); refresh_character_gongfa(human); };
            button.SetMeta("manual_status", true);
        }
        var keyword = CharacterButton(_manualKeyword.Length == 0 ? "关键词：全部 ▾" : _manualKeyword + " ▾", 20); keyword.CustomMinimumSize = new Vector2(190, 44); keyword.ClipText = true; filters.AddChild(keyword);
        keyword.Pressed += () => CharacterChoose("检索关键词", character_manuals(human).SelectMany(character_manual_tags).Distinct().Prepend("全部"), value => { _manualKeyword = value == "全部" ? "" : value; keyword.Text = "关键词：" + value + " ▾"; refresh_character_gongfa(human); });
        var reset = CharacterButton("重置", 20); filters.AddChild(reset); reset.Pressed += () => { _manualQuery = ""; _manualFilter = "全部"; _manualKeyword = ""; build_character_gongfa(human); };
        var library = CharacterPanel(page, 0, 154, 380, 641); var listBox = CharacterBox(library);
        var heading = new HBoxContainer(); listBox.AddChild(heading); var title = CharacterText("秘籍库", 29); title.SizeFlagsHorizontal = SizeFlags.ExpandFill; heading.AddChild(title);
        _manualCounter = CharacterText("", 18, true); _manualCounter.CustomMinimumSize = new Vector2(100, 0); _manualCounter.HorizontalAlignment = HorizontalAlignment.Right; heading.AddChild(_manualCounter); listBox.AddChild(new HSeparator());
        _manualList = CharacterBox(CharacterScroll(listBox));
        _manualWorkspace = new Control(); page.AddChild(_manualWorkspace); CharacterRect(_manualWorkspace, 396, 154, 954, 641);
        refresh_character_gongfa(human);
    }

    private void refresh_character_gongfa(Character human)
    {
        if (_manualList is null || _manualWorkspace is null || _manualSlots is null) return;
        hide_tooltip(); CharacterClear(_manualSlots); CharacterClear(_manualList);
        void Slot(string caption, GongfaResource? book, int index)
        {
            var button = CharacterButton("", 22, index < 0);
            button.CustomMinimumSize = new Vector2(318, 78); _manualSlots.AddChild(button);
            var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; row.AddThemeConstantOverride("separation", 8);
            button.AddChild(row); row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); row.OffsetLeft = 14; row.OffsetRight = -10; row.OffsetTop = 10; row.OffsetBottom = -10;
            var captionLabel = CharacterText(caption, 19); captionLabel.CustomMinimumSize = new Vector2(index < 0 ? 40 : 60, 0); row.AddChild(captionLabel);
            row.AddChild(CharacterIcon(book is null ? null : character_manual_icon(human, book), 44));
            var name = CharacterClippedText(book?.gongfa_name ?? "尚未设置", 20); name.SizeFlagsHorizontal = SizeFlags.ExpandFill; row.AddChild(name);
            var change = CharacterButton("›", 26); change.CustomMinimumSize = new Vector2(30, 0); change.AddThemeStyleboxOverride("normal", new StyleBoxEmpty()); row.AddChild(change); change.Pressed += () => show_character_manual_picker(human, index);
            if (book is not null) CharacterHover(button, book.gongfa_name, book.description, character_manual_icon(human, book));
            button.Pressed += () => { if (book is null) show_character_manual_picker(human, index); else { _viewedGongfa = book; _manualQuery = _manualKeyword = ""; _manualFilter = "全部"; build_character_gongfa(human); } };
        }
        Slot("主修", human.main_gongfa, -1);
        for (var i = 0; i < human.max_sub_slots; i++) Slot("辅修" + chinese_volume_number(i + 1), i < human.sub_gongfa_list.Count ? human.sub_gongfa_list[i] : null, i);
        var all = character_manuals(human);
        bool Held(GongfaResource book) => human.pack_items.Any(path => ResourceLoader.Exists(path) && ResourceLoader.Load<ItemData>(path)?.gongfa?.ResourcePath == book.ResourcePath);
        var displayed = all.Where(book => _manualFilter == "全部" || (_manualFilter == "已学" ? human.has_learned_gongfa(book) : Held(book)))
            .Where(book => _manualKeyword.Length == 0 || character_manual_tags(book).Contains(_manualKeyword))
            .Where(book => CharacterMatches(character_manual_search(book), _manualQuery)).ToList();
        _manualCounter!.Text = $"{displayed.Count} / {all.Count} 本";
        if (_viewedGongfa is null || !displayed.Any(book => book.ResourcePath == _viewedGongfa.ResourcePath)) _viewedGongfa = displayed.FirstOrDefault();
        foreach (var book in displayed)
        {
            var state = book == human.main_gongfa ? "主修" : human.sub_gongfa_list.Contains(book) ? "辅修" : "未装配";
            var button = CharacterButton("", selected: book == _viewedGongfa); button.CustomMinimumSize = new Vector2(0, 76); _manualList.AddChild(button);
            var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; button.AddChild(row); row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); row.OffsetLeft = 12; row.OffsetRight = -10; row.OffsetTop = 7; row.OffsetBottom = -7;
            row.AddThemeConstantOverride("separation", 12); row.AddChild(CharacterIcon(character_manual_icon(human, book), 56));
            var box = CharacterBox(row); box.MouseFilter = MouseFilterEnum.Ignore; box.AddThemeConstantOverride("separation", 3);
            box.AddChild(CharacterClippedText(book.gongfa_name, 22)); box.AddChild(CharacterClippedText($"{(human.has_learned_gongfa(book) ? "已学" : "未学")}{(Held(book) ? " · 持有" : "")} · {state}", 17, true));
            button.Pressed += () => { _viewedGongfa = book; refresh_character_gongfa(human); };
            CharacterHover(button, book.gongfa_name, book.description, character_manual_icon(human, book));
        }
        if (displayed.Count == 0) _manualList.AddChild(CharacterParagraph("没有符合条件的秘籍\n可调整检索条件或重置筛选。", 21, true));
        render_character_manual_workspace(human, _viewedGongfa);
    }

    private void render_character_manual_workspace(Character human, GongfaResource? book)
    {
        CharacterClear(_manualWorkspace!);
        if (book is null) { CharacterEmpty(_manualWorkspace!, "暂无可查看的秘籍"); return; }
        var panel = CharacterPanel(_manualWorkspace!, 0, 0, 954, 641); var box = CharacterBox(panel);
        var top = new HBoxContainer(); top.AddThemeConstantOverride("separation", 16); box.AddChild(top);
        top.AddChild(CharacterIcon(character_manual_icon(human, book), 72)); var info = CharacterBox(top); info.AddThemeConstantOverride("separation", 6);
        info.AddChild(CharacterClippedText(book.gongfa_name, 28));
        var isMain = book == human.main_gongfa; var isSub = human.sub_gongfa_list.Contains(book); var learned = human.get_learned_gongfa_chapter_ids(book);
        var chapters = book.get_sorted_chapters(); info.AddChild(CharacterText($"已学 {learned.Count}/{chapters.Count} 篇 · {(isMain ? "主修" : isSub ? "辅修" : "未装配")}", 20, true));
        var equip = new VBoxContainer(); top.AddChild(equip);
        var main = CharacterButton("设为主修", 19, padding: 6); main.CustomMinimumSize = new Vector2(112, 38); main.Disabled = isMain || !human.has_learned_gongfa(book) || !book.can_be_main || !book.is_wuxing_compatible(human.spirit_root_type); equip.AddChild(main);
        main.Pressed += () => { if (human.switch_main_gongfa(book)) refresh_character_gongfa(human); };
        var sub = CharacterButton(isSub ? "卸下辅修" : "设为辅修", 19, padding: 6); sub.CustomMinimumSize = new Vector2(112, 38); sub.Disabled = !isSub && (!human.has_learned_gongfa(book) || !book.can_be_sub || isMain || human.max_sub_slots == 0); equip.AddChild(sub);
        sub.Pressed += () => { if (isSub) { human.remove_sub_gongfa(book); refresh_character_gongfa(human); } else show_character_sub_target(human, book); };
        var order = chapters.Where(chapter => learned.Contains(chapter.get_id())).Select(chapter => chapter.order).DefaultIfEmpty(0).Max();
        var heart = book.get_heart_method_skill(isMain);
        var heartButton = CharacterButton("心法 · " + (heart?.skill_name ?? book.gongfa_name), 19, padding: 5); heartButton.CustomMinimumSize = new Vector2(0, 36); heartButton.SizeFlagsHorizontal = SizeFlags.ShrinkBegin; box.AddChild(heartButton);
        var state = !human.has_learned_gongfa(book) ? "未学 · 首卷预览" : isMain || isSub ? "已装配 · 当前版本" : "未装配 · 预览效果";
        CharacterHover(heartButton, heart?.skill_name ?? "心法", $"{state}\n版本：{(order > 0 ? "第" + chinese_volume_number(order) + "卷" : "首卷")}\n\n{book.get_heart_method_description(Math.Max(1, order), true)}\n\n主修额外效果仅作为主修时生效。");
        box.AddChild(new HSeparator()); var scroll = CharacterScroll(box); var rows = CharacterBox(scroll); rows.AddThemeConstantOverride("separation", 8);
        Control? matching = null;
        foreach (var chapter in chapters)
        {
            var known = human.is_gongfa_chapter_learned(book, chapter.get_id()); var route = human.get_sub_gongfa_chapter_route(book, chapter.get_id());
            var line = new PanelContainer { CustomMinimumSize = new Vector2(0, 66) }; line.AddThemeStyleboxOverride("panel", CharacterStyle(known, 8)); rows.AddChild(line);
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 12); line.AddChild(row);
            var name = CharacterButton($"第{chinese_volume_number(chapter.order)}卷 · {chapter.chapter_name}", 22); name.ClipText = true; name.CustomMinimumSize = new Vector2(240, 42); name.Alignment = HorizontalAlignment.Left; name.SizeFlagsHorizontal = SizeFlags.ExpandFill; row.AddChild(name);
            CharacterHover(name, chapter.chapter_name, character_chapter_detail(human, book, chapter));
            var status = CharacterText(known ? "已掌握" : "未掌握", 18, true); status.CustomMinimumSize = new Vector2(68, 0); status.HorizontalAlignment = HorizontalAlignment.Center; row.AddChild(status);
            foreach (var branch in new[] { (name: "修持", id: "left"), (name: "化法", id: "right"), (name: "习得", id: "learn") })
            {
                var entry = CharacterButton(branch.name, 20, known && (isMain && branch.id != "learn" || isSub && route == branch.id), 6); entry.CustomMinimumSize = new Vector2(88, 40); row.AddChild(entry);
                var detail = branch.id == "learn" ? character_chapter_learning(human, book, chapter) : gongfa_branch_tooltip(chapter, branch.id == "left", isMain, human);
                CharacterHover(entry, $"{chapter.chapter_name} · {branch.name}", $"{(known ? "已掌握" : "未掌握 · 效果预览")}\n{(isMain ? "主修两条分支均生效" : isSub ? route == branch.id ? "此分支已激活" : "此分支未激活" : "秘籍未装配")}\n\n{detail}");
                if (branch.id != "learn")
                {
                    entry.Pressed += () => { if (!known || !isSub) return; human.set_sub_gongfa_chapter_route(book, chapter.get_id(), branch.id); refresh_character_gongfa(human); };
                }
            }
            var chapterSkills = chapter.get_learned_skills(true).OfType<ActiveSkillData>()
                .Concat(chapter.get_deck_rewrites(true).Where(rewrite => rewrite.enabled).Select(rewrite => rewrite.get_result_skill(true)).OfType<ActiveSkillData>()).ToList();
            var chapterSearch = chapter.chapter_name + " " + character_chapter_learning(human, book, chapter) + " " + string.Join(" ", chapterSkills.SelectMany(skill => skill.tags));
            if ((_manualQuery.Trim().Length > 0 || _manualKeyword.Length > 0)
                && CharacterMatches(chapterSearch, _manualQuery)
                && (_manualKeyword.Length == 0 || chapterSkills.Any(skill => skill.tags.Contains(_manualKeyword)))) matching ??= line;
        }
        if (matching is not null) { var target = matching; scroll.CallDeferred(ScrollContainer.MethodName.EnsureControlVisible, target); }
        box.AddChild(CharacterText("悬停查看完整效果；辅修已学卷章可点击修持／化法切换分支。", 17, true));
    }

    private static string character_chapter_learning(Character human, GongfaResource book, GongfaChapterConfig chapter)
    {
        var known = human.is_gongfa_chapter_learned(book, chapter.get_id());
        var skills = chapter.learned_active_skills.OfType<ActiveSkillData>().Select(skill => $"[b]{skill.skill_name}[/b]\n{CardPresentation.Face(skill, human)}")
            .Concat(chapter.main_learned_active_skills.OfType<ActiveSkillData>().Select(skill => $"[b]{skill.skill_name} · 主修限定[/b]\n{CardPresentation.Face(skill, human)}"));
        var permanent = chapter.learn_permanent_bonuses.Keys.Select(key => $"{battle_attribute_name(key.AsString())} +{chapter.learn_permanent_bonuses[key]}")
            .Concat(chapter.learn_permanent_multipliers.Keys.Select(key => $"{battle_attribute_name(key.AsString())} +{chapter.learn_permanent_multipliers[key].AsSingle() * 100:0.#}%"));
        return $"{(known ? "已掌握" : "学习后可掌握")}\n\n[b]永久所得[/b]\n{(permanent.Any() ? string.Join("\n", permanent) : "无永久属性奖励")}\n\n[b]战斗技能[/b]\n{(skills.Any() ? string.Join("\n\n", skills) : "无新增主动技能")}\n\n{gongfa_chapter_world_spell_details(chapter, known)}\n\n[b]本卷心法版本[/b]\n{book.get_heart_method_description(chapter.order, true)}";
    }

    private static string character_chapter_detail(Character human, GongfaResource book, GongfaChapterConfig chapter)
    {
        var requirements = gongfa_requirement_lines(human, book, chapter).Select(label => { var text = label.Text; label.Free(); return text; });
        var roots = book.wuxing == GongfaResource.Wuxing.NONE ? "无五行要求" : "灵根适配：" + (book.is_wuxing_compatible(human.spirit_root_type) ? "符合" : "不符合");
        return $"[b]学习条件[/b]\n{roots}\n{string.Join("\n", requirements)}\n{(chapter.requires_main_slot ? "须作为主修研读\n" : "")}研读需求：{chapter.required_study_progress:0.#}\n\n{character_chapter_learning(human, book, chapter)}\n\n{chapter.description}";
    }

    private void CharacterChoose(string title, IEnumerable<string> options, Action<string> select)
    {
        hide_tooltip();
        build_gongfa_modal(); CharacterClear(_gongfaModalChoices!); _gongfaModalTitle!.Text = title; _gongfaModalBody!.Text = "";
        var search = CharacterSearch("输入名称检索"); _gongfaModalChoices!.AddChild(search); var list = CharacterBox(_gongfaModalChoices);
        var values = options.Distinct().ToList();
        void Refresh(string query)
        {
            CharacterClear(list);
            foreach (var value in values.Where(value => CharacterMatches(value, query)))
            { var button = CharacterButton(value); list.AddChild(button); button.Pressed += () => { _gongfaModal!.Hide(); select(value); }; }
            if (list.GetChildCount() == 0) list.AddChild(CharacterText("没有匹配项", 20, true));
        }
        search.TextChanged += Refresh; Refresh(""); _gongfaModal!.Show(); _gongfaModal.MoveToFront(); search.GrabFocus();
    }

    private void show_character_manual_picker(Character human, int index)
    {
        var current = index < 0 ? human.main_gongfa : index < human.sub_gongfa_list.Count ? human.sub_gongfa_list[index] : null;
        var books = character_manuals(human).Where(human.has_learned_gongfa)
            .Where(book => index < 0 ? book.can_be_main && book.is_wuxing_compatible(human.spirit_root_type) : book.can_be_sub && book != human.main_gongfa && (!human.sub_gongfa_list.Contains(book) || book == current)).ToList();
        CharacterChoose(index < 0 ? "更换主修秘籍" : "更换辅修秘籍", books.Select(book => book.gongfa_name).Prepend("卸下当前秘籍"), value =>
        {
            if (value == "卸下当前秘籍") { if (index < 0) human.unequip_main_gongfa(); else if (current is not null) human.remove_sub_gongfa(current); }
            else if (books.FirstOrDefault(book => book.gongfa_name == value) is { } book)
            { if (index < 0) human.switch_main_gongfa(book); else replace_character_sub(human, book, current); }
            refresh_character_gongfa(human);
        });
    }

    private static void replace_character_sub(Character human, GongfaResource book, GongfaResource? current)
    {
        if (book == current) return;
        var index = current is not null ? human.sub_gongfa_list.IndexOf(current) : -1;
        if (current is not null) human.remove_sub_gongfa(current);
        if (!human.add_sub_gongfa(book) && current is not null) human.add_sub_gongfa(current);
        if (index >= 0 && index < human.sub_gongfa_list.Count - 1 && human.entity_data is HumanData data)
        {
            var moved = human.sub_gongfa_list[^1]; human.sub_gongfa_list.RemoveAt(human.sub_gongfa_list.Count - 1); human.sub_gongfa_list.Insert(index, moved);
            data.sub_gongfa_paths.Remove(moved.ResourcePath); data.sub_gongfa_paths.Insert(index, moved.ResourcePath);
            human.refresh_gongfa_effects();
        }
    }

    private void show_character_sub_target(Character human, GongfaResource book)
    {
        var slots = Enumerable.Range(0, human.max_sub_slots).Select(index => $"辅修{chinese_volume_number(index + 1)} · {(index < human.sub_gongfa_list.Count ? human.sub_gongfa_list[index].gongfa_name : "空闲")}").ToList();
        CharacterChoose("选择辅修位置", slots, value => { var index = slots.IndexOf(value); if (index < 0) return; replace_character_sub(human, book, index < human.sub_gongfa_list.Count ? human.sub_gongfa_list[index] : null); refresh_character_gongfa(human); });
    }
}
