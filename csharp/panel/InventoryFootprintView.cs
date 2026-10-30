using Godot;

/// <summary>A mouse-transparent miniature of the item's configured occupied cells.</summary>
public partial class InventoryFootprintView : Control
{
    private const float CellSize = 16f;
    private const float MaximumSide = 96f;
    public Vector2I? Footprint { get; private set; }
    public int OccupiedCellCount => Footprint is { } size ? size.X * size.Y : 0;
    public float DisplayCellSize { get; private set; } = CellSize;

    public InventoryFootprintView()
    {
        Name = "InventoryFootprint";
        MouseFilter = MouseFilterEnum.Ignore;
        SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
        Visible = false;
    }

    public void SetFootprint(Vector2I? footprint)
    {
        Footprint = footprint is { X: > 0, Y: > 0 } ? footprint : null;
        Visible = Footprint.HasValue;
        DisplayCellSize = Footprint is { } cells ? Mathf.Min(CellSize, MaximumSide / Mathf.Max(cells.X, cells.Y)) : CellSize;
        CustomMinimumSize = Footprint is { } dimensions ? (Vector2)dimensions * DisplayCellSize : Vector2.Zero;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (Footprint is not { } cells) return;
        var extent = (Vector2)cells * DisplayCellSize;
        var origin = ((Size - extent) / 2f).Round();
        var fill = new Color("899d82", .28f);
        var line = new Color("d0c296", .78f);
        DrawRect(new Rect2(origin, extent), fill);
        // Every occupied cell remains visible; this is the footprint, not an icon frame.
        for (var x = 0; x <= cells.X; x++)
        {
            var offset = Mathf.Clamp(x * DisplayCellSize, .5f, extent.X - .5f);
            DrawLine(origin + new Vector2(offset, .5f), origin + new Vector2(offset, extent.Y - .5f), line, 1f);
        }
        for (var y = 0; y <= cells.Y; y++)
        {
            var offset = Mathf.Clamp(y * DisplayCellSize, .5f, extent.Y - .5f);
            DrawLine(origin + new Vector2(.5f, offset), origin + new Vector2(extent.X - .5f, offset), line, 1f);
        }
    }
}
