using Godot;

/// <summary>将天气单例状态映射到雨雪粒子效果。</summary>
public partial class WeatherEffects : CanvasLayer
{
    private GpuParticles2D? _rain;

    public override void _Ready()
    {
        _rain = GetNodeOrNull<GpuParticles2D>("Rain");
    }

    public override void _Process(double delta)
    {
        var weatherManager = GetNodeOrNull<WeatherMgr>("/root/WeatherManager");
        if (weatherManager is null)
            return;

        stop_all();
        switch (weatherManager.weather)
        {
            case "rain":
            case "thunderstorm":
                if (_rain is not null)
                {
                    _rain.Emitting = true;
                    _rain.Amount = (int)(200 + weatherManager.weather_intensity * 300);
                }
                break;
        }
    }

    private void stop_all()
    {
        if (_rain is not null) _rain.Emitting = false;
    }
}
