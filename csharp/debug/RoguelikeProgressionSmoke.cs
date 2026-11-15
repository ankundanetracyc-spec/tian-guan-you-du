using Godot;
using System;
using System.Linq;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

/// <summary>Exercises isolated runs without creating or changing a player save.</summary>
public partial class RoguelikeProgressionSmoke : Node
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public override async void _Ready()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var instance = CheckLayouts();
            CheckManualsAndCultivation();
            CheckPastProgressRecovery(instance);
            CheckQuietRoom(instance);
            CheckShop(instance);
            CheckSaveRoundtrip(instance);
            await CheckDirectArrival(instance);
            GD.Print("ROGUELIKE_PROGRESSION PASS: layouts, encounters, zero stamina travel/gathering, three manuals, cultivation, quiet room, market and direct interaction");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError("ROGUELIKE_PROGRESSION: " + error);
            GetTree().Quit(1);
        }
    }

    private static GDictionary CheckLayouts()
    {
        GDictionary? first = null;
        for (var index = 0; index < 12; index++)
        {
            var instance = DynamicDungeonExpedition.CreateRoguelikeInstance($"rogue-smoke-{index}", $"rogue-smoke-seed-{index}", 0, "sword");
            first ??= instance;
            var nodes = DynamicDungeonExpedition.GetNodes(instance);
            Check(nodes.Count(node => node.ContentType == "manual") >= 9, "静室层缺失。");
            Check(nodes.Count(node => node.ContentType == "shop") == 5, "五处商栈未生成。");
            foreach (var node in nodes)
            {
                Check(DynamicDungeonExpedition.MovementStaminaCost(instance, node.Id) == 0, "肉鸽行进应免精力。");
                var actions = DynamicDungeonExpedition.AvailableActions(instance, node.Id, new GDictionary());
                if (node.ContentType == "manual") Check(actions.Count == 1 && actions[0].Id == "rogue_meditation", "静室没有可执行行动。");
                if (node.ContentType == "shop") Check(actions.Count == 1 && actions[0].Id == "rogue_shop", "商栈没有可执行行动。");
                if (node.ContentType == "cultivation") Check(actions.Count == 1 && actions[0].Id.StartsWith("rogue_cultivation_", StringComparison.Ordinal), "修行关隘没有可执行行动。");
                if (node.ContentType == DynamicDungeonExpedition.ResourceNodeType)
                    Check(actions.Count == 1 && actions[0].Stamina == 0 && actions[0].ItemPath.Length > 0 && ResourceLoader.Exists(actions[0].ItemPath), "采集行动、资源或精力费用错误。");
                if (node.ContentType is DynamicDungeonExpedition.BattleNodeType or DynamicDungeonExpedition.BossNodeType)
                    Check(actions.Count == 1 && actions[0].Battle && actions[0].EncounterIdentity.Length > 0, "战斗节点缺少遭遇。");
                if (node.ContentType != DynamicDungeonExpedition.ResourceNodeType)
                    Check(node.ResourceNodeId.Length == 0, "节点保留了无关资源。");
            }
        }

        var old = first!.Duplicate(true);
        old["instance_id"] = "rogue-smoke-old-nest";
        var oldNest = DynamicDungeonExpedition.GetNodes(old).First(node => node.Feature == "beast_nest");
        var snapshot = old["map_snapshot"].AsGodotDictionary();
        foreach (var value in snapshot["nodes"].AsGodotArray())
        {
            var node = value.AsGodotDictionary();
            if (node["id"].AsString() == oldNest.Id) node["resource_id"] = "common_herb";
        }
        old["instance_id"] = "rogue-smoke-old-nest-mutated";
        var preserved = new GArray();
        foreach (var value in old["beasts"].AsGodotArray())
            if (value.AsGodotDictionary().GetValueOrDefault("node", "").AsString() != oldNest.Id) preserved.Add(value);
        old["beasts"] = preserved;
        DynamicDungeonExpedition.SynchronizeInstanceEcology(old, 0);
        Check(DynamicDungeonExpedition.AvailableActions(old, oldNest.Id, new GDictionary()).Single().Battle,
            "旧档兽巢缺失遭遇后应能恢复战斗。");
        var ordinaryState = DynamicDungeonExpedition.CreateEmpty(0);
        DynamicDungeonExpedition.SynchronizeTowerOpportunities(ordinaryState, 0);
        var ordinary = ordinaryState["instances"].AsGodotArray()[0].AsGodotDictionary();
        foreach (var node in DynamicDungeonExpedition.GetNodes(ordinary))
        {
            var expected = node.ContentType switch
            {
                DynamicDungeonExpedition.BattleNodeType => 3f,
                DynamicDungeonExpedition.ResourceNodeType => 2f,
                DynamicDungeonExpedition.RestNodeType => .5f,
                "exit" => 1f,
                _ => 0f,
            };
            Check(DynamicDungeonExpedition.MovementStaminaCost(ordinary, node.Id) == expected, "普通爬塔的移动精力费用发生变化。");
        }
        return first;
    }

    private Character NewPlayer()
    {
        var human = new HumanData
        {
            entity_name = "秘境验收人物",
            cultivation_realm = "云息一层",
            spirit_strands = 6,
            spirit_root_type = (int)GongfaResource.Wuxing.METAL_WOOD_WATER,
            spirit_root_name = "三灵根",
            root_weights = [34, 33, 33, 0, 0],
            main_gongfa_path = RoguelikeManualRules.MainBookPath,
            current_world_stamina = 100,
            max_world_stamina = 100,
        };
        human.learned_gongfa_paths.Add(RoguelikeManualRules.MainBookPath);
        var player = new Character { entity_data = human, ProcessMode = ProcessModeEnum.Disabled };
        AddChild(player);
        return player;
    }

    private void CheckManualsAndCultivation()
    {
        foreach (var role in new[] { "sword", "alchemy", "forging" })
        {
            var player = NewPlayer();
            Check(RoguelikeManualRules.CanStudyLoadout(role, out _) && RoguelikeManualRules.EnsureBooks(player, role, out _), $"{role}的秘籍未发放。");
            var human = (HumanData)player.entity_data!;
            var slots = human.max_sub_slots;
            Check(RoguelikeManualRules.EnsureBooks(player, role, out _) && human.max_sub_slots == slots, "重复检查秘籍改变了辅修位。");
            foreach (var book in RoguelikeManualRules.BookPaths(role))
                Check(player.get_inventory_total_count(RoguelikeManualRules.ItemPathForBook(book)) == 1, "秘籍发放数量不正确。");
            if (role == "sword")
            {
                const string bagPath = "res://tools/fixtures/storage_bag_narrow.tres";
                Check(player.add_item_to_inventory(bagPath), "储物袋场景准备失败。");
                var bag = player.get_storage_bags().Single();
                var sharedPath = RoguelikeManualRules.ItemPathForBook(RoguelikeManualRules.SharedBookPath);
                var book = player.get_spatial_inventory().Single(entry => entry.ItemPath == sharedPath);
                Check(player.try_transfer_storage_item(book.InstanceId, bag.InstanceId, out _) &&
                    RoguelikeManualRules.EnsureBooks(player, role, out _) &&
                    player.get_accessible_spatial_inventory().Count(entry => entry.ItemPath == sharedPath) == 1,
                    "秘籍放入储物袋后不应再次发放副本。");
            }
            Check(RoguelikeManualRules.StudyAllThroughStage(player, role, 1) == 3 &&
                RoguelikeManualRules.StudyAllThroughStage(player, role, 1) == 0, "首卷或重复研读结算错误。");
            for (var layer = 3; layer <= 27; layer += 3)
                Check(player.apply_roguelike_cultivation(layer, "physique", out _), $"第{layer}层无法晋升。");
            Check(player.cultivation_realm == "云息十层", "第27层未达到云息十层。");
            Check(RoguelikeManualRules.StudyAllThroughStage(player, role, 4) == 9, "终局未补齐跳过的卷章。");
            var physique = player.base_physique;
            Check(RoguelikeManualRules.StudyAllThroughStage(player, role, 4) == 0 && player.base_physique == physique,
                "完整研读后再次结算不应重复奖励。");
            foreach (var book in RoguelikeManualRules.BookPaths(role))
                Check(RoguelikeManualRules.LearnedChapterCount(player, book) == 4, "秘籍未完成四卷。");
            Check(human.sub_gongfa_paths.Contains(RoguelikeManualRules.SharedBookPath) &&
                human.sub_gongfa_paths.Contains(RoguelikeManualRules.BookPaths(role)[2]), "已研读秘籍未接入辅修。");
            var restored = new Character { entity_data = (HumanData)player.entity_data!.Duplicate(true), ProcessMode = ProcessModeEnum.Disabled };
            AddChild(restored);
            foreach (var book in RoguelikeManualRules.BookPaths(role))
                Check(RoguelikeManualRules.LearnedChapterCount(restored, book) == 4, "保存快照丢失秘籍进度。");
            restored.QueueFree();
            player.QueueFree();
        }

        var full = NewPlayer();
        Check(full.add_item_to_inventory(RoguelikeShopRules.StonePath, full.get_inventory_free_capacity() - 1), "满包场景准备失败。");
        Check(!RoguelikeManualRules.EnsureBooks(full, "sword", out _), "满包不应部分发放秘籍。");
        Check(RoguelikeManualRules.BookPaths("sword").All(book => full.get_inventory_total_count(RoguelikeManualRules.ItemPathForBook(book)) == 0), "满包发放产生了部分物品。");
        Check(RoguelikeManualRules.StudyAllThroughStage(full, "sword", 4) == 12, "满包不应阻断秘境研读。");
        full.QueueFree();
    }

    private void CheckQuietRoom(GDictionary instance)
    {
        var player = NewPlayer();
        Check(RoguelikeManualRules.EnsureBooks(player, "sword", out _), "静室验收人物缺少秘籍。");
        var changed = 0;
        var panel = RoguelikeMeditationPanel.ShowFor(this, player, instance, 1, "sword", () => changed++);
        var rooms = instance["roguelike_quiet_rooms"].AsGodotDictionary();
        Check(rooms.ContainsKey("1"), "首次进入静室未初始化状态。");
        var before = player.base_physique;
        FindButton(panel, "体魄 +1").EmitSignal(Button.SignalName.Pressed);
        Check(player.base_physique == before + 1 && rooms["1"].AsGodotDictionary()["trained"].AsBool(), "静室修习没有记账。");
        FindButton(panel, "体魄 +1").EmitSignal(Button.SignalName.Pressed);
        Check(player.base_physique == before + 1 && changed == 1, "静室修习可重复领取。");
        FindButton(panel, "全部研读至本层").EmitSignal(Button.SignalName.Pressed);
        Check(changed == 2 && RoguelikeManualRules.BookPaths("sword").All(book => RoguelikeManualRules.LearnedChapterCount(player, book) == 1),
            "静室一键研读未推进全部秘籍。");
        panel.QueueFree();
        player.QueueFree();
    }

    private void CheckPastProgressRecovery(GDictionary original)
    {
        var player = NewPlayer();
        for (var layer = 3; layer <= 24; layer += 3)
            Check(player.apply_roguelike_cultivation(layer, "physique", out _), "旧档修行准备失败。");
        var instance = original.Duplicate(true);
        var nodes = DynamicDungeonExpedition.GetNodes(instance);
        var route = DynamicDungeonExpedition.GetRoute(instance);
        var visited = new GArray { DynamicDungeonExpedition.EntryNodeId };
        foreach (var layer in new[] { 3, 6, 9, 12, 15, 18, 21, 24, 27 })
            visited.Add(nodes.First(node => node.ContentType == "cultivation" &&
                DynamicDungeonExpedition.RoguelikeLayerForNodeId(node.Id) == layer).Id);
        route["visited"] = visited;
        route["current"] = nodes.First(node => DynamicDungeonExpedition.RoguelikeLayerForNodeId(node.Id) == 28).Id;
        DynamicDungeonExpedition.StoreRoute(instance, route);
        Check(RoguelikeManualRules.ReconcilePastProgress(player, instance) && player.cultivation_realm == "云息十层",
            "旧档未按走过的第27层路线补齐境界。");
        Check(RoguelikeManualRules.BookPaths("sword").All(book => RoguelikeManualRules.LearnedChapterCount(player, book) == 4),
            "已越过第27层的旧档未补齐三本秘籍。");
        var physique = player.base_physique;
        var intelligence = player.base_intelligence;
        var soul = player.base_soul;
        Check(!RoguelikeManualRules.ReconcilePastProgress(player, instance) && player.base_physique == physique &&
            player.base_intelligence == intelligence && player.base_soul == soul, "旧档补记重复发放成长。");
        player.QueueFree();
    }

    private void CheckShop(GDictionary instance)
    {
        var player = NewPlayer();
        var shop = DynamicDungeonExpedition.GetNodes(instance).First(node => node.ContentType == "shop" && DynamicDungeonExpedition.RoguelikeLayerForNodeId(node.Id) == 4);
        var material = "res://resources/entities/items/苦苔根.tres";
        Check(player.add_item_to_inventory(material, 6), "商栈测试材料未入囊。");
        Check(RoguelikeShopRules.TrySell(player, instance, shop.Id, material, 3, out _) &&
            player.get_inventory_total_count(RoguelikeShopRules.StonePath) == 6 &&
            RoguelikeShopRules.GetBuybackRemaining(instance, shop.Id) == 6, "出售材料结算错误。");
        var pill = AlchemyProductCatalog.Path("huichun", 0);
        Check(RoguelikeShopRules.TryBuy(player, instance, shop.Id, pill, 1, out _) &&
            player.get_inventory_total_count(pill) == 1 && player.get_inventory_total_count(RoguelikeShopRules.StonePath) == 0, "购买丹药结算错误。");
        Check(!RoguelikeShopRules.TryBuy(player, instance, shop.Id, pill, 1, out _) &&
            player.get_inventory_total_count(pill) == 1, "灵石不足时不应获得商品。");
        var restored = instance.Duplicate(true);
        Check(RoguelikeShopRules.GetBuybackRemaining(restored, shop.Id) == 6 &&
            RoguelikeShopRules.GetOffers(restored, shop.Id).Single(offer => offer.ItemPath == pill).Quantity == 1,
            "商栈库存或收购余额没有进入快照。");
        var artifactShop = DynamicDungeonExpedition.GetNodes(instance).First(node => node.ContentType == "shop" && DynamicDungeonExpedition.RoguelikeLayerForNodeId(node.Id) == 22);
        Check(player.add_item_to_inventory(RoguelikeShopRules.StonePath, RoguelikeShopRules.QingheSwordPrice), "法器购买测试灵石未入囊。");
        Check(RoguelikeShopRules.TryBuy(player, instance, artifactShop.Id, RoguelikeShopRules.QingheOfferPath, 1, out _) &&
            player.artifact_inventory.Count == 1 && player.get_inventory_total_count(RoguelikeShopRules.StonePath) == 0, "法器未以实例入囊或未扣灵石。");
        Check(!RoguelikeShopRules.TryBuy(player, instance, artifactShop.Id, RoguelikeShopRules.QingheOfferPath, 1, out _), "法器库存不可重复购买。");
        player.QueueFree();
    }

    private async System.Threading.Tasks.Task CheckDirectArrival(GDictionary instance)
    {
        var room = DynamicDungeonExpedition.GetNodes(instance).First(node => node.ContentType == "manual");
        var route = DynamicDungeonExpedition.GetRoute(instance);
        route["current"] = room.Id;
        route["inside"] = true;
        DynamicDungeonExpedition.StoreRoute(instance, route);
        var panel = new EastWoodsDungeonPanel();
        AddChild(panel);
        panel.configure_dynamic_instance(instance);
        var received = 0;
        panel.ActionRequested += (node, action) => { if (node == room.Id && action == "rogue_meditation") received++; };
        panel.show_dungeon();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Check(received == 1, "抵达静室没有直接互动。");
        var button = panel.FindChild("Node_" + room.Id, true, false) as Button;
        Check(button is not null, "静室节点按钮不存在。");
        button!.EmitSignal(Button.SignalName.Pressed);
        Check(received == 2, "点击当前节点无法再次互动。");
        var shop = DynamicDungeonExpedition.GetNodes(instance).First(node => node.ContentType == "shop");
        var resource = DynamicDungeonExpedition.GetNodes(instance).First(node => node.ContentType == DynamicDungeonExpedition.ResourceNodeType);
        var shopIcon = panel.FindChild("Node_" + shop.Id, true, false)?.FindChild("RoguelikeNodeIcon", true, false) as TextureRect;
        var resourceIcon = panel.FindChild("Node_" + resource.Id, true, false)?.FindChild("RoguelikeNodeIcon", true, false) as TextureRect;
        Check(shopIcon?.Texture is AtlasTexture shopAtlas && resourceIcon?.Texture is AtlasTexture resourceAtlas &&
            shopAtlas.Region.Position != resourceAtlas.Region.Position, "商栈与普通资源使用了同一地图图标。");
        DynamicDungeonExpedition.StoreRoute(instance, panel.get_route_state());
        panel.QueueFree();

        var restored = new EastWoodsDungeonPanel();
        AddChild(restored);
        restored.configure_dynamic_instance(instance.Duplicate(true));
        var resumed = 0;
        restored.ActionRequested += (_, _) => resumed++;
        restored.show_dungeon();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Check(resumed == 0, "重新载入不应自动重复已触发的节点。");
        var restoredButton = restored.FindChild("Node_" + room.Id, true, false) as Button;
        Check(restoredButton is not null, "读档后节点按钮不存在。");
        restoredButton!.EmitSignal(Button.SignalName.Pressed);
        Check(resumed == 1, "读档后应仍可主动再次互动。");
        restored.QueueFree();
    }

    private void CheckSaveRoundtrip(GDictionary instance)
    {
        var file = "res://outputs/roguelike-roundtrip-" + Guid.NewGuid().ToString("N") + ".tres";
        var state = DynamicDungeonExpedition.CreateEmpty(0);
        state["instances"] = new GArray { instance };
        var player = NewPlayer();
        Check(RoguelikeManualRules.EnsureBooks(player, "sword", out _) &&
            RoguelikeManualRules.StudyAllThroughStage(player, "sword", 4) == 12, "存档研读准备失败。");
        var save = new PersistentGameSave { game_mode = "roguelike", dynamic_dungeon_state = state,
            player_data = (HumanData)player.entity_data!.Duplicate(true) };
        try
        {
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://outputs"));
            Check(ResourceSaver.Save(save, file) == Error.Ok, "肉鸽存档快照写入失败。");
            var loaded = ResourceLoader.Load<PersistentGameSave>(file, cacheMode: ResourceLoader.CacheMode.Ignore);
            Check(loaded is not null && loaded.game_mode == "roguelike", "肉鸽存档读取失败。");
            var restoredPlayer = new Character { entity_data = loaded!.player_data, ProcessMode = ProcessModeEnum.Disabled };
            AddChild(restoredPlayer);
            Check(RoguelikeManualRules.BookPaths("sword").All(book => RoguelikeManualRules.LearnedChapterCount(restoredPlayer, book) == 4),
                "读档丢失秘籍卷章。");
            restoredPlayer.QueueFree();
            var savedRun = loaded!.dynamic_dungeon_state["instances"].AsGodotArray()[0].AsGodotDictionary();
            var rooms = savedRun["roguelike_quiet_rooms"].AsGodotDictionary();
            Check(rooms["1"].AsGodotDictionary()["trained"].AsBool(), "读档丢失静室修习状态。");
            var shop = DynamicDungeonExpedition.GetNodes(savedRun).First(node => node.ContentType == "shop" &&
                DynamicDungeonExpedition.RoguelikeLayerForNodeId(node.Id) == 4);
            Check(RoguelikeShopRules.GetBuybackRemaining(savedRun, shop.Id) == 6 &&
                RoguelikeShopRules.GetOffers(savedRun, shop.Id).Single(offer => offer.ItemPath == AlchemyProductCatalog.Path("huichun", 0)).Quantity == 1,
                "读档丢失商栈库存或收购余额。");
        }
        finally
        {
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(file));
            player.QueueFree();
        }
    }

    private static Button FindButton(Node root, string label)
    {
        if (root is Button button && button.Text == label) return button;
        foreach (var child in root.GetChildren())
        {
            try { return FindButton(child, label); }
            catch (InvalidOperationException) { }
        }
        throw new InvalidOperationException("没有找到按钮：" + label);
    }
}
