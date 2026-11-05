using Godot;

/// <summary>A storage specification, not a quality-to-capacity progression table.</summary>
[GlobalClass]
public partial class StorageRingItemData : ItemData
{
    public const string ItemName = "储物戒指";
    [Export] public string storage_quality = "";
    [Export] public int storage_width;
    [Export] public int storage_height;

    public StorageRingItemData()
    {
        entity_name = ItemName;
        inventory_width = inventory_height = 1;
        item_category = "器具";
        item_subcategory = "储物戒指";
    }

    public override string get_grade_string() => storage_quality;

    public override Godot.Collections.Array<string> validate_definition()
    {
        var errors = base.validate_definition();
        if (inventory_width != 1 || inventory_height != 1) errors.Add("储物戒指本体必须占1×1格");
        if (storage_width <= 0 || storage_height <= 0) errors.Add("储物戒指必须明确配置矩形内腔宽高");
        if (string.IsNullOrWhiteSpace(storage_quality)) errors.Add("储物戒指缺少品质说明");
        return errors;
    }
}
