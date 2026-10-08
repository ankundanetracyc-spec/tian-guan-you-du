using Godot;

/// <summary>左上角紧凑时间纸签。单人模式的时间只由明确行动推进，因此不提供暂停或倍速控件。</summary>
public partial class GameTimeUI : Control
{
    private static readonly Color Ink = new("343d33");
    private GameTimeMgr? _manager;
    private WeatherMgr? _weather;
    private Label _date = null!, _hour = null!, _conditions = null!;
    private string _lastReadout = "";

    public override void _Ready()
    {
        ZIndex = 1000;
        // 时间读数只是信息显示，不占用右侧独立测试入口的鼠标事件。
        MouseFilter = MouseFilterEnum.Ignore;
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
        var panel = new PanelContainer { Position = new Vector2(18, 16), Size = new Vector2(354, 112), MouseFilter = MouseFilterEnum.Ignore };
        var frame = new StyleBoxFlat
        {
            BgColor = new Color(.035f, .045f, .04f, .91f),
            BorderColor = new Color("b49a65"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 2,
            ContentMarginTop = 2,
            ContentMarginRight = 2,
            ContentMarginBottom = 2,
        };
        panel.AddThemeStyleboxOverride("panel", frame);
        AddChild(panel);
        var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        margin.AddThemeConstantOverride("margin_left", 15); margin.AddThemeConstantOverride("margin_top", 10); margin.AddThemeConstantOverride("margin_right", 15); margin.AddThemeConstantOverride("margin_bottom", 10); panel.AddChild(margin);
        var column = new VBoxContainer { Name = "TimeReadout", MouseFilter = MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 2); margin.AddChild(column);
        var head = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; column.AddChild(head);
        _date = MakeLabel("", 15, new Color("e0c98f")); _date.SizeFlagsHorizontal = SizeFlags.ExpandFill; head.AddChild(_date);
        var calendar_mark = MakeLabel("百川历", 12, new Color(.67f, .68f, .59f));
        head.AddChild(calendar_mark);
        _hour = MakeLabel("", 28, Ink); _hour.AddThemeColorOverride("font_color", new Color("f3ecd9")); column.AddChild(_hour);
        _conditions = MakeLabel("", 13, new Color("c2cbb4")); column.AddChild(_conditions);

    }

    private static Label MakeLabel(string text, int size, Color color)
    {
        var label = new Label { Text = text, VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private void RefreshReadout()
    {
        if (_manager is null) return;
        var temperature = _weather is null ? "" : $"{_weather.get_temperature():F1}°C";
        var weather = _weather?.weather ?? "";
        var key = $"{_manager.year}/{_manager.month}/{_manager.day}/{_manager.hour}/{weather}/{temperature}";
        if (key == _lastReadout) return;
        _lastReadout = key;
        _date.Text = $"{_manager.year}年 · {_manager.month}月{_manager.day}日";
        _hour.Text = $"{_manager.get_time_branch()}时  {_manager.hour:00}时";
        var phase = _manager.hour switch { >= 6 and < 18 => "昼", _ => "夜" };
        var name = weather switch { "clear" => "晴", "cloudy" => "多云", "rain" => "雨", "snow" => "雪", "thunderstorm" => "雷雨", _ => weather };
        _conditions.Text = $"{_manager.get_season()} · {phase}　{name} {temperature}".Trim();
    }
}
