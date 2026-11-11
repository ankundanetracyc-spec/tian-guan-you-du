using Godot;

/// <summary>休息行前配置。人物与时间仍只由调用方的统一行动结算修改。</summary>
public partial class RestPanel : Control
{
    private Character _character = null!;
    private GameTimeMgr _clock = null!;
    private Action<int>? _confirm;
    private TextureRect _background = null!;
    private Texture2D _day = null!, _night = null!;
    private Label _place = null!, _time = null!, _stamina = null!, _preview = null!, _note = null!;
    private Label _selectionHeading = null!, _fullDuration = null!, _fixedDuration = null!, _fixedMark = null!;
    private Label _afterStamina = null!, _recovered = null!, _healthResult = null!, _wakeDate = null!, _validation = null!, _unknownSelection = null!;
    private HBoxContainer _modes = null!, _hoursRow = null!, _fullRow = null!, _fixedRow = null!;
    private VBoxContainer _dateRow = null!;
    private Control _body = null!;
    private ScrollContainer _scroll = null!;
    private SpinBox _hours = null!, _year = null!, _month = null!, _date = null!;
    private Button _byHours = null!, _byDate = null!, _byFull = null!, _start = null!;
    private ProgressBar _before = null!, _after = null!;
    private readonly List<RestPaperNumber> _numbers = [];
    private int? _fixedHours;
    private bool _dateMode, _fullStaminaMode;
    private bool? _shownDay;

    public override void _Ready()
    {
        Name = "RestPanel";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop; ZIndex = 600;
        Theme = CraftingPaperStyle.MakeTheme();
        _day = GD.Load<Texture2D>(QuietRoomStyle.Assets + "rest_day.png");
        _night = GD.Load<Texture2D>(QuietRoomStyle.Assets + "rest_night.png");
        _background = QuietRoomStyle.Background(this, "rest_day.png");
        var canvas = QuietRoomStyle.Canvas(this);

        var title = new PanelContainer { Name = "RestTitle", MouseFilter = MouseFilterEnum.Ignore };
        var titleStyle = CraftingPaperStyle.Title();
        titleStyle.TextureMarginLeft = titleStyle.TextureMarginRight = 16;
        titleStyle.TextureMarginTop = titleStyle.TextureMarginBottom = 12;
        titleStyle.ContentMarginLeft = titleStyle.ContentMarginRight = 16;
        titleStyle.ContentMarginTop = titleStyle.ContentMarginBottom = 8;
        title.AddThemeStyleboxOverride("panel", titleStyle);
        Place(canvas, title, 24, 18, 190, 68);
        var caption = CraftingPaperStyle.Text("休息", 39);
        caption.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddChild(caption);
        _place = CraftingPaperStyle.Text("—", 23, true); _place.ClipText = true;
        Place(canvas, _place, 258, 18, 750, 68);
        _time = CraftingPaperStyle.Text("—", 22);
        _time.HorizontalAlignment = HorizontalAlignment.Right; _time.VerticalAlignment = VerticalAlignment.Center;
        Place(canvas, _time, 1080, 18, 414, 46);
        var close = new Button { Name = "RestReturn", Text = "返回" };
        PaperButtonFeedback.Apply(close, fontSize: 22);
        close.Pressed += Close; Place(canvas, close, 1534, 18, 114, 46);
        var returnArrow = new RestPaperTriangle { Name = "ReturnTriangle", Right = true, MouseFilter = MouseFilterEnum.Ignore };
        Place(close, returnArrow, 91, 20, 6, 7);

        var paper = new PanelContainer { Name = "RestNote" };
        var paperStyle = CraftingPaperStyle.Paper(40, brightness: 1);
        paperStyle.TextureMarginLeft = paperStyle.TextureMarginRight = 12;
        paperStyle.TextureMarginTop = paperStyle.TextureMarginBottom = 12;
        paperStyle.ContentMarginTop = 31; paperStyle.ContentMarginBottom = 28;
        paper.AddThemeStyleboxOverride("panel", paperStyle);
        Place(canvas, paper, 1010, 151, 560, 752);
        var layout = new VBoxContainer(); layout.AddThemeConstantOverride("separation", 14); paper.AddChild(layout);
        _scroll = new ScrollContainer { Name = "RestNoteScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical = SizeFlags.ExpandFill };
        layout.AddChild(_scroll);
        _body = new Control { Name = "RestNoteContents", CustomMinimumSize = new Vector2(0, 583), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _scroll.AddChild(_body);
        Place(_body, CraftingPaperStyle.Text("安心歇息", 30), 0, 0, 360, 40);
        _fixedMark = CraftingPaperStyle.Text("剧情限定", 18, true);
        _fixedMark.HorizontalAlignment = HorizontalAlignment.Right; Place(_body, _fixedMark, 360, 0, 120, 40);
        Place(_body, CraftingPaperStyle.Text("当前精力", 22), 0, 60, 220, 36);
        _stamina = CraftingPaperStyle.Text("—", 30); _stamina.HorizontalAlignment = HorizontalAlignment.Right;
        Place(_body, _stamina, 220, 60, 260, 36);
        _before = new ProgressBar { Name = "CurrentStamina", ShowPercentage = false };
        Place(_body, _before, 0, 103, 480, 12);

        _modes = new HBoxContainer { Name = "RestModes" }; _modes.AddThemeConstantOverride("separation", 10);
        Place(_body, _modes, 0, 140, 480, 45);
        _byHours = ModeButton("按时长", () => SelectMode(false, false));
        _byDate = ModeButton("指定日期", () => SelectMode(true, false));
        _byFull = ModeButton("精力回满", () => SelectMode(false, true));
        _selectionHeading = CraftingPaperStyle.Text("休息时长", 20, true);
        Place(_body, _selectionHeading, 0, 207, 480, 28);
        _hoursRow = new HBoxContainer { Name = "DurationFields" }; _hoursRow.AddThemeConstantOverride("separation", 18);
        Place(_body, _hoursRow, 0, 245, 480, 61);
        _hours = Number(1, 720, 8);
        _hoursRow.AddChild(PaperNumber(_hours, 150, 61, 32, "休息时长"));
        _hoursRow.AddChild(CraftingPaperStyle.Text("小时", 23));
        _dateRow = new VBoxContainer { Name = "WakeDateFields" };
        Place(_body, _dateRow, 0, 245, 480, 61);
        var date = new HBoxContainer(); date.AddThemeConstantOverride("separation", 10); _dateRow.AddChild(date);
        _year = Number(1, 9999, 1); _month = Number(1, 12, 1); _date = Number(1, 30, 1);
        foreach (var (number, label, width, description) in new[]
        {
            (_year, "年", 120, "醒来年份"), (_month, "月", 90, "醒来月份"), (_date, "日", 90, "醒来日期")
        })
        {
            date.AddChild(PaperNumber(number, width, 59, 27, description));
            date.AddChild(CraftingPaperStyle.Text(label, 20));
        }
        _fullRow = new HBoxContainer { Name = "FullStaminaDuration" }; _fullRow.AddThemeConstantOverride("separation", 18);
        Place(_body, _fullRow, 16, 245, 464, 61);
        _fullDuration = CraftingPaperStyle.Text("—", 35); _fullRow.AddChild(_fullDuration); _fullRow.AddChild(CraftingPaperStyle.Text("小时", 23));
        _fixedRow = new HBoxContainer { Name = "FixedRestDuration" }; _fixedRow.AddThemeConstantOverride("separation", 18);
        Place(_body, _fixedRow, 16, 245, 464, 61);
        _fixedDuration = CraftingPaperStyle.Text("—", 35); _fixedRow.AddChild(_fixedDuration); _fixedRow.AddChild(CraftingPaperStyle.Text("小时", 23));
        _unknownSelection = CraftingPaperStyle.Text("—", 32); Place(_body, _unknownSelection, 16, 245, 464, 61);
        foreach (var number in new[] { _hours, _year, _month, _date }) number.ValueChanged += _ => Refresh();

        var divider = CraftingPaperStyle.Divider();
        divider.Modulate = new Color(1, 1, 1, .65f);
        Place(_body, divider, 0, 327, 480, 11);
        Place(_body, CraftingPaperStyle.Text("醒来后", 24), 0, 355, 480, 34);
        _afterStamina = ResultRow("精力", 400, 28);
        _recovered = ResultRow("恢复精力", 438, 22);
        _recovered.AddThemeColorOverride("font_color", CraftingPaperStyle.Accent);
        _healthResult = ResultRow("生命", 476, 22);
        _wakeDate = ResultRow("醒来日期", 514, 22);
        _validation = CraftingPaperStyle.Text("", 18); _validation.AddThemeColorOverride("font_color", CraftingPaperStyle.Danger);
        _validation.AutowrapMode = TextServer.AutowrapMode.WordSmart; Place(_body, _validation, 0, 555, 480, 28);
        _note = CraftingPaperStyle.Text("", 18, true); _note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        Place(_body, _note, 0, 600, 480, 90);
        // Preserve the original machine-readable fields without repeating their text in the UI.
        _preview = CraftingPaperStyle.Text(""); _preview.Visible = false; _body.AddChild(_preview);
        _after = new ProgressBar { Name = "ProjectedStamina", ShowPercentage = false, Visible = false }; _body.AddChild(_after);

        var action = new VBoxContainer(); action.AddThemeConstantOverride("separation", 11); layout.AddChild(action);
        _start = new Button { Name = "StartRest", Text = "开始休息" };
        PaperButtonFeedback.Apply(_start, CraftingPaperStyle.ButtonKind.Primary, 25);
        _start.Pressed += Confirm; action.AddChild(_start);
        var rule = CraftingPaperStyle.Text($"每小时恢复 {WorldStaminaRules.RestRecoveryPerGameHour:0.#} 点精力 · 开始后推进时间", 16, true);
        rule.HorizontalAlignment = HorizontalAlignment.Center; action.AddChild(rule);
        _body.Resized += FitBody; FitBody();
        Refresh(); Hide();
    }

    private static void Place(Control parent, Control child, float x, float y, float width, float height)
    {
        parent.AddChild(child); child.Position = new Vector2(x, y); child.Size = new Vector2(width, height);
        if (parent.Name == "RestNoteContents")
        {
            child.SetMeta("rest_design_x", x); child.SetMeta("rest_design_y", y); child.SetMeta("rest_design_width", width);
        }
    }
    private void FitBody()
    {
        float ratio = _body.Size.X > 0 ? _body.Size.X / 480f : 1;
        float omittedModes = _fixedHours.HasValue ? 70 : 0;
        foreach (var child in _body.GetChildren().OfType<Control>())
            if (child.HasMeta("rest_design_width"))
            {
                float designY = child.GetMeta("rest_design_y").AsSingle();
                child.Position = new Vector2(child.GetMeta("rest_design_x").AsSingle() * ratio,
                    designY >= 207 ? designY - omittedModes : designY);
                child.Size = new Vector2(child.GetMeta("rest_design_width").AsSingle() * ratio, child.Size.Y);
            }
        if (_note is null) return;
        // Fixed-story mode omits the choice row. Size notes by actual text instead of
        // reserving a full extra page for even a single sentence.
        float height = 583 - omittedModes;
        if (_note.Visible)
        {
            _note.Size = new Vector2(_note.Size.X, _note.GetMinimumSize().Y);
            height = Math.Max(height, _note.Position.Y + _note.Size.Y + 10);
        }
        _body.CustomMinimumSize = new Vector2(0, height);
    }
    private Button ModeButton(string text, Action choose)
    {
        var button = new Button { Text = text, ToggleMode = true, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        PaperButtonFeedback.Apply(button, CraftingPaperStyle.ButtonKind.Regular, 20);
        button.Pressed += choose; _modes.AddChild(button); return button;
    }
    private RestPaperNumber PaperNumber(SpinBox value, int width, int height, int fontSize, string description)
    {
        var input = new RestPaperNumber(value, fontSize, description) { CustomMinimumSize = new Vector2(width, height) };
        _numbers.Add(input); return input;
    }
    private Label ResultRow(string name, int y, int size)
    {
        Place(_body, CraftingPaperStyle.Text(name, 20, true), 0, y, 160, 32);
        var value = CraftingPaperStyle.Text("—", size); value.HorizontalAlignment = HorizontalAlignment.Right;
        Place(_body, value, 160, y, 320, 32); return value;
    }
    private static SpinBox Number(int min, int max, int value) => new()
    {
        MinValue = min, MaxValue = max, Step = 1, Value = value, Rounded = true, AllowGreater = false, AllowLesser = false
    };
    private void SelectMode(bool date, bool full) { _dateMode = date; _fullStaminaMode = full; Refresh(); }
    private bool HasContext => GodotObject.IsInstanceValid(_clock) && GodotObject.IsInstanceValid(_character)
        && _clock.year >= 1 && _clock.month is >= 1 and <= 12 && _clock.day is >= 1 and <= 30 && _clock.hour is >= 0 and < 24;

    public void Open(Character character, GameTimeMgr clock, string place, Action<int> confirm, int? fixedHours = null, string note = "")
    {
        if (GodotObject.IsInstanceValid(_clock)) _clock.time_changed -= Refresh;
        if (GodotObject.IsInstanceValid(_character)) _character.WorldStaminaChanged -= OnStaminaChanged;
        _character = character; _clock = clock; _confirm = confirm; _fixedHours = fixedHours;
        if (GodotObject.IsInstanceValid(_clock)) _clock.time_changed += Refresh;
        if (GodotObject.IsInstanceValid(_character)) _character.WorldStaminaChanged += OnStaminaChanged;
        _dateMode = _fullStaminaMode = false; _place.Text = string.IsNullOrWhiteSpace(place) ? "—" : place;
        _note.Text = note; _shownDay = null;
        if (HasContext)
        {
            var tomorrow = clock.get_total_game_hours() / 24 + 1;
            // 现行30日/月、12月/年；日期选择沿用当前隐含小时。
            _year.SetValueNoSignal(tomorrow / 360 + 1); _month.SetValueNoSignal(tomorrow % 360 / 30 + 1); _date.SetValueNoSignal(tomorrow % 30 + 1);
        }
        _hours.SetValueNoSignal(8);
        foreach (var number in _numbers) number.SyncValue();
        Refresh(); _scroll.ScrollVertical = 0; Show(); MoveToFront();
    }

    private int Hours()
    {
        if (!HasContext) return 0;
        if (_fixedHours.HasValue) return _fixedHours.Value;
        if (_fullStaminaMode)
        {
            // 回满独立于手动输入，不能用720小时截断后仍显示已满。
            double deficit = Math.Max(0d, (double)_character.max_world_stamina - _character.current_world_stamina);
            double hours = Math.Ceiling(deficit / WorldStaminaRules.RestRecoveryPerGameHour);
            return double.IsFinite(hours) && hours <= int.MaxValue ? (int)hours : 0;
        }
        return _dateMode
            ? (((int)_year.Value - 1) * 360 + ((int)_month.Value - 1) * 30 + (int)_date.Value - 1) * 24 + _clock.hour - _clock.get_total_game_hours()
            : (int)_hours.Value;
    }

    public void Refresh()
    {
        if (_start is null) return;
        bool bound = HasContext, free = !_fixedHours.HasValue;
        if (GodotObject.IsInstanceValid(_clock))
        {
            var isDay = QuietRoomStyle.IsDay(_clock);
            if (_shownDay != isDay) { _background.Texture = isDay ? _day : _night; _shownDay = isDay; }
        }
        _time.Text = bound ? QuietRoomStyle.Date(_clock) : "—";
        _modes.Visible = free; _fixedMark.Visible = !free;
        foreach (var (button, selected) in new[] { (_byHours, !_dateMode && !_fullStaminaMode), (_byDate, _dateMode), (_byFull, _fullStaminaMode) })
        {
            button.Disabled = !bound; button.SetPressedNoSignal(selected);
            PaperButtonFeedback.Apply(button, CraftingPaperStyle.ButtonKind.Regular, 20, selected);
        }
        _hoursRow.Visible = bound && free && !_dateMode && !_fullStaminaMode;
        _dateRow.Visible = bound && free && _dateMode;
        _fullRow.Visible = bound && free && _fullStaminaMode;
        _fixedRow.Visible = bound && !free;
        _unknownSelection.Visible = !bound;
        _selectionHeading.Text = _dateMode && free ? "醒来日期" : _fullStaminaMode && free ? "预计休息时长" : "休息时长";
        _note.Visible = !string.IsNullOrWhiteSpace(_note.Text);
        FitBody();
        _validation.Text = "";
        if (!bound)
        {
            _stamina.Text = _afterStamina.Text = _recovered.Text = _healthResult.Text = _wakeDate.Text = "—";
            _before.Value = _after.Value = 0; _before.Modulate = new Color(1, 1, 1, .45f);
            _preview.Text = "—"; _start.Disabled = true; return;
        }
        var current = _character.current_world_stamina; var max = _character.max_world_stamina;
        _healthResult.Text = "恢复至上限";
        _stamina.Text = $"{current:0.#} / {max:0.#}";
        _before.MaxValue = _after.MaxValue = Math.Max(1, max); _before.Value = current; _before.Modulate = Colors.White;
        var hours = Hours(); _fullDuration.Text = _fixedDuration.Text = hours.ToString();
        _start.Disabled = hours <= 0 || _confirm is null;
        var recovered = hours <= 0 ? 0d : Math.Max(0d, Math.Min((double)max - current, (double)hours * WorldStaminaRules.RestRecoveryPerGameHour));
        _afterStamina.Text = $"{current + recovered:0.#} / {max:0.#}";
        _recovered.Text = $"{recovered:0.#} 点"; _after.Value = current + recovered;
        if (hours <= 0)
        {
            _wakeDate.Text = "—";
            _validation.Text = _fullStaminaMode && free && current >= max ? "精力已满，无需休息。"
                : _dateMode && free ? "请选择晚于当前日期的醒来日期。" : "当前无法安排休息。";
            _preview.Text = _validation.Text; return;
        }
        long end = (long)_clock.get_total_game_hours() + hours, day = end / 24;
        _wakeDate.Text = $"{day / 360 + 1}年{day % 360 / 30 + 1}月{day % 30 + 1}日";
        _preview.Text = $"醒来后\n精力  {current:0.#} → {current + recovered:0.#}\n恢复精力 {recovered:0.#} 点\n生命恢复至上限\n\n休息 {hours} 小时\n醒来日期：{_wakeDate.Text}";
    }
    private void Confirm()
    {
        foreach (var number in _numbers) number.CommitInput();
        var count = Hours(); if (count <= 0 || _confirm is null) return;
        var confirm = _confirm; Close(); confirm(count);
    }
    public void Close() { _confirm = null; Hide(); }
    private void OnStaminaChanged(Character _) => Refresh();
    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_clock)) _clock.time_changed -= Refresh;
        if (GodotObject.IsInstanceValid(_character)) _character.WorldStaminaChanged -= OnStaminaChanged;
    }
}
