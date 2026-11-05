using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GArray = Godot.Collections.Array;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

/// <summary>Exercises formal node-map click, double-click, wheel, drag, and lock handling in a real viewport.</summary>
public partial class BaiChuanFormalDoubleClickSmoke : Node
{
    private const int DoubleClickWindowMs = 450;

    private sealed record PlaceTarget(string Id, string RuntimeId, string Name, Button Hit, Button Nameplate, Sprite2D Icon, int RouteCount);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static string NormalizePath(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static string UserArgument(string prefix) =>
        OS.GetCmdlineUserArgs().FirstOrDefault(value => value.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..] ?? "";

    private static object? Invoke(object owner, string name, params object[] args) =>
        typeof(MainScene).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(owner, args);

    private static object? Field(object owner, string name) =>
        typeof(MainScene).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner);

    private void VerifyIsolatedUserData()
    {
        var expectedAppData = UserArgument("--expected-appdata=");
        Check(!string.IsNullOrWhiteSpace(expectedAppData), "缺少隔离 APPDATA 参数，拒绝访问正式玩家存档。" );
        Check(string.Equals(NormalizePath(OS.GetEnvironment("APPDATA")), NormalizePath(expectedAppData), StringComparison.OrdinalIgnoreCase),
            "Godot 进程 APPDATA 与隔离目录不一致，拒绝访问正式玩家存档。" );
        var customName = ProjectSettings.GetSetting("application/config/custom_user_dir_name").AsString();
        Check(!string.IsNullOrWhiteSpace(customName), "项目没有独立用户数据目录名，拒绝运行。" );
        var expectedUserData = NormalizePath(Path.Combine(expectedAppData, customName));
        Check(string.Equals(NormalizePath(OS.GetUserDataDir()), expectedUserData, StringComparison.OrdinalIgnoreCase),
            $"user:// 未解析到隔离目录：期望 {expectedUserData}，实际 {OS.GetUserDataDir()}。" );
        GD.Print($"BAICHUAN_FORMAL_DOUBLE_CLICK_ISOLATION appdata={expectedAppData} user_data={OS.GetUserDataDir()}");
    }

    private async Task Frames(int count = 1)
    {
        for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    // Frame counts are not elapsed time on fast/uncapped renderers.
    private async Task ExpireClickPair() =>
        await ToSignal(GetTree().CreateTimer((DoubleClickWindowMs + 60) / 1000.0), SceneTreeTimer.SignalName.Timeout);

    private async Task MoveMouse(Vector2 point, Vector2 relative = default, bool leftHeld = false)
    {
        Input.ParseInputEvent(new InputEventMouseMotion
        {
            Position = point,
            GlobalPosition = point,
            Relative = relative,
            ButtonMask = leftHeld ? MouseButtonMask.Left : (MouseButtonMask)0
        });
        await Frames(2);
    }

    private async Task SendMouseButton(Vector2 point, bool pressed)
    {
        Input.ParseInputEvent(new InputEventMouseButton
        {
            Position = point,
            GlobalPosition = point,
            ButtonIndex = MouseButton.Left,
            Pressed = pressed,
            ButtonMask = pressed ? MouseButtonMask.Left : (MouseButtonMask)0
        });
        await Frames(2);
    }

    private async Task Click(Control map, PlaceTarget target, bool useNameplate = false)
    {
        var point = useNameplate ? target.Nameplate.GetGlobalRect().GetCenter() : FindOpaqueIconPoint(target.Hit, target.Icon);
        await MoveMouse(point);
        var expected = useNameplate ? (Control)target.Nameplate : target.Hit;
        CheckHovered(GetViewport(), expected, $"点击 {target.Name} 的{(useNameplate ? "名牌" : "图标")}");
        await SendMouseButton(point, true);
        await SendMouseButton(point, false);
    }

    private async Task DoubleClick(Control map, PlaceTarget target, bool firstUsesNameplate = false, bool secondUsesNameplate = false)
    {
        await Click(map, target, firstUsesNameplate);
        await Click(map, target, secondUsesNameplate);
    }

    private async Task Wheel(Control map, MouseButton button, int expectedLevel, string step)
    {
        var point = GetViewport().GetVisibleRect().Size * 0.5f;
        await MoveMouse(point);
        Input.ParseInputEvent(new InputEventMouseButton
        {
            Position = point,
            GlobalPosition = point,
            ButtonIndex = button,
            Pressed = true
        });
        await Frames(2);
        Check(ZoomLevel(map) == expectedLevel, $"{step}滚轮按下后档位错误：期望 {expectedLevel}，实际 {ZoomLevel(map)}。" );
        Input.ParseInputEvent(new InputEventMouseButton
        {
            Position = point,
            GlobalPosition = point,
            ButtonIndex = button,
            Pressed = false
        });
        await Frames(2);
        Check(ZoomLevel(map) == expectedLevel, $"{step}滚轮释放时重复缩放：档位变为 {ZoomLevel(map)}。" );
    }

    private async Task SetZoom(Control map, int level)
    {
        map.Call("_set_zoom_level", level, GetViewport().GetVisibleRect().Size * 0.5f);
        await Frames(5);
        Check(ZoomLevel(map) == level, $"地图未切换到缩放档 {level}，实际 {ZoomLevel(map)}。" );
    }

    private async Task FocusPlace(Control map, PlaceTarget target)
    {
        map.Call("_focus_place", target.Name);
        await Frames(4);
    }

    private static int ZoomLevel(Control map) => map.Call("get_zoom_level").AsInt32();
    private static string CurrentNode(Control map) => map.Call("get_current_node").AsString();
    private static Vector2 Camera(Control map) => map.Get("camera_pos").AsVector2();
    private static float Zoom(Control map) => (float)map.Get("world_zoom").AsDouble();
    private static string SelectedNode(Control map) => map.Get("selected_node").AsString();

    private static Vector2 FindOpaqueIconPoint(Button hit, Sprite2D icon)
    {
        var texture = icon.Texture ?? throw new InvalidOperationException($"地点图标纹理缺失：{hit.GetPath()}。" );
        var image = texture.GetImage();
        var rect = hit.GetGlobalRect();
        for (var y = 0; y < image.GetHeight(); y += 2)
        {
            for (var x = 0; x < image.GetWidth(); x += 2)
            {
                if (image.GetPixel(x, y).A < 0.3f) continue;
                return rect.Position + new Vector2(
                    (x + 0.5f) * rect.Size.X / image.GetWidth(),
                    (y + 0.5f) * rect.Size.Y / image.GetHeight());
            }
        }
        throw new InvalidOperationException($"地点图标没有可点像素：{hit.GetPath()}。" );
    }

    private static void CheckHovered(Viewport viewport, Control expected, string step)
    {
        var hovered = viewport.GuiGetHoveredControl();
        Check(hovered == expected,
            $"{step}悬停目标不正确：期望 {expected.GetPath()}，实际 {(hovered == null ? "null" : hovered.GetPath().ToString())}。" );
    }

    private static PlaceTarget[] FindTargets(Control map, string currentNode)
    {
        var places = map.GetNode<Node>("FixedScaleWorld/Places");
        return places.GetChildren()
            .OfType<Control>()
            .Where(marker => marker.Visible && marker.HasNode("SelectIcon") && marker.HasNode("SelectPlace") && marker.HasNode("NativeIcon"))
            .Select(marker =>
            {
                var id = marker.Name;
                var runtimeId = map.Call("get_runtime_id", id).AsString();
                var route = string.IsNullOrEmpty(runtimeId)
                    ? Array.Empty<string>()
                    : map.Call("get_assigned_route_to_node", runtimeId).AsGodotArray<string>().ToArray();
                var hit = marker.GetNode<Button>("SelectIcon");
                var nameplate = marker.GetNode<Button>("SelectPlace");
                var icon = marker.GetNode<Sprite2D>("NativeIcon");
                return new PlaceTarget(id, runtimeId, nameplate.Text.Replace("\n", ""), hit, nameplate, icon, route.Length);
            })
            .Where(target => target.Id != currentNode && !string.IsNullOrWhiteSpace(target.RuntimeId) && target.RouteCount > 1 &&
                target.Hit.IsVisibleInTree() && target.Icon.Texture != null)
            .OrderBy(target => target.RouteCount)
            .ThenBy(target => target.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static PlaceTarget FindById(Control map, string nodeId)
    {
        var marker = map.GetNode<Control>($"FixedScaleWorld/Places/{nodeId}");
        return new PlaceTarget(
            nodeId,
            map.Call("get_runtime_id", nodeId).AsString(),
            marker.GetNode<Button>("SelectPlace").Text.Replace("\n", ""),
            marker.GetNode<Button>("SelectIcon"),
            marker.GetNode<Button>("SelectPlace"),
            marker.GetNode<Sprite2D>("NativeIcon"),
            0);
    }

    private async Task WaitForTravel(Control map, int timeoutMilliseconds = 90000)
    {
        var deadline = Time.GetTicksMsec() + (ulong)timeoutMilliseconds;
        while (map.Call("is_traveling").AsBool() && Time.GetTicksMsec() < deadline)
            await Frames();
        Check(!map.Call("is_traveling").AsBool(), "行旅超时，地图仍处于行走状态。" );
    }

    public override void _Ready() => CallDeferred(nameof(Run));

    private async void Run()
    {
        MainScene? main = null;
        Control? map = null;
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
            typeof(MainScene).GetField("_baishiLecture", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(main, null);
            (typeof(MainScene).GetField("story_dialogue", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(main) as Control)?.Hide();
            Invoke(main, "open_world");
            await Frames(8);

            map = main.GetNode<Control>("MapLayer/BaiChuanNodeWorld");
            Check(map.IsVisibleInTree() && map.IsInGroup("node_world_map_presentation"), "正式节点地图没有显示。" );
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
            DisplayServer.WindowSetSize(new Vector2I(1920, 1080));
            await Frames(6);
            var viewportSize = GetViewport().GetVisibleRect().Size;
            Check(viewportSize.X >= 1280 && viewportSize.Y >= 720, $"测试视口尺寸异常：{viewportSize}。" );
            Check(ZoomLevel(map) == 2, $"正式地图没有默认打开近景：当前档位 {ZoomLevel(map)}。" );

            var initialNode = CurrentNode(map);
            var initialRuntimeId = map.Call("get_runtime_id", initialNode).AsString();
            Check(!string.IsNullOrWhiteSpace(initialRuntimeId), $"新档起点没有运行时地点身份：{initialNode}。" );
            Check((bool)Invoke(main, "is_world_location_unlocked", initialRuntimeId)!, $"新档起点未解锁，无法验证地点进入：{initialRuntimeId}。" );
            var initialPlace = FindById(map, initialNode);
            Check(initialPlace.Hit.IsVisibleInTree() && initialPlace.Nameplate.IsVisibleInTree(), "起点图标或名牌不可见。" );

            var travelStarted = 0;
            var enteredIds = new System.Collections.Generic.List<string>();
            map.Connect("node_travel_started", Callable.From(() => travelStarted++));
            map.Connect("node_enter_requested", Callable.From<string>(id => enteredIds.Add(id)));

            // Prevent the independent random-encounter clock from interrupting this deterministic input check.
            Check((bool)Invoke(main, "initialize_road_encounter_clock")!, "随机遭遇时钟初始化失败。" );
            var encounterClock = (GodotObject)Field(main, "_roadEncounterClock")!;
            encounterClock.Set("distance_until_encounter", 1000000000.0);

            // Mouse-wheel presses move one tier; releases and boundary presses do not repeat.
            await SetZoom(map, 0);
            await Wheel(map, MouseButton.WheelUp, 1, "远景→中景");
            await Wheel(map, MouseButton.WheelUp, 2, "中景→近景");
            await Wheel(map, MouseButton.WheelUp, 2, "近景上界");
            await Wheel(map, MouseButton.WheelDown, 1, "近景→中景");
            await Wheel(map, MouseButton.WheelDown, 0, "中景→远景");
            await Wheel(map, MouseButton.WheelDown, 0, "远景下界");
            Check(travelStarted == 0 && enteredIds.Count == 0, "滚轮缩放错误触发了行旅或地点进入。" );

            var farTargets = FindTargets(map, initialNode);
            Check(farTargets.Length >= 2, "最远档没有足够的可点击具名地点用于交互检查。" );
            var targetA = farTargets[0];
            var targetB = farTargets.First(target => target.Id != targetA.Id);

            // A short click selects at each zoom tier without beginning travel.
            foreach (var level in new[] { 0, 1, 2 })
            {
                await SetZoom(map, level);
                Check(targetA.Hit.IsVisibleInTree(), $"{targetA.Name} 在缩放档 {level} 不可见。" );
                await FocusPlace(map, targetA);
                await Click(map, targetA);
                Check(SelectedNode(map) == targetA.Id, $"縮放档 {level} 单击未选中 {targetA.Name}。" );
                Check(!map.Call("is_traveling").AsBool() && travelStarted == 0 && enteredIds.Count == 0,
                    $"缩放档 {level} 单击启动了行旅或地点进入。" );
                await ExpireClickPair();
            }

            // A wheel gesture between two clicks on the same node must break the double-click pair.
            await SetZoom(map, 1);
            await FocusPlace(map, targetA);
            await Click(map, targetA);
            await Wheel(map, MouseButton.WheelUp, 2, "单击间滚轮");
            await FocusPlace(map, targetA);
            await Click(map, targetA);
            Check(SelectedNode(map) == targetA.Id && travelStarted == 0 && enteredIds.Count == 0,
                "节点单击、滚轮、同节点单击被误判为双击。" );
            await ExpireClickPair();

            // Changing from one node hotspot to another cannot be mistaken for a double-click.
            await SetZoom(map, 2);
            await FocusPlace(map, targetA);
            await Click(map, targetA);
            await FocusPlace(map, targetB);
            await Click(map, targetB);
            Check(SelectedNode(map) == targetB.Id && CurrentNode(map) == initialNode && travelStarted == 0 && enteredIds.Count == 0,
                "连续点击不同节点被误判为双击或没有选中第二个节点。" );
            await ExpireClickPair();

            // A drag over a real node hotspot pans the map, but never counts as the first click of a pair.
            await FocusPlace(map, targetA);
            var dragPoint = FindOpaqueIconPoint(targetA.Hit, targetA.Icon);
            await MoveMouse(dragPoint);
            CheckHovered(GetViewport(), targetA.Hit, "拖拽前节点图标");
            var beforeDrag = Camera(map);
            await SendMouseButton(dragPoint, true);
            Check(map.Get("_map_press_active").AsBool(), "节点图标按下没有建立地图手势。" );
            var dragDelta = new Vector2(24, 18);
            await MoveMouse(dragPoint + dragDelta, dragDelta, true);
            Check(Camera(map).DistanceTo(beforeDrag - dragDelta / Zoom(map)) <= 1.5f, "节点拖拽没有按缩放倍率平移地图。" );
            await SendMouseButton(dragPoint + dragDelta, false);
            Check(!map.Get("_map_press_active").AsBool() && !map.Call("is_traveling").AsBool() && travelStarted == 0,
                "拖拽松手触发了地图点击或行旅。" );
            await Click(map, targetA);
            Check(!map.Call("is_traveling").AsBool() && travelStarted == 0,
                "拖拽被错误记录为双击的第一下。" );
            await ExpireClickPair();

            // A click made while encounter-blocked is discarded. After unlock, one fresh click is still only a selection.
            await SetZoom(map, 0);
            map.Set("travel_paused", true); // Tail-node encounter state: no remaining route, but the pause flag remains set.
            Check(map.Call("get_remaining_travel_path").AsGodotArray<string>().Count == 1, "尾节点遭遇测试不是空剩余路线。" );
            map.Call("set_encounter_blocked", true);
            await FocusPlace(map, targetA);
            await Click(map, targetA);
            Check(!map.Call("is_traveling").AsBool() && travelStarted == 0, "遭遇锁期间点击启动了行旅。" );
            map.Call("set_encounter_blocked", false);
            Check(map.Get("travel_paused").AsBool(), "清除遭遇锁时错误清除了尾节点暂停标记。" );
            await Click(map, targetA);
            Check(!map.Call("is_traveling").AsBool() && travelStarted == 0,
                "遭遇锁期间的首击被保留，解锁后一次点击就误启了行旅。" );
            Check(SelectedNode(map) == targetA.Id, "解锁后的新单击没有选择远端节点。" );

            // The unlocked second click may use a different hotspot of the same node (icon then nameplate).
            await Click(map, targetA, useNameplate: true);
            Check(map.Call("is_traveling").AsBool() && travelStarted == 1,
                "同一节点先点图标再点名牌没有启动行旅。" );
            var enterButton = map.Get("enter_button").AsGodotObject() as Button ?? throw new InvalidOperationException("进入按钮缺失。" );
            Check(enterButton.Disabled && !enterButton.Visible && map.GetNodeOrNull("WorldMapDetails") is null,
                "行旅中仍显示地点入口，或旧右上角操作框仍存在。" );

            // While the route is moving, a same-node double-click cannot enter the origin or start another route.
            await DoubleClick(map, initialPlace);
            Check(travelStarted == 1 && enteredIds.Count == 0 && map.Call("is_traveling").AsBool(),
                "行旅锁期间双击起点触发了地点进入或第二段行旅。" );
            await WaitForTravel(map);
            Check(CurrentNode(map) == targetA.Id, $"双击行旅没有抵达所选节点：{CurrentNode(map)}，期望 {targetA.Id}。" );
            Check(travelStarted == 1 && enteredIds.Count == 0, "单次双击产生了重复行旅或地点进入。" );

            // Exercise the travel lock's click-sequence reset deterministically, then prove one post-unlock click is not enough.
            map.Set("travel_busy", true);
            map.Call("_refresh_actions");
            await FocusPlace(map, targetB);
            await Click(map, targetB);
            Check(map.Get("travel_busy").AsBool() && travelStarted == 1, $"行旅锁期间首击清除了锁状态或启动了额外行旅：busy={map.Get("travel_busy")} started={travelStarted} current={CurrentNode(map)}。" );
            map.Set("travel_busy", false);
            map.Call("_refresh_actions");
            await Click(map, targetB);
            Check(!map.Call("is_traveling").AsBool() && travelStarted == 1,
                "行旅锁期间的首击被保留，解锁后一次点击就误启了行旅。" );
            await ExpireClickPair();

            // Restore the fresh-start location so the final enter test uses the known unlocked, enterable place.
            var resetState = new GDictionary
            {
                ["current_node"] = initialNode,
                ["remaining_route"] = new GArray()
            };
            Check(map.Call("restore_node_state", resetState).AsBool(), "无法恢复测试起点。" );
            Invoke(main, "on_node_world_arrived", initialNode);
            Check(CurrentNode(map) == initialNode && !map.Call("is_traveling").AsBool(), "测试起点恢复状态不一致。" );

            // A blocked first click is not carried over to unlock; a fresh icon/nameplate double-click enters the current place.
            await SetZoom(map, 2);
            await FocusPlace(map, initialPlace);
            map.Set("travel_paused", true);
            map.Call("set_encounter_blocked", true);
            await Click(map, initialPlace);
            Check(enteredIds.Count == 0 && map.IsVisibleInTree(), "遭遇锁期间双击前的单击进入了地点。" );
            map.Call("set_encounter_blocked", false);
            await Click(map, initialPlace);
            Check(enteredIds.Count == 0 && map.IsVisibleInTree(), "遭遇锁期间点击被带到解锁后，误进入了当前地点。" );
            await Click(map, initialPlace, useNameplate: true);
            await Frames(8);
            Check(enteredIds.Count == 1 && enteredIds[0] == initialRuntimeId,
                $"双击当前位置没有发出正确的地点进入请求：{string.Join(",", enteredIds)}。" );
            Check(main.GetNodeOrNull<Control>("MapLayer/BaiChuanNodeWorld") is null,
                "双击当前位置后地图仍留在前台。" );
            var locationInterior = main.GetNodeOrNull<Control>("SceneLayer/WorldLocationInteriorPanel");
            Check(locationInterior is not null && locationInterior.IsVisibleInTree(), "双击当前位置没有打开地点内景。" );

            GD.Print($"BAICHUAN_FORMAL_DOUBLE_CLICK_SMOKE_OK wheel_tiers=true release_once=true wheel_breaks_pair=true zoom_clicks=0,1,2 different_node_reset=true drag_reset=true encounter_lock_reset=true travel_pause_empty_route=true icon_nameplate_same_node=true travel_started={travelStarted} enter={initialRuntimeId}");
            main.QueueFree();
            await Frames(3);
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError("BAICHUAN_FORMAL_DOUBLE_CLICK_SMOKE_FAILED: " + error);
            if (main is not null && GodotObject.IsInstanceValid(main))
            {
                typeof(MainScene).GetField("_skipExitSave", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(main, true);
                main.QueueFree();
            }
            GetTree().Quit(1);
        }
    }
}
