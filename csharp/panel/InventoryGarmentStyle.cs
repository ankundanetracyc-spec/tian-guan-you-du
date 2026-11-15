using Godot;

/// <summary>The accepted v5 garment lining shared by the inventory and receipt workspace.</summary>
internal static class InventoryGarmentStyle
{
    internal const string PanelTexturePath = "res://textures/ui/inventory_material_20261107/garment_inventory_panel_v5.png";
    internal static readonly Color TitleInk = new("4b5142"), RegionInk = new("626951"), CountInk = new("73775e");
    private const float Cell = InventoryGridMetrics.DesignCell;

    internal static TextureRect CreateLining() => new()
    {
        Name = "GarmentLining", Texture = GD.Load<Texture2D>(PanelTexturePath),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
        MouseFilter = Control.MouseFilterEnum.Ignore
    };

    internal static GradientTexture2D CreateCellTint() => new()
    {
        Width = 64, Height = 64, FillFrom = Vector2.Zero, FillTo = Vector2.One,
        Gradient = new Gradient { Colors = [new Color("faf5e84d"), new Color("f5f0e33d")] }
    };

    internal static ColorRect CreateRule() => new() { Color = new("a59e856e"), MouseFilter = Control.MouseFilterEnum.Ignore };

    internal static void DrawGrid(Control surface, SpatialInventoryRegion region, Rect2 rect, Texture2D? tint)
    {
        if (tint is not null) surface.DrawTextureRect(tint, rect, false);
        var dark = new Color(97f / 255, 91f / 255, 72f / 255, .35f);
        var light = new Color(1, 252f / 255, 232f / 255, .30f);
        for (int x = 1; x < region.Width; x++)
        {
            var start = rect.Position + new Vector2(x * Cell + .5f, 1);
            var end = start + new Vector2(0, rect.Size.Y - 2);
            surface.DrawLine(start, end, dark);
            surface.DrawLine(start + Vector2.Right, end + Vector2.Right, light);
        }
        for (int y = 1; y < region.Height; y++)
        {
            var start = rect.Position + new Vector2(1, y * Cell + .5f);
            var end = start + new Vector2(rect.Size.X - 2, 0);
            surface.DrawLine(start, end, dark);
            surface.DrawLine(start + Vector2.Down, end + Vector2.Down, light);
        }
        // Folded edges stay inside the real rectangle; visuals never change cell hit testing.
        surface.DrawLine(rect.Position + new Vector2(1, 2.5f), rect.Position + new Vector2(rect.Size.X - 1, 2.5f), new Color("fbf6e878"));
        surface.DrawLine(rect.Position + new Vector2(1, 4.5f), rect.Position + new Vector2(rect.Size.X - 1, 4.5f), new Color("81765a20"));
        surface.DrawRect(rect.Grow(-.5f), new Color("8d887173"), false);
    }
}
