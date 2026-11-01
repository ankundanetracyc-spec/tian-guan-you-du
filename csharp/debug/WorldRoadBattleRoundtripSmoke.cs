using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using D = Godot.Collections.Dictionary;

/// <summary>Real battle scene and return; use a defeated-enemy fixture to finish the fight.</summary>
public partial class WorldRoadBattleRoundtripSmoke : Node
{
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    private async Task Until(Func<bool> predicate, string message, int max = 9000)
    { for (var i=0; i<max; i++) { if (predicate()) return; await Frame(); } throw new Exception(message); }
    public override async void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        try
        {
            var saves = GetNode<GameSaveManager>("/root/GameSaveManager");
            Check(!saves.has_active_slot, "test must not use a player save slot");
            var fixture = new PersistentGameSave
            {
                player_data = new HumanData { entity_name = "旅途遭遇验收", cultivation_realm = "云息三层", data_initialized = true,
                    physique = 10, soul = 10, intelligence = 10, base_physique = 10, base_soul = 10, base_intelligence = 10,
                    current_health = 10000, max_health = 10000 },
                current_node_id = "white_stone_town", year = 2, month = 4, day = 8, hour = 9,
                node_world_state = new D { ["current_node"] = "bc_place_021", ["remaining_route"] = new Godot.Collections.Array<string>() },
                scene_progress = new D { ["story_free_start"] = true, ["qingxi_prologue"] = 3, ["cloud_state_initialized"] = true,
                    ["world_road_encounter"] = new D { ["pending"] = new D { ["id"] = "real_roundtrip", ["kind"] = "wildlife" } } }
            };
            typeof(GameSaveManager).GetField("_pendingLoad", Private)!.SetValue(saves, fixture);
            GetTree().CurrentScene = null;
            Check(GetTree().ChangeSceneToFile("res://tscn/scene/MainScene.tscn") == Error.Ok, "main scene load");
            await Until(() => GetTree().CurrentScene is MainScene { StartupComplete: true }, "main scene startup");
            var main = (MainScene)GetTree().CurrentScene;
            await Until(() => main.GetNodeOrNull<ForestWolfEventView>("WorldEncounterLayer/WorldEncounterEvent") is { Visible: true }, "encounter event missing");
            typeof(MainScene).GetMethod("on_world_encounter_choice", Private)!.Invoke(main, new object[] { "battle" });
            var transition = GetNode<BattleTransitionManager>("/root/BattleTransitionManager");
            Check(transition.has_open_flow_view(), "briefing missing");
            var flow = transition.GetChildren().OfType<BattleFlowView>().Single();
            flow.FindChildren("*", "Button", true, false).OfType<Button>().Single(b => b.Text == "开始战斗").EmitSignal(Button.SignalName.Pressed);
            await Until(() => GetTree().CurrentScene?.SceneFilePath == "res://tscn/battle/BattleScene.tscn" && !transition.is_flow_transition_active(), "battle did not start");
            var manager = GetTree().CurrentScene.GetNode<BattleManager>("BattleMgr");
            Check(manager.battle_running && manager.player_units.Count == 1 && manager.enemy_units.Count == 1, "battle units missing");
            Check(manager.enemy_units[0].Get("entity_name").AsString() == "林地凶狼", "wrong beast");
            ((Creature)manager.enemy_units[0]).current_health = 0;
            typeof(BattleManager).GetMethod("CheckBattleOver", Private)!.Invoke(manager, null);
            await Until(() => (bool)typeof(BattleTransitionManager).GetField("_awaitingSettlement", Private)!.GetValue(transition)! &&
                !transition.is_flow_transition_active() && transition.has_open_flow_view(), "battle settlement missing");
            flow = transition.GetChildren().OfType<BattleFlowView>().Single();
            flow.FindChildren("*", "Button", true, false).OfType<Button>().Single(b => b.Text is "返回" or "返回并领取").EmitSignal(Button.SignalName.Pressed);
            await Until(() => GetTree().CurrentScene is MainScene { StartupComplete: true } && !transition.is_flow_transition_active(), "return to world failed");
            for (var i=0; i<30; i++) await Frame();
            main = (MainScene)GetTree().CurrentScene;
            var state = (D)typeof(MainScene).GetField("_worldEncounterState", Private)!.GetValue(main)!;
            Check(!state.ContainsKey("pending"), "battle result did not clear encounter");
            Check(main.GetNodeOrNull<Control>("MapLayer/BaiChuanNodeWorld") is { Visible: true }, "node world not restored");
            GD.Print("WORLD_ROAD_BATTLE_ROUNDTRIP_OK");
            GetTree().Quit();
        }
        catch (Exception ex) { GD.PushError("WORLD_ROAD_BATTLE_ROUNDTRIP_FAILED: " + ex); GetTree().Quit(1); }
    }
}
