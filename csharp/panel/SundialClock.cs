using Godot;

/// <summary>月历轮以屏幕角落为圆心，每个游戏月转一圈。</summary>
public partial class SundialClock : Control
{
    public const float Radius = 220;
    private const float PointerAngle = Mathf.Pi / 4;
    private const int DaysPerMonth = 30;
    private const int HoursPerMonth = DaysPerMonth * 24;
    private Texture2D _disc = null!;
    private double _hours = double.NaN;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _disc = GD.Load<Texture2D>("res://textures/ui/time/month_dial_clean_v2.png");
    }

    public void SetTime(double hours)
    {
        if (hours == _hours) return;
        _hours = hours;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_disc is null || double.IsNaN(_hours)) return;
        var rotation = PointerAngle - (float)(_hours % HoursPerMonth / HoursPerMonth * Mathf.Tau);
        DrawSetTransform(Vector2.Zero, rotation);
        DrawTextureRect(_disc, new Rect2(-Radius, -Radius, Radius * 2, Radius * 2), false);
        DrawSetTransform(Vector2.Zero);
        // 外圈只表达月内日期，昼夜继续由盘内文字与针尖颜色表示。
        DrawArc(Vector2.Zero, Radius * .957f, 0, Mathf.Tau, 144, new Color("8b8467"), 2, true);
        var font = GetThemeDefaultFont();
        for (var day = 1; day <= DaysPerMonth; day++)
        {
            var angle = rotation + (day - 1) / (float)DaysPerMonth * Mathf.Tau;
            var direction = Vector2.FromAngle(angle);
            // 每五日标一次，首日仍保留细刻度但不额外显示“1”。
            var major = day % 5 == 0;
            DrawLine(direction * (Radius * .832f), direction * (Radius * (major ? .913f : .875f)), new Color("514d40"), major ? 1.6f : 1, true);
            if (!major) continue;
            var text = day.ToString();
            var center = direction * (Radius * .735f);
            var size = font.GetStringSize(text, HorizontalAlignment.Left, -1, 16);
            DrawString(font, center + new Vector2(-size.X / 2, 6), text, HorizontalAlignment.Left, -1, 16, new Color("393c32"));
        }
        // 指针收在外圈，内圈留给固定读数，避免影子穿过文字。
        var directionToTime = Vector2.FromAngle(PointerAngle);
        var basePoint = directionToTime * (Radius * .57f);
        var tip = directionToTime * (Radius * .67f);
        var daylight = _hours % 24 is >= 6 and < 18;
        DrawColoredPolygon([basePoint + new Vector2(0, 6), tip + new Vector2(4, 5), basePoint + new Vector2(10, 0)], new Color(0.12f, 0.15f, 0.12f, daylight ? .16f : .05f));
        DrawColoredPolygon([basePoint - new Vector2(3, -3), tip, basePoint + new Vector2(3, -3)], new Color("47483c"));
        var marker = new Color(daylight ? "964f3d" : "657f92");
        DrawLine(directionToTime * (Radius * .805f), directionToTime * (Radius * .924f), marker, 2, true);
        DrawCircle(directionToTime * (Radius * .924f), 3, marker, true, -1, true);
        DrawCircle(Vector2.Zero, 9, new Color("49493b"), true, -1, true);
    }
}
