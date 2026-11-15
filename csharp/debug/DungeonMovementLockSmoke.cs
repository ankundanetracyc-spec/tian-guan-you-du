using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

public partial class DungeonMovementLockSmoke : Control
{
    private static object? Call(object o, string m, params object[] a) => o.GetType().GetMethod(m, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(o, a);
    private static T Read<T>(object o, string f) => (T)o.GetType().GetField(f, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(o)!;
    private static void Check(bool ok, string m) { if (!ok) throw new InvalidOperationException(m); }
    private async Task Frames(int n = 3) { for(var i=0;i<n;i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private async Task Settle(EastWoodsDungeonPanel panel)
    {
        for(var i=0;i<400 && (panel.is_route_traveling() || Read<Control?>(panel,"_travelPawn") is not null);i++)
            await ToSignal(GetTree().CreateTimer(.01), SceneTreeTimer.SignalName.Timeout);
        Check(!panel.is_route_traveling() && Read<Control?>(panel,"_travelPawn") is null, "Movement remained locked after real tween");
    }
    public override async void _Ready()
    {
        try
        {
            GetNode<NpcWorldManager>("/root/NpcWorldManager").set_simulation_enabled(false);
            EastWoodsDungeonRules.generate_layout("movement-lock-20261025");
            var panel = new EastWoodsDungeonPanel(); AddChild(panel); panel.show_dungeon(); await Frames();
            var initial = panel.get_current_route_node_id();
            var first = EastWoodsDungeonRules.get_neighbors(initial)[0];
            var second = EastWoodsDungeonRules.get_neighbors(first).FirstOrDefault(id => id != initial) ?? first;
            Check(panel.begin_assigned_route(second), "Could not start real route"); await Settle(panel);
            Check(panel.get_current_route_node_id() == second, "Continuous route stopped early");
            panel.close_action_details(); // Arrival at a content point now waits for the player's choice.
            Check(panel.begin_assigned_route(initial), "Could not route back"); await Settle(panel);
            var target = EastWoodsDungeonRules.get_neighbors(initial)[0];
            var direction = EastWoodsDungeonRules.get_node(target)!.GridPosition - EastWoodsDungeonRules.get_node(initial)!.GridPosition;
            var key = direction.X > 0 ? Key.Right : direction.X < 0 ? Key.Left : direction.Y > 0 ? Key.Down : Key.Up;
            var button = panel.GetNode<Button>("ExplorationNotes"); button.GrabFocus();
            Input.ParseInputEvent(new InputEventKey { Keycode=key, PhysicalKeycode=key, Pressed=true }); await Frames();
            Input.ParseInputEvent(new InputEventKey { Keycode=key, PhysicalKeycode=key, Pressed=false });
            Check(panel.get_current_route_node_id() == target, "Focused map toolbar swallowed arrow movement"); await Settle(panel);
            var saved = panel.get_route_state();
            var resetNode = panel.get_current_route_node_id();
            var resetNeighbor = EastWoodsDungeonRules.get_neighbors(resetNode)[0];
            var resetTarget = EastWoodsDungeonRules.get_neighbors(resetNeighbor).FirstOrDefault(id => id != resetNode) ?? resetNeighbor;
            Check(panel.begin_assigned_route(resetTarget), "Could not start route for restore");
            panel.restore_route_state(saved);
            await Settle(panel);
            Check(panel.get_current_route_node_id() == resetNode, "Old route continued after restoring saved position");
            var repeatTarget = EastWoodsDungeonRules.get_neighbors(resetNode)[0];
            var repeatDirection = EastWoodsDungeonRules.get_node(repeatTarget)!.GridPosition - EastWoodsDungeonRules.get_node(resetNode)!.GridPosition;
            var repeatKey = repeatDirection.X > 0 ? Key.Right : repeatDirection.X < 0 ? Key.Left : repeatDirection.Y > 0 ? Key.Down : Key.Up;
            Input.ParseInputEvent(new InputEventKey { Keycode=repeatKey, PhysicalKeycode=repeatKey, Pressed=true, Echo=true }); await Frames();
            Check(panel.get_current_route_node_id() == repeatTarget, "Held direction key echo was discarded after previous step finished");
            Input.ParseInputEvent(new InputEventKey { Keycode=repeatKey, PhysicalKeycode=repeatKey, Pressed=true, Echo=true }); await Frames();
            Check(panel.get_current_route_node_id() == repeatTarget, "Repeat overlapped an unfinished step"); await Settle(panel);
            var reverseKey = repeatKey == Key.Right ? Key.Left : repeatKey == Key.Left ? Key.Right : repeatKey == Key.Down ? Key.Up : Key.Down;
            panel.show_action_result("查看", "移动锁验证");
            Input.ParseInputEvent(new InputEventKey { Keycode=reverseKey, PhysicalKeycode=reverseKey, Pressed=true, Echo=true }); await Frames();
            Check(panel.get_current_route_node_id() == repeatTarget, "Repeat moved behind action modal"); panel.close_action_details();
            panel.set_npc_encounter_busy(true);
            Input.ParseInputEvent(new InputEventKey { Keycode=reverseKey, PhysicalKeycode=reverseKey, Pressed=true, Echo=true }); await Frames();
            Check(panel.get_current_route_node_id() == repeatTarget, "Repeat bypassed encounter busy lock"); panel.set_npc_encounter_busy(false);
            Input.ParseInputEvent(new InputEventKey { Keycode=reverseKey, PhysicalKeycode=reverseKey, Pressed=true, Echo=true }); await Frames();
            Check(panel.get_current_route_node_id() == resetNode, "Movement failed to resume after modal/encounter"); await Settle(panel);
            Input.ParseInputEvent(new InputEventKey { Keycode=reverseKey, PhysicalKeycode=reverseKey, Pressed=false });
            GD.Print("DUNGEON_MOVEMENT_LOCK PASS real_route/focused_arrow/held_repeat/no_overlap/modal/encounter_resume"); GetTree().Quit();
        }
        catch(Exception e) { GD.PushError("DUNGEON_MOVEMENT_LOCK FAIL " + e); GetTree().Quit(1); }
    }
}
