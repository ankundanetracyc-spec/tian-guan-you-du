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
    private static Button Choice(StoryDialoguePanel panel, string prefix) => panel.FindChildren("*", "Button", true, false).OfType<Button>().First(b => b.IsVisibleInTree() && b.Text.StartsWith(prefix));
    private async Task Capture(string name)
    {
        if (!OS.GetCmdlineUserArgs().Contains("--capture")) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng("res://tmp/npc_social_process_" + name + ".png");
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
                scene_progress = new D { ["qingxi_prologue"] = 3, ["cloud_state_initialized"] = true, ["xuanjian_awakened"] = true } };
            fixture.world_npcs.Add(new WorldNpcData { npc_id = "social_smoke", npc_name = "陈砚", faction_id = "yuxu_xianzong", formal_identity = "外门弟子", current_node_id = "yuxu_xianzong_core", current_subscene_id = "guanfu", cultivation_realm = "云息二层", current_age = 20, gender = 0 });
            typeof(GameSaveManager).GetField("_pendingLoad", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(saves, fixture);
            GetTree().CurrentScene = null;
            Check(GetTree().ChangeSceneToFile("res://tscn/scene/MainScene.tscn") == Error.Ok, "主场景加载失败");
            await Frames(10);
            var main = (MainScene)GetTree().CurrentScene;
            var world = GetNode<NpcWorldManager>("/root/NpcWorldManager"); var npc = world.get_npc("social_smoke")!;
            var player = Read<Character>(main, "player"); var dialogue = Read<StoryDialoguePanel>(main, "story_dialogue");
            var trueDisciple = OS.GetCmdlineUserArgs().Contains("--true-disciple");
            if (trueDisciple) ((HumanData)player.entity_data!).sect_current_rank = "真传弟子";
            var openingChoice = "接受";
            var overview = main.GetNode<Control>("SceneLayer/YuxuXianzongOverview");
            Read<Control>(main, "tile_world_map").Hide();
            overview.Call("open_overview"); overview.Call("_on_peak_pressed", "guanfu");
            Call(main, "sync_social_membership_month");
            Call(main, "process_npc_social_visit", .6);
            Check(!dialogue.Visible, "入宗当月不应立即主动结识");
            var clock = GetNode<GameTimeMgr>("/root/GameTimeManager"); clock.restore_calendar(1, 2, 8, 9, 0);
            Call(main, "process_npc_social_visit", .6);
            Check(dialogue.Visible && Choice(dialogue, openingChoice) is not null, "次月在场同门未主动搭话");
            Check(!Read<PanelContainer>(dialogue, "_dialogueFrame").Visible && dialogue.GetNodeOrNull("SocialInteraction") is not null, "交往不应显示底部剧情栏");
            Check(!NpcSocialRules.KnowsName(npc) && player.current_world_stamina == 100, "打开选择不应交换姓名或扣体力");
            await Frames(20); await Capture("choices");
            Choice(dialogue, "婉拒").EmitSignal(BaseButton.SignalName.Pressed);
            dialogue.close(); dialogue.close();
            Check(!NpcSocialRules.KnowsName(npc) && player.current_world_stamina == 100, "拒绝来访不应收费或自动认识");
            main.open_world_npc_talk(npc);
            var hours = clock.get_total_game_hours();
            Choice(dialogue, openingChoice).EmitSignal(BaseButton.SignalName.Pressed);
            Check(NpcSocialRules.KnowsName(npc), "自报姓名未记忆");
            Check(player.current_world_stamina == 100 - WorldStaminaRules.ConversationCost && npc.player_relationship == 0, "费用或无依据关系加分错误");
            Check(clock.get_total_game_hours() == hours && NpcSocialRules.Address(npc, 16) == "道友" && !npc.player_social_memory.ContainsKey("player_realm"), "交换姓名不应泄露境界或推进时间");
            Call(dialogue, "process_social_reveal", .7); await Frames(); await Capture("progress");
            dialogue.close(); Check(dialogue.Visible, "展开判定应保留对话");
            await Frames(); await Capture("result"); dialogue.close();
            Call(main, "process_npc_social_visit", .6); Check(!dialogue.Visible, "同月反复弹出");
            var saved = main.capture_battle_world_snapshot()!;
            Check(saved.scene_progress["social_visit_month"].AsInt32() == 1 && saved.world_npcs.First(n => n.npc_id == npc.npc_id).player_social_memory["player_name"].AsString() == "沈照微", "快照丢失交谈记忆或月游标");
            var path = "res://tmp/npc_social_smoke_save.tres";
            Check(ResourceSaver.Save(saved, path) == Error.Ok, "资源存档失败");
            var reloaded = ResourceLoader.Load<PersistentGameSave>(path, cacheMode: ResourceLoader.CacheMode.Ignore)!;
            Check(reloaded.world_npcs.First(n => n.npc_id == npc.npc_id).player_social_memory["player_name"].AsString() == "沈照微", "实际资源读档丢失姓名");
            var restoredNpc = reloaded.world_npcs.First(n => n.npc_id == npc.npc_id);
            Check(restoredNpc.social_personality.Count == 6 && restoredNpc.social_priorities.Count > 0 &&
                restoredNpc.player_known_npc_info.ContainsKey("name"), "实际读档丢失六轴、所重或交际所得");
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
            main.open_world_npc_talk(npc); Check(Read<Label>(dialogue, "_body").Text.Contains("闲谈近况"), "熟人仍重复初识");
            var stamina = player.current_world_stamina; dialogue.close(); dialogue.close(); dialogue.close();
            Check(!dialogue.Visible && player.current_world_stamina == stamina, "Esc告辞应免费且能正常关闭");
            dialogue.show_narration("剧情对白回归检查");
            Check(Read<PanelContainer>(dialogue, "_dialogueFrame").Visible && Read<Control?>(dialogue, "_socialContent") is null, "普通剧情对白未恢复底栏"); dialogue.close();
            npc.current_subscene_id = "zifu"; main.open_world_npc_talk(npc); Check(!dialogue.Visible, "不在同峰的人不应远程交谈");
            npc.current_subscene_id = "guanfu"; player.current_world_stamina = 0;
            main.open_world_npc_talk(npc); Choice(dialogue, "接受").EmitSignal(BaseButton.SignalName.Pressed);
            Check(player.current_world_stamina == 0, "体力不足不应扣成负数"); dialogue.close();
            npc.player_social_memory["relationship"] = "enemy";
            Check(NpcSocialRules.Address(npc, 16) == "沈照微", "仇人应使用已知姓名");
            GD.Print("NPC_SOCIAL PASS: actual month greeting, choices, judgement reveal, known-realm address, costs, repeat/remote guards, memory resource save/load, no relation farming, no save-slot writes");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
