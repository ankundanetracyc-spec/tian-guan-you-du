using Godot;
using System.Globalization;

public partial class QuietRoomTriangle : Button
{
    public bool Up { get; set; }
    public bool Right { get; set; }
    public override Vector2 _GetMinimumSize() => Vector2.Zero;
    public override void _Ready()
    {
        foreach (string state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled", "focus" })
            AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        MouseDefaultCursorShape = CursorShape.PointingHand;
    }
    public override void _Draw()
    {
        var color = CraftingPaperStyle.Accent; if (Disabled) color.A = .35f;
        var middle = Size / 2;
        Vector2[] points = Right ? [new(0, 0), new(Size.X, Size.Y / 2), new(0, Size.Y)]
            : Up ? [middle + new Vector2(-5, 3), middle + new Vector2(5, 3), middle + new Vector2(0, -3)]
            : [middle + new Vector2(-5, -3), middle + new Vector2(5, -3), middle + new Vector2(0, 3)];
        DrawColoredPolygon(points, color);
    }
}
