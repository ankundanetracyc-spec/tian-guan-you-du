using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class BaiChuanFreeWorldFlowSmoke
{
    private static GDictionary NpcVisual(AnimatedSprite2D avatar) => avatar.Call("get_visual_summary").AsGodotDictionary();

    private async Task CaptureNpcAvatar(AnimatedSprite2D avatar, string name)
    {
        // Keep the isolated calendar moving through the actual draw, so this is
        // a walking capture rather than the idle frame after a stopped probe.
        var priorAuto = _time.auto_advance;
        _time.auto_advance = true;
        try
        {
            await Capture(name);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            Check(avatar.Animation.ToString().StartsWith("walk_") && avatar.IsPlaying(), "抓图时NPC仍在真实迈步 " + name);
            using var image = GetViewport().GetTexture().GetImage();
            var foot = avatar.GetGlobalTransformWithCanvas().Origin;
            var region = new Rect2I((int)foot.X - 90, (int)foot.Y - 80, 180, 120)
                .Intersection(new Rect2I(0, 0, image.GetWidth(), image.GetHeight()));
            using var detail = image.GetRegion(region);
            detail.Resize(region.Size.X * 4, region.Size.Y * 4, Image.Interpolation.Nearest);
            Check(detail.SavePng(Folder + "/" + name + "_detail.png") == Error.Ok, "真实NPC画面局部放大 " + name);
        }
        finally { _time.auto_advance = priorAuto; }
    }

    private async Task NpcAvatar()
    {
        AtAnchor(); _world.set_simulation_enabled(false); _time.restore_calendar(1, 1, 2, 0, 0);
        var config = Json.ParseString(Godot.FileAccess.GetFileAsString("res://resources/world/bai_chuan/npc_avatar_20261106.json")).AsGodotDictionary();
        foreach (var source in config["source_files"].AsGodotDictionary())
            Check(Godot.FileAccess.GetSha256(source.Key.AsString()) == source.Value.AsString(), "NPC形象采集对应当前人物来源 " + source.Key);
        Check(Godot.FileAccess.GetSha256(config["atlas"].AsString()) == config["atlas_sha256"].AsString(), "正式NPC动画图集校验一致");
        Check(config["frame_audit"].AsGodotArray().Count == 64, "同一主角运行时采集64帧左右待机与行走");
        var tile = (Node)Field(_world, "_tile_world_map")!;
        var route = _graph.Call("shortest_path", "bc_place_001", "bc_place_070").AsGodotArray<string>();
        Require(route.Count > 2, "NPC形象使用真实已有行旅路段");
        var a = _world.node_world_cell_for_node_id(route[0]);
        var b = _world.node_world_cell_for_node_id(route[1]);
        var hours = tile.Call("get_npc_edge_travel_hours", a, b, 100f).AsDouble();
        var fixture = new WorldNpcData
        {
            npc_id = "qa_native_world_npc", npc_name = "行旅形象验收", current_node_id = "yuxu_xianzong_core", origin_node_id = "yuxu_xianzong_core",
            is_traveling = true, travel_origin_node_id = "yuxu_xianzong_core", travel_destination_node_id = "east_woods", current_subscene_id = "",
            has_world_grid_position = true, current_grid_cell = a, world_travel_path = new Godot.Collections.Array<Vector2I> { a, b },
            world_travel_next_cell_index = 1, world_travel_cell_progress_hours = (float)(hours * .2), dun_speed = 100,
        };
        _world.register_npc(fixture);
        Vector2 Sample() => tile.Call("project_map_position", fixture.current_grid_cell, fixture.world_travel_path, 1, fixture.world_travel_cell_progress_hours, _time.get_pending_hours(), 100f).AsVector2();
        var point = Sample();
        var nearby = point;
        foreach (var offset in new[] { new Vector2(80, 0), new Vector2(-80, 0), new Vector2(0, 80), new Vector2(0, -80) })
            if (_nav.Call("is_walkable", point + offset).AsBool()) { nearby = point + offset; break; }
        Require(nearby.DistanceTo(point) > 50, "NPC画面有与主角分开的可通行对照点");
        AtPosition(nearby);
        _map.Call("_set_zoom_level", 2, GetViewport().GetVisibleRect().Size / 2); _map.Call("focus_player_node");
        _map.Call("_refresh_people"); await Frames(5);
        var views = _map.Get("npc_views").AsGodotDictionary();
        Require(views.ContainsKey(fixture.npc_id) && views[fixture.npc_id].AsGodotObject() is AnimatedSprite2D, "真实世界人物使用动画小人");
        var avatar = (AnimatedSprite2D)views[fixture.npc_id].AsGodotObject();
        Require(NpcVisual(avatar)["on_screen"].AsBool(), "验收NPC位于真实可见画面");
        var shared = avatar.SpriteFrames.GetInstanceId();
        foreach (var row in views)
        {
            var other = row.Value.AsGodotObject() as AnimatedSprite2D;
            Check(other is not null && other.SpriteFrames.GetInstanceId() == shared, "地图NPC共享人物动作素材 " + row.Key);
            Check(other?.FindChild("CombatCharacterRig", true, false) is null, "NPC没有各自重复运算整套骨架 " + row.Key);
        }
        Check(NpcVisual(avatar)["appearance"].AsString() == "current_world_player_native_101" && avatar.Modulate == Colors.White, "NPC使用主角同套白青衣与束发原色");
        foreach (var level in new[] { 0, 1, 2 })
        {
            _map.Call("_set_zoom_level", level, GetViewport().GetVisibleRect().Size / 2); _map.Call("focus_player_node"); await Frames(3);
            Check(avatar.Visible == (level >= 1), "原有NPC缩放可见性保持 " + level);
            var height = avatar.Get("native_height").AsDouble() * avatar.Scale.Y;
            var visualScale = GD.Load<Script>("res://gdscripts/world/WorldMapVisualScale.gd").GetScriptConstantMap();
            Check(Math.Abs(height - visualScale["AVATAR_WORLD_HEIGHT"].AsDouble()) < .001, "NPC与主角共用固定人物世界标尺 " + level);
        }
        async Task AdvanceAnimation()
        {
            _time.resume_time();
            for (var i = 0; i < 16; i++) { _time.advance_time(.025f, "qa_npc_avatar"); await Frames(); }
        }
        var previous = Sample(); fixture.world_travel_cell_progress_hours = (float)(hours * .4);
        _map.Call("_refresh_people"); var forward = Sample() - previous;
        await AdvanceAnimation();
        Check(avatar.Animation.ToString().StartsWith("walk_") && avatar.IsPlaying(), "真实历法推进时行旅NPC播放迈步");
        Check(NpcVisual(avatar)["facing_left"].AsBool() == (forward.X < 0), "NPC朝向跟随实际行旅位移");
        var firstFacing = NpcVisual(avatar)["facing_left"].AsBool();
        await CaptureNpcAvatar(avatar, "npc_native_walk_outbound_1920");
        _time.pause_time(); await Frames(3);
        var heldFrame = avatar.Frame; var heldProgress = avatar.FrameProgress; await Frames(10);
        Check(avatar.Frame == heldFrame && avatar.FrameProgress == heldProgress && !avatar.IsPlaying(), "暂停时NPC动作帧和相位保持");
        _time.resume_time(); await Frames(5);
        Check(avatar.Animation.ToString().StartsWith("idle_"), "历法驻足后NPC回到待机");
        fixture.current_grid_cell = b; fixture.world_travel_path = new Godot.Collections.Array<Vector2I> { b, a };
        fixture.world_travel_cell_progress_hours = (float)(hours * .6); _map.Call("_refresh_people");
        fixture.world_travel_cell_progress_hours = (float)(hours * .8); _map.Call("_refresh_people");
        await AdvanceAnimation();
        Check(NpcVisual(avatar)["facing_left"].AsBool() != firstFacing && avatar.Animation.ToString().StartsWith("walk_"), "反向实际行程切换另一朝向的迈步");
        await CaptureNpcAvatar(avatar, "npc_native_walk_return_1920");
        _map.Hide(); await Frames(3); heldFrame = avatar.Frame; heldProgress = avatar.FrameProgress; await Frames(8);
        Check(avatar.Frame == heldFrame && avatar.FrameProgress == heldProgress && !avatar.IsPlaying(), "地图隐藏停止NPC动作");
        _map.Show(); await Frames(4);
        var cameraBefore = _map.Get("camera_pos").AsVector2();
        var worldSize = _nav.Get("world_size").AsSingle();
        var offscreen = new Vector2(avatar.Position.X < worldSize / 2 ? worldSize : 0, avatar.Position.Y < worldSize / 2 ? worldSize : 0);
        _map.Call("focus_world_point", offscreen); await Frames(3);
        await AdvanceAnimation();
        Check(!avatar.GetNode<VisibleOnScreenNotifier2D>("AvatarVisibility").IsOnScreen() && !avatar.IsPlaying(), "真实移镜至屏外后NPC暂停动作播放");
        _map.Set("camera_pos", cameraBefore); _map.Call("_update_camera"); await Frames(3);
        await AdvanceAnimation();
        Check(avatar.GetNode<VisibleOnScreenNotifier2D>("AvatarVisibility").IsOnScreen() && avatar.IsPlaying(), "真实移镜返回后NPC继续播放");
        point = Sample();
        foreach (var offset in new[] { new Vector2(80, 0), new Vector2(-80, 0), new Vector2(0, 80), new Vector2(0, -80) })
            if (_nav.Call("is_walkable", point + offset).AsBool()) { nearby = point + offset; break; }
        AtPosition(nearby); await Frames(4);
        var hit = avatar.GetNode<Button>("Approach");
        Check(hit.GetMeta("free_npc_id").AsString() == fixture.npc_id && hit.GetGlobalRect().HasPoint(avatar.GetGlobalTransformWithCanvas().Origin + Vector2.Up * 13), "NPC点击目标仍绑定原人物身份");
        await Motion(hit.GetGlobalRect().GetCenter(), Vector2.Zero);
        await MouseButton(hit.GetGlobalRect().GetCenter(), Godot.MouseButton.Left, true);
        await MouseButton(hit.GetGlobalRect().GetCenter(), Godot.MouseButton.Left, false);
        Check(Moving && _map.Get("_target_npc").AsString() == fixture.npc_id, "真实鼠标点击新小人仍按原规则靠近NPC");
        Stop();
        var savedPoint = Position;
        var saved = DiskRead(_main!.capture_battle_world_snapshot()!, "npc_avatar_world");
        await StartMain(saved); _time.pause_time();
        Check(Position.DistanceTo(savedPoint) < .002 && _world.get_npc(fixture.npc_id) is not null, "完整读档保留主角位置与原NPC身份");
        views = _map.Get("npc_views").AsGodotDictionary();
        Check(views.ContainsKey(fixture.npc_id) && views[fixture.npc_id].AsGodotObject() is AnimatedSprite2D restored && NpcVisual(restored)["appearance"].AsString() == "current_world_player_native_101", "读档后NPC继续使用同套人物形象");
    }
}
