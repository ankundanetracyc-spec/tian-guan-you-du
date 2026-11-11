using Godot;
using System;

/// <summary>Isolated coverage of the shared item/actual artifact travel fields.</summary>
public partial class ItemTravelSmoke : Node
{
    private int _checks;
    private void Check(bool value, string message) { _checks++; if (!value) throw new InvalidOperationException(message); }

    public override void _Ready()
    {
        try
        {
            var normal = GD.Load<ItemData>("res://resources/entities/items/灵石.tres") ?? throw new InvalidOperationException("灵石资源缺失");
            Check(normal.travel_data is null, "既有物品不应自动变成载具");
            Check(!StoragePanel.ItemHover(normal).Body.Contains("遁速："), "普通物品信息应保持不变");

            var empty = new ItemTravelData();
            var sample = new ItemData { entity_name = "行旅夹具", item_category = "器具", travel_data = empty };
            var emptyHover = StoragePanel.ItemHover(sample);
            Check(!empty.try_get_dun_speed(out _) && emptyHover.Body.Contains("遁速：未定") && emptyHover.Body.Contains("消耗：未定"),
                "未配置的数据不能当作零速度或免费使用");
            empty.visual_id = "spirit_boat";
            Check(!empty.try_get_passenger_capacity(out _) && StoragePanel.ItemHover(sample).Body.Contains("可承载人数：未定"),
                "未配置的承载人数应显示未定");
            empty.has_dun_speed = true;
            empty.dun_speed = double.NaN;
            Check(!empty.try_get_dun_speed(out _), "非法速度不能用于行旅");

            var travel = GD.Load<ItemTravelData>("res://resources/world/bai_chuan/yuxu_escort_boat_travel_20261106.tres") ?? throw new InvalidOperationException("接引舟数据缺失");
            Check(travel.try_get_dun_speed(out var speed) && speed == 4800, "接引舟遁速未读取");
            Check(travel.try_get_passenger_capacity(out var capacity) && capacity == 6, "接引舟承载人数未读取");
            Check(travel.describe_consumption() == "灵石 ×1／24小时", "实际资源路径或消耗说明不匹配");
            sample.travel_data = travel;
            var hover = StoragePanel.ItemHover(sample);
            Check(hover.Body.Contains("遁速：4800") && hover.Body.Contains("消耗：灵石 ×1／24小时") && hover.Body.Contains("可承载人数：6人"), "通用物品悬停缺少行旅字段");
            Check(hover.Footprint == sample.get_inventory_size(), "加入行旅信息不应改变占地");

            var artifact = new ArtifactInstanceData { artifact_name = "器物夹具", instance_id = "travel-test", travel_data = travel };
            Check(EquipmentItemPresentation.Hover(artifact).Body.Contains("遁速：4800"), "器物实例悬停缺少自身遁速");
            const string path = "user://item_travel_smoke.tres";
            Check(ResourceSaver.Save(artifact, path) == Error.Ok, "器物行旅数据无法保存");
            var restored = ResourceLoader.Load<ArtifactInstanceData>(path, cacheMode: ResourceLoader.CacheMode.Ignore);
            Check(restored?.travel_data?.try_get_dun_speed(out var restoredSpeed) == true && restoredSpeed == 4800,
                "器物实例读档丢失行旅遁速");
            Check(restored?.travel_data?.describe_consumption() == "灵石 ×1／24小时", "器物实例读档丢失消耗");
            Check(restored?.travel_data?.try_get_passenger_capacity(out var restoredCapacity) == true && restoredCapacity == 6,
                "器物实例读档丢失承载人数");
            GD.Print($"ITEM_TRAVEL_SMOKE_OK {_checks}");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError("ITEM_TRAVEL_SMOKE_FAILED " + e); GetTree().Quit(1); }
    }
}
