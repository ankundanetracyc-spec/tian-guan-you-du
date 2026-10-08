using Godot;

/// <summary>道承的静态水墨树脉，节点由 InfoPanel 叠在其上。</summary>
public partial class DaoBearingFlow : Control
{
    /// <summary>0 为尚未定道；1/2/3 分别高亮锻躯、行炁、蕴神分支。</summary>
    public int Bearing { get; set; }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Draw()
    {
        var ink = new Color(.18f, .15f, .10f, .58f);
        var faded = new Color(.24f, .21f, .16f, .22f);
        var active = new Color(.48f, .32f, .09f, .84f);
        var root = new Vector2(Size.X * .5f, Size.Y * .18f);
        var body = new Vector2(Size.X * .18f, Size.Y * .74f);
        var soul = new Vector2(Size.X * .5f, Size.Y * .74f);
        var qi = new Vector2(Size.X * .82f, Size.Y * .74f);
        DrawCircle(root, 10, new Color(.31f, .22f, .09f, .8f));
        DrawRoute(root, body, Bearing == 1 ? active : faded, Bearing == 1 ? 3.2f : 2.2f);
        DrawRoute(root, soul, Bearing == 3 ? active : faded, Bearing == 3 ? 3.2f : 2.2f);
        DrawRoute(root, qi, Bearing == 2 ? active : faded, Bearing == 2 ? 3.2f : 2.2f);
    }

    private void DrawRoute(Vector2 from, Vector2 to, Color color, float width)
    {
        DrawLine(from, to, new Color(color, color.A * .35f), width + 5, true);
        DrawLine(from, to, color, width, true);
        var direction = (to - from).Normalized();
        var left = direction.Rotated(Mathf.Pi * .78f) * 12;
        var right = direction.Rotated(-Mathf.Pi * .78f) * 12;
        DrawLine(to, to + left, color, width, true);
        DrawLine(to, to + right, color, width, true);
    }
}
