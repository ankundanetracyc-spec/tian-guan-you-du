using Godot;

/// <summary>休息行前配置。时间仅由调用方的统一行动结算推进。</summary>
public partial class RestPanel : Control
{
    private Character _character = null!;
    private GameTimeMgr _clock = null!;
    private Action<int>? _confirm;
    private TextureRect _background = null!;
    private Texture2D _day = null!, _night = null!;
    private Label _place = null!, _time = null!, _stamina = null!, _preview = null!, _note = null!;
    private HBoxContainer _modes = null!, _hoursRow = null!;
    private VBoxContainer _dateRow = null!;
    private SpinBox _hours = null!, _year = null!, _month = null!, _date = null!;
    private Button _byHours = null!, _byDate = null!, _start = null!;
    private ProgressBar _before = null!, _after = null!;
    private int? _fixedHours;
    private bool _dateMode;
    private bool? _shownDay;

    public override void _Ready()
    {
        Name = "RestPanel"; SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); MouseFilter = MouseFilterEnum.Stop; ZIndex = 600;
        Theme = QuietRoomStyle.MakeTheme();
        _day = GD.Load<Texture2D>(QuietRoomStyle.Assets + "rest_day.png"); _night = GD.Load<Texture2D>(QuietRoomStyle.Assets + "rest_night.png");
        _background = QuietRoomStyle.Background(this, "rest_day.png");
        var canvas = QuietRoomStyle.Canvas(this);
        var header = new HBoxContainer { AnchorLeft = .035f, AnchorRight = .965f, AnchorTop = .025f, AnchorBottom = .10f }; header.AddThemeConstantOverride("separation", 24); canvas.AddChild(header);
        header.AddChild(QuietRoomStyle.Text("休息", 32)); _place = QuietRoomStyle.Text("", 21); _place.SizeFlagsHorizontal = SizeFlags.ExpandFill; header.AddChild(_place);
        _time = QuietRoomStyle.Text("", 23); header.AddChild(_time);
        var close = new Button { Text = "返回", CustomMinimumSize = new Vector2(120, 48) }; close.Pressed += Close; header.AddChild(close);
        var paper = new PanelContainer { AnchorLeft = .60f, AnchorRight = .965f, AnchorTop = .165f, AnchorBottom = .955f }; paper.AddThemeStyleboxOverride("panel", QuietRoomStyle.Paper(.98f, 26)); canvas.AddChild(paper);
        var layout = new VBoxContainer(); layout.AddThemeConstantOverride("separation", 14); paper.AddChild(layout);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsVertical = SizeFlags.ExpandFill }; layout.AddChild(scroll);
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill }; box.AddThemeConstantOverride("separation", 14); scroll.AddChild(box);
        var title = QuietRoomStyle.Text("安心歇息", 28); box.AddChild(title); box.AddChild(new HSeparator());
        _stamina = QuietRoomStyle.Text("", 21); box.AddChild(_stamina);
        _before = new ProgressBar { ShowPercentage = false, CustomMinimumSize = new Vector2(0, 12) }; box.AddChild(_before);
        _modes = new HBoxContainer(); _modes.AddThemeConstantOverride("separation", 12); box.AddChild(_modes);
        _byHours = new Button { Text = "按时长", ToggleMode = true, SizeFlagsHorizontal = SizeFlags.ExpandFill }; _byHours.Pressed += () => { _dateMode = false; Refresh(); }; _modes.AddChild(_byHours);
        _byDate = new Button { Text = "指定日期", ToggleMode = true, SizeFlagsHorizontal = SizeFlags.ExpandFill }; _byDate.Pressed += () => { _dateMode = true; Refresh(); }; _modes.AddChild(_byDate);
        _hoursRow = new HBoxContainer(); _hoursRow.AddThemeConstantOverride("separation", 14); box.AddChild(_hoursRow); _hoursRow.AddChild(QuietRoomStyle.Text("休息时长"));
        _hours = Number(1, 720, 8); _hoursRow.AddChild(_hours); _hoursRow.AddChild(QuietRoomStyle.Text("小时"));
        _dateRow = new VBoxContainer(); box.AddChild(_dateRow); _dateRow.AddChild(QuietRoomStyle.Text("醒来日期", 18)); var date = new HBoxContainer(); _dateRow.AddChild(date);
        _year = Number(1, 9999, 1); _month = Number(1, 12, 1); _date = Number(1, 30, 1);
        foreach (var (number, label) in new[] { (_year, "年"), (_month, "月"), (_date, "日") }) { number.CustomMinimumSize = new Vector2(80, 44); date.AddChild(number); date.AddChild(QuietRoomStyle.Text(label, 18)); }
        foreach (var number in new[] { _hours, _year, _month, _date }) number.ValueChanged += _ => { if (_clock is not null) Refresh(); };
        _preview = QuietRoomStyle.Text("", 21); _preview.AutowrapMode = TextServer.AutowrapMode.WordSmart; box.AddChild(_preview);
        _after = new ProgressBar { ShowPercentage = false, CustomMinimumSize = new Vector2(0, 16) }; box.AddChild(_after);
        _note = QuietRoomStyle.Text("", 18); _note.AutowrapMode = TextServer.AutowrapMode.WordSmart; box.AddChild(_note);
        box.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        _start = new Button { Text = "开始休息" }; QuietRoomStyle.Primary(_start); _start.Pressed += Confirm; layout.AddChild(_start);
        var rule = QuietRoomStyle.Text($"每小时恢复 {WorldStaminaRules.RestRecoveryPerGameHour:0.#} 点精力 · 开始后推进时间", 16); rule.AutowrapMode = TextServer.AutowrapMode.WordSmart; layout.AddChild(rule);
        QuietRoomStyle.Frame(paper);
        Hide();
    }
    private static SpinBox Number(int min, int max, int value) => new() { MinValue = min, MaxValue = max, Step = 1, Value = value, Rounded = true, CustomMinimumSize = new Vector2(110, 44) };
    public void Open(Character character, GameTimeMgr clock, string place, Action<int> confirm, int? fixedHours = null, string note = "")
    {
        if (_clock is not null) _clock.time_changed -= Refresh;
        _character = character; _clock = clock; _clock.time_changed += Refresh; _confirm = confirm; _fixedHours = fixedHours; _dateMode = false;
        _place.Text = place; _note.Text = note; _shownDay = null;
        var tomorrow = clock.get_total_game_hours() / 24 + 1;
        // 保持现有 30日/月、12月/年规则，并处理月末与年末。
        _year.SetValueNoSignal(tomorrow / 360 + 1); _month.SetValueNoSignal(tomorrow % 360 / 30 + 1); _date.SetValueNoSignal(tomorrow % 30 + 1); _hours.SetValueNoSignal(8);
        Refresh(); Show(); MoveToFront();
    }
    private int Hours() => _fixedHours ?? (_dateMode ? ((((int)_year.Value - 1) * 360 + ((int)_month.Value - 1) * 30 + (int)_date.Value - 1) * 24 + _clock.hour - _clock.get_total_game_hours()) : (int)_hours.Value);
    public void Refresh()
    {
        if (_clock is null || _character is null) return;
        var isDay = QuietRoomStyle.IsDay(_clock);
        if (_shownDay != isDay) { _background.Texture = isDay ? _day : _night; _shownDay = isDay; }
        _time.Text = QuietRoomStyle.Date(_clock) + (isDay ? " · 昼" : " · 夜");
        var current = _character.current_world_stamina; var max = _character.max_world_stamina;
        _stamina.Text = $"当前精力  {current:0.#} / {max:0.#}";
        _before.MaxValue = _after.MaxValue = Math.Max(1, max); _before.Value = current;
        _modes.Visible = !_fixedHours.HasValue; _hoursRow.Visible = !_fixedHours.HasValue && !_dateMode; _dateRow.Visible = !_fixedHours.HasValue && _dateMode;
        _byHours.ButtonPressed = !_dateMode; _byDate.ButtonPressed = _dateMode;
        var hours = Hours(); _start.Disabled = hours <= 0;
        if (hours <= 0) { _preview.Text = "请选择晚于当前日期的醒来日期。"; _after.Value = current; return; }
        var recovered = Math.Max(0, Math.Min(max - current, hours * WorldStaminaRules.RestRecoveryPerGameHour));
        var end = _clock.get_total_game_hours() + hours; var day = end / 24;
        _preview.Text = $"醒来后\n精力  {current:0.#} → {current + recovered:0.#}\n恢复精力 {recovered:0.#} 点\n生命恢复至上限\n\n休息 {hours} 小时\n醒来日期：{day / 360 + 1}年{day % 360 / 30 + 1}月{day % 30 + 1}日";
        _after.Value = current + recovered;
    }
    private void Confirm() { var count = Hours(); if (count <= 0) return; var confirm = _confirm; Close(); confirm?.Invoke(count); }
    public void Close() { _confirm = null; Hide(); }
    public override void _ExitTree() { if (GodotObject.IsInstanceValid(_clock)) _clock.time_changed -= Refresh; }
}
