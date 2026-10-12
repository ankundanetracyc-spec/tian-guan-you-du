using Godot;

/// <summary>
/// 基于游戏历法的天气服务。成员名与原 GDScript 保持一致，供混合迁移期间调用。
/// </summary>
public partial class WeatherMgr : Node
{
    [Export] public float temperature = 20.0f;
    [Export] public string weather = "clear";
    [Export] public float weather_intensity;
    [Export] public float wind_speed;
    [Export] public float weather_duration_hours;

    private int _lastWeatherCheckHour = -1;
    private int _lastWeatherDecisionHour = -1;
    private const int WeatherCheckIntervalHours = 24;
    private const float MinimumWeatherDurationHours = 7.0f * 24.0f; // Seven in-game days.
    private readonly RandomNumberGenerator _random = new();
    private ulong _weatherSeedOffset;

    [Signal] public delegate void weather_changedEventHandler(string new_weather, float intensity);

    public override void _Ready()
    {
        SetProcess(false);
        _random.Randomize();
        _weatherSeedOffset = _random.Randi();
        recalc_temperature();
        recalc_weather();

        var timeManager = get_time_manager();
        if (timeManager is null)
            return;

        _lastWeatherCheckHour = timeManager.get_total_game_hours();
        _lastWeatherDecisionHour = _lastWeatherCheckHour;
        var callback = Callable.From(on_game_time_changed);
        if (!timeManager.IsConnected(GameTimeMgr.SignalName.time_changed, callback))
            timeManager.Connect(GameTimeMgr.SignalName.time_changed, callback);
    }

    private GameTimeMgr? get_time_manager() => GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");

    private void on_game_time_changed()
    {
        var timeManager = get_time_manager();
        if (timeManager is null)
            return;

        var currentHour = timeManager.get_total_game_hours();
        if (_lastWeatherCheckHour < 0)
        {
            _lastWeatherCheckHour = currentHour;
            _lastWeatherDecisionHour = currentHour;
            return;
        }

        var hoursPassed = currentHour - _lastWeatherCheckHour;
        if (hoursPassed <= 0)
            return;

        _lastWeatherCheckHour = currentHour;
        weather_duration_hours += hoursPassed;
        if (_lastWeatherDecisionHour >= 0 && currentHour - _lastWeatherDecisionHour < WeatherCheckIntervalHours)
            return;

        _lastWeatherDecisionHour = currentHour;
        recalc_temperature();
        try_change_weather();
    }

    private void recalc_temperature()
    {
        var timeManager = get_time_manager();
        if (timeManager is null)
            return;

        var season = timeManager.get_season();
        var baseTemperature = season switch
        {
            "春" => Mathf.Lerp(8.0f, 22.0f, (timeManager.month - 2) / 3.0f),
            "夏" => Mathf.Lerp(22.0f, 36.0f, (timeManager.month - 5) / 3.0f),
            "秋" => Mathf.Lerp(22.0f, 8.0f, (timeManager.month - 8) / 3.0f),
            "冬" when timeManager.month == 12 => 0.0f,
            "冬" when timeManager.month == 1 => -3.0f,
            "冬" => 4.0f,
            _ => 20.0f,
        };

        var hourAngle = (timeManager.hour - 14.0f) / 24.0f * Mathf.Tau;
        var dailySeed = (ulong)(timeManager.year * 10_000 + timeManager.month * 100 + timeManager.day);
        _random.Seed = dailySeed;
        temperature = baseTemperature + Mathf.Cos(hourAngle) * 6.0f + _random.RandfRange(-3.0f, 3.0f);
    }

    private void try_change_weather()
    {
        if (weather_duration_hours >= MinimumWeatherDurationHours)
            recalc_weather();
    }

    private void recalc_weather()
    {
        var oldWeather = weather;
        var oldIntensity = weather_intensity;
        var oldWindSpeed = wind_speed;
        var timeManager = get_time_manager();
        var season = timeManager?.get_season() ?? "春";
        var rainChance = 0.0f;
        var snowChance = 0.0f;
        var thunderChance = 0.0f;

        if (temperature is > 5.0f and < 30.0f)
        {
            rainChance = 0.2f + Mathf.Abs(temperature - 17.0f) * 0.01f;
            if (season is "春" or "秋") rainChance += 0.1f;
        }
        else if (temperature >= 30.0f)
        {
            rainChance = 0.08f;
            thunderChance = 0.15f;
        }

        if (temperature <= 2.0f)
        {
            snowChance = 0.25f + Mathf.Abs(temperature) * 0.02f;
            if (season == "冬") snowChance += 0.2f;
        }
        else if (temperature <= 5.0f)
        {
            snowChance = 0.1f;
        }

        _random.Seed = (ulong)(timeManager?.get_total_game_hours() ?? 0) ^ _weatherSeedOffset;
        var roll = _random.Randf();
        if (roll < thunderChance && temperature > 20.0f)
        {
            weather = "thunderstorm";
            weather_intensity = _random.RandfRange(0.5f, 1.0f);
        }
        else if (roll < thunderChance + snowChance)
        {
            weather = "snow";
            weather_intensity = _random.RandfRange(0.3f, 1.0f);
        }
        else if (roll < thunderChance + snowChance + rainChance)
        {
            weather = "rain";
            weather_intensity = _random.RandfRange(0.3f, 0.9f);
        }
        else if (_random.Randf() < 0.3f)
        {
            weather = "cloudy";
            weather_intensity = _random.RandfRange(0.1f, 0.5f);
        }
        else
        {
            weather = "clear";
            weather_intensity = 0.0f;
        }

        wind_speed = weather == "clear" ? _random.RandfRange(0.0f, 3.0f) : _random.RandfRange(0.0f, 8.0f);
        if (weather == oldWeather)
        {
            // Keep the current rain/snow tier through this weather spell; otherwise a daily roll
            // could turn light rain into a downpour before the spell itself changes.
            weather_intensity = oldIntensity;
            wind_speed = oldWindSpeed;
            return;
        }

        weather_duration_hours = 0.0f;
        EmitSignal(SignalName.weather_changed, weather, weather_intensity);
    }

    public float get_temperature() => temperature;

    public void restore_persistent_state(string savedWeather, float savedIntensity, float savedWindSpeed, float savedDurationHours)
    {
        var timeManager = get_time_manager();
        if (string.IsNullOrWhiteSpace(savedWeather))
        {
            recalc_temperature();
            recalc_weather();
        }
        else
        {
            weather = savedWeather;
            weather_intensity = Mathf.Clamp(savedIntensity, 0.0f, 1.0f);
            wind_speed = Mathf.Clamp(savedWindSpeed, 0.0f, 8.0f);
            weather_duration_hours = Mathf.Max(savedDurationHours, 0.0f);
            recalc_temperature();
            EmitSignal(SignalName.weather_changed, weather, weather_intensity);
        }

        _lastWeatherCheckHour = timeManager?.get_total_game_hours() ?? -1;
        _lastWeatherDecisionHour = _lastWeatherCheckHour;
    }
}
