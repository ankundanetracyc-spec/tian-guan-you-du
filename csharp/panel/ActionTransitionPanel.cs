using Godot;

/// <summary>Long-action full-screen settlement view. The ring advances around the time settlement point for future monthly world simulation.</summary>
public partial class ActionTransitionPanel : Control
{
    private const float RingSize = 304f;
    // 环形素材的中心孔与正方形画布中心基本重合，不再额外偏移。
    private static readonly Vector2 RingVisualOffset = Vector2.Zero;
    private ColorRect? _shade;
    private Control? _center;
    private TextureProgressBar? _progress;
    private Label? _title;
    private Label? _state;
    private Label? _detail;
    private bool _playing;
    public bool IsPlaying => _playing;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ZIndex = 900;
        MouseFilter = MouseFilterEnum.Stop;
        ProcessMode = ProcessModeEnum.Always;
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
        tween.TweenCallback(Callable.From(() => settle_and_finish(settle, complete, after)));
    }

    private async void settle_and_finish(Action settle, string complete, Action? after)
    {
        var previousPause = GetTree().Paused;
        var manager = GetNodeOrNull<NpcWorldManager>("/root/NpcWorldManager");
        var deferred = false;
        var success = false;
        try
        {
            // Prevent scene actions, Esc and autosave from observing a half-settled month.
            GetTree().Paused = true;
            if (manager is not null) { manager.defer_time_settlement(); deferred = true; }
            settle();
            if (manager is not null)
                await manager.flush_time_settlement_async((ratio, state) => { _progress!.Value = 92 + ratio * 8; _state!.Text = state; });
            _state!.Text = "已完成"; _detail!.Text = complete; _progress!.Value = 100;
            await ToSignal(GetTree().CreateTimer(.28, processAlways: true), SceneTreeTimer.SignalName.Timeout);
            success = true;
        }
        catch (Exception ex)
        {
            GD.PushError($"长行动结算失败：{ex}");
            // If the player action failed after changing the calendar, still reconcile that elapsed time.
            if (deferred && manager?.HasPendingSettlement == true) await manager.flush_time_settlement_async();
        }
        finally
        {
            GetTree().Paused = previousPause;
            Hide(); _playing = false;
        }
        if (success) after?.Invoke();
    }

    // The same view for actual computation, with no artificial multi-second animation.
    public async System.Threading.Tasks.Task run_work_async(string title, Func<Action<double, string>, System.Threading.Tasks.Task> work)
    {
        if (_playing) throw new InvalidOperationException("加载界面正在处理另一项行动");
        _playing = true;
        var previousPause = GetTree().Paused;
        try
        {
            _title!.Text = title; _state!.Text = "正在准备…"; _detail!.Text = "人物、行旅与行动记录结算完成后继续。";
            _progress!.Value = 0; _shade!.Modulate = _center!.Modulate = Colors.White; _center.Scale = Vector2.One;
            Show(); MoveToFront(); GetTree().Paused = true;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await work((ratio, state) => { _progress.Value = Math.Clamp(ratio, 0, 1) * 100; _state.Text = state; });
        }
        finally
        {
            Hide(); _playing = false; GetTree().Paused = previousPause;
        }
    }

    private void build()
    {
        _shade = new ColorRect { Color = new Color(.105f, .097f, .078f, .985f) };
        _shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_shade);

        _center = new Control { CustomMinimumSize = new Vector2(540, 430), PivotOffset = new Vector2(270, 215) };
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
