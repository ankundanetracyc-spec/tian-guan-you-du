using Godot;
using System;
using System.Linq;

/// <summary>Test actual inventory transactions and per-artifact charge, with an isolated fixture.</summary>
public partial class SpiritBoatFuelSmoke : Node
{
    private int _checks;
    private void Check(bool value, string message) { _checks++; if (!value) throw new InvalidOperationException(message); }

    public override void _Ready()
    {
        try
        {
            var actor = new Character(); AddChild(actor);
            var human = new HumanData { entity_name = "灵舟事务夹具" };
            actor.load_from_data(human);
            var profile = GD.Load<ItemTravelData>("res://resources/world/bai_chuan/yuxu_escort_boat_travel_20261106.tres");
            var snapshot = new ProcessForgingTrial.Artifact {
                InstanceId = "boat-fixture", Name = "灵舟夹具", Category = "舟", Tier = "法器", Shape = "灵舟",
                InventoryIconPath = "res://textures/items/inventory_density_20261029/spirit_boat_icon_20261106.png", InventoryWidth = 2, InventoryHeight = 6
            };
            var boat = ForgedArtifactInventory.Create(snapshot); boat.travel_data = profile;
            Check(actor.add_artifact_to_inventory(boat), "器物实例不能进入真实行囊");
            var secondSnapshot = ProcessForgingTrial.Clone(snapshot); secondSnapshot.InstanceId = "boat-fixture-2"; secondSnapshot.Name = "另一艘舟夹具";
            var second = ForgedArtifactInventory.Create(secondSnapshot); second.travel_data = profile; second.travel_charge_hours = 9;
            Check(actor.add_artifact_to_inventory(second), "第二艘独立器物无法入包");
            Check(actor.add_item_to_inventory("res://resources/entities/items/灵石.tres", 3), "灵石未进入行囊");
            Check(!actor.board_spirit_boat(boat.instance_id, 7) && !boat.travel_in_use, "超载时不得登舟");
            Check(actor.board_spirit_boat(boat.instance_id), "无法登上自己持有的灵舟");
            Check(!actor.board_spirit_boat(boat.instance_id), "不能重复登舟");
            Check(!actor.board_spirit_boat(second.instance_id), "已有使用中的舟时不能同时登上另一艘");
            Check(actor.prepare_spirit_boat_hours(boat.instance_id, 30), "充能失败");
            Check(actor.get_inventory_total_count("res://resources/entities/items/灵石.tres") == 1 && boat.travel_charge_hours == 48,
                "应真实扣除两枚灵石并保留48小时续航");
            Check(actor.consume_spirit_boat_hours(boat.instance_id, 5.5) && Math.Abs(boat.travel_charge_hours - 42.5) < 1e-7, "飞行未实际消耗舟内续航");
            Check(second.travel_charge_hours == 9, "不同舟的续航不能互相覆盖");
            Check(actor.prepare_spirit_boat_hours(boat.instance_id, 10) && actor.get_inventory_total_count("res://resources/entities/items/灵石.tres") == 1,
                "已有续航充足时不应再扣灵石");
            Check(!actor.prepare_spirit_boat_hours(boat.instance_id, 100) && boat.travel_charge_hours == 42.5 && actor.get_inventory_total_count("res://resources/entities/items/灵石.tres") == 1,
                "灵石不足时不得扣石或改变续航");
            var entry = actor.get_spatial_inventory().Single(e => e.ArtifactId == boat.instance_id);
            Check(!actor.remove_artifact_from_inventory(boat.instance_id) && !actor.can_store_spatial_item(entry.InstanceId), "飞行器不得在使用中丢弃或转移");
            var info = ForgedArtifactInventory.Hover(boat);
            Check(info.Body.Contains("遁速：4800") && info.Body.Contains("可承载人数：6人") && info.Body.Contains("剩余续航：42.5小时") && !info.Body.Contains("锋锐："), "器物信息字段或器类显示错误");
            Check(ForgedArtifactInventory.IconPath(boat) == snapshot.InventoryIconPath && entry.Size == new Vector2I(2, 6), "非剑器物图标和占地未读取实例");
            const string path = "user://spirit_boat_fuel_smoke.tres";
            Check(ResourceSaver.Save((HumanData)actor.entity_data!, path) == Error.Ok, "人物资源无法保存");
            var copy = ResourceLoader.Load<HumanData>(path, cacheMode: ResourceLoader.CacheMode.IgnoreDeep);
            var restored = copy?.artifact_inventory.FirstOrDefault(x => x.instance_id == boat.instance_id);
            Check(restored is { travel_in_use: true } && Math.Abs(restored.travel_charge_hours - 42.5) < 1e-7, "读档丢失本件舟的续航或使用状态");
            Check(actor.leave_spirit_boat(boat.instance_id) && actor.remove_artifact_from_inventory(boat.instance_id), "落地后无法收起或转移");
            Check(actor.board_spirit_boat(second.instance_id) && actor.prepare_spirit_boat_hours(second.instance_id, 9) && second.travel_charge_hours == 9,
                "第二艘舟应沿用自己的余量");
            GD.Print($"SPIRIT_BOAT_FUEL_SMOKE_OK {_checks}"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError("SPIRIT_BOAT_FUEL_SMOKE_FAILED " + error); GetTree().Quit(1); }
    }
}
