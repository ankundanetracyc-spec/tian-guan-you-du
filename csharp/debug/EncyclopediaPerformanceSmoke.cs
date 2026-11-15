using Godot;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using System.Linq;

public partial class EncyclopediaPerformanceSmoke : Node
{
    private async Task Frames(int n = 4) { for (var i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private static object? Read(object value, string field) => value.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(value);
    public override async void _Ready()
    {
        try
        {
            var watch = Stopwatch.StartNew();
            var panel = new EncyclopediaPanel(); AddChild(panel);
            GD.Print($"ENCYCLOPEDIA_PERF build_ms={watch.Elapsed.TotalMilliseconds:F2}");
            foreach (var section in new[] { "Statuses", "Items", "Beasts", "Statuses", "Items", "Beasts" })
            {
                watch.Restart();
                if (section == "Statuses") panel.show_panel();
                else if (section == "Beasts") panel.show_beasts();
                else panel.FindChild("TabItems", true, false)!.EmitSignal(BaseButton.SignalName.Pressed);
                var immediateMs = watch.Elapsed.TotalMilliseconds;
                var frames = 0;
                while (panel.IsCatalogLoading && frames++ < 3000) await Frames(1);
                if (panel.IsCatalogLoading) throw new Exception("百科加载超时");
                GD.Print($"ENCYCLOPEDIA_PERF section={section} immediate_ms={immediateMs:F2} ready_ms={watch.Elapsed.TotalMilliseconds:F2} loading_frames={frames} max_slice_ms={panel.LastCatalogMaxSliceMilliseconds:F2} rows={((VBoxContainer)Read(panel, "_entryList")!).GetChildren().OfType<Button>().Count(b => b.Visible)}");
            }
            var search = (LineEdit)Read(panel, "_search")!;
            watch.Restart(); search.Text = "狼";
            GD.Print($"ENCYCLOPEDIA_PERF search_ms={watch.Elapsed.TotalMilliseconds:F2}");
            await Frames();
            panel.hide_panel(); await Frames(40);
            if (panel.Visible) throw new Exception("百科关闭失败");
            GD.Print("ENCYCLOPEDIA_PERFORMANCE PASS");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
