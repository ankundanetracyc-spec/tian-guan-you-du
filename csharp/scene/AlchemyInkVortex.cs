using Godot;

/// <summary>普通透明混合的淡墨旋流；仅为投料呈现，不驱动炼制计算。</summary>
public partial class AlchemyInkVortex : Control
{
    public bool Dissipating;
    private float _time, _opacity;
    public override void _Process(double delta)
    {
        _time += (float)delta;
        _opacity = Mathf.MoveToward(_opacity, Dissipating ? 0 : 1, (float)delta * 2);
        QueueRedraw();
    }
    public override void _Draw()
    {
        for (int strand = 0; strand < 7; strand++)
        {
            var points = new Vector2[64];
            for (int j = 0; j < points.Length; j++)
            {
                float t = j / 63f, a = t * Mathf.Tau * 1.3f + strand * Mathf.Tau / 7 - _time * 2.6f;
                float r = t * 175;
                points[j] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * .38f - t * 28);
            }
            DrawPolyline(points, new Color(.65f, .69f, .57f, .17f * _opacity), 1.6f, true);
        }
    }
}
