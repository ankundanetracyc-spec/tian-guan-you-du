using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using D = Godot.Collections.Dictionary;
using A = Godot.Collections.Array;

/// <summary>隔离存档中的真实地点、非同门对练及主动攻击往返。</summary>
public partial class NpcInteractionPageSmoke : Node
{
    private int _checks;
    private void Check(bool ok, string why) { _checks++; if (!ok) throw new Exception(why); }
    private static T Read<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(target)!;
    private static object? Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(target, args);
    private async Task Frames(int n = 6) { for (var i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private async Task Until(Func<bool> done, string why) { for (var i = 0; i < 3600; i++) { if (done()) return; await Frames(1); } throw new Exception(why); }
    private static Button Button(Node page, string prefix) => page.FindChildren("*", "Button", true, false).OfType<Button>().First(b => b.IsVisibleInTree() && b.Text.StartsWith(prefix));
    private static void Press(Node page, string prefix) => Button(page, prefix).EmitSignal(BaseButton.SignalName.Pressed);
    public override async void _Ready()
    {
        try
        {
            Check(OS.GetEnvironment("APPDATA").Contains("npc_page"), "必须使用隔离存档");
            var mode = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--case="))?[7..] ?? "spar";
            var saves = GetNode<GameSaveManager>("/root/GameSaveManager");
            Check(!saves.has_active_slot, "不能写玩家正式存档");
            var fixture = new PersistentGameSave { year = 1, month = 1, day = 8, hour = 9,
                current_node_id = "white_stone_town", npc_population_version = 1, npc_simulation_enabled = false,
                player_data = new HumanData { entity_name = "沈照微", cultivation_realm = "云息三层", current_age = 18,
                    base_physique = 5, base_intelligence = 5, base_soul = 5, current_world_stamina = 73 },
                scene_progress = new D { ["story_free_start"] = true, ["cloud_state_initialized"] = true, ["baishi_lecture"] = new BaishiLectureProgress { departed_qingxi = true, fang_returned = true, finished = true } } };
            fixture.node_world_state["current_node"] = "bc_place_021";
            const string item = "res://resources/entities/items/兽骨.tres";
            var npc = new WorldNpcData { npc_id = "npc_page_smoke", npc_name = "陆衡", faction_id = "baopu_shan",
                formal_identity = "行脚修士", current_node_id = "white_stone_town", current_subscene_id = "inn",
                cultivation_realm = "云息二层", current_age = 20, current_health = 92, max_health = 150, gender = 0 };
            npc.social_personality["risk"] = "冒进"; npc.social_priorities.Add("修行");
            npc.player_social_memory["player_name"] = "沈照微";
            npc.carried_items[item] = 2; npc.trade_inventory[item] = 7;
            fixture.world_npcs.Add(npc);
            var other = (WorldNpcData)npc.Duplicate(true); other.npc_id = "npc_page_second"; other.npc_name = "许知秋"; other.gender = 1;
            fixture.world_npcs.Add(other);
            typeof(GameSaveManager).GetField("_pendingLoad", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(saves, fixture);
            GetTree().CurrentScene = null;
            Check(GetTree().ChangeSceneToFile("res://tscn/scene/MainScene.tscn") == Error.Ok, "主场景加载失败");
            await Until(() => GetTree().CurrentScene is MainScene, "没有进入正式主场景"); await Frames(20);
            var main = (MainScene)GetTree().CurrentScene;
            var world = GetNode<NpcWorldManager>("/root/NpcWorldManager");
            npc = world.get_npc(npc.npc_id)!; other = world.get_npc(other.npc_id)!;
            var player = main.get_player_character()!;
            var human = (HumanData)player.entity_data!;
            var health = player.current_health;
            var uid = player.unique_id;
            var time = GetNode<GameTimeMgr>("/root/GameTimeManager"); var hours = time.get_total_game_hours();
            var dialogue = Read<StoryDialoguePanel>(main, "story_dialogue");
            for (var i = 0; i < 40 && dialogue.Visible; i++) dialogue.close();
            Call(main, "on_node_enter_requested", "white_stone_town");
            var interior = Read<WorldLocationInteriorPanel>(main, "location_interior");
            var nodes = Read<WorldNodeMapData>(main, "_world_node_data");
            interior.show_baishi_space(nodes.get_node("white_stone_town")!, world, "inn"); await Frames();
            main.open_world_npc_actions(npc); await Frames();
            var page = Read<NpcInteractionPanel>(main, "npc_interactions");
            Check(page.Visible && !Button(page, "切磋").Disabled && Button(page, "攻击").Disabled, "城中应允许邀约切磋并禁止私斗");
            nodes.get_node("white_stone_town")!.hostile_combat_policy = WorldConflictRules.Policy.Allowed;
            main.open_world_npc_actions(npc);
            var stale = Button(page, "攻击"); main.open_world_npc_actions(other); stale.EmitSignal(BaseButton.SignalName.Pressed);
            Check(page.get_displayed_npc_id() == other.npc_id && Button(page, "攻击") is not null, "换人后旧按钮攻击了新人物");
            main.open_world_npc_actions(npc); Press(page, "攻击"); await Frames();
            Check(page.Visible && Button(page, "确认攻击") is not null, "攻击没有局部二次确认");
            Press(page, "收手"); await Frames();
            Check(player.current_world_stamina == 73 && npc.current_health == 92 && world.get_all_east_woods_incidents().Count == 0,
                "取消确认产生费用、伤害或案卷");
            var relationship = CharacterRelationshipRules.Value(npc, "player", CharacterRelationshipRules.Resentment);
            npc.current_subscene_id = "market";
            Check(((string)Call(main, "npc_spar_unavailable_reason", npc)!).Length > 0 && ((string)Call(main, "npc_attack_unavailable_reason", npc, false)!).Length > 0,
                "人物离开后仍可交手");
            npc.current_subscene_id = "inn";
            npc.is_yuxu_lineage_staff = true;
            Check(((string)Call(main, "npc_attack_unavailable_reason", npc, false)!).Contains("带教"), "关键带教人物未保护");
            npc.is_yuxu_lineage_staff = false;
            npc.current_state = "闭关修行";
            Check(((string)Call(main, "npc_attack_unavailable_reason", npc, false)!).Contains("闭关"), "闭关人物仍可攻击");
            npc.current_state = "空闲";
            var transition = GetNode<BattleTransitionManager>("/root/BattleTransitionManager");
            if (mode == "spar")
            {
                Check(NpcSparringRules.Supports(npc, human) && NpcSparringRules.Evaluate(npc, human).AgreesToSpar,
                    "非同门云息修士未能接受切磋");
                Check(!NpcSparringRules.WantsToInvite(npc, human), "主动邀约错误扩大了宗内月度来访");
                Call(main, "request_npc_spar", npc, false); await Frames(); Press(dialogue, "取消"); dialogue.close(); dialogue.close();
                Check(player.current_world_stamina == 73, "取消切磋扣费");
                Call(main, "request_npc_spar", npc, false); await Frames(); Press(dialogue, "发出邀约");
                dialogue.close(); dialogue.close(); await Frames();
                Check(player.current_world_stamina == 72, "商议切磋未准确扣一次精力");
            }
            else
            {
                Call(main, "start_npc_attack", npc.npc_id); await Frames();
                Check(transition.has_open_flow_view(), "攻击未进入战前页");
                npc.current_subscene_id = "market";
                Call(transition, "BeginConfirmedBattle"); await Frames();
                Check(!transition.has_open_flow_view() && world.get_all_east_woods_incidents().Count == 0,
                    "战前目标离开后仍启动交手");
                npc.current_subscene_id = "inn";
                for (var i = 0; i < 40 && dialogue.Visible; i++) dialogue.close();
                Call(main, "start_npc_attack", npc.npc_id); await Frames();
                Check(transition.try_return_from_open_flow(), "战前取消不可用"); await Frames();
                Check(world.get_all_east_woods_incidents().Count == 0 && npc.current_health == 92 &&
                    CharacterRelationshipRules.Value(npc, "player", CharacterRelationshipRules.Resentment) == relationship,
                    "战前取消已写入仇怨或伤亡");
                Call(main, "start_npc_attack", npc.npc_id); await Frames();
            }
            var before = Read<System.Collections.Generic.List<BattleEntityData>>(transition, "_beforeBattle");
            Check(before.Any(s => s.uid == uid && s.current_health == health) &&
                before.Any(s => s.uid == "worldnpc:" + npc.npc_id && s.current_health == 92 && s.entity_name == npc.npc_name),
                "战斗未使用双方真实快照");
            var context = Read<D>(transition, "return_context").Duplicate(true);
            Call(transition, "BeginConfirmedBattle");
            await Until(() => GetTree().CurrentScene?.SceneFilePath == "res://tscn/battle/BattleScene.tscn" && !transition.is_flow_transition_active(), "未进入真实战斗");
            var attackers = new A(); var defenders = new A();
            foreach (var entry in before)
            {
                var copy = (BattleEntityData)entry.Duplicate(true);
                if (copy.uid == uid) { copy.current_health = mode == "loss" ? 0 : 1; attackers.Add(copy); }
                else { copy.current_health = mode == "loss" ? 1 : mode == "escape" ? 80 : mode == "unharmed" ? 92 : 0; defenders.Add(copy); }
            }
            if (mode is "escape" or "unharmed") transition.end_team_battle_escaped(attackers, defenders);
            else transition.end_team_battle(attackers, defenders);
            await Until(() => Read<bool>(transition, "_awaitingSettlement") && !transition.is_flow_transition_active(), "战后结算未准备好");
            var summary = Read<D>(transition, "last_summary").Duplicate(true);
            Call(transition, "ReturnAfterSettlement");
            await Until(() => GetTree().CurrentScene is MainScene, "未返回正式世界"); await Frames(20);
            main = (MainScene)GetTree().CurrentScene; player = main.get_player_character()!;
            world = GetNode<NpcWorldManager>("/root/NpcWorldManager"); npc = world.get_npc(npc.npc_id)!;
            Check(player.unique_id == uid && player.entity_name == "沈照微", "回传丢失主角身份");
            Check(Read<WorldLocationInteriorPanel>(main, "location_interior").get_baishi_space_id() == "inn", "没有返回发起交手的客栈");
            Check(time.get_total_game_hours() == hours, "交手额外推进时间");
            Check(npc.trade_inventory[item].AsInt32() == 7, "错误掠夺商贸库存");
            if (mode == "spar")
            {
                Check(player.current_health == health && npc.current_health == 92 && npc.is_alive, "切磋写回伤亡");
                Check(npc.get_carried_item_count(item) == 2 && world.get_all_east_woods_incidents().Count == 0, "切磋产生掠夺或敌对案卷");
                Check(npc.player_social_memory.ContainsKey("last_spar_battle"), "未保存非同门切磋经历");
                Check(player.current_world_stamina == 72, "切磋重复收费");
            }
            else
            {
                var incidentId = context["incident_id"].AsString();
                var incident = world.get_east_woods_incident(incidentId);
                Check(incident is not null && incident.attacker_npc_id == "player" && incident.target_npc_id == npc.npc_id,
                    "主动攻击未生成准确的冲突事实");
                Check(incident!.case_status == EastWoodsNpcIncidentRecord.StatusPlayerConflictPendingReview && world.get_east_woods_incidents_for_review().Count == 0,
                    "玩家案件误入NPC自动裁断/差事");
                Check(CharacterRelationshipRules.Value(npc, "player", CharacterRelationshipRules.Resentment) == (mode == "unharmed" ? 0 : CharacterRelationshipRules.HarmBaseAmount),
                    "实际伤害没有沿用受害者单向怨结");
                Check(CharacterRelationshipRules.Value((HumanData)player.entity_data!, npc.npc_id, CharacterRelationshipRules.Resentment) == 0,
                    "主动攻击反向创造玩家怨结");
                if (mode == "win")
                {
                    Check(!npc.is_alive && npc.current_health == 0 && npc.death_cause.Contains("玩家"), "目标死亡未持久化或死因错误");
                    Check(!world.get_subscene_roster("white_stone_town", "inn").Any(n => n.npc_id == npc.npc_id), "死者仍可在左侧名单交互");
                    await Until(() => !transition.is_flow_transition_active() && main.has_active_item_claim(), "实际随身物品未进入领取流程");
                    Check(main.has_active_item_claim(), "实际随身物品进入领取流程");
                    Call(Read<ItemClaimPanel>(main, "_active_item_claim"), "claim_selected_items"); await Frames();
                    Check(!GetTree().Paused, "领取后未恢复正常游戏");
                    Check(npc.get_carried_item_count(item) == 0 && player.get_inventory_total_count(item) >= 2, "战利品未从实际携带账转移");
                    Check(incident.item_transfers.Count == 1 && incident.item_transfers[0].quantity == 2,
                        $"未记录实际领取物品：{incident.item_transfers.Count} 条，当前案卷 {world.get_east_woods_incident(incidentId)!.item_transfers.Count} 条；账 {transition.export_reward_ledger()}");
                    transition.resume_pending_loot();
                    Check(!main.has_active_item_claim(), "已领取战利品可以被重复领取");
                }
                if (mode == "loss") Check(npc.is_alive && player.current_health >= 1 && incident.item_transfers.All(t => t.from_npc_id == "player"), "战败恢复或夺物方向错误");
                if (mode == "escape") Check(npc.is_alive && npc.current_health == 80 && incident.item_transfers.Count == 0, "撤离丢弃真实伤势或错误掠夺");
                if (mode == "unharmed") Check(npc.is_alive && npc.current_health == 92 && incident.item_transfers.Count == 0, "无伤撤离错误结算伤势或夺物");
                var count = npc.action_history.Count; var transfers = incident.item_transfers.Count;
                var injury = ((HumanData)player.entity_data!).world_status_records.FirstOrDefault(s => s.status_id == WorldStatusRules.SevereInjuryId);
                if (mode == "loss") { Check(injury is not null, "正式战败未沿用原重伤规则"); injury!.recovery_hours_progress = 1; }
                main.handle_persistent_battle_return(attackers, defenders, summary, context);
                Check(npc.action_history.Count == count && incident.item_transfers.Count == transfers && world.get_all_east_woods_incidents().Count == 1,
                    "重复战后回调重复伤亡、物品或案卷");
                if (mode == "loss") Check(injury!.recovery_hours_progress == 1, "重复战后回调重置已经开始的重伤休养");
            }
            var savePath = "user://npc_page_roundtrip.tres";
            Check(ResourceSaver.Save(main.capture_battle_world_snapshot()!, savePath) == Error.Ok, "存档失败");
            var loaded = ResourceLoader.Load<PersistentGameSave>(savePath, cacheMode: ResourceLoader.CacheMode.Ignore)!;
            Check(loaded.world_npcs.First(n => n.npc_id == npc.npc_id).is_alive == npc.is_alive, "读档丢失人物生死状态");
            Check(loaded.world_npcs.First(n => n.npc_id == npc.npc_id).get_carried_item_count(item) == npc.get_carried_item_count(item), "读档丢失随身物品");
            if (mode != "spar")
                Check(loaded.east_woods_npc_incidents.Count == 1 &&
                    loaded.east_woods_npc_incidents[0].case_status == EastWoodsNpcIncidentRecord.StatusPlayerConflictPendingReview &&
                    loaded.east_woods_npc_incidents[0].item_transfers.Count == world.get_all_east_woods_incidents()[0].item_transfers.Count,
                    "读档丢失待核冲突事实或实际转移记录");
            GD.Print($"NPC_PAGE_SMOKE PASS: {mode}, {_checks} checks"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
