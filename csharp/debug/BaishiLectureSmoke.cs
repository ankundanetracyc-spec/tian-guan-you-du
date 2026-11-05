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
            Drain(dialogue);
            Check(!main.try_return_to_previous_page(), "苏醒地点根页面应允许通用菜单接管返回键");
            var roster = GetTree().GetFirstNodeInGroup("cell_character_list") as CellCharacterList;
            Check(roster is not null && Read<string>(roster, "_roster_context").StartsWith("interior:qingxi"), "方远未进入公共人物列表");
            main.open_world_npc_actions(state.companion);
            var interactions = Read<NpcInteractionPanel>(main, "npc_interactions");
            await Capture("lecture_npc_menu");
            Button(interactions, "交谈").EmitSignal(BaseButton.SignalName.Pressed);
            await Capture("lecture_dialogue"); Drain(dialogue);
            main.open_world_npc_actions(state.companion);
            Button(interactions, "一同前往白石").EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            Check(state.departed_qingxi && state.host_snapshot is not null, "赴会未分配主持修士");
            Check(state.host_actor_id != "" && !string.IsNullOrWhiteSpace(state.host_snapshot!.npc_name), "主持人缺少实际身份");
            var host = state.host_actor_id; var hostName = state.host_snapshot!.npc_name;
            Call(main, "show_baishi_lecture"); Check(host == state.host_actor_id, "重开场景重抽主持人");
            main.open_world_npc_actions(state.host_snapshot!);
            Button(interactions, "听讲法").EmitSignal(BaseButton.SignalName.Pressed); Drain(dialogue);
            var roots = human.root_weights.ToArray();
            main.open_world_npc_actions(state.host_snapshot!);
            Button(interactions, "查验灵根").EmitSignal(BaseButton.SignalName.Pressed); Drain(dialogue);
            Check(state.roots_checked && roots.SequenceEqual(human.root_weights), "验灵重抽了资质或没有完成");
            main.open_world_npc_actions(state.companion);
            Button(interactions, "交谈").EmitSignal(BaseButton.SignalName.Pressed); Drain(dialogue);
            Check(state.fang_returned && state.companion.current_state == "返乡", "方远未返乡");
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
            main.open_world_npc_actions(state.host_snapshot!);
            Button(interactions, "前往玉虚问道").EmitSignal(BaseButton.SignalName.Pressed); Drain(dialogue); await Frames();
            Check(state.chose_yuxu && human.sect_affiliation_id == "", "指路提前录籍");
            Check(Read<string>(main, "_player_world_node_id") == "white_stone_town", "指路瞬移到玉虚");
            var save = (PersistentGameSave)Call(main, "capture_progress_snapshot", false)!;
            var path = "user://baishi_roundtrip.tres";
            Check(ResourceSaver.Save(save, path) == Error.Ok, "正式快照写盘失败");
            var restored = ResourceLoader.Load<PersistentGameSave>(path, "", ResourceLoader.CacheMode.Ignore)!;
            var copy = (BaishiLectureProgress)restored.scene_progress["baishi_lecture"].AsGodotObject();
            Check(copy.host_actor_id == host && copy.host_snapshot?.npc_name == hostName && copy.fang_returned && copy.chose_yuxu, "重载丢失主持／返乡／路线状态");
            Check(restored.player_data?.baishi_first_volume_path == book.ResourcePath && restored.player_data.cultivation_realm == "云息一层", "重载丢失功法与境界");
            var alternate = new BaishiLectureProgress(); alternate.AssignHost("player.test", true);
            Check(alternate.host_is_player && alternate.host_actor_id == "player.test", "活动角色不能接受玩家身份");
            await Capture("lecture_route");
            Call(main, "show_baishi_lecture"); await Frames(); await Capture("lecture_place");
            Call(main, "open_baishi_books"); await Frames(); await Capture("lecture_books");
            Call(main, "restore_scene_progress", restored.scene_progress);
            var resumed = Read<BaishiLectureProgress>(main, "_baishiLecture");
            Check(resumed.host_actor_id == host && resumed.fang_returned && resumed.chose_yuxu, "主场景恢复没有使用本期持久状态");
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
