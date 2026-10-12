using Godot;
using System.Linq;

/// <summary>八回合圆牌轨道：独立透明素材、呼吸高亮、回弹到达提示。</summary>
public partial class CondensationTurnTrack : Control
{
    private readonly Label[] _numbers = new Label[8];
    private readonly Label[] _recoils = new Label[8];
    private Label _caption = null!, _future = null!;
    private int _round = 1;
    private double _time;
    private bool _finished;
    private readonly int[] _strengths = new int[8];
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _caption = Line(new Rect2(0, 0, 800, 32), 22);
        _future = Line(new Rect2(0, 124, 800, 27), 17);
        var numerals = new[] { "一", "二", "三", "四", "五", "六", "七", "八" };
        for (var i = 0; i < 8; i++)
        {
            var x = 38 + i * 103;
            _numbers[i] = Line(new Rect2(x - 23, 45, 46, 46), 23); _numbers[i].Text = numerals[i];
            _recoils[i] = Line(new Rect2(x - 48, 99, 96, 25), 16);
        }
        SetState(null);
    }
    private Label Line(Rect2 rect, int fontSize)
    {
        var label = new Label { MouseFilter = MouseFilterEnum.Ignore, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", fontSize); label.AddThemeColorOverride("font_color", new Color("d3bd91"));
        AddChild(label); label.Position = rect.Position; label.Size = rect.Size; return label;
    }
    public void SetState(CondensationCardGame? game)
    {
        _round = game?.Round ?? 1; _finished = game?.Finished ?? false;
        _caption.Text = $"第{new[] { "一", "二", "三", "四", "五", "六", "七", "八" }[_round - 1]}回合 / 八回合";
        for (var i = 0; i < 8; i++)
        {
            _strengths[i] = game?.Recoils.Where(r => r.Round == i + 1).Sum(r => r.Strength) ?? 0;
            _recoils[i].Text = _strengths[i] > 0 ? $"回弹 {_strengths[i]}" : "";
            _numbers[i].Modulate = i + 1 == _round ? new Color("fff0c7") : new Color(1, 1, 1, i + 1 < _round ? .45f : .8f);
        }
        var future = game?.Recoils.Where(r => r.Round > 8).Sum(r => r.Strength) ?? 0;
        _future.Text = future > 0 ? $"第八回合后仍有回弹 {future} · 定形前须结清" : "";
        QueueRedraw();
    }
    public override void _Process(double delta)
    {
        if (!IsVisibleInTree()) return;
        _time += delta; QueueRedraw();
    }
    public override void _Draw()
    {
        var pulse = .5f + .5f * Mathf.Sin((float)_time * 1.65f);
        var activeX = 38 + (_round - 1) * 103;
        DrawLine(new Vector2(38, 68), new Vector2(759, 68), new Color(.67f, .57f, .39f, .55f), 1, true);
        if (!_finished)
        {
            for (var i = 4; i >= 1; i--) DrawCircle(new Vector2(activeX, 68), 24 + i * 2 + pulse * 2, new Color(.94f, .79f, .45f, .012f + pulse * .009f));
        }
        for (var i = 0; i < 8; i++)
        {
            var x = 38 + i * 103; var active = i + 1 == _round;
            var zoom = active && !_finished ? 1f + pulse * .025f : 1f;
            var half = 23 * zoom;
            DrawTextureRect(CondensationUiArt.Turn, new Rect2(x - half, 68 - half, half * 2, half * 2), false,
                active ? new Color(1.16f, 1.1f, .9f) : new Color(1, 1, 1, i + 1 < _round ? .55f : .85f));
            if (active) DrawArc(new Vector2(x, 68), 24, 0, Mathf.Tau, 64, new Color(1f, .88f, .59f, .5f + pulse * .25f), 1.2f, true);
            if (_strengths[i] > 0)
            {
                var y = 95 + Mathf.Sin((float)_time * 1.3f + i) * 1.2f;
                DrawArc(new Vector2(x, y), 5, .1f, 5.5f, 24, new Color(.92f, .79f, .49f, .65f + pulse * .22f), 1.2f, true);
            }
        }
    }
}
