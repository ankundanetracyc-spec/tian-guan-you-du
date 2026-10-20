using Godot;
using System;

/// <summary>百草卷横轴固定在下沿，布面从卷轴处向上延展；药材内容在布面铺定后显现。</summary>
public partial class AlchemyHerbClothPanel : Control
{
    private readonly Control _revealClip = new() { ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
    private readonly AlchemyHerbClothSurface _surface = new() { MouseFilter = MouseFilterEnum.Ignore };
    private readonly Control _contentRoot = new() { Name = "ClothContents", MouseFilter = MouseFilterEnum.Pass, Visible = false };
    private readonly AlchemyClothRevealEdge _leadingEdge = new() { MouseFilter = MouseFilterEnum.Ignore, ZIndex = 2 };
    private Tween? _motion;
    private float _revealProgress;
    private bool _open;

    public bool IsOpen => _open;
    public Control ContentRoot => _contentRoot;

    public AlchemyHerbClothPanel()
    {
        ClipContents = true;
        MouseFilter = MouseFilterEnum.Pass;
        CustomMinimumSize = Vector2.Zero;

        AddChild(_revealClip);
        _revealClip.AddChild(_surface);
        AddChild(_contentRoot);
        _contentRoot.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_leadingEdge);
        Resized += ApplyRevealProgress;
    }

    public override void _Ready() => ApplyRevealProgress();

    public void Toggle() => SetOpen(!_open);

    public void SetOpen(bool open)
    {
        if (_open == open && open == Visible) return;
        _open = open;

        if (!IsInsideTree())
        {
            Visible = open;
            SetRevealProgress(open ? 1f : 0f);
            _contentRoot.Visible = open;
            return;
        }

        if (_motion?.IsRunning() == true) _motion.Kill();

        if (open)
        {
            Visible = true;
            _contentRoot.Visible = false;
            _contentRoot.Modulate = Colors.White;
            _motion = CreateTween();
            _motion.TweenMethod(Callable.From<float>(SetRevealProgress), _revealProgress, 1f, .42)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            _motion.TweenCallback(Callable.From(ShowContents));
            _motion.TweenProperty(_contentRoot, "modulate", Colors.White, .14)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        }
        else
        {
            _contentRoot.Visible = false;
            _motion = CreateTween();
            _motion.TweenMethod(Callable.From<float>(SetRevealProgress), _revealProgress, 0f, .27)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
            _motion.TweenCallback(Callable.From(() =>
            {
                if (!_open) Visible = false;
            }));
        }
    }

    private void ShowContents()
    {
        if (!_open) return;
        _contentRoot.Modulate = new Color(1, 1, 1, 0);
        _contentRoot.Visible = true;
    }

    private void SetRevealProgress(float progress)
    {
        _revealProgress = Mathf.Clamp(progress, 0, 1);
        ApplyRevealProgress();
    }

    private void ApplyRevealProgress()
    {
        float width = Size.X;
        float height = Size.Y;
        float top = height * (1f - _revealProgress);
        _revealClip.Position = new Vector2(0, top);
        _revealClip.Size = new Vector2(width, height - top);
        _surface.Position = new Vector2(0, -top);
        _surface.Size = new Vector2(width, height);
        _leadingEdge.Position = new Vector2(0, top - 2);
        _leadingEdge.Size = new Vector2(width, 5);
        _leadingEdge.Visible = _revealProgress > .015f && _revealProgress < .995f;
        _leadingEdge.QueueRedraw();
    }
}

/// <summary>百草卷的淡彩布面，整幅绘制后由下沿向上的裁切显示。</summary>
public partial class AlchemyHerbClothSurface : Control
{
    public override void _Draw()
    {
        float w = Size.X;
        float h = Size.Y;
        if (w < 30 || h < 40) return;

        Vector2[] cloth =
        [
            new(10, 9), new(w - 10, 9),
            new(w - 8, h * .20f), new(w - 11, h * .43f),
            new(w - 8, h * .70f), new(w - 12, h - 15),
            new(w * .78f, h - 11), new(w * .52f, h - 14),
            new(w * .27f, h - 10), new(12, h - 15),
            new(8, h * .73f), new(11, h * .46f), new(8, h * .19f)
        ];
        DrawColoredPolygon(cloth, new Color("#d1d1c5"));

        var warp = new Color(.20f, .27f, .22f, .050f);
        var weft = new Color(.82f, .82f, .76f, .10f);
        for (float y = 19; y < h - 12; y += 15)
        {
            float inset = 13 + Mathf.Sin(y * .025f) * 2.2f;
            DrawLine(new Vector2(inset, y), new Vector2(w - inset, y + 1.3f), weft, 1, true);
        }
        for (float x = 19; x < w - 12; x += 17)
        {
            float bend = Mathf.Sin(x * .043f) * 2.2f;
            DrawLine(new Vector2(x, 15), new Vector2(x + bend, h - 15), warp, 1, true);
        }

        var jade = new Color("#657666");
        DrawPolyline([new(14, 12), new(w * .28f, 14), new(w * .53f, 11), new(w * .78f, 14), new(w - 14, 12)],
            new Color(.43f, .48f, .42f, .52f), 1.2f, true);

        Vector2[] leftHem = [new(10, 18), new(12, h * .23f), new(9, h * .49f), new(12, h * .75f), new(10, h - 17)];
        Vector2[] rightHem = [new(w - 10, 18), new(w - 12, h * .24f), new(w - 9, h * .48f), new(w - 12, h * .73f), new(w - 10, h - 17)];
        DrawPolyline(leftHem, new Color(.29f, .36f, .29f, .48f), 1.2f, true);
        DrawPolyline(rightHem, new Color(.29f, .36f, .29f, .48f), 1.2f, true);
        for (float y = 32; y < h - 24; y += 19)
        {
            DrawLine(new Vector2(12, y), new Vector2(16, y + 2), new Color(.48f, .51f, .45f, .55f), 1, true);
            DrawLine(new Vector2(w - 16, y + 2), new Vector2(w - 12, y), new Color(.48f, .51f, .45f, .55f), 1, true);
        }

        Vector2[] foldLeft = [new(w * .12f, h * .14f), new(w * .18f, h * .34f), new(w * .15f, h * .58f), new(w * .20f, h * .82f)];
        Vector2[] foldRight = [new(w * .88f, h * .17f), new(w * .83f, h * .39f), new(w * .87f, h * .61f), new(w * .81f, h * .84f)];
        DrawPolyline(foldLeft, new Color(.26f, .32f, .26f, .075f), 2, true);
        DrawPolyline(foldRight, new Color(.26f, .32f, .26f, .075f), 2, true);

        DrawLine(new Vector2(16, h - 17), new Vector2(w - 16, h - 17), jade, 13, true);
        DrawLine(new Vector2(19, h - 23), new Vector2(w - 19, h - 23), new Color("#deded2"), 1.2f, true);
        DrawLine(new Vector2(19, h - 10), new Vector2(w - 19, h - 10), new Color(.31f, .37f, .31f, .52f), 1, true);
    }
}

/// <summary>随布面向上移动的一道柔和卷边，强调布料正在展开。</summary>
public partial class AlchemyClothRevealEdge : Control
{
    public override void _Draw()
    {
        if (Size.X < 30) return;
        DrawLine(new Vector2(14, 1), new Vector2(Size.X - 14, 1), new Color(.31f, .38f, .31f, .45f), 1.5f, true);
        DrawLine(new Vector2(16, 3), new Vector2(Size.X - 16, 3), new Color(.91f, .90f, .83f, .52f), 1, true);
    }
}

/// <summary>把原生滚动位置映射成布边的一枚细线签，可拖动或点击定位。</summary>
public partial class AlchemyClothScrollMarker : Control
{
    public ScrollContainer? Scroll { get; set; }
    private float _position;
    private float _target;
    private bool _dragging;

    public AlchemyClothScrollMarker()
    {
        MouseFilter = MouseFilterEnum.Stop;
        SetProcess(true);
    }

    public override void _Ready()
    {
        if (Scroll is null) return;
        Scroll.GetVScrollBar().ValueChanged += _ => UpdateTarget();
        UpdateTarget();
    }

    private void UpdateTarget()
    {
        if (Scroll is null) return;
        var bar = Scroll.GetVScrollBar();
        float range = (float)(bar.MaxValue - bar.Page);
        _target = range <= 1 ? 0 : Mathf.Clamp((float)bar.Value / range, 0, 1);
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        if (Scroll is null) return;
        var bar = Scroll.GetVScrollBar();
        if (inputEvent is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } pressed)
        {
            _dragging = true;
            SetFromPointer(pressed.Position.Y, bar);
            AcceptEvent();
        }
        else if (inputEvent is InputEventMouseMotion motion && _dragging)
        {
            SetFromPointer(motion.Position.Y, bar);
            AcceptEvent();
        }
        else if (inputEvent is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
        {
            _dragging = false;
            AcceptEvent();
        }
        else if (inputEvent is InputEventMouseButton wheel && wheel.Pressed &&
                 wheel.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
        {
            bar.Value += wheel.ButtonIndex == MouseButton.WheelUp ? -bar.Page * .18 : bar.Page * .18;
            AcceptEvent();
        }
    }

    private void SetFromPointer(float pointerY, ScrollBar bar)
    {
        float top = 9;
        float bottom = Size.Y - 9;
        float total = (float)bar.MaxValue;
        float range = total - (float)bar.Page;
        if (range <= 1) return;
        float thumbHeight = Mathf.Clamp((bottom - top) * (float)bar.Page / Mathf.Max(1, total), 25, (bottom - top) * .46f);
        float usable = bottom - top - thumbHeight;
        if (usable <= 1) return;
        _target = Mathf.Clamp((pointerY - top - thumbHeight * .5f) / usable, 0, 1);
        bar.Value = _target * range;
    }

    public override void _Process(double delta)
    {
        if (Scroll is null) return;
        float old = _position;
        _position = Mathf.Lerp(_position, _target, 1 - Mathf.Exp((float)delta * -13));
        if (Mathf.Abs(old - _position) > .001f) QueueRedraw();
    }

    public override void _Draw()
    {
        float h = Size.Y;
        if (h < 30 || Scroll is null) return;
        var bar = Scroll.GetVScrollBar();
        float total = (float)bar.MaxValue;
        float range = total - (float)bar.Page;
        if (range <= 1) return;

        float x = Size.X * .5f;
        float top = 9;
        float bottom = h - 9;
        DrawLine(new Vector2(x, top), new Vector2(x, bottom), new Color(.25f, .31f, .25f, .20f), 1, true);

        float thumbHeight = Mathf.Clamp((bottom - top) * (float)bar.Page / Mathf.Max(1, total), 25, (bottom - top) * .46f);
        float y = top + (bottom - top - thumbHeight) * _position;
        var ink = new Color("#657666");
        DrawLine(new Vector2(x, y + 4), new Vector2(x, y + thumbHeight - 4), ink, 2, true);
        DrawCircle(new Vector2(x, y + thumbHeight * .5f), 3.5f, new Color("#879083"));
        DrawLine(new Vector2(x - 3, y + thumbHeight * .5f), new Vector2(x + 3, y + thumbHeight * .5f), new Color("#d8d8cb"), 1, true);
    }
}
