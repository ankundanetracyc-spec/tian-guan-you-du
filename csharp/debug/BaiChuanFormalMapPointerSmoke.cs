using Godot;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

/// <summary>Exercises the formal node map's left-button behavior with isolated user data.</summary>
public partial class BaiChuanFormalMapPointerSmoke : Node
{
    private const string ExpectedPlaceId = "bc_place_021";
    private const string ExpectedPlaceTitle = "白石城";
    private const float WorldSize = 3762f;

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static string NormalizePath(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static string UserArgument(string prefix) =>
        OS.GetCmdlineUserArgs().FirstOrDefault(value => value.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..] ?? "";

    private void VerifyIsolatedUserData()
    {
        var expectedAppData = UserArgument("--expected-appdata=");
        Check(!string.IsNullOrWhiteSpace(expectedAppData), "缺少隔离 APPDATA 参数，拒绝访问玩家存档。");
        Check(string.Equals(NormalizePath(OS.GetEnvironment("APPDATA")), NormalizePath(expectedAppData), StringComparison.OrdinalIgnoreCase),
            "Godot 进程 APPDATA 与隔离目录不一致，拒绝访问玩家存档。");
        var customName = ProjectSettings.GetSetting("application/config/custom_user_dir_name").AsString();
        Check(!string.IsNullOrWhiteSpace(customName), "项目没有独立用户数据目录名，拒绝运行。");
        var expectedUserData = NormalizePath(Path.Combine(expectedAppData, customName));
        Check(string.Equals(NormalizePath(OS.GetUserDataDir()), expectedUserData, StringComparison.OrdinalIgnoreCase),
            $"user:// 未解析到隔离目录：期望 {expectedUserData}，实际 {OS.GetUserDataDir()}。");
    }

    private async Task Frames(int count = 1)
    {
        for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task MoveMouse(Vector2 position, Vector2 relative, bool leftHeld = false)
    {
        var motion = new InputEventMouseMotion
        {
            Position = position,
            Relative = relative,
            ButtonMask = leftHeld ? MouseButtonMask.Left : (MouseButtonMask)0
        };
        Input.ParseInputEvent(motion);
        await Frames(2);
    }

    private async Task SendMouseButton(Vector2 position, bool pressed)
    {
        var button = new InputEventMouseButton
        {
            Position = position,
            ButtonIndex = Godot.MouseButton.Left,
            Pressed = pressed,
            ButtonMask = pressed ? MouseButtonMask.Left : (MouseButtonMask)0
        };
        Input.ParseInputEvent(button);
        await Frames(2);
    }

    private static Vector2 Camera(Control map) => map.Get("camera_pos").AsVector2();
    private static float Zoom(Control map) => (float)map.Get("world_zoom").AsDouble();

    private static Label TitleLabel(Control map) =>
        map.Get("title_label").AsGodotObject() as Label ?? throw new InvalidOperationException("正式地图标题控件缺失。");

    private static Vector2 FindOpaqueIconPoint(Button hit, Sprite2D icon)
    {
        var texture = icon.Texture ?? throw new InvalidOperationException("白石镇节点图标纹理缺失。");
        var image = texture.GetImage();
        var rect = hit.GetGlobalRect();
        for (var y = 0; y < image.GetHeight(); y += 2)
        {
            for (var x = 0; x < image.GetWidth(); x += 2)
            {
                if (image.GetPixel(x, y).A < 0.3f) continue;
                var point = rect.Position + new Vector2(
                    (x + 0.5f) * rect.Size.X / image.GetWidth(),
                    (y + 0.5f) * rect.Size.Y / image.GetHeight());
                return point;
            }
        }
        throw new InvalidOperationException("白石镇节点图标没有可点像素。");
    }

    private static void CheckHovered(Viewport viewport, Control expected, string step)
    {
        var hovered = viewport.GuiGetHoveredControl();
        Check(hovered == expected,
            $"{step}鼠标悬停目标不正确：期望 {expected.GetPath()}，实际 {(hovered == null ? "null" : hovered.GetPath().ToString())}。");
    }

    public override void _Ready() => CallDeferred(nameof(Run));

    private async void Run()
    {
        MainScene? main = null;
        try
        {
            VerifyIsolatedUserData();
            main = GD.Load<PackedScene>("res://tscn/scene/MainScene.tscn").Instantiate<MainScene>();
            AddChild(main);
            var startupDeadline = Time.GetTicksMsec() + 90000UL;
            while (!main.StartupComplete && Time.GetTicksMsec() < startupDeadline) await Frames();
            Check(main.StartupComplete && main.StartupError is null, "正式主场景启动失败：" + main.StartupError);

            typeof(MainScene).GetField("_skipExitSave", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(main, true);
            var stage = typeof(MainScene).GetField("_qingxi_prologue", BindingFlags.Instance | BindingFlags.NonPublic)!;
            stage.SetValue(main, Enum.Parse(stage.FieldType, "Departed"));
            typeof(MainScene).GetMethod("open_world", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(main, []);
            await Frames(8);

            var map = main.GetNode<Control>("MapLayer/BaiChuanNodeWorld");
            Check(map.IsVisibleInTree(), "正式节点地图没有显示。");
            Check(map.IsInGroup("node_world_map_presentation"), "当前地图不是正式节点地图展示。");
            var viewport = GetViewport();
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
            DisplayServer.WindowSetSize(new Vector2I(1920, 1080));
            await Frames(4);
            var viewportSize = viewport.GetVisibleRect().Size;
            Check(viewportSize.X >= 1280 && viewportSize.Y >= 720, $"测试视口尺寸异常：{viewportSize}。");

            map.Call("_focus_place", "白石城");
            await Frames(3);
            var iconHit = map.GetNode<Button>($"FixedScaleWorld/Places/{ExpectedPlaceId}/SelectIcon");
            var icon = map.GetNode<Sprite2D>($"FixedScaleWorld/Places/{ExpectedPlaceId}/NativeIcon");
            var title = TitleLabel(map);
            title.Text = "选择地点";

            // A press on the actual node artwork must pan with the pointer and must not select on release.
            var nodePoint = FindOpaqueIconPoint(iconHit, icon);
            await MoveMouse(nodePoint, Vector2.Zero);
            CheckHovered(viewport, iconHit, "节点拖动按下前");
            var beforeDrag = Camera(map);
            await SendMouseButton(nodePoint, true);
            Check(map.Get("_map_press_active").AsBool(), "节点按下没有建立地图拖动手势。");
            var dragDelta = new Vector2(32, 24);
            await MoveMouse(nodePoint + dragDelta, dragDelta, true);
            var afterDrag = Camera(map);
            var expectedDraggedCamera = beforeDrag - dragDelta / Zoom(map);
            Check(afterDrag.DistanceTo(expectedDraggedCamera) <= 1.5f,
                $"节点拖动没有按缩放倍率跟手：期望相机 {expectedDraggedCamera}，实际 {afterDrag}，倍率 {Zoom(map):0.00}。");
            await SendMouseButton(nodePoint + dragDelta, false);
            Check(title.Text == "选择地点", $"从节点拖动松手时误选了地点：{title.Text}。");
            Check(!map.Get("_map_press_active").AsBool(), "拖动松手后仍保留按压状态。");

            // A stationary short click on that same node must select it.
            nodePoint = FindOpaqueIconPoint(iconHit, icon);
            await MoveMouse(nodePoint, Vector2.Zero);
            CheckHovered(viewport, iconHit, "短点击按下前");
            await SendMouseButton(nodePoint, true);
            await SendMouseButton(nodePoint, false);
            Check(title.Text == ExpectedPlaceTitle, $"短点击没有选中节点 {ExpectedPlaceTitle}，当前标题：{title.Text}。");

            // A gesture originating on the map information panel must not start map panning.
            var details = map.GetNode<PanelContainer>("WorldMapDetails");
            var detailsRect = details.GetGlobalRect();
            var panelPoint = detailsRect.Position + new Vector2(detailsRect.Size.X - 8, detailsRect.Size.Y * 0.5f);
            await MoveMouse(panelPoint, Vector2.Zero);
            var panelHover = viewport.GuiGetHoveredControl();
            Check(panelHover == details || (panelHover != null && details.IsAncestorOf(panelHover)),
                $"UI 面板测试点未命中面板：实际 {(panelHover == null ? "null" : panelHover.GetPath().ToString())}。");
            var beforePanelDrag = Camera(map);
            await SendMouseButton(panelPoint, true);
            Check(!map.Get("_map_press_active").AsBool(), "从 UI 面板按下仍启动了地图手势。");
            var panelDelta = new Vector2(-80, 20);
            await MoveMouse(panelPoint + panelDelta, panelDelta, true);
            await SendMouseButton(panelPoint + panelDelta, false);
            Check(Camera(map).DistanceTo(beforePanelDrag) <= 0.5f, "从 UI 面板拖动时地图发生平移。");

            // From the top-left map boundary, ordinary in-window left drags can bring the map center into view.
            var halfViewport = viewportSize * 0.5f;
            var zoom = Zoom(map);
            map.Set("camera_pos", -halfViewport / zoom);
            map.Call("_update_camera");
            var edgeStart = new Vector2(viewportSize.X - 10, viewportSize.Y - 20);
            var expectedWorldCenter = Vector2.One * (WorldSize * 0.5f);
            var startWorldCenter = Camera(map) + halfViewport / zoom;
            var edgeDelta = (startWorldCenter.X - expectedWorldCenter.X) * zoom;
            var edgeEnd = new Vector2(edgeStart.X + edgeDelta, edgeStart.Y);
            await MoveMouse(edgeStart, Vector2.Zero);
            await SendMouseButton(edgeStart, true);
            await MoveMouse(edgeEnd, edgeEnd - edgeStart, true);
            await SendMouseButton(edgeEnd, false);

            // The horizontal pan reaches the middle; two ordinary gestures continue vertically.
            var verticalStart = new Vector2(viewportSize.X * 0.5f, viewportSize.Y - 10);
            var centerAfterHorizontal = Camera(map) + halfViewport / zoom;
            var firstVerticalDelta = Mathf.Clamp((centerAfterHorizontal.Y - expectedWorldCenter.Y) * zoom, -1000f, 0f);
            var verticalEnd = verticalStart + new Vector2(0, firstVerticalDelta);
            await MoveMouse(verticalStart, Vector2.Zero);
            await SendMouseButton(verticalStart, true);
            await MoveMouse(verticalEnd, verticalEnd - verticalStart, true);
            await SendMouseButton(verticalEnd, false);
            verticalStart = new Vector2(viewportSize.X * 0.5f, viewportSize.Y - 10);
            var centerAfterFirstVertical = Camera(map) + halfViewport / zoom;
            var secondVerticalDelta = (centerAfterFirstVertical.Y - expectedWorldCenter.Y) * zoom;
            verticalEnd = verticalStart + new Vector2(0, secondVerticalDelta);
            await MoveMouse(verticalStart, Vector2.Zero);
            await SendMouseButton(verticalStart, true);
            await MoveMouse(verticalEnd, verticalEnd - verticalStart, true);
            await SendMouseButton(verticalEnd, false);

            var centerWorldPoint = Camera(map) + halfViewport / zoom;
            Check(centerWorldPoint.DistanceTo(expectedWorldCenter) < 2,
                $"从地图边缘拖不到中心：视野中心 {centerWorldPoint}，地图中心 {expectedWorldCenter}。");

            GD.Print("BAICHUAN_FORMAL_MAP_POINTER_SMOKE_OK node_drag_follow=true release_no_click=true short_click_select=true ui_panel_no_drag=true edge_to_center=true");
            main.QueueFree();
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError("BAICHUAN_FORMAL_MAP_POINTER_SMOKE_FAILED: " + error);
            main?.QueueFree();
            GetTree().Quit(1);
        }
    }
}
