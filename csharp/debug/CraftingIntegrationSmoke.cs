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
    private Character Actor(ProcessForgingTrial model, ProcessForgingTrial.Draft draft, int copies = 3)
    {
        var required = draft.Inputs.GroupBy(i => i.Material).ToArray();
        var data = new HumanData { forging_proficiency = draft.Proficiency, current_world_stamina = 100,
            max_spirit_perception = 10000, current_health = 100, max_health = 100,
            inventory_item_paths = new(required.Select(g => model.Materials[g.Key].ResourcePath)),
            inventory_item_counts = new(required.Select(g => g.Sum(i => i.Quantity) * copies)),
            inventory_item_positions = new(Enumerable.Range(0, required.Length)) };
        var actor = new Character { entity_data = data }; _actors.Add(actor); return actor;
    }
    public override void _Ready() => CallDeferred(nameof(Run));
    private async void Run()
    {
        try
        {
            var model = new ProcessForgingTrial(); var draft = model.ExampleDraft(1);
            var owner = Actor(model, draft); var data = (HumanData)owner.entity_data!;
            data.used_spirit_perception = 7;
            var stock = owner.GetForgingStock(model);
            Check(owner.TryForge(model, draft, out var result) && result.Success, "真实库存炼器成功");
            Check(result.Required.All(p => owner.GetForgingStock(model)[p.Key] == stock[p.Key] - p.Value), "同材跨岗位只扣整炉合计");
            Check(owner.current_world_stamina == 100 - result.Energy && result.Hours == (int)Math.Ceiling(result.Units / 3d), "正式工时与精力采用确认参数");
            Check(data.artifact_inventory.Count == 1 && owner.used_spirit_perception == 7, "成器入包且保留其他灵识占用");
            Check(ForgedArtifactInventory.TryRead(data.artifact_inventory[0], out var archived) && archived.InstanceId.StartsWith("forged-"), "正式唯一实例带完整快照");
            Check(owner.TryForge(model, draft, out _) && data.artifact_inventory[0].instance_id != data.artifact_inventory[1].instance_id, "相同配伍可再炼且实例不同");
            var tired = Actor(model, draft); ((HumanData)tired.entity_data!).current_world_stamina = 0;
            Check(!tired.TryForge(model, draft, out _) && tired.GetForgingStock(model).SequenceEqual(stock), "精力不足零扣料");
            var limited = Actor(model, draft); ((HumanData)limited.entity_data!).used_spirit_perception = 10000;
            Check(!limited.TryForge(model, draft, out _) && limited.current_world_stamina == 100, "其他占用导致灵识不足时拒绝");
            var missing = Actor(model, draft, 0);
            Check(!missing.TryForge(model, draft, out _) && missing.current_world_stamina == 100, "缺材料不扣精力");
            var invalid = model.ExampleDraft(1); invalid.Inputs.RemoveAll(i => i.Role == "主体");
            Check(!owner.TryForge(model, invalid, out _), "非法岗位拒绝开炼");
            var full = Actor(model, draft); var fullData = (HumanData)full.entity_data!;
            while (full.get_inventory_slot_count() < Character.InventorySlotCapacity)
                full.add_artifact_to_inventory(new ArtifactInstanceData { instance_id = Guid.NewGuid().ToString() });
            Check(!full.TryForge(model, draft, out _) && full.current_world_stamina == 100 && full.GetForgingStock(model).SequenceEqual(stock), "满包预检不吞料");
            var freed = Actor(model, draft, 1);
            while (freed.get_inventory_slot_count() < Character.InventorySlotCapacity)
                freed.add_artifact_to_inventory(new ArtifactInstanceData { instance_id = Guid.NewGuid().ToString() });
            Check(freed.TryForge(model, draft, out var freedResult) && freedResult.Success, "满包可使用本炉扣料腾出的格位");
            const string savePath = "user://tests/crafting_integration.tres";
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("user://tests"));
            Check(ResourceSaver.Save(new PersistentGameSave { player_data = data }, savePath) == Error.Ok, "炼成器写入正式保存容器");
            var restored = ResourceLoader.Load<PersistentGameSave>(savePath, cacheMode: ResourceLoader.CacheMode.Ignore)!;
            Check(restored.player_data!.artifact_inventory.Count == 2 && ForgedArtifactInventory.TryRead(restored.player_data.artifact_inventory[0], out _), "存档读回保留器物快照");
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(savePath));
            data.alchemy_proficiency = 36;
            var panel = new BaigongPanel(); AddChild(panel); panel.show_for(owner);
            typeof(BaigongPanel).GetMethod("select_page", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(panel, ["丹药"]);
            var open = panel.FindChildren("*", "Button", true, false).OfType<Button>().Single(b => b.Text == "进入炼丹房");
            open.EmitSignal(Button.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var room = panel.GetChildren().OfType<AlchemySlotBrewPreviewScene>().Single();
            Check(room.OwnerCharacter == owner, "游戏内炼丹房绑定当前人物");
            var hidden = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(AlchemySlotBrewPreviewScene).GetMethod("ApplySampleRoute", hidden)!.Invoke(room, [AlchemyRecipeCatalog.Pills[0].SampleRoutes.Last(), false]);
            var inputs = (IReadOnlyList<AlchemySlotSimulation.Input>)typeof(AlchemySlotBrewPreviewScene).GetMethod("GetSimulationInputs", hidden)!.Invoke(room, null)!;
            var pillData = new HumanData { alchemy_proficiency = 36, max_spirit_perception = 10000, current_world_stamina = 100,
                inventory_item_paths = new(inputs.Select(i => i.Path)), inventory_item_counts = new(inputs.Select(i => i.Dose * 3)),
                inventory_item_positions = new(Enumerable.Range(0, inputs.Count)) };
            var alchemist = new Character { entity_data = pillData }; _actors.Add(alchemist);
            Check(alchemist.TryBrewAlchemy(inputs, out var brewed, out _) && brewed!.OutputCount > 0, "正式炼丹自动产量成功");
            var rewards = new Godot.Collections.Array();
            foreach (var group in brewed!.Pills.Where(p => p.Quality >= 20).GroupBy(p => AlchemyQualityRules.Tier(p.Quality)))
                rewards.Add(new Godot.Collections.Dictionary { ["item_path"] = AlchemyProductCatalog.Path(brewed.ProductId, group.Key), ["count"] = group.Count() });
            Check(rewards.All(entry => alchemist.get_inventory_total_count(entry.AsGodotDictionary()["item_path"].AsString()) == 0) &&
                inputs.All(i => alchemist.get_inventory_total_count(i.Path) == i.Dose * 2), "开炼只扣投料，成品等待领取");
            var claim = new ItemClaimPanel(); GetTree().Root.AddChild(claim);
            Check(claim.Present(alchemist, rewards, "炼丹所得"), "炼丹成果进入通用领取页");
            typeof(ItemClaimPanel).GetMethod("claim_selected_items", hidden)!.Invoke(claim, null);
            Check(rewards.All(entry => alchemist.get_inventory_total_count(entry.AsGodotDictionary()["item_path"].AsString()) == entry.AsGodotDictionary()["count"].AsInt32()), "确认领取后丹药入包");
            typeof(AlchemySlotBrewPreviewScene).GetField("_brewing", hidden)!.SetValue(room, true);
            panel.close(); Check(!room.IsQueuedForDeletion() && panel.Visible, "炼丹结算期间返回不会隐藏成果");
            typeof(AlchemySlotBrewPreviewScene).GetField("_brewing", hidden)!.SetValue(room, false);
            panel.close(); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(panel.Visible && !panel.GetChildren().OfType<AlchemySlotBrewPreviewScene>().Any(), "返回先关闭炼丹房并保留百工");
            typeof(BaigongPanel).GetMethod("select_page", hidden)!.Invoke(panel, ["法宝"]);
            panel.FindChildren("*", "Button", true, false).OfType<Button>().Single(b => b.Text == "进入炼器工坊").EmitSignal(Button.SignalName.Pressed);
            var workshop = panel.GetChildren().OfType<ProcessForgingScene>().Single();
            var session = (ProcessForgingTrial.Session)typeof(ProcessForgingScene).GetField("_session", hidden)!.GetValue(workshop)!;
            Check(session.Stock.SequenceEqual(owner.GetForgingStock(model)), "游戏内工坊显示真实库存");
            typeof(ProcessForgingScene).GetMethod("Gallery", hidden)!.Invoke(workshop, null);
            var gallery = (Control)typeof(ProcessForgingScene).GetField("_overlay", hidden)!.GetValue(workshop)!;
            Check(gallery.FindChildren("ForgedArtifactItem_*", "Button", true, false).Count == data.artifact_inventory.Count, "重新进入器物册读取正式持有物");
            panel.close(); Check(!workshop.IsQueuedForDeletion(), "工坊返回优先关闭详情弹层");
            panel.close(); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(panel.Visible && !panel.GetChildren().OfType<ProcessForgingScene>().Any(), "工坊返回人物百工");
            panel.close(); Check(!panel.Visible, "再次返回关闭百工"); panel.QueueFree();
            var clockOwner = Actor(model, draft); AddChild(clockOwner);
            var clockData = (HumanData)clockOwner.entity_data!;
            clockData.forging_proficiency = draft.Proficiency; clockData.max_spirit_perception = 10000;
            clockData.current_world_stamina = 100;
            var time = GetNode<GameTimeMgr>("/root/GameTimeManager"); var before = time.get_precise_game_hours();
            Check(clockOwner.TryForge(model, draft, out var timed) && Math.Abs(time.get_precise_game_hours() - before - timed.Hours * 2) < .001,
                "树内正式炼器推进真实历法且时辰换算正确");
            GD.Print("CRAFTING_INTEGRATION_OK " + _checks); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
        finally { foreach (var actor in _actors) actor.Free(); }
    }
}
