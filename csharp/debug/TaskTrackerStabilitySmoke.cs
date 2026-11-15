using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class TaskTrackerStabilitySmoke : Control
{
    private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    private async Task Frames(int count = 1) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    public override async void _Ready()
    {
        try
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            var hours = GetNode<GameTimeMgr>("/root/GameTimeManager").get_total_game_hours();
            var hud = GD.Load<PackedScene>("res://tscn/panel/MainHUD.tscn").Instantiate<MainHUD>(); AddChild(hud);
            JournalPanel.Entry[] entries = [new("问道", "前往[u]静室[/u]拜见[u]李守拙[/u]。", "来源", "静室", "进行中", Id: "test.one"), new("回峰复命", "回到[u]和光峰[/u]交付[u]苦苔根[/u]。", "来源", "和光峰", "进行中", Id: "test.two")];
            foreach (var size in new[] { new Vector2I(1280, 720), new Vector2I(1920, 1080) })
            {
                GetWindow().ContentScaleSize = size;
                foreach (var count in new[] { 2, 1 })
                {
                    var shown = entries.Take(count).ToArray();
                    hud.set_task_entries(shown, true); await Frames(15);
                    var tracker = hud.GetNode<Control>("TaskTracker"); var toggle = hud.GetNode<Button>("TaskTrackerToggle");
                    var list = tracker.GetNode<VBoxContainer>("Margin/" + tracker.GetNode<MarginContainer>("Margin").GetChild(0).Name);
                    var rows = list.GetChildren().OfType<Button>().ToArray();
                    Check(rows.Length == count, "Tracker count changed");
                    var ids = rows.Select(row => row.GetInstanceId()).ToArray();
                    var rect = tracker.GetRect();
                    Check(rect.Size.Y < 240, $"Short tasks produced an oversized panel: {rect.Size.Y}");
                    Check(Math.Abs(rect.Size.Y - tracker.GetCombinedMinimumSize().Y) < 1, "Tracker retained a temporary minimum height");
                    for (var frame = 0; frame < 50; frame++)
                    {
                        // Fresh record instances with unrelated metadata still display the same text.
                        hud.set_task_entries(shown.Select(entry => entry with { Time = frame.ToString(), State = "刷新中" }), true);
                        await Frames();
                        Check(rect.IsEqualApprox(tracker.GetRect()), "Unchanged task refresh moved/resized tracker");
                        Check(ids.SequenceEqual(list.GetChildren().OfType<Button>().Select(row => row.GetInstanceId())), "Unchanged task refresh rebuilt rows");
                        Check(Math.Abs(toggle.GetGlobalRect().Position.Y - tracker.GetGlobalRect().Position.Y) < 1 && Math.Abs(toggle.Size.Y - tracker.Size.Y) < 1, "Triangle strip is not full tracker height");
                    }
                    toggle.EmitSignal(BaseButton.SignalName.Pressed); await Frames(8);
                    Check(tracker.GetRect().Position.X >= hud.Size.X && Math.Abs(toggle.Size.Y - tracker.Size.Y) < 1, "Collapsed tracker bounds changed");
                    toggle.EmitSignal(BaseButton.SignalName.Pressed); await Frames(8);
                    Check(rect.IsEqualApprox(tracker.GetRect()), "Expand failed to restore stable bounds");
                    hud.set_task_entries(shown, false); await Frames(); Check(!tracker.Visible && !toggle.Visible, "Hidden tracker left triangle visible");
                    hud.set_task_entries(shown, true); await Frames(4); Check(rect.IsEqualApprox(tracker.GetRect()) && toggle.Visible, "Restore changed task layout");
                    GD.Print($"TASK_TRACKER_STABILITY canvas={size} tasks={count} height={tracker.Size.Y} toggle_height={toggle.Size.Y} unchanged_frames=50 PASS");
                }
                for (var refresh = 0; refresh < 8; refresh++)
                {
                    hud.set_task_entries([], false);
                    hud.set_task_entries([
                        new("同门引路", $"前往[u]玄览峰[/u]，拜见[u]{(refresh % 2 == 0 ? "赵川" : "李守拙")}[/u]", "", "", "进行中", Id: "refresh.one"),
                        new("继续精进", "继续修行，查看当前境界目标", "", "", "进行中", Id: "refresh.two")
                    ], true);
                    await Frames(15);
                    var tracker = hud.GetNode<Control>("TaskTracker");
                    Check(tracker.Size.Y < 240, $"Changed tasks did not shrink to content: {tracker.Size.Y}");
                    Check(Math.Abs(tracker.Size.Y - tracker.GetCombinedMinimumSize().Y) < 1, "Changed tasks retained a temporary height");
                    hud.Hide(); await Frames(); hud.Show(); await Frames(4);
                    var toggle = hud.GetNode<Button>("TaskTrackerToggle");
                    Check(Math.Abs(toggle.Size.Y - tracker.Size.Y) < 1 && Math.Abs(toggle.GlobalPosition.Y - tracker.GlobalPosition.Y) < 1,
                        "Restored HUD left toggle bounds stale");
                }
                GD.Print($"TASK_TRACKER_REFRESH canvas={size} changed_and_restored=8 PASS");
            }
            hud.set_task_entries([], true); await Frames(4);
            Check(!hud.GetNode<Control>("TaskTracker").Visible && !hud.GetNode<Button>("TaskTrackerToggle").Visible, "Empty tracker remained visible");
            Check(hours == GetNode<GameTimeMgr>("/root/GameTimeManager").get_total_game_hours(), "Tracker advanced time");
            GD.Print("TASK_TRACKER_STABILITY PASS"); GetTree().Quit();
        }
        catch (Exception exception) { GD.PushError(exception.ToString()); GetTree().Quit(1); }
    }
}
