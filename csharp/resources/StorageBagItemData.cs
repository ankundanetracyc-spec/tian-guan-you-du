using Godot;

/// <summary>A rectangular storage bag specification; quality-to-capacity progression is configured separately.</summary>
[GlobalClass]
public partial class StorageBagItemData : ItemData
{
    public const string ItemName = "储物袋";
    public const string IconPath = "res://textures/items/storage_bag_20261108/storage_bag_2x2.png";
    [Export] public string storage_quality = "";
    [Export] public int storage_width;
    [Export] public int storage_height;

    public StorageBagItemData()
    {
        entity_name = ItemName;
        icon_path = IconPath;
        inventory_width = inventory_height = 2;
        storage_volume = 2;
        item_category = "器具";
        item_subcategory = ItemName;
    }

    public override string get_grade_string() => storage_quality;

    public override Godot.Collections.Array<string> validate_definition()
    {
        var errors = base.validate_definition();
        if (inventory_width != 2 || inventory_height != 2) errors.Add("储物袋图标显示格形必须为2×2");
        if (get_storage_volume() != 2) errors.Add("储物袋本体体积固定为2容量");
        if (storage_width <= 0 || storage_height <= 0) errors.Add("储物袋必须明确配置矩形内腔宽高");
        if (string.IsNullOrWhiteSpace(storage_quality)) errors.Add("储物袋缺少品质说明");
        return errors;
    }
}
