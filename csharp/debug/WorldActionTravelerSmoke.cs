using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

public partial class WorldActionTravelerSmoke : Node
{
    private const string Folder = "res://output/world_action_traveler_20261104";
    private int _checks;
    private void Check(bool value, string message) { if (!value) throw new Exception(message); _checks++; }
    private async Task Frames(int count = 1) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private static Godot.Collections.Dictionary Summary(Node avatar) => avatar.Call("get_visual_summary").AsGodotDictionary();
    private async Task Capture(Node2D marker, string name)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        Check(image.SavePng($"{Folder}/{name}.png") == Error.Ok, "真实窗口截图 " + name);
        var point = marker.GetGlobalTransformWithCanvas().Origin;
        var transform = GetViewport().GetFinalTransform();
        point = transform * point;
        var cropRect = new Rect2I((int)point.X - 58, (int)point.Y - 88, 116, 116).Intersection(new Rect2I(0, 0, image.GetWidth(), image.GetHeight()));
        if (cropRect.Size.X <= 0 || cropRect.Size.Y <= 0) return;
        using var crop = image.GetRegion(cropRect);
        crop.Resize(crop.GetWidth() * 4, crop.GetHeight() * 4, Image.Interpolation.Nearest);
        Check(crop.SavePng($"{Folder}/{name}_detail.png") == Error.Ok, "局部原生放大 " + name);
    }

    public override void _Ready() => CallDeferred(nameof(Run));
    private async void Run()
    {
        MainScene? main = null;
        try
        {
            var expected = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--expected-appdata="))?[19..];
            Check(!string.IsNullOrEmpty(expected) && OS.GetEnvironment("APPDATA") == expected, "必须使用隔离用户目录");
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Folder));
            GetWindow().Size = new Vector2I(1920, 1080);
            main = GD.Load<PackedScene>("res://tscn/scene/MainScene.tscn").Instantiate<MainScene>();
            AddChild(main);
            var deadline = Time.GetTicksMsec() + 90000UL;
            while (!main.StartupComplete && Time.GetTicksMsec() < deadline) await Frames();
            Check(main.StartupComplete && main.StartupError is null, "真实旅程启动");
            typeof(MainScene).GetField("_skipExitSave", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(main, true);
            var stage = typeof(MainScene).GetField("_qingxi_prologue", BindingFlags.Instance | BindingFlags.NonPublic)!;
            stage.SetValue(main, Enum.Parse(stage.FieldType, "Departed"));
            if (typeof(MainScene).GetField("_baishiLecture", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(main) is BaishiLectureProgress lecture)
                lecture.departed_qingxi = true;
            typeof(MainScene).GetMethod("open_world", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(main, []);
            await Frames(8);
            var map = main.GetNode<Control>("MapLayer/BaiChuanNodeWorld");
            var marker = map.GetNode<Sprite2D>("FixedScaleWorld/PlayerNodeMark");
            var avatar = marker.GetNode<Node2D>("WorldActionTraveler");
            var summary = Summary(avatar);
            Check(summary["world_avatar_loaded"].AsBool() && summary["art_folder"].AsString() == "yuxu_fullbody_v15", "正式大世界使用现行局内形象");
            Check(summary["bone_bound_art_ready"].AsBool() && summary["anatomical_skin"].AsGodotDictionary()["errors"].AsGodotArray().Count == 0, "局内骨骼与衣袍分件有效");
            Check(marker.SelfModulate.A == 0 && avatar.Visible && avatar.Modulate.A == 1, "只绘制局内人物，不叠旧静态棋子");
            Check(summary["clip"].AsString() == "Idle", "驻足播放待机");
            foreach (var level in new[] { 0, 1, 2 })
            {
                map.Call("_set_zoom_level", level, GetViewport().GetVisibleRect().Size / 2);
                map.Call("focus_player_node"); await Frames(3);
                var world = map.GetNode<Node2D>("FixedScaleWorld");
                var height = summary["native_height"].AsSingle() * avatar.Scale.Y * marker.Scale.Y * world.Scale.Y;
                Check(Mathf.Abs(height - 32) < .1f, "缩放档位人物保持原32像素高度 " + level);
                await Capture(marker, "zoom_" + level);
            }
            var origin = map.Call("get_current_node").AsString();
            var neighbor = map.Call("get_current_neighbours").AsGodotArray<string>()[0];
            var clock = GetNode<GameTimeMgr>("/root/GameTimeManager");
            var hoursBefore = clock.get_precise_game_hours();
            var start = marker.Position;
            var sawWalking = false; var sawLeft = false; var sawRight = false;
            async Task TravelTo(string node, string captureName)
            {
                map.Call("set_encounter_blocked", false);
                map.Call("_select_transit", node); map.Call("_travel_selected"); map.Call("pause_travel_at_node");
                var limit = Time.GetTicksMsec() + 45000UL;
                var captured = false;
                while (map.Call("is_traveling").AsBool() && Time.GetTicksMsec() < limit)
                {
                    await Frames();
                    var state = Summary(avatar);
                    if (state["clip"].AsString() != "Run_New1") continue;
                    sawWalking = true;
                    sawLeft |= state["facing"].AsSingle() < 0;
                    sawRight |= state["facing"].AsSingle() > 0;
                    if (!captured) { await Capture(marker, captureName); captured = true; }
                }
                Check(!map.Call("is_traveling").AsBool() && map.Call("get_current_node").AsString() == node, "正式路线抵达并提交 " + node);
                await Frames(3);
                Check(Summary(avatar)["clip"].AsString() == "Idle", "抵达后回到待机");
            }
            await TravelTo(neighbor, "walk_outbound");
            Check(marker.Position.DistanceTo(start) > 1 && clock.get_precise_game_hours() > hoursBefore, "路线位移与游戏行旅时间仍正常推进");
            await TravelTo(origin, "walk_return");
            Check(sawWalking && sawLeft && sawRight, "正式往返行旅包含走路与双向朝向");
            Check(marker.Position.DistanceTo(start) < .01f, "脚点仍精确锚定原地点");
            var beforePose = Summary(avatar)["motion_clock"].AsSingle();
            map.Hide(); await Frames(5);
            Check(Summary(avatar)["motion_clock"].AsSingle() == beforePose, "地图隐藏时暂停角色逐帧动画");
            map.Show(); await Frames(3);
            var saved = map.Call("get_node_state");
            Check(map.Call("restore_node_state", saved).AsBool() && Summary(avatar)["clip"].AsString() == "Idle", "存档位置恢复不误触走路");
            GetWindow().Size = new Vector2I(1280, 720); await Frames(6);
            map.Call("focus_player_node"); await Frames(3);
            await Capture(marker, "small_window");
            GD.Print($"WORLD_ACTION_TRAVELER_OK {_checks}");
            main.QueueFree(); await Frames(2); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); if (IsInstanceValid(main)) main!.QueueFree(); GetTree().Quit(1); }
    }
}
