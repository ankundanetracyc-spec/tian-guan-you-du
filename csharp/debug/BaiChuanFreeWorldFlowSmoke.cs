using Godot;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

/// <summary>Formal free-world flow acceptance. All saves and windows are isolated from the player's session.</summary>
public partial class BaiChuanFreeWorldFlowSmoke : Node
{
    private const string Folder = "res://output/bai_chuan_terrain_speed_20261105/qa";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly System.Collections.Generic.List<string> _results = [];
    private int _checks, _failures;
    private MainScene? _main;
    private Control _map = null!;
    private GodotObject _nav = null!, _graph = null!, _terrain = null!;
    private GDictionary _terrainData = null!;
    private string _runName = "flow";
    private GameTimeMgr _time = null!;
    private NpcWorldManager _world = null!;
    private double _kmPerPixel;

    private static object? Invoke(object owner, string method, params object[] args) => owner.GetType().GetMethod(method, Private)!.Invoke(owner, args);
    private static object? Field(object owner, string name) => owner.GetType().GetField(name, Private)!.GetValue(owner);
    private static void Set(object owner, string name, object? value) => owner.GetType().GetField(name, Private)!.SetValue(owner, value);
    private void Check(bool ok, string message)
    {
        _checks++;
        if (!ok) _failures++;
        var line = (ok ? "PASS " : "FAIL ") + message;
        _results.Add(line); GD.Print(line);
        WriteReport();
    }
    private void Require(bool ok, string message) { Check(ok, message); if (!ok) throw new InvalidOperationException(message); }
    private void WriteReport()
    {
        using var file = Godot.FileAccess.Open(Folder + "/" + _runName + "_report.txt", Godot.FileAccess.ModeFlags.Write);
        file?.StoreString(string.Join("\n", _results) + $"\nCHECKS {_checks} FAILURES {_failures}\n");
    }
    private async Task Frames(int count = 1) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private Vector2 Position => _map.Call("get_world_position").AsVector2();
    private double Remaining => _map.Call("_remaining_length").AsDouble();
    private double RemainingCost => _map.Call("_remaining_cost_length").AsDouble();
    private bool Moving => _map.Call("is_traveling").AsBool();
    private GDictionary State => _map.Call("get_node_state").AsGodotDictionary();
    private GDictionary Positions => _graph.Get("positions").AsGodotDictionary();
    private bool Pending => (bool)Invoke(_main!, "has_pending_world_encounter")!;
    private bool Navigate(Vector2 point, string id = "") => _map.Call("navigate_to_world", point, id).AsBool();
    private void Stop() => _map.Call("stop_free_travel");
    private GDictionary Surface(Vector2 point) => _terrain.Call("terrain_at", point).AsGodotDictionary();
    private double SegmentCost(Vector2 from, Vector2 to) => _terrain.Call("segment_cost_length", from, to).AsDouble();
    private double HoursForCost(double cost, float speed = 100) => _graph.Call("hours_for_km", cost * _kmPerPixel, speed).AsDouble();
    private double CurrentPathCost()
    {
        var previous = Position;
        var cost = 0.0;
        foreach (var raw in State["waypoints"].AsGodotArray())
        {
            var point = raw.AsGodotArray();
            var next = new Vector2(point[0].AsSingle(), point[1].AsSingle());
            cost += SegmentCost(previous, next); previous = next;
        }
        return cost;
    }

    private void VerifyIsolation()
    {
        var expected = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--expected-appdata="))?[19..] ?? "";
        static string Normalize(string p) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(p));
        Require(expected.Length > 0 && Normalize(OS.GetEnvironment("APPDATA")) == Normalize(expected), "隔离 APPDATA 必须与命令参数一致");
        var custom = ProjectSettings.GetSetting("application/config/custom_user_dir_name").AsString();
        Require(Normalize(OS.GetUserDataDir()) == Normalize(Path.Combine(expected, custom)), "user:// 只指向隔离目录");
    }

    private async Task StartMain(PersistentGameSave? saved = null)
    {
        if (_main is not null && IsInstanceValid(_main))
        {
            Set(_main, "_skipExitSave", true); _main.QueueFree(); _main = null; await Frames(3);
            _map = null!; _graph = null!; _nav = null!;
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        }
        if (saved is not null) Set(GetNode<GameSaveManager>("/root/GameSaveManager"), "_pendingLoad", saved);
        _main = GD.Load<PackedScene>("res://tscn/scene/MainScene.tscn").Instantiate<MainScene>();
        Set(_main, "_skipExitSave", true);
        AddChild(_main);
        var deadline = Time.GetTicksMsec() + 90000UL;
        while (!_main.StartupComplete && Time.GetTicksMsec() < deadline) await Frames();
        Require(_main.StartupComplete && _main.StartupError is null, "真实 MainScene 完成启动：" + _main.StartupError);
        var stage = typeof(MainScene).GetField("_qingxi_prologue", Private)!;
        stage.SetValue(_main, Enum.Parse(stage.FieldType, "Departed"));
        if (Field(_main, "_baishiLecture") is BaishiLectureProgress lecture) lecture.departed_qingxi = true;
        (Field(_main, "story_dialogue") as Control)?.Hide();
        Invoke(_main, "open_world"); await Frames(8);
        _map = _main.GetNode<Control>("MapLayer/BaiChuanNodeWorld");
        Require(_map.IsVisibleInTree() && _map.HasMethod("navigate_to_world"), "正式地图已接自由行旅运行时");
        _nav = _map.Get("navigation").AsGodotObject();
        _terrain = _map.Get("terrain_travel").AsGodotObject();
        var terrainConstants = GD.Load<Script>("res://gdscripts/world/BaiChuanTerrainTravel.gd").GetScriptConstantMap();
        _terrainData = Json.ParseString(Godot.FileAccess.GetFileAsString(terrainConstants["DATA_PATH"].AsString())).AsGodotDictionary();
        Require(_terrain.Get("ready").AsBool() && _terrainData["profile_id"].AsString() == terrainConstants["PROFILE_ID"].AsString(), "正式地图读取当前可调地形速度配置");
        _graph = _map.Get("travel").AsGodotObject().Get("graph").AsGodotObject();
        _time = GetNode<GameTimeMgr>("/root/GameTimeManager");
        _world = GetNode<NpcWorldManager>("/root/NpcWorldManager");
        _world.set_simulation_enabled(false);
        Require((bool)Invoke(_main, "initialize_road_encounter_clock")!, "正式遭遇时钟初始化");
        ((GodotObject)Field(_main, "_roadEncounterClock")!).Set("distance_until_encounter", 1e9);
        var edgePoints = _graph.Get("edge_points").AsGodotDictionary();
        var edgeKm = _graph.Get("edge_km").AsGodotDictionary();
        var edge = edgePoints.Keys.First();
        var points = edgePoints[edge].AsVector2Array();
        var length = 0.0;
        for (var i = 1; i < points.Length; i++) length += points[i - 1].DistanceTo(points[i]);
        _kmPerPixel = edgeKm[edge].AsDouble() / length;
    }

    private void AtAnchor(string id = "bc_place_001")
    {
        _map.Call("set_encounter_blocked", false);
        _map.Call("interrupt_assigned_route");
        Require(_map.Call("restore_node_state", new GDictionary { ["current_node"] = id, ["remaining_route"] = new GArray() }).AsBool(), "旧节点锚点可恢复 " + id);
        _map.Call("set_dun_speed", 100f);
        _time.resume_time();
    }

    private void AtPosition(Vector2 position)
    {
        Stop();
        var state = State;
        state["world_position"] = new GArray { position.X, position.Y };
        state["waypoints"] = new GArray(); state["resume_movement"] = false;
        state["movement_mode"] = "free"; state["remaining_route"] = new GArray();
        Require(_map.Call("restore_node_state", state).AsBool(), "连续野外坐标可恢复");
        _map.Call("_refresh_people");
    }

    private Vector2 GroundTarget(float minLength = 320)
    {
        foreach (var offset in new[] { new Vector2(420, 0), new Vector2(-420, 0), new Vector2(0, 420), new Vector2(0, -420), new Vector2(400, 250), new Vector2(-400, -250) })
        {
            var point = Position + offset;
            var path = _nav.Call("find_path", Position, point).AsVector2Array();
            if (path.Length > 1 && _nav.Call("path_length", path).AsDouble() >= minLength) return point;
        }
        foreach (var entry in Positions)
        {
            var point = entry.Value.AsVector2();
            var path = _nav.Call("find_path", Position, point).AsVector2Array();
            if (path.Length > 1 && _nav.Call("path_length", path).AsDouble() > minLength) return point;
        }
        throw new InvalidOperationException("未找到可达测试目的地");
    }

    private async Task MoveDistance(double distance, int timeoutMs = 15000)
    {
        var start = Remaining;
        var deadline = Time.GetTicksMsec() + (ulong)timeoutMs;
        while (Moving && start - Remaining < distance && Time.GetTicksMsec() < deadline) await Frames();
        Require(start - Remaining >= Math.Min(distance, start) - 1, "真实逐帧移动达到测试距离");
    }

    private async Task FinishFast(int iterations = 250)
    {
        var deadline = Time.GetTicksMsec() + 90000UL;
        for (var i = 0; i < iterations && Moving && Time.GetTicksMsec() < deadline; i++)
        {
            _map.Call("_advance_motion", 500f);
            while (_map.Get("_clock_waiting").AsBool() && Time.GetTicksMsec() < deadline) await Frames();
        }
    }

    private async Task Capture(string name)
    {
        if (DisplayServer.GetName() == "headless") { GD.Print("SKIP native window screenshot in headless compatibility run: " + name); return; }
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        Check(image.SavePng(Folder + "/" + name + ".png") == Error.Ok, "真实窗口截图 " + name);
    }

    private async Task MouseButton(Vector2 point, MouseButton button, bool pressed)
    {
        Input.ParseInputEvent(new InputEventMouseButton { Position = point, ButtonIndex = button, Pressed = pressed, ButtonMask = pressed && button == Godot.MouseButton.Left ? MouseButtonMask.Left : 0 });
        await Frames(2);
    }

    private async Task Motion(Vector2 point, Vector2 relative, bool held = false)
    {
        Input.ParseInputEvent(new InputEventMouseMotion { Position = point, Relative = relative, ButtonMask = held ? MouseButtonMask.Left : 0 }); await Frames(2);
    }

    private sealed record TerrainLeg(string Code, Vector2 Start, Vector2 End);

    private TerrainLeg FindTerrainLeg(string code, float length = 30)
    {
        var rows = _terrainData["rows"].AsGodotArray().Select(v => v.AsString()).ToArray();
        var cell = _terrainData["cell_size"].AsSingle();
        var home = Positions["bc_place_001"].AsVector2();
        var candidates = new System.Collections.Generic.List<TerrainLeg>();
        for (var y = 3; y < rows.Length - 3; y++)
        for (var x = 3; x < rows[y].Length - 3; x++)
        {
            if (rows[y][x].ToString() != code) continue;
            var start = new Vector2((x + .5f) * cell, (y + .5f) * cell);
            if (length < cell) start.X -= length * .5f;
            var end = start + Vector2.Right * length;
            var endCell = Mathf.FloorToInt(end.X / cell);
            if (endCell >= rows[y].Length || Enumerable.Range(x, endCell - x + 1).Any(col => rows[y][col].ToString() != code)) continue;
            candidates.Add(new TerrainLeg(code, start, end));
        }
        foreach (var candidate in candidates.OrderBy(p => p.Start.DistanceSquaredTo(home)).Take(100))
        {
            if (!_nav.Call("segment_clear", candidate.Start, candidate.End).AsBool()) continue;
            var approach = _nav.Call("find_path", home, candidate.Start).AsVector2Array();
            if (approach.Length < 2 || !_nav.Call("validate_path", approach).AsBool()) continue;
            var id = _terrainData["types"].AsGodotDictionary()[code].AsGodotDictionary()["id"].AsString();
            var clear = Enumerable.Range(0, 61).All(i => Surface(candidate.Start.Lerp(candidate.End, i / 60f))["id"].AsString() == id);
            if (!clear) continue;
            Check(true, $"自动选择可从正式入口抵达的{id}同类短段 {candidate.Start} → {candidate.End}");
            return candidate;
        }
        throw new InvalidOperationException("没有找到连续且可达的同类地形短段 " + code);
    }

    private TerrainLeg FindTerrainBoundary()
    {
        var rows = _terrainData["rows"].AsGodotArray().Select(v => v.AsString()).ToArray();
        var types = _terrainData["types"].AsGodotDictionary();
        var cell = _terrainData["cell_size"].AsSingle();
        var home = Positions["bc_place_001"].AsVector2();
        var candidates = new System.Collections.Generic.List<TerrainLeg>();
        for (var y = 3; y < rows.Length - 3; y++)
        for (var x = 3; x < rows[y].Length - 3; x++)
        {
            var from = rows[y][x].ToString(); var to = rows[y][x + 1].ToString();
            if (from == "_" || to == "_" || from == to) continue;
            if (types[from].AsGodotDictionary()["multiplier"].AsDouble() == types[to].AsGodotDictionary()["multiplier"].AsDouble()) continue;
            candidates.Add(new TerrainLeg(from + "→" + to, new Vector2((x + .2f) * cell, (y + .5f) * cell), new Vector2((x + 1.8f) * cell, (y + .5f) * cell)));
        }
        foreach (var candidate in candidates.OrderBy(p => p.Start.DistanceSquaredTo(home)).Take(100))
        {
            if (!_nav.Call("segment_clear", candidate.Start, candidate.End).AsBool()) continue;
            var approach = _nav.Call("find_path", home, candidate.Start).AsVector2Array();
            if (approach.Length > 1 && _nav.Call("validate_path", approach).AsBool()) return candidate;
        }
        throw new InvalidOperationException("没有找到可达的真实地形交界短段");
    }

    private void CheckTerrainStatus(GDictionary surface, bool eta = true)
    {
        var status = ((Label)_map.Get("travel_status").AsGodotObject()).Text;
        var percent = Math.Round(surface["multiplier"].AsDouble() * 100, MidpointRounding.AwayFromZero);
        Check(status.Contains(surface["name"].AsString()) && status.Contains($"速度{percent}%"), "顶部显示当前位置地形及配置速度比例：" + status);
        if (!eta) return;
        var expected = HoursForCost(CurrentPathCost(), _map.Get("dun_speed").AsSingle());
        Check(Math.Abs(_map.Call("_remaining_hours").AsDouble() - expected) < .0001, "剩余ETA按完整剩余路径的地形成本计算");
        Check(status.Contains(expected.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " 小时"), "顶部ETA与加权剩余小时一致");
    }

    private void VerifyTerrainSources()
    {
        var sources = _terrainData["source_files"].AsGodotDictionary();
        Require(sources.Count >= 3, "地形速度配置记录原底图、导航和地点来源");
        foreach (var source in sources)
        {
            var path = "res://" + source.Key.AsString().Replace('\\', '/');
            Check(Godot.FileAccess.GetSha256(path).Equals(source.Value.AsString(), StringComparison.OrdinalIgnoreCase), "原文件保持校验值 " + path);
        }
        var scale = GD.Load<Script>("res://gdscripts/world/BaiChuanWorldScale.gd").GetScriptConstantMap();
        Check((scale["ART_BASE"].AsString() + scale["MASTER_FILE"].AsString()).Contains("original_3x3_20261104"), "正式地图仍使用用户选定的原黄绿底图");
        Check(State["terrain_speed_profile"].AsString() == _terrainData["profile_id"].AsString(), "正式地图存档带当前地形速度版本");
    }

    private async Task<double> MeasureTerrainLeg(TerrainLeg leg, bool screenshot)
    {
        const float speed = 25;
        AtPosition(leg.Start); _map.Call("set_dun_speed", speed); _time.resume_time();
        _map.SetProcess(false);
        Require(Navigate(leg.End), "真实Main可开始地形短程 " + leg.Code);
        Require(Math.Abs(Remaining - leg.Start.DistanceTo(leg.End)) < .001, "同类地形使用相同直线路程，未绕路");
        var surface = Surface(leg.Start);
        var multiplier = surface["multiplier"].AsDouble();
        var initialCost = CurrentPathCost();
        Check(Math.Abs(initialCost - Remaining / multiplier) < .001, "同类短段成本读取配置倍率 " + surface["id"]);
        CheckTerrainStatus(surface);
        var hours = _time.get_precise_game_hours();
        var physicalKm = 0.0;
        var observer = Callable.From<double>(km => physicalKm += km);
        _map.Connect("distance_traveled", observer);
        var before = Time.GetTicksUsec(); var previousTick = before;
        var largestFrame = 0.0; var stalledSeconds = 0.0; var frames = 0;
        var deadline = Time.GetTicksMsec() + 20000UL;
        try
        {
            _map.SetProcess(true);
            while (Moving && Time.GetTicksMsec() < deadline)
            {
                await Frames(); frames++;
                var now = Time.GetTicksUsec();
                var elapsed = (now - previousTick) / 1000000.0;
                largestFrame = Math.Max(largestFrame, elapsed);
                // The production loop caps one frame at 0.1s; account only for
                // observed frame stalls when comparing real elapsed duration.
                stalledSeconds += Math.Max(0, elapsed - .1);
                previousTick = now;
            }
        }
        finally { _map.Disconnect("distance_traveled", observer); }
        var seconds = (Time.GetTicksUsec() - before) / 1000000.0;
        Require(!Moving && Position.DistanceTo(leg.End) < .002, "真实逐帧走完地形短段 " + surface["id"]);
        Check(frames >= 8, "地形速度采样包含多帧实际位移 " + surface["id"] + " frames=" + frames);
        var distance = leg.Start.DistanceTo(leg.End);
        var baseRate = GD.Load<Script>("res://gdscripts/world/BaiChuanFreeWorldRuntime.gd").GetScriptConstantMap()["WALK_PIXELS_PER_SECOND"].AsDouble() * speed / 100;
        var expectedSeconds = distance / (baseRate * multiplier);
        Check(Math.Abs(seconds - expectedSeconds) <= largestFrame * 2 + stalledSeconds + .01, "实测行走时长按配置倍率变化，仅容纳观测帧边界 " + surface["id"]);
        var expectedHours = HoursForCost(initialCost, speed);
        var actualHours = _time.get_precise_game_hours() - hours;
        Check(Math.Abs(actualHours - expectedHours) < .003, "实际游戏小时精确符合地形成本 " + surface["id"]);
        Check(Math.Abs(physicalKm - distance * _kmPerPixel) < .0005, "遭遇里程仍为真实公里，未累计减速成本 " + surface["id"]);
        GD.Print($"TERRAIN_SPEED_MEASUREMENT id={surface["id"]} distance={distance:R} multiplier={multiplier:R} frames={frames} elapsed_seconds={seconds:R} expected_seconds={expectedSeconds:R} max_frame_seconds={largestFrame:R} stalled_seconds={stalledSeconds:R} pixels_per_second={distance / seconds:R} cost={initialCost:R} actual_hours={actualHours:R} expected_hours={expectedHours:R} encounter_km={physicalKm:R}");
        _results.Add($"MEASUREMENT {surface["id"]} distance={distance:R} speed={distance / seconds:R}px/s elapsed={seconds:R}s expected={expectedSeconds:R}s game_hours={actualHours:R} expected_hours={expectedHours:R} multiplier={multiplier:R}");
        if (screenshot)
        {
            AtPosition(leg.Start); _map.Call("set_dun_speed", speed);
            _map.Call("_set_zoom_level", 2, GetViewport().GetVisibleRect().Size / 2); _map.Call("focus_player_node");
            Require(Navigate(leg.End), "实机截图开始真实地形行走 " + surface["id"]);
            await MoveDistance(7);
            Check(Moving && Position.DistanceTo(leg.Start) >= 6, "截图时正式主角已经移动且仍在行旅 " + surface["id"]);
            _map.Call("_update_status");
            CheckTerrainStatus(Surface(Position));
            await Capture("terrain_" + surface["id"].AsString() + "_1920");
            Stop();
        }
        return distance / seconds;
    }

    private async Task TerrainBoundary()
    {
        var leg = FindTerrainBoundary();
        AtPosition(leg.Start); _map.Call("set_dun_speed", 25f); _time.resume_time();
        var from = Surface(leg.Start); var to = Surface(leg.End);
        var before = _time.get_precise_game_hours();
        Require(Navigate(leg.End), "真实地形交界可直接通行 " + leg.Code);
        CheckTerrainStatus(from);
        var cost = CurrentPathCost();
        var deadline = Time.GetTicksMsec() + 10000UL;
        while (Moving && Surface(Position)["id"].AsString() == from["id"].AsString() && Time.GetTicksMsec() < deadline) await Frames();
        Require(Moving && Surface(Position)["id"].AsString() == to["id"].AsString(), "真实行走跨边界后切换到新地形");
        _time.pause_time();
        var point = Position; var hours = _time.get_precise_game_hours();
        var updateDeadline = Time.GetTicksMsec() + 350UL;
        while (Time.GetTicksMsec() < updateDeadline) await Frames();
        Check(point == Position && hours == _time.get_precise_game_hours(), "地形边界途中暂停不推进位置和游戏小时");
        CheckTerrainStatus(to);
        _time.resume_time();
        while (Moving && Time.GetTicksMsec() < deadline) await Frames();
        Require(!Moving && Position.DistanceTo(leg.End) < .002, "跨边界短段完整走完");
        Check(Math.Abs(_time.get_precise_game_hours() - before - HoursForCost(cost, 25)) < .003, "跨边界实际游戏小时逐段积分，未整帧套用单一倍率");
    }

    private async Task TerrainSaveAndPause(TerrainLeg leg)
    {
        AtPosition(leg.Start); _map.Call("set_dun_speed", 25f); _time.resume_time();
        var standing = Position; var standingHours = _time.get_precise_game_hours(); await Frames(10);
        Check(Position == standing && _time.get_precise_game_hours() == standingHours, "慢地形驻足不耗时");
        Require(Navigate(leg.End), "慢地形移动档短程开始");
        await MoveDistance(8); _time.pause_time();
        var point = Position; var cost = CurrentPathCost(); var hours = _time.get_precise_game_hours();
        await Frames(10);
        Check(Position == point && _time.get_precise_game_hours() == hours, "慢地形行走途中暂停不重放位移或时间");
        var saved = DiskRead(_main!.capture_battle_world_snapshot()!, "terrain_speed_moving");
        Check(saved.node_world_state["terrain_speed_profile"].AsString() == _terrainData["profile_id"].AsString(), "移动档写入地形配置版本");
        await StartMain(saved);
        _time.pause_time();
        Check(Position.DistanceTo(point) < .002 && Math.Abs(CurrentPathCost() - cost) < .002, "完整Main重建保留慢地形精确坐标及加权剩余路径");
        Check(Math.Abs(_time.get_precise_game_hours() - hours) < .0001, "完整Main读档不重算已经走过的地形耗时");
        Check(Moving, "慢地形移动档保留续走状态");
        Stop();
        var stopped = Position; var stoppedHours = _time.get_precise_game_hours();
        saved = DiskRead(_main!.capture_battle_world_snapshot()!, "terrain_speed_stopped");
        await StartMain(saved); _time.resume_time(); await Frames(10);
        Check(Position.DistanceTo(stopped) < .002 && !Moving && !State["resume_movement"].AsBool(), "完整Main读取停下档后不自动续走");
        Check(Math.Abs(_time.get_precise_game_hours() - stoppedHours) < .0001, "停下档重新读取不重放地形时间");
    }

    private async Task TerrainSpeed()
    {
        AtAnchor(); _world.set_simulation_enabled(false); _time.restore_calendar(1, 1, 2, 0, 0);
        VerifyTerrainSources();
        var plain = FindTerrainLeg("P"); var forest = FindTerrainLeg("F"); var hill = FindTerrainLeg("H");
        var plainSpeed = await MeasureTerrainLeg(plain, true);
        var forestSpeed = await MeasureTerrainLeg(forest, true);
        var hillSpeed = await MeasureTerrainLeg(hill, true);
        Check(plainSpeed > forestSpeed && forestSpeed > hillSpeed, "相同实际路程的实测速度顺序为平地、林地、山地");
        await MeasureTerrainLeg(FindTerrainLeg("C", 10), false);
        await TerrainBoundary();
        await TerrainSaveAndPause(forest);
        _map.Call("set_dun_speed", 100f);
    }

    private async Task Movement()
    {
        AtAnchor();
        Require(_map.Get("transit_views").AsGodotDictionary().Count == 0, "正式地图没有空节点控件");
        Check(_map.Get("place_views").AsGodotDictionary().Count == 123, "保留123个具名地点");
        var ink = _map.Get("ink").AsGodotObject();
        Check(!ink.Get("paths_visible").AsBool() && !ink.Get("marks_visible").AsBool(), "无空节点路网和标记绘制");
        var origin = Position;
        var target = GroundTarget();
        var before = _time.get_precise_game_hours();
        Require(Navigate(target), "空地目标可开始自由行旅");
        await MoveDistance(45);
        var stop = _map.FindChild("StopFreeTravel", true, false) as Button;
        Require(stop is not null && !stop.Disabled, "正式停下按钮可用");
        stop!.EmitSignal(Button.SignalName.Pressed);
        var point = Position;
        Check(!Moving && State["waypoints"].AsGodotArray().Count == 0 && !State["resume_movement"].AsBool(), "停下清除自动续走");
        Check(point.DistanceTo(origin) > 1 && point.DistanceTo(target) > 1, "主角停在途中连续位置");
        var stoppedHours = _time.get_precise_game_hours(); await Frames(8);
        GD.Print($"FREE_WORLD_STOP_DIAGNOSTIC position_before={point} position_after={Position} hours_before={stoppedHours:R} hours_after={_time.get_precise_game_hours():R} moving={Moving} clock_waiting={_map.Get("_clock_waiting").AsBool()}");
        Check(Position == point && _time.get_precise_game_hours() == stoppedHours, "驻足不移动也不推进时间");
        var redirect = GroundTarget();
        Require(Navigate(redirect), "途中坐标可重新指定方向");
        var length = Remaining; var costBefore = CurrentPathCost(); var hours = _time.get_precise_game_hours();
        await MoveDistance(35); _time.pause_time();
        var consumed = length - Remaining;
        var consumedCost = costBefore - CurrentPathCost();
        var expected = HoursForCost(consumedCost);
        GD.Print($"FREE_WORLD_TERRAIN_TIME pixels={consumed:R} cost_pixels={consumedCost:R} expected_hours={expected:R} actual_hours={_time.get_precise_game_hours() - hours:R}");
        Check(Math.Abs(_time.get_precise_game_hours() - hours - expected) < .003, "实际经过路径的地形积分与游戏小时一致，不结算未走路程");
        var paused = Position; var pausedTime = _time.get_precise_game_hours(); await Frames(10);
        Check(Position == paused && _time.get_precise_game_hours() == pausedTime, "历法暂停阻止行旅和耗时");
        _time.resume_time(); _map.Hide();
        var hidden = Position; var hiddenTime = _time.get_precise_game_hours(); await Frames(10);
        Check(Position == hidden && _time.get_precise_game_hours() == hiddenTime, "地图隐藏时不移动、不耗时");
        _map.Show(); await MoveDistance(15);
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Space, Pressed = true }); await Frames(2);
        Check(!Moving, "空格通过正式输入停下");
        Check(_time.get_precise_game_hours() > before, "真实行旅推进历法");
        _map.Call("_set_zoom_level", 2, GetViewport().GetVisibleRect().Size / 2); _map.Call("focus_player_node"); await Frames(3);
        await Capture("free_world_1920");
    }

    private async Task Pointer()
    {
        AtAnchor(); _map.Call("_set_zoom_level", 2, GetViewport().GetVisibleRect().Size / 2); _map.Call("focus_player_node"); await Frames(3);
        var screen = GetViewport().GetVisibleRect().Size;
        var point = new Vector2(screen.X * .68f, screen.Y * .61f);
        await Motion(point, Vector2.Zero);
        var before = _time.get_precise_game_hours(); var stationary = Position;
        var camera = _map.Get("camera_pos").AsVector2();
        await MouseButton(point, Godot.MouseButton.Left, true);
        var shift = new Vector2(55, 30);
        await Motion(point + shift, shift, true);
        Check(_map.Get("dragging").AsBool(), "真实指针建立地图拖动手势");
        Check(_map.Get("camera_pos").AsVector2().DistanceTo(camera) > 10, "驻足时地图拖动跟手");
        await MouseButton(point + shift, Godot.MouseButton.Left, false);
        GD.Print($"FREE_WORLD_DRAG_DIAGNOSTIC position_before={stationary} position_after={Position} hours_before={before:R} hours_after={_time.get_precise_game_hours():R} moving={Moving}");
        Check(!Moving && Position == stationary && _time.get_precise_game_hours() == before, "拖动浏览不误设目的地或推进时间");
        await MouseButton(point, Godot.MouseButton.WheelDown, true);
        await MouseButton(point, Godot.MouseButton.WheelDown, false);
        GD.Print($"FREE_WORLD_ZOOM_DIAGNOSTIC position_before={stationary} position_after={Position} hours_before={before:R} hours_after={_time.get_precise_game_hours():R} moving={Moving}");
        Check(!Moving && Position == stationary && _time.get_precise_game_hours() == before, "滚轮缩放不推进时间");
        _map.Call("_set_zoom_level", 2, screen / 2); _map.Call("focus_player_node"); await Frames(3);
        var groundClick = Vector2.Zero; var groundWorld = Vector2.Zero;
        var worldView = _map.GetNode<Node2D>("FixedScaleWorld");
        foreach (var screenPoint in new[] { new Vector2(screen.X * .68f, screen.Y * .61f), new Vector2(screen.X * .34f, screen.Y * .63f), new Vector2(screen.X * .64f, screen.Y * .40f) })
        {
            await Motion(screenPoint, Vector2.Zero);
            var hovered = GetViewport().GuiGetHoveredControl();
            if (hovered is Button || (hovered is not null && hovered != _map && hovered.MouseFilter != Control.MouseFilterEnum.Ignore)) continue;
            var candidate = worldView.GetGlobalTransformWithCanvas().AffineInverse() * screenPoint;
            var path = _nav.Call("find_path", Position, candidate).AsVector2Array();
            if (path.Length > 1 && _nav.Call("path_length", path).AsDouble() > 80) { groundClick = screenPoint; groundWorld = candidate; break; }
        }
        Require(groundClick != Vector2.Zero, "找到实际视口内无UI遮挡的可通行空地");
        var pressCamera = _map.Get("camera_pos").AsVector2();
        await MouseButton(groundClick, Godot.MouseButton.Left, true);
        var releaseWorld = worldView.GetGlobalTransformWithCanvas().AffineInverse() * groundClick;
        var releaseCamera = _map.Get("camera_pos").AsVector2();
        await MouseButton(groundClick, Godot.MouseButton.Left, false);
        Require(Moving, "真实鼠标点击空地直接开始自由行旅");
        var clickedRoute = State["waypoints"].AsGodotArray();
        var clickedEnd = clickedRoute[clickedRoute.Count - 1].AsGodotArray();
        var clickedPosition = new Vector2(clickedEnd[0].AsSingle(), clickedEnd[1].AsSingle());
        GD.Print($"FREE_WORLD_CLICK_DIAGNOSTIC screen={groundClick} candidate_world={groundWorld} release_world={releaseWorld} clicked_end={clickedPosition} camera_before_press={pressCamera} camera_before_release={releaseCamera}");
        Check(clickedPosition.DistanceTo(releaseWorld) < .01f, "空地点击使用释放时实际指针地图坐标作为终点");
        await MoveDistance(15); Stop();
        Require(Navigate(GroundTarget(450)), "行旅中拖图测试路线"); await MoveDistance(15);
        await MouseButton(point, Godot.MouseButton.Left, true);
        var dragStart = Position; var dragTime = _time.get_precise_game_hours();
        camera = _map.Get("camera_pos").AsVector2();
        await Motion(point + shift, shift, true); await Frames(6);
        Check(Position == dragStart && _time.get_precise_game_hours() == dragTime, "行旅中拖图暂挂位移与耗时");
        Check(_map.Get("camera_pos").AsVector2().DistanceTo(camera) > 10, "行旅中拖图不被主角跟随拉回");
        await MouseButton(point + shift, Godot.MouseButton.Left, false);
        Stop();
    }

    private async Task PopulationTravel()
    {
        AtAnchor();
        var count = _world.npcs.Count;
        var travelers = _world.npcs.Count(n => n.is_alive && n.is_traveling);
        _world.set_simulation_enabled(true);
        var clockBefore = _time.get_precise_game_hours();
        var started = Time.GetTicksMsec();
        try
        {
            Require(Navigate(GroundTarget()), "开启真实世界人口演算后可开始行旅");
            await MoveDistance(35, 30000);
            Stop();
            Check(_time.get_precise_game_hours() > clockBefore, "完整世界人口演算下行旅正常推进历法");
            GD.Print($"FREE_WORLD_POPULATION_TRAVEL npcs={count} traveling={travelers} moved_pixels=35 elapsed_ms={Time.GetTicksMsec() - started}");
        }
        finally { _world.set_simulation_enabled(false); Stop(); }
    }

    private PersistentGameSave DiskRead(PersistentGameSave save, string name)
    {
        var path = "user://" + name + ".tres";
        Require(ResourceSaver.Save(save, path) == Error.Ok, "完整世界快照写盘 " + name);
        var loaded = ResourceLoader.Load<PersistentGameSave>(path, "", ResourceLoader.CacheMode.IgnoreDeep);
        Require(loaded is not null, "完整世界快照重新读盘 " + name);
        return loaded!;
    }

    private static GDictionary PreviousCanvasState(GDictionary current)
    {
        var scale = GD.Load<Script>("res://gdscripts/world/BaiChuanWorldScale.gd").GetScriptConstantMap();
        var previous = current.Duplicate(true);
        var factor = scale["PREVIOUS_FREE_MAP_PIXELS"].AsDouble() / scale["MAP_PIXELS"].AsDouble();
        previous["world_size"] = scale["PREVIOUS_FREE_MAP_PIXELS"];
        previous["world_km"] = scale["WORLD_KM"];
        static void ScalePosition(GArray point, double ratio)
        {
            point[0] = point[0].AsDouble() * ratio;
            point[1] = point[1].AsDouble() * ratio;
        }
        ScalePosition(previous["world_position"].AsGodotArray(), factor);
        foreach (var row in previous["waypoints"].AsGodotArray()) ScalePosition(row.AsGodotArray(), factor);
        foreach (var row in previous["visible_encounters"].AsGodotArray()) ScalePosition(row.AsGodotDictionary()["position"].AsGodotArray(), factor);
        return previous;
    }

    private async Task SaveAndEncounter()
    {
        AtAnchor(); var target = GroundTarget(420);
        Require(Navigate(target), "移动存档测试路线");
        Require(_map.Call("spawn_visible_encounter", new GDictionary { ["id"] = "qa_visible_wildlife", ["kind"] = "wildlife", ["overshoot_km"] = 0.0 }).AsBool(), "正式运行时生成前方可见遭遇");
        Check(!Pending && Moving && !_map.Get("encounter_blocked").AsBool(), "事件标记可见但未接近时不强制弹窗");
        await MoveDistance(35); _time.pause_time();
        var savedPoint = Position; var savedRemaining = Remaining; var savedHours = _time.get_precise_game_hours();
        var loaded = DiskRead(_main!.capture_battle_world_snapshot()!, "free_world_moving");
        var raw = loaded.node_world_state["world_position"].AsGodotArray();
        Check(new Vector2(raw[0].AsSingle(), raw[1].AsSingle()) == savedPoint, "写盘保留当前帧精确地图坐标");
        Check(loaded.node_world_state["visible_encounters"].AsGodotArray().Count > 0, "可见遭遇位置与剩余路径一起写盘");
        loaded.node_world_state = PreviousCanvasState(loaded.node_world_state);
        loaded = DiskRead(loaded, "free_world_previous_canvas_moving");
        await StartMain(loaded);
        Check(Position.DistanceTo(savedPoint) < .002 && Math.Abs(Remaining - savedRemaining) < .002, "旧12288画布移动档重建后位置及剩余路径正确缩放");
        Check(Math.Abs(_time.get_precise_game_hours() - savedHours) < .0001, "读取位置不重算已走路程耗时");
        Check(Moving && _map.Get("hazards").AsGodotArray().Count > 0 && !Pending, "移动存档续走且可见事件保持未触发");
        _time.resume_time();
        var rows = _map.Get("hazards").AsGodotArray(); var hazard = rows[0].AsGodotDictionary()["position"].AsVector2();
        Stop();
        var detour = Vector2.Zero;
        foreach (var offset in new[] { new Vector2(0, 160), new Vector2(0, -160), new Vector2(-150, 70), new Vector2(150, -100) })
        {
            var candidate = Position + offset;
            var path = _nav.Call("find_path", Position, candidate).AsVector2Array();
            if (path.Length < 2) continue;
            var clear = true;
            for (var i = 1; i < path.Length; i++) if (Geometry2D.GetClosestPointToSegment(hazard, path[i - 1], path[i]).DistanceTo(hazard) <= 35) clear = false;
            if (clear) { detour = candidate; break; }
        }
        Require(detour != Vector2.Zero && Navigate(detour), "可见遭遇允许选择合法绕行路线");
        await MoveDistance(80); Stop();
        Check(!Pending && !_map.Get("encounter_blocked").AsBool(), "实际绕开事件未触发战斗或阻断");
        Require(Navigate(hazard), "主动接近仍在地图上的事件");
        var deadline = Time.GetTicksMsec() + 15000UL;
        while (!Pending && Time.GetTicksMsec() < deadline) await Frames();
        Require(Pending && !Moving && _map.Get("encounter_blocked").AsBool(), "接近可见事件才暂停并进入正式事件链");
        await Frames(4);
        var eventView = _main!.GetNode<ForestWolfEventView>("WorldEncounterLayer/WorldEncounterEvent");
        Check(eventView.Visible, "正式遭遇窗口显示");
        await Capture("visible_encounter_1920");
        eventView.GetNode<Button>("CloseEvent").EmitSignal(Button.SignalName.Pressed);
        Check(eventView.Visible && Pending, "关闭按钮不越过未解决遭遇");
        var leave = eventView.FindChild("Choice_leave", true, false) as Button;
        Require(leave is not null, "正式事件提供退开选项");
        var distanceClock = (GodotObject)Field(_main!, "_roadEncounterClock")!;
        distanceClock.Set("pending", new GDictionary { ["id"] = "qa_waiting_later_event", ["kind"] = "bandit", ["overshoot_km"] = 0.0 });
        leave!.EmitSignal(Button.SignalName.Pressed); await Frames(3);
        Check(!Pending && !_map.Get("encounter_blocked").AsBool() && _map.Get("hazards").AsGodotArray().Count == 0, "事件解决移除标记并解除阻断");
        Check(distanceClock.Get("pending").AsGodotDictionary().GetValueOrDefault("id", "").AsString() == "qa_waiting_later_event", "解决旧地图事件不吞掉时钟中尚未落位的新事件");
        distanceClock.Call("resolve_encounter");
        ((GDictionary)Field(_main!, "_worldEncounterState")!)["clock"] = distanceClock.Call("persistent_state").AsGodotDictionary();
        Check(Moving || Position.DistanceTo(hazard) < .01f, "事件解决后恢复剩余连续路径直到抵达");
        Stop(); var stopped = Position;
        loaded = DiskRead(_main.capture_battle_world_snapshot()!, "free_world_stopped");
        await StartMain(loaded); await Frames(5);
        Check(Position == stopped && !Moving, "停下后完整主场景读档保持精确位置且不自动续走");
        loaded = _main!.capture_battle_world_snapshot()!;
        loaded.node_world_state = new GDictionary { ["current_node"] = "bc_place_070", ["remaining_route"] = new GArray() };
        loaded.current_node_id = "east_woods";
        await StartMain(DiskRead(loaded, "free_world_legacy_node"));
        Check(Position.DistanceTo(Positions["bc_place_070"].AsVector2()) < .01f && _map.Call("get_current_location_runtime_id").AsString() == "east_woods", "旧节点存档经过正式读取恢复对应地点入口");
    }

    private async Task Npcs()
    {
        AtAnchor();
        var path = _graph.Call("shortest_path", "bc_place_001", "bc_place_070").AsGodotArray<string>();
        Require(path.Count >= 4, "真实NPC测试可用既定多段行程");
        var tile = (Node)Field(_world, "_tile_world_map")!;
        var cells = new Godot.Collections.Array<Vector2I>();
        foreach (var id in path) cells.Add(_world.node_world_cell_for_node_id(id));
        var first = 1;
        var edgeHours = tile.Call("get_npc_edge_travel_hours", cells[first], cells[first + 1], 100f).AsDouble();
        Require(double.IsFinite(edgeHours) && edgeHours > 0, "NPC真实行程耗时有效");
        var fixture = new WorldNpcData
        {
            npc_id = "qa_free_world_traveler", npc_name = "行旅验收人物", current_node_id = "yuxu_xianzong_core", origin_node_id = "yuxu_xianzong_core",
            is_traveling = true, travel_origin_node_id = "yuxu_xianzong_core", travel_destination_node_id = "east_woods", current_subscene_id = "",
            has_world_grid_position = true, current_grid_cell = cells[first], world_travel_path = cells,
            world_travel_next_cell_index = first + 1, world_travel_cell_progress_hours = (float)(edgeHours * .45), dun_speed = 100f,
        };
        _world.register_npc(fixture);
        var projectedAnchor = tile.Call("get_map_position", cells[first]).AsVector2();
        Check(projectedAnchor.DistanceTo(Positions[path[first]].AsVector2()) < .02f, "真实NPC与自由地图共享同一空间比例");
        GDictionary Sample()
        {
            foreach (var row in _world.get_free_world_map_people(Vector2.One * (_nav.Get("world_size").AsSingle() / 2), _nav.Get("world_size").AsSingle() * 2))
                if (row["id"].AsString() == fixture.npc_id) return row;
            throw new InvalidOperationException("真实NPC行旅采样中缺少登记人物");
        }
        var npcPoint = Sample()["position"].AsVector2();
        var farPoint = Vector2.Zero;
        foreach (var offset in new[] { new Vector2(130, 0), new Vector2(-130, 0), new Vector2(0, 130), new Vector2(0, -130) })
            if (_nav.Call("is_walkable", npcPoint + offset).AsBool()) { farPoint = npcPoint + offset; break; }
        Require(farPoint != Vector2.Zero, "NPC远距对照点可通行"); AtPosition(farPoint); await Frames(3); _map.Call("_refresh_people");
        Check(!_map.Call("can_contact_map_npc", fixture.npc_id).AsBool() && !string.IsNullOrEmpty((string)Invoke(_main!, "social_contact_failure_reason", fixture)!), "远处真实NPC无法直接交谈");
        AtPosition(npcPoint); await Frames(3); _map.Call("_refresh_people");
        Check(_map.Call("can_contact_map_npc", fixture.npc_id).AsBool() && string.IsNullOrEmpty((string)Invoke(_main!, "social_contact_failure_reason", fixture)!), "靠近真实行旅NPC后可交谈");
        Invoke(_main!, "begin_npc_social_talk", fixture, false, false); await Frames(2);
        Check((Field(_main!, "story_dialogue") as Control)?.Visible == true, "近距交谈复用正式人物对话界面");
        (Field(_main!, "story_dialogue") as Control)?.Hide();
        _world.set_simulation_enabled(false); _time.restore_calendar(1, 1, 2, 0, 0); _world.set_simulation_enabled(true);
        var before = Sample()["position"].AsVector2(); _time.advance_time(.25f, "qa_npc_projection"); _map.Call("_refresh_people");
        var after = Sample()["position"].AsVector2();
        Check(after.DistanceTo(before) > .01f, "真实NPC随月内小数时间移动，而非静态地图装饰");
        _world.set_simulation_enabled(false);
        _time.restore_calendar(1, 1, 30, 14, 0);
        fixture.world_travel_cell_progress_hours = 0; fixture.world_travel_next_cell_index = first + 1; fixture.current_grid_cell = cells[first]; fixture.is_traveling = true;
        fixture.dun_speed = (float)(100 * edgeHours / 1800);
        _world.set_simulation_enabled(true);
        _time.add_hours(5); var five = fixture.world_travel_cell_progress_hours;
        Check(Math.Abs(five - 5) < .01, "月内实际5小时立即结算NPC行程");
        _time.EmitSignal(GameTimeMgr.SignalName.time_changed); _time.EmitSignal(GameTimeMgr.SignalName.time_changed);
        Check(Math.Abs(fixture.world_travel_cell_progress_hours - five) < .001, "重复同小时通知不重复推进NPC");
        _time.add_hours(10);
        Check(fixture.is_traveling && Math.Abs(fixture.world_travel_cell_progress_hours - 15) < .01, "跨月累计15小时，不重复补算月内已走5小时");
        var stamp = fixture.world_travel_cell_progress_hours;
        _world.simulate_until_month(_world.get_last_processed_month());
        Check(fixture.world_travel_cell_progress_hours == stamp, "同月再次结算保持NPC行程幂等");
        _world.set_simulation_enabled(false);
        await Capture("nearby_npc_1920");
    }

    private async Task LegacyTravelAndNpcHours()
    {
        AtAnchor(); _time.pause_time();
        var route = _graph.Call("shortest_path", "bc_place_001", "bc_place_070").AsGodotArray<string>();
        Require(route.Count > 3, "旧普通行旅兼容测试有真实连续节点路线");
        var saved = _main!.capture_battle_world_snapshot()!;
        var traveler = saved.world_npcs.FirstOrDefault(n => n.npc_id == "qa_free_world_traveler");
        Require(traveler is not null, "旧NPC迁移测试使用实际世界行旅人物存档");
        var legacyGraph = GD.Load<GDScript>("res://gdscripts/world/BaiChuanNodeGraph.gd").New().AsGodotObject();
        Require(legacyGraph.Call("load_layout").AsBool(), "原200公里路网可用于旧存档来源验证");
        var edgeIndex = 1;
        // Prefer a mixed edge so a mistaken total-hours * 37% conversion is
        // observably different from preserving the old physical position.
        for (var i = 1; i < route.Count - 1; i++)
        {
            var candidate = _graph.Call("points_between", route[i], route[i + 1]).AsVector2Array();
            if (candidate.Length < 2) continue;
            var prefix = TerrainPrefix(candidate, .37);
            var full = TerrainPrefix(candidate, 1);
            if (Math.Abs(prefix.Cost - full.Cost * .37) > .02) { edgeIndex = i; break; }
        }
        var pair = new Godot.Collections.Array<string> { route[edgeIndex], route[edgeIndex + 1] };
        var legacyKm = legacyGraph.Call("path_km", pair).AsDouble();
        var expandedKm = _graph.Call("path_km", pair).AsDouble();
        const double fraction = .37;
        var speed = 100f;
        traveler!.is_traveling = true; traveler.dun_speed = speed;
        traveler.current_grid_cell = _world.node_world_cell_for_node_id(route[edgeIndex]);
        traveler.world_travel_path.Clear();
        foreach (var id in route) traveler.world_travel_path.Add(_world.node_world_cell_for_node_id(id));
        traveler.world_travel_next_cell_index = edgeIndex + 1;
        traveler.world_travel_cell_progress_hours = (float)(legacyGraph.Call("hours_for_km", legacyKm, speed).AsDouble() * fraction);
        traveler.travel_origin_node_id = "yuxu_xianzong_core"; traveler.travel_destination_node_id = "east_woods";
        traveler.current_node_id = "yuxu_xianzong_core"; traveler.current_subscene_id = "";
        var edgePoints = _graph.Call("points_between", pair[0], pair[1]).AsVector2Array();
        var expectedPrefix = TerrainPrefix(edgePoints, fraction);
        var expected = HoursForCost(expectedPrefix.Cost, speed);
        var tileBefore = (Node)Field(_world, "_tile_world_map")!;
        var uniformHours = _graph.Call("hours_for_km", expandedKm, speed).AsDouble() * fraction;
        Check(Math.Abs(tileBefore.Call("upgrade_uniform_travel_progress", traveler.current_grid_cell, traveler.world_travel_path[edgeIndex + 1], uniformHours, speed).AsDouble() - expected) < .001, "旧8000公里均速进度转换与独立弧长前缀积分一致");
        Check(Math.Abs(expectedPrefix.Cost - TerrainPrefix(edgePoints, 1).Cost * fraction) > .02, "37%旧档验证使用地形分布不均匀的真实路段");
        var placeIds = _graph.Get("runtime_ids").AsGodotDictionary().Values.Select(v => v.AsString()).ToArray();
        var legacyIndex = Enumerable.Range(1, route.Count - 2).FirstOrDefault(i =>
            _graph.Call("runtime_id_for_node", route[i]).AsString().Length == 0 && placeIds.All(id => Positions[id].AsVector2().DistanceTo(Positions[route[i]].AsVector2()) > 45));
        Require(legacyIndex > 0, "旧普通行旅来源包含真实野外途经点");
        var legacyRoute = new Godot.Collections.Array<string>(route.Skip(legacyIndex));
        saved.node_world_state = new GDictionary { ["current_node"] = route[legacyIndex], ["remaining_route"] = legacyRoute };
        saved.current_node_id = "";
        await StartMain(DiskRead(saved, "free_world_legacy_ordinary_in_flight"));
        Check(Moving && State["movement_mode"].AsString() == "free" && State["waypoints"].AsGodotArray().Count > 0, "旧普通在途存档恢复为自由几何路线");
        Check(_map.Call("get_current_location_runtime_id").AsString().Length == 0 &&
            _graph.Call("runtime_id_for_node", _map.Call("get_current_node").AsString()).AsString().Length > 0, "旧野外节点只迁移坐标，不保留普通当前位置空节点身份");
        Check((string)Invoke(_main!, "get_current_profile_location")! == "百川野外", "旧野外行旅恢复后人物资料显示百川野外");
        Stop();
        Check(!Moving && !State["resume_movement"].AsBool(), "旧普通在途存档可在当前位置停下");
        Require(Navigate(GroundTarget()), "旧普通在途存档停下后可改向"); Stop();
        var migrated = _world.get_npc("qa_free_world_traveler");
        Require(migrated is not null, "旧NPC人物ID与行程保持");
        Check(Math.Abs(migrated!.world_travel_cell_progress_hours - expected) < .003, "旧200公里NPC先迁移尺度、再按37%真实弧长转换地形小时");
        var tile = (Node)Field(_world, "_tile_world_map")!;
        Vector2 Project(WorldNpcData npc) => tile.Call("project_map_position", npc.current_grid_cell, npc.world_travel_path, npc.world_travel_next_cell_index, npc.world_travel_cell_progress_hours, 0f, npc.dun_speed).AsVector2();
        Check(Project(migrated).DistanceTo(expectedPrefix.Point) < .02, "旧NPC地形迁移保持37%弧长地图位置");
        var once = migrated.world_travel_cell_progress_hours;
        saved = _main!.capture_battle_world_snapshot()!;
        Check(saved.node_world_state["version"].AsInt32() == 2, "迁移后写入连续地图版本");
        Check(saved.node_world_state["terrain_speed_profile"].AsString() == _terrainData["profile_id"].AsString(), "迁移后存档写地形配置标记");
        saved.node_world_state = new GDictionary { ["current_node"] = "bc_place_001", ["remaining_route"] = new GArray(), ["terrain_speed_profile"] = _terrainData["profile_id"] };
        await StartMain(DiskRead(saved, "free_world_tagged_v1_npc"));
        migrated = _world.get_npc("qa_free_world_traveler");
        Check(migrated is not null && Math.Abs(migrated.world_travel_cell_progress_hours - once) < .001, "已迁移但地图仍是v1的序章存档重读不再放大或换算NPC进度");
        saved = _main!.capture_battle_world_snapshot()!;
        saved.node_world_state = PreviousCanvasState(saved.node_world_state);
        saved.node_world_state["terrain_speed_profile"] = "qa_nonempty_future_profile";
        await StartMain(DiskRead(saved, "free_world_previous_canvas_npc"));
        migrated = _world.get_npc("qa_free_world_traveler");
        Check(migrated is not null && Math.Abs(migrated.world_travel_cell_progress_hours - once) < .001, "旧12288画布v2与非空未知地形标记不重复迁移NPC小时");
        tile = (Node)Field(_world, "_tile_world_map")!;
        Check(migrated is not null && Project(migrated).DistanceTo(expectedPrefix.Point) < .02, "带标记档重复重建后NPC真实地图位置仍幂等");
        legacyGraph.Dispose(); _time.resume_time();
    }

    private (double Cost, Vector2 Point) TerrainPrefix(Vector2[] points, double fraction)
    {
        var total = 0.0;
        for (var i = 1; i < points.Length; i++) total += points[i - 1].DistanceTo(points[i]);
        var remaining = total * fraction;
        var cost = 0.0;
        for (var i = 1; i < points.Length; i++)
        {
            var distance = points[i - 1].DistanceTo(points[i]);
            if (remaining <= distance)
            {
                var point = points[i - 1].MoveToward(points[i], (float)remaining);
                return (cost + SegmentCost(points[i - 1], point), point);
            }
            cost += SegmentCost(points[i - 1], points[i]); remaining -= distance;
        }
        return (cost, points[^1]);
    }

    private void VerifyNativeTerrain()
    {
        var scale = GD.Load<Script>("res://gdscripts/world/BaiChuanWorldScale.gd").GetScriptConstantMap();
        var columns = scale["ART_COLUMNS"].AsInt32();
        using var master = Godot.FileAccess.Open(scale["ART_BASE"].AsString() + scale["MASTER_FILE"].AsString(), Godot.FileAccess.ModeFlags.Read);
        Require(master is not null, "正式母图存在");
        var header = master!.GetBuffer(24);
        Require(header.Length == 24 && header.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }), "正式母图保留PNG尺寸头");
        var width = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(16, 4));
        var height = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(20, 4));
        Require(width == height && width % columns == 0, "正式母图可以精确分片");
        var tilePixels = width / columns;
        var world = _map.GetNode<Node2D>("FixedScaleWorld");
        var backgrounds = world.GetChildren().OfType<Sprite2D>().Where(n => n.Name.ToString().StartsWith("Background", StringComparison.Ordinal)).ToArray();
        Check(backgrounds.Length == columns * columns, $"正式地图实际加载{columns}×{columns}共{columns * columns}片地形");
        Check(backgrounds.All(n => n.Texture is not null && n.Texture.GetWidth() == tilePixels && n.Texture.GetHeight() == tilePixels), $"正式地形精确分片尺寸{tilePixels}×{tilePixels}");
        foreach (var row in Enumerable.Range(0, columns))
        foreach (var col in Enumerable.Range(0, columns))
        {
            var path = _map.Call("_background_tile_path", $"bc_r{row}c{col}.png").AsString();
            Check(Godot.FileAccess.FileExists(path), $"正式地形分片存在 r{row}c{col}");
            if (!Godot.FileAccess.FileExists(path)) continue;
            using var image = Image.LoadFromFile(path);
            Check(image.GetWidth() == tilePixels && image.GetHeight() == tilePixels, $"母图精确分片尺寸 r{row}c{col} {image.GetWidth()}×{image.GetHeight()}");
        }
    }

    private async Task EnterAndWindow()
    {
        AtAnchor("bc_place_021");
        var point = Position; var entrance = _map.FindChild("EnterCurrentLocation", true, false) as Button;
        Check(_map.Call("get_current_location_runtime_id").AsString() == "white_stone_town", "靠近入口获得原有地点身份");
        _map.Call("_enter_current"); await Frames(5);
        var interior = Field(_main!, "location_interior") as Control;
        Check(interior?.Visible == true && Field(_main!, "_player_world_node_id") as string == "white_stone_town", "从自由地图进入白石城原有内景");
        Invoke(_main!, "open_world"); await Frames(5);
        _map = _main!.GetNode<Control>("MapLayer/BaiChuanNodeWorld");
        Check(Position.DistanceTo(point) < .01f, "退出地点仍回到同一地点入口");
        GetWindow().Size = new Vector2I(1280, 720); await Frames(8);
        _map.Call("_set_zoom_level", 0, GetViewport().GetVisibleRect().Size / 2); _map.Call("focus_player_node"); await Frames(3);
        var panel = _map.FindChild("FreeTravelControls", true, false) as Control;
        Check(panel is not null && panel.GetGlobalRect().Position.X >= 0 && panel.GetGlobalRect().End.X <= GetViewport().GetVisibleRect().Size.X + 1, "1280窗口行旅控制条留在视口内");
        var timePaper = _main!.GetNodeOrNull<Control>("HUDLayer/WorldInfo/TimePaper");
        Check(panel is not null && timePaper is not null && !panel.GetGlobalRect().Intersects(timePaper.GetGlobalRect()), "1280窗口行旅控制条与左上历法留开间距");
        await Capture("free_world_1280");
    }

    private async Task Escort()
    {
        const string incidentId = "qa_free_escort_case", taskId = "qa_free_escort_task", dutyId = "qa_free_escort_duty", npcId = "qa_free_escort_npc";
        AtAnchor("bc_place_070");
        Invoke(_main!, "on_node_world_arrived", "bc_place_070");
        var human = ((Character)Field(_main!, "player")!).entity_data as HumanData;
        Require(human is not null, "押送验收主角存在");
        human!.sect_affiliation_id = "yuxu_xianzong";
        human.sect_active_yuxu_duty_id = "yuxu_case_field_task";
        human.sect_active_yuxu_duty_incident_id = incidentId;
        human.sect_active_yuxu_duty_task_id = taskId;
        human.sect_active_yuxu_duty_instance_id = dutyId;
        _world.set_simulation_enabled(false); _world.npcs.Clear();
        var npc = new WorldNpcData { npc_id = npcId, npc_name = "押送验收当事人", current_node_id = "east_woods", origin_node_id = "east_woods" };
        _world.register_npc(npc);
        var incident = new EastWoodsNpcIncidentRecord
        {
            incident_id = incidentId, occurred_at = "验收", location_id = "east_woods", attacker_npc_id = npcId,
            target_npc_id = "qa_free_escort_target", appellant_npc_id = npcId,
        };
        incident.field_tasks.Add(new YuxuSectCaseTaskRecord
        {
            task_id = taskId, task_type = "appeal_transport", target_npc_id = npcId,
            status = YuxuSectCaseTaskRecord.StatusAssigned, duty_instance_id = dutyId,
        });
        Require(_world.record_east_woods_npc_incident(incident), "正式押送案卷建立");
        var task = _world.get_yuxu_case_task(incidentId, taskId)!;
        _map.SetProcess(false);
        Require(Navigate(Positions["bc_place_001"].AsVector2(), "bc_place_001"), "普通行旅可走到押送终点");
        await FinishFast();
        Check(!Moving && task.journey_status == YuxuSectCaseTaskRecord.JourneyNotStarted && !task.temporary_party_member_active, "普通自由行旅不误报押送抵达");
        AtAnchor("bc_place_070"); Invoke(_main!, "on_node_world_arrived", "bc_place_070");
        var stopOnce = true; var checkpoint = "";
        var interrupted = Callable.From<string>(id =>
        {
            if (!stopOnce) return;
            stopOnce = false; checkpoint = id; _map.Call("interrupt_assigned_route");
        });
        _map.Connect("node_step_committed", interrupted);
        Invoke(_main!, "start_yuxu_case_transport", npc);
        await FinishFast(100);
        _map.Disconnect("node_step_committed", interrupted);
        Require(checkpoint.Length > 0 && !Moving, "指定押送实际提交检查点并可中断");
        Check(task.temporary_party_member_active && task.journey_status == YuxuSectCaseTaskRecord.JourneyInTransit &&
            task.journey_current_cell == _world.node_world_cell_for_node_id(checkpoint) && task.journey_next_index > 1, "连续地图押送队伍位置与原检查点进度同步");
        _time.pause_time();
        var legacyEscort = _main!.capture_battle_world_snapshot()!;
        legacyEscort.node_world_state = new GDictionary { ["current_node"] = checkpoint, ["remaining_route"] = _graph.Call("shortest_path", checkpoint, "bc_place_001").AsGodotArray<string>() };
        await StartMain(DiskRead(legacyEscort, "free_world_legacy_assigned_escort"));
        Check(Moving && State["movement_mode"].AsString() == "assigned", "真实旧押送在途存档仍保留指定检查点约束");
        Stop(); Check(Moving, "旧押送不被普通停下按钮解散指定路线");
        _map.Call("interrupt_assigned_route"); _map.SetProcess(false); _time.resume_time();
        task = _world.get_yuxu_case_task(incidentId, taskId)!;
        var interceptor = new WorldNpcData { npc_id = "qa_free_escort_interceptor", npc_name = "拦截验收人物", current_node_id = "east_woods", origin_node_id = "east_woods" };
        _world.register_npc(interceptor); interceptor.has_world_grid_position = false;
        Require(_world.mark_yuxu_case_transport_intercepted(incidentId, taskId, dutyId, interceptor.npc_id, "world_map", task.journey_current_cell), "正式拦截状态建立");
        var loaded = DiskRead(_main!.capture_battle_world_snapshot()!, "free_world_escort_interrupted");
        Check(loaded.east_woods_npc_incidents.Any(r => r.incident_id == incidentId && r.field_tasks.Any(t => t.task_id == taskId && t.journey_status == YuxuSectCaseTaskRecord.JourneyIntercepted && t.journey_next_index == task.journey_next_index)), "拦截状态与检查点一起写盘读回");
        var context = new GDictionary { ["kind"] = "yuxu_case_transport_interception", ["incident_id"] = incidentId, ["task_id"] = taskId, ["duty_instance_id"] = dutyId, ["source"] = "world_map" };
        Require((bool)Invoke(_main, "handle_yuxu_case_transport_battle_return", new GArray(), new GArray(), new GDictionary { ["player_victory"] = true }, context)!, "真实战后押送返回链接受结果");
        await Frames(4); _map.SetProcess(false);
        await FinishFast();
        Check(!Moving && _map.Call("get_current_node").AsString() == "bc_place_001" && task.journey_status == YuxuSectCaseTaskRecord.JourneyArrived, "拦截战后沿原检查点接续至玉虚地点");
        Check(task.journey_last_interception_result.Contains("未发生拘押") && task.status == YuxuSectCaseTaskRecord.StatusAssigned, "战斗与抵达保留须由官员交接的原事务条件");
        _map.SetProcess(true);
        (Field(_main, "story_dialogue") as Control)?.Hide();
    }

    private async Task Stage(string name, Func<Task> body)
    {
        _results.Add("STAGE " + name); GD.Print("FREE_WORLD_FLOW_STAGE " + name); WriteReport();
        try { await body(); }
        catch (Exception error) { Check(false, name + "异常：" + (error.InnerException ?? error)); }
    }

    public override void _Ready() => CallDeferred(nameof(Run));
    private async void Run()
    {
        try
        {
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Folder));
            _runName = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--run-name="))?[11..] ?? "flow";
            Require(_runName.Length > 0 && _runName.All(c => char.IsLetterOrDigit(c) || c is '_' or '-'), "验收运行名仅包含文件名安全字符");
            VerifyIsolation();
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed); GetWindow().Size = new Vector2I(1920, 1080);
            await StartMain();
            if (OS.GetCmdlineUserArgs().Contains("--terrain-only"))
            {
                await Stage("terrain_speed_real_main", TerrainSpeed);
                GD.Print($"BAICHUAN_TERRAIN_SPEED_{(_failures == 0 ? "OK" : "FAILED")} {_checks} checks {_failures} failures");
                return;
            }
            var movementOnly = OS.GetCmdlineUserArgs().Contains("--movement-only");
            if (movementOnly)
            {
                await Stage("real_population_travel", PopulationTravel);
                await Stage("continuous_movement", Movement);
                await Stage("actual_pointer", Pointer);
                await Stage("enter_location_and_small_window", EnterAndWindow);
                await Stage("native_terrain_resources", () => { VerifyNativeTerrain(); return Task.CompletedTask; });
                GD.Print($"BAICHUAN_FREE_WORLD_MOVEMENT_{(_failures == 0 ? "OK" : "FAILED")} {_checks} checks {_failures} failures");
                return;
            }
            var eventOnly = OS.GetCmdlineUserArgs().Contains("--event-only");
            if (eventOnly)
            {
                await Stage("disk_restart_visible_event", SaveAndEncounter);
                GD.Print($"BAICHUAN_FREE_WORLD_EVENT_{(_failures == 0 ? "OK" : "FAILED")} {_checks} checks {_failures} failures");
                return;
            }
            var compatibilityOnly = OS.GetCmdlineUserArgs().Contains("--compatibility-only");
            if (compatibilityOnly)
            {
                await Stage("real_npc_and_month_boundary", Npcs);
                await Stage("legacy_ordinary_and_npc_hours", LegacyTravelAndNpcHours);
                await Stage("assigned_escort_and_interruption", Escort);
                GD.Print($"BAICHUAN_FREE_WORLD_COMPAT_{(_failures == 0 ? "OK" : "FAILED")} {_checks} checks {_failures} failures");
                return;
            }
            await Stage("real_population_travel", PopulationTravel);
            await Stage("terrain_speed_real_main", TerrainSpeed);
            await Stage("continuous_movement", Movement);
            await Stage("actual_pointer", Pointer);
            await Stage("disk_restart_visible_event", SaveAndEncounter);
            await Stage("real_npc_and_month_boundary", Npcs);
            await Stage("legacy_ordinary_and_npc_hours", LegacyTravelAndNpcHours);
            await Stage("assigned_escort_and_interruption", Escort);
            await Stage("enter_location_and_small_window", EnterAndWindow);
            await Stage("native_terrain_resources", () => { VerifyNativeTerrain(); return Task.CompletedTask; });
            GD.Print($"BAICHUAN_FREE_WORLD_FLOW_{(_failures == 0 ? "OK" : "FAILED")} {_checks} checks {_failures} failures");
        }
        catch (Exception error) { Check(false, "主场景冒烟异常：" + (error.InnerException ?? error)); }
        finally
        {
            if (_main is not null && IsInstanceValid(_main)) { Set(_main, "_skipExitSave", true); _main.QueueFree(); await Frames(3); }
            WriteReport(); GetTree().Quit(_failures == 0 ? 0 : 1);
        }
    }
}
