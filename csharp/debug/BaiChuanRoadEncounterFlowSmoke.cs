using Godot;
using Godot.Collections;
using System;
using System.Reflection;
using System.Threading.Tasks;

public partial class BaiChuanRoadEncounterFlowSmoke : Node
{
    private static object? Invoke(object owner, string name, params object[] args) =>
        typeof(MainScene).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(owner, args);
    private static object? Field(object owner, string name) =>
        typeof(MainScene).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner);
    private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
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
            Check(main.StartupComplete && main.StartupError is null, "startup: " + main.StartupError);
            var stage = typeof(MainScene).GetField("_qingxi_prologue", BindingFlags.Instance | BindingFlags.NonPublic)!;
            stage.SetValue(main, Enum.Parse(stage.FieldType, "Departed"));
            Invoke(main, "open_world");
            await Frames(4);
            var map = main.GetNode<Control>("MapLayer/BaiChuanNodeWorld");
            Check((bool)Invoke(main, "initialize_road_encounter_clock")!, "clock setup");
            var clock = (GodotObject)Field(main, "_roadEncounterClock")!;
            clock.Call("initialize", 512L);
            clock.Set("distance_until_encounter", 0.00001);
            var route = map.Call("get_assigned_route_to_node", "xuandu_tianfu_core").AsGodotArray<string>();
            Check(route.Count > 4, "expected long route");
            var firstFinish = ToSignal(map, "node_travel_finished");
            Check(map.Call("start_assigned_route", route, "xuandu_tianfu_core").AsBool(), "route not started");
            await firstFinish;
            await Frames(5);
            Check((bool)Invoke(main, "has_pending_world_encounter")!, "missing encounter");
            Check(map.Get("encounter_blocked").AsBool() && !map.Call("is_traveling").AsBool(), "encounter did not stop travel");
            var remaining = map.Call("get_remaining_travel_path").AsGodotArray<string>();
            Check(remaining.Count > 1 && remaining[remaining.Count - 1] == route[route.Count - 1], "remaining route lost");
            var view = main.GetNode<ForestWolfEventView>("WorldEncounterLayer/WorldEncounterEvent");
            Check(view.Visible, "event not displayed");
            view.GetNode<Button>("CloseEvent").EmitSignal(Button.SignalName.Pressed);
            Check(view.Visible && map.Get("encounter_blocked").AsBool(), "closing bypassed pending encounter");
            var save = main.capture_battle_world_snapshot();
            Check(save is not null && save.scene_progress["world_road_encounter"].AsGodotDictionary()["pending"].AsGodotDictionary().Count > 0,
                "pending encounter not saved");
            Check(save!.node_world_state["remaining_route"].AsGodotArray().Count > 1, "pending route not saved");
            var encounterId = save.scene_progress["world_road_encounter"].AsGodotDictionary()["pending"].AsGodotDictionary()["id"].AsString();
            var savedNode = map.Call("get_current_node").AsString();
            var savePath = "user://road_encounter_flow_" + Guid.NewGuid().ToString("N") + ".tres";
            Check(ResourceSaver.Save(save, savePath) == Error.Ok, "snapshot write failed");
            var loaded = ResourceLoader.Load<PersistentGameSave>(savePath, cacheMode: ResourceLoader.CacheMode.IgnoreDeep);
            Check(loaded is not null && loaded.scene_progress["world_road_encounter"].AsGodotDictionary()["pending"].AsGodotDictionary()["id"].AsString() == encounterId,
                "pending encounter did not survive file serialization");
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(savePath));
            typeof(MainScene).GetField("_skipExitSave", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(main, true);
            main.QueueFree();
            await Frames(3);
            var saveManager = GetNode<GameSaveManager>("/root/GameSaveManager");
            typeof(GameSaveManager).GetField("_pendingLoad", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(saveManager, loaded);
            main = GD.Load<PackedScene>("res://tscn/scene/MainScene.tscn").Instantiate<MainScene>();
            AddChild(main);
            deadline = Time.GetTicksMsec() + 90000UL;
            while (!main.StartupComplete && Time.GetTicksMsec() < deadline) await Frames();
            Check(main.StartupComplete && main.StartupError is null, "loaded startup: " + main.StartupError);
            await Frames(10);
            map = main.GetNode<Control>("MapLayer/BaiChuanNodeWorld");
            Check(map.Call("get_current_node").AsString() == savedNode && map.Get("encounter_blocked").AsBool(), "loaded map did not preserve pause position");
            Check((bool)Invoke(main, "has_pending_world_encounter")!, "loaded encounter missing");
            Check(map.Call("get_remaining_travel_path").AsGodotArray<string>().Count == remaining.Count, "loaded remaining route differs");
            view = main.GetNode<ForestWolfEventView>("WorldEncounterLayer/WorldEncounterEvent");
            Check(view.Visible, "loaded encounter not displayed");
            var leave = view.FindChild("Choice_leave", true, false) as Button;
            Check(leave is not null, "leave choice missing");
            var secondFinish = ToSignal(map, "node_travel_finished");
            leave!.EmitSignal(Button.SignalName.Pressed);
            clock = (GodotObject)Field(main, "_roadEncounterClock")!;
            clock.Set("distance_until_encounter", 100000.0);
            await secondFinish;
            Check(!(bool)Invoke(main, "has_pending_world_encounter")!, "encounter did not resolve");
            Check(map.Call("get_current_node").AsString() == route[route.Count - 1], "route did not resume to destination");
            GD.Print("BAICHUAN_ROAD_ENCOUNTER_FLOW_OK");
            typeof(MainScene).GetField("_skipExitSave", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(main, true);
            main.QueueFree();
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError("BAICHUAN_ROAD_ENCOUNTER_FLOW_FAILED " + e); GetTree().Quit(1); }
    }
}
