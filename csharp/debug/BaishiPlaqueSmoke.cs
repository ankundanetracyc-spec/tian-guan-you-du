using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using D = Godot.Collections.Dictionary;

/// <summary>真实白石场景、鼠标、悬绳动力及共用峰内控件验收；只使用隔离存档。</summary>
public partial class BaishiPlaqueSmoke : Node
{
    private int _checks;
    private MainScene _main = null!;
    private WorldLocationInteriorPanel _interior = null!;
    private Control _view = null!;
    private StoryDialoguePanel _dialogue = null!;
    private const string Art = "res://docs/美术规范/白石城分层与挂牌_20261106/";
    private static T Read<T>(object target, string field) => (T)target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(target)!;
    private object? CallMain(string method, params object[] args) => typeof(MainScene).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(_main, args);
    private JournalPanel.Entry CurrentTask => (JournalPanel.Entry)CallMain("baishi_current_task")!;
    private void Check(bool ok, string why) { _checks++; if (!ok) throw new Exception(why); }
    private async Task Frames(int count = 8) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private async Task Until(Func<bool> predicate)
    { for (var i = 0; i < 3000; i++) { if (predicate()) return; await Frames(1); } throw new Exception("等待场景超时"); }
    private Button[] Tags => _view.GetNode<Control>("FacilityLabels").GetChildren().OfType<Button>().Where(b => b.IsVisibleInTree()).ToArray();
    private Control Rope => _view.GetNode<Control>("FacilityLabels/ActionRope");
    private string Space => _view.Call("get_current_space_id").AsString();
    private void Mouse(Vector2 point, Vector2 relative = default, MouseButtonMask mask = 0) =>
        GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point, Relative = relative, ButtonMask = mask }, true);
    private void MouseButton(Vector2 point, bool down) => GetViewport().PushInput(new InputEventMouseButton {
        Position = point, GlobalPosition = point, ButtonIndex = Godot.MouseButton.Left, Pressed = down,
        ButtonMask = down ? MouseButtonMask.Left : 0 }, true);
    private async Task Click(Button button)
    {
        var at = button.GetGlobalRect().GetCenter(); Mouse(at); MouseButton(at, true); MouseButton(at, false); await Frames(12);
    }
    private async Task Open(string space) { _view.Call("open_space", space); await Frames(12); }
    private async Task Capture(string name)
    {
        if (!OS.GetCmdlineUserArgs().Contains("--capture")) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var output = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--capture-dir="))?[14..]
            ?? (OS.GetCmdlineUserArgs().Contains("--lecture-guide") ? "res://docs/美术规范/白石讲法入场引导_20261106/" : Art);
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(output));
        Check(GetViewport().GetTexture().GetImage().SavePng(output + name + ".png") == Error.Ok, "保存实机失败");
    }
    private static float MaxOffset(Control rope) => rope.Get("_offsets").AsVector2Array().Max(v => v.Length());
    private void CheckTags(int count)
    {
        Check(Tags.Length == count && Tags.All(t => t.IsInGroup("scene_action_tag")), "二级入口必须使用公共挂牌");
        var viewport = GetViewport().GetVisibleRect();
        Check(Tags.All(t => viewport.Encloses(t.GetGlobalRect())), "挂牌须位于窗口内");
        Check(Tags.All(t => t.GetThemeStylebox("normal") is StyleBoxTexture), "牌面必须复用宣纸素材");
        var points = Rope.Get("_points").AsVector2Array();
        Check(points.Length == 65 && points[32].Y > points[0].Y, "主绳应连续弯垂");
    }
    private void Drain() { for (var i = 0; i < 24 && _dialogue.Visible; i++) _dialogue.close(); }

    public override async void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        try
        {
            Check(OS.GetEnvironment("APPDATA").Contains("baishi_plaque"), "必须使用隔离目录");
            await Frames(); GetTree().CurrentScene = null;
            var saves = GetNode<GameSaveManager>("/root/GameSaveManager");
            Check(!saves.has_active_slot, "禁止触碰玩家存档");
            var fixture = new PersistentGameSave { current_node_id = "white_stone_town", npc_population_version = 1,
                npc_simulation_enabled = false, player_data = new HumanData { entity_name = "沈照微", cultivation_realm = "云息三层",
                    current_age = 18, base_physique = 5, base_intelligence = 5, base_soul = 5, current_world_stamina = 73 },
                node_world_state = new D { ["current_node"] = "bc_place_021" },
                scene_progress = new D { ["story_free_start"] = true, ["cloud_state_initialized"] = true, ["baishi_lecture"] = new BaishiLectureProgress { departed_qingxi = true, fang_returned = true, finished = true } } };
            fixture.world_npcs.Add(new WorldNpcData { npc_id = "baishi_plaque_fixture", npc_name = "谢谨修", current_age = 24,
                cultivation_realm = "云息三层", current_node_id = "white_stone_town", current_subscene_id = "street_east" });
            if (OS.GetCmdlineUserArgs().Contains("--lecture-guide"))
            {
                var progress = (BaishiLectureProgress)fixture.scene_progress["baishi_lecture"].AsGodotObject();
                progress.finished = false; progress.fang_returned = false;
                fixture.player_data!.cultivation_realm = "凡人";
                fixture.player_data.spirit_root_type = (int)GongfaResource.Wuxing.METAL_WOOD_WATER;
                fixture.player_data.spirit_root_name = "金木水三灵根";
                fixture.player_data.root_weights = [34, 33, 33, 0, 0];
                if (OS.GetCmdlineUserArgs().Contains("--inner"))
                {
                    fixture.player_data.spirit_root_type = (int)GongfaResource.Wuxing.METAL_WOOD;
                    fixture.player_data.spirit_root_name = "金木双灵根";
                    fixture.player_data.root_weights = [50, 50, 0, 0, 0];
                }
                if (OS.GetCmdlineUserArgs().Contains("--servant"))
                {
                    fixture.player_data.spirit_root_type = (int)GongfaResource.Wuxing.METAL_WOOD_WATER_FIRE;
                    fixture.player_data.spirit_root_name = "金木水火四灵根";
                    fixture.player_data.root_weights = [25, 25, 25, 25, 0];
                }
                if (OS.GetCmdlineUserArgs().Contains("--provisional"))
                {
                    fixture.player_data.spirit_root_type = (int)GongfaResource.Wuxing.METAL;
                    fixture.player_data.spirit_root_name = "金灵根";
                    fixture.player_data.root_weights = [100, 0, 0, 0, 0];
                }
                if (OS.GetCmdlineUserArgs().Contains("--no-root"))
                {
                    fixture.player_data.spirit_root_type = 0;
                    fixture.player_data.spirit_root_name = "无灵根";
                    fixture.player_data.root_weights = [0, 0, 0, 0, 0];
                }
            }
            typeof(GameSaveManager).GetField("_pendingLoad", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(saves, fixture);
            GetTree().ChangeSceneToFile("res://tscn/scene/MainScene.tscn");
            await Until(() => GetTree().CurrentScene is MainScene { StartupComplete: true });
            _main = (MainScene)GetTree().CurrentScene; _interior = Read<WorldLocationInteriorPanel>(_main, "location_interior");
            _dialogue = Read<StoryDialoguePanel>(_main, "story_dialogue"); Drain();
            typeof(MainScene).GetMethod("on_node_enter_requested", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(_main, ["white_stone_town"]);
            _view = Read<Control>(_interior, "_baishiView"); GetWindow().Size = new Vector2I(1920, 1080); await Frames(24);
            var clock = GetNode<GameTimeMgr>("/root/GameTimeManager"); clock.is_paused = true;
            var hours = clock.get_total_game_hours(); var stamina = _main.get_player_character()!.current_world_stamina;
            if (OS.GetCmdlineUserArgs().Contains("--lecture-guide"))
            {
                await CheckLectureGuide();
                _main.prepare_return_to_title_from_menu(false); _main.QueueFree(); await Frames(8);
                GD.Print($"BAISHI_LECTURE_GUIDE PASS: {_checks} checks"); GetTree().Quit(); return;
            }
            Check(Space == "" && _view.GetNode<Control>("OverviewPlaces").GetChildren().OfType<Button>().Count(b => b.Visible) == 6 &&
                !_view.GetNode<Control>("DistrictLabels").Visible,
                "一级总览应直接平铺主要场所，不显示必经街区标签");
            await Capture("白石总览");
            _view.Call("open_district", "east"); await Frames(); Check(Space == "street_east", "兼容入口未能打开现有东市空间");
            CheckTags(2); await Capture("东市_二级挂牌");

            var first = Tags[0]; var second = Tags[1]; var point = first.GetGlobalRect().GetCenter();
            Mouse(point, new Vector2(42, 0)); await Frames(10);
            Check(MaxOffset(Rope) > .01f && Math.Abs(first.Rotation) > .0001f, "鼠标应同时带动主绳和牌面");
            Check(Math.Abs(first.GetNode<Node2D>("Tassel").Rotation) > .0001f, "红绳穗应独立响应");
            await Frames(20); Check(Math.Abs(second.Rotation) > .000001f || MaxOffset(Rope) > .01f, "扰动应沿绳传动");
            var original = Rope.Get("_points").AsVector2Array();
            var grab = Rope.GlobalPosition + original[32]; Mouse(grab); MouseButton(grab, true);
            Mouse(grab + new Vector2(0, 30), new Vector2(0, 30), MouseButtonMask.Left); await Frames(6);
            Check(Rope.Get("_drag_index").AsInt32() >= 0 && MaxOffset(Rope) >= 25, "主绳应可轻拉");
            await Capture("东市_拉绳反馈"); MouseButton(grab + new Vector2(0, 30), false); Mouse(new Vector2(100, 650));
            Check(Rope.Get("_drag_index").AsInt32() == -1, "松手应解除拖绳");
            if (OS.GetCmdlineUserArgs().Contains("--record"))
            {
                DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://tmp/baishi_plaque_video/"));
                for (var frame = 0; frame < 120; frame++)
                {
                    if (frame is 30 or 70) Mouse(first.GetGlobalRect().GetCenter(), new Vector2(frame == 30 ? -45 : 45, 0));
                    if (frame is 31 or 71) Mouse(new Vector2(100, 650));
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    GetViewport().GetTexture().GetImage().SaveJpg($"res://tmp/baishi_plaque_video/{frame:D4}.jpg", .86f);
                }
            }
            for (var i = 0; i < 800; i++)
            {
                Rope.Call("_process", 1d / 120); first.Call("_process", 1d / 120); second.Call("_process", 1d / 120);
            }
            var offsets = Rope.Get("_offsets").AsVector2Array();
            Check(offsets[0] == Vector2.Zero && offsets[^1] == Vector2.Zero && offsets.All(v => v.IsFinite()), "绳端固定且坐标有限");
            Check(MaxOffset(Rope) < .12f && Math.Abs(first.Rotation) < .005f, "绳与牌应阻尼回稳");
            Rope.Call("_push_string", 960, new Vector2(30, 30)); Rope.Call("_process", .8);
            Check(Rope.Get("_offsets").AsVector2Array().All(v => v.IsFinite() && v.Length() < 48), "低帧率不能炸绳");
            await Click(Tags.First(b => b.Name == "Facility_bookshop")); Check(Space == "bookshop", "挂牌应进入对应书坊");
            CheckTags(1); await Capture("书坊_功能挂牌");
            await Click(Tags[0]); Check(_dialogue.Visible, "书目挂牌应接原对白"); Drain(); await Frames();
            Check(_main.try_return_to_previous_page() && Space == "", "公共返回应直接回白石城总览");
            await Click(_view.GetNode<Button>("ReturnButton")); Check(Space == "", "可见返回应回总览");

            await Open("notice_hall"); CheckTags(3); await Capture("告示所_三项挂牌");
            foreach (var (name, title) in new[] { ("Action_notice_lecture", "讲法告示"), ("Action_notice_city", "城中去处"), ("Action_notice_admission", "问道须知") })
            {
                await Click(Tags.First(b => b.Name == name));
                Check(Read<Label>(_dialogue, "_body").Text.Contains(title), "告示回调对象错误"); Drain(); await Frames();
            }
            await Open("city_office"); CheckTags(1); await Capture("城守府_侧院挂牌");
            await Open("lecture_study"); Check(Tags.Length == 0, "无研习权限时不能补造入口");
            await Open("street_west"); Check(Tags.Length == 0, "空场所不能出现虚假功能");
            await Open("street_east"); GetWindow().Size = new Vector2I(1280, 720); await Frames(24); CheckTags(2);
            await Capture("东市_1280"); GetWindow().Size = new Vector2I(1920, 1080); await Frames(24);
            Check(clock.get_total_game_hours() == hours && _main.get_player_character()!.current_world_stamina == stamina,
                "挂牌游览与物理效果不得推进时间或消耗精力");

            var layer = new CanvasLayer { Layer = 50 }; AddChild(layer);
            var peak = GD.Load<PackedScene>("res://tscn/world/bai_chuan/YuxuPeakInterior.tscn").Instantiate<Control>(); layer.AddChild(peak);
            peak.Call("set_life_context", true, "外门弟子", 0, 0); peak.Call("open_peak", "guanfu"); await Frames(12);
            Check(peak.FindChildren("*", "Button", true, false).OfType<Button>().Any(b => b.IsVisibleInTree() && b.IsInGroup("scene_action_tag")),
                "玉虚峰内须继续复用挂牌");
            peak.QueueFree(); await Frames();
            var board = GD.Load<PackedScene>("res://tscn/world/bai_chuan/YuxuSectDutyBoard.tscn").Instantiate<Control>(); layer.AddChild(board);
            board.Call("open_board", ""); await Frames(16);
            Check(board.FindChildren("DutyRope*", "Control", true, false).Count == 3, "和光公事保留三绳布局");
            Check(board.FindChildren("*", "Button", true, false).OfType<Button>().Where(b => b.IsVisibleInTree() && b.IsInGroup("scene_action_tag"))
                .All(b => b.GetGlobalRect().End.Y <= 1080), "任务绳签不可越出窗口");
            layer.QueueFree(); await Frames();
            GD.Print($"BAISHI_PLAQUE PASS: {_checks} checks"); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private async Task CheckLectureGuide()
    {
        var state = Read<BaishiLectureProgress>(_main, "_baishiLecture");
        await Open("lecture");
        var guide = _view.GetNode<Control>("LectureGuidance");
        Check(Tags.Length == 1 && Tags[0].Text.Replace("\n", "") == "听讲并验灵" && Tags[0].IsInGroup("scene_action_tag"),
            "前院中央应有讲法与个人验灵共用的挂牌");
        Check(guide.IsVisibleInTree() && guide.Call("get_instruction_text").AsString().Contains("当场查验") &&
            guide.Call("get_instruction_text").AsString().Contains("验灵"), "初入应有明确的场景引导");
        Check(guide.GetNode<Label>("Step").Text == "1", "引导使用独立步骤浮层");
        Check(guide.Size.Y <= 100 && !guide.GetGlobalRect().Intersects(Tags[0].GetGlobalRect()),
            "首次入场提示必须保持紧凑浮层，不能因文字测量撑大并覆盖入口");
        Check(!state.heard_lecture && !state.roots_checked && !_dialogue.Visible, "入场不可自动完成旁听或验灵");
        Check(CurrentTask.Body == "听讲法并查验灵根。", "任务显示合并后的讲法与验灵目标");
        var hostId = state.host_actor_id;
        Check(hostId.Length > 0 && state.host_snapshot is not null, "沿用实际随机主持");
        await Capture("讲法初入_中央挂牌引导");
        var page = Read<NpcInteractionPanel>(_main, "npc_interactions");
        _main.open_world_npc_actions(state.host_snapshot!); await Frames();
        Check(!page.FindChildren("*", "Button", true, false).OfType<Button>().Any(b => b.Text.Replace("\n", "") is "听讲法" or "旁听讲法"),
            "人物菜单不再承接公共旁听");
        page.close();
        _view.Call("open_space", "city_office"); await Frames();
        Check(!guide.IsVisibleInTree(), "离开前院应收起引导"); await Open("lecture");
        Check(state.host_actor_id == hostId && guide.IsVisibleInTree(), "未听完重入保持原主持与引导");
        var hours = GetNode<GameTimeMgr>("/root/GameTimeManager").get_total_game_hours();
        var stamina = _main.get_player_character()!.current_world_stamina;
        GetWindow().Size = new Vector2I(1280, 720); await Frames(24);
        Check(GetViewport().GetVisibleRect().Encloses(guide.GetGlobalRect()) && !guide.GetGlobalRect().Intersects(Tags[0].GetGlobalRect()),
            "小窗口引导不得越界或压住挂牌");
        await Capture("讲法引导_1280"); GetWindow().Size = new Vector2I(1920, 1080); await Frames(24);
        await Click(Tags[0]);
        Check(_dialogue.Visible && !page.Visible && !state.heard_lecture && !guide.Visible, "点击旁听后应进入原讲法对白并收起引导");
        await Capture("点击挂牌_讲法对白"); Drain(); await Frames();
        Check(state.heard_lecture && state.roots_checked && !guide.Visible &&
            Tags.All(b => b.GetMeta("action_id").AsString() != "lecture.roots"), "讲法结束时应完成本人的一次验灵，不再显示第二个验灵入口");
        Check(GetNode<GameTimeMgr>("/root/GameTimeManager").get_total_game_hours() == hours &&
            _main.get_player_character()!.current_world_stamina == stamina, "场景入口不得额外添加费用或时间");
        const string savePath = "user://baishi_lecture_guide.tres";
        Check(ResourceSaver.Save(_main.capture_battle_world_snapshot()!, savePath) == Error.Ok, "旁听状态应进入原存档");
        var loaded = ResourceLoader.Load<PersistentGameSave>(savePath, cacheMode: ResourceLoader.CacheMode.Ignore)!;
        var savedLecture = (BaishiLectureProgress)loaded.scene_progress["baishi_lecture"].AsGodotObject();
        Check(savedLecture.heard_lecture && savedLecture.roots_checked, "读取后应同时恢复讲法与验灵完成标记");
        await Open("lecture"); Check(!guide.Visible && Tags.All(b => b.GetMeta("action_id").AsString() != "lecture.roots"), "重入不得再次显示已完成的验灵入口");
        _main.open_world_npc_actions(state.host_snapshot!); await Frames();
        var roots = page.FindChildren("*", "Button", true, false).OfType<Button>().FirstOrDefault(b => b.Text.Replace("\n", "") == "查验灵根");
        Check(roots is null, "验灵不应要求从人物菜单办理"); page.close();
        await Capture("讲法与验灵完成后_前院入口");
        if (OS.GetCmdlineUserArgs().Contains("--no-root"))
        {
            Drain(); await Frames();
            Check(state.roots_checked && !state.fang_returned && !state.finished &&
                Tags.Any(b => b.GetMeta("action_id").AsString() == "lecture.farewell") &&
                Tags.All(b => b.GetMeta("action_id").AsString() != "lecture.sect"), "无灵根应以吊牌道别，不开放择宗");
            await Click(Tags.Single(b => b.GetMeta("action_id").AsString() == "lecture.farewell"));
            Check(_dialogue.Visible, "点击道别吊牌应开始对话");
            Drain(); await Frames();
            Check(state.fang_returned && !state.finished && Tags.Any(b => b.GetMeta("action_id").AsString() == "lecture.leave"),
                "方远返乡后应开放离开吊牌");
            await Click(Tags.Single(b => b.GetMeta("action_id").AsString() == "lecture.leave"));
            await Frames();
            Check(state.finished && !state.spirit_boat_in_transit, "无灵根应可自行离开，不能误登灵舟");
        }
        else if (OS.GetCmdlineUserArgs().Contains("--departure-flow"))
        {
            Drain();
            await Frames(); await CheckDepartureFlow();
        }
        else { Drain(); await Frames(); Check(state.roots_checked, "原验灵流程应正常接续"); }
    }

    private async Task CheckDepartureFlow()
    {
        var state = Read<BaishiLectureProgress>(_main, "_baishiLecture");
        var player = _main.get_player_character()!;
        var human = (HumanData)player.entity_data!;
        var clock = GetNode<GameTimeMgr>("/root/GameTimeManager");
        var hours = clock.get_total_game_hours(); var stamina = player.current_world_stamina;
        Check(state.roots_checked && !_dialogue.Visible && state.selected_sect_id == "" && !state.roots_session_finished &&
            Tags.Any(b => b.GetMeta("action_id").AsString() == "lecture.sect"), "验灵后应提供择宗吊牌，而非自动选宗或登舟");
        await Click(Tags.Single(b => b.GetMeta("action_id").AsString() == "lecture.sect"));
        Check(Read<Label>(_dialogue, "_speaker").Text == "旁白" && Read<Label>(_dialogue, "_body").Text.StartsWith("择定去向："),
            "择宗说明应显示旁白，并将事项标题放在正文中");
        var options = _dialogue.FindChildren("*", "Button", true, false).OfType<Button>().Where(b => b.IsVisibleInTree()).ToArray();
        Check(options.Select(b => b.Text).SequenceEqual(new[] { "玉虚仙宗", "暂不决定" }), "只开放已实装宗门及取消");
        await Capture("验灵后_选择宗门");
        await Click(options[1]);
        Check(state.selected_sect_id == "" && !state.spirit_boat_in_transit && CurrentTask.Body == "选定求道宗门。", "取消不得偷偷选择或登舟");
        CallMain("choose_baishi_boat"); CallMain("begin_baishi_boat_flight");
        Check(!state.spirit_boat_in_transit && !state.fang_returned, "未选去向不得绕过顺序直接启航");
        await Click(Tags.Single(b => b.GetMeta("action_id").AsString() == "lecture.sect"));
        await Click(_dialogue.FindChildren("*", "Button", true, false).OfType<Button>().Single(b => b.Text == "玉虚仙宗")); Drain(); await Frames();
        Check(state.selected_sect_id == "yuxu_xianzong" && !state.roots_session_finished && human.sect_affiliation_id == "",
            "选择只是意向，不完成验灵会或录籍");
        Check(Tags.Any(b => b.GetMeta("action_id").AsString() == "lecture.wait" && b.Text.Replace("\n", "") == "等候验灵") && CurrentTask.Body == "等候本场验灵结束。",
            "选择后应等待整场结束");
        CallMain("choose_baishi_boat"); CallMain("begin_baishi_boat_flight");
        Check(!state.spirit_boat_in_transit && state.spirit_boat_driver_id == "", "验灵会未完不得创建驾舟人或开船");
        await Capture("择宗后_静候本场验灵");
        const string pending = "user://baishi_reception_pending.tres";
        Check(ResourceSaver.Save(_main.capture_battle_world_snapshot()!, pending) == Error.Ok, "保存候验状态");
        var restored = ResourceLoader.Load<PersistentGameSave>(pending, cacheMode: ResourceLoader.CacheMode.Ignore)!;
        CallMain("restore_scene_progress", restored.scene_progress); state = Read<BaishiLectureProgress>(_main, "_baishiLecture");
        CallMain("show_baishi_lecture"); await Frames();
        Check(state.selected_sect_id == "yuxu_xianzong" && !state.roots_session_finished && CurrentTask.Body == "等候本场验灵结束。",
            "读档应恢复候验阶段，不自动把全场判为结束");
        await Click(Tags.Single(b => b.GetMeta("action_id").AsString() == "lecture.wait"));
        Check(_dialogue.Visible && !state.roots_session_finished, "等候过场未结束前不能放行");
        _dialogue.close(); Check(!state.roots_session_finished, "主持宣布结束前不能放行");
        await Capture("等候验灵_本场收尾"); Drain(); await Frames();
        Check(state.roots_session_finished && !state.spirit_boat_in_transit && !state.fang_returned, "全场结束只开放登舟，仍由玩家确认");
        Check(Tags.Any(b => b.GetMeta("action_id").AsString() == "lecture.board" && b.Text.Replace("\n", "") == "登舟启程") && CurrentTask.Body == "乘舟前往玉虚仙宗。", "结束后开放登舟");
        Check(clock.get_total_game_hours() == hours && player.current_world_stamina == stamina, "叙事过场不额外新增时间或费用");
        await Capture("验灵结束_登舟启程");
        await Click(Tags.Single(b => b.GetMeta("action_id").AsString() == "lecture.board")); Drain(); await Frames();
        Check(state.spirit_boat_in_transit && state.fang_returned && state.chose_yuxu, "登舟前应完成方远道别并接上真实航程");
        Check(player.cultivation_realm == "凡人" && human.intro_gongfa_path == "" && human.sect_affiliation_id == "",
            "新流程不得先发功法、修炼或提前录籍");
        Check(CurrentTask.Body == "抵达玉虚仙宗。", "航行任务仍只写目标");
        await Capture("登舟后_赴玉虚");
        if (OS.GetCmdlineUserArgs().Contains("--admission-flow")) await CheckArrivalAndRegistration();
    }

    private async Task CheckArrivalAndRegistration()
    {
        var state = Read<BaishiLectureProgress>(_main, "_baishiLecture");
        var actor = _main.get_player_character()!; var human = (HumanData)actor.entity_data!;
        var manager = GetNode<NpcWorldManager>("/root/NpcWorldManager"); var driverId = state.spirit_boat_driver_id;
        var map = Read<Control>(_main, "_nodeWorldMap"); GetNode<GameTimeMgr>("/root/GameTimeManager").is_paused = false;
        for (var i = 0; i < 2000 && map.Call("is_traveling").AsBool(); i++) map.Call("_advance_flight", .1f);
        await Frames();
        Check(!state.spirit_boat_in_transit && map.Call("get_current_location_runtime_id").AsString() == "yuxu_xianzong_core" &&
            manager.get_npc(driverId) is { is_player_travel_companion: false, current_node_id: "yuxu_xianzong_core" }, "灵舟与实际接引弟子未到玉虚");
        map.Call("_enter_current"); await Frames(20);
        var overview = _main.GetNode<Control>("SceneLayer/YuxuXianzongOverview");
        var gate = overview.GetNode<Button>("GateActions/GateWelcome");
        Check(gate.IsVisibleInTree() && gate.IsInGroup("scene_action_tag") && !_dialogue.Visible && human.sect_affiliation_id == "",
            "山门应先显示中央迎真吊牌，不能自动录籍");
        await Capture("玉虚山门_迎真吊牌"); await Click(gate);
        Check(_dialogue.Visible && !human.sect_yuxu_registration_pending && human.sect_affiliation_id == "", "迎真未进入对白或提前报到");
        var welcomeLines = Read<System.Collections.Generic.IReadOnlyList<NpcSocialPresentationText.Line>>(_dialogue, "_socialLines");
        var expectedWelcomeLines = new[] { "长老核对了白石留下的验灵记录，确认结果已在案，没有再让你查验灵根。" }
            .Concat(human.get_yuxu_gate_judgment_lines());
        Check(state.roots_checked && welcomeLines.Select(line => line.Result).SequenceEqual(expectedWelcomeLines) &&
            welcomeLines.All(line => line.Rule == "yuxu_evidence" && !line.Pending.Contains("灵机")),
            "白石验灵后，山门迎真只应评述经历，不得重复查验灵根");
        for (var i = 0; i < 30 && !human.sect_yuxu_registration_pending; i++)
        {
            typeof(StoryDialoguePanel).GetMethod("process_social_reveal", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(_dialogue, [100d]);
            var noSect = _dialogue.FindChildren("*", "Button", true, false).OfType<Button>().FirstOrDefault(b => b.IsVisibleInTree() && b.Text == "无");
            if (noSect is not null) noSect.EmitSignal(BaseButton.SignalName.Pressed);
            else if (Read<Button>(_dialogue, "_socialDone").IsVisibleInTree()) Read<Button>(_dialogue, "_socialDone").EmitSignal(BaseButton.SignalName.Pressed);
            await Frames(2);
        }
        Check(human.sect_yuxu_registration_pending && human.sect_affiliation_id == "" && human.sect_admission_guide_npc_id == driverId,
            "迎真后应由原驾舟弟子引路，报到之前不能加入宗门");
        var peak = overview.GetNode<Control>("PeakInterior");
        Button Registration() => (Button)peak.Get("_action_buttons").AsGodotDictionary()["yuxu_registration"].AsGodotObject();
        Check(peak.IsVisibleInTree() && Registration().IsVisibleInTree() && Registration().IsInGroup("scene_action_tag") && CurrentTask.Body == "在观复峰办理入门。",
            "观复峰必须显示中央报到吊牌和简短任务");
        var tasks = (List<JournalPanel.Entry>)typeof(MainScene).GetMethod("get_quest_entries", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(_main, [])!;
        Check(tasks.Count(task => task.State != "已完成" && task.Body == "在观复峰办理入门。") == 1, "报到目标不应重复登记");
        await Capture("观复峰_入门报到吊牌");
        const string pendingSave = "user://yuxu_registration_pending.tres";
        Check(ResourceSaver.Save(_main.capture_battle_world_snapshot()!, pendingSave) == Error.Ok, "待报到存档失败");
        var loaded = ResourceLoader.Load<PersistentGameSave>(pendingSave, cacheMode: ResourceLoader.CacheMode.Ignore);
        Check(loaded?.player_data is { sect_yuxu_registration_pending: true, sect_affiliation_id: "" } saved && saved.sect_admission_guide_npc_id == driverId,
            "待报到状态或接引人未存档");
        var mainScene = GD.Load<PackedScene>("res://tscn/scene/MainScene.tscn");
        _main.prepare_return_to_title_from_menu(false); _main.QueueFree(); await Frames(8);
        var saves = GetNode<GameSaveManager>("/root/GameSaveManager");
        typeof(GameSaveManager).GetField("_pendingLoad", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(saves, loaded);
        var reloaded = mainScene.Instantiate<MainScene>();
        GetTree().Root.AddChild(reloaded); GetTree().CurrentScene = reloaded;
        await Until(() => reloaded.StartupComplete); await Frames(18);
        Check(reloaded.StartupError is null, "待报到存档未能重建主场景");
        _main = reloaded; _interior = Read<WorldLocationInteriorPanel>(_main, "location_interior");
        _dialogue = Read<StoryDialoguePanel>(_main, "story_dialogue");
        actor = _main.get_player_character()!; human = (HumanData)actor.entity_data!;
        state = Read<BaishiLectureProgress>(_main, "_baishiLecture");
        overview = _main.GetNode<Control>("SceneLayer/YuxuXianzongOverview"); peak = overview.GetNode<Control>("PeakInterior");
        map = Read<Control>(_main, "_nodeWorldMap"); map.Call("_enter_current"); await Frames(20);
        Check(human.sect_yuxu_registration_pending && human.sect_affiliation_id == "" && Registration().IsVisibleInTree() && !_dialogue.Visible,
            "读档重进应恢复报到吊牌，不重复迎真");
        var alreadyClaimed = OS.GetCmdlineUserArgs().Contains("--already-claimed");
        if (alreadyClaimed)
        {
            var existingBook = YuxuManualCopy.Catalog()[0];
            Check(actor.add_item_to_inventory(existingBook.Item.ResourcePath), "旧档首本实体书应能放入背包");
            human.sect_library_first_manual_path = existingBook.Book.ResourcePath;
        }
        await Click(Registration());
        Check(_dialogue.Visible && human.sect_yuxu_registration_pending && human.sect_affiliation_id == "", "点击报到应先对话，不能提前写门籍");
        var registrationGuide = GetNode<NpcWorldManager>("/root/NpcWorldManager").get_npc(human.sect_admission_guide_npc_id)!;
        Check(ReferenceEquals(CallMain("resolve_scripted_dialogue_npc", registrationGuide.npc_name), registrationGuide),
            "报到对白须使用实际接引弟子的立绘");
        var expectedRank = OS.GetCmdlineUserArgs().Contains("--provisional") ? "预备真传" :
            OS.GetCmdlineUserArgs().Contains("--inner") ? "内门弟子" : OS.GetCmdlineUserArgs().Contains("--servant") ? "杂役" : "外门弟子";
        await Capture("观复峰_报到对白");
        _dialogue.close(); _dialogue.close();
        var rankDescription = Read<Label>(_dialogue, "_body").Text;
        Check(rankDescription.Contains(expectedRank.Replace("弟子", "")) && Read<Label>(_dialogue, "_speaker").Text == registrationGuide.npc_name,
            "报到对白按实际身份分支并使用实际接引人");
        _dialogue.close();
        var bookDescription = Read<Label>(_dialogue, "_body").Text;
        Check(alreadyClaimed ? bookDescription.Contains("已经领过") && !bookDescription.Contains("免费") : bookDescription.Contains("第一本可以免费拓印"),
            "按实际首本记录显示引导，不重复许诺免费领取");
        Drain(); await Frames(18);
        Check(!human.sect_yuxu_registration_pending && human.sect_affiliation_id == "yuxu_xianzong" &&
            human.sect_entry_rank == expectedRank && human.sect_current_rank == expectedRank && human.sect_teacher_npc_id == "" &&
            human.sect_guide_npc_id == "" && state.finished, "报到后的身份、序章收束或师承时点错误");
        Check(human.sect_true_disciple_lecture_next_due_hour == -1 && (alreadyClaimed == (human.sect_library_first_manual_path.Length > 0)),
            "预备／普通弟子不得提前开讲席，已有首本记录应保留");
        Check(expectedRank == "预备真传"
            ? human.sect_yuxu_exam_start_hour == human.sect_entry_total_game_hours && human.sect_yuxu_exam_start_hour == GetNode<GameTimeMgr>("/root/GameTimeManager").get_total_game_hours()
            : human.sect_yuxu_exam_start_hour < 0, "仅预备真传从完成报到时开始计算考核期限");
        var buttons = peak.Get("_action_buttons").AsGodotDictionary();
        Check(((Button)buttons["meditation"].AsGodotObject()).IsVisibleInTree() &&
            !((Button)buttons["lineage_choose_teacher"].AsGodotObject()).IsVisibleInTree(), "入门后应有观复静室且不能提前择师");
        await Capture("观复峰_报到完成");
        overview.Call("_on_peak_interior_back"); overview.Call("_on_peak_pressed", "xuanlan"); await Frames(15);
        var selectBook = (Button)buttons["lineage_book_selection"].AsGodotObject();
        Check(selectBook.IsVisibleInTree(), "新弟子无法从玄览峰吊牌选书"); await Click(selectBook);
        var library = Read<YuxuManualLibraryPanel>(_main, "_yuxuManualLibrary");
        Check(library.IsVisibleInTree(), "选书吊牌无法打开藏经阁");
        if (!alreadyClaimed)
        {
            await Click((Button)library.FindChild("CopyManual", true, false)!);
            Check(_main.has_active_item_claim() && human.sect_library_first_manual_path == "", "拓印应先打开领取，不提前登记首本");
            var claim = Read<ItemClaimPanel>(_main, "_active_item_claim");
            await Click((Button)claim.FindChild("ClaimAutoPlace", true, false)!);
            await Click((Button)claim.FindChild("ClaimFooterPrimary", true, false)!);
        }
        Check(!_main.has_active_item_claim() && human.sect_library_first_manual_path.Length > 0 &&
            actor.get_known_gongfas().Any(b => b.ResourcePath == human.sect_library_first_manual_path) && actor.main_gongfa is null,
            "免费秘籍未实际入包，或领取即自动主修");
        var cultivationGoal = (JournalPanel.Entry)CallMain("get_cultivation_guidance_task")!;
        Check(cultivationGoal.Id == "cultivation.enter_yunxi" && cultivationGoal.Body == "踏入云息境。",
            "新入门凡人在领取秘籍后应有简洁的入道目标");
        var compactGoal = (string)typeof(MainScene).GetMethod("GetJournalTaskTrackerObjective", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [cultivationGoal])!;
        Check(compactGoal == "踏入云息境", "入道引导不能复用旧序章的寻人目标");
        actor.cultivation_realm = "云息一层";
        Check(((JournalPanel.Entry)CallMain("get_cultivation_guidance_task")!).Id != cultivationGoal.Id, "踏入云息后应切换至后续修行目标");
        actor.cultivation_realm = "凡人";
        library.Hide();
        human.sect_current_rank = "真传"; CallMain("refresh_yuxu_life_context"); await Frames();
        Check(((Button)buttons["xuanlan_study"].AsGodotObject()).IsVisibleInTree(), "没有旧师门借读的真传应能在玄览峰研读实体书");
        human.sect_lineage_gongfa_path = human.sect_library_first_manual_path;
        human.sect_lineage_reported_first_volume = false; CallMain("refresh_yuxu_life_context"); await Frames();
        Check(((Button)buttons["xuanlan_study"].AsGodotObject()).IsVisibleInTree(), "已领取秘籍后不应再被旧首卷汇报流程挡住");
        human.sect_lineage_gongfa_path = ""; human.sect_current_rank = expectedRank;
        human.sect_true_disciple_lecture_next_due_hour = -1; CallMain("refresh_yuxu_life_context");
        var rankMethod = typeof(MainScene).GetMethod("get_yuxu_entry_rank", BindingFlags.NonPublic | BindingFlags.Static)!;
        Check((string)rankMethod.Invoke(null, [1])! == "预备真传" && (string)rankMethod.Invoke(null, [2])! == "内门弟子" &&
            (string)rankMethod.Invoke(null, [3])! == "外门弟子" && (string)rankMethod.Invoke(null, [4])! == "杂役" &&
            (string)rankMethod.Invoke(null, [5])! == "杂役" && (string)rankMethod.Invoke(null, [0])! == "", "灵根与现行入门身份对应有误");
        const string completedSave = "user://yuxu_registration_complete.tres";
        Check(ResourceSaver.Save(_main.capture_battle_world_snapshot()!, completedSave) == Error.Ok, "入门存档失败");
        var completed = ResourceLoader.Load<PersistentGameSave>(completedSave, cacheMode: ResourceLoader.CacheMode.Ignore);
        Check(completed?.player_data is { sect_yuxu_registration_pending: false } restored &&
            restored.sect_current_rank == expectedRank && restored.sect_library_first_manual_path == human.sect_library_first_manual_path,
            "身份或首本秘籍未随正式快照保存");
    }
}
