using Godot;
using System;
using System.Linq;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using GDictionary = Godot.Collections.Dictionary;

/// <summary>Isolated scene: preserves historical records and verifies long-route UI reuse.</summary>
public partial class DungeonRefreshPerformanceSmoke : Control
{
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static object? Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, args);
    private static T Read<T>(object target, string field) => (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;
    private async Task Frames(int count = 2) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private static ulong[] DescendantIds(Node node) => node.GetChildren().SelectMany(child => DescendantIds(child).Prepend(child.GetInstanceId())).ToArray();
    public override async void _Ready()
    {
        try
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            GetNode<NpcWorldManager>("/root/NpcWorldManager").set_simulation_enabled(false);
            var hour = GetNode<GameTimeMgr>("/root/GameTimeManager").get_total_game_hours();
            EastWoodsDungeonRules.generate_layout("refresh-performance-20261024");
            var nodes = EastWoodsActions.OrdinaryBeastPoints().Select(point => point.Node).Distinct().ToArray();
            var records = new Godot.Collections.Array();
            for (var i = 0; i < 5000; i++) records.Add(new GDictionary {
                ["node"] = nodes[i % nodes.Length], ["action_id"] = "past-" + i,
                ["encounter_id"] = "past-identity-" + i, ["status"] = "defeated", ["resolved_hour"] = i
            });
            for (var i = 0; i < nodes.Length; i++) records.Add(new GDictionary {
                ["node"] = nodes[i], ["action_id"] = "live-" + i, ["encounter_id"] = "live-identity-" + i, ["status"] = "active"
            });
            records.Add(new GDictionary { ["node"] = nodes[0], ["action_id"] = "done", ["encounter_id"] = "done-identity", ["status"] = "active" });
            records.Add(new GDictionary { ["node"] = nodes[0], ["action_id"] = "", ["encounter_id"] = "bad", ["status"] = "active" });
            var actions = new GDictionary { [nodes[0] + ":done"] = 1,
                ["__east_woods_ecology"] = new GDictionary { ["ordinary_beast_encounters"] = records } };
            var saved = Json.Stringify(actions);
            var clock = Stopwatch.StartNew();
            var expected = nodes.ToDictionary(node => node, node => EastWoodsEcology.AvailableRegeneratedBeasts(node, actions));
            var repeatedMs = clock.Elapsed.TotalMilliseconds;
            clock.Restart();
            var indexed = EastWoodsEcology.IndexAvailableRegeneratedBeasts(actions);
            var indexedMs = clock.Elapsed.TotalMilliseconds;
            foreach (var node in nodes)
                Check(expected[node].Select(a => (a.Id, a.EncounterIdentity)).SequenceEqual(indexed.GetValueOrDefault(node, []).Select(a => (a.Id, a.EncounterIdentity))), "Historical encounter index changed eligibility/order");
            Check(Json.Stringify(actions) == saved, "History/index query mutated saved state");
            GD.Print($"DUNGEON_HISTORY records={records.Count} nodes={nodes.Length} repeated_ms={repeatedMs:F2} indexed_ms={indexedMs:F2} preserved=PASS");

            var panel = new EastWoodsDungeonPanel(); AddChild(panel); panel.show_dungeon(); await Frames(5);
            var steps = 0;
            void Walk(string node, System.Collections.Generic.HashSet<string> visited)
            {
                visited.Add(node);
                foreach (var next in EastWoodsDungeonRules.get_neighbors(node))
                {
                    if (visited.Contains(next)) continue;
                    Check((bool)Call(panel, "move_one_node", next)!, "Legal route edge rejected"); Call(panel, "finish_pawn_step"); steps++;
                    Walk(next, visited);
                    Check((bool)Call(panel, "move_one_node", node)!, "Return route edge rejected"); Call(panel, "finish_pawn_step"); steps++;
                }
            }
            Walk("forest_edge", []); await Frames(5);
            var map = panel.GetNode<Control>("MapViewport/OrthogonalNodeMap");
            var ids = DescendantIds(map);
            var actionRows = Read<VBoxContainer>(panel, "_actionRows");
            var actionIds = DescendantIds(actionRows);
            var stateBefore = Json.Stringify(panel.get_route_state());
            clock.Restart();
            for (var i = 0; i < 100; i++) Call(panel, "refresh");
            var refreshMs = clock.Elapsed.TotalMilliseconds;
            await Frames(5);
            Check(ids.SequenceEqual(DescendantIds(map)), "Unchanged refresh rebuilt map/pawns/connections");
            Check(actionIds.SequenceEqual(DescendantIds(actionRows)), "Unchanged refresh rebuilt action rows");
            Check(stateBefore == Json.Stringify(panel.get_route_state()), "UI refresh changed route/discovery/actions");
            Check(GetNode<GameTimeMgr>("/root/GameTimeManager").get_total_game_hours() == hour, "UI test advanced game time");
            Check(Read<object?>(panel, "_refreshBeastActions") is null, "Refresh index escaped snapshot lifetime");
            GD.Print($"DUNGEON_REUSE walked_edges={steps} nodes={EastWoodsDungeonRules.get_nodes().Count} ui_objects={ids.Length} refresh100_ms={refreshMs:F2} stable_ids=PASS");
            panel.QueueFree(); await Frames(4);
            GD.Print("DUNGEON_REFRESH_PERFORMANCE PASS"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError("DUNGEON_REFRESH_PERFORMANCE FAIL " + error); GetTree().Quit(1); }
    }
}
