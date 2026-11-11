using Godot;

public partial class StoragePanel
{
    private GradientTexture2D? _nativeGarmentCellTint;

    private void build_native_garment_lining(Control space)
    {
        // The accepted v5 cloth and paper edge are static; cells, labels and items remain native controls.
        var lining = InventoryGarmentStyle.CreateLining();
        place(lining, space, 0, 0, 1416, 906);
        _nativeGarmentCellTint = InventoryGarmentStyle.CreateCellTint();
    }

    private void draw_native_garment_grid(NativeInventorySurface surface, SpatialInventoryRegion region, Rect2 rect)
        => InventoryGarmentStyle.DrawGrid(surface, region, rect, _nativeGarmentCellTint);
}
