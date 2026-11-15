using Godot;

/// <summary>Small ink traces that make logical board steps and forced displacement readable.</summary>
public partial class BattleBoardMovementEffect : Node2D
{
    private Vector2 _travel;
    private bool _forced;
    private float _elapsed;
    private float _duration;

    public static void Play(Node2D unit, Vector2 from, Vector2 to, bool forced)
    {
        if (from.DistanceSquaredTo(to) < 4f || !GodotObject.IsInstanceValid(unit)) return;
        var scene = unit.GetTree()?.CurrentScene;
        if (scene is null) return;
        var effect = new BattleBoardMovementEffect
        {
            Name = forced ? "BoardKnockbackTrace" : "BoardStepTrace",
            _travel = to - from,
            _forced = forced,
            _duration = forced ? .32f : .42f,
            ZAsRelative = false,
            ZIndex = unit.ZIndex - 1
        };
        (unit.GetParent() ?? scene).AddChild(effect);
        effect.GlobalPosition = from;
    }

    public override void _Process(double delta)
    {
        _elapsed += (float)delta;
        QueueRedraw();
        if (_elapsed >= _duration) QueueFree();
    }

    public override void _Draw()
    {
        var length = _travel.Length();
        if (length < 2f) return;
        var direction = _travel / length;
        var normal = direction.Orthogonal();
        var progress = Mathf.Clamp(_elapsed / Mathf.Max(.001f, _duration), 0f, 1f);
        var head = _travel * progress;
        var tailLength = Mathf.Min(length * .82f, _forced ? 126f : 78f);
        var tail = head - direction * tailLength;
        var fade = (1f - Mathf.SmoothStep(.45f, 1f, progress)) * (_forced ? .72f : .46f);
        var ink = new Color(.075f, .095f, .083f, fade);

        DrawLine(tail + normal * (_forced ? 10f : 6f), head, ink, _forced ? 6f : 4f, true);
        DrawLine(tail + normal * (_forced ? -5f : -3f), head - direction * tailLength * .22f,
            new Color(ink.R, ink.G, ink.B, fade * .72f), _forced ? 3f : 2f, true);

        var landing = Mathf.SmoothStep(.55f, .9f, progress) * (1f - Mathf.SmoothStep(.9f, 1f, progress));
        if (landing > 0f)
        {
            var dust = head;
            DrawCircle(dust + normal * 9f, _forced ? 8f : 5f, new Color(.16f, .18f, .15f, landing * (_forced ? .38f : .26f)));
            DrawCircle(dust - normal * 7f, _forced ? 5f : 3.5f, new Color(.16f, .18f, .15f, landing * (_forced ? .34f : .22f)));
        }
    }
}
