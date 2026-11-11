using Godot;
using System.Reflection;
using D = Godot.Collections.Dictionary;

/// <summary>Isolated market transactions, persistence and native UI; never writes a player save slot.</summary>
public partial class YuxuMarketSmoke : Node
{
    private const string Stone = YuxuMarketRules.StonePath;
    private const string Herb = "res://resources/entities/items/雾铃花.tres";
    private const string Bone = "res://resources/entities/items/兽骨.tres";
    private int _checks;
    private void Check(bool ok, string message) { _checks++; if (!ok) throw new InvalidOperationException(message); }
    private async Task Frames(int n = 4) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private static T Field<T>(object o, string name) => (T)o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(o)!;
    private static void Call(object o, string name) => o.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(o, null);
    private Character Player(string sect = "yuxu_xianzong")
    {
        var p = new Character { entity_data = new HumanData { entity_name = "坊市测试", sect_affiliation_id = sect, sect_current_rank = "外门弟子", current_world_stamina = 100 } };
        AddChild(p); return p;
    }
    private static WorldNpcData Npc(string id) => new() { npc_id = id, npc_name = "测试同门", faction_id = "yuxu_xianzong",
        current_node_id = YuxuMarketRules.NodeId, current_subscene_id = YuxuMarketRules.PeakId, current_state = "空闲", is_alive = true };
    private async Task Capture(string name)
    {
        if (!OS.GetCmdlineUserArgs().Contains("--capture") || DisplayServer.GetName() == "headless") return;
        if (OS.GetCmdlineUserArgs().Contains("--small")) name = name.Replace("_1920", "_1280", StringComparison.Ordinal);
        await Frames(8); HoverInfoManager.HideAll(); await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        Check(GetViewport().GetTexture().GetImage().SavePng("res://docs/美术规范/宗内坊市交易界面_20261107/" + name + ".png") == Error.Ok, "截图失败");
    }
    private async Task CheckMainSceneEntry()
    {
        var saves = GetNode<GameSaveManager>("/root/GameSaveManager");
        Check(!saves.has_active_slot, "入口检查不能使用真实存档槽");
        var main = GD.Load<PackedScene>("res://tscn/scene/MainScene.tscn").Instantiate<MainScene>(); AddChild(main);
        for (int i = 0; i < 600 && !main.StartupComplete; i++) await Frames(1);
        Check(main.StartupComplete && main.StartupError is null, "正式主场景启动失败"); await Frames(8);
        var player = Field<Character>(main, "player"); var human = (HumanData)player.entity_data!;
        var dialogue = Field<StoryDialoguePanel>(main, "story_dialogue"); for (int i = 0; i < 10 && dialogue.Visible; i++) dialogue.close();
        human.sect_affiliation_id = "yuxu_xianzong"; human.sect_current_rank = "外门弟子";
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(MainScene).GetField("_player_world_node_id", flags)!.SetValue(main, "yuxu_xianzong_core");
        typeof(MainScene).GetMethod("refresh_yuxu_life_context", flags)!.Invoke(main, null);
        var overview = main.GetNode<Control>("SceneLayer/YuxuXianzongOverview"); overview.Call("open_overview"); overview.Call("_on_peak_pressed", "heguang"); await Frames(10);
        var peak = overview.GetNode<Control>("PeakInterior");
        var entry = peak.Get("_action_buttons").AsGodotDictionary().GetValueOrDefault("heguang_market", default).AsGodotObject() as Button;
        Check(entry?.IsVisibleInTree() == true, "和光峰没有显示坊市挂牌");
        entry!.EmitSignal(BaseButton.SignalName.Pressed); await Frames(8);
        var panel = Field<YuxuMarketPanel>(main, "_yuxuMarket");
        Check(panel.IsVisibleInTree(), "和光峰挂牌没有打开正式坊市");
        human.yuxu_market_pending_stones = 2; panel.Refresh();
        Field<Button>(panel, "_stallTab").EmitSignal(BaseButton.SignalName.Pressed); await Frames();
        int originalStones = player.get_inventory_total_count(Stone);
        Field<Button>(panel, "_claim").EmitSignal(BaseButton.SignalName.Pressed); await Frames();
        Check(main.has_active_item_claim(), "坊市所得没有打开正式领取界面");
        var claim = Field<ItemClaimPanel>(main, "_active_item_claim"); Call(claim, "commit_receipt"); await Frames();
        Check(!main.has_active_item_claim() && human.yuxu_market_pending_stones == 2 && player.get_inventory_total_count(Stone) == originalStones,
            "放弃领取损失所得或发放物品");
        Field<Button>(panel, "_claim").EmitSignal(BaseButton.SignalName.Pressed); await Frames();
        claim = Field<ItemClaimPanel>(main, "_active_item_claim"); Call(claim, "auto_place"); Call(claim, "commit_receipt"); await Frames();
        Check(!main.has_active_item_claim() && human.yuxu_market_pending_stones == 0 && player.get_inventory_total_count(Stone) == originalStones + 2,
            "正式领取没有按实际入包数量结算");
        Check(main.try_return_to_previous_page() && !panel.Visible && overview.Visible && peak.Visible, "公共返回未退回和光峰");
        Check(!saves.has_active_slot && !saves.IsBackgroundSaveBusy, "隔离入口测试写入了存档槽");
        main.prepare_return_to_title_from_menu(false); main.QueueFree(); await Frames(4);
    }
    public override void _Ready() => CallDeferred(nameof(Run));
    private async void Run()
    {
        try
        {
            if (OS.GetCmdlineUserArgs().Contains("--capture") && DisplayServer.GetName() != "headless")
            {
                GetWindow().Mode = Window.ModeEnum.Windowed;
                GetWindow().Size = OS.GetCmdlineUserArgs().Contains("--small") ? new Vector2I(1280, 720) : new Vector2I(1920, 1080);
                await Frames(8);
            }
            Check(YuxuMarketRules.ReferencePrice(Herb) == 2 && YuxuMarketRules.ReferencePrice(Bone) == 3, "试玩价格表未加载");
            var outsider = Player(""); Check(outsider.add_item_to_inventory(Stone, 2), "非成员夹具失败");
            Check(!YuxuMarketRules.TryRent(outsider, 0, out _) && outsider.get_inventory_total_count(Stone) == 2, "非成员租摊或扣钱");
            var p = Player(); var h = (HumanData)p.entity_data!;
            Check(p.add_item_to_inventory(Stone, 10) && p.add_item_to_inventory(Herb, 4), "行囊夹具失败");
            Check(YuxuMarketRules.TryRent(p, 50, out _) && p.get_inventory_total_count(Stone) == 9, "租金没有真实扣除");
            Check(h.yuxu_market_lease_end_hour == 770 && YuxuMarketRules.Active(h, 769) && !YuxuMarketRules.Active(h, 770), "租期边界错误");
            Check(!YuxuMarketRules.TryRent(p, 60, out _) && p.get_inventory_total_count(Stone) == 9, "重复租摊扣钱");
            Check(YuxuMarketRules.TryList(p, Herb, 2, 2, 60, out _) && p.get_inventory_total_count(Herb) == 2, "上架未真实移出物品");
            Check(!YuxuMarketRules.TryList(p, Stone, 1, 1, 60, out _) && !YuxuMarketRules.TryList(p, Herb, 4, 2, 60, out _), "灵石或超量物品被上架");
            var listing = h.yuxu_market_listings[0]; var listingId = listing.GetValueOrDefault("id", "").AsString();
            var buyer = Npc("herb_buyer"); buyer.short_term_goal = "补齐随身的药材和干粮。"; buyer.resources["灵石"] = 2;
            NpcPurchaseRules.SettleMonth(buyer, 1); Check(buyer.purchase_request is not null, "药材需求未生成"); buyer.purchase_request!.item_path = Herb;
            Check(YuxuMarketRules.TryAutomaticSale(h, buyer, 1), "真实需求未能购买摊货");
            Check(buyer.get_resource("灵石") == 0 && buyer.get_carried_item_count(Herb) == 1 && h.yuxu_market_pending_stones == 2 &&
                listing.GetValueOrDefault("quantity", 0).AsInt32() == 1 && buyer.purchase_request.state == "completed", "买卖资产或原求购状态不一致");
            Check(!YuxuMarketRules.TryAutomaticSale(h, buyer, 1) && h.yuxu_market_pending_stones == 2, "同一同门重复成交");
            var tooPoor = Npc("poor_buyer"); tooPoor.short_term_goal = "与当地人换取一件有用的物品。"; tooPoor.resources["灵石"] = 1;
            Check(!YuxuMarketRules.TryAutomaticSale(h, tooPoor, 1) && tooPoor.get_resource("灵石") == 1, "无钱仍购买");
            var absent = Npc("absent_buyer"); absent.short_term_goal = "与当地人换取一件有用的物品。"; absent.resources["灵石"] = 5; absent.current_subscene_id = "guanfu";
            Check(!YuxuMarketRules.TryAutomaticSale(h, absent, 1), "不在和光峰仍可购买");
            Check(!YuxuMarketRules.TryAutomaticSale(h, Npc("no_need"), 1), "无需求仍购买");
            Check(!YuxuMarketRules.TryAutomaticSale(h, absent, 2), "租期外仍成交");
            Check(YuxuMarketRules.TryReprice(h, listingId, 4, 100, out _) && listing.GetValueOrDefault("unit_price", 0).AsInt32() == 4, "改价未保存");
            var high = Npc("high_price"); high.short_term_goal = "与当地人换取一件有用的物品。"; high.resources["灵石"] = 20;
            Check(!YuxuMarketRules.TryAutomaticSale(h, high, 1), "高于同门愿付价仍成交");
            Check(YuxuMarketRules.TryWithdraw(p, listingId, out _) && p.get_inventory_total_count(Herb) == 3 && h.yuxu_market_listings.Count == 0, "撤下未返还未售物品");
            Check(YuxuMarketRules.TryClaim(p, out _) && p.get_inventory_total_count(Stone) == 11 && h.yuxu_market_pending_stones == 0, "领取所得不守恒");
            Check(!YuxuMarketRules.TryClaim(p, out _), "所得重复领取");
            var seller = Npc("seller"); seller.resources["灵石"] = 4; seller.trade_inventory[Bone] = 1; seller.trade_inventory[Stone] = 7;
            Check(YuxuMarketRules.Offers([seller]).Count == 1, "商品清单包括交易库存灵石或不显示货品");
            Check(!YuxuMarketRules.TryBuy(p, seller, Bone, 1, 2, 100, out _), "过时报价仍成交");
            int playerBefore = p.get_inventory_total_count(Stone);
            float staminaBefore = p.current_world_stamina;
            Check(YuxuMarketRules.TryBuy(p, seller, Bone, 1, 3, 100, out _) && p.get_inventory_total_count(Bone) == 1 &&
                p.get_inventory_total_count(Stone) == playerBefore - 3 && seller.get_resource("灵石") == 7 && seller.trade_inventory[Stone].AsInt32() == 7,
                "买入未守恒或改动旧交易库存灵石");
            Check(Mathf.IsEqualApprox(p.current_world_stamina, staminaBefore - WorldStaminaRules.InteractionCost), "购买未消耗交互精力");
            Check(YuxuMarketRules.Offers([seller]).Count == 0 && !seller.trade_inventory.ContainsKey(Bone), "售罄后自动补货");
            Check(!YuxuMarketRules.TryBuy(p, seller, Bone, 1, 3, 100, out _), "售罄仍可重复购买");
            var ordinaryTrade = new TradePanel(); AddChild(ordinaryTrade);
            var emptySectMember = Npc("empty_sect_member"); ordinaryTrade.show_for(p, emptySectMember);
            Check(emptySectMember.trade_inventory.Count == 0, "当面交易为玉虚同门自动补货"); ordinaryTrade.close();
            var emptyOutsider = Npc("empty_outsider"); emptyOutsider.faction_id = "other_faction"; ordinaryTrade.show_for(p, emptyOutsider);
            Check(emptyOutsider.trade_inventory.Count > 0, "非玉虚人物原有当面交易库存受到影响"); ordinaryTrade.close(); ordinaryTrade.QueueFree();
            Check(h.yuxu_market_receipts.Count == 2 && seller.action_history.Any(x => x.action_id == "yuxu_market_sale"), "真实成交未记入人物与账目");
            Check(YuxuMarketRules.TryList(p, Herb, 1, 2, 100, out _), "存档样本上架失败");
            var savePath = "user://yuxu_market_smoke_" + Guid.NewGuid().ToString("N") + ".tres";
            Check(ResourceSaver.Save((HumanData)h.Duplicate(true), savePath) == Error.Ok, "坊市存档失败");
            var loaded = ResourceLoader.Load<HumanData>(savePath, cacheMode: ResourceLoader.CacheMode.Ignore);
            Check(loaded is not null && loaded.yuxu_market_lease_end_hour == 770 && loaded.yuxu_market_receipts.Count == 2 &&
                loaded.yuxu_market_listings.Count == 1 && loaded.yuxu_market_listings[0].GetValueOrDefault("quantity", 0).AsInt32() == 1, "坊市存档读回错误");
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(savePath));
            Check(YuxuMarketRules.TryClose(h, 200, out _) && !YuxuMarketRules.Active(h, 200) && h.yuxu_market_listings.Count == 1, "结束摊位丢失货物");
            Check(!YuxuMarketRules.TryList(p, Herb, 1, 2, 770, out _) && YuxuMarketRules.TryWithdraw(p, h.yuxu_market_listings[0]["id"].AsString(), out _), "到期后不能安全取回未售货物");

            var edgePlayer = Player(); var edge = (HumanData)edgePlayer.entity_data!;
            Check(edgePlayer.add_item_to_inventory(Stone) && edgePlayer.add_item_to_inventory(Herb) && YuxuMarketRules.TryRent(edgePlayer, 0, out _) &&
                YuxuMarketRules.TryList(edgePlayer, Herb, 1, 2, 0, out _), "月界租约夹具失败");
            var edgeBuyer = Npc("edge_buyer"); edgeBuyer.short_term_goal = "与当地人换取一件有用的物品。"; edgeBuyer.resources["灵石"] = 2;
            Check(YuxuMarketRules.TryAutomaticSale(edge, edgeBuyer, 1) && !YuxuMarketRules.Active(edge, 720), "到期月界的最后一期处理错误");

            var latePlayer = Player(); var late = (HumanData)latePlayer.entity_data!;
            Check(latePlayer.add_item_to_inventory(Stone) && latePlayer.add_item_to_inventory(Herb) && YuxuMarketRules.TryRent(latePlayer, 50, out _) &&
                YuxuMarketRules.TryList(latePlayer, Herb, 1, 2, 720, out _), "月界新挂牌夹具失败");
            var lateBuyer = Npc("late_buyer"); lateBuyer.short_term_goal = "与当地人换取一件有用的物品。"; lateBuyer.resources["灵石"] = 2;
            Check(!YuxuMarketRules.TryAutomaticSale(late, lateBuyer, 1) && late.yuxu_market_pending_stones == 0, "月界新挂牌被追溯成交");

            var full = Player(); var fullHuman = (HumanData)full.entity_data!;
            int fillCount = full.get_inventory_free_slot_count();
            Check(fillCount > 3 && full.add_item_to_inventory(Stone, fillCount), $"满包灵石夹具失败，剩余空间 {fillCount}，已有物品 {full.get_inventory_slot_count()}");
            fullHuman.yuxu_market_listings.Add(new D { ["id"] = "full_listing", ["item_path"] = Herb,
                ["quantity"] = 1, ["unit_price"] = 2, ["listed_hour"] = 0 });
            fullHuman.yuxu_market_pending_stones = 2; var fullListing = fullHuman.yuxu_market_listings[0]["id"].AsString();
            Check(!YuxuMarketRules.TryClaim(full, out _) && fullHuman.yuxu_market_pending_stones == 2, "满包领取损失所得");
            Check(!YuxuMarketRules.TryWithdraw(full, fullListing, out _) && fullHuman.yuxu_market_listings.Count == 1, "满包撤货损失物品");
            var fullSeller = Npc("full_seller"); fullSeller.resources["灵石"] = 1; fullSeller.trade_inventory[Bone] = 1;
            int fullStones = full.get_inventory_total_count(Stone);
            Check(!YuxuMarketRules.TryBuy(full, fullSeller, Bone, 1, 3, 0, out _) && full.get_inventory_total_count(Stone) == fullStones &&
                fullSeller.get_resource("灵石") == 1 && fullSeller.trade_inventory[Bone].AsInt32() == 1, "满包购买改动双方资产");

            var cyclePlayer = Player(); var cycleHuman = (HumanData)cyclePlayer.entity_data!;
            Check(cyclePlayer.add_item_to_inventory(Stone) && cyclePlayer.add_item_to_inventory(Herb) && YuxuMarketRules.TryRent(cyclePlayer, 0, out _) &&
                YuxuMarketRules.TryList(cyclePlayer, Herb, 1, 2, 0, out _), "真实月结夹具失败");
            var seedMethod = typeof(NpcWorldManager).GetMethod("stable_seed", BindingFlags.Static | BindingFlags.NonPublic)!;
            string workerId = Enumerable.Range(0, 100).Select(i => "market_worker_" + i).First(id =>
            {
                var seed = (ulong)seedMethod.Invoke(null, [1, id, "action"])!;
                var random = new RandomNumberGenerator { Seed = seed }; var roll = random.RandiRange(0, 99); return roll >= 28 && roll < 53;
            });
            var worker = Npc(workerId); worker.short_term_goal = "与当地人换取一件有用的物品。"; worker.resources["灵石"] = 2;
            var monthly = new NpcWorldManager(); AddChild(monthly); monthly.npcs.Add(worker); monthly.set_player_data(cycleHuman); monthly.set_simulation_enabled(true);
            monthly.simulate_until_month(1);
            Check(cycleHuman.yuxu_market_pending_stones == 2 && worker.get_carried_item_count(Herb) == 1 &&
                worker.action_history.Any(x => x.action_id == "yuxu_market_purchase") && worker.action_history.All(x => x.action_id != "work"),
                "真实月度工作分支未成交或重复营生");
            monthly.simulate_until_month(1); Check(cycleHuman.yuxu_market_pending_stones == 2, "真实月结重放重复成交"); monthly.QueueFree(); await Frames();

            var uiPlayer = Player(); Check(uiPlayer.add_item_to_inventory(Stone, 10) && uiPlayer.add_item_to_inventory(Herb, 2), "界面夹具失败");
            var uiSeller = Npc("ui_seller"); uiSeller.trade_inventory[Bone] = 2; uiSeller.resources["灵石"] = 1;
            var world = new NpcWorldManager(); AddChild(world); world.npcs.Add(uiSeller);
            var panel = new YuxuMarketPanel(); AddChild(panel); panel.Open(uiPlayer, world, () => true); await Frames(8);
            Check(panel.IsVisibleInTree() && panel.FindChild("MarketOffer", true, false) is Button, "原生界面没有显示真实摊货");
            await Capture("实机_逛坊_1920");
            Field<Button>(panel, "_buy").EmitSignal(BaseButton.SignalName.Pressed); world.npcs.Remove(uiSeller);
            panel.FindChild("ConfirmMarketAction", true, false)!.EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            Check(uiPlayer.get_inventory_total_count(Stone) == 10 && uiSeller.trade_inventory[Bone].AsInt32() == 2, "离开名单的摊主仍能成交");
            world.npcs.Add(uiSeller); panel.Refresh();
            Field<Button>(panel, "_stallTab").EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            Field<Button>(panel, "_rent").EmitSignal(BaseButton.SignalName.Pressed); panel.FindChild("ConfirmMarketAction", true, false)!.EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            Check(((HumanData)uiPlayer.entity_data!).yuxu_market_lease_end_hour > 0 && uiPlayer.get_inventory_total_count(Stone) == 9, "原生租摊按钮未扣费");
            Field<Button>(panel, "_list").EmitSignal(BaseButton.SignalName.Pressed); panel.FindChild("ConfirmMarketAction", true, false)!.EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            Check(((HumanData)uiPlayer.entity_data!).yuxu_market_listings.Count == 1 && uiPlayer.get_inventory_total_count(Herb) == 1, "原生上架按钮未转移物品");
            await Capture("实机_我的摊位_1920");
            bool returned = false; panel.BackRequested += () => { returned = true; panel.Hide(); };
            Check(panel.HandleBack() && returned && !panel.Visible, "原生返回未关闭坊市");
            panel.QueueFree(); world.QueueFree(); await Frames();
            await CheckMainSceneEntry();
            GD.Print($"YUXU_MARKET PASS: {_checks} checks; real inventory, price, rent, live NPC assets, sale, expiry, save and native panel"); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError("YUXU_MARKET FAIL: " + e); GetTree().Quit(1); }
    }
}
