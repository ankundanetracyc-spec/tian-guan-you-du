using Godot;

/// <summary>极效的淡晕和满效的水墨丹霞，普通透明混合。</summary>
public partial class AlchemyPillAura : Control
{
    public bool Full;
    private float _time;
    public override void _Process(double delta) { _time += (float)delta; QueueRedraw(); }
    public override void _Draw()
    {
        var center = Size / 2;
        if (!Full)
        {
            for (int i = 0; i < 5; i++) DrawArc(center, 34 + i * 2, 0, Mathf.Tau, 64, new Color(.76f, .72f, .56f, .055f - i * .009f), 3, true);
            return;
        }
        for (int strand = 0; strand < 3; strand++)
        {
            var points = new Vector2[40];
            for (int i = 0; i < points.Length; i++)
            {
                float t = i / 39f, a = t * Mathf.Tau + strand * 1.6f + _time * .25f;
                points[i] = center + new Vector2(Mathf.Cos(a) * (39 + Mathf.Sin(t * 9 + _time) * 5), Mathf.Sin(a) * 23 - 8);
            }
            DrawPolyline(points, strand == 0 ? new Color(.7f,.47f,.46f,.20f) : new Color(.58f,.57f,.73f,.16f), 5, true);
        }
    }
}
