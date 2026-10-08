using Godot;

/// <summary>Long-action full-screen settlement view. The ring advances around the time settlement point for future monthly world simulation.</summary>
public partial class ActionTransitionPanel : Control
{
    private const float RingSize = 304f;
    // 两张圆环素材的透明边界视觉重心略偏右下；统一向左上收 1px，避免进度环看起来偏离文字中心。
    private static readonly Vector2 RingVisualOffset = new(-1f, -1f);
    private ColorRect? _shade;
    private Control? _center;
    private TextureProgressBar? _progress;
    private Label? _title;
    private Label? _state;
    private Label? _detail;
    private bool _playing;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ZIndex = 900;
        MouseFilter = MouseFilterEnum.Stop;
        AddToGroup("action_transition");
        build();
        Hide();
    }

    public void play(string title, string plan, string complete, Action settle, Action? after = null)
    {
        if (_playing || _title is null || _state is null || _detail is null || _shade is null || _center is null || _progress is null) return;
        _playing = true;
        _title.Text = title;
        _state.Text = "时光流转中…";
        _detail.Text = plan;
        _progress.Value = 0;
        _shade.Modulate = new Color(1, 1, 1, 0);
        _center.Modulate = new Color(1, 1, 1, 0);
        _center.Scale = new Vector2(.94f, .94f);
        Show();
        MoveToFront();

        var tween = CreateTween().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.SetParallel();
        tween.TweenProperty(_shade, "modulate:a", 1f, .18f);
        tween.TweenProperty(_center, "modulate:a", 1f, .18f);
        tween.TweenProperty(_center, "scale", Vector2.One, .18f);
        tween.Chain().TweenProperty(_progress, "value", 28d, .34f);
        tween.TweenInterval(.23f);
        tween.TweenCallback(Callable.From(() => _state.Text = "推演行程中…"));
        tween.TweenProperty(_progress, "value", 68d, .45f);
        tween.TweenInterval(.25f);
        tween.TweenCallback(Callable.From(() => _state.Text = "正在结算…"));
        tween.TweenProperty(_progress, "value", 92d, .32f);
        tween.TweenInterval(.18f);
        tween.TweenCallback(Callable.From(() =>
        {
            settle();
            _state.Text = "已完成";
            _detail.Text = complete;
        }));
        tween.TweenProperty(_progress, "value", 100d, .18f);
        tween.TweenInterval(.28f);
        tween.TweenProperty(_shade, "modulate:a", 0f, .18f);
        tween.Parallel().TweenProperty(_center, "modulate:a", 0f, .18f);
        tween.TweenCallback(Callable.From(() => { Hide(); _playing = false; after?.Invoke(); }));
    }

    private void build()
    {
        _shade = new ColorRect { Color = new Color(.105f, .097f, .078f, .985f) };
        _shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_shade);

        _center = new Control { CustomMinimumSize = new Vector2(540, 430) };
        _center.SetAnchorsPreset(LayoutPreset.Center);
        _center.OffsetLeft = -270;
        _center.OffsetTop = -215;
        _center.OffsetRight = 270;
        _center.OffsetBottom = 215;
        AddChild(_center);

        var darkRing = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://textures/ui/qingxi/loading_ring_dark_v1.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        center_control(darkRing, new Vector2(RingSize, RingSize), RingVisualOffset);
        _center.AddChild(darkRing);

        _progress = new TextureProgressBar
        {
            TextureProgress = GD.Load<Texture2D>("res://textures/ui/qingxi/loading_ring_bright_v1.png"),
            FillMode = (int)TextureProgressBar.FillModeEnum.Clockwise,
            RadialInitialAngle = 270f,
            RadialFillDegrees = 360f,
            NinePatchStretch = false,
            MouseFilter = MouseFilterEnum.Ignore,
            MinValue = 0,
            MaxValue = 100,
            Value = 0
        };
        center_control(_progress, new Vector2(RingSize, RingSize), RingVisualOffset);
        _center.AddChild(_progress);

        _title = new Label { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        _title.SetAnchorsPreset(LayoutPreset.Center);
        _title.OffsetLeft = -170;
        _title.OffsetTop = -38;
        _title.OffsetRight = 170;
        _title.OffsetBottom = 8;
        _title.AddThemeFontSizeOverride("font_size", 34);
        _title.AddThemeColorOverride("font_color", new Color(.93f, .88f, .74f, 1));
        _center.AddChild(_title);

        _state = new Label { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        _state.SetAnchorsPreset(LayoutPreset.Center);
        _state.OffsetLeft = -170;
        _state.OffsetTop = 14;
        _state.OffsetRight = 170;
        _state.OffsetBottom = 40;
        _state.AddThemeFontSizeOverride("font_size", 18);
        _state.AddThemeColorOverride("font_color", new Color(.78f, .74f, .65f, 1));
        _center.AddChild(_state);

        _detail = new Label { HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _detail.SetAnchorsPreset(LayoutPreset.Center);
        _detail.OffsetLeft = -245;
        _detail.OffsetTop = 180;
        _detail.OffsetRight = 245;
        _detail.OffsetBottom = 230;
        _detail.AddThemeFontSizeOverride("font_size", 17);
        _detail.AddThemeColorOverride("font_color", new Color(.72f, .68f, .59f, 1));
        _center.AddChild(_detail);
    }

    private static void center_control(Control control, Vector2 size, Vector2 visualOffset)
    {
        control.SetAnchorsPreset(LayoutPreset.Center);
        control.OffsetLeft = -size.X * .5f + visualOffset.X;
        control.OffsetTop = -size.Y * .5f + visualOffset.Y;
        control.OffsetRight = size.X * .5f + visualOffset.X;
        control.OffsetBottom = size.Y * .5f + visualOffset.Y;
    }
}
