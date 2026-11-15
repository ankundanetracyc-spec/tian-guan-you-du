using Godot;

/// <summary>普通透明混合的淡墨旋流；仅为投料呈现，不驱动炼制计算。</summary>
public partial class AlchemyInkVortex : Control
{
    public bool Dissipating;
    private const int Strands = 5, Samples = 48;
    private readonly Vector2[][] _points = new Vector2[Strands][];
    private readonly Color[][] _colors = new Color[Strands][];
    private float _time, _opacity;
    public override void _Ready()
    {
        for (int i = 0; i < Strands; i++)
        {
            _points[i] = new Vector2[Samples];
            _colors[i] = new Color[Samples];
        }
    }
    public override void _Process(double delta)
    {
        _time += (float)delta;
        _opacity = Mathf.MoveToward(_opacity, Dissipating ? 0 : 1, (float)delta * 2);
        QueueRedraw();
        if (Dissipating && _opacity <= 0) SetProcess(false);
    }
    public override void _Draw()
    {
        for (int strand = 0; strand < Strands; strand++)
        {
            var points = _points[strand];
            var colors = _colors[strand];
            for (int j = 0; j < points.Length; j++)
            {
                float t = j / (Samples - 1f);
                float a = t * Mathf.Tau * .95f + strand * Mathf.Tau / Strands - _time * 1.9f;
                float r = t * (142 + Mathf.Sin(strand * 2.3f) * 12);
                points[j] = new Vector2(Mathf.Cos(a) * r,
                    Mathf.Sin(a) * r * .30f - Mathf.Sin(t * Mathf.Pi) * 18);
                float taper = Mathf.Pow(Mathf.Max(0, Mathf.Sin(t * Mathf.Pi)), 1.3f);
                float grain = .72f + .28f * Mathf.Sin(t * 23 + strand * 2 + _time * .6f);
                colors[j] = new Color(.66f, .68f, .57f, taper * grain * _opacity * .14f);
            }
            DrawPolyline(points, new Color(.51f, .55f, .46f, .022f * _opacity), 5, true);
            DrawPolylineColors(points, colors, 1.2f + strand * .16f, true);
        }
    }
}
