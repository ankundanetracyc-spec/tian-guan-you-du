using Godot;

/// <summary>装配和核对统一的方形物品图标。原格形清单用于核对库存定义。</summary>
public static class InventoryIconDensityPack
{
    public const string Root = "res://textures/items/inventory_density_20261029/";
    public const string Manifest = "res://docs/美术规范/行囊全物品图标与占地_20261029/物品占地与图标清单.json";
    public const string Tasks = "res://docs/美术规范/行囊全物品图标与占地_20261029/生成任务.json";
    public const string AnimationPillPath = Root + "pill_2x2.png";
    public const int IconPixels = 256;
    private const string SourceRoot = "res://textures/items/item_icon_sources_20261115/";
    private const float UsableFraction = .88f;
    private const int BoundsAlphaThreshold = 16;

    public readonly record struct Spec(string Path, string Name);
    private readonly record struct LegacySpec(string File, int Width, int Height, string Name);

    private static LegacySpec[] LegacySpecs() => Json.ParseString(Godot.FileAccess.GetFileAsString(Tasks)).AsGodotArray().Select(value =>
    {
        var row = value.AsGodotDictionary();
        var cells = row["cells"].AsGodotArray();
        return new LegacySpec(row["file"].AsString(), cells[0].AsInt32(), cells[1].AsInt32(), row["name"].AsString());
    }).ToArray();

    internal static readonly Spec[] TalismanSpecs =
    [
        new(TalismanArtCatalog.PaintedRoot + "paper.png", "符纸"),
        new(TalismanArtCatalog.PaintedRoot + "ink.png", "符墨"),
        new(TalismanArtCatalog.PaintedRoot + "hushen.png", "护身符"),
        new(TalismanArtCatalog.PaintedRoot + "yanshi.png", "炎矢符"),
        new(TalismanArtCatalog.PaintedRoot + "brush_qingzhu.png", "青竹符笔"),
        new(TalismanArtCatalog.PaintedRoot + "brush_jinghao.png", "静毫符笔"),
        new(TalismanArtCatalog.PaintedRoot + "brush_cangfeng.png", "藏锋符笔")
    ];

    public static Spec[] Specs =>
    [
        ..LegacySpecs().Select(row => new Spec(Root + row.File, row.Name)),
        ..TalismanSpecs,
        new(StorageBagItemData.IconPath, "储物袋"),
        new(Root + "spirit_boat_icon_20261106.png", "灵舟")
    ];

    public static void Pack()
    {
        ValidateManifest();
        Pack(Specs);
    }

    internal static void Pack(Spec[] specs)
    {
        var prepared = new List<(Spec Spec, Image Image)>(specs.Length);
        try
        {
            foreach (var spec in specs)
                prepared.Add((spec, PackImage(spec)));
            foreach (var (spec, image) in prepared)
                if (image.SavePng(spec.Path) != Error.Ok)
                    throw new InvalidOperationException("保存图标失败: " + spec.Path);
        }
        finally
        {
            foreach (var (_, image) in prepared)
                image.Dispose();
        }
        Verify(specs);
    }

    private static Image PackImage(Spec spec)
    {
        string sourcePath = SourceRoot + spec.Path.GetFile();
        using var source = Image.LoadFromFile(ProjectSettings.GlobalizePath(sourcePath));
        if (source.IsEmpty() || source.DetectAlpha() == Image.AlphaMode.None)
            throw new InvalidOperationException("缺少有效的透明源图: " + sourcePath);

        source.Convert(Image.Format.Rgba8);
        var bounds = ContentBounds(source);
        if (!bounds.HasArea())
            throw new InvalidOperationException("源图没有可见物品: " + sourcePath);
        bounds = bounds.Grow(3).Intersection(new Rect2I(Vector2I.Zero, source.GetSize()));

        float scale = IconPixels * UsableFraction / Math.Max(bounds.Size.X, bounds.Size.Y);
        if (scale > 1f)
            throw new InvalidOperationException("物品源像素不足: " + sourcePath);
        var fitted = new Vector2I(Math.Max(1, Mathf.RoundToInt(bounds.Size.X * scale)),
            Math.Max(1, Mathf.RoundToInt(bounds.Size.Y * scale)));

        using var cropped = source.GetRegion(bounds);
        cropped.Resize(fitted.X, fitted.Y, Image.Interpolation.Lanczos);
        var output = Image.CreateEmpty(IconPixels, IconPixels, false, Image.Format.Rgba8);
        output.Fill(Colors.Transparent);
        output.BlitRect(cropped, new Rect2I(Vector2I.Zero, fitted), (output.GetSize() - fitted) / 2);
        return output;
    }

    private static Rect2I ContentBounds(Image image)
    {
        var pixels = image.GetData();
        var width = image.GetWidth();
        var height = image.GetHeight();
        var minX = width;
        var minY = height;
        var maxX = -1;
        var maxY = -1;
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                if (pixels[(y * width + x) * 4 + 3] >= BoundsAlphaThreshold)
                {
                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                }
        return maxX < minX ? new Rect2I() : new Rect2I(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    public static void ValidateManifest()
    {
        var specs = LegacySpecs();
        if (specs.Length != 66 || specs.Select(s => s.File).Distinct().Count() != 66)
            throw new InvalidOperationException("原始图标清单必须为66张且不重复");
        var rows = Json.ParseString(Godot.FileAccess.GetFileAsString(Manifest)).AsGodotArray();
        if (rows.Count != 96)
            throw new InvalidOperationException("原始物品清单必须为96项");

        foreach (var entry in rows)
        {
            var row = entry.AsGodotDictionary();
            var spec = specs.Single(s => Root + s.File == row["new_icon"].AsString());
            if (row["width"].AsInt32() != spec.Width || row["height"].AsInt32() != spec.Height || row["area"].AsInt32() != spec.Width * spec.Height
                || row["footprint"].AsString() != "rectangle" || !row["can_rotate"].AsBool())
                throw new InvalidOperationException("原始清单的库存格形不一致: " + row["path"]);
            if (row["category"].AsString() == "秘籍" && spec.File != "manual_generic_2x3.png")
                throw new InvalidOperationException("所有秘籍必须共用朴素书册");
            if (row["category"].AsString() == "丹药" && spec.File != "pill_2x2.png")
                throw new InvalidOperationException("所有丹药必须共用统一丹丸图");
            if (row["script"].AsString() == "PillContainerItemData" && spec.File != "pill_container_empty_porcelain.png")
                throw new InvalidOperationException("空瓷瓶模板必须使用独立瓷瓶图");
        }
        GD.Print("INVENTORY_ICON_MANIFEST_OK: 96 original item entries");
    }

    public static void Verify()
    {
        ValidateManifest();
        Verify(Specs);
    }

    private static void Verify(Spec[] specs)
    {
        foreach (var spec in specs)
        {
            using var image = Image.LoadFromFile(ProjectSettings.GlobalizePath(spec.Path));
            if (image.IsEmpty() || image.GetSize() != new Vector2I(IconPixels, IconPixels))
                throw new InvalidOperationException("物品图标尺寸不一致: " + spec.Path);
            int last = IconPixels - 1;
            if (image.DetectAlpha() == Image.AlphaMode.None || image.GetPixel(0, 0).A != 0f || image.GetPixel(last, 0).A != 0f
                || image.GetPixel(0, last).A != 0f || image.GetPixel(last, last).A != 0f)
                throw new InvalidOperationException("图标透明通道或角落异常: " + spec.Path);
        }
        GD.Print($"INVENTORY_ICON_DENSITY_OK: {specs.Length} RGBA item icons, {IconPixels}x{IconPixels}");
    }

    public static void VerifyBindings()
    {
        ValidateManifest();
        var bindings = Json.ParseString(Godot.FileAccess.GetFileAsString(Manifest)).AsGodotArray();
        foreach (var entry in bindings)
        {
            var row = entry.AsGodotDictionary();
            var path = "res://" + row["path"].AsString();
            var item = GD.Load<ItemData>(path) ?? throw new InvalidOperationException("物品资源未加载: " + path);
            if (item.icon_path != row["new_icon"].AsString() || item.inventory_width != row["width"].AsInt32()
                || item.inventory_height != row["height"].AsInt32() || item.inventory_can_rotate != row["can_rotate"].AsBool()
                || item.get_inventory_footprint_shape() != "rectangle" || item.get_inventory_footprint_cells().Count != row["area"].AsInt32())
                throw new InvalidOperationException("正式物品图标或库存格形与清单不符: " + path);
        }
        foreach (var spec in Specs)
        {
            var texture = GD.Load<Texture2D>(spec.Path);
            if (texture is null || texture.GetWidth() != IconPixels || texture.GetHeight() != IconPixels)
                throw new InvalidOperationException("已导入物品图标尺寸不一致: " + spec.Path);
        }
        GD.Print($"INVENTORY_ICON_BINDINGS_OK: {bindings.Count} original item entries and all imported item icons");
    }
}
