using Godot;
using System;
using System.Diagnostics;
using System.Linq;

public partial class NpcSettlementPerformanceSmoke : Node
{
    private static Variant Canonical(Variant value)
    {
        if (value.VariantType == Variant.Type.Object && value.AsGodotObject() is Resource resource)
        {
            var data = new Godot.Collections.Dictionary();
            foreach (var field in resource.GetType().GetFields().Where(f => Attribute.IsDefined(f, typeof(ExportAttribute))).OrderBy(f => f.Name, StringComparer.Ordinal))
                data[field.Name] = Canonical(resource.Get(field.Name));
            return data;
        }
        if (value.VariantType == Variant.Type.Array)
        {
            var array = new Godot.Collections.Array();
            foreach (var element in value.AsGodotArray()) array.Add(Canonical(element));
            return array;
        }
        if (value.VariantType == Variant.Type.Dictionary)
        {
            var source = value.AsGodotDictionary(); var data = new Godot.Collections.Dictionary();
            foreach (var key in source.Keys.OrderBy(k => k.ToString(), StringComparer.Ordinal)) data[key] = Canonical(source[key]);
            return data;
        }
        return value;
    }
    private static string Snapshot(NpcWorldManager manager) => Json.Stringify(Canonical(manager.get_persistent_npcs()));
    public override async void _Ready()
    {
        try
        {
            var manager = GetNode<NpcWorldManager>("/root/NpcWorldManager"); manager.clear_for_new_game();
            var map = GD.Load<PackedScene>("res://tscn/world/TileWorldMapPresentation.tscn").Instantiate<Control>(); AddChild(map); map.Hide();
            manager.set_tile_world_map(map);
            CloudStateNpcCatalog.activate(manager, GD.Load<WorldNodeMapData>("res://resources/world/yunqu_state_nodes.tres"));
            var refreshes = 0; manager.world_npcs_changed += () => refreshes++;
            var watch = Stopwatch.StartNew();
            GetNode<GameTimeMgr>("/root/GameTimeManager").add_hours(720);
            GD.Print($"NPC_PERF_ONE_MONTH {watch.Elapsed.TotalMilliseconds:0.00}ms refreshes={refreshes}");
            refreshes = 0; watch.Restart();
            var time = GetNode<GameTimeMgr>("/root/GameTimeManager");
            var original = manager.get_persistent_npcs(); var initialMonth = manager.get_last_processed_month();
            var year = time.year; var month = time.month; var day = time.day; var hour = time.hour;
            time.add_hours(720 * 12);
            GD.Print($"NPC_PERF_TWELVE_MONTHS {watch.Elapsed.TotalMilliseconds:0.00}ms refreshes={refreshes} people={manager.npcs.Count}");
            var expected = Snapshot(manager);
            time.restore_calendar(year, month, day, hour, 0);
            manager.restore_persistent_state(original, true, initialMonth, savedPopulationVersion: CloudStateNpcCatalog.PopulationVersion);
            var frames = 0; void Frame() => frames++; GetTree().ProcessFrame += Frame;
            refreshes = 0; watch.Restart();
            var panel = new ActionTransitionPanel(); AddChild(panel);
            await panel.run_work_async("测试结算", async progress =>
            {
                manager.defer_time_settlement(); time.add_hours(720 * 12);
                if (!manager.HasPendingSettlement || !GetTree().Paused || !panel.Visible) throw new Exception("加载未阻断半结算状态");
                await manager.flush_time_settlement_async(progress);
            });
            GetTree().ProcessFrame -= Frame;
            if (expected != Snapshot(manager)) throw new Exception("分帧演算改变了NPC完整字段或行动历史");
            if (frames < 2 || refreshes != 1 || GetTree().Paused || panel.IsPlaying || manager.HasPendingSettlement) throw new Exception("分帧、刷新合并或加载恢复错误");
            GD.Print($"NPC_ASYNC_EQUIVALENCE PASS: 12 months, all exported NPC fields/histories identical, {frames} render frames, {watch.Elapsed.TotalMilliseconds:0.00}ms wall time, max simulation slice={manager.LastSettlementMaxSliceMilliseconds:0.00}ms, refreshes={refreshes}; loading blocks input and restores pause");
            // Exercise the same signal bridge awaited by actual world-map movement.
            var done = false; time.smooth_time_advanced += () => done = true;
            time.advance_time_smooth(720, "world_map_travel");
            while (!done) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (time.IsSmoothAdvanceBusy || panel.IsPlaying || manager.HasPendingSettlement || GetTree().Paused) throw new Exception("地图过月加载未收尾");
            GD.Print("NPC_WORLD_TIME_BRIDGE PASS: world travel awaits reused loading and NPC settlement");
            var finished = false;
            panel.play("长行动回归", "测试", "完成", () => time.add_hours(720), () => finished = true);
            while (!finished) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (panel.IsPlaying || manager.HasPendingSettlement || GetTree().Paused) throw new Exception("原长行动入口未恢复");
            GD.Print("NPC_LONG_ACTION_LOADING PASS: original play entry, deferred settlement, completion callback and pause restoration");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GetTree().Quit();
        }
        catch(Exception ex) { GD.PushError(ex.ToString()); GetTree().Quit(1); }
    }
}
