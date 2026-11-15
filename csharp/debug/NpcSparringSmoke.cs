using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using D = Godot.Collections.Dictionary;
using A = Godot.Collections.Array;

/// <summary>真实主场景→同门对练→原峰返回；战斗结果用快照注入，不写正式存档。</summary>
public partial class NpcSparringSmoke : Node
{
    private static void Check(bool ok, string text) { if (!ok) throw new Exception(text); }
    private static T Read<T>(object value, string name) => (T)value.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
    private static object? Call(object value, string name, params object[] args) => value.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(value, args);
    private async Task Frames(int count = 5) { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private async Task Until(Func<bool> done, string failure)
    { for (int i = 0; i < 1800; i++) { if (done()) return; await Frames(1); } throw new Exception(failure); }
    private static Button Button(StoryDialoguePanel panel, string prefix) => panel.FindChildren("*", "Button", true, false).OfType<Button>().First(b => b.IsVisibleInTree() && b.Text.StartsWith(prefix));
    public override async void _Ready()
    {
        try
        {
            var saves = GetNode<GameSaveManager>("/root/GameSaveManager"); Check(!saves.has_active_slot, "禁止写正式存档");
            var fixture = new PersistentGameSave { year = 1, month = 1, day = 8, hour = 9, current_node_id = "yuxu_xianzong_core", world_cell = new(34, 166), npc_population_version = 1, npc_simulation_enabled = false,
                player_data = new HumanData { entity_name = "沈照微", cultivation_realm = "云息三层", current_age = 16, sect_affiliation_id = "yuxu_xianzong", sect_current_rank = "外门弟子", current_world_stamina = 73 },
                scene_progress = new D { ["story_free_start"] = true, ["cloud_state_initialized"] = true } };
            var npc = new WorldNpcData { npc_id = "spar_smoke", npc_name = "陆衡", personality = "爽直", faction_id = "yuxu_xianzong", formal_identity = "外门弟子", current_node_id = "yuxu_xianzong_core", current_subscene_id = "guanfu", cultivation_realm = "云息二层", current_age = 20, gender = 0, current_health = 92, max_health = 150 };
            npc.player_social_memory["player_name"] = "沈照微"; npc.social_priorities.Add("修行"); npc.social_personality["risk"] = "冒进";
            fixture.world_npcs.Add(npc);
            typeof(GameSaveManager).GetField("_pendingLoad", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(saves, fixture);
            GetTree().CurrentScene = null;
            Check(GetTree().ChangeSceneToFile("res://tscn/scene/MainScene.tscn") == Error.Ok, "加载失败");
            await Until(() => GetTree().CurrentScene is MainScene, "主场景未载入"); await Frames(10);
            var main = (MainScene)GetTree().CurrentScene;
            npc = GetNode<NpcWorldManager>("/root/NpcWorldManager").get_npc("spar_smoke")!;
            var player = Read<Character>(main, "player"); var uid = player.unique_id; var health = player.current_health;
            var physique = player.physique; var name = player.entity_name;
            var dialogue = Read<StoryDialoguePanel>(main, "story_dialogue");
            Read<Control>(main, "tile_world_map").Hide();
            var overview = main.GetNode<Control>("SceneLayer/YuxuXianzongOverview");
            overview.Call("open_overview"); overview.Call("_on_peak_pressed", "guanfu");
            Call(main, "request_npc_spar", npc, false);
            await Frames();
            Button(dialogue, "取消").EmitSignal(BaseButton.SignalName.Pressed); dialogue.close(); dialogue.close();
            await Frames();
            Check(player.current_world_stamina == 73, "取消切磋不能收费");
            var clock = GetNode<GameTimeMgr>("/root/GameTimeManager");
            Call(main, "sync_social_membership_month"); clock.restore_calendar(1, 2, 8, 9, 0);
            Call(main, "process_npc_social_visit", .6);
            Check(dialogue.Visible && Button(dialogue, "接受") is not null, "修行同门未主动邀请切磋");
            await Frames();
            var hours = clock.get_total_game_hours();
            Button(dialogue, "接受").EmitSignal(BaseButton.SignalName.Pressed);
            await Frames();
            dialogue.close(); dialogue.close();
            await Frames();
            Check(player.current_world_stamina == 73 - WorldStaminaRules.ConversationCost, "邀约费用错误");
            var transition = GetNode<BattleTransitionManager>("/root/BattleTransitionManager");
            var before = Read<System.Collections.Generic.List<BattleEntityData>>(transition, "_beforeBattle");
            Check(before.Any(d => d.uid == uid && d.entity_name == name && d.current_health == health), "参战主角姓名或生命被替换");
            Check(before.Any(d => d.uid == "worldnpc:spar_smoke" && d.entity_name == "陆衡" && d.current_health == 92), "未使用真实对手快照");
            Call(transition, "BeginConfirmedBattle");
            await Until(() => GetTree().CurrentScene?.SceneFilePath == "res://tscn/battle/BattleScene.tscn" && !transition.is_flow_transition_active(), "未进入战斗");
            var loss = OS.GetCmdlineUserArgs().Contains("--loss");
            var attackers = new A(); var defenders = new A();
            foreach (var entry in before)
            {
                var copy = (BattleEntityData)entry.Duplicate(true);
                if (copy.uid == uid) { copy.current_health = loss ? 0 : 1; attackers.Add(copy); }
                else { copy.current_health = loss ? 1 : 0; defenders.Add(copy); }
            }
            transition.end_team_battle(attackers, defenders);
            await Until(() => Read<bool>(transition, "_awaitingSettlement") && !transition.is_flow_transition_active(), "结算未就绪");
            Call(transition, "ReturnAfterSettlement");
            await Until(() => GetTree().CurrentScene is MainScene, "未返回主场景"); await Frames(12);
            main = (MainScene)GetTree().CurrentScene; player = Read<Character>(main, "player");
            npc = GetNode<NpcWorldManager>("/root/NpcWorldManager").get_npc("spar_smoke")!;
            Check(player.unique_id == uid && player.entity_name == name && player.physique == physique && player.current_health == health, "演练回传覆盖玩家身份、属性或伤势");
            Check(player.current_world_stamina == 73 - WorldStaminaRules.ConversationCost && clock.get_total_game_hours() == hours, "重复收费或额外推进时间");
            Check(npc.current_health == 92 && npc.is_alive && npc.player_relationship == 0, "演练导致NPC伤亡或无依据关系变化");
            Check(Read<string>(main, "_player_current_yuxu_peak_id") == "guanfu" && (bool)Call(main, "has_open_world_access")!, "返回错误峰或重播青溪剧情");
            Check(npc.player_social_memory["last_spar_outcome"].AsString().Contains(loss ? "对方更胜" : "你略胜") && npc.player_known_npc_info.ContainsKey("spar_experience"), "未记住真实切磋胜负");
            Check(!transition.has_pending_battle_return_data(), "回传未一次消费");
            var summary = new D { ["battle_id"] = npc.player_social_memory["last_spar_battle"], ["player_victory"] = !loss, ["player_defeated"] = loss };
            var historyCount = npc.action_history.Count;
            main.show_npc_spar_result(summary, new D { ["npc_id"] = npc.npc_id, ["origin_peak"] = "guanfu" });
            Check(npc.action_history.Count == historyCount, "重复回传重复登记经历");
            var savePath = "res://tmp/npc_spar_memory_test.tres";
            Check(ResourceSaver.Save(main.capture_battle_world_snapshot()!, savePath) == Error.Ok, "切磋记忆保存失败");
            var loaded = ResourceLoader.Load<PersistentGameSave>(savePath, cacheMode: ResourceLoader.CacheMode.Ignore)!;
            Check(loaded.world_npcs.First(n => n.npc_id == npc.npc_id).player_social_memory["last_spar_battle"].AsString() == battleId(npc) &&
                loaded.world_npcs.First(n => n.npc_id == npc.npc_id).player_known_npc_info.ContainsKey("spar_experience"), "实际资源读档丢失交手经历");
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(savePath));
            GD.Print("NPC_SPAR PASS: " + (loss ? "loss" : "victory") + ", actual battle roundtrip, monthly invitation, free cancel, one fee, player identity/stats, no injury/loot/relation changes, original peak, one-shot memory");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
    private static string battleId(WorldNpcData npc) => npc.player_social_memory["last_spar_battle"].AsString();
}
