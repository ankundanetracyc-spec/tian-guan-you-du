using Godot;

public partial class Character
{
    private bool _advancingYuanxi;
    public int yuanxi_count => Math.Clamp(human?.yuanxi_count ?? 0, 0, 9);
    public bool is_yuanxi_realm() => cultivation_realm == "云息十层";
    public bool is_yuanxi_active() => human?.yuanxi_active == true;
    public bool is_yunxi_complete() => is_yuanxi_realm() && yuanxi_count == 9;
    public event Action<Character>? YuanxiChanged;

    public override void _ExitTree()
    {
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        if (time is not null) time.time_changed -= process_yuanxi_external_time;
        base._ExitTree();
    }

    // 用现有倍率之比，不把缕/小时转换成新的养息资源，也不改全局吐纳数值。
    public double get_yuanxi_efficiency() => get_element_efficiency(main_gongfa) * get_cultivation_environment_factor();
    public double get_yuanxi_phase_required(int phase) => phase switch
    {
        0 => 640d,
        1 => (8 + yuanxi_count) * 16d,
        _ => (6 + 2 * yuanxi_count) * 16d
    };
    public string get_yuanxi_phase_name() => (human?.yuanxi_phase ?? 0) switch { 0 => "周行", 1 => "牵引", _ => "平衡" };
    public double get_yuanxi_remaining_hours()
    {
        if (human is null || yuanxi_count == 9) return 0;
        var efficiency = get_yuanxi_efficiency();
        if (efficiency <= 0) return double.PositiveInfinity;
        double remaining = 0;
        for (var phase = human.yuanxi_phase; phase < 3; phase++)
            remaining += Math.Max(0, get_yuanxi_phase_required(phase) - (phase == human.yuanxi_phase ? human.yuanxi_phase_hours : 0)) / (phase == 0 ? efficiency : 1);
        return remaining;
    }
    public string get_yuanxi_notice() => human?.yuanxi_notice ?? "";
    public bool start_yuanxi(int target)
    {
        if (human is null || !is_yuanxi_realm() || yuanxi_count >= 9 || is_in_breakthrough_retreat() || get_cultivation_rate() <= 0 || is_yuanxi_active()) return false;
        human.yuanxi_target = Math.Clamp(target, yuanxi_count + 1, 9);
        human.yuanxi_phase = 0; human.yuanxi_phase_hours = 0;
        human.yuanxi_clock_hour = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
        human.yuanxi_notice = "闭关开始；修炼不消耗精力，主动出关将失去当前未成一息。";
        human.yuanxi_active = true;
        NotifyYuanxi();
        return true;
    }
    public void stop_yuanxi(string reason = "已出关，当前未成一息的进度已清空。")
    {
        if (human is null || !human.yuanxi_active) return;
        human.yuanxi_active = false; human.yuanxi_phase = 0; human.yuanxi_phase_hours = 0;
        human.yuanxi_notice = reason;
        NotifyYuanxi();
    }
    public void advance_yuanxi(int requestedHours = 16)
    {
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        if (human is null || !is_yuanxi_active() || time is null) return;
        if (!is_yuanxi_realm() || get_cultivation_rate() <= 0) { stop_yuanxi("修炼条件已改变，本息中断，已有归元息保留。"); return; }
        var hours = Math.Max(0, requestedHours);
        if (hours <= 0) return;
        double budget = hours, used = 0;
        while (budget > 0.000001 && human.yuanxi_active)
        {
            var phase = human.yuanxi_phase;
            var rate = phase == 0 ? get_yuanxi_efficiency() : 1d;
            var remaining = Math.Max(0, get_yuanxi_phase_required(phase) - human.yuanxi_phase_hours);
            var spent = Math.Min(budget, remaining / rate);
            human.yuanxi_phase_hours += spent * rate; budget -= spent; used += spent;
            if (human.yuanxi_phase_hours + 0.000001 < get_yuanxi_phase_required(phase)) break;
            human.yuanxi_phase_hours = 0; human.yuanxi_phase++;
            if (human.yuanxi_phase < 3) continue;
            human.yuanxi_count = yuanxi_count + 1; human.yuanxi_phase = 0;
            human.yuanxi_notice = yuanxi_count == 9 ? "归元九息已成，云息十层圆满。" : $"第{yuanxi_count}息已成。";
            if (yuanxi_count >= human.yuanxi_target) human.yuanxi_active = false;
        }
        // 沿用普通修炼的实际小时推进历法；没有额外免费休息或离线收益。
        _advancingYuanxi = true;
        try { time.advance_time((float)used, "yuanxi"); human.yuanxi_clock_hour = time.get_total_game_hours(); }
        finally { _advancingYuanxi = false; }
        NotifyYuanxi();
    }
    private void process_yuanxi_external_time()
    {
        if (human is null || !human.yuanxi_active || _advancingYuanxi) return;
        var clock = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? human.yuanxi_clock_hour;
        if (clock != human.yuanxi_clock_hour) stop_yuanxi("已进行其他行动，本息中断；已有归元息保留。");
    }
    private void NotifyYuanxi()
    {
        YuanxiChanged?.Invoke(this);
        foreach (var node in GetTree().GetNodesInGroup("main_scene"))
            if (node is MainScene main) main.notify_yuanxi_changed(this);
    }
}
