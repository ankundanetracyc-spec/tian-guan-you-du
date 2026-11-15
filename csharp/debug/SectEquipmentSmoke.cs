using Godot;
using System.Reflection;

public partial class SectEquipmentSmoke : Node
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); GD.Print("PASS " + message); }
    public override void _Ready()
    {
        try
        {
            var main = new EastWoodsActionHarness(); AddChild(main); main.AddChild(new CanvasLayer { Name = "SceneLayer" });
            var human = new HumanData { entity_name = "器物兑换验收", sect_affiliation_id = "yuxu_xianzong" };
            var owner = new Character { entity_data = human }; main.AddChild(owner);
            human = (HumanData)owner.entity_data!;
            typeof(MainScene).GetField("player", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(main, owner);
            Check(human.record_yuxu_contribution(new YuxuSectContributionRecord { record_id = "initial", source_transaction_id = "initial", transaction_kind = YuxuSectContributionRecord.KindOfficialDutyReward, contribution_delta = 100, source_title = "测试履职", occurred_at = "隔离测试", cost_summary = "测试", result_summary = "测试" }), "隔离贡献初始化");
            var purchase = typeof(MainScene).GetMethod("on_yuxu_exchange_purchase_requested", BindingFlags.Instance | BindingFlags.NonPublic)!;
            int cost = 10;
            foreach (var name in new[] { "素行剑", "承露剑", "含章剑" })
            {
                var path = "res://resources/entities/equipment/" + name + ".tres";
                var item = GD.Load<EquipmentData>(path);
                Check(YuxuSectPrototypeBalance.is_exchangeable_item(item) && YuxuSectPrototypeBalance.exchange_base_cost(item) == 0, name + "目录与零价");
                var balance = human.get_yuxu_contribution_total();
                purchase.Invoke(main, [path, cost + 1]);
                Check(human.get_yuxu_contribution_total() == balance, "错误报价不扣贡献");
                purchase.Invoke(main, [path, 0]);
                Check(owner.get_inventory_total_count(path) == 1 && human.get_yuxu_contribution_total() == balance, name + "零价兑换不扣贡献");
                Check(owner.equip_inventory_item(path, "weapon"), "装入武器槽");
                var defense = owner.physical_defense; owner.refresh_gongfa_effects();
                Check(owner.physical_defense == defense, "刷新不重复叠加");
                if (cost > 10) Check(defense == cost / 10, "物防实际生效");
                Check(!owner.equip_inventory_item(path, "ornament_1"), "剑不能装入器物槽");
                Check(owner.unequip_inventory_item(path) && owner.physical_defense == 0, "卸下撤销收益");
                cost += 10;
            }
            foreach (var name in new[] { "清和剑" })
            {
                var path = "res://resources/entities/equipment/" + name + ".tres";
                Check(YuxuSectPrototypeBalance.is_exchangeable_item(GD.Load<ItemData>(path)), "法器目录有效");
                var before = human.artifact_inventory.Count;
                purchase.Invoke(main, [path, 0]); purchase.Invoke(main, [path, 0]);
                Check(human.artifact_inventory.Count == before + 2, name + "可重复免费兑换");
                var pair = human.artifact_inventory.Skip(before).ToList();
                Check(pair[0].instance_id != pair[1].instance_id && pair.All(item => ForgedArtifactInventory.TryRead(item, out var snapshot) && snapshot.Tier == "法器"), "法器真实评阶与独立实例");
            }
            Check(human.get_yuxu_contribution_total() == 100 && human.get_yuxu_official_service_total() == 100 && human.sect_contribution_records.Count == 1, "免费兑换不造零额流水");
            const string equippedPath = "res://resources/entities/equipment/含章剑.tres";
            owner.equip_inventory_item(equippedPath, "weapon");
            DirAccess.MakeDirRecursiveAbsolute("user://tests");
            const string savePath = "user://tests/sect_equipment.tres";
            Check(ResourceSaver.Save(new PersistentGameSave { player_data = human }, savePath) == Error.Ok, "存档容器保存");
            var loaded = ResourceLoader.Load<PersistentGameSave>(savePath, cacheMode: ResourceLoader.CacheMode.Ignore)!;
            var restored = new Character(); main.AddChild(restored); restored.load_from_data(loaded.player_data!);
            Check(restored.physical_defense == 3 && restored.get_inventory_total_count(equippedPath) == 1 && ((HumanData)restored.entity_data!).get_yuxu_contribution_total() == 100 && restored.get_artifact_items().Count == 2, "存档读回凡器法器装备贡献及收益");
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(savePath));
            human.remove_yuxu_contribution("initial");
            var countBefore = human.artifact_inventory.Count;
            purchase.Invoke(main, ["res://resources/entities/equipment/清和剑.tres", 0]);
            Check(human.get_yuxu_contribution_total() == 0 && human.artifact_inventory.Count == countBefore + 1, "零余额可兑换法器");
            GD.Print("SECT_EQUIPMENT_OK"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
