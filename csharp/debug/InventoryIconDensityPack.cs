using Godot;

/// <summary>以现行清单装配65张行囊图及独立动画药丸，每格128源像素，禁止放大。</summary>
public static class InventoryIconDensityPack
{
    public const string Root = "res://textures/items/inventory_density_20261029/";
    public const string Manifest = "res://docs/美术规范/行囊全物品图标与占地_20261029/物品占地与图标清单.json";
    public const string Tasks = "res://docs/美术规范/行囊全物品图标与占地_20261029/生成任务.json";
    public const string AnimationPillPath = Root + "pill_2x2.png";
    public const int PixelsPerCell = 128;
    public const float UsableFraction = .88f;
    public const int BoundsAlphaThreshold = 2;
    public readonly record struct Spec(string File, int Width, int Height);
    public static Spec[] Specs => Json.ParseString(Godot.FileAccess.GetFileAsString(Tasks)).AsGodotArray().Select(value =>
    {
        var row = value.AsGodotDictionary(); var cells = row["cells"].AsGodotArray();
        return new Spec(row["file"].AsString(), cells[0].AsInt32(), cells[1].AsInt32());
    }).ToArray();
    private static IEnumerable<Spec> AllSpecs => Specs.Append(new Spec("pill_2x2.png", 2, 2));

    public static void Pack()
    {
        ValidateManifest();
        // Preflight every source before writing any packed output.
        foreach (var spec in AllSpecs) ValidateSource(spec);
        var metadata = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var spec in AllSpecs)
        {
            using var source = Image.LoadFromFile(ProjectSettings.GlobalizePath(SourcePath(spec)));
            var original = source.GetSize();
            var width = spec.Width * PixelsPerCell; var height = spec.Height * PixelsPerCell;
            source.Convert(Image.Format.Rgba8);
            // Ignore isolated 1/255-alpha generation noise when locating the item.
            // Cropping preserves every original RGBA pixel inside these bounds;
            // this threshold never hard-cuts alpha throughout the image.
            var bounds = ContentBounds(source);
            var fittedSize = FittedSize(bounds, spec);
            using var cropped = source.GetRegion(bounds);
            if (cropped.GetSize() != fittedSize)
                cropped.Resize(fittedSize.X, fittedSize.Y, Image.Interpolation.Lanczos);
            using var output = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
            output.Fill(Colors.Transparent);
            output.BlitRect(cropped, new Rect2I(Vector2I.Zero, cropped.GetSize()), (new Vector2I(width, height) - cropped.GetSize()) / 2);
            if (output.SavePng(Root + spec.File) != Error.Ok) throw new InvalidOperationException("保存图标失败: " + spec.File);
            metadata.Add(new() { ["file"] = spec.File, ["cells_w"] = spec.Width, ["cells_h"] = spec.Height,
                ["pixels_w"] = width, ["pixels_h"] = height, ["pixels_per_cell"] = PixelsPerCell,
                ["source"] = SourcePath(spec), ["source_w"] = original.X, ["source_h"] = original.Y,
                ["source_used_x"] = bounds.Position.X, ["source_used_y"] = bounds.Position.Y,
                ["source_used_w"] = bounds.Size.X, ["source_used_h"] = bounds.Size.Y,
                ["fitted_w"] = fittedSize.X, ["fitted_h"] = fittedSize.Y,
                ["usable_fraction"] = UsableFraction,
                ["bounds_alpha_threshold"] = BoundsAlphaThreshold });
        }
        using var file = Godot.FileAccess.Open(Root + "density_manifest.json", Godot.FileAccess.ModeFlags.Write);
        file.StoreString(Json.Stringify(metadata, "  "));
        Verify();
    }
    private static string SourcePath(Spec spec) => Root + "raw_unified_final/" + spec.File;
    private static void ValidateSource(Spec spec)
    {
        using var image = Image.LoadFromFile(ProjectSettings.GlobalizePath(SourcePath(spec)));
        if (image.IsEmpty()) throw new InvalidOperationException("生成源图未找到: " + SourcePath(spec));
        if (image.DetectAlpha() == Image.AlphaMode.None)
            throw new InvalidOperationException("源图缺少透明通道: " + spec.File);
        image.Convert(Image.Format.Rgba8);
        FittedSize(ContentBounds(image), spec);
    }
    private static Rect2I ContentBounds(Image image)
    {
        var pixels = image.GetData();
        var width = image.GetWidth(); var height = image.GetHeight();
        var minX = width; var minY = height; var maxX = -1; var maxY = -1;
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                if (pixels[(y * width + x) * 4 + 3] >= BoundsAlphaThreshold)
                {
                    minX = Math.Min(minX, x); minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
                }
        return maxX < minX ? new Rect2I() : new Rect2I(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }
    private static Vector2I FittedSize(Rect2I bounds, Spec spec)
    {
        if (bounds.Size.X <= 0 || bounds.Size.Y <= 0)
            throw new InvalidOperationException("源图没有非透明物品像素: " + spec.File);
        var available = new Vector2I(Math.Max(1, Mathf.RoundToInt(spec.Width * PixelsPerCell * UsableFraction)),
            Math.Max(1, Mathf.RoundToInt(spec.Height * PixelsPerCell * UsableFraction)));
        var factor = Mathf.Min((float)available.X / bounds.Size.X, (float)available.Y / bounds.Size.Y);
        if (factor > 1f)
            throw new InvalidOperationException($"物品有效像素不足，禁止放大: {spec.File} ({bounds.Size.X}×{bounds.Size.Y})");
        return new Vector2I(Math.Max(1, Mathf.RoundToInt(bounds.Size.X * factor)),
            Math.Max(1, Mathf.RoundToInt(bounds.Size.Y * factor)));
    }
    public static void ValidateManifest()
    {
        var specs = Specs;
        if (specs.Length != 65 || specs.Select(s => s.File).Distinct().Count() != 65) throw new InvalidOperationException("行囊目标图必须为65张且不重复");
        var rows = Json.ParseString(Godot.FileAccess.GetFileAsString(Manifest)).AsGodotArray();
        if (rows.Count != 96) throw new InvalidOperationException("正式物品清单必须为96项");
        foreach (var entry in rows)
        {
            var row = entry.AsGodotDictionary(); var icon = row["new_icon"].AsString();
            var spec = specs.Single(s => Root + s.File == icon);
            if (row["width"].AsInt32() != spec.Width || row["height"].AsInt32() != spec.Height || row["area"].AsInt32() != spec.Width * spec.Height
                || row["footprint"].AsString() != "rectangle" || !row["can_rotate"].AsBool()
                || row["pixels_w"].AsInt32() != spec.Width * PixelsPerCell || row["pixels_h"].AsInt32() != spec.Height * PixelsPerCell)
                throw new InvalidOperationException("清单占地与目标图不一致: " + row["path"]);
            if (row["category"].AsString() == "秘籍" && spec.File != "manual_generic_2x3.png") throw new InvalidOperationException("秘籍必须共用朴素书册");
            if ((row["category"].AsString() == "丹药" || row["script"].AsString() == "PillContainerItemData") && spec.File != "pill_container_empty_porcelain.png") throw new InvalidOperationException("丹药必须共用瓷瓶");
        }
        GD.Print("INVENTORY_ICON_MANIFEST_OK: 96 items, 65 inventory icons; animation pill excluded");
    }
    public static void Verify()
    {
        ValidateManifest();
        foreach (var spec in AllSpecs)
        {
            using var image = Image.LoadFromFile(ProjectSettings.GlobalizePath(Root + spec.File));
            if (image.IsEmpty() || image.GetWidth() != spec.Width * PixelsPerCell || image.GetHeight() != spec.Height * PixelsPerCell)
                throw new InvalidOperationException("源像素密度不一致: " + spec.File);
            if (image.DetectAlpha() == Image.AlphaMode.None || image.GetPixel(0, 0).A > .01f || image.GetPixel(image.GetWidth() - 1, image.GetHeight() - 1).A > .01f)
                throw new InvalidOperationException("图标透明通道或角落异常: " + spec.File);
        }
        GD.Print("INVENTORY_ICON_DENSITY_OK: 65 inventory sprites + 1 animation pill, alpha, 128 source pixels per cell");
    }
    public static void VerifyBindings()
    {
        ValidateManifest();
        var bindings = Json.ParseString(Godot.FileAccess.GetFileAsString(Manifest)).AsGodotArray();
        foreach (var entry in bindings)
        {
            var row = entry.AsGodotDictionary(); var path = "res://" + row["path"].AsString();
            var item = GD.Load<ItemData>(path) ?? throw new InvalidOperationException("物品资源未加载: " + path);
            if (item.icon_path != row["new_icon"].AsString() || item.inventory_width != row["width"].AsInt32()
                || item.inventory_height != row["height"].AsInt32() || item.inventory_can_rotate != row["can_rotate"].AsBool()
                || item.get_inventory_footprint_shape() != "rectangle" || item.get_inventory_footprint_cells().Count != row["area"].AsInt32())
                throw new InvalidOperationException("正式物品图标/占地与清单不符: " + path);
            var texture = GD.Load<Texture2D>(item.icon_path);
            if (texture is null || texture.GetWidth() != row["pixels_w"].AsInt32() || texture.GetHeight() != row["pixels_h"].AsInt32())
                throw new InvalidOperationException("正式物品图标尺寸错误: " + path);
        }
        GD.Print($"INVENTORY_ICON_BINDINGS_OK: {bindings.Count} formal item resources");
    }
}
