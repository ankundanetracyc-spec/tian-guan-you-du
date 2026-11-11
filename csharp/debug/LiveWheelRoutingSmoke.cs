using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

public partial class LiveWheelRoutingSmoke : Node
{
    private static object? Read(object value, string field) => value.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(value);
    private static object? Call(object value, string method, params object[] args) => value.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(value, args);
    private async Task Frames(int n = 6) { for (var i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private void Wheel(Vector2 point)
    {
        GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
        GD.Print("WHEEL_TARGET " + GetViewport().GuiGetHoveredControl()?.GetPath());
        foreach (var pressed in new[] { true, false }) GetViewport().PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.WheelDown, Pressed = pressed }, true);
    }
    public override async void _Ready()
    {
        try
        {
            GetWindow().ContentScaleSize = new(1920, 1080);
            var saves = GetNode<GameSaveManager>("/root/GameSaveManager");
            var fixture = new PersistentGameSave { year = 5, player_data = new HumanData { entity_name = "滚轮验收", cultivation_realm = "云息三层" }, current_node_id = "yuxu_xianzong_core", world_cell = new(34, 166), npc_population_version = 1,
                scene_progress = new() { ["story_free_start"] = true, ["cloud_state_initialized"] = true } };
            for (var i = 0; i < 25; i++) fixture.world_npcs.Add(new WorldNpcData { npc_id = "wheel_" + i, npc_name = "同修" + i, current_node_id = "yuxu_xianzong_core" });
            typeof(GameSaveManager).GetField("_pendingLoad", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(saves, fixture);
            GetTree().CurrentScene = null;
            GetTree().ChangeSceneToFile("res://tscn/scene/MainScene.tscn");
            await Frames(20);
            var main = (MainScene)GetTree().CurrentScene;
            Call(main, "open_world");
            await Frames(10);
            var world = (Control)Read(main, "tile_world_map")!;
            if (main.GetNodeOrNull("MapLayer/NodeWorldMap") is not null || main.FindChildren("WorldMapViewport", "", true, false).Count != 0)
                throw new Exception("旧地图层级仍留在主场景");
            if (main.GetNodeOrNull("Weather") is not null || main.FindChildren("DesktopBg", "", true, false).Count != 0)
                throw new Exception("废弃天气或背景层级仍留在主场景");
            if ((Read(main, "_world_node_data") as WorldNodeMapData)?.get_node("east_woods") is null)
                throw new Exception("删除旧地图影响了现行地点目录");
            if (main.GetNodeOrNull("CombatTrigger") is null || main.GetNodeOrNull("TestingOverlay/PrologueSkipButton") is null)
                throw new Exception("仍在使用的战斗或序章入口被误删");
            var before = world.Get("_user_zoom").AsSingle();
            Wheel(new Vector2(1100, 650));
            await Frames();
            GD.Print($"LIVE_WORLD_ZOOM before={before} after={world.Get("_user_zoom")}");
            var roster = GetTree().GetFirstNodeInGroup("cell_character_list") as CellCharacterList;
            roster!.show_world_npcs(fixture.world_npcs, "world:wheel");
            await Frames(20);
            var scroll = roster.GetNode<ScrollContainer>("ListPanel/CharacterScroll");
            Wheel(scroll.GetGlobalTransformWithCanvas() * (scroll.Size * .5f));
            await Frames();
            GD.Print($"LIVE_ROSTER scroll={scroll.ScrollVertical} rect={scroll.GetGlobalRect()} range={scroll.GetVScrollBar().MaxValue - scroll.GetVScrollBar().Page}");
            if (Mathf.IsEqualApprox(before, world.Get("_user_zoom").AsSingle())) throw new Exception("完整主场景地图未缩放");
            if (scroll.ScrollVertical <= 0) throw new Exception("完整主场景名单未滚动");
            GD.Print("LIVE_WHEEL_ROUTING PASS");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
