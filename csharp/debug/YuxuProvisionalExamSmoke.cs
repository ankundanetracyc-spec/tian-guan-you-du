using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using D = Godot.Collections.Dictionary;

/// <summary>隔离存档下验证预备真传期限、真实研读及演法场景往返。</summary>
public partial class YuxuProvisionalExamSmoke : Node
{
    private const long Due = 360L * 24L;
    private int _checks;
    private bool _choiceLayoutCaptured;
    private MainScene _main = null!;
    private Character Actor => _main.get_player_character()!;
    private HumanData Human => (HumanData)Actor.entity_data!;
    private GameTimeMgr Clock => GetNode<GameTimeMgr>("/root/GameTimeManager");
    private BattleTransitionManager Transition => GetNode<BattleTransitionManager>("/root/BattleTransitionManager");
    private StoryDialoguePanel Dialogue => Read<StoryDialoguePanel>(_main, "story_dialogue");
    private static T Read<T>(object obj, string name) => (T)obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(obj)!;
    private static object? Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(obj, args);
    private void Check(bool ok, string message) { if (!ok) throw new Exception(message); _checks++; GD.Print("YUXU_EXAM_CHECK: " + message); }
    private async Task Frames(int count = 5) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private async Task Until(Func<bool> predicate, string message, int frames = 4000)
    { for (var i = 0; i < frames; i++) { if (predicate()) return; await Frames(1); } throw new Exception(message); }
    private void Drain() { for (var i = 0; i < 30 && Dialogue.Visible; i++) Dialogue.close(); }
    private async Task Capture(string file)
    {
        if (DisplayServer.GetName() == "headless" || !OS.GetCmdlineUserArgs().Contains("--capture")) return;
        await Frames(); await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var folder = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--capture-dir="))?[14..]
            ?? "res://docs/美术规范/玉虚真传考核_20261107/";
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(folder));
        Check(GetViewport().GetTexture().GetImage().SavePng(folder + file + ".png") == Error.Ok, "保存实际场景画面");
    }

    public override async void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        try
        {
            Check(OS.GetEnvironment("APPDATA").Contains("yuxu_exam"), "使用隔离存档目录");
            var args = OS.GetCmdlineUserArgs();
            var mode = args.FirstOrDefault(a => a.StartsWith("--case="))?[7..] ?? "battle";
            if (args.Contains("--capture")) GetWindow().Size = new Vector2I(1920, 1080);
            var saves = GetNode<GameSaveManager>("/root/GameSaveManager");
            Check(!saves.has_active_slot, "不打开正式存档槽");
            var book = YuxuManualCopy.Catalog()[1].Book;
            var chapters = book.get_sorted_chapters().Take(2).ToArray();
            var atDeadline = mode is "early" or "boundary" or "late" or "timeout" or "breakthrough";
            var legacy = mode == "legacy";
            var teacherLegacy = mode == "teacherlegacy";
            var teacherPassed = mode == "teacherpassed";
            var human = new HumanData
            {
                entity_name = "考核验收", cultivation_realm = mode == "breakthrough" ? "云息二层" : "云息三层",
                data_initialized = true, has_spirit_core = true, current_world_stamina = 100, max_world_stamina = 100,
                physique = 20, intelligence = 20, soul = 20, base_physique = 20, base_intelligence = 20, base_soul = 20,
                current_health = 10000, max_health = 10000,
                spirit_root_type = (int)GongfaResource.Wuxing.METAL, spirit_root_name = "金灵根", root_weights = [100, 0, 0, 0, 0],
                sect_affiliation_id = "yuxu_xianzong", sect_entry_rank = "预备真传", sect_current_rank = "预备真传",
                sect_entry_total_game_hours = 0, sect_yuxu_exam_start_hour = legacy ? -1 : 0,
                sect_yuxu_exam_realm_hour = mode == "breakthrough" ? -1 : 0,
                sect_yuxu_exam_book_hour = mode is "early" or "boundary" or "late" or "progress" ? -1 : 0,
                sect_yuxu_exam_trial_hour = mode is "battle" or "timeout" or "expired" || legacy ? -1 : 0,
                sect_library_first_manual_path = book.ResourcePath,
                main_gongfa_path = mode == "breakthrough" ? book.ResourcePath : "",
                sect_allowance_month_cursor = atDeadline ? 11 : 0,
            };
            human.learned_gongfa_paths.Add(book.ResourcePath);
            human.learned_gongfa_chapters[book.ResourcePath] = new Godot.Collections.Array<string>(
                mode is "early" or "boundary" or "late" or "progress" ? [chapters[0].get_id()] : chapters.Select(c => c.get_id()));
            if (teacherLegacy)
            {
                human.sect_current_rank = "真传"; human.sect_entry_rank = "真传";
                human.sect_yuxu_exam_start_hour = -1; human.sect_yuxu_exam_realm_hour = -1;
                human.sect_yuxu_exam_book_hour = -1; human.sect_yuxu_exam_trial_hour = -1;
                human.sect_library_first_manual_path = ""; human.learned_gongfa_paths.Clear();
                human.learned_gongfa_chapters.Clear();
            }
            if (teacherPassed) { human.sect_current_rank = "真传"; human.sect_yuxu_exam_result = 1; }
            var fixture = new PersistentGameSave
            {
                player_data = human, current_node_id = "yuxu_xianzong_core", world_cell = new Vector2I(34, 166),
                node_world_state = new D { ["current_node"] = "bc_place_001", ["remaining_route"] = new Godot.Collections.Array<string>() },
                year = legacy || mode == "expired" ? 2 : 1, month = legacy ? 2 : atDeadline ? 12 : 1,
                day = atDeadline ? 30 : 1, hour = atDeadline ? 22 : 0,
                npc_population_version = 1, npc_simulation_enabled = false,
                scene_progress = new D { ["story_free_start"] = true, ["cloud_state_initialized"] = true, ["baishi_lecture"] = new BaishiLectureProgress { departed_qingxi = true, fang_returned = true, finished = true } },
            };
            typeof(GameSaveManager).GetField("_pendingLoad", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(saves, fixture);
            GetTree().CurrentScene = null;
            Check(GetTree().ChangeSceneToFile("res://tscn/scene/MainScene.tscn") == Error.Ok, "加载正式主场景");
            await Until(() => GetTree().CurrentScene is MainScene { StartupComplete: true }, "主场景未就绪");
            _main = (MainScene)GetTree().CurrentScene; await Frames(15); Drain();
            Check(_main.StartupError is null && Human.sect_current_rank == (mode == "expired" ? "内门弟子" : teacherLegacy || teacherPassed ? "真传" : "预备真传"), "读取后身份正确恢复");

            if (mode is "early" or "boundary" or "late") await CheckStudyBoundary(book, mode);
            else if (mode == "timeout") CheckTimeout();
            else if (mode == "expired") CheckExpiredLoad();
            else if (mode == "legacy") CheckLegacy();
            else if (mode == "breakthrough") CheckBreakthroughBoundary();
            else if (mode == "progress") await CheckProgress();
            else if (teacherLegacy || teacherPassed) await CheckTeacherSelection();
            else await CheckBattle();
            GD.Print($"YUXU_EXAM_PASS case={mode} checks={_checks}"); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private async Task CheckStudyBoundary(GongfaResource book, string mode)
    {
        Check(Clock.get_total_game_hours() == Due - 2, "研读从截止前两时开始");
        Check(Actor.add_item_to_inventory(YuxuManualCopy.Catalog()[1].Item.ResourcePath), "首本实体书在背包");
        Check(Actor.select_gongfa_for_study(book), "按现有条件开始研读第二卷");
        var study = Actor.GetChildren().OfType<SecretBook>().Single(b => b.is_studying_now);
        var rate = Actor.get_learning_rate();
        study.current_study_progress = study.get_required_study_progress() - rate * (mode == "early" ? .5f : mode == "boundary" ? 1.5f : 3.5f);
        var result = Actor.study_for_world_hours(6); await Frames();
        Check(result.GetValueOrDefault("success", false).AsBool() && Clock.get_total_game_hours() == Due + 4, "六时研读确实跨过期限");
        var completedAt = Due + (mode == "early" ? -1 : mode == "boundary" ? 0 : 2);
        var passed = mode != "late";
        Check(Human.sect_yuxu_exam_book_hour == completedAt, "按卷章实际完成时刻记录");
        Check(Human.sect_yuxu_exam_result == (passed ? 1 : 2) && Human.sect_current_rank == (passed ? "真传" : "内门弟子"), "按截止前后区分转正与转内门");
        Check(Human.sect_yuxu_exam_resolved_hour == (passed ? completedAt : Due), "身份变更记在真实完成或截止时刻");
        Check(Actor.is_gongfa_chapter_learned(book, book.get_sorted_chapters().Skip(1).First().get_id()) &&
            Actor.get_inventory_total_count(YuxuManualCopy.Catalog()[1].Item.ResourcePath) == 1, "跨期后保留真实学习记录与实体书");
        if (passed) Check(Human.sect_true_disciple_lecture_next_due_hour == completedAt + Due, "讲席从正式转正起算");
    }

    private void CheckExpiredLoad()
    {
        Check(Clock.get_total_game_hours() == Due && Human.sect_yuxu_exam_result == 2 && Human.sect_yuxu_exam_resolved_hour == Due,
            "读入已到期存档立即转内门，不等待下一次行动");
    }

    private void CheckTimeout()
    {
        Check(Clock.get_total_game_hours() == Due - 2 && Human.sect_yuxu_exam_trial_hour < 0, "开始时还有两时且演法未完成");
        Clock.add_hours(1); Check(Human.sect_current_rank == "预备真传", "期限内仍保留预备身份");
        Clock.add_hours(1); Check(Human.sect_current_rank == "内门弟子" && Human.sect_yuxu_exam_result == 2 && Human.sect_yuxu_exam_resolved_hour == Due,
            "到期自动转内门，无需找人或再次确认");
        var atDue = Human.sect_unclaimed_spirit_stones;
        Clock.add_hours(2 * 30 * 24);
        Check(Human.sect_unclaimed_spirit_stones == atDue + 2 * YuxuSectPrototypeBalance.monthly_stipend("内门弟子"), "到期后的月例按新身份累计");
    }

    private void CheckLegacy()
    {
        var now = Clock.get_total_game_hours();
        Check(Human.sect_yuxu_exam_start_hour == now && Human.sect_yuxu_exam_realm_hour == now && Human.sect_yuxu_exam_book_hour == now,
            "旧预备真传从首次进入起计一年并承认已学境界书卷");
        Check(Human.sect_yuxu_exam_trial_hour == -1 && Human.sect_current_rank == "预备真传", "旧档不虚构新增演法的胜利");
    }

    private void CheckBreakthroughBoundary()
    {
        var realms = GetNode<RealmManager>("/root/RealmManager");
        Actor.spirit_strands = realms.get_effective_requirements_for_next_realm(Actor).GetValueOrDefault("spirit_strands", 9999).AsInt32();
        Human.is_in_breakthrough_retreat = true; Human.breakthrough_target_realm = "云息三层";
        Human.breakthrough_started_hour = (int)Due - 2; Human.breakthrough_finish_hour = (int)Due - 1;
        Check(realms.can_breakthrough(Actor), "真实突破条件已满足");
        Clock.add_hours(6);
        Check(Actor.cultivation_realm == "云息三层" && Human.sect_yuxu_exam_realm_hour == Due - 1,
            "跨期闭关按突破完成时刻记录境界");
        Check(Human.sect_current_rank == "真传" && Human.sect_yuxu_exam_resolved_hour == Due - 1,
            "截止前突破在长行动结束后仍正确转正");
    }

    private async Task CheckProgress()
    {
        Call(_main, "open_yuxu_peak_after_admission", "tianwen"); await Frames(12);
        var peak = _main.GetNode<Control>("SceneLayer/YuxuXianzongOverview/PeakInterior");
        var tag = (Button)peak.Get("_action_buttons").AsGodotDictionary()["provisional_exam_trial"].AsGodotObject();
        Check(tag.IsVisibleInTree(), "演法已过而秘籍未成时保留考核吊牌以查看进度");
        tag.EmitSignal(BaseButton.SignalName.Pressed); await Frames();
        var options = Dialogue.FindChildren("*", "Button", true, false).OfType<Button>().Where(b => b.IsVisibleInTree()).ToArray();
        Check(Dialogue.Visible && options.Any(b => b.Text == "知道了") && options.All(b => b.Text != "开始演法"), "已取胜后显示进度且不重复开启战斗");
        var body = Read<Label>(Dialogue, "_body").Text;
        Check(body.Contains("秘籍：未学会") && body.Contains("演法：已取胜"), "下方对白反映真实考核进度");
    }

    private async Task OpenTrial()
    {
        Call(_main, "open_yuxu_peak_after_admission", "tianwen"); await Frames(12);
        var peak = _main.GetNode<Control>("SceneLayer/YuxuXianzongOverview/PeakInterior");
        var tag = (Button)peak.Get("_action_buttons").AsGodotDictionary()["provisional_exam_trial"].AsGodotObject();
        Check(tag.IsVisibleInTree() && tag.IsInGroup("scene_action_tag"), "天问峰场景中央显示考核吊牌");
        await Capture("天问峰_考核吊牌");
        tag.EmitSignal(BaseButton.SignalName.Pressed); await Frames();
        Check(Dialogue.Visible, "吊牌打开下方考核对白");
        Check(Read<Label>(Dialogue, "_speaker").Text == "旁白" && Read<Label>(Dialogue, "_body").Text.StartsWith("真传考核："),
            "考核说明以旁白为说话者，事项标题位于正文开头");
        await Capture("天问峰_考核说明与进度");
        if (DisplayServer.GetName() != "headless" && !_choiceLayoutCaptured)
        {
            _choiceLayoutCaptured = true;
            CheckChoiceAnchor();
            var originalFrame = Dialogue.get_dialogue_rect();
            var originalChoices = Dialogue.get_response_rect();
            var body = Read<Label>(Dialogue, "_body"); var originalText = body.Text;
            body.Text = new string('字', 400); Call(Dialogue, "layout_bottom_dialogue"); await Frames();
            var expandedFrame = Dialogue.get_dialogue_rect(); var movedChoices = Dialogue.get_response_rect();
            Check(expandedFrame.Position.Y < originalFrame.Position.Y && Mathf.Abs(movedChoices.End.Y - expandedFrame.Position.Y) < 1 &&
                Mathf.Abs(movedChoices.Position.X - originalChoices.Position.X) < 1,
                "对话栏变高时选项随上沿上移，横向位置不变");
            body.Text = originalText; Call(Dialogue, "layout_bottom_dialogue"); await Frames();
            CheckChoiceAnchor();
            if (OS.GetCmdlineUserArgs().Contains("--capture"))
            {
                await Capture("考核选项_1920");
                GetWindow().Size = new Vector2I(1280, 720); await Frames(24);
                CheckChoiceAnchor(); await Capture("考核选项_1280");
                GetWindow().Size = new Vector2I(1920, 1080); await Frames(24);
                CheckChoiceAnchor();
            }
        }
        if (OS.GetCmdlineUserArgs().Contains("--layout-only")) return;
        var button = Dialogue.FindChildren("*", "Button", true, false).OfType<Button>().Single(b => b.IsVisibleInTree() && b.Text == "开始演法");
        button.EmitSignal(BaseButton.SignalName.Pressed); await Frames();
        Check(Transition.has_open_flow_view() && Human.sect_yuxu_exam_pending_attempt_id.Length > 0, "打开战前准备并登记本次演法");
    }

    private void CheckChoiceAnchor()
    {
        var frame = Dialogue.get_dialogue_rect();
        var choices = Dialogue.get_response_rect();
        var viewport = GetViewport().GetVisibleRect().Size;
        var right = (viewport.X + Mathf.Min(1500f, viewport.X * .76f)) * .5f - 12f;
        Check(Mathf.Abs(choices.End.X - right) < 2 && Mathf.Abs(choices.End.Y - frame.Position.Y) < 1,
            "选项横向固定，并紧贴对话栏上沿");
        Check(GetViewport().GetVisibleRect().Encloses(choices) && !frame.Intersects(choices), "选项完整显示且不盖住对话正文");
    }

    private async Task Fight(bool win)
    {
        var flow = Transition.GetChildren().OfType<BattleFlowView>().Single();
        flow.FindChildren("*", "Button", true, false).OfType<Button>().Single(b => b.Text == "开始战斗").EmitSignal(BaseButton.SignalName.Pressed);
        await Until(() => GetTree().CurrentScene?.SceneFilePath == "res://tscn/battle/BattleScene.tscn" && !Transition.is_flow_transition_active(), "未进入战场");
        var manager = GetTree().CurrentScene.GetNode<BattleManager>("BattleMgr");
        Check(manager.battle_running && manager.enemy_units.Count == 2 && manager.enemy_units.OfType<Creature>().Any(x => x.entity_name.Contains("狼")) &&
            manager.enemy_units.OfType<Creature>().Any(x => x.entity_name.Contains("狈")), "真实战场有指定的一狼一狈");
        if (win) foreach (var enemy in manager.enemy_units) ((Creature)enemy).current_health = 0;
        else foreach (var unit in manager.player_units) ((Creature)unit).current_health = 0;
        Call(manager, "CheckBattleOver");
        await Until(() => Read<bool>(Transition, "_awaitingSettlement") && !Transition.is_flow_transition_active() && Transition.has_open_flow_view(), "未进入战斗结算");
        flow = Transition.GetChildren().OfType<BattleFlowView>().Single();
        flow.FindChildren("*", "Button", true, false).OfType<Button>().Single(b => b.Text is "返回" or "返回并领取").EmitSignal(BaseButton.SignalName.Pressed);
        await Until(() => GetTree().CurrentScene is MainScene { StartupComplete: true } && !Transition.is_flow_transition_active(), "未返回主场景");
        _main = (MainScene)GetTree().CurrentScene; await Frames(25);
    }

    private async Task CheckBattle()
    {
        await OpenTrial();
        if (OS.GetCmdlineUserArgs().Contains("--layout-only"))
        {
            var later = Dialogue.FindChildren("*", "Button", true, false).OfType<Button>().Single(b => b.IsVisibleInTree() && b.Text == "稍后再来");
            var point = later.GetGlobalRect().GetCenter();
            GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
            foreach (var down in new[] { true, false })
                GetViewport().PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point,
                    ButtonIndex = MouseButton.Left, ButtonMask = down ? MouseButtonMask.Left : 0, Pressed = down }, true);
            await Frames();
            Check(!Dialogue.Visible && !Transition.has_open_flow_view(), "固定位置的选项可用真实鼠标点击");
            return;
        }
        Check(Transition.try_return_from_open_flow(), "战前可取消"); await Frames();
        Check(Human.sect_yuxu_exam_pending_attempt_id == "" && Human.sect_yuxu_exam_trial_hour < 0, "取消不计胜负且允许再试");
        await OpenTrial(); await Fight(false);
        Check(Human.sect_yuxu_exam_trial_hour < 0 && Human.sect_yuxu_exam_pending_attempt_id == "" && Human.sect_current_rank == "预备真传", "失败不记胜利且可重试");
        Drain(); await OpenTrial(); await Fight(true);
        Check(Human.sect_yuxu_exam_trial_hour >= 0 && Human.sect_yuxu_exam_result == 1 && Human.sect_current_rank == "真传", "指定演法胜利后立刻转为真传");
        Check(Human.sect_teacher_npc_id == "" && Human.sect_true_disciple_lecture_next_due_hour == Human.sect_yuxu_exam_resolved_hour + Due, "转正时尚未指定师父且讲席从此时起算");
        await CheckTeacherSelection();
    }

    private async Task CheckTeacherSelection()
    {
        Drain(); Call(_main, "open_yuxu_peak_after_admission", "zifu"); await Frames(12);
        var peak = _main.GetNode<Control>("SceneLayer/YuxuXianzongOverview/PeakInterior");
        var teacher = (Button)peak.Get("_action_buttons").AsGodotDictionary()["lineage_choose_teacher"].AsGodotObject();
        Check(teacher.IsVisibleInTree() && teacher.IsInGroup("scene_action_tag"), "正式真传可从紫府峰吊牌择师");
        await Capture("紫府峰_择师吊牌");
        var npcManager = GetNode<NpcWorldManager>("/root/NpcWorldManager");
        var candidates = (List<WorldNpcData>)Call(_main, "get_yuxu_lineage_teachers")!;
        Check(candidates.Count > 0 && candidates.All(n => !n.npc_id.StartsWith("yuxu_lineage_teacher_") && n.relation_tags.Contains("师承候选")), "候选是真实登记且非固定编号的人物");
        var chosen = candidates[0];
        var beforeCount = npcManager.npcs.Count; var beforeTime = Clock.get_total_game_hours();
        var beforeBook = Human.sect_library_first_manual_path;
        chosen.relation_tags.Remove("师承候选");
        Check(!((List<WorldNpcData>)Call(_main, "get_yuxu_lineage_teachers")!).Any(n => n.npc_id == chosen.npc_id) && npcManager.npcs.Count == beforeCount,
            "没有收徒资格及意愿记录时不能仅按境界列入，也不能临时补人");
        chosen.relation_tags.Add("师承候选");
        Button Response(string prefix) => Dialogue.FindChildren("*", "Button", true, false).OfType<Button>()
            .Single(b => b.IsVisibleInTree() && b.Text.StartsWith(prefix));
        var page = Read<NpcInteractionPanel>(_main, "npc_interactions");
        Button PageAction(string id) => page.FindChildren("*", "Button", true, false).OfType<Button>()
            .Single(b => b.HasMeta("action_id") && b.GetMeta("action_id").AsString() == id);

        teacher.EmitSignal(BaseButton.SignalName.Pressed); await Frames();
        Check(Dialogue.Visible && Read<Label>(Dialogue, "_speaker").Text == "旁白" && Read<Label>(Dialogue, "_body").Text.StartsWith("择师："), "吊牌打开旁白格式的师长名录");
        await Capture("紫府峰_择师名录");
        Response("暂不择师").EmitSignal(BaseButton.SignalName.Pressed); await Frames();
        Check(!Dialogue.Visible && Human.sect_teacher_npc_id == "" && npcManager.npcs.Count == beforeCount, "暂不择师不写师承、不生成人物");

        chosen.current_subscene_id = "guanfu";
        teacher.EmitSignal(BaseButton.SignalName.Pressed); await Frames();
        Response(chosen.npc_name).EmitSignal(BaseButton.SignalName.Pressed); await Frames();
        Check(page.Visible && page.get_displayed_npc_id() == chosen.npc_id && PageAction("lineage.confirm").Disabled &&
            Read<Label>(page, "_actionHint").Text.Contains("不在紫府峰"), $"师长不在场时显示真实资料但不能拜师：提示={Read<Label>(page, "_actionHint").Text}");
        PageAction("lineage.back").EmitSignal(BaseButton.SignalName.Pressed); await Frames();
        chosen.current_subscene_id = "zifu"; chosen.current_state = "闭关修行";
        Response(chosen.npc_name).EmitSignal(BaseButton.SignalName.Pressed); await Frames();
        Check(PageAction("lineage.confirm").Disabled && Read<Label>(page, "_actionHint").Text.Contains("闭关"), "闭关的师长不能会面");
        PageAction("lineage.back").EmitSignal(BaseButton.SignalName.Pressed); await Frames();
        chosen.current_state = "师门常驻";
        Response(chosen.npc_name).EmitSignal(BaseButton.SignalName.Pressed); await Frames();
        Check(page.Visible && page.get_displayed_npc_id() == chosen.npc_id && !PageAction("lineage.confirm").Disabled &&
            Human.sect_teacher_npc_id == "", "资料页展示所选真人，确认之前不写师承");
        await Capture("紫府峰_师长资料");
        var overview = _main.GetNode<Control>("SceneLayer/YuxuXianzongOverview");
        overview.Call("_on_peak_interior_back"); await Frames();
        if (page.Visible) page.refresh_menu_actions();
        Check(Human.sect_teacher_npc_id == "" && (!page.Visible || PageAction("lineage.confirm").Disabled),
            "中途离开紫府峰不写师承，也不能在别处确认");
        page.close(); Call(_main, "open_yuxu_peak_after_admission", "zifu"); await Frames();
        teacher.EmitSignal(BaseButton.SignalName.Pressed); await Frames();
        Response(chosen.npc_name).EmitSignal(BaseButton.SignalName.Pressed); await Frames();
        if (beforeBook.Length == 0) chosen.sect_assigned_guide_npc_id = "";
        PageAction("lineage.confirm").EmitSignal(BaseButton.SignalName.Pressed); await Frames();
        Check(Human.sect_teacher_npc_id == chosen.npc_id && Human.sect_guide_npc_id == chosen.sect_assigned_guide_npc_id &&
            (Human.sect_guide_npc_id.Length == 0 || npcManager.get_npc(Human.sect_guide_npc_id)?.sect_teacher_npc_id == chosen.npc_id),
            "确认后只写入所选真人及真实师门关系，无带教记录时不虚构同门");
        Check(!teacher.IsVisibleInTree(), "择师完成后移除原场景吊牌");
        Check(Dialogue.Visible && Read<Label>(Dialogue, "_speaker").Text == Actor.entity_name &&
            Read<Label>(Dialogue, "_body").Text == "弟子见过师父。", "玩家只作简短拜师问候");
        Dialogue.close(); await Frames();
        Check(Dialogue.Visible && Read<Label>(Dialogue, "_speaker").Text == chosen.npc_name &&
            Read<Label>(Dialogue, "_body").Text == "嗯，不必多礼。" && Read<string>(Dialogue, "_portraitSpeaker") == chosen.npc_name,
            "师长对白使用真实姓名、立绘与简短回应");
        await Capture("紫府峰_拜师对白"); Drain();
        Check(Clock.get_total_game_hours() == beforeTime && Human.sect_library_first_manual_path == beforeBook && npcManager.npcs.Count == beforeCount,
            "择师不耗时、不重复选书或生成人物");
        var guide = npcManager.get_npc(Human.sect_guide_npc_id);
        if (guide is not null) guide.is_alive = false;
        Call(_main, "request_yuxu_lineage_teacher_choice", Human); await Frames();
        Check(!Dialogue.Visible && Human.sect_teacher_npc_id == chosen.npc_id && Human.sect_guide_npc_id == (guide?.npc_id ?? ""),
            "带教者缺席或不可用时保留原师承，不重新择师");
        if (guide is not null) guide.is_alive = true;
        var snapshot = _main.capture_battle_world_snapshot()!;
        const string save = "user://yuxu_exam_roundtrip.tres";
        Check(ResourceSaver.Save(snapshot, save) == Error.Ok, "转正结果可随正式快照保存");
        var loaded = ResourceLoader.Load<PersistentGameSave>(save, cacheMode: ResourceLoader.CacheMode.Ignore)!;
        Check(loaded.player_data is { sect_current_rank: "真传" } saved && saved.sect_yuxu_exam_result == Human.sect_yuxu_exam_result &&
            saved.sect_yuxu_exam_trial_hour == Human.sect_yuxu_exam_trial_hour && saved.sect_teacher_npc_id == chosen.npc_id &&
            loaded.world_npcs.Any(n => n.npc_id == chosen.npc_id), "存档保留师父真人、身份及既有考核成绩");
        DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(save));
    }
}
