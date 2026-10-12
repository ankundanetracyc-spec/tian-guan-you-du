using Godot;

/// <summary>行囊细线角笔与纸墨材质；按控件尺寸绘制，避免整张理想图充当交互界面。</summary>
public partial class InventoryPaperFrame : Control
{
    public bool Dark { get; set; }
    public bool Gold { get; set; }
    public bool Selected { get; set; }
    public bool FillPaper { get; set; } = true;
    public bool EmptyMountain { get; set; }
    public float PointerX { get; set; } = -1;
    public bool PointerAtBottom { get; set; }
    private Texture2D? _paper, _ink;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _paper = GD.Load<Texture2D>(WorldHudStyle.PaperPath);
        _ink = GD.Load<Texture2D>("res://textures/ui/inventory_20261012/approved_shell_v1.png");
        Resized += QueueRedraw;
    }
    public override void _Draw()
    {
        if (Size.X < 2 || Size.Y < 2) return;
        var edge = new Rect2(1, 1, Size.X - 2, Size.Y - 2);
        if (FillPaper)
        {
            if (Dark)
            {
                DrawRect(edge, new Color("1e211a"));
                if (_ink is not null)
                    DrawTextureRectRegion(_ink, edge, new Rect2(30, 100, 210, 760), new Color(1, 1, 1, .28f));
            }
            else if (_paper is not null)
                DrawTextureRectRegion(_paper, edge, new Rect2(100, 140, 1972, 420), new Color(1, 1, 1, .34f));
            if (EmptyMountain && _ink is not null)
                DrawTextureRectRegion(_ink, new Rect2(8, Size.Y - 94, Size.X - 16, 86),
                    new Rect2(1050, 810, 420, 90), new Color(1, 1, 1, .3f));
        }
        var color = Selected ? new Color("5e8476") : Gold ? new Color("b9a16a") : Dark ? new Color("b7ab8b") : new Color("b3a07d");
        DrawRect(edge, color, false, Selected || Gold ? 1.5f : 1f);
        for (var corner = 0; corner < 4; corner++) corner_stroke(corner, color);
        if (Gold) DrawRect(new Rect2(4, 4, Size.X - 8, Size.Y - 8), new Color(.73f, .62f, .38f, .5f), false, 1);
        if (PointerX >= 0)
        {
            var y = PointerAtBottom ? Size.Y - 1 : 1;
            var tip = new Vector2(PointerX, y + (PointerAtBottom ? 12 : -12));
            Vector2[] triangle = [new(PointerX - 11, y), tip, new(PointerX + 11, y)];
            DrawColoredPolygon(triangle, new Color("1e211a"));
            DrawPolyline(triangle, color, 1, true);
        }
    }
    private void corner_stroke(int corner, Color color)
    {
        var x = corner % 2 == 0 ? 1f : Size.X - 1;
        var y = corner < 2 ? 1f : Size.Y - 1;
        var sx = corner % 2 == 0 ? 1f : -1f;
        var sy = corner < 2 ? 1f : -1f;
        Vector2[] points =
        [
            new(x + sx * 3, y + sy * 12), new(x + sx * 3, y + sy * 3),
            new(x + sx * 12, y + sy * 3), new(x + sx * 12, y + sy * 7),
            new(x + sx * 7, y + sy * 7), new(x + sx * 7, y + sy * 12)
        ];
        DrawPolyline(points, color, 1, true);
    }
}
