using Godot;
using GDict = Godot.Collections.Dictionary;

/// <summary>已确认的静室三页材质与排版，行动仍交给CultivationUI原接口。</summary>
public partial class CultivationUI
{
    private static readonly Color RoomSpiritInk = new("5d9da8");
    private Control _paperRoomCanvas = null!, _paperLegacy = null!;
    private Label _paperRoomTitle = null!, _paperPlanNote = null!;
    private VBoxContainer _paperPlanFacts = null!;
    private GridContainer _paperStudyChapters = null!;
    private TextureRect _paperStudyIcon = null!;
    private Label _paperBreakRealm = null!, _paperBreakPower = null!, _paperBreakNeed = null!, _paperBreakCondition = null!;
    private ProgressBar _paperBreakProgress = null!;
    private Control? _paperDetail;
    private bool _paperReducedMotion;
    private readonly List<QuietRoomNumber> _paperNumbers = [];

    private static void RoomPlace(Control parent, Control child, float x, float y, float width, float height)
    { parent.AddChild(child); child.Position = new Vector2(x, y); child.Size = new Vector2(width, height); }
    private static Label RoomText(string text, int size = 22, bool muted = false) => CraftingPaperStyle.Text(text, size, muted);
    private static PanelContainer RoomPaper(int padding = 24, bool title = false)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", title ? CraftingPaperStyle.Title() : CraftingPaperStyle.Paper(padding, brightness: .96f));
        return panel;
    }
    private static VBoxContainer RoomStack(PanelContainer panel, int separation = 16)
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", separation); panel.AddChild(box); return box;
    }
    private static Button RoomButton(string caption, Action action, bool primary = false)
    {
        var button = new Button { Text = caption, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        PaperButtonFeedback.Apply(button, primary ? CraftingPaperStyle.ButtonKind.Primary : CraftingPaperStyle.ButtonKind.Regular, 23);
        button.Pressed += action; return button;
    }
    private static ProgressBar RoomProgress(bool spirit = false)
    {
        var progress = new ProgressBar { ShowPercentage = false, CustomMinimumSize = new Vector2(0, 20), MouseFilter = MouseFilterEnum.Ignore };
        if (spirit)
        {
            var fill = (StyleBoxTexture)CraftingPaperStyle.MakeTheme().GetStylebox("fill", "ProgressBar").Duplicate();
            fill.ModulateColor = new Color(.85f, .9f, 1.5f); progress.AddThemeStyleboxOverride("fill", fill);
        }
        return progress;
    }
    private static void RoomDivider(VBoxContainer parent, string caption)
    {
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 16); parent.AddChild(row);
        row.AddChild(RoomText(caption, 20, true));
        var divider = CraftingPaperStyle.Divider();
        divider.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        divider.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.AddChild(divider);
    }
    private static Label RoomFact(VBoxContainer parent, string caption, string value, bool spirit = false)
    {
        var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 34) }; parent.AddChild(row);
        var label = RoomText(caption, 21, true); label.SizeFlagsHorizontal = SizeFlags.ExpandFill; row.AddChild(label);
        var result = RoomText(value, 22); result.HorizontalAlignment = HorizontalAlignment.Right;
        if (spirit) result.AddThemeColorOverride("font_color", RoomSpiritInk);
        row.AddChild(result); return result;
    }
    private QuietRoomNumber RoomNumber(SpinBox spin, string name, int width = 124, bool spirit = false)
    {
        var result = new QuietRoomNumber(spin, spirit) { Name = name, CustomMinimumSize = new Vector2(width, 46) };
        _paperNumbers.Add(result); return result;
    }
    private T RoomLegacy<T>(T control) where T : Control { _paperLegacy.AddChild(control); return control; }

    private void build_paper_room_shell()
    {
        _room = new Control { Name = "QuietRoom", Theme = CraftingPaperStyle.MakeTheme(), MouseFilter = MouseFilterEnum.Stop };
        AddChild(_room); _room.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        QuietRoomStyle.Background(_room, "quiet_room.png");
        _paperRoomCanvas = QuietRoomStyle.Canvas(_room);
        var outline = new InventoryPaperFrame { FillPaper = false, MouseFilter = MouseFilterEnum.Ignore };
        RoomPlace(_paperRoomCanvas, outline, 9, 8, 1654, 925);
        var headerPaper = RoomPaper(12, true); headerPaper.Name = "QuietRoomTitlePaper";
        RoomPlace(_paperRoomCanvas, headerPaper, 24, 18, 410, 74);
        _paperRoomTitle = RoomText("静室 · 吐纳修行", 30); _paperRoomTitle.HorizontalAlignment = HorizontalAlignment.Center; headerPaper.AddChild(_paperRoomTitle);
        var header = new HBoxContainer(); header.AddThemeConstantOverride("separation", 22);
        RoomPlace(_paperRoomCanvas, header, 464, 18, 1048, 46);
        _name = RoomText("", 26); header.AddChild(_name);
        _realm = RoomText("", 21, true); header.AddChild(_realm);
        _roomTime = RoomText("", 22, true);
        _roomTime.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _roomTime.HorizontalAlignment = HorizontalAlignment.Right;
        foreach (var label in new[] { _name, _realm, _roomTime })
        { label.VerticalAlignment = VerticalAlignment.Center; label.SizeFlagsVertical = SizeFlags.Fill; }
        header.AddChild(_roomTime);
        var back = RoomButton("返回", close_panel); back.Name = "QuietRoomReturnButton";
        back.AddThemeFontSizeOverride("font_size", 22);
        RoomPlace(_paperRoomCanvas, back, 1534, 18, 114, 46);
        var arrow = new QuietRoomTriangle { Name = "ReturnTriangle", Right = true, MouseFilter = MouseFilterEnum.Ignore };
        back.AddChild(arrow); arrow.AnchorLeft = arrow.AnchorRight = 1; arrow.AnchorTop = arrow.AnchorBottom = .5f;
        arrow.OffsetLeft = -25; arrow.OffsetRight = -19; arrow.OffsetTop = -4; arrow.OffsetBottom = 4;
        var rail = RoomPaper(15); rail.Name = "QuietRoomNavigation";
        RoomPlace(_paperRoomCanvas, rail, 24, 116, 214, 777);
        var nav = RoomStack(rail, 17);
        var iconSheet = GD.Load<Texture2D>("res://textures/ui/infopanel_nav_icons_ink_v1.png");
        var titles = new[] { "吐纳修行", "功法研读", "闭关突破" }; int[] iconIndices = [0, 2, 4];
        for (int index = 0; index < titles.Length; index++)
        {
            int page = index, iconIndex = iconIndices[index];
            var icon = new AtlasTexture { Atlas = iconSheet, Region = new Rect2(iconIndex % 3 * 512, iconIndex / 3 * 512, 512, 512), FilterClip = true };
            var tab = new QuietRoomPaperTab(titles[index], icon) { Name = "RoomTab" + index, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            tab.Pressed += () => select_room_page(page); nav.AddChild(tab); _roomTabs.Add(tab);
        }
        _roomContent = new Control { Name = "RoomPages", MouseFilter = MouseFilterEnum.Pass };
        RoomPlace(_paperRoomCanvas, _roomContent, 256, 116, 1392, 777);
        _paperLegacy = new Control { Name = "QuietRoomCompatibility", Visible = false, MouseFilter = MouseFilterEnum.Ignore };
        _paperRoomCanvas.AddChild(_paperLegacy);
        _roomMain = RoomLegacy(new Label()); _roomBottleneckStatus = RoomLegacy(new Label());
        _roomInspirationSummary = RoomLegacy(new Label()); _roomInspirationButton = RoomLegacy(new Button());
        _autoRefinementFeedback = RoomLegacy(new Label());
        _roomClock = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        if (_roomClock is not null) _roomClock.time_changed += room_time_changed;
        _room.Hide();
    }

    private void build_paper_practice()
    {
        _panel = room_page("PracticePage");
        var layout = new Control { Name = "PracticeLayout", MouseFilter = MouseFilterEnum.Pass }; _panel.AddChild(layout);
        var scenery = new Control { MouseFilter = MouseFilterEnum.Ignore }; RoomPlace(layout, scenery, 0, 0, 760, 777);
        var heading = RoomPaper(22); RoomPlace(scenery, heading, 0, 0, 760, 92); heading.AddChild(RoomText("吐纳修行", 29));
        var summary = RoomPaper(25); summary.Name = "QuietRoomPowerSummary";
        RoomPlace(scenery, summary, 0, 580, 760, 197);
        var stats = RoomStack(summary, 15);
        var powerRow = new HBoxContainer(); stats.AddChild(powerRow);
        var powerTitle = RoomText("当前灵力", 27); powerTitle.SizeFlagsHorizontal = SizeFlags.ExpandFill; powerRow.AddChild(powerTitle);
        _power = RoomText("", 25); _power.AddThemeColorOverride("font_color", RoomSpiritInk); powerRow.AddChild(_power);
        _roomPowerBar = RoomProgress(true); stats.AddChild(_roomPowerBar);
        _rate = RoomText("", 21); _rate.AddThemeColorOverride("font_color", RoomSpiritInk); stats.AddChild(_rate);
        var actions = RoomPaper(26); actions.Name = "QuietRoomPracticePaper";
        RoomPlace(layout, actions, 782, 0, 610, 777);
        var outer = RoomStack(actions, 16); outer.AddChild(RoomText("本次修炼", 29));
        _mode = RoomLegacy(new OptionButton { Name = "PracticeMode" });
        var modes = new GridContainer { Columns = 2 }; modes.AddThemeConstantOverride("h_separation", 12); modes.AddThemeConstantOverride("v_separation", 12); outer.AddChild(modes);
        foreach (var (caption, index) in new[] { ("指定灵力", 0), ("指定天数", 1), ("指定日期", 2), ("下层所需", 3) })
        {
            int selected = index; _mode.AddItem(caption);
            var button = RoomButton(caption, () => { _mode.Select(selected); update_mode_visibility(); refresh_plan(); sync_mode_tabs(); });
            button.Name = "PracticeMode" + index; button.ToggleMode = true; button.CustomMinimumSize = new Vector2(0, 48);
            PaperButtonFeedback.Apply(button, CraftingPaperStyle.ButtonKind.Regular, 23); modes.AddChild(button); _modeTabs.Add(button);
        }
        _mode.ItemSelected += _ => { update_mode_visibility(); refresh_plan(); sync_mode_tabs(); };
        _strandRow = new VBoxContainer(); outer.AddChild(_strandRow);
        var strands = new HBoxContainer(); strands.AddThemeConstantOverride("separation", 12); _strandRow.AddChild(strands);
        var strandCaption = RoomText("目标灵力", 21, true); strandCaption.SizeFlagsHorizontal = SizeFlags.ExpandFill; strands.AddChild(strandCaption);
        _strands = spin(1, 999, 1); strands.AddChild(RoomNumber(_strands, "PracticeStrands", spirit: true));
        var unit = RoomText("缕", 22); unit.AddThemeColorOverride("font_color", RoomSpiritInk); strands.AddChild(unit); _strands.ValueChanged += _ => refresh_plan();
        _dayRow = new VBoxContainer(); outer.AddChild(_dayRow); var days = new HBoxContainer(); days.AddThemeConstantOverride("separation", 12); _dayRow.AddChild(days);
        var dayCaption = RoomText("修炼时长", 21, true); dayCaption.SizeFlagsHorizontal = SizeFlags.ExpandFill; days.AddChild(dayCaption);
        _days = spin(1, 999, 1); days.AddChild(RoomNumber(_days, "PracticeDays")); days.AddChild(RoomText("日", 22)); _days.ValueChanged += _ => refresh_plan();
        _dateRow = new VBoxContainer(); _dateRow.AddThemeConstantOverride("separation", 10); outer.AddChild(_dateRow); _dateRow.AddChild(RoomText("结束日期", 21, true));
        var date = new HBoxContainer(); date.AddThemeConstantOverride("separation", 8); _dateRow.AddChild(date);
        _year = spin(1, 9999, 1); _month = spin(1, 12, 1); _date = spin(1, 30, 1);
        foreach (var (field, caption, width) in new[] { (_year, "年", 125), (_month, "月", 92), (_date, "日", 92) })
        { date.AddChild(RoomNumber(field, "PracticeDate" + caption, width)); date.AddChild(RoomText(caption, 20)); field.ValueChanged += _ => refresh_plan(); }
        _inviteScheduleButton = RoomButton("按最近应邀日期修炼", apply_next_invitation_schedule); _dateRow.AddChild(_inviteScheduleButton);
        _inviteScheduleHint = RoomText("", 16, true); _inviteScheduleHint.AutowrapMode = TextServer.AutowrapMode.WordSmart; _dateRow.AddChild(_inviteScheduleHint);
        RoomDivider(outer, "修炼安排");
        _roomStamina = RoomFact(outer, "当前精力", "—");
        var scroll = new ScrollContainer { Name = "PracticePlanScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsVertical = SizeFlags.ExpandFill }; outer.AddChild(scroll);
        var plan = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; plan.AddThemeConstantOverride("separation", 12); scroll.AddChild(plan);
        _paperPlanFacts = new VBoxContainer(); _paperPlanFacts.AddThemeConstantOverride("separation", 9); plan.AddChild(_paperPlanFacts);
        _roomWorldStatus = RoomText("", 17, true); _roomWorldStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart; plan.AddChild(_roomWorldStatus);
        _paperPlanNote = RoomText("", 18, true); _paperPlanNote.AutowrapMode = TextServer.AutowrapMode.WordSmart; plan.AddChild(_paperPlanNote);
        _plan = RoomLegacy(new Label());
        _cultivate = RoomButton("开始修炼", cultivate, true); _cultivate.Name = "PracticeStartButton"; outer.AddChild(_cultivate);
        build_room_study(); build_room_breakthrough(); sync_mode_tabs();
    }

    private void build_paper_study()
    {
        _studyPage = room_page("StudyPage");
        var layout = new Control { Name = "StudyLayout", MouseFilter = MouseFilterEnum.Pass }; _studyPage.AddChild(layout);
        var books = RoomPaper(24); books.Name = "QuietRoomStudyBooks"; RoomPlace(layout, books, 0, 0, 400, 777);
        var list = RoomStack(books); list.AddChild(RoomText("可研读功法", 28));
        var scroll = new ScrollContainer { Name = "StudyBookScroll", SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; list.AddChild(scroll);
        _studyList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; _studyList.AddThemeConstantOverride("separation", 12); scroll.AddChild(_studyList);
        var details = RoomPaper(28); details.Name = "QuietRoomStudyDetails"; RoomPlace(layout, details, 422, 0, 970, 777);
        var root = RoomStack(details, 18);
        var heading = new HBoxContainer(); heading.AddThemeConstantOverride("separation", 24); root.AddChild(heading);
        _paperStudyIcon = new TextureRect { CustomMinimumSize = new Vector2(64, 86), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore }; heading.AddChild(_paperStudyIcon);
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; titles.AddThemeConstantOverride("separation", 8); heading.AddChild(titles);
        _studyTitle = RoomText("", 29); _studyTitle.AutowrapMode = TextServer.AutowrapMode.WordSmart; titles.AddChild(_studyTitle); titles.AddChild(RoomText("功法研读", 20, true));
        _studyBook = RoomLegacy(new OptionButton()); _studyBook.ItemSelected += select_study_book;
        var contentScroll = new ScrollContainer { Name = "StudyDetailScroll", SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; root.AddChild(contentScroll);
        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; content.AddThemeConstantOverride("separation", 15); contentScroll.AddChild(content);
        RoomDivider(content, "本卷研读");
        _studyHint = RoomText("", 22); _studyHint.AutowrapMode = TextServer.AutowrapMode.WordSmart; content.AddChild(_studyHint);
        _studyProgress = RoomProgress(); content.AddChild(_studyProgress);
        _studyRequirementTitle = RoomText("本卷研读条件", 20, true); _studyRequirementTitle.Hide(); content.AddChild(_studyRequirementTitle);
        _studyRequirementList = new VBoxContainer { Visible = false }; _studyRequirementList.AddThemeConstantOverride("separation", 7); content.AddChild(_studyRequirementList);
        RoomDivider(content, "卷章目录");
        _paperStudyChapters = new GridContainer { Name = "StudyChapterGrid", Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _paperStudyChapters.AddThemeConstantOverride("h_separation", 14); _paperStudyChapters.AddThemeConstantOverride("v_separation", 12); content.AddChild(_paperStudyChapters);
        _study = RoomButton("开始研读", study, true); _study.Name = "StudyStartButton"; _study.SizeFlagsHorizontal = SizeFlags.ShrinkEnd; _study.CustomMinimumSize = new Vector2(220, 56); root.AddChild(_study);
    }

    private void build_paper_breakthrough()
    {
        _breakPage = room_page("BreakthroughPage");
        var layout = new Control { Name = "BreakthroughLayout", MouseFilter = MouseFilterEnum.Pass }; _breakPage.AddChild(layout);
        var heading = RoomPaper(22); RoomPlace(layout, heading, 0, 0, 760, 92); heading.AddChild(RoomText("闭关突破", 29));
        var summary = RoomPaper(25); RoomPlace(layout, summary, 0, 550, 760, 227); var stats = RoomStack(summary, 14);
        _paperBreakRealm = RoomText("", 29); stats.AddChild(_paperBreakRealm); _paperBreakProgress = RoomProgress(true); stats.AddChild(_paperBreakProgress);
        _paperBreakPower = RoomFact(stats, "当前灵力", "—", true); _paperBreakNeed = RoomFact(stats, "下层所需", "—", true);
        var paper = RoomPaper(27); paper.Name = "QuietRoomBreakthroughPaper"; RoomPlace(layout, paper, 782, 0, 610, 777);
        var box = RoomStack(paper, 18); box.AddChild(RoomText("闭关准备", 29));
        RoomDivider(box, "突破条件"); _paperBreakCondition = RoomText("", 22); _paperBreakCondition.AutowrapMode = TextServer.AutowrapMode.WordSmart; box.AddChild(_paperBreakCondition);
        RoomDivider(box, "当前安排");
        var scroll = new ScrollContainer { Name = "BreakthroughHintScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsVertical = SizeFlags.ExpandFill }; box.AddChild(scroll);
        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; content.AddThemeConstantOverride("separation", 18); scroll.AddChild(content);
        _breakthroughHint = RoomText("", 22); _breakthroughHint.AutowrapMode = TextServer.AutowrapMode.WordSmart; content.AddChild(_breakthroughHint);
        _breakthroughPillPicker = new OptionButton { Name = "BreakthroughPillPicker", Visible = false, CustomMinimumSize = new Vector2(0, 46), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        PaperButtonFeedback.Apply(_breakthroughPillPicker, CraftingPaperStyle.ButtonKind.Input, 21); _breakthroughPillPicker.ItemSelected += on_breakthrough_pill_selected; content.AddChild(_breakthroughPillPicker);
        _breakthroughPillEffect = RoomText("", 19, true); _breakthroughPillEffect.AutowrapMode = TextServer.AutowrapMode.WordSmart; _breakthroughPillEffect.Hide(); content.AddChild(_breakthroughPillEffect);
        _breakthrough = RoomButton("闭关突破", breakthrough, true); _breakthrough.Name = "BreakthroughStartButton"; _breakthrough.AutowrapMode = TextServer.AutowrapMode.WordSmart; _breakthrough.CustomMinimumSize = new Vector2(0, 72); box.AddChild(_breakthrough);
    }

    public void SetRoomReducedMotion(bool reduced)
    {
        _paperReducedMotion = reduced;
        for (int index = 0; index < _roomTabs.Count; index++)
            if (_roomTabs[index] is QuietRoomPaperTab tab) tab.SelectPaper(index == _roomPage, true);
    }
    private void refresh_paper_room_header()
    {
        _paperRoomTitle.Text = "静室 · " + (_roomPage == 1 ? "功法研读" : _roomPage == 2 ? "闭关突破" : "吐纳修行");
        foreach (var number in _paperNumbers) number.RefreshNumber();
        if (_target is null) return;
        var progress = call("get_next_realm_progress").AsGodotDictionary();
        int required = progress.GetValueOrDefault("required", 0).AsInt32(), current = progress.GetValueOrDefault("current", 0).AsInt32();
        _paperBreakRealm.Text = value("cultivation_realm", "凡人").AsString();
        _paperBreakPower.Text = $"{current} 缕"; _paperBreakNeed.Text = required > 0 ? $"{required} 缕" : "—";
        _paperBreakProgress.MaxValue = Math.Max(1, required); _paperBreakProgress.Value = current;
        bool ready = call("is_breakthrough_ready").AsBool();
        _paperBreakCondition.Text = $"{(required > 0 && current >= required ? "✓" : "○")} 灵力达到本层所需\n\n{(ready ? "✓" : "○")} {(ready ? "当前突破条件已满足" : "尚须满足当前突破条件")}";
    }
    private void refresh_paper_plan(GDict? preview)
    {
        foreach (var node in _paperPlanFacts.GetChildren()) { _paperPlanFacts.RemoveChild(node); node.QueueFree(); }
        if (preview is null) { _paperPlanNote.Text = _plan?.Text ?? ""; return; }
        int hours = preview.GetValueOrDefault("hours", 0).AsInt32(), strands = preview.GetValueOrDefault("strands", 0).AsInt32();
        RoomFact(_paperPlanFacts, "预计灵力", $"{strands} 缕", true);
        RoomFact(_paperPlanFacts, "耗时", format_duration_hours(hours));
        RoomFact(_paperPlanFacts, "精力消耗", $"{preview.GetValueOrDefault("stamina_cost", 0f).AsSingle():0.#}");
        RoomFact(_paperPlanFacts, "预期结束", _roomClock is not null ? format_date_after(_roomClock, hours) : "—");
        _paperPlanNote.Text = "未满一缕的灵力在结束时散去。" + ((_mode?.Selected is 1 or 2) && strands == 0 ? "\n本次时长不足以获得一整缕，可增加修炼天数或改用指定灵力。" : "");
    }
    private void refresh_paper_study_chapters(GongfaResource? book, Character? student, GongfaChapterConfig? current)
    {
        foreach (var child in _paperStudyChapters.GetChildren()) { _paperStudyChapters.RemoveChild(child); child.QueueFree(); }
        _paperStudyIcon.Texture = book is null ? null : manual_icon(book);
        if (book is null) return;
        foreach (var chapter in book.get_sorted_chapters())
        {
            var selected = chapter; bool learned = student?.is_gongfa_chapter_learned(book, chapter.get_id()) == true;
            string status = learned ? "已研读" : chapter == current ? "当前研读" : "尚未研读";
            var button = RoomButton(chapter.chapter_name + "\n" + status, () => show_paper_chapter(book, selected, learned));
            button.Name = "StudyChapter_" + chapter.get_id(); button.AutowrapMode = TextServer.AutowrapMode.WordSmart; button.CustomMinimumSize = new Vector2(0, 76);
            PaperButtonFeedback.Apply(button, CraftingPaperStyle.ButtonKind.Regular, 21, chapter == current); _paperStudyChapters.AddChild(button);
        }
    }
    private void show_paper_chapter(GongfaResource book, GongfaChapterConfig chapter, bool learned)
    {
        _paperDetail?.QueueFree();
        _paperDetail = new Control { Name = "QuietRoomChapterOverlay", MouseFilter = MouseFilterEnum.Stop, ZIndex = 100 };
        _paperRoomCanvas.AddChild(_paperDetail); _paperDetail.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var shade = new ColorRect { Color = new Color(.10f, .16f, .13f, .65f), MouseFilter = MouseFilterEnum.Stop }; _paperDetail.AddChild(shade); shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var panel = RoomPaper(28); RoomPlace(_paperDetail, panel, 430, 150, 812, 630); var box = RoomStack(panel, 18);
        box.AddChild(RoomText(book.gongfa_name + " · " + chapter.chapter_name, 28));
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; box.AddChild(scroll);
        var text = RoomText((learned ? "已研读\n\n" : "尚未研读\n\n") + chapter.description, 22); text.AutowrapMode = TextServer.AutowrapMode.WordSmart; text.SizeFlagsHorizontal = SizeFlags.ExpandFill; scroll.AddChild(text);
        box.AddChild(RoomButton("返回", () => _paperDetail?.Hide()));
    }
    private void style_paper_yuanxi()
    {
        if (_yuanxiPanel is null || _yuanxiPanel.HasMeta("quiet_paper_style")) return;
        _yuanxiPanel.SetMeta("quiet_paper_style", true); _yuanxiPanel.Theme = CraftingPaperStyle.MakeTheme();
        foreach (var panel in _yuanxiPanel.FindChildren("*", "PanelContainer", true, false).OfType<PanelContainer>())
            panel.AddThemeStyleboxOverride("panel", CraftingPaperStyle.Paper(24, brightness: .96f));
        foreach (var button in _yuanxiPanel.FindChildren("*", "Button", true, false).OfType<Button>())
            PaperButtonFeedback.Apply(button, button.Text is "养成一息" or "连续养息" or "继续闭关"
                ? CraftingPaperStyle.ButtonKind.Primary : CraftingPaperStyle.ButtonKind.Regular, 21);
        foreach (var spin in _yuanxiPanel.FindChildren("*", "SpinBox", true, false).OfType<SpinBox>().ToArray())
        {
            var parent = spin.GetParent(); int order = spin.GetIndex(); parent.RemoveChild(spin);
            var number = RoomNumber(spin, "YuanxiTarget", 100); parent.AddChild(number); parent.MoveChild(number, order);
        }
    }
    public override void _Input(InputEvent input)
    {
        if (IsVisibleInTree() && _paperDetail?.Visible == true && input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        { _paperDetail.Hide(); GetViewport().SetInputAsHandled(); }
    }
}
