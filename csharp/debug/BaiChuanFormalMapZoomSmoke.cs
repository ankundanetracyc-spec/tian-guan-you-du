using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

/// <summary>Checks the three formal map zoom levels in a real Godot Mono viewport.</summary>
public partial class BaiChuanFormalMapZoomSmoke : Node
{
    private static readonly float[] ZoomLevels = [0.24f, 0.48f, 0.96f];
    private const float MapSize = 3762f;
    private const string CaptureFolder = "res://output/bai_chuan_zoom_qa_mono";

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
        if (string.IsNullOrWhiteSpace(expectedAppData))
            expectedAppData = OS.GetEnvironment("BAICHUAN_EXPECTED_APPDATA");
        Check(!string.IsNullOrWhiteSpace(expectedAppData), "缺少隔离 APPDATA 参数，拒绝访问玩家存档。");
        Check(string.Equals(NormalizePath(OS.GetEnvironment("APPDATA")), NormalizePath(expectedAppData), StringComparison.OrdinalIgnoreCase),
            "Godot 进程 APPDATA 与隔离目录不一致，拒绝访问玩家存档。");
        var customName = ProjectSettings.GetSetting("application/config/custom_user_dir_name").AsString();
        Check(!string.IsNullOrWhiteSpace(customName), "项目未设置独立用户数据目录名。");
        Check(string.Equals(NormalizePath(OS.GetUserDataDir()), NormalizePath(Path.Combine(expectedAppData, customName)), StringComparison.OrdinalIgnoreCase),
            $"user:// 未解析到隔离目录：{OS.GetUserDataDir()}。");
    }

    private async Task Frames(int count = 1)
    {
        for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

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

    private async Task Wheel(Control map, MouseButton button, Vector2 point, bool pressed)
    {
        Input.ParseInputEvent(new InputEventMouseButton
        {
            Position = point,
            GlobalPosition = point,
            ButtonIndex = button,
            Pressed = pressed
        });
        await Frames(2);
    }

    private async Task Click(Vector2 point, bool pressed)
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

    private static Vector2 Camera(Control map) => map.Get("camera_pos").AsVector2();
    private static float Zoom(Control map) => (float)map.Get("world_zoom").AsDouble();
    private static int Level(Control map) => map.Call("get_zoom_level").AsInt32();

    private static Rect2 MapSafeRect(Control map) => map.Call("get_map_safe_rect").AsRect2();

    private static void VerifyWholeMapFits(Control map, Vector2 viewportSize, string label)
    {
        var zoom = Zoom(map);
        var side = MapSize * zoom;
        var origin = -Camera(map) * zoom;
        var safeRect = MapSafeRect(map);
        var viewportRect = new Rect2(Vector2.Zero, viewportSize);
        var mapRect = new Rect2(origin, Vector2.One * side);
        Check(viewportRect.Encloses(safeRect), $"{label} 安全视区超出视口：safe={safeRect} viewport={viewportSize}。");
        Check(side <= safeRect.Size.X + 0.5f && side <= safeRect.Size.Y + 0.5f && safeRect.Encloses(mapRect),
            $"{label} 地图没有完整放进安全视区：map={mapRect} safe={safeRect} viewport={viewportSize}。");
        GD.Print($"BAICHUAN_FAR_VIEW {label} zoom={zoom:0.0000} mapSide={side:0.0}px safeRect={safeRect} viewport={viewportSize} full=true");
    }

    private static float ExpectedZoom(int level, Control map) => level == 0
        ? Math.Min(ZoomLevels[0], Math.Min(MapSafeRect(map).Size.X, MapSafeRect(map).Size.Y) * 0.90f / MapSize)
        : ZoomLevels[level];

    private static bool ExpectedPlaceVisible(Dictionary row, int level)
    {
        var type = row.GetValueOrDefault("type", "wild").AsString();
        if (type == "sect")
        {
            var sectLevel = row.GetValueOrDefault("level", 2).AsInt32();
            return sectLevel == 1 || (sectLevel == 2 && level >= 1);
        }
        if (type == "city") return true;
        if (type is "town" or "market" or "village" or "dock") return level >= 1;
        return level >= 2;
    }

    private void VerifyZoomLevel(Control map, int level, Dictionary data, Dictionary placeViews, Dictionary transitViews)
    {
        var viewportSize = GetViewport().GetVisibleRect().Size;
        var expectedZoom = ExpectedZoom(level, map);
        Check(Level(map) == level, $"level{level} 实际档位是 {Level(map)}。");
        Check(Mathf.Abs(Zoom(map) - expectedZoom) < 0.001f, $"level{level} 倍率错误：期望 {expectedZoom:0.000}，实际 {Zoom(map):0.000}。");
        var world = map.GetNode<Node2D>("FixedScaleWorld");
        Check(Mathf.Abs(world.Scale.X - expectedZoom) < 0.001f, $"level{level} 地图根缩放不同步。");

        var nodes = data["nodes"].AsGodotArray<Dictionary>();
        var visiblePlaces = 0;
        foreach (var row in nodes)
        {
            var id = row["id"].AsString();
            var expected = ExpectedPlaceVisible(row, level);
            var marker = placeViews[id].AsGodotObject() as Control ?? throw new InvalidOperationException($"节点控件缺失：{id}。");
            Check(marker.Visible == expected, $"level{level} 的地点节点显隐错误：{id} 应为 {expected}，实际 {marker.Visible}。");
            if (expected) visiblePlaces++;
        }
        var expectedPlaceCount = level switch { 0 => 19, 1 => 53, _ => 123 };
        Check(visiblePlaces == expectedPlaceCount,
            $"level{level} 具名地点应显示 {expectedPlaceCount} 个，实际 {visiblePlaces} 个。");

        var transitExpected = level >= 2;
        foreach (var entry in transitViews)
        {
            var marker = entry.Value.AsGodotObject() as Button ?? throw new InvalidOperationException($"途经点控件缺失：{entry.Key}。");
            Check(marker.Visible == transitExpected, $"level{level} 途经点 {entry.Key} 显隐错误。");
        }

        var ink = map.Get("ink").AsGodotObject() ?? throw new InvalidOperationException("地图路线层缺失。");
        Check(ink.Get("paths_visible").AsBool() == transitExpected && ink.Get("marks_visible").AsBool() == transitExpected,
            $"level{level} 路线或途经标记显隐错误。");
        var badges = map.Get("population_badges").AsGodotObject() ?? throw new InvalidOperationException("人物数量标记层缺失。");
        var visibleBadgeIds = badges.Get("visible_node_ids").AsGodotDictionary();
        Check(visibleBadgeIds.Count == visiblePlaces + (transitExpected ? transitViews.Count : 0),
            $"level{level} 人数标记集合不匹配：{visibleBadgeIds.Count}。");

        var pawn = map.GetNode<Sprite2D>("FixedScaleWorld/PlayerNodeMark");
        Check(pawn.Texture is not null, "玩家棋子没有纹理。");
        var pawnScreenHeight = pawn.Texture!.GetHeight() * pawn.Scale.Y * world.Scale.Y;
        Check(Mathf.Abs(pawnScreenHeight - 32f) <= 0.6f, $"level{level} 玩家棋子尺寸错误：{pawnScreenHeight:0.00}px。");

        GD.Print($"BAICHUAN_ZOOM_LEVEL level={level} zoom={Zoom(map):0.00} places={visiblePlaces}/{nodes.Count} transit={(transitExpected ? transitViews.Count : 0)}/{transitViews.Count} pawn={pawnScreenHeight:0.0}px");
    }

    private void VerifyFarNameplateCompensation(Control map, Dictionary data, Dictionary placeViews, string viewportLabel)
    {
        var level = Level(map);
        Check(level == 0, $"{viewportLabel} 名牌补偿检查预期 level0，当前 level{level}。");
        var nodes = data["nodes"].AsGodotArray<Dictionary>();
        var zoom = Zoom(map);
        var safeRect = MapSafeRect(map);
        Check(!map.Get("_far_city_hover_fallback").AsBool(), $"{viewportLabel} 城市静态名牌退化为悬停提示。");
        var badges = map.Get("population_badges").AsGodotObject() ?? throw new InvalidOperationException("人物数量标记层缺失。");
        var counts = badges.Get("counts").AsGodotDictionary();
        var fontScales = badges.Get("font_scales").AsGodotDictionary();
        var anchors = badges.Get("anchors").AsGodotDictionary();
        var visibleRows = nodes.Where(row => ExpectedPlaceVisible(row, level)).ToArray();
        var labelRects = new List<Rect2>();
        var primaryCount = 0;
        var cityCount = 0;
        var smallestLabelFont = float.MaxValue;
        var smallestBadgeFont = float.MaxValue;
        foreach (var row in visibleRows)
        {
            var id = row["id"].AsString();
            var type = row.GetValueOrDefault("type", "").AsString();
            var isPrimary = type == "sect" && row.GetValueOrDefault("level", 0).AsInt32() == 1;
            var isCity = type == "city";
            Check(isPrimary || isCity, $"level0 不应显示非一级势力/城市名牌：{id} / {type}。");
            if (isPrimary) primaryCount++;
            if (isCity) cityCount++;

            var marker = placeViews[id].AsGodotObject() as Control ?? throw new InvalidOperationException($"{viewportLabel} 名牌控件缺失：{id}。");
            Check(marker.Visible, $"{viewportLabel} level0 名牌节点不可见：{id}。");
            var icon = marker.GetNode<Sprite2D>("NativeIcon");
            var hit = marker.GetNode<Button>("SelectIcon");
            var label = marker.GetNode<Button>("SelectPlace");
            var expectedIconFactor = isPrimary ? Math.Min(1.65f, 1.0f / zoom) : Math.Min(2.0f, 1.0f / zoom);
            var baseIconScale = icon.GetMeta("zoom_base_scale").AsVector2();
            var baseLabelScale = label.GetMeta("zoom_base_scale").AsVector2();
            Check(icon.Scale.DistanceTo(baseIconScale * expectedIconFactor) <= 0.02f,
                $"{viewportLabel} {(isPrimary ? "一级宗门" : "城市")}图标补偿错误：{id}倍率 {icon.Scale / baseIconScale}，应为 {expectedIconFactor:0.00}。");
            Check(hit.Visible && safeRect.Encloses(hit.GetGlobalRect()),
                $"{viewportLabel} 节点点击区超出HUD安全区：{id} / {hit.GetGlobalRect()} safe={safeRect}。");
            Check(label.Visible, $"{viewportLabel} 静态名牌被隐藏：{id}。");
            var labelFactor = (float)fontScales.GetValueOrDefault(id, 1.0).AsDouble();
            var baseFontSize = label.GetThemeFontSize("font_size");
            var targetFontPixels = isPrimary ? 15.0f : 14.0f;
            var targetFactor = Math.Min(6.5f, targetFontPixels / (baseFontSize * zoom));
            Check(labelFactor >= 2.48f && labelFactor <= targetFactor + 0.02f,
                $"{viewportLabel} 名牌补償倍率异常：{id} factor={labelFactor:0.00}, target={targetFactor:0.00}。");
            Check(label.Scale.DistanceTo(baseLabelScale * labelFactor) <= 0.02f,
                $"{viewportLabel} 名牌控件与字号补偿不同步：{id}。");
            var screenFontSize = baseFontSize * labelFactor * zoom;
            smallestLabelFont = Math.Min(smallestLabelFont, screenFontSize);
            Check(screenFontSize >= 10.0f,
                $"{viewportLabel} 名牌字号低于10px：{id} 当前 {screenFontSize:0.0}px。");
            var labelRect = label.GetGlobalRect();
            Check(safeRect.Encloses(labelRect),
                $"{viewportLabel} 名牌被HUD安全区挤出：{id} rect={labelRect} safe={safeRect}。");
            foreach (var prior in labelRects)
                Check(!labelRect.Intersects(prior), $"{viewportLabel} 名牌互相遮挡：{id} rect={labelRect} prior={prior}。");
            labelRects.Add(labelRect);

            if (!anchors.ContainsKey(id) || counts.GetValueOrDefault(id, 0).AsInt32() <= 0) continue;
            var badgeScale = (float)fontScales.GetValueOrDefault(id, 1.0).AsDouble();
            var badgeFontSize = Math.Max(1, Mathf.RoundToInt(12.0f * badgeScale));
            var countText = counts[id].AsInt32().ToString();
            var badgeWidth = Math.Max(18.0f * badgeScale,
                ThemeDB.FallbackFont.GetStringSize(countText, HorizontalAlignment.Left, -1, badgeFontSize).X + 8.0f * badgeScale);
            var badgeAnchor = anchors[id].AsVector2();
            var badgeScreenRect = new Rect2(
                (badgeAnchor - Camera(map)) * zoom,
                new Vector2(badgeWidth, 18.0f * badgeScale) * zoom);
            Check(safeRect.Encloses(badgeScreenRect),
                $"{viewportLabel} 人数牌被HUD遮挡：{id} rect={badgeScreenRect} safe={safeRect}。");
            smallestBadgeFont = Math.Min(smallestBadgeFont, badgeFontSize * zoom);
        }
        Check(primaryCount == 3 && cityCount == 16 && labelRects.Count == 19,
            $"{viewportLabel} 远景名牌集合错误：一级势力{primaryCount}、城市{cityCount}、名牌{labelRects.Count}。");
        Check(smallestBadgeFont == float.MaxValue || smallestBadgeFont >= 7.0f,
            $"{viewportLabel} 人数牌字号过小：{smallestBadgeFont:0.0}px。");
        GD.Print($"BAICHUAN_FAR_NAMEPLATE_COMPENSATION viewport={viewportLabel} zoom={zoom:0.0000} primary={primaryCount} cities={cityCount} labels={labelRects.Count} minLabel={smallestLabelFont:0.0}px minBadge={smallestBadgeFont:0.0}px hudSafe=true collisionFree=true");
    }

    private async Task VerifyFarCityHitAndSelection(Control map, Dictionary data, Dictionary placeViews, string viewportLabel)
    {
        var city = FindFarthestCity(map, data);
        var id = city["id"].AsString();
        var marker = placeViews[id].AsGodotObject() as Control ?? throw new InvalidOperationException($"城市控件缺失：{id}。");
        var hit = marker.GetNode<Button>("SelectIcon");
        var clickPoint = hit.GetGlobalRect().GetCenter();
        await MoveMouse(clickPoint);
        Check(GetViewport().GuiGetHoveredControl() == hit, $"{viewportLabel} 悬停未命中可见城市节点：{id}。");
        Check(map.Get("hover_id").AsString() == id, $"{viewportLabel} 悬停没有激活城市标签：{id}。");
        await Click(clickPoint, true);
        await Click(clickPoint, false);
        Check(map.Get("selected_node").AsString() == id, $"{viewportLabel} 城市点击没有选择节点：{id}。");
        GD.Print($"BAICHUAN_FAR_CITY_POINTER viewport={viewportLabel} id={id} hover=true click=true");
    }

    private static Dictionary FindFarthestCity(Control map, Dictionary data)
    {
        var currentNode = map.Call("get_current_node").AsString();
        Dictionary? farthest = null;
        var longestHours = -1.0;
        foreach (var row in data["nodes"].AsGodotArray<Dictionary>())
        {
            if (row.GetValueOrDefault("type", "").AsString() != "city") continue;
            var id = row["id"].AsString();
            if (id == currentNode) continue;
            var runtimeId = map.Call("get_runtime_id", id).AsString();
            if (string.IsNullOrWhiteSpace(runtimeId)) continue;
            var route = map.Call("get_assigned_route_to_node", runtimeId).AsGodotArray<string>();
            if (route.Count < 2) continue;
            var hours = map.Call("get_path_travel_hours", route).AsDouble();
            if (hours <= longestHours) continue;
            longestHours = hours;
            farthest = row;
        }
        return farthest ?? throw new InvalidOperationException("没有找到可测试的远距离城邑。");
    }

    private async Task VerifySelectedLocationActions(MainScene main, Control map, Dictionary data,
        Dictionary placeViews, string viewportLabel, bool exerciseTravelAndEnter)
    {
        var currentNode = map.Call("get_current_node").AsString();
        var selectedNode = map.Get("selected_node").AsString();
        Check(!string.IsNullOrWhiteSpace(selectedNode) && selectedNode != currentNode,
            $"{viewportLabel} 动作卡测试没有选中远处地点。");

        var details = map.GetNode<Control>("WorldMapDetails");
        var actionRow = details.FindChild("WorldMapActionButtons", true, false) as Control
            ?? throw new InvalidOperationException($"{viewportLabel} 地点卡双按钮容器缺失。");
        var travelButton = map.Get("travel_button").AsGodotObject() as Button
            ?? throw new InvalidOperationException("正式地图前往按钮缺失。");
        var enterButton = map.Get("enter_button").AsGodotObject() as Button
            ?? throw new InvalidOperationException("正式地图进入按钮缺失。");
        var taskTracker = main.GetNodeOrNull<Control>("HUDLayer/MainHUD/TaskTracker");
        Check(taskTracker is not null && taskTracker.IsVisibleInTree(), $"{viewportLabel} 持续任务卡未显示，无法验证遮挡。");
        Check(details.IsVisibleInTree() && actionRow.IsVisibleInTree() && actionRow.GetChildCount() == 2,
            $"{viewportLabel} 地点卡/双按钮容器未显示或不是两个按钮。");
        Check(travelButton.IsVisibleInTree() && enterButton.IsVisibleInTree() && !travelButton.Disabled && enterButton.Disabled,
            $"{viewportLabel} 远处地点动作状态异常：travelVisible={travelButton.IsVisibleInTree()} travelDisabled={travelButton.Disabled} enterVisible={enterButton.IsVisibleInTree()} enterDisabled={enterButton.Disabled}。");

        var viewportRect = new Rect2(Vector2.Zero, GetViewport().GetVisibleRect().Size);
        var cardRect = details.GetGlobalRect();
        var taskRect = taskTracker!.GetGlobalRect();
        var mapSafeRect = map.Call("get_map_safe_rect").AsRect2();
        var travelRect = travelButton.GetGlobalRect();
        var enterRect = enterButton.GetGlobalRect();
        Check(viewportRect.Encloses(cardRect) && viewportRect.Encloses(travelRect) && viewportRect.Encloses(enterRect),
            $"{viewportLabel} 地点卡按钮超出窗口：card={cardRect} travel={travelRect} enter={enterRect} viewport={viewportRect}。");
        Check(!cardRect.Intersects(taskRect),
            $"{viewportLabel} 地点卡整体被持续任务卡覆盖：card={cardRect} task={taskRect}。");
        Check(!cardRect.Intersects(mapSafeRect),
            $"{viewportLabel} 地点卡与地图安全区重叠：card={cardRect} mapSafe={mapSafeRect}。");
        Check(!travelRect.Intersects(taskRect) && !enterRect.Intersects(taskRect),
            $"{viewportLabel} 行旅按钮被任务卡盖住：travel={travelRect} enter={enterRect} task={taskRect}。");

        var detailLabel = map.Get("detail_label").AsGodotObject() as Label
            ?? throw new InvalidOperationException("地点卡详情文字缺失。");
        Check(detailLabel.Text.Contains("路程", StringComparison.Ordinal) && detailLabel.Text.Length >= 14,
            $"{viewportLabel} 远处地点长详情没有进入卡片：{detailLabel.Text}。");

        var travelCenter = travelRect.GetCenter();
        await MoveMouse(travelCenter);
        Check(GetViewport().GuiGetHoveredControl() == travelButton,
            $"{viewportLabel} 前往按钮被遮挡或不可命中：hover={GetViewport().GuiGetHoveredControl()?.GetPath()} rect={travelRect} task={taskRect}。");
        GD.Print($"BAICHUAN_ACTION_CARD viewport={viewportLabel} card={cardRect} travel={travelRect} enter={enterRect} task={taskRect} mapSafe={mapSafeRect} visible=true hover=true longDetail=true");
        if (!exerciseTravelAndEnter) return;

        var cityId = selectedNode;
        var runtimeId = map.Call("get_runtime_id", cityId).AsString();
        var route = map.Call("get_assigned_route_to_node", runtimeId).AsGodotArray<string>();
        Check(route.Count > 1 && route[0] == currentNode, $"{viewportLabel} 远处地点没有行旅路线。");
        await Click(travelCenter, true);
        await Click(travelCenter, false);
        Check(map.Call("is_traveling").AsBool(), $"{viewportLabel} 点击前往按钮没有启动行旅。");
        map.Call("pause_travel_at_node");
        var travelDeadline = Time.GetTicksMsec() + 45000UL;
        while (map.Call("is_traveling").AsBool() && Time.GetTicksMsec() < travelDeadline) await Frames();
        Check(!map.Call("is_traveling").AsBool() && map.Call("get_current_node").AsString() == route[1],
            $"{viewportLabel} 点击前往按钮后没有完成第一段行旅：{map.Call("get_current_node")}。");

        // Put the test traveler at a known enterable city, then activate the other button through its real hit area.
        const string enterCityId = "bc_place_014";
        Check(map.Call("restore_node_state", new Dictionary
        {
            ["current_node"] = enterCityId,
            ["remaining_route"] = new Godot.Collections.Array<string>()
        }).AsBool(), "测试用的进入地点状态恢复失败。");
        typeof(MainScene).GetMethod("on_node_world_arrived", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(main, [enterCityId]);
        await Frames(3);
        Check(travelButton.IsVisibleInTree() && enterButton.IsVisibleInTree(),
            "到达地点后行旅动作按钮不再显示。");
        Check(enterButton.Disabled == false && travelButton.Disabled,
            $"到达当前地点后的动作状态错误：travelDisabled={travelButton.Disabled}, enterDisabled={enterButton.Disabled}。");
        var currentEnterRect = enterButton.GetGlobalRect();
        Check(!currentEnterRect.Intersects(taskTracker.GetGlobalRect()),
            $"{viewportLabel} 当前地点的进入按钮被任务卡盖住：enter={currentEnterRect} task={taskTracker.GetGlobalRect()}。");
        await MoveMouse(currentEnterRect.GetCenter());
        Check(GetViewport().GuiGetHoveredControl() == enterButton,
            $"{viewportLabel} 进入按钮被遮挡或不可命中：hover={GetViewport().GuiGetHoveredControl()?.GetPath()} rect={currentEnterRect}。");
        await Click(currentEnterRect.GetCenter(), true);
        await Click(currentEnterRect.GetCenter(), false);
        await Frames(6);
        Check(main.FindChild("WorldLocationInteriorPanel", true, false) is Control { Visible: true },
            $"{viewportLabel} 点击进入按钮没有打开地点界面。");
        // Opening a location intentionally frees the previous map instance. Keep the entry
        // click as the final interaction so later checks do not use that disposed reference.
        GD.Print($"BAICHUAN_ACTION_BUTTONS_CLICK viewport={viewportLabel} travel=true enter=true firstStep={route[1]} enterCity={enterCityId}");
    }

    private async Task VerifyLevelZeroTravelKeepsFullMap(MainScene main, Control map, Dictionary data, Dictionary placeViews, Vector2 viewportSize)
    {
        var currentNode = map.Call("get_current_node").AsString();
        var primaryTarget = data["nodes"].AsGodotArray<Dictionary>().FirstOrDefault(row =>
            row.GetValueOrDefault("type", "").AsString() == "sect" &&
            row.GetValueOrDefault("level", 0).AsInt32() == 1 &&
            row["id"].AsString() != currentNode);
        Check(primaryTarget is not null, "找不到可见的一级宗门行旅目标。");
        var runtimeId = map.Call("get_runtime_id", primaryTarget!["id"].AsString()).AsString();
        Check(!string.IsNullOrWhiteSpace(runtimeId), "一级宗门没有正式运行时地点ID。");
        var route = map.Call("get_assigned_route_to_node", runtimeId).AsGodotArray<string>();
        Check(route.Count > 1 && route[0] == currentNode, "一级宗门目标没有可行路线。");

        var firstStep = new Godot.Collections.Array<string> { route[0], route[1] };
        var travel = map.Get("travel").AsGodotObject() ?? throw new InvalidOperationException("正式地图行旅状态缺失。");
        Check(travel.Call("begin", firstStep).AsBool(), "level0 测试路线启动失败。");
        var cameraBefore = Camera(map);
        var pawn = map.GetNode<Sprite2D>("FixedScaleWorld/PlayerNodeMark");
        var pawnBefore = pawn.Position;
        map.Call("_run_travel");
        map.Call("pause_travel_at_node");
        var deadline = Time.GetTicksMsec() + 45000UL;
        while (map.Call("is_traveling").AsBool() && Time.GetTicksMsec() < deadline) await Frames();
        Check(!map.Call("is_traveling").AsBool(), "level0 测试行旅未在限时内抵达节点。");
        Check(map.Call("get_current_node").AsString() == route[1], "level0 测试行旅没有提交第一段节点移动。");
        Check(pawn.Position.DistanceTo(pawnBefore) > 0.5f, "level0 玩家棋子未沿路线移动。");
        Check(Level(map) == 0 && Camera(map).DistanceTo(cameraBefore) <= 0.5f,
            $"level0 行旅移动镜头跟随棋子：镜头 {cameraBefore} -> {Camera(map)}。");
        VerifyWholeMapFits(map, viewportSize, "1280×720 行旅后 level0");
        VerifyZoomLevel(map, 0, data, placeViews, map.Get("transit_views").AsGodotDictionary());
        GD.Print($"BAICHUAN_LEVEL0_TRAVEL camera_stable=true pawn_moved=true from={currentNode} to={route[1]}");
    }

    private async Task<bool> VerifyHiddenPlaceCannotBeClicked(Control map, int level, Dictionary data, Dictionary placeViews)
    {
        if (level >= 2) return true;
        var nodes = data["nodes"].AsGodotArray<Dictionary>();
        var world = map.GetNode<Node2D>("FixedScaleWorld");
        var viewport = GetViewport();
        var viewportSize = viewport.GetVisibleRect().Size;
        var originalCamera = Camera(map);
        var currentSelected = map.Get("selected_node").AsString();
        var title = map.Get("title_label").AsGodotObject() as Label ?? throw new InvalidOperationException("地点标题控件缺失。");
        var currentTitle = title.Text;

        foreach (var row in nodes)
        {
            if (ExpectedPlaceVisible(row, level)) continue;
            var id = row["id"].AsString();
            var marker = placeViews[id].AsGodotObject() as Control;
            if (marker is null) continue;
            var hiddenHit = marker.GetNodeOrNull<Button>("SelectIcon");
            if (hiddenHit is null) continue;

            // Recover the hidden node's map position from its current screen rect, then center it.
            var oldCenter = hiddenHit.GetGlobalRect().GetCenter();
            var nodeWorldPoint = Camera(map) + oldCenter / Zoom(map);
            map.Set("camera_pos", nodeWorldPoint - viewportSize * 0.5f / Zoom(map));
            map.Call("_update_camera");
            var clickPoint = viewportSize * 0.5f;
            await MoveMouse(clickPoint);
            var hovered = viewport.GuiGetHoveredControl();
            if (hovered is BaseButton { Visible: true }) continue;
            if (hovered is not null && hovered != map && !world.IsAncestorOf(hovered)) continue;

            await Click(clickPoint, true);
            await Click(clickPoint, false);
            Check(map.Get("selected_node").AsString() == currentSelected,
                $"level{level} 点击隐藏地点 {id} 后改变了选择：{map.Get("selected_node").AsString()}。");
            Check(title.Text == currentTitle, $"level{level} 点击隐藏地点 {id} 后打开了地点详情。");
            GD.Print($"BAICHUAN_HIDDEN_NODE_CLICK level={level} id={id} hovered={(hovered is null ? "null" : hovered.GetPath().ToString())} unchanged=true");
            map.Set("camera_pos", originalCamera);
            map.Call("_update_camera");
            return true;
        }

        return false;
    }

    private async Task CaptureLevel(int level)
    {
        await Frames(3);
        using var image = GetViewport().GetTexture().GetImage();
        Check(image is not null && !image.IsEmpty(), $"level{level} 画面采集为空。");
        var path = $"{CaptureFolder}/level_{level}_{image!.GetWidth()}x{image.GetHeight()}.png";
        Check(image.SavePng(path) == Error.Ok, $"level{level} 截图保存失败：{path}。");
    }

    public override void _Ready() => CallDeferred(nameof(Run));

    private async void Run()
    {
        MainScene? main = null;
        try
        {
            VerifyIsolatedUserData();
            var folder = ProjectSettings.GlobalizePath(CaptureFolder);
            var folderError = DirAccess.MakeDirRecursiveAbsolute(folder);
            Check(folderError is Error.Ok or Error.AlreadyExists, $"无法建立截图目录：{folderError}。");

            main = GD.Load<PackedScene>("res://tscn/scene/MainScene.tscn").Instantiate<MainScene>();
            AddChild(main);
            var startupDeadline = Time.GetTicksMsec() + 90000UL;
            while (!main.StartupComplete && Time.GetTicksMsec() < startupDeadline) await Frames();
            Check(main.StartupComplete && main.StartupError is null, "正式主场景启动失败：" + main.StartupError);
            typeof(MainScene).GetField("_skipExitSave", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(main, true);
            var stage = typeof(MainScene).GetField("_qingxi_prologue", BindingFlags.Instance | BindingFlags.NonPublic)!;
            stage.SetValue(main, Enum.Parse(stage.FieldType, "Departed"));
            typeof(MainScene).GetMethod("open_world", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(main, []);
            await Frames(10);

            var map = main.GetNode<Control>("MapLayer/BaiChuanNodeWorld");
            Check(map.IsVisibleInTree() && map.IsInGroup("node_world_map_presentation"), "正式百川节点地图未显示。");
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
            DisplayServer.WindowSetSize(new Vector2I(1920, 1080));
            await Frames(8);
            var viewportSize = GetViewport().GetVisibleRect().Size;
            Check(viewportSize.X >= 1280 && viewportSize.Y >= 720, $"测试视口尺寸异常：{viewportSize}。");

            var data = map.Get("data").AsGodotDictionary();
            var placeViews = map.Get("place_views").AsGodotDictionary();
            var transitViews = map.Get("transit_views").AsGodotDictionary();
            Check(data["nodes"].AsGodotArray<Dictionary>().Count == 123, "正式地点目录不是123个节点。");
            Check(data["transitNodes"].AsGodotArray<Dictionary>().Count == 321, "正式途经点目录不是321个节点。");
            Check(placeViews.Count == 123 && transitViews.Count == 321, "正式地图地点/途经点控件数量不匹配。");
            Check(Level(map) == 2 && Mathf.Abs(Zoom(map) - 0.96f) < 0.001f,
                $"正式地图默认档应为 level2/0.96，当前 {Level(map)}/{Zoom(map):0.00}。");

            var anchor = new Vector2(viewportSize.X * 0.36f, viewportSize.Y * 0.68f);
            // Keep a stable world landmark under the pointer while scrolling through every level.
            map.Set("camera_pos", Vector2.One * (MapSize * 0.5f) - anchor / Zoom(map));
            map.Call("_update_camera");
            await MoveMouse(anchor);
            var anchorWorld = Camera(map) + anchor / Zoom(map);
            Check(await VerifyHiddenPlaceCannotBeClicked(map, 2, data, placeViews), "level2 不应有隐藏的具名地点。");

            for (var level = 2; level > 0; level--)
            {
                var before = Level(map);
                var beforeAnchor = Camera(map) + anchor / Zoom(map);
                await Wheel(map, MouseButton.WheelDown, anchor, true);
                Check(Level(map) == before - 1, $"滚轮按下未恰好缩小一档：{before} -> {Level(map)}。");
                var afterPress = Level(map);
                await Wheel(map, MouseButton.WheelDown, anchor, false);
                Check(Level(map) == afterPress, $"滚轮释放重复触发缩放：level{afterPress} -> {Level(map)}。");
                var afterAnchor = Camera(map) + anchor / Zoom(map);
                if (afterPress == 0)
                {
                    VerifyWholeMapFits(map, viewportSize, "1920×1080 level0");
                    anchorWorld = afterAnchor;
                }
                else
                    Check(afterAnchor.DistanceTo(beforeAnchor) <= 5.0f,
                        $"level{before}->{afterPress} 滚轮锚点漂移 {afterAnchor.DistanceTo(beforeAnchor):0.00} 地图像素。");
                VerifyZoomLevel(map, afterPress, data, placeViews, transitViews);
                Check(await VerifyHiddenPlaceCannotBeClicked(map, afterPress, data, placeViews), $"level{afterPress} 未找到可验证的隐藏地点。");
                if (afterPress == 0) VerifyFarNameplateCompensation(map, data, placeViews, "1920×1080 level0");
                if (afterPress == 0) VerifyWholeMapFits(map, viewportSize, "1920×1080 level0");
                await CaptureLevel(afterPress);
            }

            for (var level = 0; level < 2; level++)
            {
                var before = Level(map);
                var beforeAnchor = Camera(map) + anchor / Zoom(map);
                await Wheel(map, MouseButton.WheelUp, anchor, true);
                Check(Level(map) == before + 1, $"滚轮按下未恰好放大一档：{before} -> {Level(map)}。");
                var afterPress = Level(map);
                await Wheel(map, MouseButton.WheelUp, anchor, false);
                Check(Level(map) == afterPress, $"滚轮释放重复触发缩放：level{afterPress} -> {Level(map)}。");
                var afterAnchor = Camera(map) + anchor / Zoom(map);
                Check(afterAnchor.DistanceTo(beforeAnchor) <= 5.0f,
                    $"level{before}->{afterPress} 滚轮锚点漂移 {afterAnchor.DistanceTo(beforeAnchor):0.00} 地图像素。");
                VerifyZoomLevel(map, afterPress, data, placeViews, transitViews);
                if (afterPress < 2)
                    Check(await VerifyHiddenPlaceCannotBeClicked(map, afterPress, data, placeViews), $"level{afterPress} 未找到可验证的隐藏地点。");
                await CaptureLevel(afterPress);
            }
            Check((Camera(map) + anchor / Zoom(map)).DistanceTo(anchorWorld) <= 15.0f,
                "连续缩放后锚点世界位置发生显著漂移。");

            // Scrolling over the floating information panel must leave zoom and camera unchanged.
            map.Call("_set_zoom_level", 2, viewportSize * 0.5f);
            var details = map.GetNode<Control>("WorldMapDetails");
            var panelRect = details.GetGlobalRect();
            var panelPoint = panelRect.Position + new Vector2(24f, Mathf.Max(20f, panelRect.Size.Y * 0.5f));
            await MoveMouse(panelPoint);
            var hoveredPanel = GetViewport().GuiGetHoveredControl();
            Check(hoveredPanel == details || hoveredPanel is not null && details.IsAncestorOf(hoveredPanel),
                $"面板滚轮测试点未命中面板：{hoveredPanel?.GetPath()}。");
            var panelLevel = Level(map);
            var panelCamera = Camera(map);
            await Wheel(map, MouseButton.WheelUp, panelPoint, true);
            await Wheel(map, MouseButton.WheelUp, panelPoint, false);
            Check(Level(map) == panelLevel && Camera(map).DistanceTo(panelCamera) <= 0.01f,
                "信息面板上的滚轮缩放了地图或移动了镜头。");

            // A drag on an unobstructed map point follows the pointer at the active zoom.
            map.Call("_set_zoom_level", 2, viewportSize * 0.5f);
            var dragStart = new Vector2(viewportSize.X * 0.34f, viewportSize.Y * 0.72f);
            await MoveMouse(dragStart);
            var beforeDrag = Camera(map);
            await Click(dragStart, true);
            Check(map.Get("_map_press_active").AsBool(), "地图拖拽按下没有建立手势。");
            var dragDelta = new Vector2(36, 22);
            await MoveMouse(dragStart + dragDelta, dragDelta, true);
            Check(Camera(map).DistanceTo(beforeDrag - dragDelta / Zoom(map)) <= 1.5f,
                "缩放后地图拖拽没有按倍率跟随指针。");
            await Click(dragStart + dragDelta, false);
            Check(!map.Get("_map_press_active").AsBool(), "拖拽释放后仍保留按压状态。");

            // Recheck the default tier after the interaction and leave the default view in the capture set.
            map.Call("_set_zoom_level", 2, viewportSize * 0.5f);
            VerifyZoomLevel(map, 2, data, placeViews, transitViews);
            await CaptureLevel(2);

            // The farthest tier adapts to viewport height so the full map fits at 1280x720.
            map.Call("_set_zoom_level", 0, viewportSize * 0.5f);
            DisplayServer.WindowSetSize(new Vector2I(1280, 720));
            await Frames(10);
            viewportSize = GetViewport().GetVisibleRect().Size;
            Check(viewportSize.X >= 1280 && viewportSize.Y >= 720, $"小屏测试视口尺寸异常：{viewportSize}。");
            VerifyZoomLevel(map, 0, data, placeViews, transitViews);
            VerifyWholeMapFits(map, viewportSize, "1280×720 level0");
            VerifyFarNameplateCompensation(map, data, placeViews, "1280×720 level0");
            await CaptureLevel(0);
            await VerifyFarCityHitAndSelection(map, data, placeViews, "1280×720 level0");
            await VerifySelectedLocationActions(main, map, data, placeViews, "1280×720", false);

            DisplayServer.WindowSetSize(new Vector2I(1920, 1080));
            await Frames(10);
            viewportSize = GetViewport().GetVisibleRect().Size;
            VerifyZoomLevel(map, 0, data, placeViews, transitViews);
            VerifyWholeMapFits(map, viewportSize, "1920×1080 resize back level0");
            VerifyFarNameplateCompensation(map, data, placeViews, "1920×1080 resize back level0");
            await VerifyFarCityHitAndSelection(map, data, placeViews, "1920×1080 resize back level0");
            await VerifySelectedLocationActions(main, map, data, placeViews, "1920×1080 resize back", false);
            DisplayServer.WindowSetSize(new Vector2I(1280, 720));
            await Frames(10);
            viewportSize = GetViewport().GetVisibleRect().Size;
            VerifyZoomLevel(map, 0, data, placeViews, transitViews);
            VerifyWholeMapFits(map, viewportSize, "1280×720 resize again level0");
            VerifyFarNameplateCompensation(map, data, placeViews, "1280×720 resize again level0");
            await VerifyFarCityHitAndSelection(map, data, placeViews, "1280×720 resize again level0");
            await VerifySelectedLocationActions(main, map, data, placeViews, "1280×720 resize again", false);

            await VerifyLevelZeroTravelKeepsFullMap(main, map, data, placeViews, viewportSize);
            await CaptureLevel(0);
            await VerifySelectedLocationActions(main, map, data, placeViews, "1280×720 final click", true);
            GD.Print($"BAICHUAN_FORMAL_MAP_ZOOM_SMOKE_OK default=2/{ZoomLevels[2]:0.00} screenshots={ProjectSettings.GlobalizePath(CaptureFolder)}");
            main.QueueFree();
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError("BAICHUAN_FORMAL_MAP_ZOOM_SMOKE_FAILED: " + error);
            if (main is not null && GodotObject.IsInstanceValid(main))
            {
                typeof(MainScene).GetField("_skipExitSave", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(main, true);
                main.QueueFree();
            }
            GetTree().Quit(1);
        }
    }
}
