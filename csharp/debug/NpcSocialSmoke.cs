using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using D = Godot.Collections.Dictionary;

/// <summary>验证实际主场景交谈、月度候选、费用、记忆、称呼及共享对话栏。</summary>
public partial class NpcSocialSmoke : Node
{
    private static void Check(bool ok, string text) { if (!ok) throw new Exception(text); }
    private static T Read<T>(object value, string name) => (T)value.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
    private static object? Call(object value, string name, params object[] args) => value.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(value, args);
    private async Task Frames(int count = 4) { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private void quit_after_cleanup(int exitCode = 0)
    {
        var tree = GetTree();
        var timer = tree.CreateTimer(.2, false);
        timer.Timeout += () =>
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            tree.Quit(exitCode);
        };
    }
    private static Button Choice(StoryDialoguePanel panel, string prefix) => panel.FindChildren("*", "Button", true, false).OfType<Button>().First(b => b.IsVisibleInTree() && b.Text.StartsWith(prefix));
    private async Task Capture(string name)
    {
        var userArgs = OS.GetCmdlineUserArgs();
        var captureOnlyBanquet = userArgs.Contains("--capture-banquet");
        if (!userArgs.Contains("--capture") && !(captureOnlyBanquet && name.StartsWith("banquet", StringComparison.Ordinal))) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng("res://tmp/npc_social_process_" + name + ".png");
    }
    private static void CheckPresentationContext()
    {
        static NpcSocialRules.Response Sample(params (string Rule, bool Favorable)[] points) =>
            new("交谈结束", "", points.Select(p => p.Rule).ToArray())
            {
                ActionName = "结识",
                DecisionSteps = points.Select(p => new NpcSocialRules.DecisionStep(p.Rule, p.Rule) { Favorable = p.Favorable }).ToArray()
            };
        static NpcSocialPresentationText.Line[] Ordered(NpcSocialRules.Response response, params string[] order) =>
            Enumerable.Range(0, 128).Select(seed => NpcSocialPresentationText.Create(response, seed).ToArray())
                .First(lines => lines.Select(line => line.Rule).SequenceEqual(order));

        var coldThenSect = Ordered(Sample(("npc_charisma", false), ("same_sect", true)), "npc_charisma", "same_sect");
        Check(coldThenSect[1].Result.Contains("才") || coldThenSect[1].Result.Contains("少了") || coldThenSect[1].Result.Contains("不再"),
            "冷淡后谈到同门应出现缓和过程，不能当作已经热络");
        var warmThenCold = Ordered(Sample(("npc_social", true), ("npc_charisma", false)), "npc_social", "npc_charisma");
        Check(warmThenCold[1].Favorable == false && (warmThenCold[1].Result.Contains("仍") || warmThenCold[1].Result.Contains("却")),
            "前句顺畅后举止疏淡，应承接前句但保留负面方向");
        var coldThenReply = Ordered(Sample(("npc_charisma", false), ("player_social", true)), "npc_charisma", "player_social");
        Check(coldThenReply[1].Result.Contains("才") || coldThenReply[1].Result.Contains("渐渐") || coldThenReply[1].Result.Contains("少了"),
            "接话得体应能衔接前句生涩，而非重复初次问候");
        var topicReturn = Ordered(Sample(("same_sect", true), ("player_mood", false), ("sect_priority", true)),
            "same_sect", "player_mood", "sect_priority");
        Check(topicReturn[2].Pending.Contains("又") && topicReturn[2].Result.Contains("宗中") && !topicReturn[2].Result.Contains("得知"),
            "话题隔行后返回应承接已知同门，不能重新发现身份");
        var fixture = Sample(("npc_mood", true), ("player_charisma", true), ("npc_social", false));
        foreach (var seed in Enumerable.Range(0, 48))
        {
            var lines = NpcSocialPresentationText.Create(fixture, seed);
            Check(lines.All(line => !line.Result.Contains("未收") && !line.Result.Contains("没了下文") && !line.Result.Contains("没有再接话")),
                "非终止判定不能提前结束交谈或假设此前已有笑意");
            Check(lines.Skip(1).Where(line => line.Rule == "player_charisma").All(line => !line.Result.Contains("见礼") && !line.Result.Contains("相迎")),
                "交谈中途不得重复开场见礼");
            Check(lines.Count == fixture.Factors.Count && lines.All(line => line.Favorable == fixture.DecisionSteps.Single(step => step.Rule == line.Rule).Favorable),
                "上下文改写不得改变判定数量与正负方向");
            Check(lines.SequenceEqual(NpcSocialPresentationText.Create(fixture, seed)), "同次展示不能在播放中重新抽取文案");
        }
    }

    private static void CheckPersonalRecall()
    {
        var human = new HumanData { entity_name = "试演主角", sect_affiliation_id = "yuxu_xianzong" };
        var witness = new WorldNpcData { npc_id = "recall_witness", faction_id = human.sect_affiliation_id };
        witness.social_personality["risk"] = "冒进";
        var first = NpcSocialRules.Evaluate(witness, human, "introduce");
        Check(NpcPersonalRecallRules.Attach(witness, first, 1).Recollection is null, "第一次接触不能回顾不存在的旧事");
        NpcSocialRules.Remember(witness, human, "introduce", "观复峰", 1, first);
        Check(witness.action_history.Last().player_contact_completed, "聊成记录未保存明确完成事实");
        var next = NpcSocialRules.Evaluate(witness, human, "introduce");
        var recalled = NpcPersonalRecallRules.Attach(witness, next, 2);
        Check(recalled.Recollection is { } recall && NpcPersonalRecallRules.IsValid(witness, recall) && recall.Text.Contains("对方") &&
            recalled.Factors.SequenceEqual(next.Factors) && recalled.DecisionSteps.SequenceEqual(next.DecisionSteps) &&
            recalled.ExchangesName == next.ExchangesName && recalled.StaminaSpent == next.StaminaSpent && witness.player_relationship == 0,
            "回顾缺少证据或被误当成新判断、费用或关系变化");
        var stranger = new WorldNpcData { npc_id = "recall_stranger" };
        Check(NpcPersonalRecallRules.Select(stranger, 2) is null && !NpcPersonalRecallRules.IsValid(stranger, recalled.Recollection!),
            "另一 NPC 不应共享见证者记忆");
        Check(NpcPersonalRecallRules.Attach(witness, next with { ExchangesName = false }, 2).Recollection is null,
            "拒绝／取消后不能借回顾强行续谈");
        var historyCount = witness.action_history.Count;
        Check(NpcPersonalRecallRules.Select(witness, 0) is null, "不可引用当前月份之后的记录");
        var ambiguous = new WorldNpcData { npc_id = "recall_legacy" };
        ambiguous.action_history.Add(new NpcActionRecord { month_index = 0, action_id = "player_social_introduce", target_npc_id = "player", outcome = "你们聊得很好。" });
        ambiguous.action_history.Add(new NpcActionRecord { month_index = 0, action_id = "player_social_spar_invitation", target_npc_id = "player" });
        ambiguous.action_history.Add(new NpcActionRecord { month_index = 0, action_id = "purchase_need", target_npc_id = "player" });
        ambiguous.action_history.Add(new NpcActionRecord { month_index = 0, action_id = "purchase_completed", target_npc_id = "another_npc" });
        Check(NpcPersonalRecallRules.Select(ambiguous, 2) is null, "旧档模糊文本、邀约、需求或他人事件不能冒充完成经历");
        foreach (var kind in new[] { "sect_spar_result", "purchase_completed" })
        {
            var evidence = new NpcActionRecord { month_index = 2, action_id = kind, target_npc_id = "player" };
            witness.record(evidence);
            var selected = NpcPersonalRecallRules.Select(witness, 2);
            Check(selected?.Evidence == evidence && (kind == "sect_spar_result" ?
                    new[] { "切磋", "交手", "对练" } : new[] { "药", "买卖" }).Any(word => selected.Text.Contains(word)),
                "回顾没有选择最新且对应类别的亲历记录");
            witness.action_history.Remove(evidence);
            Check(!NpcPersonalRecallRules.IsValid(witness, selected!), "原始证据删除后仍然展示回顾");
        }
        const string recallPath = "res://tmp/npc_social_recall_save.tres";
        Check(ResourceSaver.Save(witness, recallPath) == Error.Ok, "亲历证据资源保存失败");
        var restored = ResourceLoader.Load<WorldNpcData>(recallPath, cacheMode: ResourceLoader.CacheMode.Ignore)!;
        Check(NpcPersonalRecallRules.Select(restored, 2)?.Text == recalled.Recollection!.Text &&
              restored.action_history.Last().player_contact_completed && witness.action_history.Count == historyCount,
            "读档丢失完成事实或回顾读取改写了历史");
        DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(recallPath));
    }

    public override async void _Ready()
    {
        try
        {
            Check(NpcSocialRules.RealmAddress("云息三层", "云息四层", 30, 16) == "前辈", "同境低对高称呼错误");
            Check(NpcSocialRules.RealmAddress("云息四层", "云息三层", 30, 16) == "道友", "同境高对低称呼错误");
            Check(NpcSocialRules.RealmAddress("玄基初期", "云息三层", 70, 16) == "小友", "跨大境称呼错误");
            Check(NpcSocialRules.RealmAddress("云息三层", "", 30, 16) == "道友", "未知境界不应读取隐藏属性");
            var famous = new HumanData { entity_name = "沈照微", sect_affiliation_id = "yuxu_xianzong", sect_current_rank = "真传弟子" };
            var hearer = new WorldNpcData { faction_id = "yuxu_xianzong", current_node_id = "yuxu_xianzong_core", npc_name = "陈砚" };
            NpcSocialRules.HearSectReputation(hearer, famous, false);
            Check(!NpcSocialRules.HeardTrueDisciple(hearer), "不能无来访上下文就强加认知");
            NpcSocialRules.HearSectReputation(hearer, famous, true);
            Check(NpcSocialRules.HeardTrueDisciple(hearer) && !NpcSocialRules.KnowsName(hearer) && hearer.player_relationship == 0 && !hearer.player_social_memory.ContainsKey("player_realm"), "闻名不能等同相识、私交或已知修为");
            Check(NpcSocialRules.Opening(hearer, famous, "玉虚仙宗", true).Contains("真传"), "真传来访仍用普通初识对白");
            hearer = new WorldNpcData { faction_id = "other", current_node_id = "yuxu_xianzong_core" };
            NpcSocialRules.HearSectReputation(hearer, famous, true);
            Check(!NpcSocialRules.HeardTrueDisciple(hearer), "不应让宗外人物自动得知身份");
            var quiet = new WorldNpcData { personality = "寡言" };
            Check(!NpcPersonalityRules.WillVisit(quiet, false) && NpcPersonalityRules.WillVisit(quiet, true), "内敛者应有闻名或已有往来才主动结识");
            quiet.player_social_memory["player_name"] = "沈照微";
            Check(NpcPersonalityRules.WillVisit(quiet, false), "内敛者不应永远不接触熟人");
            var wary = new WorldNpcData { personality = "谨慎", faction_id = "other" };
            var refusal = NpcSocialRules.Evaluate(wary, famous, "introduce");
            NpcSocialRules.Remember(wary, famous, "introduce", "野外", 1, refusal);
            Check(!refusal.ExchangesName && !NpcSocialRules.KnowsName(wary), "谨慎拒绝不能仍交换姓名");
            Check(refusal.DecisionSteps.Any(s => s.Rule == "caution_veto") && refusal.Factors.Count == refusal.DecisionSteps.Count(s => s.Visible),
                "每条提示必须来自实际判定，谨慎拒绝必须有对应规则");
            wary.faction_id = famous.sect_affiliation_id;
            var sectAcceptance = NpcSocialRules.Evaluate(wary, famous, "introduce");
            Check(sectAcceptance.DecisionSteps.Any(step => step.Rule == "player_initiated") &&
                !sectAcceptance.DecisionSteps.Any(step => step.Rule == "player_accepted"), "玩家主动搭话被记成接受来访");
            var cancelledApproach = NpcSocialRules.Evaluate(wary, famous, "leave", false);
            Check(cancelledApproach.ActionName == "取消交谈" && cancelledApproach.Result.Contains("作罢") &&
                cancelledApproach.DecisionSteps.Single().Rule == "player_cancelled" &&
                !cancelledApproach.Result.Contains("来访"), "主动交谈取消被记成婉拒来访");
            var acceptedVisit = NpcSocialRules.Evaluate(wary, famous, "introduce", true);
            Check(acceptedVisit.DecisionSteps.Any(step => step.Rule == "player_accepted") &&
                !acceptedVisit.DecisionSteps.Any(step => step.Rule == "player_initiated"), "NPC 来访丢失接受语义");
            Check(Enumerable.Range(0, 12).All(seed => !NpcSocialPresentationText.Create(sectAcceptance, seed)
                .Single(line => line.Rule == "player_initiated").Result.Contains("来意说全")), "主动交谈过程仍沿用接受来意文案");
            Check(sectAcceptance.ExchangesName && sectAcceptance.DecisionSteps.Any(s => s.Rule == "same_sect") &&
                sectAcceptance.DecisionSteps.Any(s => s.Rule == "basis_overcomes_caution") && !sectAcceptance.DecisionSteps.Any(s => s.Rule == "caution_veto"),
                "同门条件必须实际改变谨慎拒绝分支，而非仅修改提示");
            wary.faction_id = "other";
            wary.player_social_memory["heard_player_rank"] = "真传弟子";
            var fameAcceptance = NpcSocialRules.Evaluate(wary, famous, "introduce");
            Check(fameAcceptance.ExchangesName && fameAcceptance.DecisionSteps.Any(s => s.Rule == "heard_rank") &&
                !fameAcceptance.DecisionSteps.Any(s => s.Rule == "same_sect"), "闻名依据与同门依据不能混用");
            wary.player_social_memory.Clear();
            wary.player_social_memory["player_name"] = famous.entity_name;
            var familiar = NpcSocialRules.Evaluate(wary, famous, "introduce");
            Check(familiar.DecisionSteps.Any(s => s.Rule == "known_name") && !familiar.DecisionSteps.Any(s => s.Rule == "heard_rank"),
                "熟人闲谈不得重复展示陌生结识判定");
            wary.player_social_memory["relationship"] = "enemy";
            var enemy = NpcSocialRules.Evaluate(wary, famous, "introduce");
            Check(!enemy.ExchangesName && enemy.DecisionSteps.Count == 1 && enemy.DecisionSteps[0].Rule == "hostile_veto",
                "敌对提前阻断后不得显示未执行的后续判断");
            wary.player_social_memory.Clear();
            wary.social_personality["risk"] = "冒进"; NpcPersonalityRules.Ensure(wary);
            Check(NpcPersonalityRules.Is(wary, "risk", "冒进"), "补缺不能覆盖已设性格");
            var stableA = new WorldNpcData { npc_id = "personality_stable" };
            var stableB = new WorldNpcData { npc_id = "personality_stable" };
            NpcPersonalityRules.Ensure(stableA); NpcPersonalityRules.Ensure(stableB);
            Check(stableA.social_personality.Count == 6 && stableA.social_priorities.Count == 1 &&
                stableA.social_personality.Keys.All(k => stableA.social_personality[k].AsString() == stableB.social_personality[k].AsString()),
                "六轴生成应按稳定 ID 一致，不能每次查阅重抽");
            Check(!NpcPersonalityRules.InspectionText(stableA, false).Contains("所重：") && stableA.player_known_npc_info.Count == 0,
                "正式认知不能读取真实所重，测试查阅不得写入知识");
            Check(NpcPersonalityRules.InspectionText(stableA, true).Contains("真实性情") && stableA.player_known_npc_info.Count == 0,
                "测试查看不能自动熟悉人物");
            var motiveNpc = new WorldNpcData { npc_id = "motive", faction_id = "yuxu_xianzong", personality = "爽直" };
            motiveNpc.social_priorities.Add("声名");
            motiveNpc.player_social_memory["heard_player_rank"] = "真传弟子";
            var proactiveMotive = NpcSocialRules.Evaluate(motiveNpc, famous, "introduce", true);
            Check(NpcPersonalityRules.HasVisitPriority(motiveNpc, true) && proactiveMotive.DecisionSteps.Any(s => s.Rule == "renown_visit") &&
                !NpcSocialRules.Evaluate(motiveNpc, famous, "introduce").DecisionSteps.Any(s => s.Rule == "renown_visit"),
                "主动来访的所重因素不能伪用于玩家主动搭话");
            var prototypeNpc = new WorldNpcData { npc_id = "prototype_direction", npc_name = "试算修士", faction_id = "yuxu_xianzong", mood_value = 18 };
            prototypeNpc.social_personality["sociability"] = "外向";
            prototypeNpc.social_personality["risk"] = "谨慎";
            prototypeNpc.social_priorities.Add("宗门");
            var prototypePlayer = new HumanData { entity_name = "沈照微", charisma = 72, social = 68, mood_value = 12, sect_affiliation_id = "yuxu_xianzong", sect_current_rank = "真传弟子" };
            var temporaryStats = NpcSocialPrototypeRules.GetTemporaryStats(prototypeNpc);
            var playerInitiates = NpcSocialPrototypeRules.Evaluate(prototypeNpc, prototypePlayer, npcInitiated: false, accepted: true);
            var npcInitiates = NpcSocialPrototypeRules.Evaluate(prototypeNpc, prototypePlayer, npcInitiated: true, accepted: true);
            var declinedPrototype = NpcSocialPrototypeRules.Evaluate(prototypeNpc, prototypePlayer, npcInitiated: false, accepted: false);
            var wordsA = NpcSocialPresentationText.Create(playerInitiates, 1);
            var wordsB = NpcSocialPresentationText.Create(playerInitiates, 2);
            Check(wordsA.Count == playerInitiates.Factors.Count && wordsA.Zip(wordsB).Any(pair => pair.First != pair.Second) &&
                wordsA.All(line => line.Pending.Length > 0 && line.Result.Length > 0 &&
                    !line.Result.Contains("魅力") && !line.Result.Contains("交际") && !line.Result.Contains("加成")),
                "交互文案必须对应实际分支、能更换措辞且不公开计算条件");
            Check(Enumerable.Range(0, 24).Select(seed => string.Join("|", NpcSocialPresentationText.Create(playerInitiates, seed).Select(line => line.Result))).Distinct().Count() > 6,
                "文案只在六份整篇模板之间循环，没有按分支分别组合");
            Check(Enumerable.Range(0, 12).Select(seed => string.Join("|", NpcSocialPresentationText.Create(playerInitiates, seed).Select(line => line.Rule))).Distinct().Count() > 1 &&
                wordsA.Select(line => line.Rule).OrderBy(rule => rule).SequenceEqual(playerInitiates.DecisionSteps.Where(step => step.Visible).Select(step => step.Rule).OrderBy(rule => rule)),
                "独立判定展示顺序没有变化，或重排丢失/添加了判定点");
            Check(NpcSocialPresentationText.Create(refusal, 3).Last().Rule == "caution_veto", "拒绝结识的收束步骤被打乱");
            CheckPresentationContext();
            CheckPersonalRecall();
            NpcPlayerImpressionChecks.Run();
            Check(temporaryStats == NpcSocialPrototypeRules.GetTemporaryStats(prototypeNpc) && temporaryStats.Charisma is >= 35 and <= 85 && temporaryStats.Social is >= 35 and <= 85,
                "NPC 测试属性必须按人物 ID 稳定派生且保持在临时范围内");
            Check(playerInitiates.DecisionSteps.Any(s => s.Rule == "player_charisma") && playerInitiates.DecisionSteps.Any(s => s.Rule == "player_social") &&
                npcInitiates.DecisionSteps.Any(s => s.Rule == "visit_initiative") && !playerInitiates.DecisionSteps.Any(s => s.Rule == "visit_initiative") &&
                playerInitiates.DecisionSteps.Any(s => s.Rule == "outgoing") &&
                !playerInitiates.DecisionSteps.Select(s => s.Rule).SequenceEqual(npcInitiates.DecisionSteps.Select(s => s.Rule)),
                "玩家主动与 NPC 来访必须走可区分的因素流程");
            Check(playerInitiates.Factors.Count >= 7 && npcInitiates.Factors.Count >= 7,
                "测试人物输入应覆盖多项真实生效因素，而非只有两行提示");
            Check(playerInitiates.Factors.Count == playerInitiates.DecisionSteps.Count(s => s.Visible) && npcInitiates.Factors.Count == npcInitiates.DecisionSteps.Count(s => s.Visible) &&
                declinedPrototype.StaminaSpent == 0 && prototypeNpc.player_social_memory.Count == 0 && prototypeNpc.social_personality.Count == 2 && prototypeNpc.social_priorities.Count == 1,
                "原型提示必须对应判定，且试算不能写入 NPC 记忆或人物属性");
            NpcSocialRules.Remember(motiveNpc, famous, "introduce", "宗内", 1, proactiveMotive);
            var knownText = NpcPersonalityRules.InspectionText(motiveNpc, false);
            Check(knownText.Contains("姓名：") && knownText.Contains("曾主动") && !knownText.Contains("所重：") &&
                !knownText.Contains("仁心："), "交际所得只应包含实际信息和行为印象");
            var sparCautious = new WorldNpcData { npc_id = "spar_cautious", faction_id = "yuxu_xianzong", cultivation_realm = "云息二层", personality = "谨慎" };
            var sparPlayer = new HumanData { sect_affiliation_id = "yuxu_xianzong", cultivation_realm = "云息三层" };
            Check(!NpcSparringRules.Evaluate(sparCautious, sparPlayer).AgreesToSpar, "谨慎者不能无条件接受陌生切磋");
            sparCautious.player_social_memory["player_name"] = "沈照微";
            Check(NpcSparringRules.Evaluate(sparCautious, sparPlayer).AgreesToSpar, "不能读取未获知的真实修为来拒绝熟人");
            sparCautious.player_social_memory["player_realm"] = "云息三层";
            Check(!NpcSparringRules.Evaluate(sparCautious, sparPlayer).AgreesToSpar, "已知修为差距未影响谨慎对练判断");
            sparCautious.social_personality["risk"] = "冒进";
            Check(NpcSparringRules.Evaluate(sparCautious, sparPlayer).AgreesToSpar, "改变风险性情应实际改变对练分支");
            sparCautious.player_social_memory["relationship"] = "enemy";
            Check(!NpcSparringRules.Evaluate(sparCautious, sparPlayer).AgreesToSpar, "不能把敌对冲突当作友好对练");
            var saves = GetNode<GameSaveManager>("/root/GameSaveManager"); Check(!saves.has_active_slot, "不能写正式存档槽");
            var fixture = new PersistentGameSave { year = 1, month = 1, day = 8, hour = 9, current_node_id = "yuxu_xianzong_core", world_cell = new(34, 166), npc_population_version = 1, npc_simulation_enabled = false,
                player_data = new HumanData { entity_name = "沈照微", cultivation_realm = "云息三层", current_age = 16, sect_affiliation_id = "yuxu_xianzong", sect_current_rank = "外门弟子", current_world_stamina = 100 },
                scene_progress = new D { ["story_free_start"] = true, ["cloud_state_initialized"] = true } };
            fixture.world_npcs.Add(new WorldNpcData { npc_id = "social_smoke", npc_name = "陈砚", faction_id = "yuxu_xianzong", formal_identity = "外门弟子", current_node_id = "yuxu_xianzong_core", current_subscene_id = "guanfu", cultivation_realm = "云息二层", current_age = 20, gender = 0 });
            typeof(GameSaveManager).GetField("_pendingLoad", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(saves, fixture);
            GetTree().CurrentScene = null;
            Check(GetTree().ChangeSceneToFile("res://tscn/scene/MainScene.tscn") == Error.Ok, "主场景加载失败");
            await Frames(10);
            var main = (MainScene)GetTree().CurrentScene;
            var world = GetNode<NpcWorldManager>("/root/NpcWorldManager"); var npc = world.get_npc("social_smoke")!;
            var player = Read<Character>(main, "player"); var dialogue = Read<StoryDialoguePanel>(main, "story_dialogue");
            var clock = GetNode<GameTimeMgr>("/root/GameTimeManager");
            var relationshipBeforeGreeting = CharacterRelationshipRules.Get((HumanData)player.entity_data!, npc.npc_id);
            var greetingNow = clock.get_total_game_hours();
            Check(main.deliver_npc_letter(npc.npc_id, "greeting-window-smoke", "愿日后结识", "若道友有暇，日后宗内相逢可再叙。", sentAtHour: greetingNow, opensAcquaintanceWindow: true),
                "符合结识意图的问候信没有投递");
            var openedUntil = greetingNow + 5 * 360 * 24;
            Check(npc.acquaintance_window_until_hour == openedUntil && relationshipBeforeGreeting is null &&
                  CharacterRelationshipRules.Get((HumanData)player.entity_data!, npc.npc_id) is null,
                "投递问候信应开启五年窗口但不能直接增加关系");
            npc.acquaintance_window_until_hour = greetingNow + 7;
            Check(!main.deliver_npc_letter(npc.npc_id, "greeting-window-smoke", "愿日后结识", "重复来源不得刷新窗口。", sentAtHour: greetingNow, opensAcquaintanceWindow: true) &&
                  npc.acquaintance_window_until_hour == greetingNow + 7,
                "同一封问候信重复回调刷新了结识窗口");
            npc.acquaintance_window_until_hour = 0;
            var trueDisciple = OS.GetCmdlineUserArgs().Contains("--true-disciple");
            if (trueDisciple) ((HumanData)player.entity_data!).sect_current_rank = "真传弟子";
            var openingChoice = "接受";
            var overview = main.GetNode<Control>("SceneLayer/YuxuXianzongOverview");
            Read<Control>(main, "tile_world_map").Hide();
            overview.Call("open_overview"); overview.Call("_on_peak_pressed", "guanfu");
            Call(main, "sync_social_membership_month");
            Call(main, "process_npc_social_visit", .6);
            Check(!dialogue.Visible, "入宗当月不应立即主动结识");
            clock.restore_calendar(1, 2, 8, 9, 0);
            Call(main, "process_npc_social_visit", .6);
            Check(dialogue.Visible && Choice(dialogue, openingChoice) is not null, "次月在场同门未主动搭话");
            Check(!Read<PanelContainer>(dialogue, "_dialogueFrame").Visible && dialogue.GetNodeOrNull("SocialInteraction") is not null, "交往不应显示底部剧情栏");
            Check(!NpcSocialRules.KnowsName(npc) && player.current_world_stamina == 100, "打开选择不应交换姓名或扣精力");
            await Frames(20); await Capture("choices");
            Choice(dialogue, "婉拒").EmitSignal(BaseButton.SignalName.Pressed);
            dialogue.close(); dialogue.close();
            Check(!NpcSocialRules.KnowsName(npc) && player.current_world_stamina == 100, "拒绝来访不应收费或自动认识");
            main.open_world_npc_talk(npc);
            openingChoice = "上前搭话";
            Check(!dialogue.FindChildren("*", "Button", true, false).OfType<Button>().Any(button => button.IsVisibleInTree() &&
                (button.Text.StartsWith("接受") || button.Text == "婉拒")), "主动交谈按钮仍显示接受或婉拒");
            var hours = clock.get_total_game_hours();
            Choice(dialogue, openingChoice).EmitSignal(BaseButton.SignalName.Pressed);
            Check(NpcSocialRules.KnowsName(npc), "自报姓名未记忆");
            Check(player.current_world_stamina == 100 - WorldStaminaRules.ConversationCost && npc.player_relationship == 0, "费用或无依据关系加分错误");
            Check(dialogue.FindChildren("SocialRecollection", "Label", true, false).Count == 0,
                "首次正式交谈错误地把本次登记说成上回");
            Check(clock.get_total_game_hours() == hours && NpcSocialRules.Address(npc, 16) == "道友" && !npc.player_social_memory.ContainsKey("player_realm"), "交换姓名不应泄露境界或推进时间");
            Call(dialogue, "process_social_reveal", .7); await Frames(); await Capture("progress");
            dialogue.close(); Check(dialogue.Visible, "展开判定应保留对话");
            await Frames(); await Capture("result"); dialogue.close();
            Call(main, "process_npc_social_visit", .6); Check(!dialogue.Visible, "同月反复弹出");
            var saved = main.capture_battle_world_snapshot()!;
            Check(saved.scene_progress["social_visit_month"].AsInt32() == 1 &&
                  saved.scene_progress["social_visit_processed_month"].AsInt32() == 1 &&
                  saved.scene_progress["social_visited_npc_ids"].AsGodotArray<string>().Contains(npc.npc_id) &&
                  saved.world_npcs.First(n => n.npc_id == npc.npc_id).player_social_memory["player_name"].AsString() == "沈照微",
                "快照丢失交谈记忆、来访月度去重游标或已处理 NPC ID");
            var path = "res://tmp/npc_social_smoke_save.tres";
            Check(ResourceSaver.Save(saved, path) == Error.Ok, "资源存档失败");
            var reloaded = ResourceLoader.Load<PersistentGameSave>(path, cacheMode: ResourceLoader.CacheMode.Ignore)!;
            Check(reloaded.world_npcs.First(n => n.npc_id == npc.npc_id).player_social_memory["player_name"].AsString() == "沈照微", "实际资源读档丢失姓名");
            var restoredNpc = reloaded.world_npcs.First(n => n.npc_id == npc.npc_id);
            Check(restoredNpc.social_personality.Count == 6 && restoredNpc.social_priorities.Count > 0 &&
                restoredNpc.player_known_npc_info.ContainsKey("name"), "实际读档丢失六轴、所重或交际所得");
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
            main.open_world_npc_talk(npc); Check(Read<Label>(dialogue, "_body").Text.Contains("认出你来"), "熟人仍重复初识");
            openingChoice = "聊聊近况";
            var repeatedStamina = player.current_world_stamina;
            Choice(dialogue, openingChoice).EmitSignal(BaseButton.SignalName.Pressed); await Frames(3);
            Check(dialogue.FindChildren("SocialRecollection", "Label", true, false).Count == 1 &&
                player.current_world_stamina == repeatedStamina - WorldStaminaRules.ConversationCost && npc.player_relationship == 0,
                "正式二次交谈没有接入亲历回顾，或为回顾重复扣费／加关系");
            dialogue.close(); dialogue.close(); await Frames(3);
            main.open_world_npc_talk(npc);
            Choice(dialogue, "作罢").EmitSignal(BaseButton.SignalName.Pressed); dialogue.close(); dialogue.close();
            var menu = Read<NpcInteractionPanel>(main, "npc_interactions");
            var originalMemoryCount = npc.player_social_memory.Count;
            var originalPersonalityCount = npc.social_personality.Count;
            var originalPriorityCount = npc.social_priorities.Count;
            var originalAppearanceSeed = npc.appearance_seed;
            var originalRelationship = npc.player_relationship;
            var originalStamina = player.current_world_stamina;
            var originalHours = clock.get_total_game_hours();
            main.open_world_npc_actions(npc); await Frames();
            var activeTest = menu.FindChildren("*", "Button", true, false).OfType<Button>().FirstOrDefault(b => b.Text == "测试：我主动搭话");
            var visitTest = menu.FindChildren("*", "Button", true, false).OfType<Button>().FirstOrDefault(b => b.Text == "测试：NPC来找我");
            Check(activeTest is null && visitTest is null &&
                menu.FindChildren("*", "Button", true, false).OfType<Button>().Any(button => button.Text == "交谈"),
                "正式 NPC 菜单仍有测试入口，或正式交谈被移除");
            menu.close();
            Call(main, "begin_npc_social_talk", npc, false, true); await Frames();
            var socialStage = dialogue.FindChildren("*", "Control", true, false).OfType<Control>()
                .LastOrDefault(control => control.Name.ToString().StartsWith("SocialInteraction", StringComparison.Ordinal) && control.IsVisibleInTree())
                ?? throw new Exception("当前人物交互浮层缺失");
            var playerPortraitCount = socialStage.FindChildren("PlayerSocialPortrait", "Control", true, false).Count;
            var npcPortraitCount = socialStage.FindChildren("NpcSocialPortrait", "Control", true, false).Count;
            var center = socialStage.FindChildren("InteractionProcess", "VBoxContainer", true, false).OfType<Control>().FirstOrDefault()
                ?? throw new Exception("交互中央过程栏缺失");
            var centerLabels = center.FindChildren("*", "Label", true, false).OfType<Label>().ToArray();
            var npcPortrait = socialStage.FindChildren("NpcSocialPortrait", "Control", true, false).OfType<Control>().FirstOrDefault()
                ?? throw new Exception("当前人物交互 NPC 立绘缺失");
            var npcNameLabel = socialStage.FindChildren("NpcSocialName", "Label", true, false).OfType<Label>().FirstOrDefault();
            var choiceButtons = center.FindChildren("*", "Button", true, false).OfType<Button>().Where(b => b.IsVisibleInTree()).OrderBy(b => b.GlobalPosition.Y).ToArray();
            Check(playerPortraitCount == 1 && npcPortraitCount == 1 && !centerLabels.Any(l => l.Text.Contains("宗内遇见") || l.Text.Contains(npc.npc_name)) &&
                choiceButtons.Length == 2 && choiceButtons[1].GlobalPosition.Y > choiceButtons[0].GlobalPosition.Y,
                $"交互浮层结构异常：立绘={playerPortraitCount}/{npcPortraitCount}，中央文字={string.Join("|", centerLabels.Select(l => l.Text))}，选项={string.Join("|", choiceButtons.Select(b => $"{b.Text}@{b.GlobalPosition.Y}"))}");
            if (NpcPersonalityRules.TestInspection)
            {
                var hoverManager = GetNodeOrNull<HoverInfoManager>("/root/HoverInfoManager");
                var hoverEntries = hoverManager is null ? null : Read<System.Collections.Generic.Dictionary<Control, HoverInfoManager.HoverInfoContent>>(hoverManager, "_entries");
                Check(npcNameLabel is not null && hoverEntries is not null && hoverEntries.TryGetValue(npcNameLabel, out var tip) && tip.Body.Contains("仅测试试算"),
                    $"测试入口悬停未显示临时数值，或测试数据缺少不存档说明：testMode={Read<bool>(dialogue, "_socialTestMode")}，tooltip={npcNameLabel?.TooltipText ?? "<空>"}");
            }
            Choice(dialogue, "上前搭话").EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            dialogue.close(); dialogue.close();
            Call(main, "begin_npc_social_talk", npc, true, true); await Frames();
            Choice(dialogue, "听听来意").EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            Check(player.current_world_stamina == originalStamina && clock.get_total_game_hours() == originalHours &&
                npc.player_social_memory.Count == originalMemoryCount && npc.social_personality.Count == originalPersonalityCount &&
                npc.social_priorities.Count == originalPriorityCount && npc.appearance_seed == originalAppearanceSeed && npc.player_relationship == originalRelationship,
                "两种测试互动不得扣精力、推进时间或改动 NPC 正式数据");
            dialogue.close(); dialogue.close();
            main.open_world_npc_talk(npc);
            var stamina = player.current_world_stamina; dialogue.close(); dialogue.close(); dialogue.close();
            Check(!dialogue.Visible && player.current_world_stamina == stamina, "Esc告辞应免费且能正常关闭");
            dialogue.show_narration("剧情对白回归检查");
            Check(Read<PanelContainer>(dialogue, "_dialogueFrame").Visible && Read<Control?>(dialogue, "_socialContent") is null, "普通剧情对白未恢复底栏"); dialogue.close();
            npc.current_subscene_id = "zifu"; main.open_world_npc_talk(npc);
            Check(dialogue.Visible && Read<Label>(dialogue, "_body").Text.Contains("不在当前峰内区域"), "无法接触的 NPC 应说明原因，不能静默无反馈或允许远程交谈");
            dialogue.close();
            npc.current_subscene_id = "guanfu"; player.current_world_stamina = 0;
            main.open_world_npc_talk(npc); Choice(dialogue, "聊聊近况").EmitSignal(BaseButton.SignalName.Pressed);
            Check(player.current_world_stamina == 0, "精力不足不应扣成负数"); dialogue.close();
            npc.player_social_memory["relationship"] = "enemy";
            CharacterRelationshipRules.PreserveLegacyMarker(npc, CharacterRelationshipRules.PlayerCharacterId, "enemy");
            Check(NpcSocialRules.Address(npc, 16) == "沈照微", "仇人应使用已知姓名");
            var preview = GD.Load<PackedScene>("res://tscn/scene/NpcSocialPreviewScene.tscn").Instantiate<NpcSocialPreviewScene>();
            var previewLayer = new CanvasLayer { Layer = 200 };
            AddChild(previewLayer); previewLayer.AddChild(preview); await Frames(5);
            Call(preview, "BeginInteraction", false); await Frames(3);
            var previewDialogue = preview.GetNode<StoryDialoguePanel>("StoryDialoguePanel");
            Check(previewDialogue.Visible && Choice(previewDialogue, "上前搭话") is not null, "首页人物交互试演未打开玩家主动交谈");
            previewDialogue.SetProcess(false);
            Choice(previewDialogue, "上前搭话").EmitSignal(BaseButton.SignalName.Pressed); await Frames(3);
            Check(Read<Control?>(previewDialogue, "_socialContent")?.Visible == true, "首页玩家主动试演没有进入过程浮层");
            Check(!Read<Control>(preview, "_setupRoot").Visible, "交互浮层下仍残留测试入口面板");
            var animatedLines = Read<System.Collections.Generic.List<Label>>(previewDialogue, "_socialFactors");
            var lineWords = Read<System.Collections.Generic.IReadOnlyList<NpcSocialPresentationText.Line>>(previewDialogue, "_socialLines");
            Check(animatedLines[0].VisibleCharacters == 0 && animatedLines.Skip(1).All(line => line.Modulate.A == 0), "尚未轮到的过程行提前显示");
            var firstTypingSlice = lineWords[0].Pending.Length * .025;
            Call(previewDialogue, "process_social_reveal", firstTypingSlice);
            Check(animatedLines[0].VisibleCharacters > 0 && animatedLines[0].VisibleCharacters < lineWords[0].Pending.Length, "等待语未从左向右逐字写出");
            await Frames(3); await Capture("preview_typing");
            var typingTime = lineWords[0].Pending.Length * .05;
            Call(previewDialogue, "process_social_reveal", typingTime + .22 - firstTypingSlice);
            Check(animatedLines[0].Text == lineWords[0].Pending + "……" && Read<int>(previewDialogue, "_socialRevealIndex") == 0, "试探阶段提前进入结果或下一行");
            await Frames(3); await Capture("preview_hesitating");
            Call(previewDialogue, "process_social_reveal", .88 + (lineWords[0].Pending.Length + 2) * .016 + .21 - .22);
            Check(animatedLines[0].Text == lineWords[0].Result && animatedLines.Skip(1).All(line => line.Modulate.A == 0), "本行等待语未撤回并替换，或后续行提前出现");
            Call(previewDialogue, "process_social_reveal", .30);
            Check(Read<int>(previewDialogue, "_socialRevealIndex") == 1 && animatedLines[0].VisibleCharacters == -1, "结果完成后未保留旧行并进入下一行");
            previewDialogue.close(); await Frames(3);
            Check(animatedLines.Select((line, index) => line.Text == lineWords[index].Result && line.VisibleCharacters == -1).All(ok => ok), "略过后仍残留等待语或半行文字");
            var done = Read<Button>(previewDialogue, "_socialDone");
            Check(done.Size.X <= 300 && Read<Control>(previewDialogue, "_socialContent").FindChildren("*", "ProgressBar", true, false).Count == 0,
                "交互确认按钮过宽或仍有横贯进度条");
            await Capture("preview_result");
            previewDialogue.SetProcess(true);
            previewDialogue.close(); await Frames(3);
            Check(Read<VBoxContainer>(preview, "_menu").Visible, "关闭试演后没有返回测试入口");
            Call(preview, "BeginInteraction", true); await Frames(3);
            Check(Choice(previewDialogue, "听听来意") is not null, "首页人物交互试演未打开 NPC 来访");
            Choice(previewDialogue, "听听来意").EmitSignal(BaseButton.SignalName.Pressed); await Frames(3);
            Check(Read<Control?>(previewDialogue, "_socialContent")?.Visible == true, "首页 NPC 来访试演没有进入过程浮层");
            previewDialogue.close(); previewDialogue.close(); await Frames(3);
            await Capture("preview_menu");
            var template = Read<WorldNpcData>(preview, "_npcTemplate");
            var previewPlayer = Read<Character>(preview, "_player");
            var previewStamina = previewPlayer.current_world_stamina;
            var originalScene = GetTree().CurrentScene;
            var formalStamina = player.current_world_stamina;
            foreach (var known in new[] { true, false })
            {
                Call(preview, "BeginSparring", known); await Frames(3);
                var active = Read<WorldNpcData?>(preview, "_activeNpc")!;
                Check(!ReferenceEquals(active, template) && NpcSocialRules.KnowsName(active) == known,
                    "切磋试演没有隔离临时人物，或沿用了上一场姓名认知");
                Check(previewDialogue.FindChildren("SocialActionIcon", "TextureRect", true, false).OfType<TextureRect>().Single().Texture.ResourcePath.Contains("spar_v1"),
                    "切磋试演没有使用对应图标");
                Choice(previewDialogue, "提出切磋").EmitSignal(BaseButton.SignalName.Pressed); await Frames(3);
                var sparLines = Read<System.Collections.Generic.IReadOnlyList<NpcSocialPresentationText.Line>>(previewDialogue, "_socialLines");
                Check(known ? sparLines.Any(line => line.Rule == "spar_terms") : sparLines.Count == 1 && sparLines[0].Rule == "spar_unknown",
                    "切磋试演没有按真实相识／谨慎规则分别展示接受与推辞");
                previewDialogue.close(); await Frames(3);
                if (known) await Capture("preview_spar");
                previewDialogue.close(); await Frames(3);
            }
            Call(preview, "BeginSparring", true); await Frames(3);
            Choice(previewDialogue, "暂且作罢").EmitSignal(BaseButton.SignalName.Pressed); await Frames(3);
            Check(Read<Label?>(previewDialogue, "_socialOutcome")!.Text.Contains("收起"), "切磋取消试演未生效");
            previewDialogue.close(); await Frames(3);
            Call(preview, "BeginPurchase"); await Frames(3);
            Check(previewDialogue.FindChildren("SocialActionIcon", "TextureRect", true, false).OfType<TextureRect>().Single().Texture.ResourcePath.Contains("trade_v1"),
                "交易试演没有使用对应图标");
            Choice(previewDialogue, "同意出售").EmitSignal(BaseButton.SignalName.Pressed); await Frames(3);
            var tradeLines = Read<System.Collections.Generic.IReadOnlyList<NpcSocialPresentationText.Line>>(previewDialogue, "_socialLines");
            Check(tradeLines.Select(line => line.Rule).SequenceEqual(new[] { "purchase_supplied", "purchase_paid" }), "交易试演颠倒交货付款或补造额外判断");
            previewDialogue.close(); await Frames(3); await Capture("preview_trade");
            previewDialogue.close(); await Frames(3);
            Call(preview, "BeginPurchase"); await Frames(3);
            Choice(previewDialogue, "婉拒求购").EmitSignal(BaseButton.SignalName.Pressed); await Frames(3);
            Check(Read<Label?>(previewDialogue, "_socialOutcome")!.Text.Contains("作罢"), "求购婉拒试演未生效");
            previewDialogue.close(); await Frames(3);
            Call(preview, "BeginPurchase"); await Frames(3);
            previewDialogue.close(); await Frames(3); // Esc 选择阶段等价关闭，应落到婉拒结果。
            Check(Read<Label?>(previewDialogue, "_socialOutcome")!.Text.Contains("作罢"), "交易试演 Esc 未按婉拒处理");
            previewDialogue.close(); await Frames(3);
            foreach (var choice in new[] { "曾经聊过天", "曾经交过手", "曾经做过药材买卖" })
            {
                Call(preview, "BeginRecollection"); await Frames(3);
                Choice(previewDialogue, choice).EmitSignal(BaseButton.SignalName.Pressed); await Frames(3);
                var recollections = previewDialogue.FindChildren("SocialRecollection", "Label", true, false).OfType<Label>().ToArray();
                Check(recollections.Length == 1 && recollections[0].Text.Contains("对方") && recollections[0].Text.Contains("『") &&
                    Read<WorldNpcData?>(preview, "_activeNpc")!.action_history.Count == 1,
                    "亲历试演缺少可见回顾、把旧事当作内心或累积了其他试演记录");
                var recallLines = Read<System.Collections.Generic.IReadOnlyList<NpcSocialPresentationText.Line>>(previewDialogue, "_socialLines");
                Check(recallLines.All(line => !line.Rule.Contains("recall")) && Read<System.Collections.Generic.List<Label>>(previewDialogue, "_socialFactors").Count == recallLines.Count,
                    "旧事回顾被当作额外判定点");
                previewDialogue.close(); await Frames(3);
                if (choice == "曾经聊过天") await Capture("preview_recall");
                previewDialogue.close(); await Frames(3);
            }
            foreach (var kind in new[] { "contact", "spar", "trade" })
            foreach (var settled in new[] { false, true })
            {
                Call(preview, "BeginImpressionComparison", kind); await Frames(3);
                Choice(previewDialogue, settled ? "过月后" : "过月前").EmitSignal(BaseButton.SignalName.Pressed); await Frames(3);
                var active = Read<WorldNpcData?>(preview, "_activeNpc")!;
                var impression = NpcPlayerImpressionRules.Feedback(active);
                Check(settled ? impression?.ImpressionKind == kind : impression is null, "过月前后印象试演未正确区分归纳阶段");
                var quote = previewDialogue.FindChildren("SocialRecollection", "Label", true, false).OfType<Label>().Single();
                Check(settled ? quote.Text == impression!.Text : quote.Text.Contains("上回") || quote.Text.Contains("前次") || quote.Text.Contains("前回"),
                    "过月印象反馈未显示，或过月前把即时回顾伪装成印象归纳");
                previewDialogue.close(); await Frames(3);
                if (kind == "contact") await Capture(settled ? "impression_after" : "impression_before");
                previewDialogue.close(); await Frames(3);
            }
            foreach (var hasSect in new[] { true, false })
            {
                Call(preview, "BeginAdmission"); await Frames(3);
                var admissionActor = Read<Character?>(preview, "_admissionPlayer")!;
                var admissionHuman = (HumanData)admissionActor.entity_data!;
                Check(string.IsNullOrEmpty(admissionHuman.sect_affiliation_id), "迎真试演混用了已入宗角色");
                var skip = Read<Button>(previewDialogue, "_socialDone");
                skip.EmitSignal(BaseButton.SignalName.Pressed); await Frames(3);
                if (hasSect) await Capture("admission_review");
                skip.EmitSignal(BaseButton.SignalName.Pressed); await Frames(3);
                if (hasSect) await Capture("admission_question");
                Choice(previewDialogue, hasSect ? "有" : "无").EmitSignal(BaseButton.SignalName.Pressed); await Frames(3);
                var admissionDone = Read<Button>(previewDialogue, "_socialDone");
                admissionDone.EmitSignal(BaseButton.SignalName.Pressed); await Frames(3);
                if (!hasSect) await Capture("admission_result");
                admissionDone.EmitSignal(BaseButton.SignalName.Pressed); await Frames(3);
                Check(!previewDialogue.Visible && Read<Control>(preview, "_setupRoot").Visible &&
                    string.IsNullOrEmpty(admissionHuman.sect_affiliation_id), "迎真试演实际办了入门或未返回菜单");
            }
            Check(GetTree().CurrentScene == originalScene && player.current_world_stamina == formalStamina &&
                previewPlayer.current_world_stamina == previewStamina && template.player_social_memory.Count == 0 &&
                template.carried_items.Count == 0 && template.resources.Count == 0 && template.action_history.Count == 0 && template.social_priorities.Count == 1 &&
                template.social_priorities.Contains("宗门") && Read<Control>(preview, "_setupRoot").Visible,
                "三类试演影响了正式场景、精力、模板资源／认知，或结束后未返回菜单");
            previewLayer.QueueFree(); await Frames(3);
            var entryAffiliation = ((HumanData)player.entity_data!).sect_affiliation_id;
            Call(main, "begin_yuxu_gate_welcome"); await Frames(3);
            Check(dialogue.Visible && dialogue.GetNodeOrNull("YuxuAdmissionInteraction") is not null &&
                Read<Character?>(dialogue, "_socialPlayer") == player,
                "正式山门入口仍使用旧面板，或未传入当前玩家立绘");
            Read<Button>(dialogue, "_socialDone").EmitSignal(BaseButton.SignalName.Pressed); await Frames(3);
            await Capture("admission_world_review");
            dialogue.close(); await Frames(3);
            Check(!dialogue.Visible && !Read<bool>(main, "_yuxuAdmissionInProgress") &&
                ((HumanData)player.entity_data!).sect_affiliation_id == entryAffiliation,
                "正式迎真取消未退出或意外改写所属宗门");
            var home = GD.Load<PackedScene>("res://tscn/scene/StartMenu.tscn").Instantiate<StartMenu>();
            AddChild(home); await Frames(5);
            Call(home, "ToggleDemoEntries"); await Frames(2);
            var homeEntry = home.FindChildren("*", "Button", true, false).OfType<Button>().FirstOrDefault(b => b.Text == "人物交互测试");
            var sharedHoverEntries = Read<System.Collections.Generic.Dictionary<Control, HoverInfoManager.HoverInfoContent>>(
                GetNode<HoverInfoManager>("/root/HoverInfoManager"), "_entries");
            Check(homeEntry is not null && sharedHoverEntries.TryGetValue(homeEntry, out var homeTip) && homeTip.Body.Contains("无需进入存档"),
                "首页‘试演与预览’列表没有人物交互测试入口或对应悬停详情");
            home.QueueFree(); await Frames(3);
            var banquet = new BanquetPanel { Name = "BanquetPanelSmoke" };
            AddChild(banquet); await Frames(3);
            var banquetBackground = banquet.GetChildren().OfType<TextureRect>().FirstOrDefault();
            Check(banquetBackground?.Texture?.ResourcePath == "res://textures/ui/banquet/banquet_outside_window_v2.png",
                "宴席没有使用窗外隔窗望入室内的虚化素材");
            banquet.ShowSetup(
                new BanquetRosterEntry { Id = "player", Name = "试验主持" },
                Enumerable.Range(1, 5).Select(index => new BanquetRosterEntry { Id = $"guest-{index}", Name = $"宾客{index}" }).ToArray(),
                [
                    new BanquetVenueOption { Id = "gathering_hall", Name = "同门雅集堂", CostText = "5 点贡献", CapacityIncludingHost = 12 },
                    new BanquetVenueOption { Id = "waterside_pavilion", Name = "临水水榭", CostText = "2 枚灵石", CapacityIncludingHost = 6 },
                ],
                [new BanquetTimeOption { Label = "当日", StartsAtHour = 120 }]);
            var banquetChecks = Read<System.Collections.Generic.Dictionary<string, CheckBox>>(banquet, "_guestChecks");
            var selectedBanquetGuests = Read<System.Collections.Generic.HashSet<string>>(banquet, "_selectedGuestIds");
            foreach (var guestId in Enumerable.Range(1, 5).Select(index => $"guest-{index}"))
            {
                banquetChecks[guestId].SetPressedNoSignal(true);
                Call(banquet, "on_guest_toggled", guestId, true);
            }
            Read<OptionButton>(banquet, "_venueSelector").Select(1);
            Call(banquet, "update_venue_details");
            BanquetSetupRequest? setupRequest = null;
            banquet.SetupRequested += request => setupRequest = request;
            Call(banquet, "submit_setup");
            Check(selectedBanquetGuests.Count == 3 && setupRequest is { HostId: "player", VenueId: "waterside_pavilion", StartsAtHour: 120 } &&
                  setupRequest.GuestIds.Count == 3,
                "水榭席位切换后未按含主持者的四人上限裁减宾客，或安排请求字段错误");

            var contributionHuman = new HumanData { entity_name = "试验主持", sect_affiliation_id = "yuxu_xianzong" };
            Check(contributionHuman.record_yuxu_contribution(new YuxuSectContributionRecord
            {
                record_id = "banquet-smoke-credit", source_transaction_id = "banquet-smoke-credit",
                transaction_kind = YuxuSectContributionRecord.KindOfficialDutyReward, source_title = "烟测预存贡献",
                occurred_at = "烟测", contribution_delta = 5, cost_summary = "增加贡献。", result_summary = "已记录。",
            }), "宴席费用烟测无法建立贡献余额");
            var hallFeeEvent = new BanquetEventData { event_id = "banquet-smoke-hall-fee", player_is_host = true, venue_id = "gathering_hall" };
            var paidHall = Call(main, "settle_banquet_fee", hallFeeEvent, contributionHuman, Array.Empty<WorldNpcData>()) is true;
            var recordsAfterHallFee = contributionHuman.sect_contribution_records.Count;
            var paidHallAgain = Call(main, "settle_banquet_fee", hallFeeEvent, contributionHuman, Array.Empty<WorldNpcData>()) is true;
            Check(paidHall && paidHallAgain && contributionHuman.get_yuxu_contribution_total() == 0 && recordsAfterHallFee == 2 &&
                  contributionHuman.sect_contribution_records.Count == recordsAfterHallFee,
                "雅集堂未扣除恰好五点贡献，或重复结算再次扣费");
            var poorHost = new HumanData { entity_name = "无贡献主持", sect_affiliation_id = "yuxu_xianzong" };
            var unpaidHall = Call(main, "settle_banquet_fee", new BanquetEventData
            {
                event_id = "banquet-smoke-unfunded", player_is_host = true, venue_id = "gathering_hall",
            }, poorHost, Array.Empty<WorldNpcData>()) is true;
            Check(!unpaidHall && poorHost.sect_contribution_records.Count == 0,
                "贡献不足时仍允许开席或留下收费记录");

            var eventData = new BanquetEventData
            {
                event_id = "banquet-smoke-response", host_id = "player", host_name = "试验主持",
                player_is_host = true, venue_id = "gathering_hall", status = BanquetEventData.InProgress,
                fee_settled = true, time_settled = true,
            };
            var socialManager = Read<NpcWorldManager>(main, "_npc_world");
            var humanPlayer = (HumanData)player.entity_data!;
            var currentMonth = Math.Max(0, clock.get_total_game_hours() / (30 * 24));
            var listenerNpc = socialManager.get_persistent_npcs()
                .Select(candidate => socialManager.get_npc(candidate.npc_id))
                .FirstOrDefault(candidate => candidate is not null && candidate.npc_id != npc.npc_id &&
                    !has_conversation_this_month(CharacterRelationshipRules.Get(humanPlayer, candidate.npc_id), currentMonth) &&
                    !has_conversation_this_month(CharacterRelationshipRules.Get(candidate, CharacterRelationshipRules.PlayerCharacterId), currentMonth))
                ?? throw new Exception("宴席烟测没有可用的独立听者人物");
            var unheardNpc = new WorldNpcData { npc_id = "banquet-smoke-unheard", npc_name = "未听见的宾客" };
            socialManager.npcs.Add(unheardNpc);
            eventData.attendee_npc_ids.Add(listenerNpc.npc_id);
            eventData.attendee_npc_ids.Add(unheardNpc.npc_id);
            var responseBeatId = eventData.event_id + ":beat:0";
            var heardBy = new Godot.Collections.Array<string> { listenerNpc.npc_id };
            eventData.beats.Add(new D
            {
                ["beat_id"] = responseBeatId,
                ["speaker_id"] = listenerNpc.npc_id,
                ["speaker_name"] = listenerNpc.npc_name,
                ["text"] = "今日听闻道友新近入门，先贺一声。",
                ["heard_by"] = heardBy,
                ["choices"] = new Godot.Collections.Array<D>
                {
                    new D { ["id"] = "humble", ["tone"] = "谦让", ["text"] = "承蒙诸位抬爱。" },
                },
            });
            eventData.beats.Add(new D
            {
                ["beat_id"] = eventData.event_id + ":beat:1",
                ["speaker_id"] = unheardNpc.npc_id,
                ["speaker_name"] = unheardNpc.npc_name,
                ["text"] = "席间尚有后话。",
                ["heard_by"] = new Godot.Collections.Array<string>(),
                ["choices"] = new Godot.Collections.Array<D>(),
            });
            Read<Godot.Collections.Array<BanquetEventData>>(main, "_banquetEvents").Add(eventData);
            var eventRecordId = $"banquet-response:{eventData.event_id}:{responseBeatId}:{listenerNpc.npc_id}";
            var relation = CharacterRelationshipRules.Get(humanPlayer, listenerNpc.npc_id);
            var pendingBefore = relation?.pending_evidence.Count ?? 0;
            var callback = new BanquetResponseSelection { EventId = eventData.event_id, BeatId = responseBeatId, TimedOut = true };
            Call(main, "on_banquet_response_chosen", callback);
            Call(main, "on_banquet_response_chosen", callback);
            var chosenResponse = eventData.responses.FirstOrDefault();
            var eventActionCount = listenerNpc.action_history.Count(record => record.dungeon_incident_id == eventRecordId);
            var unheardActionCount = unheardNpc.action_history.Count(record => record.dungeon_incident_id.Contains(eventData.event_id));
            var pendingAfter = CharacterRelationshipRules.Get(humanPlayer, listenerNpc.npc_id)?.pending_evidence.Count ?? 0;
            Check(eventData.responses.Count == 1 && chosenResponse is not null && chosenResponse["timed_out"].AsBool() &&
                  chosenResponse["choice_id"].AsString() == "humble" &&
                  eventActionCount == 1 && unheardActionCount == 0 && pendingAfter == pendingBefore + 1,
                $"超时回应重复回调后重抽或重复记录关系／非听者印象：回应={eventData.responses.Count}，听者记录={eventActionCount}，非听者记录={unheardActionCount}，待归纳={pendingBefore}→{pendingAfter}，选项={chosenResponse?.GetValueOrDefault("choice_id", "").AsString()}，超时={chosenResponse?.GetValueOrDefault("timed_out", false).AsBool()}");
            var priorResponseCount = eventData.responses.Count;
            var leaveHour = clock.get_total_game_hours();
            Call(main, "on_banquet_exit_requested");
            Check(eventData.status == BanquetEventData.LeftEarly && eventData.responses.Count == priorResponseCount &&
                  clock.get_total_game_hours() == leaveHour && eventData.fee_settled && eventData.time_settled,
                $"提前离席丢失已经发生的回应，或退还席费／已结算时辰：状态={eventData.status}，回应={eventData.responses.Count}/{priorResponseCount}，时辰={clock.get_total_game_hours()}/{leaveHour}，席费／时辰={eventData.fee_settled}/{eventData.time_settled}");

            var timeoutCount = 0;
            BanquetResponseSelection? timedOutResponse = null;
            banquet.ResponseChosen += response => { timeoutCount++; timedOutResponse = response; };
            banquet.StartActivity(new BanquetActivityPresentation
            {
                EventId = "banquet-panel-smoke",
                Title = "同门雅集",
                HostName = "试验主持",
                VenueName = "同门雅集堂",
                Participants = new[] { new BanquetParticipantPresentation { Id = "player", Name = "试验主持" } },
            });
            banquet.ShowBeat(new BanquetBeatPresentation
            {
                EventId = "banquet-panel-smoke", BeatId = "banquet-panel-smoke:beat:0",
                SpeakerName = "尹蓁", Text = "你方才说这段旧注读了三遍？",
            });
            banquet.ShowBeat(new BanquetBeatPresentation
            {
                EventId = "banquet-panel-smoke", BeatId = "banquet-panel-smoke:beat:1",
                SpeakerName = "陆晚", Text = "前两遍只顾记字句，第三遍才看明白前后原是一层意思。",
            });
            banquet.ShowBeat(new BanquetBeatPresentation
            {
                EventId = "banquet-panel-smoke", BeatId = "banquet-panel-smoke:beat:2",
                SpeakerName = "陆晚", Text = "沈照微道友，读法门时若遇到一处始终想不明白，你会先自己推敲，还是找人请益？",
                Choices = new[]
                {
                    new BanquetChoicePresentation { Id = "ask", Tone = "请益", Text = "找一位走过相似阶段的同门，把各自体会对照一遍。" },
                    new BanquetChoicePresentation { Id = "study", Tone = "细察", Text = "把前后几日逐处梳理，看看变化从哪一步开始。", RequirementText = "需悟性≥65（当前48）", IsAvailable = false },
                    new BanquetChoicePresentation { Id = "honest", Tone = "坦率", Text = "我也未必一开始就想得明白，先记下疑处再慢慢推敲。" },
                    new BanquetChoicePresentation { Id = "ask_all", Tone = "照顾同席", Text = "不妨请席间每位同门各说一件最有用的经验。", RequirementText = "需至少3位同门在席（当前2位）", IsAvailable = false },
                },
                ResponseSeconds = 12,
            });
            var recentBanquetBubbles = Read<System.Collections.Generic.IReadOnlyList<Control>>(banquet, "_recentBubbles");
            var ambientBubbles = recentBanquetBubbles.Take(2).ToArray();
            Check(ambientBubbles.Length == 2 && ambientBubbles.All(bubble =>
                    bubble.FindChildren("*", "Label", true, false).OfType<Label>().All(label =>
                        !label.Text.Contains("尹蓁", StringComparison.Ordinal) && !label.Text.Contains("陆晚", StringComparison.Ordinal))),
                "非玩家闲谈气泡显示了发言姓名");
            var bubbleLayer = Read<Control>(banquet, "_bubbleLayer");
            Check(ambientBubbles.All(bubble => bubble.Position.X >= bubbleLayer.Size.X * .05f &&
                    bubble.Position.X + bubble.Size.X <= bubbleLayer.Size.X * .95f && bubble.Position.Y >= bubbleLayer.Size.Y * .04f),
                "宴席闲谈气泡贴近屏幕极端边缘或没有留出安全区");
            await Frames(18);
            await Capture("banquet_window_review_20261016");
            banquet._Process(13);
            banquet._Process(13);
            Check(timeoutCount == 1 && timedOutResponse is { TimedOut: true, EventId: "banquet-panel-smoke" },
                "宴席应答超时重复发出或没有报告超时事件");
            banquet.QueueFree(); await Frames(3);
            GD.Print("NPC_SOCIAL PASS: monthly flow and costs; two test-only initiators, temporary profiles, window-view banquet dialogue, natural judgement reveal, and no persistent test side effects");
            quit_after_cleanup();
        }
        catch (Exception e) { GD.PushError(e.ToString()); quit_after_cleanup(1); }
    }

    private static bool has_conversation_this_month(CharacterRelationshipRecord? relation, int month) => relation is not null &&
        (relation.settled_conversation_months.Contains(month) ||
         relation.pending_evidence.Any(item => item is not null && item.event_kind == CharacterRelationshipRules.SuccessfulConversation && item.month_index == month));
}
