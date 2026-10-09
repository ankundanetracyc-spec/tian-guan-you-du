using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>修行百科：展示 Buff／状态与物品资料，可在未载入存档时查阅。</summary>
public partial class EncyclopediaPanel : Control
{
    private enum Section { Buffs, Items }
    private sealed record Entry(string Title, string Meta, string Body, string SearchText, Texture2D? Icon = null, bool IsNegative = false);

    private readonly Dictionary<Section, List<Entry>> _entries = [];
    private readonly Dictionary<Section, Button> _tabs = [];
    private VBoxContainer? _entryList;
    private Label? _detailTitle;
    private Label? _detailMeta;
    private Label? _detailBody;
    private TextureRect? _detailIcon;
    private PanelContainer? _detailIconPlate;
    private Label? _sectionTitle;
    private Label? _entryCount;
    private LineEdit? _search;
    private TextureRect? _scroll;
    private Control? _pageContent;
    private Control? _sideTabs;
    private Control? _closeControl;
    private Section _current = Section.Buffs;
    private bool _closing;

    private static readonly Color Ink = new("2a251f");
    private static readonly Color MutedInk = new("665d4c");

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        ZIndex = 250;
        Build();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible || @event is not InputEventKey { Pressed: true, Echo: false } key || key.Keycode != Key.Escape) return;
        hide_panel();
        GetViewport().SetInputAsHandled();
    }

    public void show_panel()
    {
        RefreshCatalog();
        if (_search is not null) _search.Text = "";
        _current = Section.Buffs;
        _closing = false;
        Visible = true;
        MoveToFront();
        Populate();
        if (_scroll is null || _pageContent is null || _sideTabs is null || _closeControl is null) return;
        _scroll.Scale = new Vector2(.025f, 1f);
        _scroll.PivotOffset = _scroll.Size * .5f;
        _pageContent.Modulate = new Color(1, 1, 1, 0);
        _sideTabs.Modulate = new Color(1, 1, 1, 0);
        _closeControl.Modulate = new Color(1, 1, 1, 0);
        CallDeferred(nameof(play_open_animation));
    }

    public void hide_panel()
    {
        if (!Visible || _closing || _scroll is null || _pageContent is null || _sideTabs is null || _closeControl is null) return;
        _closing = true;
        _scroll.PivotOffset = _scroll.Size * .5f;
        var tween = CreateTween().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        tween.SetParallel();
        tween.TweenProperty(_scroll, "scale:x", .025f, .28f);
        tween.TweenProperty(_pageContent, "modulate:a", 0f, .12f);
        tween.TweenProperty(_sideTabs, "modulate:a", 0f, .12f);
        tween.TweenProperty(_closeControl, "modulate:a", 0f, .12f);
        tween.Chain().TweenCallback(Callable.From(finish_close));
    }

    private void Build()
    {
        var shade = new ColorRect { Color = new Color(.02f, .025f, .02f, .62f), MouseFilter = MouseFilterEnum.Stop };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);

        _scroll = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://textures/ui/qingxi/journal_scroll_slim_v2.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _scroll.SetAnchorsPreset(LayoutPreset.Center);
        _scroll.AnchorLeft = .075f; _scroll.AnchorTop = -.16f; _scroll.AnchorRight = .97f; _scroll.AnchorBottom = 1.16f;
        AddChild(_scroll);

        var root = new MarginContainer();
        _pageContent = root;
        root.SetAnchorsPreset(LayoutPreset.Center);
        root.AnchorLeft = .13f; root.AnchorTop = .12f; root.AnchorRight = .91f; root.AnchorBottom = .88f;
        root.AddThemeConstantOverride("margin_left", 22); root.AddThemeConstantOverride("margin_right", 22);
        root.AddThemeConstantOverride("margin_top", 16); root.AddThemeConstantOverride("margin_bottom", 16);
        AddChild(root);

        var vertical = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        vertical.AddThemeConstantOverride("separation", 12);
        root.AddChild(vertical);

        var heading = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        vertical.AddChild(heading);
        var title = MakeLabel("修行百科", 30, Ink);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        heading.AddChild(title);
        _entryCount = MakeLabel("收录 0 条", 16, MutedInk);
        _entryCount.HorizontalAlignment = HorizontalAlignment.Right;
        heading.AddChild(_entryCount);

        var content = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 18);
        vertical.AddChild(content);

        var listPanel = new PanelContainer { CustomMinimumSize = new Vector2(438, 0) };
        StylePanel(listPanel, new Color(.97f, .94f, .86f, .78f));
        content.AddChild(listPanel);
        var listMargin = new MarginContainer();
        listMargin.AddThemeConstantOverride("margin_left", 14); listMargin.AddThemeConstantOverride("margin_right", 14);
        listMargin.AddThemeConstantOverride("margin_top", 12); listMargin.AddThemeConstantOverride("margin_bottom", 12);
        listPanel.AddChild(listMargin);
        var listBox = new VBoxContainer();
        listBox.AddThemeConstantOverride("separation", 9);
        listMargin.AddChild(listBox);
        _sectionTitle = MakeLabel("功法", 24, Ink);
        listBox.AddChild(_sectionTitle);

        var categoryRow = new HBoxContainer();
        categoryRow.AddThemeConstantOverride("separation", 4);
        listBox.AddChild(categoryRow);
        AddTab(categoryRow, "Buff", Section.Buffs, "res://textures/ui/encyclopedia/buff_category_v1.svg");
        AddTab(categoryRow, "物品", Section.Items, "res://textures/ui/encyclopedia/item_category_v1.svg");

        _search = new LineEdit { PlaceholderText = "搜索名称、来源或说明…", ClearButtonEnabled = true };
        _search.AddThemeFontSizeOverride("font_size", 17);
        _search.TextChanged += _ => Populate();
        listBox.AddChild(_search);

        var scrollList = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        listBox.AddChild(scrollList);
        _entryList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _entryList.AddThemeConstantOverride("separation", 8);
        scrollList.AddChild(_entryList);

        var detailPanel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StylePanel(detailPanel, new Color(.98f, .95f, .88f, .86f));
        content.AddChild(detailPanel);
        var detailMargin = new MarginContainer();
        detailMargin.AddThemeConstantOverride("margin_left", 26); detailMargin.AddThemeConstantOverride("margin_right", 26);
        detailMargin.AddThemeConstantOverride("margin_top", 22); detailMargin.AddThemeConstantOverride("margin_bottom", 22);
        detailPanel.AddChild(detailMargin);
        var detailBox = new VBoxContainer();
        detailBox.AddThemeConstantOverride("separation", 12);
        detailMargin.AddChild(detailBox);
        var detailHeader = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        detailHeader.AddThemeConstantOverride("separation", 18);
        detailBox.AddChild(detailHeader);
        _detailIconPlate = new PanelContainer { CustomMinimumSize = new Vector2(104, 104), SizeFlagsVertical = SizeFlags.ShrinkCenter };
        StyleIconPlate(_detailIconPlate, false);
        detailHeader.AddChild(_detailIconPlate);
        var iconMargin = new MarginContainer();
        iconMargin.AddThemeConstantOverride("margin_left", 9); iconMargin.AddThemeConstantOverride("margin_right", 9);
        iconMargin.AddThemeConstantOverride("margin_top", 9); iconMargin.AddThemeConstantOverride("margin_bottom", 9);
        _detailIconPlate.AddChild(iconMargin);
        _detailIcon = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore };
        iconMargin.AddChild(_detailIcon);
        var detailHeading = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        detailHeading.AddThemeConstantOverride("separation", 7);
        detailHeader.AddChild(detailHeading);
        _detailTitle = MakeLabel("", 30, Ink);
        detailHeading.AddChild(_detailTitle);
        _detailMeta = MakeLabel("", 17, MutedInk);
        detailHeading.AddChild(_detailMeta);
        detailBox.AddChild(new HSeparator());
        var detailScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        detailBox.AddChild(detailScroll);
        _detailBody = MakeLabel("", 20, Ink);
        _detailBody.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        detailScroll.AddChild(_detailBody);

        var navMargin = new MarginContainer();
        _sideTabs = navMargin;
        navMargin.SetAnchorsPreset(LayoutPreset.Center);
        navMargin.AnchorLeft = .006f; navMargin.AnchorTop = .27f; navMargin.AnchorRight = .103f; navMargin.AnchorBottom = .73f;
        AddChild(navMargin);
        var navLabel = MakeLabel("图鉴", 21, Ink);
        navLabel.HorizontalAlignment = HorizontalAlignment.Center;
        navMargin.AddChild(navLabel);

        var close = new TextureButton
        {
            TextureNormal = GD.Load<Texture2D>("res://textures/ui/qingxi/journal_close_ribbon_v1.png"),
            IgnoreTextureSize = true,
            StretchMode = TextureButton.StretchModeEnum.Scale,
            TooltipText = "收卷"
        };
        _closeControl = close;
        close.SetAnchorsPreset(LayoutPreset.Center);
        close.AnchorLeft = .887f; close.AnchorTop = .105f; close.AnchorRight = .94f; close.AnchorBottom = .31f;
        close.Pressed += hide_panel;
        AddChild(close);
        var closeLabel = MakeLabel("收卷", 17, Ink);
        closeLabel.HorizontalAlignment = HorizontalAlignment.Center;
        closeLabel.VerticalAlignment = VerticalAlignment.Center;
        closeLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        closeLabel.MouseFilter = MouseFilterEnum.Ignore;
        close.AddChild(closeLabel);
    }

    private void AddTab(Container parent, string text, Section section, string iconPath)
    {
        var button = new Button { Text = text, SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 46) };
        button.Icon = LoadTexture(iconPath);
        button.ExpandIcon = true;
        button.IconAlignment = HorizontalAlignment.Left;
        StyleTab(button, false);
        button.Pressed += () => { _current = section; Populate(); };
        _tabs[section] = button;
        parent.AddChild(button);
    }

    private void Populate()
    {
        if (_entryList is null || _detailTitle is null || _detailMeta is null || _detailBody is null || _sectionTitle is null) return;
        foreach (var child in _entryList.GetChildren()) child.QueueFree();
        foreach (var pair in _tabs) StyleTab(pair.Value, pair.Key == _current);
        var title = SectionTitle(_current);
        _sectionTitle.Text = title;
        var allEntries = _entries.GetValueOrDefault(_current, []);
        var query = _search?.Text.Trim() ?? "";
        var visibleEntries = string.IsNullOrEmpty(query)
            ? allEntries
            : allEntries.Where(entry => entry.SearchText.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        if (_entryCount is not null) _entryCount.Text = $"收录 {visibleEntries.Count} 条";
        if (visibleEntries.Count == 0)
        {
            _detailTitle.Text = $"暂无{title}";
            _detailMeta.Text = string.IsNullOrEmpty(query) ? "此类内容尚未收录。" : $"没有找到与“{query}”相符的词条。";
            _detailBody.Text = "可切换分类，或尝试缩短搜索词。";
            if (_detailIcon is not null) _detailIcon.Texture = LoadTexture("res://textures/ui/encyclopedia/item_placeholder_v1.svg");
            if (_detailIconPlate is not null) StyleIconPlate(_detailIconPlate, false);
            return;
        }
        foreach (var entry in visibleEntries)
        {
            var button = new Button
            {
                Text = "",
                CustomMinimumSize = new Vector2(0, 76),
                TooltipText = entry.Body
            };
            StyleEntry(button);
            var padding = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
            padding.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            padding.AddThemeConstantOverride("margin_left", 10); padding.AddThemeConstantOverride("margin_right", 8);
            padding.AddThemeConstantOverride("margin_top", 7); padding.AddThemeConstantOverride("margin_bottom", 7);
            button.AddChild(padding);
            var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            row.AddThemeConstantOverride("separation", 12);
            padding.AddChild(row);
            var icon = new TextureRect
            {
                Texture = entry.Icon ?? LoadTexture("res://textures/ui/encyclopedia/item_placeholder_v1.svg"),
                CustomMinimumSize = new Vector2(48, 48),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore
            };
            row.AddChild(icon);
            var labels = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
            labels.AddThemeConstantOverride("separation", 3);
            row.AddChild(labels);
            var titleLabel = MakeLabel(entry.Title, 17, Ink);
            titleLabel.MouseFilter = MouseFilterEnum.Ignore;
            labels.AddChild(titleLabel);
            var metaLabel = MakeLabel(entry.Meta, 14, MutedInk);
            metaLabel.MouseFilter = MouseFilterEnum.Ignore;
            labels.AddChild(metaLabel);
            button.Pressed += () => ShowEntry(entry);
            _entryList.AddChild(button);
        }
        ShowEntry(visibleEntries[0]);
    }

    private void ShowEntry(Entry entry)
    {
        if (_detailTitle is null || _detailMeta is null || _detailBody is null || _detailIcon is null || _detailIconPlate is null) return;
        _detailTitle.Text = entry.Title;
        _detailMeta.Text = entry.Meta;
        _detailBody.Text = entry.Body;
        _detailIcon.Texture = entry.Icon ?? LoadTexture("res://textures/ui/encyclopedia/item_placeholder_v1.svg");
        StyleIconPlate(_detailIconPlate, entry.IsNegative);
    }

    private void RefreshCatalog()
    {
        _entries[Section.Buffs] = LoadBuffEntries();
        _entries[Section.Items] = LoadItemEntries();
    }

    private static List<Entry> LoadBuffEntries()
    {
        var result = new List<Entry>();
        foreach (var path in FindResources("res://resources/battle/statuses"))
        {
            var status = ResourceLoader.Load<StatusEffect>(path);
            if (status is null || string.IsNullOrWhiteSpace(status.name)) continue;
            var kind = status.is_debuff ? "负面状态" : "增益状态";
            var body = new StringBuilder();
            AppendSection(body, "类别", kind);
            AppendSection(body, "叠加上限", status.max_stacks.ToString());
            AppendSection(body, "效果", string.IsNullOrWhiteSpace(status.description) ? "暂无详细说明。" : status.description);
            var icon = BattleUnitOverlay.GetStatusIcon(status.effect_id, null) ?? LoadTexture("res://textures/ui/encyclopedia/buff_category_v1.svg");
            result.Add(new Entry(status.name, kind, EmptyBody(body), $"{status.name} {kind} {body}", icon, status.is_debuff));
        }

        foreach (var path in FindResources("res://resources/activeskills"))
        {
            var skill = ResourceLoader.Load<ActiveSkillData>(path);
            if (skill is null || string.IsNullOrWhiteSpace(skill.skill_name)) continue;
            var isPassive = skill.skill_type == ActiveSkillData.SkillType.POWER || skill.slot_category == "passive" || skill.keywords.Contains("固有");
            if (!isPassive) continue;
            var source = string.IsNullOrWhiteSpace(skill.source_gongfa_name) ? "角色固有" : skill.source_gongfa_name;
            var body = new StringBuilder();
            AppendSection(body, "类别", "常驻被动");
            AppendSection(body, "来源", source);
            AppendSection(body, "效果", string.IsNullOrWhiteSpace(skill.description) ? "暂无详细说明。" : skill.description);
            var icon = BattleUnitOverlay.GetStatusIcon($"心法:{skill.skill_name}", new StatusInstance { source_name = source })
                ?? LoadTexture("res://textures/ui/encyclopedia/buff_category_v1.svg");
            result.Add(new Entry(skill.skill_name, $"常驻心法 · {source}", EmptyBody(body), $"{skill.skill_name} {source} {body}", icon));
        }

        AddStatusResourceEntry(result, "气血", "战斗资源", "每当自身主动使气血下降，触发公共气血回落，回复最大生命的3%；气血上限由角色与锻躯养成决定，战斗结束时清空。", "回落 生命 回复");
        AddStatusResourceEntry(result, "血炉", "战斗资源", "每2点提高1%物理伤害；可被相关招式消耗，战斗结束时清空。", "物理伤害 赤阳");
        AddStatusResourceEntry(result, "护盾", "战斗资源", "优先吸收受到的伤害，并随实际吸收量减少。", "防御 吸收");
        return result.OrderBy(entry => entry.Title, StringComparer.Ordinal).ToList();
    }

    private static void AddStatusResourceEntry(List<Entry> result, string title, string kind, string description, string search)
    {
        var body = new StringBuilder();
        AppendSection(body, "类别", kind);
        AppendSection(body, "效果", description);
        var icon = BattleUnitOverlay.GetStatusIcon(title, null) ?? LoadTexture("res://textures/ui/encyclopedia/buff_category_v1.svg");
        result.Add(new Entry(title, kind, EmptyBody(body), $"{title} {kind} {description} {search}", icon));
    }

    private static List<Entry> LoadItemEntries()
    {
        var result = new List<Entry>();
        foreach (var path in FindResources("res://resources/entities/items"))
        {
            var item = ResourceLoader.Load<ItemData>(path);
            if (item is null || string.IsNullOrWhiteSpace(item.entity_name)) continue;
            var category = string.IsNullOrWhiteSpace(item.item_subcategory) ? item.item_category : $"{item.item_category} · {item.item_subcategory}";
            var body = new StringBuilder();
            var isManual = item.item_category == "秘籍" || item.gongfa is GongfaResource;
            AppendSection(body, "类别", category);
            if (isManual)
                AppendSection(body, "修习境界", ManualRealmDescription(item));
            else if (item.craft_data is MaterialCraftData material)
                AppendSection(body, "品阶", $"{material.material_grade_name()}（材评 {material.material_score():0.##}）");
            else
                AppendSection(body, "品阶", item.get_grade_string());
            var tags = StoragePanel.build_tags(item.tags, item.craft_data as MaterialCraftData);
            if (tags != "无词缀") AppendSection(body, "物品词缀", tags);
            AppendSection(body, "堆叠", item.can_stack ? $"可堆叠，最多 {item.max_stack} 件" : "不可堆叠");
            AppendSection(body, "使用范围", item.usage_type switch { 1 => "仅限战斗", 2 => "大世界与战斗均可使用", _ => "大世界物品" });
            if (!string.IsNullOrWhiteSpace(item.battle_skill_path) && ResourceLoader.Load<ActiveSkillData>(item.battle_skill_path) is { } battleSkill)
                AppendSection(body, "战斗效果", battleSkill.description);

            var icon = LoadTexture(item.icon_path) ?? LoadTexture("res://textures/ui/encyclopedia/item_placeholder_v1.svg");
            var searchable = $"{item.entity_name} {category} {(isManual ? ManualRealmDescription(item) : item.get_grade_string())} {tags} {body}";
            result.Add(new Entry(item.entity_name, category, EmptyBody(body), searchable, icon));
        }
        return result.OrderBy(entry => entry.Title, StringComparer.Ordinal).ToList();
    }

    private static string ManualRealmDescription(ItemData item)
    {
        if (item.gongfa is not GongfaResource gongfa) return "境界未标注";
        if (!string.IsNullOrWhiteSpace(gongfa.main_realm_start) && !string.IsNullOrWhiteSpace(gongfa.main_realm_end))
            return $"{gongfa.main_realm_start}—{gongfa.main_realm_end}";
        if (!string.IsNullOrWhiteSpace(gongfa.required_realm)) return gongfa.required_realm;
        var chapterRealms = gongfa.get_sorted_chapters()
            .Select(chapter => chapter.required_realm)
            .Where(realm => !string.IsNullOrWhiteSpace(realm))
            .Distinct()
            .ToList();
        return chapterRealms.Count > 0 ? $"卷章门槛：{string.Join("、", chapterRealms)}" : "境界未标注";
    }

    private static Texture2D? LoadTexture(string path) =>
        string.IsNullOrWhiteSpace(path) || !ResourceLoader.Exists(path) ? null : ResourceLoader.Load<Texture2D>(path);

    private static IEnumerable<string> FindResources(string root)
    {
        var files = new List<string>();
        CollectResources(root, files);
        files.Sort(StringComparer.Ordinal);
        return files;
    }

    private static void CollectResources(string directory, List<string> files)
    {
        var access = DirAccess.Open(directory);
        if (access is null) return;
        foreach (var file in access.GetFiles())
            if (file.EndsWith(".tres", StringComparison.OrdinalIgnoreCase)) files.Add($"{directory}/{file}");
        foreach (var child in access.GetDirectories()) CollectResources($"{directory}/{child}", files);
    }

    private static void AppendSection(StringBuilder builder, string heading, string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (builder.Length > 0) builder.AppendLine().AppendLine();
        builder.Append(heading).AppendLine().Append(text.Trim());
    }

    private static string EmptyBody(StringBuilder builder) => builder.Length == 0 ? "暂无详细说明。" : builder.ToString().Trim();

    private static string SectionTitle(Section section) => section switch
    {
        Section.Buffs => "Buff", _ => "物品"
    };

    private void play_open_animation()
    {
        if (_scroll is null || _pageContent is null || _sideTabs is null || _closeControl is null || !Visible || _closing) return;
        _scroll.PivotOffset = _scroll.Size * .5f;
        var tween = CreateTween().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_scroll, "scale:x", 1f, .32f);
        tween.TweenInterval(.08f);
        tween.SetParallel();
        tween.TweenProperty(_pageContent, "modulate:a", 1f, .16f);
        tween.TweenProperty(_sideTabs, "modulate:a", 1f, .16f);
        tween.TweenProperty(_closeControl, "modulate:a", 1f, .16f);
    }

    private void finish_close()
    {
        Hide();
        _closing = false;
    }

    private static Label MakeLabel(string text, int size, Color color)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static void StylePanel(PanelContainer panel, Color color)
    {
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = color, BorderColor = new Color("82755c"),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12, CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12
        });
    }

    private static void StyleIconPlate(PanelContainer panel, bool negative)
    {
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = negative ? new Color(.45f, .20f, .16f, .20f) : new Color(.73f, .62f, .39f, .18f),
            BorderColor = negative ? new Color("a45b4c") : new Color("af965c"),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12, CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12
        });
    }

    private static void StyleTab(Button button, bool selected)
    {
        button.AddThemeFontSizeOverride("font_size", 18);
        button.AddThemeColorOverride("font_color", Ink);
        button.Modulate = selected ? new Color(1.08f, .91f, .62f, 1f) : Colors.White;
        button.AddThemeStyleboxOverride("normal", new StyleBoxFlat
        {
            BgColor = selected ? new Color(.84f, .74f, .52f, .65f) : new Color(1f, .98f, .91f, .42f),
            BorderColor = new Color(.55f, .49f, .36f, .5f), BorderWidthLeft = 1, BorderWidthTop = 1,
            BorderWidthRight = 1, BorderWidthBottom = 1, CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6
        });
        button.AddThemeStyleboxOverride("hover", new StyleBoxFlat { BgColor = new Color(.98f, .92f, .77f, .9f), CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6 });
    }

    private static void StyleEntry(Button button)
    {
        button.AddThemeFontSizeOverride("font_size", 17);
        button.AddThemeColorOverride("font_color", Ink);
        var normal = new StyleBoxFlat
        {
            BgColor = new Color(1f, .98f, .91f, .56f), BorderColor = new Color(.55f, .49f, .36f, .5f),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            ContentMarginLeft = 12, ContentMarginRight = 10, ContentMarginTop = 7, ContentMarginBottom = 7
        };
        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", new StyleBoxFlat
        {
            BgColor = new Color(.98f, .92f, .77f, .9f), BorderColor = new Color(.65f, .49f, .23f, .85f),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            ContentMarginLeft = 12, ContentMarginRight = 10, ContentMarginTop = 7, ContentMarginBottom = 7
        });
    }
}
