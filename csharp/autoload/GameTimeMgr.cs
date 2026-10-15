using Godot;

/// <summary>
/// 游戏内历法与时间推进的唯一入口。
/// 保持与原 GDScript 单例相同的成员名，供尚未迁移的脚本调用。
/// </summary>
public partial class GameTimeMgr : Node
{
    public const int AUTO_SIMULATION_TICK_HOURS = 4;
    // 入世第一年对应昭明历 2371 年；仅转换显示，不改变存档的相对时间。
    public const int ZhaomingStartYear = 2371;
    public int get_zhaoming_year() => ZhaomingStartYear + year - 1;

    [Export] public int year = 1;
    [Export] public int month = 1;
    [Export] public int day = 1;
    [Export] public int hour;
    [Export] public bool is_paused;
    [Export] public float speed_multiplier = 1.0f;
    [Export] public bool auto_advance = false;

    private float _pendingHours;
    private int _lastSimulationTickHour;
    private bool _isEmittingTimeChanged;
    private bool _queuedTimeChanged;
    private string _advanceReason = "";
    public event Action<int, int, string>? TimeAdvanceStarting;

    [Signal] public delegate void time_changedEventHandler();
    [Signal] public delegate void pause_changedEventHandler(bool paused);
    [Signal] public delegate void speed_changedEventHandler(float multiplier);
    [Signal] public delegate void slumber_startedEventHandler(int days);
    [Signal] public delegate void slumber_endedEventHandler(int days);

    // 单人修仙采用行动制：只有休息、修行、研读、闭关、行走等明确行动才会推进历法。
    public override void _Ready()
    {
        auto_advance = false;
        is_paused = false;
    }

    public override void _Process(double delta)
    {
        if (!is_paused && auto_advance)
            advance_time((float)delta * speed_multiplier, "auto");
    }

    public void pause_time()
    {
        is_paused = true;
        EmitSignal(SignalName.pause_changed, true);
    }

    public void resume_time()
    {
        is_paused = false;
        EmitSignal(SignalName.pause_changed, false);
    }

    public void set_speed(float multiplier)
    {
        speed_multiplier = Mathf.Clamp(multiplier, 1.0f, 10.0f);
        EmitSignal(SignalName.speed_changed, speed_multiplier);
    }

    public void advance_time(float hours, string reason = "")
    {
        if (hours <= 0.0f)
            return;

        _pendingHours += hours;
        var wholeHours = (int)_pendingHours;
        if (wholeHours < 1)
            return;

        _pendingHours -= wholeHours;
        var previousReason = _advanceReason; _advanceReason = reason;
        try { add_hours(wholeHours, reason != "auto"); }
        finally { _advanceReason = previousReason; }
    }

    public void add_hours(int count, bool force_simulation_tick = true)
    {
        if (count <= 0)
            return;

        var fromHour = get_total_game_hours();
        TimeAdvanceStarting?.Invoke(fromHour, fromHour + count, _advanceReason);
        hour += count;
        while (hour >= 24)
        {
            hour -= 24;
            day++;
            while (day > 30)
            {
                day -= 30;
                month++;
                while (month > 12)
                {
                    month -= 12;
                    year++;
                }
            }
        }

        var totalHours = get_total_game_hours();
        if (force_simulation_tick || totalHours - _lastSimulationTickHour >= AUTO_SIMULATION_TICK_HOURS)
        {
            _lastSimulationTickHour = totalHours;
            emit_time_changed_safely();
        }
    }

    /// <summary>Restore the saved calendar and fractional hour without advancing a simulation tick.</summary>
    public void restore_calendar(int savedYear, int savedMonth, int savedDay, int savedHour, float pendingHours)
    {
        year = Mathf.Max(1, savedYear);
        month = Mathf.Clamp(savedMonth, 1, 12);
        day = Mathf.Clamp(savedDay, 1, 30);
        hour = Mathf.Clamp(savedHour, 0, 23);
        _pendingHours = Mathf.Clamp(pendingHours, 0f, 0.9999f);
        _lastSimulationTickHour = get_total_game_hours();
        emit_time_changed_safely();
    }

    public float get_pending_hours() => _pendingHours;

    private void emit_time_changed_safely()
    {
        if (_isEmittingTimeChanged)
        {
            _queuedTimeChanged = true;
            return;
        }

        _isEmittingTimeChanged = true;
        EmitSignal(SignalName.time_changed);
        _isEmittingTimeChanged = false;

        if (_queuedTimeChanged)
        {
            _queuedTimeChanged = false;
            CallDeferred(MethodName.emit_time_changed_safely);
        }
    }

    public string get_season() => month switch
    {
        1 => "冬",
        2 or 3 or 4 => "春",
        5 or 6 or 7 => "夏",
        8 or 9 or 10 => "秋",
        11 or 12 => "冬",
        _ => string.Empty,
    };

    public string get_time_branch(int h = -1)
    {
        if (h < 0) h = hour;
        // 子时跨越午夜（23:00–01:00），各刻度标在对应时辰的中点。
        return (((h % 24 + 1) % 24) / 2) switch
        {
            0 => "子", 1 => "丑", 2 => "寅", 3 => "卯", 4 => "辰", 5 => "巳",
            6 => "午", 7 => "未", 8 => "申", 9 => "酉", 10 => "戌", 11 => "亥",
            _ => string.Empty,
        };
    }

    public string get_time_display() => $"{hour}时（{get_time_branch(hour)}）";

    public void slumber(int days)
    {
        if (days <= 0) return;
        EmitSignal(SignalName.slumber_started, days);
        var elapsed = 0;
        while (elapsed < days)
        {
            add_hours(24);
            elapsed++;
        }
        EmitSignal(SignalName.slumber_ended, elapsed);
    }

    // 供日晷等连续动画读取；与模拟共享累积时间，暂停和倍速无需另建计时器。
    public double get_precise_game_hours() => get_total_game_hours() + (double)_pendingHours;

    public int get_total_game_hours() => (year - 1) * 12 * 30 * 24 + (month - 1) * 30 * 24 + (day - 1) * 24 + hour;
}
