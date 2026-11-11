using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

public partial class BaiChuanFreeWorldFlowSmoke
{
    private readonly List<object> _travelPerformance = [];
    private const string PerformanceFolder = "res://output/bai_chuan_travel_performance_20261106";

    private void MeasureTravelWork(string name, Action action, int count = 5)
    {
        var samples = new List<double>();
        for (var i = 0; i < count; i++)
        {
            var watch = Stopwatch.StartNew();
            action();
            samples.Add(watch.Elapsed.TotalMilliseconds);
        }
        samples.Sort();
        var row = new { name, count, median_ms = samples[count / 2], max_ms = samples[^1], total_ms = samples.Sum() };
        _travelPerformance.Add(row);
        GD.Print("TRAVEL_WORK " + JsonSerializer.Serialize(row));
    }

    private async Task MeasureTravelFrames(string name, bool move, bool simulation, int count = 100)
    {
        Stop(); _time.resume_time(); _world.set_simulation_enabled(simulation);
        if (move) Require(Navigate(GroundTarget(900)), "性能采样开始连续行走 " + name);
        await Frames(3);
        var samples = new List<double>();
        var previous = Time.GetTicksUsec();
        var stoppedAt = -1;
        var stoppedReason = "";
        var viewport = GetViewport();
        var priorGuiInput = viewport.GuiDisableInput;
        viewport.GuiDisableInput = true;
        _map.SetProcessInput(false);
        for (var i = 0; i < count; i++)
        {
            await Frames();
            var now = Time.GetTicksUsec();
            samples.Add((now - previous) / 1000.0);
            previous = now;
            if (move && !Moving && stoppedAt < 0)
            {
                stoppedAt = i;
                stoppedReason = ((Label)_map.Get("travel_status").AsGodotObject()).Text;
                GD.Print($"TRAVEL_PROBE_STOP frame={i} point={Position} reason={stoppedReason} remaining={Remaining:R} target_npc={_map.Get("_target_npc")} blocked={_map.Get("encounter_blocked")}");
            }
        }
        viewport.GuiDisableInput = priorGuiInput;
        _map.SetProcessInput(true);
        var advancing = Moving;
        Stop();
        samples.Sort();
        var row = new { name, simulation, frames = count, median_ms = samples[count / 2], p95_ms = samples[(int)(count * .95)], max_ms = samples[^1], over_50ms = samples.Count(x => x > 50), over_100ms = samples.Count(x => x > 100), average_ms = samples.Average(), still_moving = advancing, stopped_at = stoppedAt, stopped_reason = stoppedReason };
        _travelPerformance.Add(row);
        GD.Print("TRAVEL_FRAMES " + JsonSerializer.Serialize(row));
        if (move) Check(advancing && stoppedAt < 0, "性能采样全程保持实际行走 " + name);
    }

    private async Task TravelPerformance()
    {
        AtAnchor(); _time.restore_calendar(1, 1, 2, 0, 0); _world.set_simulation_enabled(false);
        var locationArg = OS.GetCmdlineUserArgs().FirstOrDefault(arg => arg.StartsWith("--performance-location=", StringComparison.Ordinal));
        if (locationArg is not null)
        {
            var runtimeId = locationArg["--performance-location=".Length..];
            Require(_map.Call("set_initial_runtime_id", runtimeId).AsBool(), "性能采样使用真实地点 " + runtimeId);
            _map.Call("_set_zoom_level", 2, GetViewport().GetVisibleRect().Size / 2);
            _map.Call("focus_player_node");
        }
        await Frames(20);
        MeasureTravelWork("map_update_context", () => _map.Call("_update_context"), 30);
        MeasureTravelWork("map_refresh_people", () => _map.Call("_refresh_people"), 12);
        MeasureTravelWork("map_nearby_person_ids", () => _map.Call("get_nearby_person_ids"), 20);
        MeasureTravelWork("node_population_counts", () => Invoke(_main!, "refresh_node_world_population_counts"), 10);
        MeasureTravelWork("main_action_time_changed", () => Invoke(_main!, "on_action_time_changed"), 10);
        MeasureTravelWork("main_persistent_chrome", () => Invoke(_main!, "update_persistent_chrome"), 10);
        MeasureTravelWork("snapshot", () => Invoke(_main!, "capture_progress_snapshot", false), 2);
        MeasureTravelWork("npc_remaining_trip_hours", () =>
        {
            foreach (var npc in _world.npcs)
                if (npc.is_traveling) Invoke(_world, "get_remaining_world_travel_hours", npc);
        }, 5);
        MeasureTravelWork("advance_1_hour_no_npc", () => _time.advance_time(1, "world_map_travel"), 12);
        _world.set_simulation_enabled(true);
        MeasureTravelWork("advance_1_hour_with_npc", () => _time.advance_time(1, "world_map_travel"), 12);
        await MeasureTravelFrames("standing", false, true);
        await MeasureTravelFrames("walking_without_npc", true, false);
        await MeasureTravelFrames("walking_with_npc", true, true);
        if (locationArg is not null) await Capture("forest_grounding_white_stone_1920");
        _world.set_simulation_enabled(false);
        await VerifyNavigationHover();
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(PerformanceFolder));
        using var report = Godot.FileAccess.Open(PerformanceFolder + "/" + _runName + ".json", Godot.FileAccess.ModeFlags.Write);
        report.StoreString(JsonSerializer.Serialize(new { samples = _travelPerformance }, new JsonSerializerOptions { WriteIndented = true }));
        await Capture("travel_performance_1920");
    }

    private async Task VerifyNavigationHover()
    {
        AtAnchor(); await Frames(8);
        var roster = GetTree().GetFirstNodeInGroup("cell_character_list") as CellCharacterList;
        Require(roster is not null && Field(roster, "_toggle_button") is Button, "人物名单折叠按钮存在");
        var toggle = (Button)Field(roster!, "_toggle_button")!;
        var hover = GetNode<HoverInfoManager>("/root/HoverInfoManager");
        for (var state = 0; state < 2; state++)
        {
            HoverInfoManager.HideAll();
            await Motion(toggle.GetGlobalRect().GetCenter(), Vector2.Zero); await Frames(20);
            Check(string.IsNullOrEmpty(toggle.TooltipText), "人物名单按钮没有原生悬停文案 " + state);
            var entries = (System.Collections.IDictionary)Field(hover, "_entries")!;
            Check(!entries.Contains(toggle), "人物名单按钮没有遗留共享悬停内容 " + state);
            Check(Field(hover, "_card") is Control { Visible: false }, "真实悬停不弹出操作说明卡片 " + state);
            if (state == 0) { toggle.EmitSignal(Button.SignalName.Pressed); await Frames(20); }
        }
        await Capture("navigation_hover_removed_1920");
        toggle.EmitSignal(Button.SignalName.Pressed); await Frames(20);
        var badges = _map.Get("population_badges").AsGodotObject();
        var before = badges.Get("counts").AsGodotDictionary().Duplicate(true);
        var hiddenCounts = before.Duplicate(true); hiddenCounts["bc_transit_116_02"] = 999;
        _map.Call("set_population_counts", hiddenCounts);
        Check(badges.Get("counts").AsGodotDictionary().Count == before.Count && !badges.Get("counts").AsGodotDictionary().ContainsKey("bc_transit_116_02"), "旧隐藏途经点人数不会进入自由地图人口徽标");
    }
}
