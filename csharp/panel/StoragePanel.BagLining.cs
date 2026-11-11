using Godot;

public partial class StoragePanel
{
    private AtlasTexture? _nativeBagLining;

    private void draw_native_bag_grid(NativeInventorySurface surface, SpatialInventoryRegion region, Rect2 rect)
    {
        if (_nativeBagLining is null)
        {
            // The approved material sheet's middle third is the warm brown bag lining.
            var sheet = GD.Load<Texture2D>("res://textures/ui/inventory_material_20261107/storage_surface_materials_v1.png");
            var width = sheet.GetWidth() / 3f;
            _nativeBagLining = new AtlasTexture { Atlas = sheet, Region = new Rect2(width, 0, width, sheet.GetHeight()), FilterClip = true };
        }
        surface.DrawTextureRect(_nativeBagLining, rect, false);
        var dark = new Color(57f / 255, 42f / 255, 25f / 255, .44f);
        var light = new Color(252f / 255, 242f / 255, 211f / 255, .24f);
        for (int x = 1; x < region.Width; x++)
        {
            var start = rect.Position + new Vector2(x * NativeCell + .5f, 1);
            var end = start + new Vector2(0, rect.Size.Y - 2);
            surface.DrawLine(start, end, dark);
            surface.DrawLine(start + Vector2.Right, end + Vector2.Right, light);
        }
        for (int y = 1; y < region.Height; y++)
        {
            var start = rect.Position + new Vector2(1, y * NativeCell + .5f);
            var end = start + new Vector2(rect.Size.X - 2, 0);
            surface.DrawLine(start, end, dark);
            surface.DrawLine(start + Vector2.Down, end + Vector2.Down, light);
        }
        surface.DrawRect(rect.Grow(-.5f), new Color(69f / 255, 49f / 255, 30f / 255, .64f), false);
    }
}
