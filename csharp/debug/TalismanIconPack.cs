using Godot;
using System.Text.Json;

/// <summary>按现有128源像素/格规范打包新生成原图，仅裁物外透明留白、等比缩小，原图保留。</summary>
public partial class TalismanIconPack : Node
{
    private sealed record PackedIcon(string File, int Width, int Height, int SourceWidth, int SourceHeight,
        int ContentX, int ContentY, int ContentWidth, int ContentHeight, int FittedWidth, int FittedHeight);

    public override void _Ready()
    {
        try
        {
            string root = TalismanArtCatalog.PaintedRoot;
            string[] names = ["paper", "ink", "hushen", "yanshi", "brush_qingzhu", "brush_jinghao", "brush_cangfeng"];
            var output = new List<PackedIcon>();
            foreach (string name in names)
            {
                using var source = Image.LoadFromFile(ProjectSettings.GlobalizePath(root + "raw/" + name + ".png"));
                if (source.IsEmpty() || source.DetectAlpha() == Image.AlphaMode.None)
                    throw new InvalidOperationException("生成源图缺失或不透明：" + name);
                source.Convert(Image.Format.Rgba8);
                var bytes = source.GetData();
                int sw = source.GetWidth(), sh = source.GetHeight(), minX = sw, minY = sh, maxX = -1, maxY = -1;
                for (int y = 0; y < sh; y++) for (int x = 0; x < sw; x++)
                    if (bytes[(y * sw + x) * 4 + 3] >= InventoryIconDensityPack.BoundsAlphaThreshold)
                    { minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y); }
                if (maxX < minX || maxY < minY) throw new InvalidOperationException("源图无主体：" + name);
                var bounds = new Rect2I(minX, minY, maxX - minX + 1, maxY - minY + 1);
                int w = InventoryGridMetrics.SourcePixelsPerCell, h = name == "ink" ? w : w * 2;
                float scale = Math.Min(w * InventoryIconDensityPack.UsableFraction / bounds.Size.X,
                    h * InventoryIconDensityPack.UsableFraction / bounds.Size.Y);
                if (scale > 1) throw new InvalidOperationException("禁止放大源图：" + name);
                var fitted = new Vector2I(Math.Max(1, Mathf.RoundToInt(bounds.Size.X * scale)), Math.Max(1, Mathf.RoundToInt(bounds.Size.Y * scale)));
                using var cropped = source.GetRegion(bounds);
                cropped.Resize(fitted.X, fitted.Y, Image.Interpolation.Lanczos);
                using var packed = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
                packed.Fill(Colors.Transparent);
                packed.BlitRect(cropped, new Rect2I(Vector2I.Zero, fitted), (new Vector2I(w, h) - fitted) / 2);
                if (packed.SavePng(root + name + ".png") != Error.Ok) throw new InvalidOperationException("保存失败：" + name);
                output.Add(new(name + ".png", w, h, sw, sh, bounds.Position.X, bounds.Position.Y,
                    bounds.Size.X, bounds.Size.Y, fitted.X, fitted.Y));
            }
            System.IO.File.WriteAllText(ProjectSettings.GlobalizePath(root + "density_manifest.json"),
                JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));
            GD.Print("TALISMAN_ICON_PACK_OK icons=7 pixels_per_cell=128 originals_preserved=true");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
