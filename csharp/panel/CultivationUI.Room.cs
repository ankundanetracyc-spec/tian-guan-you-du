using Godot;

public partial class CultivationUI
{
    private Control _room = null!, _roomContent = null!;
    private PanelContainer _studyPage = null!, _breakPage = null!;
    private ScrollContainer _roomBodyScroll = null!, _roomNatureScroll = null!;
    private Label _roomTime = null!, _roomStamina = null!, _roomMain = null!, _roomBottleneckStatus = null!, _roomWorldStatus = null!, _roomInspirationSummary = null!, _studyTitle = null!;
    private Button _roomInspirationButton = null!;
    private VBoxContainer _studyList = null!;
    private ProgressBar _roomPowerBar = null!, _studyProgress = null!;
    private readonly List<Button> _roomTabs = [], _modeTabs = [];
    private readonly Dictionary<string, Texture2D> _manualIcons = [];
    private GameTimeMgr? _roomClock;
    private bool _roomActions;
    private int _roomPage;

    private void build_room_shell()
    {
        _room = new Control { Name = "QuietRoom", Theme = QuietRoomStyle.MakeTheme(), MouseFilter = MouseFilterEnum.Stop };
        AddChild(_room); _room.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        QuietRoomStyle.Background(_room, "quiet_room.png");
        var canvas = QuietRoomStyle.Canvas(_room);
        var header = new HBoxContainer { AnchorLeft = .035f, AnchorRight = .965f, AnchorTop = .025f, AnchorBottom = .10f };
        header.AddThemeConstantOverride("separation", 22); canvas.AddChild(header);
        header.AddChild(QuietRoomStyle.Text("静室", 32));
        _name = QuietRoomStyle.Text("", 22); _name.SizeFlagsHorizontal = SizeFlags.ExpandFill; header.AddChild(_name);
        _roomTime = QuietRoomStyle.Text("", 23); header.AddChild(_roomTime);
        _roomInspirationButton = new Button { Name = "OpenInspirationLedger", Text = "灵感清册", CustomMinimumSize = new Vector2(144, 48) };
        QuietRoomStyle.Primary(_roomInspirationButton);
        _roomInspirationButton.TooltipText = "查看可用灵感；可炼化当前瓶颈，或预留给用途相符的行动。";
        _roomInspirationButton.Pressed += () => select_room_page(5);
        header.AddChild(_roomInspirationButton);
        var close = new Button { Text = "离开静室", CustomMinimumSize = new Vector2(150, 48) }; close.Pressed += close_panel; header.AddChild(close);
        var status = new HBoxContainer { AnchorLeft = .035f, AnchorRight = .965f, AnchorTop = .115f, AnchorBottom = .165f };
        status.AddThemeConstantOverride("separation", 32); canvas.AddChild(status);
        _realm = QuietRoomStyle.Text("", 21); status.AddChild(_realm);
        _roomStamina = QuietRoomStyle.Text("", 21); status.AddChild(_roomStamina);
        _roomMain = QuietRoomStyle.Text("", 21); status.AddChild(_roomMain);
        var rail = new InventoryPaperFrame { Dark = true, AnchorLeft = .02f, AnchorRight = .17f, AnchorTop = .195f, AnchorBottom = .955f };
        canvas.AddChild(rail);
        var nav = new VBoxContainer { AnchorLeft = .03f, AnchorRight = .16f, AnchorTop = .22f, AnchorBottom = .925f };
        nav.AddThemeConstantOverride("separation", 14); canvas.AddChild(nav);
        var titles = new[] { "吐纳修行", "功法研读", "闭关突破", "修命·淬体", "修性·观法", "灵感·瓶颈" };
        for (var i = 0; i < titles.Length; i++)
        {
            var page = i; var tab = new Button { Name = "RoomTab" + i, Text = titles[i], ToggleMode = true, CustomMinimumSize = new Vector2(0, 66) };
            style_room_navigation(tab);
            tab.Pressed += () => select_room_page(page); nav.AddChild(tab); _roomTabs.Add(tab);
        }
        nav.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        var overview = new Button { Text = "修行总览", CustomMinimumSize = new Vector2(0, 46) }; style_room_navigation(overview); overview.Pressed += () => { if (_target is not null) show_cultivation_overview(_target); }; nav.AddChild(overview);
        _roomContent = new Control { Name = "RoomPages", AnchorLeft = .185f, AnchorRight = .965f, AnchorTop = .195f, AnchorBottom = .955f };
        canvas.AddChild(_roomContent);
        _roomClock = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        if (_roomClock is not null) _roomClock.time_changed += room_time_changed;
        _room.Hide();
    }
    private static void style_room_navigation(Button tab)
    {
        tab.AddThemeFontSizeOverride("font_size", 24);
        tab.AddThemeColorOverride("font_color", new Color("eee6d3"));
        tab.AddThemeColorOverride("font_hover_color", new Color("fff4d4"));
        tab.AddThemeColorOverride("font_pressed_color", QuietRoomStyle.Ink);
        tab.AddThemeColorOverride("font_hover_pressed_color", QuietRoomStyle.Ink);
        tab.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        tab.AddThemeStyleboxOverride("hover", QuietRoomStyle.Edge(true));
        var plaque = new StyleBoxTexture { Texture = GD.Load<Texture2D>("res://textures/ui/infopanel_nav_plaque_clean_v2.png"), TextureMarginLeft = 24, TextureMarginRight = 24, TextureMarginTop = 12, TextureMarginBottom = 12, ContentMarginTop = 8, ContentMarginBottom = 8 };
        tab.AddThemeStyleboxOverride("pressed", plaque); tab.AddThemeStyleboxOverride("hover_pressed", plaque);
        tab.MouseDefaultCursorShape = CursorShape.PointingHand;
    }
    public override void _ExitTree() { if (GodotObject.IsInstanceValid(_roomClock)) _roomClock!.time_changed -= room_time_changed; }
    private void room_time_changed() { if (Visible && _room.Visible) { refresh_room_header(); if (_panel?.Visible == true) refresh_plan(); if (_inspirationPage?.Visible == true) refresh_room_inspiration(); } }
    private PanelContainer room_page(string name)
    {
        var page = new PanelContainer { Name = name, MouseFilter = MouseFilterEnum.Pass };
        page.AddThemeStyleboxOverride("panel", new StyleBoxEmpty()); _roomContent.AddChild(page); page.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); page.Hide(); return page;
    }
    private static VBoxContainer room_box(PanelContainer parent)
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill }; box.AddThemeConstantOverride("separation", 14); parent.AddChild(box); return box;
    }
    private void build_room_ui()
    {
        _panel = room_page("PracticePage");
        var columns = new HBoxContainer(); columns.AddThemeConstantOverride("separation", 30); _panel.AddChild(columns);
        var scenery = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 1.15f }; columns.AddChild(scenery);
        var heading = new PanelContainer(); heading.AddThemeStyleboxOverride("panel", QuietRoomStyle.Paper(.94f, 14)); scenery.AddChild(heading);
        var headingBox = room_box(heading); headingBox.AddThemeConstantOverride("separation", 6); headingBox.AddChild(QuietRoomStyle.Text("吐纳修行", 28)); headingBox.AddChild(QuietRoomStyle.Text("一呼一吸，积养灵力。", 20));
        scenery.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        var summary = new PanelContainer(); summary.AddThemeStyleboxOverride("panel", QuietRoomStyle.Paper(.93f, 18)); scenery.AddChild(summary);
        var stats = room_box(summary); stats.AddChild(QuietRoomStyle.Text("当前灵力", 21)); _power = QuietRoomStyle.Text("", 26); stats.AddChild(_power);
        _roomPowerBar = new ProgressBar { ShowPercentage = false, CustomMinimumSize = new Vector2(0, 14) }; stats.AddChild(_roomPowerBar);
        _rate = QuietRoomStyle.Text("", 18); stats.AddChild(_rate);
        _roomBottleneckStatus = QuietRoomStyle.Text("", 17);
        _roomBottleneckStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        stats.AddChild(_roomBottleneckStatus);
        _roomWorldStatus = QuietRoomStyle.Text("", 16);
        _roomWorldStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        stats.AddChild(_roomWorldStatus);
        _roomInspirationSummary = QuietRoomStyle.Text("", 16);
        _roomInspirationSummary.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        stats.AddChild(_roomInspirationSummary);
        var actions = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; actions.AddThemeStyleboxOverride("panel", QuietRoomStyle.Paper(.95f, 24)); columns.AddChild(actions);
        var actionScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; actions.AddChild(actionScroll);
        var practice = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill }; practice.AddThemeConstantOverride("separation", 14); actionScroll.AddChild(practice); practice.AddChild(QuietRoomStyle.Text("本次修炼", 28));
        _mode = new OptionButton { Name = "PracticeMode", Visible = false }; practice.AddChild(_mode);
        var grid = new GridContainer { Columns = 2 }; grid.AddThemeConstantOverride("h_separation", 12); grid.AddThemeConstantOverride("v_separation", 12); practice.AddChild(grid);
        var modes = new[] { "指定灵力", "指定天数", "指定日期", "下层所需" };
        for (var i = 0; i < modes.Length; i++)
        {
            var mode = i; _mode.AddItem(modes[i]); var tab = new Button { Text = modes[i], ToggleMode = true, SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 48) };
            tab.Pressed += () => { _mode.Select(mode); update_mode_visibility(); refresh_plan(); sync_mode_tabs(); }; grid.AddChild(tab); _modeTabs.Add(tab);
        }
        _mode.ItemSelected += _ => { update_mode_visibility(); refresh_plan(); sync_mode_tabs(); };
        _strandRow = new VBoxContainer(); practice.AddChild(_strandRow); var strands = new HBoxContainer(); _strandRow.AddChild(strands); strands.AddChild(QuietRoomStyle.Text("目标灵力")); _strands = spin(1, 999, 1); strands.AddChild(_strands); strands.AddChild(QuietRoomStyle.Text("缕")); _strands.ValueChanged += _ => refresh_plan();
        _dayRow = new VBoxContainer(); practice.AddChild(_dayRow); var days = new HBoxContainer(); _dayRow.AddChild(days); days.AddChild(QuietRoomStyle.Text("修炼时长")); _days = spin(1, 999, 1); days.AddChild(_days); days.AddChild(QuietRoomStyle.Text("日")); _days.ValueChanged += _ => refresh_plan();
        _dateRow = new VBoxContainer(); practice.AddChild(_dateRow); _dateRow.AddChild(QuietRoomStyle.Text("结束日期")); var date = new HBoxContainer(); _dateRow.AddChild(date);
        _year = spin(1, 9999, 1); _month = spin(1, 12, 1); _date = spin(1, 30, 1);
        foreach (var (field, text) in new[] { (_year, "年"), (_month, "月"), (_date, "日") }) { field.CustomMinimumSize = new Vector2(74, 44); date.AddChild(field); date.AddChild(QuietRoomStyle.Text(text, 18)); field.ValueChanged += _ => refresh_plan(); }
        _plan = QuietRoomStyle.Text("", 20); _plan.AutowrapMode = TextServer.AutowrapMode.WordSmart; practice.AddChild(_plan);
        practice.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        _cultivate = new Button { Text = "开始修炼" }; QuietRoomStyle.Primary(_cultivate); _cultivate.Pressed += cultivate; practice.AddChild(_cultivate);
        var note = QuietRoomStyle.Text("开始后才推进游戏时间", 16); note.HorizontalAlignment = HorizontalAlignment.Center; practice.AddChild(note);
        build_room_study(); build_room_breakthrough(); build_room_inspiration(); sync_mode_tabs();
    }
    private void build_room_study()
    {
        _studyPage = room_page("StudyPage"); var columns = new HBoxContainer(); columns.AddThemeConstantOverride("separation", 24); _studyPage.AddChild(columns);
        var books = new PanelContainer { CustomMinimumSize = new Vector2(290, 0) }; books.AddThemeStyleboxOverride("panel", QuietRoomStyle.Paper(.94f, 18)); columns.AddChild(books);
        var list = room_box(books); list.AddChild(QuietRoomStyle.Text("可研读功法", 27));
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; list.AddChild(scroll);
        _studyList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; _studyList.AddThemeConstantOverride("separation", 16); scroll.AddChild(_studyList);
        var details = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; details.AddThemeStyleboxOverride("panel", QuietRoomStyle.Paper(.94f, 30)); columns.AddChild(details);
        var box = room_box(details); box.AddChild(QuietRoomStyle.Text("功法研读", 32)); _studyTitle = QuietRoomStyle.Text("", 28); box.AddChild(_studyTitle);
        _studyBook = new OptionButton { Visible = false }; _studyBook.ItemSelected += select_study_book; box.AddChild(_studyBook);
        _studyHint = QuietRoomStyle.Text("", 22); _studyHint.AutowrapMode = TextServer.AutowrapMode.WordSmart; box.AddChild(_studyHint);
        _studyProgress = new ProgressBar { ShowPercentage = false, CustomMinimumSize = new Vector2(0, 16) }; box.AddChild(_studyProgress);
        box.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        _study = new Button { Text = "研读一日" }; QuietRoomStyle.Primary(_study); _study.Pressed += study; box.AddChild(_study);
    }
    private void build_room_breakthrough()
    {
        _breakPage = room_page("BreakthroughPage"); var columns = new HBoxContainer(); columns.AddThemeConstantOverride("separation", 32); _breakPage.AddChild(columns);
        var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 1.4f }; columns.AddChild(left);
        var heading = new PanelContainer(); heading.AddThemeStyleboxOverride("panel", QuietRoomStyle.Paper(1, 14)); left.AddChild(heading);
        var headingBox = room_box(heading); headingBox.AddThemeConstantOverride("separation", 6);
        headingBox.AddChild(QuietRoomStyle.Text("闭关突破", 28)); headingBox.AddChild(QuietRoomStyle.Text("心神归一，静候功成。", 20));
        var paper = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; paper.AddThemeStyleboxOverride("panel", QuietRoomStyle.Paper(.95f, 28)); columns.AddChild(paper);
        var box = room_box(paper); box.AddChild(QuietRoomStyle.Text("突破条件与安排", 28));
        _breakthroughHint = QuietRoomStyle.Text("", 23); _breakthroughHint.AutowrapMode = TextServer.AutowrapMode.WordSmart; box.AddChild(_breakthroughHint);
        box.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill }); _breakthrough = new Button { Text = "闭关突破" }; QuietRoomStyle.Primary(_breakthrough); _breakthrough.Pressed += breakthrough; box.AddChild(_breakthrough);
    }
    private ScrollContainer mount_room_panel(PanelContainer panel)
    {
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; _roomContent.AddChild(scroll); scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        panel.Reparent(scroll); panel.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft); panel.SizeFlagsHorizontal = SizeFlags.ExpandFill; panel.SizeFlagsVertical = SizeFlags.ExpandFill; panel.ZIndex = 0;
        panel.AddThemeStyleboxOverride("panel", QuietRoomStyle.Paper(1, 16));
        foreach (var text in panel.FindChildren("*", "Label", true, false).OfType<Label>())
            if (text.Text.Length > 24) text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        foreach (var column in panel.FindChildren("*", "PanelContainer", true, false).OfType<PanelContainer>())
            if (column.CustomMinimumSize.X >= 520) column.CustomMinimumSize = new Vector2(380, column.CustomMinimumSize.Y);
        if (panel.GetChildOrNull<VBoxContainer>(0) is { } root)
        {
            // 公共顶栏负责标题、日期和退出，避免每页重复占用空间。
            if (root.GetChildOrNull<Control>(0) is { } head) head.Hide();
            if (root.GetChildOrNull<Control>(1) is { } name) name.Hide();
            if (root.GetChildOrNull<Control>(2) is { } realm) realm.Hide();
        }
        scroll.Hide(); return scroll;
    }
    private void finish_room_shell()
    {
        _roomBodyScroll = mount_room_panel(_bodyPanel!); _roomNatureScroll = mount_room_panel(_visualPanel!);
        foreach (var button in new[] { _bodyStart, _visualPractice }) if (button is not null) QuietRoomStyle.Primary(button);
        foreach (var paper in _room.FindChildren("*", "PanelContainer", true, false).OfType<PanelContainer>())
            if (paper.GetThemeStylebox("panel") is StyleBoxTexture) QuietRoomStyle.Frame(paper);
    }
    private Texture2D? manual_icon(GongfaResource book)
    {
        if (_manualIcons.TryGetValue(book.ResourcePath, out var cached)) return cached;
        // 已有秘籍按此约定命名；通过 Godot 加载，导出包内也可读取。
        var path = "res://resources/entities/items/" + book.gongfa_name + "秘籍.tres";
        if (ResourceLoader.Exists(path) && GD.Load<Resource>(path) is ItemData item && !string.IsNullOrEmpty(item.icon_path) && ResourceLoader.Exists(item.icon_path))
        {
            var texture = GD.Load<Texture2D>(item.icon_path); _manualIcons[book.ResourcePath] = texture; return texture;
        }
        return null;
    }
    private void select_room_page(int page)
    {
        if (!_roomActions && page != _roomPage) return;
        _roomPage = page; _room.Show();
        _overviewPanel?.Hide();
        _panel!.Visible = page == 0; _studyPage.Visible = page == 1; _breakPage.Visible = page == 2; if (_inspirationPage is not null) _inspirationPage.Visible = page == 5;
        _roomBodyScroll.Visible = page == 3; _bodyPanel!.Visible = page == 3;
        _roomNatureScroll.Visible = page == 4; _visualPanel!.Visible = page == 4;
        _yuanxiPanel?.Hide();
        if (page == 0 && _target is Character yuanxi && yuanxi.is_yuanxi_realm())
        {
            _panel.Hide();
            if (_yuanxiPanel is null)
            {
                _yuanxiPanel = new YuanxiPanel { EmbeddedInQuietRoom = true };
                _roomContent.AddChild(_yuanxiPanel); _yuanxiPanel.Closed += close_panel;
            }
            _yuanxiPanel.ShowFor(yuanxi);
        }
        for (var i = 0; i < _roomTabs.Count; i++) { _roomTabs[i].ButtonPressed = i == page; _roomTabs[i].Disabled = !_roomActions && i != page; }
        refresh_room_header();
        if (page == 3) refresh_body_ui(); else if (page == 4) refresh_visualization_ui(); else if (page == 5) refresh_room_inspiration(); else refresh();
    }
    private void refresh_room_header()
    {
        if (_roomClock is not null) _roomTime.Text = QuietRoomStyle.Date(_roomClock);
        _name!.Text = value("entity_name", "").AsString(); _realm!.Text = value("cultivation_realm", "凡人").AsString();
        _roomStamina.Text = $"精力  {value("current_world_stamina", 0f).AsSingle():0.#} / {value("max_world_stamina", 100f).AsSingle():0.#}";
        _roomMain.Text = value("main_gongfa").AsGodotObject() is GongfaResource book ? $"主修  {book.gongfa_name}" : "主修  尚未装配";
        refresh_bottleneck_status();
        refresh_world_status();
        refresh_room_inspiration_access();
    }
    private void refresh_room_inspiration_access()
    {
        if (_roomInspirationButton is null || _roomInspirationSummary is null) return;
        if (_target is not Character character)
        {
            _roomInspirationButton.Text = "灵感清册";
            _roomInspirationSummary.Text = "灵感需由经历获得。";
            return;
        }

        var available = character.get_cultivation_inspiration_records();
        var counts = new int[4];
        foreach (var record in available)
        {
            if (record.quality_rank >= 1 && record.quality_rank <= 3)
                counts[record.quality_rank]++;
        }
        _roomInspirationButton.Text = available.Count > 0 ? $"灵感清册　{available.Count}" : "灵感清册";
        if (_roomTabs.Count > 5)
            _roomTabs[5].Text = "灵感·瓶颈";
        _roomInspirationSummary.Text = available.Count > 0
            ? $"可用灵感 {available.Count} 份：见几 {counts[1]} · 明辨 {counts[2]} · 通变 {counts[3]}\n可炼化瓶颈，或预留给用途相符的行动。"
            : "暂无可用灵感。战斗、采集、旅途、研修与宗门活动的实际结果都可能再生灵感。点击上方“灵感清册”查看详情。";
    }
    private void refresh_bottleneck_status()
    {
        if (_roomBottleneckStatus is null) return;
        if (_target is not Character character)
        {
            _roomBottleneckStatus.Text = "闭关瓶颈：当前境界不受此规则影响。";
            return;
        }

        var summary = character.get_cultivation_bottleneck_summary();
        if (!summary.GetValueOrDefault("applicable", false).AsBool())
        {
            _roomBottleneckStatus.Text = "闭关瓶颈：当前境界不受此规则影响；归元九息不受瓶颈减速。";
            return;
        }

        var level = summary.GetValueOrDefault("level", 0).AsInt32();
        var stages = summary.GetValueOrDefault("stages", CultivationBottleneckRules.Stages).AsInt32();
        var penalty = summary.GetValueOrDefault("penalty", 0d).AsDouble();
        var accumulated = summary.GetValueOrDefault("accumulated_cultivation_hours", 0d).AsDouble();
        var grace = summary.GetValueOrDefault("grace_hours", 0d).AsDouble();
        var untilNext = summary.GetValueOrDefault("hours_until_next_level", 0d).AsDouble();
        var trainingRate = character.get_cultivation_rate_after_bottleneck();

        if (level <= 0)
        {
            var untilFirstStage = grace + CultivationBottleneckRules.get_stage_interval_hours() - accumulated;
            _roomBottleneckStatus.Text =
                $"闭关瓶颈：尚未形成　·　首次减速还需 {format_training_days(untilFirstStage)} 修炼日\n" +
                $"当前修炼速率 {trainingRate:0.####} 缕／时";
            return;
        }

        var next = level >= stages ? "已达最高阶" : $"距下一阶 {format_training_days(untilNext)} 修炼日";
        _roomBottleneckStatus.Text =
            $"闭关瓶颈：{level}／{stages} 阶　·　减速 {penalty:P1}　·　效率 {1d - penalty:P1}\n" +
            $"{next}　·　当前修炼速率 {trainingRate:0.####} 缕／时";
    }
    private static string format_training_days(double hours)
        => $"{Math.Max(0d, hours) / CultivationBottleneckRules.HoursPerGameDay:0.#}";
    private void refresh_world_status()
    {
        if (_roomWorldStatus is null) return;
        if (_target is not Character character || character.entity_data is not HumanData human)
        {
            _roomWorldStatus.Hide();
            return;
        }

        var now = _roomClock?.get_total_game_hours() ?? 0;
        var summary = WorldStatusRules.format_summary(human.world_status_records, now);
        _roomWorldStatus.Text = summary == "暂无特殊状态" ? "身体状态：暂无特殊状态" : $"身体状态：{summary}";
        _roomWorldStatus.Visible = summary != "暂无特殊状态";
    }
    private void sync_mode_tabs() { for (var i = 0; i < _modeTabs.Count; i++) _modeTabs[i].ButtonPressed = i == _mode!.Selected; }
    private void refresh_room_study()
    {
        foreach (var node in _studyList.GetChildren()) { _studyList.RemoveChild(node); node.QueueFree(); }
        if (_studyCandidates.Count == 0) { var empty = QuietRoomStyle.Text("暂无可研读功法", 19); _studyList.AddChild(empty); }
        for (var i = 0; i < _studyCandidates.Count; i++)
        {
            var index = i; var book = _studyCandidates[i];
            var button = new Button { Text = book.gongfa_name, ToggleMode = true, ButtonPressed = i == _studyBook!.Selected, CustomMinimumSize = new Vector2(250, 88), ExpandIcon = true, IconAlignment = HorizontalAlignment.Left, Alignment = HorizontalAlignment.Left, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            button.Icon = manual_icon(book);
            button.AddThemeConstantOverride("icon_max_width", 58); button.AddThemeFontSizeOverride("font_size", 19); button.Pressed += () => select_study_book(index); _studyList.AddChild(button);
        }
        _studyTitle.Text = _studyCandidates.Count > 0 ? _studyCandidates[Math.Clamp(_studyBook!.Selected, 0, _studyCandidates.Count - 1)].gongfa_name : "尚无读本";
        var selected = selected_study_book();
        var chapter = _target is Character reader && selected is not null ? selected.get_sorted_chapters().FirstOrDefault(c => !reader.is_gongfa_chapter_learned(selected,c.get_id())) : null;
        _studyProgress.MaxValue = Math.Max(1f, chapter?.required_study_progress ?? selected?.required_study_progress ?? 1f);
        _studyProgress.Value = chapter is not null && _target is Character student ? student.get_gongfa_chapter_study_progress(selected!,chapter.get_id()) : 0;
    }
}
