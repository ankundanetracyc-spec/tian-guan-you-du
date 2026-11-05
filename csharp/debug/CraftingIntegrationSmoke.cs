using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;

public partial class CraftingIntegrationSmoke : Node
{
    private int _checks;
    private readonly List<Character> _actors = [];
    private void Check(bool ok, string label) { if (!ok) throw new Exception(label); GD.Print("PASS " + label); _checks++; }
    private Character Actor(ProcessForgingTrial model, ProcessForgingTrial.Draft draft, int copies = 1)
    {
        var actor = ForgingSmokeFixtures.Actor(model, draft, copies); _actors.Add(actor); return actor;
    }
    public override void _Ready() => CallDeferred(nameof(Run));
    private async void Run()
    {
        try
        {
            var model = new ProcessForgingTrial(); var draft = ForgingSmokeFixtures.SmallDraft(model, 0);
            var owner = Actor(model, draft); var data = (HumanData)owner.entity_data!;
            data.used_spirit_perception = 7;
            var stock = owner.GetForgingStock(model);
            Check(owner.TryForge(model, draft, out var result) && result.Success, "真实库存炼器成功");
            Check(result.Required.All(p => owner.GetForgingStock(model)[p.Key] == stock[p.Key] - p.Value), "同材跨岗位只扣整炉合计");
            Check(owner.current_world_stamina == 100 - result.Energy && result.Hours == (int)Math.Ceiling(result.Units / 3d), "正式工时与精力采用确认参数");
            Check(data.artifact_inventory.Count == 1 && owner.used_spirit_perception == 7, "成器入包且保留其他灵识占用");
            Check(ForgedArtifactInventory.TryRead(data.artifact_inventory[0], out var archived) && archived.InstanceId.StartsWith("forged-"), "正式唯一实例带完整快照");
            ForgingSmokeFixtures.Restock(owner, model, draft);
            Check(owner.TryForge(model, draft, out _) && data.artifact_inventory[0].instance_id != data.artifact_inventory[1].instance_id, "重新投料可再炼且实例不同");
            var tired = Actor(model, draft); ((HumanData)tired.entity_data!).current_world_stamina = 0;
            Check(!tired.TryForge(model, draft, out _) && tired.GetForgingStock(model).SequenceEqual(stock), "精力不足零扣料");
            var limited = Actor(model, draft); ((HumanData)limited.entity_data!).used_spirit_perception = 10000;
            Check(!limited.TryForge(model, draft, out _) && limited.current_world_stamina == 100, "其他占用导致灵识不足时拒绝");
            var missing = Actor(model, draft, 0);
            Check(!missing.TryForge(model, draft, out _) && missing.current_world_stamina == 100, "缺材料不扣精力");
            var invalid = model.ExampleDraft(1); invalid.Inputs.RemoveAll(i => i.Role == "主体");
            Check(!owner.TryForge(model, invalid, out _), "非法岗位拒绝开炼");
            var tightDraft = new ProcessForgingTrial.Draft { Proficiency = 12, Inputs = [
                new() { Role = "主体", Material = "铜髓矿" }, new() { Role = "承接", Material = "青木芯" }] };
            var full = Actor(model, tightDraft);
            var fullWood = full.get_spatial_inventory().Single(i => i.ItemPath == model.Materials["青木芯"].ResourcePath);
            Check(full.move_spatial_item(fullWood.InstanceId, "waist", 0, 0, 1), "满包保护夹具按真实尺寸摆放木芯");
            ForgingSmokeFixtures.FillWithStones(full); var fullStock = full.GetForgingStock(model);
            Check(!full.TryForge(model, tightDraft, out _) && full.current_world_stamina == 100 && full.GetForgingStock(model).SequenceEqual(fullStock), "满包碎片空间不足时不吞料");
            tightDraft.Inputs.Add(new() { Role = "质材", Material = "铜髓矿" });
            var freed = Actor(model, tightDraft);
            var entries = freed.get_spatial_inventory();
            var wood = entries.Single(i => i.ItemPath == model.Materials["青木芯"].ResourcePath);
            Check(freed.move_spatial_item(wood.InstanceId, "waist", 0, 0, 1), "扣料腾位夹具合法旋转木芯");
            var copper = entries.Where(i => i.ItemPath == model.Materials["铜髓矿"].ResourcePath).ToArray();
            Check(freed.move_spatial_item(copper[0].InstanceId, "back", 0, 0, 0) && freed.move_spatial_item(copper[1].InstanceId, "back", 0, 3, 0), "扣料后可腾出连续3乘6空间");
            ForgingSmokeFixtures.FillWithStones(freed);
            Check(freed.TryForge(model, tightDraft, out var freedResult) && freedResult.Success && freed.get_spatial_inventory().All(i => i.IsPlaced), "满包可使用本炉扣料腾出的实际空间");
            const string savePath = "user://tests/crafting_integration.tres";
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("user://tests"));
            Check(ResourceSaver.Save(new PersistentGameSave { player_data = data }, savePath) == Error.Ok, "炼成器写入正式保存容器");
            var restored = ResourceLoader.Load<PersistentGameSave>(savePath, cacheMode: ResourceLoader.CacheMode.Ignore)!;
            Check(restored.player_data!.artifact_inventory.Count == 2 && ForgedArtifactInventory.TryRead(restored.player_data.artifact_inventory[0], out _), "存档读回保留器物快照");
            Check(restored.player_data.forging_records.Count == 2, "炼器过往记录随人物存档读回");
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(savePath));
            data.alchemy_proficiency = 36;
            var browsingStock = owner.GetForgingStock(model); var browsingEnergy = owner.current_world_stamina;
            var panel = new BaigongPanel(); AddChild(panel); panel.show_for(owner);
            typeof(BaigongPanel).GetMethod("select_page", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(panel, ["丹药"]);
            Check(!panel.FindChildren("*", "Button", true, false).OfType<Button>().Any(b => b.Text.Contains("进入炼")), "百工丹药页只有资料，没有炼制入口");
            typeof(BaigongPanel).GetMethod("select_page", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(panel, ["法宝"]);
            Check(!panel.FindChildren("*", "Button", true, false).OfType<Button>().Any(b => b.Text.Contains("进入炼")), "百工法宝页只有记录，没有炼制入口");
            Check(owner.current_world_stamina == browsingEnergy && owner.GetForgingStock(model).SequenceEqual(browsingStock), "查阅百工不消耗材料或精力");
            var hidden = BindingFlags.NonPublic | BindingFlags.Instance;
            var main = new EastWoodsActionHarness(); AddChild(main);
            var sceneLayer = new CanvasLayer { Name = "SceneLayer" }; main.AddChild(sceneLayer);
            var overview = GD.Load<PackedScene>("res://tscn/world/bai_chuan/YuxuXianzongOverview.tscn").Instantiate<Control>();
            overview.Name = "YuxuXianzongOverview"; sceneLayer.AddChild(overview);
            var facilities = new LocationCraftingPanel(); main.AddChild(facilities);
            typeof(MainScene).GetField("_locationCrafting", hidden)!.SetValue(main, facilities);
            typeof(MainScene).GetField("player", hidden)!.SetValue(main, owner);
            data.sect_affiliation_id = "yuxu_xianzong"; data.sect_current_rank = "外门弟子";
            var action = typeof(MainScene).GetMethod("on_yuxu_life_action_requested", hidden)!;
            action.Invoke(main, ["tiangong_alchemy", "tiangong"]);
            Check(!facilities.Visible, "不在天工峰不能打开丹房");
            overview.Show(); overview.Call("_on_peak_pressed", "tiangong");
            typeof(MainScene).GetField("_player_current_yuxu_peak_id", hidden)!.SetValue(main, "tiangong");
            var peak = overview.GetNode<Control>("PeakInterior");
            peak.Call("set_sect_duty_context", true, "");
            var actionButtons = peak.Get("_action_buttons").AsGodotDictionary();
            Check(((Button)actionButtons["tiangong_alchemy"].AsGodotObject()).Visible && ((Button)actionButtons["tiangong_forging"].AsGodotObject()).Visible,
                "天工峰显示丹房与器作地点入口");
            data.sect_affiliation_id = "";
            action.Invoke(main, ["tiangong_alchemy", "tiangong"]);
            Check(!facilities.Visible, "沿用宗门身份限制"); data.sect_affiliation_id = "yuxu_xianzong";
            action.Invoke(main, ["tiangong_alchemy", "tiangong"]);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var room = facilities.GetChildren().OfType<AlchemySlotBrewPreviewScene>().Single();
            Check(room.OwnerCharacter == owner, "地点丹房绑定当前人物");
            typeof(AlchemySlotBrewPreviewScene).GetMethod("ApplySampleRoute", hidden)!.Invoke(room, [AlchemyRecipeCatalog.Pills[0].SampleRoutes.Last(), false]);
            var inputs = (IReadOnlyList<AlchemySlotSimulation.Input>)typeof(AlchemySlotBrewPreviewScene).GetMethod("GetSimulationInputs", hidden)!.Invoke(room, null)!;
            var pillData = new HumanData { alchemy_proficiency = 36, max_spirit_perception = 10000, current_world_stamina = 100,
                max_health = 100, current_health = 100 };
            var alchemist = new Character { entity_data = pillData }; _actors.Add(alchemist);
            Check(alchemist.try_exchange_inventory(new Dictionary<string, int>(), inputs.ToDictionary(i => i.Path, i => i.Dose)), "炼丹投料实际摆入行囊");
            Check(alchemist.TryBrewAlchemy(inputs, out var brewed, out _) && brewed!.OutputCount > 0, "正式炼丹自动产量成功");
            Check(pillData.alchemy_records.Count == 1, "正式开炉新增一条炼丹记录");
            const string pillSave = "user://tests/crafting_alchemy_records.tres";
            ResourceSaver.Save(new PersistentGameSave { player_data = pillData }, pillSave);
            Check(ResourceLoader.Load<PersistentGameSave>(pillSave, cacheMode: ResourceLoader.CacheMode.Ignore)!.player_data!.alchemy_records.Count == 1,
                "炼丹过往记录随人物存档读回");
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(pillSave));
            var rewards = new Godot.Collections.Array();
            foreach (var group in brewed!.Pills.Where(p => p.Quality >= 20).GroupBy(p => AlchemyQualityRules.Tier(p.Quality)))
                rewards.Add(new Godot.Collections.Dictionary { ["item_path"] = AlchemyProductCatalog.Path(brewed.ProductId, group.Key), ["count"] = group.Count() });
            Check(rewards.All(entry => alchemist.get_inventory_total_count(entry.AsGodotDictionary()["item_path"].AsString()) == 0) &&
                inputs.All(i => alchemist.get_inventory_total_count(i.Path) == 0), "开炼只扣投料，成品等待领取");
            var claim = new ItemClaimPanel(); GetTree().Root.AddChild(claim);
            Check(claim.Present(alchemist, rewards, "炼丹所得"), "炼丹成果进入通用领取页");
            claim.FindChildren("ClaimAutoPlace", "Button", true, false).OfType<Button>().Single().EmitSignal(Button.SignalName.Pressed);
            typeof(ItemClaimPanel).GetMethod("claim_selected_items", hidden)!.Invoke(claim, null);
            Check(rewards.All(entry => alchemist.get_inventory_total_count(entry.AsGodotDictionary()["item_path"].AsString()) == entry.AsGodotDictionary()["count"].AsInt32()), "确认领取后丹药入包");
            typeof(AlchemySlotBrewPreviewScene).GetField("_brewing", hidden)!.SetValue(room, true);
            facilities.ReturnToLocation(); Check(!room.IsQueuedForDeletion() && facilities.Visible, "炼丹结算期间返回不会隐藏成果");
            typeof(AlchemySlotBrewPreviewScene).GetField("_brewing", hidden)!.SetValue(room, false);
            main.try_return_to_previous_page(); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(!facilities.Visible && peak.Visible, "返回关闭丹房并保留实际地点");
            action.Invoke(main, ["tiangong_forging", "tiangong"]);
            var workshop = facilities.GetChildren().OfType<ProcessForgingScene>().Single();
            var session = (ProcessForgingTrial.Session)typeof(ProcessForgingScene).GetField("_session", hidden)!.GetValue(workshop)!;
            Check(session.Stock.SequenceEqual(owner.GetForgingStock(model)), "游戏内工坊显示真实库存");
            typeof(ProcessForgingScene).GetMethod("Gallery", hidden)!.Invoke(workshop, null);
            var gallery = (Control)typeof(ProcessForgingScene).GetField("_overlay", hidden)!.GetValue(workshop)!;
            Check(gallery.FindChildren("ForgedArtifactItem_*", "Button", true, false).Count == data.artifact_inventory.Count, "重新进入器物册读取正式持有物");
            facilities.ReturnToLocation(); Check(!workshop.IsQueuedForDeletion(), "工坊返回优先关闭详情弹层");
            facilities.ReturnToLocation(); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(!facilities.Visible && peak.Visible, "器作返回天工峰"); main.QueueFree();
            panel.close(); Check(!panel.Visible, "再次返回关闭百工"); panel.QueueFree();
            var clockOwner = Actor(model, draft); AddChild(clockOwner);
            var clockData = (HumanData)clockOwner.entity_data!;
            clockData.forging_proficiency = draft.Proficiency; clockData.max_spirit_perception = 10000;
            clockData.current_world_stamina = 100;
            var time = GetNode<GameTimeMgr>("/root/GameTimeManager"); var before = time.get_precise_game_hours();
            Check(clockOwner.TryForge(model, draft, out var timed) && Math.Abs(time.get_precise_game_hours() - before - timed.Hours * 2) < .001,
                "树内正式炼器推进真实历法且时辰换算正确");
            // 连通真实配伍页面、开炼演出与领取按钮，避免只验算两个独立接口。
            Check(alchemist.try_exchange_inventory(new Dictionary<string, int>(), inputs.ToDictionary(i => i.Path, i => i.Dose)), "下一炉重新装入真实投料");
            pillData.base_alchemy_proficiency = 36; AddChild(alchemist);
            pillData.alchemy_proficiency = 36; pillData.max_spirit_perception = 10000; pillData.current_world_stamina = 100;
            var realFacilities = new LocationCraftingPanel(); AddChild(realFacilities); realFacilities.ShowFor(alchemist, true);
            var realRoom = realFacilities.GetChildren().OfType<AlchemySlotBrewPreviewScene>().Single();
            typeof(AlchemySlotBrewPreviewScene).GetMethod("ApplySampleRoute", hidden)!.Invoke(realRoom, [AlchemyRecipeCatalog.Pills[0].SampleRoutes.Last(), false]);
            typeof(AlchemySlotBrewPreviewScene).GetField("_simpleAlchemyPresentation", hidden)!.SetValue(realRoom, true);
            var oldStock = inputs.ToDictionary(i => i.Path, i => alchemist.get_inventory_total_count(i.Path));
            var oldPills = rewards.ToDictionary(e => e.AsGodotDictionary()["item_path"].AsString(),
                e => alchemist.get_inventory_total_count(e.AsGodotDictionary()["item_path"].AsString()));
            var oldTime = time.get_precise_game_hours();
            var brewTask = (System.Threading.Tasks.Task)typeof(AlchemySlotBrewPreviewScene).GetMethod("BrewWithPresentation", hidden)!.Invoke(realRoom, null)!;
            for (int frame = 0; frame < 600 && !brewTask.IsCompleted; frame++)
            {
                var bottle = realRoom.FindChildren("*", "Button", true, false).OfType<Button>()
                    .FirstOrDefault(b => b.Text == "确定 · 按丹效装瓶" && b.IsVisibleInTree());
                bottle?.EmitSignal(Button.SignalName.Pressed);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            Check(brewTask.IsCompleted, "真实炼丹鉴看后确认装瓶完成"); await brewTask;
            var realClaim = (ItemClaimPanel)typeof(AlchemySlotBrewPreviewScene).GetField("_alchemyClaim", hidden)!.GetValue(realRoom)!;
            Check(realClaim is not null && inputs.All(i => alchemist.get_inventory_total_count(i.Path) == oldStock[i.Path] - i.Dose),
                "真实炼丹页面开炼扣除行囊投料并打开领取页");
            Check(alchemist.current_world_stamina < 100 && time.get_precise_game_hours() > oldTime,
                "真实开炉同时扣精力并推进历法");
            Check(oldPills.All(p => alchemist.get_inventory_total_count(p.Key) == p.Value), "真实领取确认前不提前发丹");
            realClaim!.FindChildren("ClaimAutoPlace", "Button", true, false).OfType<Button>().Single().EmitSignal(Button.SignalName.Pressed);
            Check(oldPills.All(p => alchemist.get_inventory_total_count(p.Key) == p.Value), "自动放入仅预览，未确认前不发丹");
            realClaim.FindChildren("ClaimFooterPrimary", "Button", true, false).OfType<Button>().Single().EmitSignal(Button.SignalName.Pressed);
            Check(oldPills.All(p => alchemist.get_inventory_total_count(p.Key) > p.Value), "点击真实领取按钮后丹药进入人物行囊");
            realFacilities.ReturnToLocation(); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(!realFacilities.Visible, "真实领取完成后可以返回地点"); realFacilities.QueueFree();
            GD.Print("CRAFTING_INTEGRATION_OK " + _checks); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
        finally { foreach (var actor in _actors) actor.Free(); }
    }
}
