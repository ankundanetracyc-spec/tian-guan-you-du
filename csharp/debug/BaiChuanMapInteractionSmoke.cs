using Godot;
using System;
using System.Reflection;
using System.Threading.Tasks;

public partial class BaiChuanMapInteractionSmoke : Node
{
    private async Task Frames(int count = 1) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    public override void _Ready() => CallDeferred(nameof(Run));
    private async void Run()
    {
        try
        {
            var main = GD.Load<PackedScene>("res://tscn/scene/MainScene.tscn").Instantiate<MainScene>();
            AddChild(main);
            var limit = Time.GetTicksMsec() + 90000UL;
            while (!main.StartupComplete && Time.GetTicksMsec() < limit) await Frames();
            Check(main.StartupComplete && main.StartupError is null, "start");
            typeof(MainScene).GetField("_skipExitSave", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(main, true);
            var stage = typeof(MainScene).GetField("_qingxi_prologue", BindingFlags.Instance | BindingFlags.NonPublic)!;
            stage.SetValue(main, Enum.Parse(stage.FieldType, "Departed"));
            typeof(MainScene).GetMethod("open_world", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(main, []);
            await Frames(8);
            var map = main.GetNode<Control>("MapLayer/BaiChuanNodeWorld");
            Check(map.GetNodeOrNull<Control>("WorldMapDetails") is not null, "missing compact card");
            foreach (var size in new[] { new Vector2I(1920, 1080), new Vector2I(1280, 720) })
            {
                DisplayServer.WindowSetSize(size);
                await Frames(8);
                var image = GetViewport().GetTexture().GetImage();
                Check(image is not null && !image.IsEmpty(), "screen capture failed");
                image.SavePng($"res://output/bai_chuan_formal_ui_{size.X}x{size.Y}.png");
            }
            var worldSize = 3762f;
            map.Set("camera_pos", new Vector2(-10000, -10000));
            map.Call("_update_camera");
            var min = map.Get("camera_pos").AsVector2();
            Check(min.X <= -GetViewport().GetVisibleRect().Size.X / 2 + 1 && min.Y <= -GetViewport().GetVisibleRect().Size.Y / 2 + 1, "left/top limits");
            map.Set("camera_pos", new Vector2(10000, 10000));
            map.Call("_update_camera");
            var max = map.Get("camera_pos").AsVector2();
            Check(max.X >= worldSize - GetViewport().GetVisibleRect().Size.X / 2 - 1 && max.Y >= worldSize - GetViewport().GetVisibleRect().Size.Y / 2 - 1, "right/bottom limits");
            GD.Print("BAICHUAN_MAP_INTERACTION_SMOKE_OK");
            main.QueueFree();
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError("BAICHUAN_MAP_INTERACTION_SMOKE_FAILED " + e); GetTree().Quit(1); }
    }
}
