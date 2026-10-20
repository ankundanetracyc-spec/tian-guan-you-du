using Godot;
using System;
using System.Diagnostics;

/// <summary>Isolated synthetic measurement; never reads or writes a player's save slot.</summary>
public partial class WorldSavePerformanceSmoke : Node
{
    public override async void _Ready()
    {
        try
        {
            var directory = "user://world_save_performance_smoke_" + Guid.NewGuid().ToString("N");
            var error = DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(directory));
            if (error != Error.Ok) throw new InvalidOperationException("Cannot create diagnostic directory: " + error);
            var manager = GetNode<NpcWorldManager>("/root/NpcWorldManager");
            manager.clear_for_new_game();
            for (var index = 0; index < 580; index++)
                manager.npcs.Add(new WorldNpcData
                {
                    npc_id = "save_perf_" + index, npc_name = "性能样本" + index,
                    origin_node_id = "test_location", current_node_id = "test_location", is_alive = true,
                });

            var priorMonths = 0;
            foreach (var months in new[] { 1, 36, 120 })
            {
                foreach (var npc in manager.npcs)
                    for (var month = priorMonths; month < months; month++)
                        npc.record(new NpcActionRecord
                        {
                            month_index = month, date_label = $"{month / 12 + 1}年{month % 12 + 1}月",
                            action_id = "work", action_name = "营生", node_id = "test_location",
                            reason = "依照既有营生行动结算月度收入。", outcome = "灵石增加 2 枚。", resource_delta = 2,
                        });
                priorMonths = months;
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                var watch = Stopwatch.StartNew();
                var snapshot = new PersistentGameSave { player_data = new HumanData(), year = months / 12 + 1 };
                snapshot.world_npcs = manager.get_persistent_npcs();
                watch.Stop();
                var copyMs = watch.Elapsed.TotalMilliseconds;
                if (snapshot.world_npcs.Count != 580 || snapshot.world_npcs[0].action_history.Count != months)
                    throw new InvalidOperationException("Snapshot did not preserve full history.");
                var path = directory + "/months_" + months + ".tres";
                watch.Restart();
                error = ResourceSaver.Save(snapshot, path);
                watch.Stop();
                if (error != Error.Ok) throw new InvalidOperationException("Diagnostic save failed: " + error);
                using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
                var bytes = file.GetLength();
                GD.Print($"WORLD_SAVE_PERF months={months} npcs=580 records={580 * months} copy_ms={copyMs:F2} write_ms={watch.Elapsed.TotalMilliseconds:F2} bytes={bytes}");
                file.Close();
                DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
                snapshot.world_npcs.Clear();
                snapshot.Dispose();
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            manager.clear_for_new_game();
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(directory));
            GD.Print("WORLD_SAVE_PERF PASS: full histories measured; isolated diagnostic files removed.");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            GetTree().Quit(1);
        }
    }
}
