using Godot;
using System;

/// <summary>Checks that an assigned case route starts automatically, advances world time, and can pause at a route step.</summary>
public partial class YuxuCaseTransportMapSmoke : Node
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    public override async void _Ready()
    {
        try
        {
            var packed = ResourceLoader.Load<PackedScene>("res://tscn/world/TileWorldMapPresentation.tscn");
            Check(packed is not null, "大世界地图场景不能载入");
            var map = packed!.Instantiate<Control>();
            AddChild(map);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            const string destination = "yuxu_xianzong_core";
            var route = map.Call("get_assigned_route_to_node", destination).AsGodotArray<Vector2I>();
            Check(route.Count > 1 && route[0] == map.Call("get_player_cell").AsVector2I() &&
                map.Call("get_location_node_id_at_cell", route[^1]).AsString() == destination,
                "系统没有从当前位置指派到执中峰地图锚点的连续路线");
            var expectedHours = map.Call("get_path_travel_hours", route).AsSingle();
            Check(expectedHours > 0f, "自动路线没有按现有遁速和地形代价计算时间");

            var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
            var beforeHours = time?.get_total_game_hours() ?? 0;
            Check(map.Call("start_assigned_route", route, destination).AsBool(), "押送自动路线未开始移动");
            await ToSignal(map, "player_cell_changed");
            map.Call("interrupt_assigned_route");
            await ToSignal(map, "player_travel_finished");
            Check(!map.Call("is_traveling").AsBool(), "自动路线在暂停后仍处于移动状态");
            Check(map.Call("get_player_cell").AsVector2I() != route[0], "自动路线没有完成任何一格行进");
            Check((time?.get_total_game_hours() ?? beforeHours) > beforeHours, "自动路线没有推进游戏时间");
            GD.Print("YUXU_CASE_TRANSPORT_MAP_SMOKE PASS: assigned destination route, current travel costs, automatic movement, time advance, and interruption");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError($"YUXU_CASE_TRANSPORT_MAP_SMOKE_FAIL: {exception}");
            GetTree().Quit(1);
        }
    }
}
