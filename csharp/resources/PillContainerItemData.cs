using Godot;

/// <summary>容器材质外观定义。保留旧容量字段与 ItemData 继承以兼容旧资源和存档；容器字段不限制丹药入包数量。</summary>
public partial class PillContainerItemData : ItemData
{
    [Export] public string material_id = "";
    // Kept to read older resource files. It no longer limits stored pill quantities.
    [Export(PropertyHint.Range, "0,999,1")] public int pill_capacity;
    [Export(PropertyHint.File, "*.png,*.webp,*.jpg")] public string open_bottle_icon_path = "";
    [Export(PropertyHint.File, "*.png,*.webp,*.jpg")] public string stopper_icon_path = "";
    // Animation can split the current inventory icon without maintaining a second bottle silhouette.
    [Export] public bool animation_from_closed_icon;
    [Export] public Vector2 animation_mouth_uv = new(.49f, .168f);
    [Export] public Rect2 animation_stopper_region_uv;
    [Export] public float animation_body_cutoff_uv;

    public override Godot.Collections.Array<string> validate_definition()
    {
        var errors = base.validate_definition();
        if (string.IsNullOrWhiteSpace(material_id)) errors.Add("缺少容器材质标识");
        if (animation_from_closed_icon)
        {
            if (string.IsNullOrWhiteSpace(icon_path) || !ResourceLoader.Exists(icon_path)) errors.Add("缺少有效的现用瓷瓶图标");
            if (animation_body_cutoff_uv <= 0 || animation_body_cutoff_uv >= 1 ||
                animation_mouth_uv.X <= 0 || animation_mouth_uv.X >= 1 || animation_mouth_uv.Y <= 0 || animation_mouth_uv.Y >= 1 ||
                !animation_stopper_region_uv.HasArea() || animation_stopper_region_uv.Position.X < 0 ||
                animation_stopper_region_uv.Position.Y < 0 || animation_stopper_region_uv.End.X > 1 || animation_stopper_region_uv.End.Y > 1)
                errors.Add("瓷瓶动画标定无效");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(open_bottle_icon_path) || !ResourceLoader.Exists(open_bottle_icon_path))
                errors.Add("缺少有效的开口容器图标");
            if (string.IsNullOrWhiteSpace(stopper_icon_path) || !ResourceLoader.Exists(stopper_icon_path))
                errors.Add("缺少有效的容器瓶塞图标");
        }
        return errors;
    }
}
