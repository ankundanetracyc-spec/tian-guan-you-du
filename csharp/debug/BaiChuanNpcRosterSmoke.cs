using Godot;
using System;
using System.Reflection;
using System.Linq;
using System.Threading.Tasks;

public partial class BaiChuanNpcRosterSmoke : Node
{
    private static object? Invoke(object owner, string name, params object[] args) =>
        typeof(MainScene).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(owner, args);
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private async Task Frames(int count = 1) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    public override void _Ready() => CallDeferred(nameof(Run));
    private async void Run()
    {
        try
        {
            var main = GD.Load<PackedScene>("res://tscn/scene/MainScene.tscn").Instantiate<MainScene>();
            AddChild(main);
            var deadline = Time.GetTicksMsec() + 90000UL;
            while (!main.StartupComplete && Time.GetTicksMsec() < deadline) await Frames();
            Check(main.StartupComplete && main.StartupError is null, "startup");
            var stage = typeof(MainScene).GetField("_qingxi_prologue", BindingFlags.Instance | BindingFlags.NonPublic)!;
            stage.SetValue(main, Enum.Parse(stage.FieldType, "Departed"));
            Invoke(main, "open_world");
            await Frames(5);
            var map = main.GetNode<Control>("MapLayer/BaiChuanNodeWorld");
            var roster = GetTree().GetFirstNodeInGroup("cell_character_list") as CellCharacterList;
            Check(roster is { Visible: true }, "roster not shown");
            var field = typeof(CellCharacterList).GetField("_roster_context", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var current = map.Call("get_current_node").AsString();
            var currentLocation = map.Call("get_runtime_id", current).AsString();
            var initialContext = string.IsNullOrEmpty(currentLocation) ? $"world-cell:{current}" : $"world:{currentLocation}";
            Check((string)field.GetValue(roster)! == initialContext, "wrong initial node roster: " + field.GetValue(roster));
            var manager = GetNode<NpcWorldManager>("/root/NpcWorldManager");
            var badges = map.GetNode<Node>("FixedScaleWorld/Places/PopulationBadges");
            var counts = manager.get_node_world_population_counts();
            var visibleCounts = badges.Get("counts").AsGodotDictionary();
            Check(counts.Count > 0 && counts.Count == visibleCounts.Count, "population badges did not initialize");
            var nodeId = counts.Keys.First().AsString();
            Check(visibleCounts[nodeId].AsInt32() == counts[nodeId].AsInt32(), "badge count differs from world population");
            var npc = manager.npcs.First(n => n.is_alive && n.has_world_grid_position && string.IsNullOrEmpty(n.dungeon_id) &&
                manager.node_world_node_id_for_cell(n.current_grid_cell) == nodeId);
            var oldCount = counts[nodeId].AsInt32();
            npc.is_alive = false;
            manager.EmitSignal(NpcWorldManager.SignalName.world_npcs_changed);
            visibleCounts = badges.Get("counts").AsGodotDictionary();
            Check(visibleCounts.GetValueOrDefault(nodeId, 0).AsInt32() == oldCount - 1, "badge did not update when population changed");
            npc.is_alive = true;
            manager.EmitSignal(NpcWorldManager.SignalName.world_npcs_changed);
            Check(roster!.Visible, "NPC update hides roster");
            var next = map.Call("get_current_neighbours").AsGodotArray<string>()[0];
            map.Call("_select_transit", next);
            var finished = ToSignal(map, "node_travel_finished");
            map.Call("_travel_selected");
            Check(!roster.Visible, "roster not suspended during travel");
            await finished;
            await Frames(2);
            Check(roster.Visible, "roster not restored on arrival");
            var actual = (string)field.GetValue(roster)!;
            var location = map.Call("get_runtime_id", next).AsString();
            Check(actual == (string.IsNullOrEmpty(location) ? $"world-cell:{next}" : $"world:{location}"), "arrival roster context");
            GD.Print("BAICHUAN_NPC_ROSTER_OK");
            main.QueueFree();
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError("BAICHUAN_NPC_ROSTER_FAILED " + e); GetTree().Quit(1); }
    }
}
