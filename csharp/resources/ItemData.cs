using System;
using Godot;
using Godot.Collections;

/// <summary>物品定义资源：材料、装备、秘籍及消耗品共用的数据。</summary>
public partial class ItemData : EntityData
{
    [Export] public StringName definition_id = new StringName("");
    [Export(PropertyHint.MultilineText)] public string description = "";
    [Export(PropertyHint.Enum, "材料,装备,秘籍,阵盘,器具,符箓,丹药,灵膳")] public string item_category = "材料";
    [Export] public string item_subcategory = "";
    [Export] public Array<string> tags = [];
    [Export(PropertyHint.Range, "0,27,1")] public int grade_value;

    // Optional composition: an item may provide a travel method without changing
    // its existing inventory category or becoming a piece of equipment.
    [ExportGroup("行旅数据")]
    [Export] public ItemTravelData? travel_data;

    [ExportGroup("悬停格形与范围")]
    [Export] public ItemLayoutPreviewData? layout_preview;
    [Export] public SwordControlPreviewData? control_preview;

    [ExportGroup("收纳体积")]
    [Export(PropertyHint.Range, "1,64,1")] public int storage_volume;
    /// <summary>单件物品所需的整数容量单位；图标格形不参与体积计算。</summary>
    public int get_storage_volume() => Math.Max(0, storage_volume);

    // Kept for the standard inventory icon grid and for independent battle-board shapes.
    // These dimensions affect display placement only; storage acceptance uses storage_volume.
    [ExportGroup("物品图标显示格形")]
    [Export] public int inventory_width = 2;
    [Export] public int inventory_height = 2;
    [Export] public bool inventory_can_rotate = true;
    public Vector2I get_inventory_size(bool rotated = false) => rotated && inventory_can_rotate
        ? new Vector2I(inventory_height, inventory_width) : new Vector2I(inventory_width, inventory_height);
    public string get_inventory_footprint_shape() => "rectangle";
    public int get_inventory_cell_count() => inventory_width * inventory_height;
    public Array<Vector2I> get_inventory_footprint_cells()
    {
        Array<Vector2I> cells = [];
        for (var y = 0; y < inventory_height; y++)
            for (var x = 0; x < inventory_width; x++) cells.Add(new Vector2I(x, y));
        return cells;
    }

    // 0=仅世界，1=仅战斗，2=两者；保持 int 以兼容既有 .tres 和 GDScript 调用方。
    [Export] public int usage_type;
    [Export] public string battle_skill_path = "";

    [ExportGroup("秘籍数据")]
    [Export] public Resource? gongfa;
    [Export] public Resource? craft_data;
    [Export] public float required_study_progress;

    public virtual string get_grade_string() => ItemAffixCatalog.grade_to_string(grade_value);

    public virtual Array<string> validate_definition()
    {
        Array<string> errors = [];
        if (definition_id.ToString().Length == 0)
            errors.Add("缺少稳定物品ID definition_id");
        if (entity_name.Trim().Length == 0)
            errors.Add("缺少物品名称");
        if (grade_value < 0 || grade_value > 27)
            errors.Add("品阶值必须位于0至27");
        if (storage_volume < 1 || storage_volume > 64)
            errors.Add("单件收纳体积必须位于1至64");
        if (inventory_width < 1 || inventory_height < 1)
            errors.Add("物品图标显示格形宽高必须为正整数");
        if (can_stack && max_stack < 2)
            errors.Add("可堆叠物品的最大堆叠数必须至少为2");
        if (!can_stack && max_stack != 1)
            errors.Add("不可堆叠物品的最大堆叠数必须为1");
        errors.AddRange(ItemAffixCatalog.validate_tags(tags, item_category == "材料"));
        if (item_category == "材料")
        {
            if (craft_data is not MaterialCraftData material)
                errors.Add("材料必须配置材性数据");
            else
            {
                var textureValues = ItemAffixCatalog.get_tag_values(tags, "质地");
                if (!ItemAffixCatalog.is_material_texture(material.texture))
                    errors.Add($"材性数据缺少有效主导加工类型：{material.texture}");
                if (textureValues.Count == 1 && textureValues[0] != material.texture)
                    errors.Add($"质地词缀与材性主导加工类型不一致：{textureValues[0]} / {material.texture}");
                if (!string.IsNullOrWhiteSpace(material.legacy_texture) && !ItemAffixCatalog.is_legacy_texture(material.legacy_texture))
                    errors.Add($"材性数据含无效旧材类兼容值：{material.legacy_texture}");
            }
        }
        return errors;
    }
}
