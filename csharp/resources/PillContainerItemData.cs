using Godot;

/// <summary>容器材质外观定义。保留旧容量字段与 ItemData 继承以兼容旧资源和存档；容器字段不限制丹药入包数量。</summary>
public partial class PillContainerItemData : ItemData
{
    [Export] public string material_id = "";
    // Kept to read older resource files. It no longer limits stored pill quantities.
    [Export(PropertyHint.Range, "0,999,1")] public int pill_capacity;
    [Export(PropertyHint.File, "*.png,*.webp,*.jpg")] public string open_bottle_icon_path = "";
    [Export(PropertyHint.File, "*.png,*.webp,*.jpg")] public string stopper_icon_path = "";

    public override Godot.Collections.Array<string> validate_definition()
    {
        var errors = base.validate_definition();
        if (string.IsNullOrWhiteSpace(material_id)) errors.Add("缺少容器材质标识");
        if (string.IsNullOrWhiteSpace(open_bottle_icon_path) || !ResourceLoader.Exists(open_bottle_icon_path))
            errors.Add("缺少有效的开口容器图标");
        if (string.IsNullOrWhiteSpace(stopper_icon_path) || !ResourceLoader.Exists(stopper_icon_path))
            errors.Add("缺少有效的容器瓶塞图标");
        return errors;
    }
}
