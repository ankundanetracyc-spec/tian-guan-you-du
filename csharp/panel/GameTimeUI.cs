using Godot;

/// <summary>左上角紧凑时间纸签。单人模式的时间只由明确行动推进，因此不提供暂停或倍速控件。</summary>
public partial class GameTimeUI : Control
{
    private GameTimeMgr? _manager;
    private WeatherMgr? _weather;
    private Label _date = null!, _conditions = null!;
    private string _lastReadout = "";
    private bool _showZhaoming = true;
    private Button _calendarToggle = null!;
    private const string PreferencesPath = "user://hud_preferences.cfg";

    public override void _Ready()
    {
        ZIndex = 1000;
        // 时间读数只是信息显示，不占用右侧独立测试入口的鼠标事件。
        MouseFilter = MouseFilterEnum.Ignore;
        var preferences = new ConfigFile();
        if (preferences.Load(PreferencesPath) == Error.Ok)
            _showZhaoming = preferences.GetValue("time", "zhaoming_calendar", true).AsBool();
        BuildPanel();
        _manager = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        _weather = GetNodeOrNull<WeatherMgr>("/root/WeatherManager");
        RefreshReadout();
    }

    public override void _Process(double delta)
    {
        RefreshReadout();
    }

    private void BuildPanel()
    {
        var panel = new Control
        {
            Name = "TimePaper", Position = new Vector2(22, 20),
            Size = new Vector2(430, 78), MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(panel);
        var frame = new WorldHudFrame { Position = Vector2.Zero, Size = new Vector2(430, 46) };
        panel.AddChild(frame);
        _date = WorldHudStyle.Text("", 22);
        _date.Position = new Vector2(16, 5);
        _date.Size = new Vector2(348, 36);
        _date.HorizontalAlignment = HorizontalAlignment.Center;
        panel.AddChild(_date);
        var divider = WorldHudStyle.Rule();
        divider.Position = new Vector2(370, 10);
        divider.Size = new Vector2(1, 26);
        panel.AddChild(divider);
        _calendarToggle = new Button
        {
            Name = "CalendarToggle", Text = "切换", Position = new Vector2(375, 5),
            Size = new Vector2(44, 36), MouseFilter = MouseFilterEnum.Stop
        };
        WorldHudStyle.StyleSmallButton(_calendarToggle, 16);
        _calendarToggle.Pressed += ToggleCalendar;
        panel.AddChild(_calendarToggle);
        _conditions = WorldHudStyle.Text("", 17, true);
        _conditions.Position = new Vector2(12, 51);
        _conditions.Size = new Vector2(406, 24);
        _conditions.HorizontalAlignment = HorizontalAlignment.Center;
        var weatherPaper = new WorldHudFrame
        {
            Position = new Vector2(7, 49), Size = new Vector2(416, 28),
            MouseFilter = MouseFilterEnum.Ignore, ShowBorder = false
        };
        panel.AddChild(weatherPaper);
        panel.AddChild(_conditions);
    }

    private void ToggleCalendar()
    {
        _showZhaoming = !_showZhaoming;
        _lastReadout = "";
        RefreshReadout();
        var preferences = new ConfigFile();
        preferences.Load(PreferencesPath);
        preferences.SetValue("time", "zhaoming_calendar", _showZhaoming);
        preferences.Save(PreferencesPath);
    }

    private void RefreshReadout()
    {
        if (_manager is null) return;
        var temperature = _weather is null ? "" : $"{_weather.get_temperature():F1}°C";
        var weather = _weather?.weather ?? "";
        var key = $"{_manager.year}/{_manager.month}/{_manager.day}/{_manager.hour}/{weather}/{temperature}";
        if (key == _lastReadout) return;
        _lastReadout = key;
        var year = _showZhaoming ? $"昭明历{_manager.get_zhaoming_year()}年" : $"入世第{_manager.year}年";
        _date.Text = $"{year}{_manager.month}月{_manager.day}日 · {_manager.hour:00}时";
        _calendarToggle.TooltipText = _showZhaoming ? "当前：昭明历。点击切换入世年数。" : "当前：入世年数。点击切换昭明历。";

        var phase = _manager.hour switch { >= 6 and < 18 => "昼", _ => "夜" };
        var name = weather switch { "clear" => "晴", "cloudy" => "多云", "rain" => "雨", "snow" => "雪", "thunderstorm" => "雷雨", _ => weather };
        _conditions.Text = $"{_manager.get_time_branch()}时 · {_manager.get_season()} · {phase}　{name} {temperature}".Trim();
    }
}
