using Godot;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using D = Godot.Collections.Dictionary;

public partial class NpcPurchaseSmoke : Node
{
    private static void Check(bool ok, string text) { if (!ok) throw new Exception(text); }
    private static T Read<T>(object obj, string name) => (T)obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(obj)!;
    private static object? Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(obj, args);
    private async Task Frames(int n = 4) { for (var i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private static Button Choice(StoryDialoguePanel panel, string prefix) => panel.FindChildren("*", "Button", true, false).OfType<Button>().First(b => b.IsVisibleInTree() && b.Text.StartsWith(prefix));
    private static WorldNpcData Buyer(string id) => new() {
        npc_id = id, npc_name = "陈砚", faction_id = "yuxu_xianzong", formal_identity = "外门弟子",
        current_node_id = "yuxu_xianzong_core", current_subscene_id = "guanfu", cultivation_realm = "云息二层",
        short_term_goal = "补齐随身的药材和干粮。", personality = "爽直", resources = new D { ["灵石"] = 24 } };
    public override async void _Ready()
    {
        try
        {
            var poor = Buyer("poor"); poor.resources["灵石"] = 1;
            NpcPurchaseRules.SettleMonth(poor, 1); Check(poor.purchase_request is null, "余额不足仍生成报价");
            var noNeed = Buyer("none"); noNeed.short_term_goal = "打听路况";
            NpcPurchaseRules.SettleMonth(noNeed, 1); Check(noNeed.purchase_request is null, "无药材目标却强造需求");
            var prepared = Buyer("prepared"); foreach (var path in NpcPurchaseRules.HerbPaths) prepared.change_carried_item(path, 1);
            NpcPurchaseRules.SettleMonth(prepared, 1); Check(prepared.purchase_request is null, "自身已备齐仍求购");
            var a = Buyer("stable"); var b = Buyer("stable");
            NpcPurchaseRules.SettleMonth(a, 1); NpcPurchaseRules.SettleMonth(b, 1);
            Check(a.purchase_request!.item_path == b.purchase_request!.item_path, "同 ID 需求不稳定");
            var id = a.purchase_request.request_id;
            for (var i = 0; i < 100; i++) NpcPurchaseRules.Offer(a);
            NpcPurchaseRules.SettleMonth(a, 1); NpcPurchaseRules.SettleMonth(a, 2);
            Check(a.purchase_request.request_id == id && a.action_history.Count == 1, "查看/同月/未完成跨月重抽需求");
            a.resources["灵石"] = 0; Check(NpcPurchaseRules.Offer(a) is null, "余额变化后仍可报价");
            a.resources["灵石"] = 24; a.change_carried_item(a.purchase_request.item_path, 1);
            Check(NpcPurchaseRules.Offer(a) is null, "需求已满足仍可成交");

            var population = Enumerable.Range(0, 580).Select(i => Buyer("perf" + i)).ToArray();
            var timer = Stopwatch.StartNew();
            for (var month = 1; month <= 12; month++) foreach (var npc in population) NpcPurchaseRules.SettleMonth(npc, month);
            timer.Stop();
            Check(population.All(n => n.purchase_last_processed_month == 12 && n.action_history.Count == 1), "跨月需求漏算/重复生成");
            GD.Print($"NPC_PURCHASE performance: 580 NPC x12 demand maintenance {timer.Elapsed.TotalMilliseconds:0.00}ms (data-only, not full-frame)");

            var saves = GetNode<GameSaveManager>("/root/GameSaveManager"); Check(!saves.has_active_slot, "测试不得写玩家存档槽");
            var fixture = new PersistentGameSave { year = 1, month = 1, day = 8, hour = 9, current_node_id = "yuxu_xianzong_core", world_cell = new(34, 166),
                npc_population_version = 1, npc_simulation_enabled = true, npc_last_processed_month = 0,
                player_data = new HumanData { entity_name = "沈照微", cultivation_realm = "云息三层", current_age = 16,
                    sect_affiliation_id = "yuxu_xianzong", sect_current_rank = "外门弟子", current_world_stamina = 100 },
                scene_progress = new D { ["qingxi_prologue"] = 3, ["cloud_state_initialized"] = true,
                    ["xuanjian_awakened"] = true, ["social_membership_month"] = 0 } };
            fixture.world_npcs.Add(Buyer("purchase_smoke"));
            typeof(GameSaveManager).GetField("_pendingLoad", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(saves, fixture);
            GetTree().CurrentScene = null;
            Check(GetTree().ChangeSceneToFile("res://tscn/scene/MainScene.tscn") == Error.Ok, "主场景未加载");
            await Frames(12);
            var main = (MainScene)GetTree().CurrentScene;
            var world = GetNode<NpcWorldManager>("/root/NpcWorldManager"); var buyer = world.get_npc("purchase_smoke")!;
            var player = Read<Character>(main, "player"); var dialogue = Read<StoryDialoguePanel>(main, "story_dialogue");
            var clock = GetNode<GameTimeMgr>("/root/GameTimeManager");
            Check(buyer.purchase_request is null, "读档或进入场景不应生成新需求");
            clock.restore_calendar(1, 2, 8, 9, 0); world.simulate_until_month(1); world.set_simulation_enabled(false);
            Check(buyer.purchase_request is not null && buyer.purchase_last_processed_month == 1, "真实月度入口没有接入需求");
            buyer.current_node_id = "yuxu_xianzong_core"; buyer.current_subscene_id = "guanfu"; buyer.is_traveling = false;
            Read<Control>(main, "tile_world_map").Hide();
            var overview = main.GetNode<Control>("SceneLayer/YuxuXianzongOverview"); overview.Call("open_overview"); overview.Call("_on_peak_pressed", "guanfu");
            Call(main, "process_npc_social_visit", .6);
            Check(dialogue.Visible && Choice(dialogue, "出售") is not null, "过月来访未显示求购");
            Check(!Read<PanelContainer>(dialogue, "_dialogueFrame").Visible, "求购错误使用剧情底栏");
            Choice(dialogue, "婉拒").EmitSignal(BaseButton.SignalName.Pressed); dialogue.close(); await Frames();
            Check(player.current_world_stamina == 100 && buyer.purchase_request!.state == "open", "拒绝收费或清空需求");
            Call(main, "process_npc_social_visit", .6); Check(!dialogue.Visible, "同月多次主动弹出");
            var request = buyer.purchase_request!; var herb = request.item_path;
            Call(main, "request_npc_purchase", buyer, false); Choice(dialogue, "出售").EmitSignal(BaseButton.SignalName.Pressed);
            Check(player.current_world_stamina == 100 && buyer.get_carried_item_count(herb) == 0, "无物品仍成交或收费");
            dialogue.close(); await Frames();
            player.add_item_to_inventory(herb, 2); var hours = clock.get_total_game_hours();
            Call(main, "request_npc_purchase", buyer, false);
            buyer.current_subscene_id = "zifu";
            Choice(dialogue, "出售").EmitSignal(BaseButton.SignalName.Pressed);
            Check(request.state == "open" && player.get_inventory_total_count(herb) == 2, "离场后仍远程成交");
            dialogue.close(); await Frames(); buyer.current_subscene_id = "guanfu";
            player.current_world_stamina = 0;
            Check(!player.try_fulfil_npc_purchase(buyer, request.request_id, 1, out _) && request.state == "open" &&
                player.get_inventory_total_count(herb) == 2, "体力不足仍交换资产");
            player.current_world_stamina = 100;
            Call(main, "request_npc_purchase", buyer, false);
            var savedBudget = buyer.get_resource("灵石"); buyer.resources["灵石"] = 0;
            Choice(dialogue, "出售").EmitSignal(BaseButton.SignalName.Pressed);
            Check(request.state == "open" && player.current_world_stamina == 100 && player.get_inventory_total_count(herb) == 2,
                "报价后付款不足仍扣物/体力");
            dialogue.close(); await Frames(); buyer.resources["灵石"] = savedBudget;
            var initialStones = buyer.get_resource("灵石"); var initialPlayerStones = player.get_inventory_total_count(NpcPurchaseRules.StonePath);
            var notifications = 0; player.InventoryChanged += _ => notifications++;
            Call(main, "request_npc_purchase", buyer, false); await Frames();
            if (OS.GetCmdlineUserArgs().Contains("--capture")) {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng("res://tmp/npc_purchase_choices.png"); }
            Choice(dialogue, "出售").EmitSignal(BaseButton.SignalName.Pressed);
            Check(request.state == "completed" && buyer.get_resource("灵石") == initialStones - 2 && buyer.get_carried_item_count(herb) == 1 &&
                player.get_inventory_total_count(herb) == 1 && player.get_inventory_total_count(NpcPurchaseRules.StonePath) == initialPlayerStones + 2,
                "双方物品或灵石不守恒");
            Check(notifications == 1 && player.current_world_stamina == 99 && clock.get_total_game_hours() == hours && buyer.player_relationship == 0,
                "重复刷新、体力、时间或关系变化错误");
            Check(!player.try_fulfil_npc_purchase(buyer, request.request_id, 1, out _) && player.current_world_stamina == 99, "重复成交");
            dialogue.close(); await Frames();
            if (OS.GetCmdlineUserArgs().Contains("--capture")) {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng("res://tmp/npc_purchase_result.png"); }
            dialogue.close(); await Frames();
            NpcPurchaseRules.SettleMonth(buyer, 1); Check(buyer.purchase_request == request, "完成后当月重生成");
            buyer.purchase_last_processed_month = 0;
            NpcPurchaseRules.SettleMonth(buyer, 1); Check(buyer.purchase_request == request, "完成月份游标未阻止重生成");

            // 满行囊试算：卖出后仍无空位时不改变任何资产；恰好释放一格时允许收款。
            var human = (HumanData)player.entity_data!;
            human.inventory_item_paths.Clear(); human.inventory_item_counts.Clear(); human.inventory_item_positions.Clear();
            for (var i = 0; i < Character.InventorySlotCapacity; i++) {
                human.inventory_item_paths.Add(i == 0 ? herb : "res://resources/entities/items/兽骨.tres");
                human.inventory_item_counts.Add(2); human.inventory_item_positions.Add(i); }
            var fullBuyer = Buyer("full"); NpcPurchaseRules.SettleMonth(fullBuyer, 1);
            fullBuyer.purchase_request!.item_path = herb;
            Check(!player.try_fulfil_npc_purchase(fullBuyer, fullBuyer.purchase_request.request_id, 1, out _) &&
                human.inventory_item_counts[0] == 2 && fullBuyer.get_resource("灵石") == 24 && fullBuyer.purchase_request.state == "open" &&
                player.current_world_stamina == 99, "满行囊失败未原子回退");
            human.inventory_item_counts[0] = 1;
            Check(player.try_fulfil_npc_purchase(fullBuyer, fullBuyer.purchase_request.request_id, 1, out _) &&
                human.inventory_item_paths.Count == Character.InventorySlotCapacity, "卖出释放空位仍不能收款");
            buyer.private_commission_escrows.Add(new NpcPrivateCommissionEscrow { commission_id = "draft-test", issuer_id = buyer.npc_id });
            var saved = main.capture_battle_world_snapshot()!;
            const string savePath = "res://tmp/npc_purchase_smoke.tres";
            Check(ResourceSaver.Save(saved, savePath) == Error.Ok, "需求保存失败");
            var reloaded = ResourceLoader.Load<PersistentGameSave>(savePath, cacheMode: ResourceLoader.CacheMode.Ignore)!;
            var restored = reloaded.world_npcs.First(n => n.npc_id == buyer.npc_id);
            Check(restored.purchase_request is { state: "completed", completed_month: 1 } && restored.purchase_last_processed_month == 1 &&
                restored.private_commission_escrows[0].reward_reserved == 0 && NpcPurchaseRules.Offer(restored) is null &&
                restored.action_history.Count(n => n.action_id == "purchase_completed") == 1, "读档丢失结算防重/私委结构");
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(savePath));
            GD.Print("NPC_PURCHASE PASS: monthly generation, actual UI, decline, absent item, remote guard, atomic assets, full bag, single notification, no relation farming, repeat/save guards");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
