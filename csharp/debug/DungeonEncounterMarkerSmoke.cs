using Godot;
using System;
using System.Reflection;
using System.Linq;
using System.Threading.Tasks;

public partial class DungeonEncounterMarkerSmoke : Control
{
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private async Task Frames(int count = 4) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private static void Refresh(EastWoodsDungeonPanel panel) => typeof(EastWoodsDungeonPanel).GetMethod("refresh", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(panel, null);
    public override async void _Ready()
    {
        try
        {
            GetNode<NpcWorldManager>("/root/NpcWorldManager").set_simulation_enabled(false);
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            EastWoodsDungeonRules.generate_layout("encounter-markers-20261025");
            var hours = GetNode<GameTimeMgr>("/root/GameTimeManager").get_total_game_hours();
            var panel = new EastWoodsDungeonPanel(); AddChild(panel); panel.show_dungeon();
            var state = panel.get_route_state(); state["current"] = "root_hollow";
            panel.restore_route_state(state); await Frames();
            var wolf = panel.GetNode<Control>("MapViewport/OrthogonalNodeMap/Node_root_hollow/BeastEncounterMarker");
            Check(wolf.IsVisibleInTree(), "Available wolf marker missing at current cell");
            Check(wolf.MouseFilter == MouseFilterEnum.Ignore, "Wolf marker intercepts map movement");
            var id = wolf.GetInstanceId();
            var before = Json.Stringify(panel.get_route_state());
            for (var i = 0; i < 50; i++) Refresh(panel);
            Check(id == panel.GetNode<Control>("MapViewport/OrthogonalNodeMap/Node_root_hollow/BeastEncounterMarker").GetInstanceId(), "Marker rebuilt on unchanged refresh");
            Check(before == Json.Stringify(panel.get_route_state()), "Marker refresh changed saved actions");
            var offered = 0;
            panel.ActionRequested += (node, action) =>
            {
                Check(node == "root_hollow" && action == "wolf", "Arrival requested a wrong encounter");
                Check(!panel.is_route_traveling(), "Arrival kept old continuous route running");
                offered++;
            };
            state = panel.get_route_state();
            state["current"] = EastWoodsDungeonRules.get_neighbors("root_hollow")[0];
            panel.restore_route_state(state); await Frames();
            Check(offered == 0, "Restoring position started combat");
            Check(panel.begin_assigned_route("root_hollow"), "Could not approach wolf");
            for (var frame = 0; frame < 180 && offered == 0; frame++) await Frames(1);
            Check(offered == 1, "Walking to live wolf did not request battle briefing");
            await Frames(20); Refresh(panel);
            Check(offered == 1, "Cancelling/viewing at same cell repeatedly offered battle");
            wolf = panel.GetNode<Control>("MapViewport/OrthogonalNodeMap/Node_root_hollow/BeastEncounterMarker");
            panel.complete_action("root_hollow", "wolf"); Refresh(panel);
            Check(!wolf.Visible, "Defeated wolf still shown");
            state = panel.get_route_state();
            state["actions"].AsGodotDictionary().Remove("root_hollow:wolf");
            var wolfPosition = EastWoodsDungeonRules.get_node("root_hollow")!.GridPosition;
            state["current"] = EastWoodsDungeonRules.get_nodes().First(node => (node.GridPosition - wolfPosition).LengthSquared() > 16).Id;
            panel.restore_route_state(state); await Frames();
            Check(!panel.is_node_visible("root_hollow"), "Test needs wolf outside current vision");
            Check(panel.GetNode<Control>("MapViewport/OrthogonalNodeMap/Node_root_hollow").GetNodeOrNull<Control>("BeastEncounterMarker") is null, "Hidden wolf marker reveals remembered encounter");
            Check(GetNode<GameTimeMgr>("/root/GameTimeManager").get_total_game_hours() == hours, "Marker viewing advances time");
            panel.QueueFree(); await Frames();
            GD.Print("DUNGEON_ENCOUNTER_MARKER PASS: visibility, victory, input passthrough, reuse, arrival briefing once, save/time unchanged");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
