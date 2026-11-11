/// <summary>Shared storage grid scale. Window scaling is applied to the entire canvas.</summary>
public static class InventoryGridMetrics
{
    public const float DesignCell = 36f;
    public const int SourcePixelsPerCell = 128;

    public static Godot.Rect2 CenteredRegion(float centerX, float centerY, int columns, int rows)
    {
        var size = new Godot.Vector2(columns, rows) * DesignCell;
        return new Godot.Rect2(new Godot.Vector2(centerX, centerY) - size * .5f, size);
    }
}
