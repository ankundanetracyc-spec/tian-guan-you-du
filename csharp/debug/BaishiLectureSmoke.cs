using Godot;
using System.Reflection;
using System.Threading.Tasks;
using D = Godot.Collections.Dictionary;

/// <summary>Actual scene, source permissions, cultivation and formal-save round trips.</summary>
public partial class BaishiLectureSmoke : Node
{
    private int _checks;
    private void Check(bool value, string why) { _checks++; if (!value) throw new Exception(why); }
    private static T Read<T>(object o, string name) => (T)o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(o)!;
    private static object? Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(o, args);
    private async Task Frames(int count = 3) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private void Drain(StoryDialoguePanel dialogue)
    {
        for (var i = 0; i < 30 && dialogue.Visible; i++) dialogue.close();
        Check(!dialogue.Visible, "对白未能结束");
    }
    private Button Button(Control panel, string text)
    {
        var buttons = panel.FindChildren("*", "Button", true, false).OfType<Button>().Where(b => b.IsVisibleInTree()).ToArray();
        return buttons.FirstOrDefault(b => b.Text.Replace("\n", "") == text) ?? throw new Exception("找不到按钮：" + text + "；现有：" + string.Join(" / ", buttons.Select(b => b.Text.Replace("\n", ""))));
    }
    public override async void _Ready()
    {
        try
        {
            Check(OS.GetEnvironment("APPDATA").Contains("baishi_lecture", StringComparison.OrdinalIgnoreCase), "必须使用隔离存档目录");
            await Frames(); // root must finish its initial ready traversal before adding gameplay
            GetTree().CurrentScene = null;
            var profile = GetNode<GameStartProfile>("/root/GameStartProfile");
            profile.roll_fated_aptitude();
            profile.fated_youngest_root_type = (int)GongfaResource.Wuxing.METAL_WOOD_WATER;
            profile.fated_youngest_root_name = "金木水三灵根";
            profile.fated_youngest_root_weights = new Godot.Collections.Array { 34, 33, 33, 0, 0 };
            profile.fated_base_physique = profile.fated_base_intelligence = profile.fated_base_soul = 5;
            profile.selected_initial_gongfa_path = GongfaAvailability.DefaultBookPath; // stale presets must not pregrant
            var saveManager = GetNode<GameSaveManager>("/root/GameSaveManager");
            saveManager.begin_new_game("白石同行验收");
            var main = GD.Load<PackedScene>("res://tscn/scene/MainScene.tscn").Instantiate<MainScene>();
            GetTree().Root.AddChild(main); GetTree().CurrentScene = main;
            for (var i = 0; i < 180 && Read<Character?>(main, "player") is null; i++) await Frames(1);
            await Frames(12);
            Check(Read<Character?>(main, "player") is not null, "主场景未生成角色");
            var actor = Read<Character>(main, "player");
            var human = (HumanData)actor.entity_data!;
            Check(human.intro_gongfa_path == "" && actor.main_gongfa is null && human.learned_gongfa_paths.Count == 0, "新旅程提前发放功法");
            var state = Read<BaishiLectureProgress>(main, "_baishiLecture");
            var panel = Read<WorldLocationInteriorPanel>(main, "location_interior");
            var dialogue = Read<StoryDialoguePanel>(main, "story_dialogue");
            Check(panel.Visible && !state.departed_qingxi, "没有进入新序章苏醒场景");
            await Capture("lecture_awake");
            Drain(dialogue); await Frames();
            Check(!main.try_return_to_previous_page(), "苏醒地点根页面应允许通用菜单接管返回键");
            var roster = GetTree().GetFirstNodeInGroup("cell_character_list") as CellCharacterList;
            Check(roster is not null && Read<string>(roster, "_roster_context").StartsWith("interior:qingxi"), "方远未进入公共人物列表");
            var interactions = Read<NpcInteractionPanel>(main, "npc_interactions");
            var openingActions = panel.GetNode<Control>("QingxiOpeningActions");
            Check(Button(openingActions, "与方远说话").IsInGroup("scene_action_tag"), "开场对话应使用中央吊牌");
            Check(Read<MainHUD>(main, "hud").FindChildren("TaskEntry*", "Button", true, false).OfType<Button>().Count(b => b.IsVisibleInTree()) == 1,
                "开场任务栏不应重复相同目标");
            await Capture("qingxi_talk_tag");
            Button(openingActions, "与方远说话").EmitSignal(BaseButton.SignalName.Pressed);
            await Capture("lecture_dialogue"); Drain(dialogue);
            Check(state.met_fang && state.departed_qingxi && state.traveling_to_baishi &&
                openingActions.GetNodeOrNull<Button>("Action_前往白石") is null,
                "与方远完成对话后应自动启程，且不显示重复的前往白石吊牌");
            await Capture("qingxi_auto_departure");
            Call(main, "initialize_road_encounter_clock");
            Read<GodotObject>(main, "_roadEncounterClock").Set("distance_until_encounter", 10000.0);
            await Frames();
            var map = Read<Control>(main, "_nodeWorldMap");
            var manager = GetNode<NpcWorldManager>("/root/NpcWorldManager");
            Check(state.departed_qingxi && state.traveling_to_baishi && map.Visible && !panel.Visible, "同行没有从地图出发");
            Check(manager.get_player_travel_party().Any(n => n.npc_id == state.companion.npc_id), "方远不是实际同行成员");
            Check(state.host_snapshot is null && map.Call("get_current_location_runtime_id").AsString() != "white_stone_town", "出发时已经到城或提前进入讲法处");
            // Retired saves without a Baishi state resume the world, preserving all player state.
            // Keep the live resource when restoring this in-memory checkpoint so the real party identity is unchanged.
            var retirementCheckpoint = (D)Call(main, "capture_scene_progress")!;
            retirementCheckpoint["baishi_lecture"] = state;
            var legacyProgressForRetirement = retirementCheckpoint.Duplicate(true);
            legacyProgressForRetirement.Remove("baishi_lecture");
            legacyProgressForRetirement.Remove("story_free_start");
            legacyProgressForRetirement["qingxi_prologue"] = 0;
            legacyProgressForRetirement["back_hill_trial_complete"] = false;
            var retirementStats = (actor.unique_id, actor.entity_name, actor.cultivation_realm, actor.current_health,
                actor.current_world_stamina, actor.physique, actor.intelligence, actor.soul);
            var retirementPaths = human.inventory_item_paths.ToArray();
            var retirementCounts = human.inventory_item_counts.ToArray();
            var retirementPlacement = string.Join("|", actor.get_spatial_inventory().Select(e => $"{e.InstanceId}:{e.RegionId}:{e.X},{e.Y}:{e.Rotation}"));
            var retirementClock = GetNode<GameTimeMgr>("/root/GameTimeManager");
            var retirementHour = retirementClock.get_precise_game_hours();
            var retirementNode = Read<string>(main, "_player_world_node_id");
            var retirementPosition = map.Call("get_world_position").AsVector2();
            Call(main, "restore_scene_progress", legacyProgressForRetirement);
            Check(Read<BaishiLectureProgress?>(main, "_baishiLecture") is null && (bool)Call(main, "has_open_world_access")!,
                "无白石状态的旧档仍依赖废弃序章或补进新开场");
            Check(ReferenceEquals(actor, Read<Character>(main, "player")) && retirementStats ==
                (actor.unique_id, actor.entity_name, actor.cultivation_realm, actor.current_health,
                 actor.current_world_stamina, actor.physique, actor.intelligence, actor.soul), "旧档兼容改变人物、境界或属性");
            Check(retirementPaths.SequenceEqual(human.inventory_item_paths) && retirementCounts.SequenceEqual(human.inventory_item_counts) &&
                retirementPlacement == string.Join("|", actor.get_spatial_inventory().Select(e => $"{e.InstanceId}:{e.RegionId}:{e.X},{e.Y}:{e.Rotation}")),
                "旧档兼容补发物品或改变库存摆放");
            Check(retirementClock.get_precise_game_hours() == retirementHour && Read<string>(main, "_player_world_node_id") == retirementNode &&
                map.Call("get_world_position").AsVector2().DistanceTo(retirementPosition) < .0001f, "旧档兼容改变时间或有效世界位置");
            var retiredCapture = (D)Call(main, "capture_scene_progress")!;
            Check(!retiredCapture.ContainsKey("qingxi_prologue") && !retiredCapture.ContainsKey("back_hill_trial_complete") &&
                !retiredCapture.ContainsKey("active_quest"), "废案进度键仍被重新保存");
            Call(main, "restore_scene_progress", retirementCheckpoint);
            Check(ReferenceEquals(state, Read<BaishiLectureProgress>(main, "_baishiLecture")) && state.traveling_to_baishi,
                "内存兼容验证未恢复真实同行状态");
            if (OS.GetCmdlineUserArgs().Contains("--opening-only"))
            {
                main.prepare_return_to_title_from_menu(false); main.QueueFree(); await Frames(8);
                GD.Print($"BAISHI_OPENING_TAGS_OK {_checks}"); GetTree().Quit(); return;
            }
            var partyViews = map.Get("party_views").AsGodotDictionary();
            Check(partyViews.ContainsKey(state.companion.npc_id), "地图未显示方远的同行形象");
            await Capture("baishi_travel_start");
            map.Call("stop_free_travel");
            var before = map.Call("get_world_position").AsVector2();
            var clock = GetNode<GameTimeMgr>("/root/GameTimeManager");
            var beforeHours = clock.get_precise_game_hours();
            Check(map.Call("travel_to_runtime_location", "white_stone_town").AsBool(), "无法从出发位置走向白石");
            map.Call("_advance_motion", 8.0f);
            map.Call("_update_party_views");
            var after = map.Call("get_world_position").AsVector2();
            var follower = partyViews[state.companion.npc_id].AsGodotObject() as Node2D;
            Check(after.DistanceTo(before) > 1 && clock.get_precise_game_hours() > beforeHours, "路途没有真实移动和推进时间");
            Check(follower is not null && follower.Visible && follower.Position.DistanceTo(after) > 0.1f, "方远没有在主角身后显示");
            Check(follower is not null && (after - follower.Position).Dot(after - before) > 0, "方远没有跟在行进方向的后方");
            await Capture("baishi_travel_in_motion");
            clock.is_paused = true;
            var savePosition = map.Call("get_world_position").AsVector2();
            var journeySave = main.capture_battle_world_snapshot()!;
            Check(journeySave.world_npcs.Any(n => n.npc_id == state.companion.npc_id && n.is_player_travel_companion), "同行成员未进入正式快照");
            Check(((BaishiLectureProgress)journeySave.scene_progress["baishi_lecture"].AsGodotObject()).traveling_to_baishi, "在途序章状态未保存");
            Check(saveManager.save_snapshot(journeySave) && saveManager.load_slot(saveManager.active_slot_id), "同行途中正式存档写入或读取失败");
            main.prepare_return_to_title_from_menu(false);
            main.QueueFree(); await Frames(8);
            main = GD.Load<PackedScene>("res://tscn/scene/MainScene.tscn").Instantiate<MainScene>();
            GetTree().Root.AddChild(main); GetTree().CurrentScene = main;
            for (var i = 0; i < 240 && !main.StartupComplete; i++) await Frames(1);
            await Frames(12);
            Check(main.StartupError is null && main.StartupComplete, "途中存档未能重建主场景");
            actor = Read<Character>(main, "player"); human = (HumanData)actor.entity_data!;
            state = Read<BaishiLectureProgress>(main, "_baishiLecture");
            panel = Read<WorldLocationInteriorPanel>(main, "location_interior");
            dialogue = Read<StoryDialoguePanel>(main, "story_dialogue");
            interactions = Read<NpcInteractionPanel>(main, "npc_interactions");
            map = Read<Control>(main, "_nodeWorldMap");
            Check(state.traveling_to_baishi && state.companion == manager.get_npc(state.companion.npc_id) && state.companion.is_player_travel_companion,
                "重进主场景后方远没有恢复为同一同行人物");
            Check(map.Get("party_views").AsGodotDictionary().ContainsKey(state.companion.npc_id), "读档后方远的地图形象丢失");
            Check(map.Call("get_world_position").AsVector2().DistanceTo(savePosition) < .01f, "读档改变了途中位置");
            clock.is_paused = false;
            map.Call("resume_saved_travel");
            for (var i = 0; i < 20 && map.Call("is_traveling").AsBool(); i++) map.Call("_advance_motion", 100.0f);
            Check(!map.Call("is_traveling").AsBool() && map.Call("get_current_location_runtime_id").AsString() == "white_stone_town", "同行没有真实抵达白石");
            Check(state.traveling_to_baishi && state.host_snapshot is null, "抵达城门时跳过了进城");
            map.Call("_enter_current"); await Frames();
            Check(panel.Visible && panel.get_baishi_space_id() == "" && !state.traveling_to_baishi, "进入白石没有先显示城内总览");
            Check(state.companion.is_player_travel_companion && manager.get_player_travel_party().Any(n => n.npc_id == state.companion.npc_id), "入城后方远没有继续同行");
            roster = GetTree().GetFirstNodeInGroup("cell_character_list") as CellCharacterList;
            Check(roster is not null && Read<string>(roster, "_worldRosterStamp").Contains("方远"), "白石总览人物名单没有同行的方远");
            var city = panel.GetNode<Control>("BaishiCityOverview");
            city.Call("open_district", "fuqian");
            Check(Read<string>(roster!, "_worldRosterStamp").Contains("方远"), "进入街区后方远没有继续同行");
            city.Call("open_space", "city_office");
            city.Call("open_space", "lecture"); await Frames();
            Check(state.host_snapshot is not null, "到讲法处后未分配主持修士");
            Check(state.host_actor_id != "" && !string.IsNullOrWhiteSpace(state.host_snapshot!.npc_name), "主持人缺少实际身份");
            var host = state.host_actor_id; var hostName = state.host_snapshot!.npc_name;
            Call(main, "show_baishi_lecture"); Check(host == state.host_actor_id, "重开场景重抽主持人");
            main.open_world_npc_actions(state.host_snapshot!);
            Check(!interactions.FindChildren("*", "Button", true, false).OfType<Button>().Any(b => b.Text.Replace("\n", "") is "听讲法" or "旁听讲法"),
                "公共旁听不应仍挂在主持人物菜单");
            interactions.close();
            Button(city, "听讲并验灵").EmitSignal(BaseButton.SignalName.Pressed); Drain(dialogue);
            Check(state.heard_lecture && state.roots_checked && !city.GetNode<Control>("LectureGuidance").Visible,
                "讲法与个人验灵未在同一流程完成或引导未收起");
            var roots = human.root_weights.ToArray();
            Check(roots.SequenceEqual(human.root_weights), "讲法后的个人验灵改动了既有灵根资质");
            Check(state.selected_sect_id == "" && !state.roots_session_finished && !state.spirit_boat_in_transit,
                "个人验灵结束不应直接选宗门或启航");
            Button(city, "选择宗门").EmitSignal(BaseButton.SignalName.Pressed);
            Button(dialogue, "玉虚仙宗").EmitSignal(BaseButton.SignalName.Pressed); Drain(dialogue); await Frames();
            Check(state.selected_sect_id == "yuxu_xianzong" && !state.roots_session_finished && human.sect_affiliation_id == "",
                "选定去向不应提前结束整场验灵或录籍");
            Check(city.GetNodeOrNull<Button>("FacilityLabels/Action_lecture_board") is null,
                "整场验灵未完就开放了登舟");
            Button(city, "等候验灵").EmitSignal(BaseButton.SignalName.Pressed); Drain(dialogue); await Frames();
            Check(state.roots_session_finished && !state.spirit_boat_in_transit && !state.fang_returned,
                "等候结束应开放登舟，不自动启航或跳过道别");
            if (OS.GetCmdlineUserArgs().Contains("--spirit-boat"))
            {
                var extra = new WorldNpcData { npc_id = "baishi_boat_extra_companion", npc_name = "随行夹具", is_alive = true };
                Check(manager.join_player_travel_party(extra), "接引测试同行者无法加入");
                for (var i = 0; i < 3; i++)
                    Check(manager.join_player_travel_party(new WorldNpcData { npc_id = $"baishi_boat_extra_{i}", npc_name = $"随行{i}", is_alive = true }), "接引同行人数未满时加入失败");
                var excessBeforeBoarding = new WorldNpcData { npc_id = "baishi_boat_excess_before", npc_name = "超载随行", is_alive = true };
                Check(manager.join_player_travel_party(excessBeforeBoarding), "登舟前同行测试准备失败");
                Button(city, "登舟启程").EmitSignal(BaseButton.SignalName.Pressed);
                Drain(dialogue); await Frames();
                Check(state.fang_returned && !state.spirit_boat_in_transit && string.IsNullOrEmpty(state.spirit_boat_driver_id),
                    "超载时接引舟仍然启航");
                Check(manager.leave_player_travel_party(excessBeforeBoarding.npc_id, "white_stone_town", "", "停留"), "测试同行者未能在白石离队");
                Button(city, "登舟启程").EmitSignal(BaseButton.SignalName.Pressed);
                Drain(dialogue); await Frames();
                map = Read<Control>(main, "_nodeWorldMap");
                Check(state.fang_returned && state.chose_yuxu && state.spirit_boat_in_transit, "验灵后未能道别并乘舟");
                Check(actor.cultivation_realm == "凡人" && human.intro_gongfa_path == "" && human.sect_affiliation_id == "", "乘舟提前修炼、发书或录籍");
                var driverId = state.spirit_boat_driver_id;
                Check(manager.get_npc(driverId) is { is_player_travel_companion: true } && map.Get("movement_mode").AsString() == "flight", "驾舟弟子不是实际同行人物");
                var onboard = map.Get("flight_boat").AsGodotObject().Get("passenger_ids").AsStringArray();
                Check(onboard.Contains(driverId) && onboard.Contains(extra.npc_id) && onboard.Length == 5, "接引舟未同时显示驾舟者和全部同行者");
                var overflow = new WorldNpcData { npc_id = "baishi_boat_over_capacity", npc_name = "超载夹具", is_alive = true };
                Check(!manager.join_player_travel_party(overflow) && manager.get_npc(overflow.npc_id) is null, "接引舟满载后仍允许加入同行");
                Check(Math.Abs(map.Get("flight_speed").AsDouble() - 4800.0) < .01, "接引没有使用灵舟自身遁速");
                var kmPerPixel = GD.Load<Script>("res://gdscripts/world/BaiChuanWorldScale.gd").GetScriptConstantMap()["KM_PER_PIXEL"].AsDouble();
                var boatStart = map.Call("get_world_position").AsVector2();
                var boatDestination = map.Get("waypoints").AsGodotArray()[0].AsGodotDictionary()["position"].AsVector2();
                var expectedHours = boatStart.DistanceTo(boatDestination) * kmPerPixel / 80.0;
                Check(expectedHours > 9.5 && expectedHours < 10.0 && Math.Abs(map.Call("_remaining_hours").AsDouble() - expectedHours) < .002,
                    "接引预计时间没有按80公里每游戏小时结算");
                await Capture("spirit_boat_formal_flight_20261106");
                clock.is_paused = true;
                var departureHours = clock.get_precise_game_hours();
                for (var i = 0; i < 20; i++) map.Call("_advance_flight", .1f);
                var boatPosition = map.Call("get_world_position").AsVector2();
                Check(map.Get("flight_height").AsDouble() > 0 && boatPosition.DistanceTo(map.Get("waypoints").AsGodotArray()[0].AsGodotDictionary()["position"].AsVector2()) > 1, "没有在空中真实移动");
                Check(Math.Abs(clock.get_precise_game_hours() - departureHours - boatStart.DistanceTo(boatPosition) * kmPerPixel / 80.0) < .002,
                    "接引实际行程耗时未按新版舟速推进");
                var airSave = main.capture_battle_world_snapshot()!;
                Check(airSave.world_npcs.Any(n => n.npc_id == driverId && n.is_player_travel_companion), "驾舟弟子未进入正式存档");
                var otherFlight = airSave.node_world_state.Duplicate(true);
                otherFlight["flight_driver_id"] = "other_driver"; otherFlight["flight_speed"] = 2400.0;
                Call(main, "upgrade_saved_baishi_escort_speed", otherFlight);
                Check(otherFlight["flight_speed"].AsDouble() == 2400.0, "无关驾驶者的旧行程不能被改速");
                var invalidFlight = airSave.node_world_state.Duplicate(true); invalidFlight["flight_speed"] = double.NaN;
                Call(main, "upgrade_saved_baishi_escort_speed", invalidFlight);
                Check(double.IsNaN(invalidFlight["flight_speed"].AsDouble()), "无效舟速不能被迁移掩盖");
                var interimFlight = airSave.node_world_state.Duplicate(true); interimFlight["flight_speed"] = 12000.0;
                Call(main, "upgrade_saved_baishi_escort_speed", interimFlight);
                Check(interimFlight["flight_speed"].AsDouble() == 4800.0,
                    $"上个版本接引舟速没有迁移：{interimFlight["flight_speed"]}，模式{interimFlight.GetValueOrDefault("movement_mode", "")}，驾驶{interimFlight.GetValueOrDefault("flight_driver_id", "")}，剧情驾驶{state.spirit_boat_driver_id}，在途{state.spirit_boat_in_transit}");
                airSave.node_world_state["flight_speed"] = 2400.0; // prior reception-boat save
                Check(saveManager.save_snapshot(airSave) && saveManager.load_slot(saveManager.active_slot_id), "空中正式存档写读失败");
                main.prepare_return_to_title_from_menu(false); main.QueueFree(); await Frames(8);
                main = GD.Load<PackedScene>("res://tscn/scene/MainScene.tscn").Instantiate<MainScene>();
                GetTree().Root.AddChild(main); GetTree().CurrentScene = main;
                for (var i = 0; i < 240 && !main.StartupComplete; i++) await Frames(1);
                await Frames(12);
                Check(main.StartupError is null && main.StartupComplete, "空中存档重建主场景失败");
                actor = Read<Character>(main, "player"); human = (HumanData)actor.entity_data!;
                state = Read<BaishiLectureProgress>(main, "_baishiLecture");
                dialogue = Read<StoryDialoguePanel>(main, "story_dialogue");
                map = Read<Control>(main, "_nodeWorldMap");
                Check(state.spirit_boat_in_transit && state.spirit_boat_driver_id == driverId && manager.get_npc(driverId) is { is_player_travel_companion: true }, "重载丢失驾舟人或行程");
                Check(map.Get("movement_mode").AsString() == "flight" && map.Call("get_world_position").AsVector2().DistanceTo(boatPosition) < .01f, "重载丢失空中位置");
                Check(Math.Abs(map.Get("flight_speed").AsDouble() - 4800.0) < .01, "旧版接引舟在途存档没有更新剩余旅程的速度");
                clock.is_paused = false;
                map.Call("resume_saved_travel");
                for (var i = 0; i < 2000 && map.Call("is_traveling").AsBool(); i++) map.Call("_advance_flight", .1f);
                Check(!map.Call("is_traveling").AsBool() && map.Call("get_current_location_runtime_id").AsString() == "yuxu_xianzong_core", "飞舟没有实际到达玉虚");
                Check(!state.spirit_boat_in_transit && manager.get_npc(driverId) is { is_player_travel_companion: false, current_node_id: "yuxu_xianzong_core" }, "抵达后驾舟弟子没有在玉虚离队");
                Check(human.sect_affiliation_id == "", "抵达即自动入宗");
                map.Call("_enter_current"); await Frames();
                for (var i = 0; i < 10 && human.sect_affiliation_id == ""; i++)
                {
                    Call(dialogue, "process_social_reveal", 100d);
                    var noSect = dialogue.FindChildren("*", "Button", true, false).OfType<Button>().FirstOrDefault(b => b.IsVisibleInTree() && b.Text == "无");
                    if (noSect is not null) noSect.EmitSignal(BaseButton.SignalName.Pressed);
                    else
                    {
                        var done = Read<Button>(dialogue, "_socialDone");
                        if (done.IsVisibleInTree()) done.EmitSignal(BaseButton.SignalName.Pressed);
                    }
                    await Frames();
                }
                Check(human.sect_affiliation_id == "yuxu_xianzong", "凡人乘舟到山门后无法按原流程入宗");
                main.prepare_return_to_title_from_menu(false); main.QueueFree(); await Frames(8);
                GD.Print($"BAISHI_SPIRIT_BOAT_SMOKE_OK {_checks}"); GetTree().Quit(); return;
            }
            main.open_world_npc_actions(state.companion);
            Button(interactions, "交谈").EmitSignal(BaseButton.SignalName.Pressed); Drain(dialogue);
            Check(state.fang_returned && state.companion.current_state == "返乡" && !state.companion.is_player_travel_companion &&
                  manager.get_player_travel_party().All(n => n.npc_id != state.companion.npc_id), "方远未离队返乡");
            Check(!Read<string>(roster!, "_worldRosterStamp").Contains("方远"), "方远离开后仍留在人物名单");
            await Capture("lecture_hall");
            var stamp = GetNode<GameTimeMgr>("/root/GameTimeManager").get_total_game_hours();
            main.open_world_npc_actions(state.host_snapshot!);
            Button(interactions, "选读功法").EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            var bookPanel = Read<InfoPanel>(main, "info");
            Check(human.intro_gongfa_path == "", "浏览书目提前确定功法");
            var books = GongfaAvailability.EnabledBooks().Where(b => b.can_be_main).ToArray();
            var book = books.First(b => actor.can_learn_gongfa_chapter(b, b.get_sorted_chapters().First()));
            var first = book.get_sorted_chapters().First();
            Button(bookPanel, book.gongfa_name).EmitSignal(BaseButton.SignalName.Pressed);
            Check(human.intro_gongfa_path == "", "查看详情消耗研习权");
            await Capture("lecture_selection");
            Button(bookPanel, "选读这一卷").EmitSignal(BaseButton.SignalName.Pressed);
            Button(bookPanel, "确认选读这一卷").EmitSignal(BaseButton.SignalName.Pressed);
            Check(human.intro_gongfa_path == book.ResourcePath && human.baishi_first_volume_only, "未登记首卷权限");
            Check(actor.get_gongfa_chapter_study_progress(book, first.get_id()) == 0 && human.intro_gongfa_study_progress == 0, "选书赠送了研读进度");
            Check(GetNode<GameTimeMgr>("/root/GameTimeManager").get_total_game_hours() == stamp, "查看或选书意外推进时间");
            Call(main, "open_baishi_study"); await Frames();
            Check(panel.get_baishi_space_id() == "lecture_study", "研习没有进入实际侧屋");
            var teaching = Read<CultivationUI>(main, "cultivation_ui");
            Check(teaching.Visible, "符合条件后没有接通已有研读吐纳界面");
            var town = panel.GetNode<Control>("BaishiCityOverview");
            Check(town.GetNodeOrNull<Button>("FacilityLabels/Action_lesson_study")?.IsVisibleInTree() == true &&
                  town.GetNodeOrNull<Button>("FacilityLabels/Action_lesson_rest")?.IsVisibleInTree() == true,
                  "验灵选书并道别后没有开放本期支持入口");
            teaching.close_panel();
            Check(GetNode<GameTimeMgr>("/root/GameTimeManager").get_total_game_hours() == stamp, "打开研习界面自动结算了时间");
            var result = actor.study_for_world_hours(1);
            Check(result.GetValueOrDefault("success", false).AsBool(), "不能正常研读首卷");
            Check(actor.get_gongfa_chapter_study_progress(book, first.get_id()) > 0, "研读未写进度");
            var other = books.First(b => b != book && actor.can_learn_gongfa_chapter(b, b.get_sorted_chapters().First()));
            Check(!actor.try_select_baishi_first_volume(other), "开始研读后仍可免费换书");
            // Complete by paid study actions; rest likewise uses real recovery and game hours.
            for (var i = 0; i < 80 && !actor.has_completed_intro_gongfa_first_volume(); i++)
            {
                if (actor.get_available_study_hours(24) == 0) { actor.restore_world_stamina(100); GetNode<GameTimeMgr>("/root/GameTimeManager").add_hours(100); }
                actor.study_for_world_hours(24);
            }
            Check(actor.has_completed_intro_gongfa_first_volume(), "首卷无法读成");
            Check(actor.switch_main_gongfa(book), "首卷读成后不能装配主修");
            foreach (var later in book.get_sorted_chapters().Skip(1)) Check(!actor.can_learn_gongfa_chapter(book, later), "公开首卷泄露后续卷权限");
            for (var i = 0; i < 250 && actor.cultivation_realm == "凡人"; i++)
            {
                actor.restore_world_stamina(100);
                GetNode<GameTimeMgr>("/root/GameTimeManager").add_hours(100);
                actor.cultivate_with_world_stamina_for_hours(100);
                if (actor.is_breakthrough_ready()) actor.start_breakthrough_retreat(false);
            }
            Check(actor.cultivation_realm == "云息一层", "正常吐纳与入境没有达到云息一层");
            Read<RealmPanel>(main, "realm_panel").Hide();
            Call(main, "refresh_qingxi_quest");
            Check(town.GetNodeOrNull<Button>("FacilityLabels/Action_lesson_study") is null &&
                  town.GetNodeOrNull<Button>("FacilityLabels/Action_lesson_rest") is null,
                  "入云息后仍开放本期研习支持");
            Call(main, "request_baishi_rest");
            Check(Read<RestPanel?>(main, "_restPanel")?.Visible != true, "入云息后还能直接调用本期暂歇");
            Call(main, "show_baishi_lecture");
            Check(state.reached_yunxi, "任务未识别实际入境");
            Check(state.selected_sect_id == "yuxu_xianzong" && state.roots_session_finished && human.sect_affiliation_id == "",
                "待登舟状态丢失或提前录籍");
            Check(Read<string>(main, "_player_world_node_id") == "white_stone_town", "等候结束后自动瞬移到玉虚");
            var save = (PersistentGameSave)Call(main, "capture_progress_snapshot", false)!;
            var path = "user://baishi_roundtrip.tres";
            Check(ResourceSaver.Save(save, path) == Error.Ok, "正式快照写盘失败");
            var restored = ResourceLoader.Load<PersistentGameSave>(path, "", ResourceLoader.CacheMode.Ignore)!;
            var copy = (BaishiLectureProgress)restored.scene_progress["baishi_lecture"].AsGodotObject();
            Check(copy.host_actor_id == host && copy.host_snapshot?.npc_name == hostName && copy.fang_returned &&
                copy.selected_sect_id == "yuxu_xianzong" && copy.roots_session_finished, "重载丢失主持／返乡／宗门及等候状态");
            Check(restored.player_data?.baishi_first_volume_path == book.ResourcePath && restored.player_data.cultivation_realm == "云息一层", "重载丢失功法与境界");
            var alternate = new BaishiLectureProgress(); alternate.AssignHost("player.test", true);
            Check(alternate.host_is_player && alternate.host_actor_id == "player.test", "活动角色不能接受玩家身份");
            await Capture("lecture_route");
            Call(main, "show_baishi_lecture"); await Frames(); await Capture("lecture_place");
            Call(main, "open_baishi_books"); await Frames(); await Capture("lecture_books");
            Call(main, "restore_scene_progress", restored.scene_progress);
            var resumed = Read<BaishiLectureProgress>(main, "_baishiLecture");
            Check(resumed.host_actor_id == host && resumed.fang_returned && resumed.selected_sect_id == "yuxu_xianzong" && resumed.roots_session_finished,
                "主场景恢复没有使用本期持久状态");
            Check(resumed.host_snapshot!.npc_id == host, "重载主持人身份与事件不符");
            if (!OS.GetCmdlineUserArgs().Contains("--ui-only")) TestEntryCoverage(books);
            var oldProgress = restored.scene_progress.Duplicate(true);
            oldProgress.Remove("baishi_lecture");
            Call(main, "restore_scene_progress", oldProgress);
            Check(Read<BaishiLectureProgress?>(main, "_baishiLecture") is null && human.intro_gongfa_path == book.ResourcePath,
                "旧档被补进新序章或丢失功法");
            Call(main, "restore_scene_progress", restored.scene_progress);
            // Place the fixture at the gate, then run the actual existing welcome and registration UI.
            typeof(MainScene).GetField("_player_world_node_id", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(main, "yuxu_xianzong_core");
            Call(main, "begin_yuxu_gate_welcome");
            for (var i = 0; i < 10 && human.sect_affiliation_id == ""; i++)
            {
                Call(dialogue, "process_social_reveal", 100d);
                var noSect = dialogue.FindChildren("*", "Button", true, false).OfType<Button>().FirstOrDefault(b => b.IsVisibleInTree() && b.Text == "无");
                if (noSect is not null) noSect.EmitSignal(BaseButton.SignalName.Pressed);
                else
                {
                    var done = Read<Button>(dialogue, "_socialDone");
                    if (done.IsVisibleInTree()) done.EmitSignal(BaseButton.SignalName.Pressed);
                }
                await Frames();
            }
            Check(human.sect_affiliation_id == "yuxu_xianzong" && human.sect_current_rank == "外门弟子", "序章之后未能正常完成玉虚入宗");
            Check(Read<BaishiLectureProgress>(main, "_baishiLecture").finished, "入宗后序章任务未收束");
            main.prepare_return_to_title_from_menu(false);
            main.QueueFree(); await Frames(8);
            GD.Print($"BaishiLectureSmoke: PASS ({_checks} checks)");
            var tree = GetTree();
            tree.CreateTimer(.2).Timeout += () => { GC.Collect(); GC.WaitForPendingFinalizers(); tree.Quit(); };
        }
        catch (Exception e) { GD.PushError("BaishiLectureSmoke: FAIL " + e); GetTree().Quit(1); }
    }
    private void TestEntryCoverage(GongfaResource[] books)
    {
        var probe = new Character(); AddChild(probe);
        probe.load_from_data(new HumanData { entity_name = "启蒙门槛检查", cultivation_realm = "凡人" });
        var unsupported = 0; var total = 0;
        for (var root = 1; root <= 31; root++)
        for (var physique = 2; physique <= 10; physique++)
        for (var intelligence = 2; intelligence <= 10; intelligence++)
        {
            var soul = 15 - physique - intelligence;
            if (soul is < 2 or > 10) continue;
            total++;
            probe.spirit_root_type = root; probe.physique = physique; probe.intelligence = intelligence; probe.soul = soul;
            if (!books.Any(b => probe.can_learn_gongfa_chapter(b, b.get_sorted_chapters().First()))) unsupported++;
        }
        GD.Print($"Baishi entry coverage: {total} legal combinations, {unsupported} have no currently eligible first volume (known limitation; mortal growth explicitly deferred).");
        probe.spirit_root_type = 1; probe.physique = 2; probe.intelligence = 3; probe.soul = 10;
        Check(books.All(b => !probe.try_select_baishi_first_volume(b)) && ((HumanData)probe.entity_data!).intro_gongfa_path == "",
            "属性不足时仍发默认功法或放宽首卷门槛");
        probe.spirit_root_type = 0;
        Check(books.All(b => !probe.try_select_baishi_first_volume(b)), "无灵根仍可登记入道研习权");
        probe.QueueFree();
    }
    private async Task Capture(string name)
    {
        if (!OS.GetCmdlineUserArgs().Contains("--capture")) return;
        await Frames(12); // the shared roster creates portraits across successive frames
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng("res://tmp/" + name + ".png");
    }
}
