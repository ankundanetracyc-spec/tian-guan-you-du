using Godot;
using Godot.Collections;
using System;
using System.Reflection;
using System.Linq;
using System.Threading.Tasks;

public partial class WorldRoadBattlePackageSmoke : Node
{
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    public override void _Ready() => CallDeferred(nameof(Run));
    private async void Run()
    {
        try
        {
            var main = GD.Load<PackedScene>("res://tscn/scene/MainScene.tscn").Instantiate<MainScene>();
            AddChild(main);
            var deadline = Time.GetTicksMsec() + 90000UL;
            while (!main.StartupComplete && Time.GetTicksMsec() < deadline) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(main.StartupComplete && main.StartupError is null, "main startup");
            var stateField = typeof(MainScene).GetField("_worldEncounterState", Private)!;
            var choose = typeof(MainScene).GetMethod("on_world_encounter_choice", Private)!;
            var show = typeof(MainScene).GetMethod("show_world_encounter_event", Private)!;
            typeof(MainScene).GetMethod("connect_road_encounter_briefing", Private)!.Invoke(main, null);
            var transition = GetNode<BattleTransitionManager>("/root/BattleTransitionManager");
            var flowField = typeof(BattleTransitionManager).GetField("_flowView", Private)!;
            var eventField = typeof(MainScene).GetField("_worldEncounterView", Private)!;
            foreach (var kind in new[] { "beast", "bandit" })
            {
                var state = new Dictionary { ["pending"] = new Dictionary { ["id"] = "test_" + kind, ["kind"] = kind == "beast" ? "wildlife" : "bandit" } };
                stateField.SetValue(main, state);
                show.Invoke(main, null);
                choose.Invoke(main, new object[] { "battle" });
                Check(transition.has_open_flow_view(), kind + " briefing missing");
                var snapshot = state["enemy_snapshot"].AsGodotObject() as BattleEntityData;
                Check(snapshot is not null && snapshot.current_health > 0 && snapshot.faction == (int)WorldEntity.Faction.ENEMY,
                    kind + " invalid enemy snapshot");
                if (kind == "bandit") Check(state["enemy_human"].AsGodotObject() is HumanData, "bandit data not stored");
                if (kind == "beast") Check(snapshot!.entity_name == "林地凶狼", "wrong beast template");
                var originalUid = snapshot!.uid;
                var flow = (Node)flowField.GetValue(transition)!;
                var cancel = flow.FindChildren("*", "Button", true, false).OfType<Button>().First(b => b.Text == "返回");
                cancel.EmitSignal(Button.SignalName.Pressed);
                Check(eventField.GetValue(main) is Control { Visible: true }, kind + " cancellation did not restore encounter");
                choose.Invoke(main, new object[] { "battle" });
                Check(transition.has_open_flow_view(), kind + " retry rejected");
                Check(((BattleEntityData)state["enemy_snapshot"].AsGodotObject()!).uid == originalUid, kind + " retry changed enemy");
                flow = (Node)flowField.GetValue(transition)!;
                cancel = flow.FindChildren("*", "Button", true, false).OfType<Button>().First(b => b.Text == "返回");
                cancel.EmitSignal(Button.SignalName.Pressed);
                var result = typeof(MainScene).GetMethod("handle_world_encounter_battle_return", Private)!;
                var context = new Dictionary { ["kind"] = "world_road_encounter", ["encounter_id"] = "test_" + kind };
                Check((bool)result.Invoke(main, new object[] { new Dictionary { ["player_victory"] = true }, context })!, kind + " result not handled");
                Check(!state.ContainsKey("pending"), kind + " battle result did not resolve encounter");
            }
            GD.Print("WORLD_ROAD_BATTLE_PACKAGE_OK");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError("WORLD_ROAD_BATTLE_PACKAGE_FAILED: " + e); GetTree().Quit(1); }
    }
}
